using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 코드로 입력을 넣는 입력 소스. AI, 허수아비, 네트워크로 받은 입력 재생, 테스트에 쓴다.
    /// SetFrame으로 다음 틱 입력을 넣으면, 일회성 값(Jump·LightAttack·HeavyRelease·Skill)은 한 틱 뒤 비워진다.
    /// </summary>
    public class ScriptedCharacterInput : MonoBehaviour, ICharacterInputSource
    {
        private CharacterInputFrame pending;
        private int cachedTick = int.MinValue;
        private CharacterInputFrame cachedFrame;

        /// <summary>다음에 읽힐 입력을 정한다.</summary>
        public void SetFrame(CharacterInputFrame frame)
        {
            pending = frame;
            cachedTick = int.MinValue; // 같은 틱 안에서 바꿔도 반영
        }

        public CharacterInputFrame ReadInput()
        {
            int tick = SimulationTick.Current;
            if (tick == cachedTick)
            {
                return cachedFrame;
            }

            cachedTick = tick;
            cachedFrame = pending;
            pending.Jump = false;
            pending.LightAttack = false;
            pending.HeavyRelease = false;
            pending.Skill = false;
            return cachedFrame;
        }
    }
}
