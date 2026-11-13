using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 리퍼 안개 칼날이 지나간 자리에 안개 구역을 남긴다(BE).
    /// - 칼날(MistCharge.LaunchBlade)이 날아가는 동안 spacing마다 안개 한 덩이를 놓는다(끝난 자리에도 하나).
    /// - 덩이마다 lifetime(7초)이 지나면 사라진다.
    /// - 리퍼가 안개 위(덩이에서 수평 radius 안, 칼날 높이 아래 땅 ~ 조금 위)에서 움직이면 이동 속도 speedBoost배(1.3 = 30% 빠름, IMoveSpeedBoost).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 리퍼만). 그리기는 FE(MistTrailVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class MistTrailField : MonoBehaviour, IMistTrail, IMoveSpeedBoost
    {
        [Tooltip("안개 한 덩이가 남아 있는 시간(초).")]
        [SerializeField, Min(0.1f)] private float lifetime = 7f;
        [Tooltip("안개 덩이 사이 거리(m).")]
        [SerializeField, Min(0.3f)] private float spacing = 1.5f;
        [Tooltip("안개 한 덩이의 반지름(m, 수평).")]
        [SerializeField, Min(0.1f)] private float radius = 1.3f;
        [Tooltip("안개 위에서 움직일 때 이동 속도 배율.")]
        [SerializeField, Min(1f)] private float speedBoost = 1.3f;
        [Tooltip("칼날 높이보다 이만큼(m) 아래까지 안개 위로 본다(칼날은 허리 높이로 날아간다).")]
        [SerializeField, Min(0f)] private float depthBelow = 3f;

        private struct Tracked { public HomingProjectile Blade; public Vector3 LastDrop; public Vector3 Swing; public float Power; }

        private readonly List<MistTrailPoint> points = new();
        private readonly List<Tracked> tracked = new();

        public IReadOnlyList<MistTrailPoint> Points => points;
        public float Lifetime => lifetime;
        public float Radius => radius;
        public int TotalAdded { get; private set; }

        /// <summary>날아가는 안개 칼날을 따라가며 안개를 놓기 시작한다(MistCharge가 칼날을 날릴 때 부른다).</summary>
        public void Track(HomingProjectile blade, Vector3 origin, Vector3 swingAxis, float power)
        {
            if (blade == null) return;
            tracked.Add(new Tracked { Blade = blade, LastDrop = origin, Swing = swingAxis, Power = power });
            Drop(origin, blade.Direction, swingAxis, power);
        }

        private void OnDisable()
        {
            points.Clear();
            tracked.Clear();
        }

        private void FixedUpdate()
        {
            float now = Time.time;
            while (points.Count > 0 && now - points[0].Born >= lifetime) points.RemoveAt(0); // 오래된 것부터 사라진다

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                var t = tracked[i];
                bool alive = t.Blade != null && t.Blade.Alive;
                Vector3 head = alive ? t.Blade.Position : t.Blade != null ? t.Blade.EndPoint : t.LastDrop;
                Vector3 dir = t.Blade != null ? t.Blade.Direction : Vector3.forward;
                // 지나간 길을 따라 spacing마다(빠르게 날아 한 틱에 여러 칸을 지나도 빠짐없이)
                Vector3 seg = head - t.LastDrop;
                int n = Mathf.FloorToInt(seg.magnitude / spacing);
                for (int k = 1; k <= n; k++) Drop(t.LastDrop + seg.normalized * (spacing * k), dir, t.Swing, t.Power);
                if (n > 0) t.LastDrop += seg.normalized * (spacing * n);
                if (!alive)
                {
                    if ((head - t.LastDrop).magnitude > spacing * 0.3f) Drop(head, dir, t.Swing, t.Power); // 부서진 자리
                    tracked.RemoveAt(i);
                    continue;
                }
                tracked[i] = t;
            }
        }

        private void Drop(Vector3 at, Vector3 dir, Vector3 swing, float power)
        {
            dir.y = 0f;
            TotalAdded++;
            points.Add(new MistTrailPoint
            {
                Position = at,
                Direction = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward,
                SwingAxis = swing,
                Born = Time.time,
                Power = power,
            });
        }

        /// <summary>리퍼가 지금 안개 위에 있는지.</summary>
        public bool InMist
        {
            get
            {
                Vector3 p = transform.position;
                float r2 = radius * radius;
                foreach (var m in points)
                {
                    float dx = p.x - m.Position.x, dz = p.z - m.Position.z;
                    if (dx * dx + dz * dz > r2) continue;
                    if (p.y > m.Position.y + 0.5f || p.y < m.Position.y - depthBelow) continue;
                    return true;
                }
                return false;
            }
        }

        public bool TryGetSpeedBoost(out float multiplier)
        {
            multiplier = speedBoost;
            return points.Count > 0 && InMist;
        }
    }
}
