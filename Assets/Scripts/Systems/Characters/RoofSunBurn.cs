using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 햇볕 화상(BE): 4층 이상 높이(지붕·천장 위 — 일부 캐릭터만 도약·비행으로 올라감)에 발을 딛고 있으면 햇볕에 타서 조금씩 데미지(도트).
    /// 공중에 떠 있는 동안은 타지 않는다(뛰어오르거나 지나가는 건 괜찮다). 3층 바닥·옥상 테라스·연결동 옥상 길(높이 12m)은 안전.
    /// 데미지는 tickInterval초마다 damagePerSecond × tickInterval — 약하게(최대 체력 100 기준 초당 4).
    /// 캐릭터 루트에 붙인다(CharacterLoadout). 연출은 SunBurnVisual(FE)이 Burning을 읽는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoofSunBurn : MonoBehaviour, ISunBurn
    {
        [Tooltip("이 높이(m, 발 위치) 이상에 서 있으면 탄다 — 3층 바닥(12.15)·점프보다 높고 지붕(18.15)보다 낮게.")]
        [SerializeField] private float minHeight = 16.5f;
        [Tooltip("초당 데미지(도트).")]
        [SerializeField, Min(0f)] private float damagePerSecond = 4f;
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;

        private ICharacterState state;
        private CharacterHealth health;
        private float burnStart = -1f, nextTick;

        public bool Burning => burnStart >= 0f;
        public float BurnTime => Burning ? Time.time - burnStart : 0f;

        private void Awake()
        {
            state = GetComponent<ICharacterState>();
            health = GetComponent<CharacterHealth>();
        }

        private void FixedUpdate()
        {
            if (state == null || health == null) Awake();
            bool onRoof = state != null && state.IsGrounded && transform.position.y >= minHeight && (health == null || !health.IsDead);
            if (!onRoof) { burnStart = -1f; return; }
            if (burnStart < 0f) { burnStart = Time.time; nextTick = Time.time + tickInterval; } // 첫 틱은 조금 뒤(잠깐 디딘 건 봐줌)
            if (Time.time < nextTick) return;
            nextTick += tickInterval;
            if (health != null) health.Damage(damagePerSecond * tickInterval);
        }

        private void OnDisable() => burnStart = -1f;
    }
}
