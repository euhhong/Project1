using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 공격 면역(BE): 이 캐릭터가 맞은 히트는 아예 나가지 않는다(HitEvents — 데미지·경직·넉백·피격 모션·막기 이펙트 없음).
    /// 예: 공장장 분신(CloneSummonSkill이 붙인다). 캐릭터 루트(CharacterIdentity)에 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterIdentity))]
    public class HitImmunity : MonoBehaviour
    {
        private int id;

        private void OnEnable() => Register();
        private void Start() => Register(); // CharacterIdentity가 ID를 정한 뒤(붙인 순서와 상관없이)

        private void Register()
        {
            int now = GetComponent<CharacterIdentity>().Id;
            if (now == 0) return;
            if (id != 0 && id != now) HitEvents.SetImmune(id, false);
            id = now;
            HitEvents.SetImmune(id, true);
        }

        private void OnDisable() { if (id != 0) HitEvents.SetImmune(id, false); }
    }
}
