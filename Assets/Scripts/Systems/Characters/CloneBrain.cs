using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 분신의 움직임 규칙(BE, 분신 캐릭터 루트의 입력 — ICharacterInputSource).
    /// - 불려 나올 때 정한 목표(그때 가장 가까이 있던 상대)를 향해 몸을 돌리고 걸어서 다가간다(달리지 않음 — 걷기 속도·걷기 모션).
    /// - 공격 범위(attackRange) 안에 들어오면 멈춰 서서 공격한다(약공격, 공격이 끝나고 attackGap초 뒤 다시). 목표가 물러나면 다시 다가간다.
    /// - lifetime초가 지나면 사라진다. 목표가 없어지면 그 자리에 서 있다가 사라진다.
    /// 입력은 틱마다 한 번 정해 같은 틱의 이동·공격·스킬이 같은 값을 읽게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CloneBrain : MonoBehaviour, ICharacterInputSource
    {
        private CharacterIdentity target;
        private float lifetime, attackRange, attackGap;
        private float age, lastAttackEnd = -999f;
        private ICharacterAttackState attack;
        private int frameTick = int.MinValue;
        private CharacterInputFrame frame;
        private bool despawned;

        public CharacterIdentity Target => target;
        public float Age => age;
        public float Lifetime => lifetime;
        public bool InRange { get; private set; }

        public void Init(CharacterIdentity target, float lifetime, float attackRange, float attackGap)
        {
            this.target = target; this.lifetime = lifetime; this.attackRange = attackRange;
            this.attackGap = attackGap;
        }

        public CharacterInputFrame ReadInput()
        {
            if (frameTick != SimulationTick.Current) { frameTick = SimulationTick.Current; frame = Decide(); }
            return frame;
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (attack == null) TryGetComponent(out attack);
            if (attack != null && attack.AttackState != CharacterAttackState.None) lastAttackEnd = age;
            if (age >= lifetime) Despawn();
        }

        public void Despawn()
        {
            if (despawned) return;
            despawned = true;
            CloneEvents.RaiseDespawning(gameObject);
            Destroy(gameObject);
        }

        private CharacterInputFrame Decide()
        {
            var f = new CharacterInputFrame { LookYaw = transform.eulerAngles.y };
            InRange = false;
            if (despawned || target == null || !target.isActiveAndEnabled) return f;
            Vector3 to = target.transform.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist > 1e-3f) f.LookYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; // 목표를 본다(몸 = 시야)
            if (dist > attackRange)
            {
                f.Move = new Vector2(0f, 1f); // 시야(목표) 쪽으로 곧장
                f.Run = false; // 늘 걷는다
                return f;
            }
            // 공격 범위 안: 멈춰 서서 공격
            InRange = true;
            bool idle = attack == null || attack.AttackState == CharacterAttackState.None;
            if (idle && age - lastAttackEnd >= attackGap)
            {
                f.LightAttack = true;
                f.AttackPressed = true;
                lastAttackEnd = age; // 같은 틱에 두 번 내지 않게(공격이 시작되면 끝날 때까지 다시 갱신된다)
            }
            return f;
        }
    }
}
