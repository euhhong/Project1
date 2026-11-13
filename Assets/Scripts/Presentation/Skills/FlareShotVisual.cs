using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공군 신호총 연출.
    /// - 신호총(오른손 소품 Coordinator_Prop)은 평소엔 넣어 두고(안 보임), 신호총 스킬을 쓰는 동안만 꺼내 든다(끝난 뒤 holsterDelay초까지).
    /// - 이 캐릭터가 쏜 신호탄(ProjectileEvents, Kind = Flare)마다 FlareTrail(붉은 연기 궤적)을 붙인다. 시작점은 신호총 총구 쪽.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공군만).
    /// </summary>
    [DisallowMultipleComponent]
    public class FlareShotVisual : MonoBehaviour
    {
        private const string SkillId = "Coordinator_Flare";

        [Tooltip("스킬이 끝난 뒤 신호총을 넣기까지(초).")]
        [SerializeField, Min(0f)] private float holsterDelay = 0.25f;

        private CharacterIdentity identity;
        private ICharacterSkillState skill;
        private Renderer gun;
        private Animator boundAnimator;
        private float showUntil = -1f;

        private void OnEnable() => ProjectileEvents.Launched += OnLaunched;

        private void OnDisable()
        {
            ProjectileEvents.Launched -= OnLaunched;
            if (gun != null) gun.enabled = true; // 다른 캐릭터로 바뀌거나 꺼지면 원래대로
        }

        private void LateUpdate()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != boundAnimator) { boundAnimator = animator; gun = FindGun(); } // 모델이 바뀌면 다시 찾는다
            if (gun == null) return;
            if (skill == null) TryGetComponent(out skill);
            bool firing = skill != null && skill.State == SkillState.Casting && skill.SkillId == SkillId;
            if (firing) showUntil = Time.time + holsterDelay;
            gun.enabled = Time.time <= showUntil;
        }

        private Renderer FindGun()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.EndsWith("_Prop")) return r;
            return null;
        }

        private void OnLaunched(IProjectile p)
        {
            if (identity == null) identity = GetComponent<CharacterIdentity>();
            if (p.Kind != "Flare" || identity == null || p.OwnerId != identity.Id) return;
            FlareTrail.Create(p, Muzzle(p));
        }

        /// <summary>오른손 신호총 총구(모델 임포트 때 총열 끝에 둔 Coordinator_Muzzle, ModelPropFixer). 없으면 소품 상자 가운데에서 몸 앞쪽, 소품이 없으면 BE 시작점.</summary>
        private Vector3 Muzzle(IProjectile p)
        {
            if (gun == null) gun = FindGun();
            if (gun == null) return p.Position;
            gun.enabled = true; // 쏘는 순간에는 꺼내 들고 있다
            var mz = gun.transform.Find("Coordinator_Muzzle");
            if (mz != null) return mz.position;
            var b = gun.bounds;
            return b.center + transform.forward * Mathf.Max(b.extents.x, b.extents.z);
        }
    }
}
