using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공군 신호총 연출: 이 캐릭터가 쏜 신호탄(ProjectileEvents, Kind = Flare)마다 FlareTrail(붉은 연기 궤적)을 붙인다.
    /// 시작점은 오른손 신호총(Coordinator_Prop)의 총구 쪽. 캐릭터 루트에 붙인다(CharacterLoadout, 공군만).
    /// </summary>
    [DisallowMultipleComponent]
    public class FlareShotVisual : MonoBehaviour
    {
        private CharacterIdentity identity;

        private void OnEnable() => ProjectileEvents.Launched += OnLaunched;
        private void OnDisable() => ProjectileEvents.Launched -= OnLaunched;

        private void OnLaunched(IProjectile p)
        {
            if (identity == null) identity = GetComponent<CharacterIdentity>();
            if (p.Kind != "Flare" || identity == null || p.OwnerId != identity.Id) return;
            FlareTrail.Create(p, Muzzle(p));
        }

        /// <summary>오른손 신호총 끝(소품 상자의 가운데에서 몸 앞쪽으로). 소품이 없으면 BE 시작점.</summary>
        private Vector3 Muzzle(IProjectile p)
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.name.EndsWith("_Prop")) continue;
                var b = r.bounds;
                return b.center + transform.forward * Mathf.Max(b.extents.x, b.extents.z);
            }
            return p.Position;
        }
    }
}
