namespace Project1.Core
{
    /// <summary>
    /// 때리는 쪽이 히트 결과를 내기 직전에 고친다(예: 맹인 지팡이 = 데미지 없이 넉백). 때린 캐릭터 루트에 붙은 것을 모두 부른다.
    /// </summary>
    public interface IHitModifier
    {
        /// <param name="hit">고칠 히트 결과.</param>
        /// <param name="victim">맞은 캐릭터 루트.</param>
        void ModifyHit(ref HitResult hit, UnityEngine.GameObject victim);
    }
}
