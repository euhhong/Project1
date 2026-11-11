<#
Unity editor access for every Claude session in this project (BE / FE / PM / main).
Run from anywhere:  powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Unity\unity.ps1 <command> [args]

Commands
  status                 Connection state.
  console [-All]         Console errors/warnings (-All adds normal logs). Prints errorCount/warningCount first.
  compile                Refresh assets, wait for Unity to recompile, then print compile result + console errors.
  run <file.cs>          Run a C# editor command (class must be `internal class CommandScript : IRunCommand`).
  capture [-Out <png>]   Multi-angle Scene View capture, saved as PNG (path printed).
  tool <name> [json]     Call any Unity MCP tool directly (e.g. tool Unity_Camera_Capture '{"cameraInstanceID":123}').

Notes
  * One shared connection (Tools/Unity/unity-daemon.ps1) is started on demand and shared by all sessions.
  * The first connection after Unity starts may need "Accept" in Unity: Edit > Project Settings > AI > Unity MCP.
  * Unity is briefly brought to the front for compile/run so the editor does not throttle; pass -NoFocus to skip.
  * Never kill relay_win.exe processes by name: Unity runs its own relay and killing it logs console errors.
#>
param(
  [Parameter(Position = 0)] [string] $Command = 'status',
  [Parameter(Position = 1)] [string] $Arg1,
  [Parameter(Position = 2)] [string] $Arg2,
  [string] $Out,
  [switch] $All,
  [switch] $NoFocus,
  [int] $TimeoutSec = 180
)
$ErrorActionPreference = 'Stop'
$Project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Queue = Join-Path $Project 'Library\ClaudeUnity\queue'
$Daemon = Join-Path $PSScriptRoot 'unity-daemon.ps1'
$utf8 = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Force $Queue | Out-Null

function Get-Status {
  $f = Join-Path $Queue 'daemon.status'
  if (Test-Path $f) { return (Get-Content $f -Raw).Trim() } else { return 'not-started' }
}

function Ensure-Daemon {
  $pidFile = Join-Path $Queue 'daemon.pid'
  if (Test-Path $pidFile) {
    $dpid = [int](Get-Content $pidFile -Raw)
    if (Get-Process -Id $dpid -ErrorAction SilentlyContinue) { return }
  }
  Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$Daemon`"",
    '-Queue', "`"$Queue`"", '-ProjectPath', "`"$Project`"") | Out-Null
  $deadline = (Get-Date).AddSeconds(100)
  while ((Get-Date) -lt $deadline) {
    if ((Get-Status) -like 'ready*') { return }
    Start-Sleep -Milliseconds 500
  }
  Write-Warning "Unity connection not ready ($(Get-Status)). Is the editor open? A first-time 'Accept' may be pending in Edit > Project Settings > AI > Unity MCP."
}

function Focus-Unity {
  if ($NoFocus) { return }
  $u = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*$(Split-Path $Project -Leaf)*" } | Select-Object -First 1
  if ($u) { try { (New-Object -ComObject WScript.Shell).AppActivate($u.Id) | Out-Null } catch {} }
}

function Invoke-Tool([string]$name, [string]$argsJson = '{}', [int]$timeout = $TimeoutSec) {
  Ensure-Daemon
  $id = "{0}_{1}__t{2}" -f (Get-Date -Format 'HHmmssfff'), $PID, $timeout
  $params = '{"name":"' + $name + '","arguments":' + $argsJson + '}'
  $tmp = Join-Path $Queue "$id.tmp"
  [IO.File]::WriteAllText($tmp, $params, $utf8)
  Rename-Item $tmp "$id.json"
  $outFile = Join-Path $Queue "$id.out"
  $deadline = (Get-Date).AddSeconds($timeout + 30)
  while (-not (Test-Path $outFile)) {
    if ((Get-Date) -gt $deadline) { throw "timeout waiting for Unity ($(Get-Status))" }
    Start-Sleep -Milliseconds 250
  }
  Start-Sleep -Milliseconds 50
  $raw = [IO.File]::ReadAllText($outFile); Remove-Item $outFile -ErrorAction SilentlyContinue
  $j = $raw | ConvertFrom-Json
  if ($j.error) { throw ("Unity error: " + $j.error.message) }
  return $j.result
}

function Get-Payload($result) {
  $t = ($result.content | Where-Object { $_.type -eq 'text' } | Select-Object -First 1).text
  try { return ($t | ConvertFrom-Json) } catch { return $t }
}

function Show-Console([switch]$IncludeLogs) {
  $types = if ($IncludeLogs) { 'error,warning,exception,assert,log' } else { 'error,warning,exception,assert' }
  $p = Get-Payload (Invoke-Tool 'Unity_GetConsoleLogs' ('{"maxEntries":50,"includeStackTrace":false,"logTypes":"' + $types + '"}') 60)
  "errors=$($p.data.errorCount) warnings=$($p.data.warningCount)"
  foreach ($l in $p.data.logs) {
    $first = ($l.message -split "`n")[0]
    "[$($l.type)] $first"
  }
}

function Invoke-Code([string]$code, [string]$title, [int]$timeout = $TimeoutSec) {
  Focus-Unity
  $argsJson = ConvertTo-Json -InputObject @{ Code = [string]$code; Title = $title } -Compress
  $p = Get-Payload (Invoke-Tool 'Unity_RunCommand' $argsJson $timeout)
  if ($p.data) {
    "compile=$($p.data.isCompilationSuccessful) exec=$($p.data.isExecutionSuccessful)"
    if ($p.data.compilationLogs) { $p.data.compilationLogs }
    if ($p.data.executionLogs) { $p.data.executionLogs }
  } else { $p | ConvertTo-Json -Depth 6 }
}

switch ($Command) {
  'status' { Ensure-Daemon; Get-Status }
  'console' { Show-Console -IncludeLogs:$All }
  'compile' {
    $code = @'
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        result.Log("refresh requested");
    }
}
'@
    try { Invoke-Code $code 'Refresh & compile' 60 | Out-Null } catch { }
    # A recompile reloads Unity's domain and drops the connection; wait for it to come back.
    Start-Sleep -Seconds 3
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $state = $null
    while ((Get-Date) -lt $deadline) {
      Focus-Unity
      try {
        $state = Invoke-Code @'
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("isCompiling={0} compileFailed={1}", EditorApplication.isCompiling, EditorUtility.scriptCompilationFailed);
    }
}
'@ 'Compile status' 60
        if (($state -join "`n") -match 'isCompiling=\[False\]') { break }
      } catch { }
      Start-Sleep -Seconds 3
    }
    $state | Where-Object { $_ -match 'isCompiling|error|Error' }
    Show-Console
  }
  'run' {
    if (-not $Arg1) { throw 'usage: run <file.cs>' }
    Invoke-Code ([IO.File]::ReadAllText((Resolve-Path $Arg1))) ([IO.Path]::GetFileNameWithoutExtension($Arg1))
  }
  'capture' {
    $r = Invoke-Tool 'Unity_SceneView_CaptureMultiAngleSceneView' '{}' 120
    $img = $r.content | Where-Object { $_.type -eq 'image' } | Select-Object -First 1
    if (-not $img) { throw 'no image returned' }
    if (-not $Out) { $Out = Join-Path $Project ("Library\ClaudeUnity\capture_{0}.png" -f (Get-Date -Format 'HHmmss')) }
    [IO.File]::WriteAllBytes($Out, [Convert]::FromBase64String($img.data))
    $Out
  }
  'tool' {
    if (-not $Arg1) { throw 'usage: tool <name> [json]' }
    $a = if ($Arg2) { $Arg2 } else { '{}' }
    $r = Invoke-Tool $Arg1 $a
    foreach ($c in $r.content) {
      if ($c.type -eq 'image') {
        $f = Join-Path $Project ("Library\ClaudeUnity\{0}_{1}.png" -f $Arg1, (Get-Date -Format 'HHmmssfff'))
        [IO.File]::WriteAllBytes($f, [Convert]::FromBase64String($c.data)); "IMAGE $f"
      } else { $c.text }
    }
  }
  default { throw "unknown command '$Command' (status|console|compile|run|capture|tool)" }
}
