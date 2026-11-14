using System.Collections.Generic;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 사다리 표시(맵 생성기가 붙인다). 오브젝트 자리 = 사다리 아래 끝 가운데(벽 면, 발 딛는 바닥 높이), forward = 벽 바깥쪽(사다리를 타는 사람이 서는 쪽).
    /// 위 끝은 height만큼 위 — 보통 위층 바닥(턱) 높이. 사다리 모양은 충돌이 없다(타기는 CharacterTraversal이 이 표시로 한다).
    /// </summary>
    [DisallowMultipleComponent]
    public class Ladder : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float height = 4f;
        [SerializeField, Min(0.2f)] private float width = 0.8f;
        [Tooltip("벽 면에서 사다리 발판까지(m).")]
        [SerializeField, Min(0f)] private float depth = 0.15f;
        [Tooltip("가로대 간격(m). 사다리 오르기 모션 한 사이클(두 손 한 번씩)에 두 칸 오른다 — 손은 한 번에 두 칸 위를 잡는다.")]
        [SerializeField, Min(0.2f)] private float rungStep = 0.8f;

        private static readonly List<Ladder> all = new();
        public static IReadOnlyList<Ladder> All => all;

        public float Height => height;
        public float Width => width;
        public float Depth => depth;
        public float RungStep => rungStep;
        /// <summary>오르기 모션 한 사이클에 오르는 높이(가로대 두 칸).</summary>
        public float CycleRise => rungStep * 2f;
        /// <summary>첫 가로대 높이(아래 끝에서, m).</summary>
        public const float FirstRung = 0.3f;
        /// <summary>i번째 가로대 높이(월드 y).</summary>
        public float RungY(int i) => BottomY + FirstRung + i * rungStep;
        /// <summary>y에 가장 가까운 가로대 번호(0 ~ 마지막).</summary>
        public int NearestRung(float y) => Mathf.Clamp(Mathf.RoundToInt((y - BottomY - FirstRung) / rungStep), 0, Mathf.Max(0, Mathf.FloorToInt((height + 0.9f - FirstRung) / rungStep)));
        public Vector3 Bottom => transform.position;
        public float BottomY => transform.position.y;
        public float TopY => transform.position.y + height;
        /// <summary>벽 바깥쪽(수평).</summary>
        public Vector3 Normal { get { Vector3 f = transform.forward; f.y = 0f; return f.normalized; } }

        public void Set(float height, float width, float depth, float rungStep) { this.height = height; this.width = width; this.depth = depth; this.rungStep = rungStep; }

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() => all.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Vector3 b = Bottom + Normal * depth, r = transform.right * (width / 2f);
            Gizmos.DrawLine(b - r, b - r + Vector3.up * height);
            Gizmos.DrawLine(b + r, b + r + Vector3.up * height);
        }
    }
}
