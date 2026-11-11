# Shared Unity MCP connection for every Claude session working in this project.
# Started automatically by unity.ps1; you never need to run this directly.
# It keeps ONE relay connection to the Unity editor and serves requests from a queue folder:
#   <Queue>\<id>.json  = MCP tools/call params: {"name": "<tool>", "arguments": {...}}
#   <Queue>\<id>.out   = the JSON-RPC response (or {"error": ...})
# If Unity reloads its domain (script compile) the relay drops; the daemon reconnects by itself.
param(
  [Parameter(Mandatory)] [string] $Queue,
  [Parameter(Mandatory)] [string] $ProjectPath,
  [int] $RequestTimeoutSec = 180,
  [int] $IdleExitMinutes = 120
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Force $Queue | Out-Null
$statusFile = Join-Path $Queue 'daemon.status'
$pidFile = Join-Path $Queue 'daemon.pid'
[IO.File]::WriteAllText($pidFile, "$PID", $utf8)

function Set-Status([string]$s) { [IO.File]::WriteAllText($statusFile, "$s $(Get-Date -Format o)", $utf8) }

$script:p = $null; $script:stdin = $null; $script:pendingRead = $null; $script:nextId = 1

function Stop-Relay {
  try { if ($script:p -and -not $script:p.HasExited) { $script:p.Kill() } } catch {}
  $script:p = $null; $script:pendingRead = $null
}

# Returns the next stdout line, or $null on timeout / relay exit.
function Read-Line([datetime]$deadline) {
  while ((Get-Date) -lt $deadline) {
    if ($null -eq $script:pendingRead) { $script:pendingRead = $script:p.StandardOutput.ReadLineAsync() }
    if ($script:pendingRead.Wait(200)) {
      $line = $script:pendingRead.Result; $script:pendingRead = $null
      return $line   # $null means the relay closed stdout
    }
    if ($script:p.HasExited) { return $null }
  }
  return $null
}

function Wait-Response([int]$id, [int]$timeoutSec) {
  $deadline = (Get-Date).AddSeconds($timeoutSec)
  while ((Get-Date) -lt $deadline) {
    $line = Read-Line $deadline
    if ($null -eq $line) { return $null }
    if ($line -match ('"id":\s*' + $id + '\s*[,}]')) { return $line }
  }
  return $null
}

function Start-Relay {
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = Join-Path $env:USERPROFILE '.unity\relay\relay_win.exe'
  $psi.Arguments = "--mcp --project-path `"$ProjectPath`""
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true
  $psi.RedirectStandardInput = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $false
  $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
  $script:p = [System.Diagnostics.Process]::Start($psi)
  $script:stdin = New-Object System.IO.StreamWriter($script:p.StandardInput.BaseStream, $utf8)
  $script:stdin.AutoFlush = $true
  $id = $script:nextId++
  $script:stdin.WriteLine('{"jsonrpc":"2.0","id":' + $id + ',"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"claude-code","version":"2.1.317"}}}')
  if (-not (Wait-Response $id 90)) { Stop-Relay; return $false }
  $script:stdin.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
  return $true
}

$lastActivity = Get-Date
while ($true) {
  if ($null -eq $script:p -or $script:p.HasExited) {
    Set-Status 'connecting'
    if (-not (Start-Relay)) { Set-Status 'waiting-for-unity'; Start-Sleep -Seconds 5; continue }
    Set-Status 'ready'
  }
  $req = Get-ChildItem $Queue -Filter *.json -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -First 1
  if (-not $req) {
    if (((Get-Date) - $lastActivity).TotalMinutes -gt $IdleExitMinutes) { break }
    Start-Sleep -Milliseconds 250; continue
  }
  $lastActivity = Get-Date
  $outPath = $req.FullName -replace '\.json$', '.out'
  $timeout = $RequestTimeoutSec
  if ($req.BaseName -match '__t(\d+)$') { $timeout = [int]$Matches[1] }
  $params = [IO.File]::ReadAllText($req.FullName).Trim()
  Remove-Item $req.FullName -ErrorAction SilentlyContinue
  $id = $script:nextId++
  Set-Status "busy $($req.BaseName)"
  $script:stdin.WriteLine((('{"jsonrpc":"2.0","id":' + $id + ',"method":"tools/call","params":' + $params + '}') -replace "`r?`n", ' '))
  $resp = Wait-Response $id $timeout
  if ($resp) {
    [IO.File]::WriteAllText($outPath, $resp, $utf8)
    Set-Status 'ready'
  } else {
    # Unity did not answer: usually a domain reload (compile) or the editor is busy. Reconnect.
    [IO.File]::WriteAllText($outPath, '{"error":{"message":"no response from Unity (compiling/reloading or busy); connection reset, try again"}}', $utf8)
    Stop-Relay
  }
}
Stop-Relay
Set-Status 'stopped'
Remove-Item $pidFile -ErrorAction SilentlyContinue
