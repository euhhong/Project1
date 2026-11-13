namespace Project1.Core
{
    /// <summary>
    /// 맞는 쪽이 히트를 막는다(예: 생존자 주먹 막기). 히트를 내기 직전에 맞은 캐릭터 루트에 붙은 것을 모두 부르고,
    /// 하나라도 true면 그 히트는 일어나지 않는다(데미지·경직·넉백·피격 모션 없음). 막은 히트는 HitEvents.HitBlocked로 나간다.
    /// </summary>
    public interface IHitBlocker
    {
        /// <param name="hit">막을지 볼 히트 결과(아직 때린 쪽 IHitModifier를 적용하기 전).</param>
        /// <param name="attacker">때린 캐릭터 루트(모르면 null).</param>
        bool BlocksHit(in HitResult hit, UnityEngine.GameObject attacker);
    }
}
