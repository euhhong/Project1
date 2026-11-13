using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 휘두르는 궤적(타격 이펙트): 약·강공격의 판정 구간 동안, 그 공격이 쓰는 판정 상자(손·발·무기)가 지나간 자리를
    /// 반투명하게 블렌드되는 띠로 남긴다. 띠는 지나간 지 trailTime초 사이에 꼬리 쪽부터 투명해지며 사라진다(알파 블렌드, 가장자리도 투명).
    /// - 무기(긴 상자·캡슐): 손잡이 쪽 ~ 끝 쪽 두 점 사이를 잇는 넓은 띠(칼날이 쓸고 간 면).
    /// - 손·발(구): 가운데를 지나는 좁은 띠.
    /// 감시자는 붉은빛, 생존자는 흰빛. 캐릭터 루트에 붙인다(CharacterLoadout, 모두). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class SwingTrail : MonoBehaviour
    {
        [Tooltip("띠가 남아 있는 시간(초).")]
        [SerializeField, Min(0.02f)] private float trailTime = 0.18f;
        [Tooltip("판정 구간이 끝난 뒤에도 조금 더 그리는 시간(초) — 휘두름이 끝까지 이어져 보이게.")]
        [SerializeField, Min(0f)] private float afterActive = 0.06f;
        [SerializeField] private Color survivorColor = new(1f, 1f, 1f, 0.75f);
        [SerializeField] private Color hunterColor = new(1f, 0.38f, 0.32f, 0.75f);
        [Tooltip("손·발 띠의 폭(구 반지름 대비).")]
        [SerializeField, Min(0.1f)] private float limbWidth = 1.1f;

        private struct Sample { public Vector3 a, b; public float t; }
        private class Track
        {
            public Hitbox hitbox;
            public readonly List<Sample> samples = new();
        }

        private ICharacterAttackState attackState;
        private CharacterAttack attack;
        private readonly List<Track> tracks = new();
        private readonly List<Hitbox> found = new();
        private Mesh mesh;
        private GameObject drawer;
        private Material mat;
        private Color color;
        private float emitUntil;
        private readonly List<Vector3> verts = new();
        private readonly List<Color> colors = new();
        private readonly List<int> tris = new();

        private void Awake()
        {
            attackState = GetComponent<ICharacterAttackState>();
            attack = GetComponent<CharacterAttack>();
        }

        private void OnDestroy()
        {
            if (drawer != null) Destroy(drawer);
            if (mesh != null) Destroy(mesh);
            if (mat != null) Destroy(mat);
        }

        private void LateUpdate()
        {
            if (attackState == null) Awake();
            if (attackState == null) return;
            if (drawer == null) Build();
            float now = Time.time;

            bool striking = attackState.AttackState == CharacterAttackState.Light || attackState.AttackState == CharacterAttackState.Heavy;
            if (striking && attackState.AttackPhase == AttackPhase.Active) emitUntil = now + afterActive;
            bool emit = striking && now <= emitUntil;
            if (emit)
            {
                if (tracks.Count == 0) color = IsHunter() ? hunterColor : survivorColor; // 새 휘두름: 캐릭터에 맞는 색
                Emit(now);
            }

            // 오래된 자리 버리기
            bool any = false;
            foreach (var tr in tracks)
            {
                tr.samples.RemoveAll(s => now - s.t > trailTime);
                if (tr.samples.Count > 0) any = true;
            }
            if (!emit && !any) { tracks.Clear(); drawer.SetActive(false); return; }
            drawer.SetActive(true);
            Rebuild(now);
        }

        private void Emit(float now)
        {
            HitboxSlot slots = CurrentSlots();
            GetComponentsInChildren(found);
            foreach (var hb in found)
            {
                if (hb == null || (hb.Slot & slots) == 0 || hb.Collider == null) continue;
                if (hb.name == "Hitbox_Reach" || hb.name == "Hitbox_CaneSweep" || hb.name == "Hitbox_KickReach" || hb.name == "Hitbox_RunJumpReach") continue; // 몸 앞에 붙은 넓은 판정 상자(보이지 않는 리치)는 궤적을 그리지 않는다
                var tr = tracks.Find(x => x.hitbox == hb);
                if (tr == null) { tr = new Track { hitbox = hb }; tracks.Add(tr); }
                Endpoints(hb.Collider, out Vector3 a, out Vector3 b);
                tr.samples.Add(new Sample { a = a, b = b, t = now });
            }
        }

        private HitboxSlot CurrentSlots()
        {
            if (attack == null) return HitboxSlot.RightHand | HitboxSlot.Weapon;
            AttackSpec spec = attackState.AttackState == CharacterAttackState.Heavy
                ? (attackState.IsRunJumpAttack ? attack.AirHeavyAttack : attack.HeavyAttack)
                : (attackState.IsRunJumpAttack ? attack.AirLightAttack : attack.LightAttack);
            return spec.Slots;
        }

        /// <summary>판정 모양의 긴 축 양 끝(구는 가운데를 지나는 짧은 선).</summary>
        private void Endpoints(Collider c, out Vector3 a, out Vector3 b)
        {
            Transform t = c.transform;
            switch (c)
            {
                case BoxCollider box:
                {
                    Vector3 s = Vector3.Scale(box.size, Abs(t.lossyScale));
                    Vector3 axis = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
                    float half = Vector3.Dot(box.size, axis) * 0.5f;
                    a = t.TransformPoint(box.center - axis * half); b = t.TransformPoint(box.center + axis * half);
                    return;
                }
                case CapsuleCollider cap:
                {
                    Vector3 axis = cap.direction == 0 ? Vector3.right : cap.direction == 1 ? Vector3.up : Vector3.forward;
                    float half = cap.height * 0.5f;
                    a = t.TransformPoint(cap.center - axis * half); b = t.TransformPoint(cap.center + axis * half);
                    return;
                }
                case SphereCollider sph:
                {
                    Vector3 center = t.TransformPoint(sph.center);
                    float r = sph.radius * Mathf.Max(Abs(t.lossyScale).x, Mathf.Max(Abs(t.lossyScale).y, Abs(t.lossyScale).z)) * limbWidth;
                    a = center - Vector3.up * r; b = center + Vector3.up * r;
                    return;
                }
                default:
                {
                    Bounds bb = c.bounds;
                    a = bb.center - Vector3.up * bb.extents.y; b = bb.center + Vector3.up * bb.extents.y;
                    return;
                }
            }
        }

        private void Rebuild(float now)
        {
            verts.Clear(); colors.Clear(); tris.Clear();
            foreach (var tr in tracks)
            {
                var s = tr.samples;
                if (s.Count < 2) continue;
                int start = verts.Count;
                for (int i = 0; i < s.Count; i++)
                {
                    float life = 1f - Mathf.Clamp01((now - s[i].t) / trailTime); // 1 = 방금, 0 = 사라짐
                    float alpha = color.a * life * life;
                    Vector3 mid = (s[i].a + s[i].b) * 0.5f;
                    // 띠 세 줄(바깥 a, 가운데, 바깥 b): 가운데가 진하고 가장자리는 투명 — 부드럽게 블렌드된다
                    verts.Add(s[i].a); colors.Add(new Color(color.r, color.g, color.b, alpha * 0.25f));
                    verts.Add(Vector3.Lerp(mid, s[i].b, 0.35f)); colors.Add(new Color(1f, 1f, 1f, alpha));
                    verts.Add(s[i].b); colors.Add(new Color(color.r, color.g, color.b, 0f));
                }
                for (int i = 0; i < s.Count - 1; i++)
                {
                    int r0 = start + i * 3, r1 = r0 + 3;
                    tris.AddRange(new[] { r0, r1, r0 + 1, r0 + 1, r1, r1 + 1, r0 + 1, r1 + 1, r0 + 2, r0 + 2, r1 + 1, r1 + 2 });
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetColors(colors); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        private void Build()
        {
            drawer = new GameObject("SwingTrail"); // 월드 좌표로 그린다(캐릭터를 따라 움직이지 않게)
            mesh = new Mesh { name = "SwingTrail" };
            mesh.MarkDynamic();
            drawer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = drawer.AddComponent<MeshRenderer>();
            mat = new Material(Shader.Find("Sprites/Default")) { name = "SwingTrail", renderQueue = 3100 }; // 꼭짓점 색 알파 블렌드, 양면
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            color = IsHunter() ? hunterColor : survivorColor;
            drawer.SetActive(false);
        }

        private bool IsHunter()
        {
            var anim = GetComponentInChildren<Animator>();
            return anim != null && anim.avatar != null && anim.avatar.name.StartsWith("Hunter_");
        }

        private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
