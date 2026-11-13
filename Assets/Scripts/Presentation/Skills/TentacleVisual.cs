using System.Collections.Generic;
using Project1.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터 촉수 연출(판정은 BE TentacleField).
    /// - 촉수마다 지렁이처럼 리깅한 모델(Resources HasturTentacle — Tools/Blender/rig_tentacle.py)을 세우고, 단계에 맞춰 모션을 재생한다:
    ///   솟아오름(Rise) → 꿈틀거림(Idle, 반복) → 내려찍기(Slam: 젖힘 0~0.4 / 내리침 0.4~0.57 / 섬 0.57~1.0) → 들어감(Sink).
    ///   모델의 앞(+Z)이 내려찍는 방향을 본다. 키는 TentacleHeight(모델 키 1).
    /// - 좌클릭을 꾹 눌러 자리를 고르는 동안 그 자리에 원(촉수가 솟을 자리, 내려찍으면 닿는 거리 원도 옅게)을 그린다. 몸 근처(취소) = 회색, 못 놓는 곳 = 빨강.
    /// - E를 꾹 눌러 고르는 동안 고른 촉수 밑동에 밝은 고리 + 내려찍을 쪽(하스터가 바라보는 쪽) 표시.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 하스터만). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class TentacleVisual : MonoBehaviour
    {
        private const string ModelResource = "HasturTentacle/HasturTentacle";
        private const int RingSegments = 48;
        // Slam 모션 안의 구간(모션 전체 0~1, Blender 0~12 / 12~17 / 17~30 프레임).
        private const float SlamWindupEnd = 12f / 30f, SlamActiveEnd = 17f / 30f;

        [SerializeField] private Color previewColor = new(0.75f, 0.45f, 1f, 0.9f);
        [SerializeField] private Color reachColor = new(0.75f, 0.45f, 1f, 0.25f);
        [SerializeField] private Color targetColor = new(1f, 0.9f, 0.35f, 0.95f);
        [Tooltip("촉수 밑동 원 반지름(m).")]
        [SerializeField, Min(0.1f)] private float footRadius = 0.7f;

        private class Piece
        {
            public GameObject Go;
            public PlayableGraph Graph;
            public AnimationClipPlayable Playable;
            public TentacleState Shown = (TentacleState)(-1);
        }

        private ITentacleField field;
        private GameObject prefab;
        private readonly Dictionary<string, AnimationClip> clips = new();
        private readonly Dictionary<int, Piece> pieces = new();
        private readonly HashSet<int> alive = new();
        private Transform root;
        private LineRenderer previewRing, reachRing, targetRing, targetArrow;

        private void Awake() => field = GetComponent<ITentacleField>();

        private void OnDisable()
        {
            foreach (var p in pieces.Values) DestroyPiece(p);
            pieces.Clear();
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var p in pieces.Values) DestroyPiece(p);
            pieces.Clear();
            if (root != null) Destroy(root.gameObject);
        }

        private void LateUpdate()
        {
            field ??= GetComponent<ITentacleField>();
            if (field == null) return;
            if (root == null) Build();
            root.gameObject.SetActive(true);

            // 촉수 모델
            alive.Clear();
            var list = field.Tentacles;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                alive.Add(t.Id);
                if (!pieces.TryGetValue(t.Id, out var p)) { p = Spawn(); if (p == null) break; pieces[t.Id] = p; }
                Pose(p, t);
            }
            var gone = new List<int>();
            foreach (var kv in pieces) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int id in gone) { DestroyPiece(pieces[id]); pieces.Remove(id); }

            // 자리 고르기 원
            bool placing = field.Placing;
            previewRing.enabled = reachRing.enabled = placing;
            if (placing)
            {
                Color c = field.PreviewCancels ? new Color(0.6f, 0.6f, 0.6f, 0.4f) : field.PreviewValid ? previewColor : new Color(1f, 0.25f, 0.2f, 0.7f);
                previewRing.startColor = previewRing.endColor = c;
                SetRing(previewRing, field.PreviewPoint, footRadius * (0.85f + 0.15f * Mathf.Sin(Time.time * 6f)));
                reachRing.startColor = reachRing.endColor = field.PreviewValid && !field.PreviewCancels ? reachColor : Color.clear;
                SetRing(reachRing, field.PreviewPoint, field.TentacleHeight);
            }

            // 고른 촉수
            int target = field.TargetIndex;
            bool showTarget = field.Selecting && target >= 0 && target < list.Count;
            targetRing.enabled = targetArrow.enabled = showTarget;
            if (showTarget)
            {
                var t = list[target];
                SetRing(targetRing, t.Position, footRadius * (1f + 0.1f * Mathf.Sin(Time.time * 10f)));
                Vector3 dir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                targetArrow.positionCount = 2;
                targetArrow.SetPosition(0, t.Position + Vector3.up * 0.05f);
                targetArrow.SetPosition(1, t.Position + dir * field.TentacleHeight + Vector3.up * 0.05f);
            }
        }

        private void Pose(Piece p, in TentacleInfo t)
        {
            var go = p.Go;
            go.transform.SetPositionAndRotation(t.Position, Quaternion.LookRotation(t.Direction.sqrMagnitude > 1e-4f ? t.Direction : Vector3.forward, Vector3.up));
            go.transform.localScale = Vector3.one * field.TentacleHeight;
            float k = t.StateDuration > 0f ? Mathf.Clamp01(t.StateTime / t.StateDuration) : 1f;
            string clipName; float at; // at = 모션 전체 0~1
            switch (t.State)
            {
                case TentacleState.Rising: clipName = "Rise"; at = k; break;
                case TentacleState.SlamWindup: clipName = "Slam"; at = Mathf.Lerp(0f, SlamWindupEnd, k); break;
                case TentacleState.SlamActive: clipName = "Slam"; at = Mathf.Lerp(SlamWindupEnd, SlamActiveEnd, k); break;
                case TentacleState.SlamRecovery: clipName = "Slam"; at = Mathf.Lerp(SlamActiveEnd, 1f, k); break;
                case TentacleState.Sinking: clipName = "Sink"; at = k; break;
                default: clipName = "Idle"; at = -1f; break;
            }
            if (!clips.TryGetValue(clipName, out var clip) || clip == null) return;
            if (!p.Graph.IsValid() || p.Playable.GetAnimationClip() != clip)
            {
                if (p.Graph.IsValid()) p.Graph.Destroy();
                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                p.Playable = AnimationPlayableUtilities.PlayClip(animator, clip, out p.Graph);
                p.Playable.Pause(); // 시간은 직접 맞춘다
            }
            float time = at >= 0f ? at * clip.length : (t.StateTime % Mathf.Max(clip.length, 0.01f));
            p.Playable.SetTime(time);
            p.Graph.Evaluate();
            p.Shown = t.State;
        }

        private Piece Spawn()
        {
            if (prefab == null) prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null) return null;
            var go = Instantiate(prefab, root);
            go.name = "HasturTentacle";
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return new Piece { Go = go };
        }

        private static void DestroyPiece(Piece p)
        {
            if (p == null) return;
            if (p.Graph.IsValid()) p.Graph.Destroy();
            if (p.Go != null) Object.Destroy(p.Go);
        }

        private void Build()
        {
            root = new GameObject("HasturTentacles").transform; // 캐릭터를 따라 움직이지 않게 월드에 둔다
            foreach (var c in Resources.LoadAll<AnimationClip>(ModelResource))
            {
                if (c.name.StartsWith("__preview")) continue;
                foreach (var key in new[] { "Rise", "Idle", "Slam", "Sink" })
                    if (c.name == key || c.name.EndsWith("|" + key)) clips[key] = c;
            }
            previewRing = Line("PreviewRing", previewColor, 0.07f, true);
            reachRing = Line("ReachRing", reachColor, 0.04f, true);
            targetRing = Line("TargetRing", targetColor, 0.09f, true);
            targetArrow = Line("TargetArrow", targetColor, 0.06f, false);
        }

        private LineRenderer Line(string name, Color color, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = loop ? RingSegments : 2;
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            return lr;
        }

        private static void SetRing(LineRenderer lr, Vector3 center, float radius)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * 0.05f);
            }
        }
    }
}
