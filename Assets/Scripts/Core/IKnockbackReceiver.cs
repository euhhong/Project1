namespace Project1.Core
{
    /// <summary>
    /// 넉백(Knockback.Push)이 걸리기 직전에 맞은 캐릭터가 반응하고 미는 방향을 고친다.
    /// 예: 턱에 매달린 채 넉백되면 손을 놓고 벽에서 떨어져 나가 떨어진다(CharacterTraversal). 맞은 캐릭터 루트에 붙은 것을 모두 부른다.
    /// </summary>
    public interface IKnockbackReceiver
    {
        /// <param name="velocity">미는 수평 속도(m/s). 고쳐도 된다.</param>
        void OnKnockback(ref UnityEngine.Vector3 velocity);
    }
}
