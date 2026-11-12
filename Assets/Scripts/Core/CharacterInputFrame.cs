using System;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 한 틱 동안 캐릭터에게 들어가는 입력. 네트워크로 보낼 수 있게 단순한 값만 담는다.
    /// </summary>
    [Serializable]
    public struct CharacterInputFrame
    {
        /// <summary>x = 좌(-)/우(+), y = 뒤(-)/앞(+). 시야 Yaw 기준. 크기 0~1.</summary>
        public Vector2 Move;

        /// <summary>달리기 키를 누르고 있는 중.</summary>
        public bool Run;

        /// <summary>지난 틱 이후 점프 키가 눌렸다.</summary>
        public bool Jump;

        /// <summary>시야 좌우 각도(도). 월드 Y축 기준, 0 = +Z.</summary>
        public float LookYaw;

        /// <summary>시야 상하 각도(도). Unity X 회전 규칙: + = 아래를 봄, - = 위를 봄.</summary>
        public float LookPitch;

        /// <summary>지난 틱 이후 공격 키를 짧게(기준 시간 미만) 눌렀다 뗐다 → 약공격.</summary>
        public bool LightAttack;

        /// <summary>공격 키를 기준 시간 이상 누르고 있는 중 → 강공격 모으기.</summary>
        public bool HeavyCharging;

        /// <summary>지난 틱 이후 기준 시간 이상 누르던 공격 키를 뗐다 → 강공격 발동.</summary>
        public bool HeavyRelease;

        /// <summary>지난 틱 이후 고유 스킬 키(E)가 눌렸다.</summary>
        public bool Skill;

        /// <summary>지난 틱 이후 공격 키가 눌렸다(누른 순간, 짧게/길게와 상관없이). 공중 공격처럼 바로 나가야 할 때 쓴다.</summary>
        public bool AttackPressed;
    }
}
