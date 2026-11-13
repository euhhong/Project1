using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 사냥터지기 그랩에 걸려 끌려가는 쪽. 그랩이 맞힌 캐릭터에 붙어(없으면 그때 붙인다),
    /// 그랩을 쏜 쪽(CharacterSkill + GrabShotSkill)이 되감는 동안 그랩 머리를 따라 쏜 쪽 앞까지 끌려온다.
    /// - 맞는 순간 기절(CharacterStun)을 끌려오는 시간 + 잠깐만큼 한 번 건다 → 끌려가는 동안·놓인 뒤 잠깐 이동·점프·공격·스킬을 못 한다.
    /// - 쏜 쪽 몸에 겹치지 않게 minGap 앞에서 멈춘다. 벽 등에 걸려 그랩 머리에서 1.5m 넘게 처지면 놓인다.
    /// 캐릭터 루트에 붙는다(직접 붙일 필요 없음).
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabPull : MonoBehaviour
    {
        private const float LoseDistance = 1.5f;

        private CharacterSkill puller;
        private GrabShotSkill grab;
        private int useCount;
        private float alongOffset;   // 그랩 머리 기준 몸 가운데가 앞(+)/뒤(-)로 떨어진 거리(m)
        private float minGap;        // 쏜 쪽 몸 가운데에서 이만큼 앞까지만 끌려온다(m)

        private CharacterController controller;
        private CharacterStun stun;

        /// <summary>지금 그랩에 끌려가는 중인지.</summary>
        public bool IsPulled => puller != null;

        /// <summary>target 캐릭터를 끌어오기 시작한다(GrabShotSkill이 맞힌 순간 부른다). stunTime = 기절 시간(끌려오는 시간 + 놓인 뒤).</summary>
        public static void Begin(GameObject target, CharacterSkill puller, GrabShotSkill grab, Vector3 hookPoint, float stunTime)
        {
            if (target == null || puller == null) return;
            if (!target.TryGetComponent(out GrabPull pull)) pull = target.AddComponent<GrabPull>();
            pull.Attach(puller, grab, hookPoint, stunTime);
        }

        private void Attach(CharacterSkill by, GrabShotSkill def, Vector3 hookPoint, float stunTime)
        {
            controller = GetComponent<CharacterController>();
            if (!TryGetComponent(out stun))
            {
                stun = gameObject.AddComponent<CharacterStun>();
                if (TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 기절(IMoveOverride)을 이동에 알린다
            }
            puller = by;
            grab = def;
            useCount = by.UseCount;
            Vector3 dir = by.Direction;
            alongOffset = Vector3.Dot(Flat(transform.position - hookPoint), dir);
            float pullerRadius = by.TryGetComponent(out CharacterController pc) ? pc.radius : 0.4f;
            float selfRadius = controller != null ? controller.radius : 0.4f;
            minGap = pullerRadius + selfRadius + 0.25f;
            stun.Stun(stunTime); // 끌려오는 시간 + 놓인 뒤 잠깐. 한 번만 건다(다시 걸면 피격 모션이 처음부터 다시 나온다)
        }

        private void FixedUpdate()
        {
            if (puller == null) return;
            if (!puller || !puller.isActiveAndEnabled || puller.UseCount != useCount || puller.State != Core.SkillState.Casting
                || grab == null || !grab.IsDragging(puller))
            {
                Release();
                return;
            }

            Vector3 dir = puller.Direction;
            Vector3 head = puller.ReachOrigin + dir * puller.Reach;
            Vector3 target = Flat(head) + dir * alongOffset;
            // 쏜 쪽 몸에 겹치지 않게 앞에서 멈춘다.
            Vector3 pullerFlat = Flat(puller.transform.position);
            float along = Vector3.Dot(target - pullerFlat, dir);
            if (along < minGap) target += dir * (minGap - along);

            Vector3 here = Flat(transform.position);
            Vector3 delta = target - here;
            if (delta.magnitude > LoseDistance) { Release(); return; } // 벽 등에 걸려 처졌다
            if (controller != null && controller.enabled) controller.Move(delta);
            else transform.position += delta;

        }

        private void Release() => puller = null;

        private void OnDisable() => Release();

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
