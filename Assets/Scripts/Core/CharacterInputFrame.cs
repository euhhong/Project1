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

        /// <summary>고유 스킬 키(E)를 지금 누르고 있는 중(꾹 눌러 모으는 스킬용, 예: 광대 로켓 돌진).</summary>
        public bool SkillHeld;

        /// <summary>지난 틱 이후 공격 키가 눌렸다(누른 순간, 짧게/길게와 상관없이). 공중 공격처럼 바로 나가야 할 때 쓴다.</summary>
        public bool AttackPressed;

        /// <summary>겨누는 곳이 있는지(화면 안에서 무언가에 닿음). 보통은 마우스 커서 아래, 우클릭으로 시점을 돌리는 동안·돌린 뒤 커서가 아직 안 움직인 동안은 화면 가운데(AimFromCursor = false).</summary>
        public bool HasAimPoint;

        /// <summary>AimPoint가 마우스 커서 아래인지(false = 시점을 돌리는 중이라 화면 가운데). 나비 미리보기는 커서일 때만 따라 옮긴다.</summary>
        public bool AimFromCursor;

        /// <summary>마우스 커서가 가리키는 곳(월드). HasAimPoint일 때만 의미 있다(예: 붉은나비 나비 놓을 자리).</summary>
        public Vector3 AimPoint;

        /// <summary>AimPoint 면의 바깥 방향(월드). 땅이면 위쪽에 가깝다.</summary>
        public Vector3 AimNormal;
    }
}
