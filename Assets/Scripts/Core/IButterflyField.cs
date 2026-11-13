using System.Collections.Generic;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 붉은나비(미치코)가 놓은 나비들과 놓는 중인 자리, 지금 겨누는 나비(읽기 전용). FE가 나비·미리보기·표적 표시를 그린다.
    /// 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface IButterflyField
    {
        /// <summary>놓은 나비 자리(땅 위 점, 월드). 오래된 것부터.</summary>
        IReadOnlyList<Vector3> Butterflies { get; }

        /// <summary>좌클릭을 꾹 눌러 놓을 자리를 고르는 중.</summary>
        bool Placing { get; }

        /// <summary>미리보기 자리(마우스가 가리키는 곳).</summary>
        Vector3 PreviewPoint { get; }

        /// <summary>미리보기 자리에 놓을 수 있는지(땅 위만 됨).</summary>
        bool PreviewValid { get; }

        /// <summary>미리보기 자리가 몸 근처라 떼면 놓기가 취소되는지(재사용 대기 없음).</summary>
        bool PreviewCancels { get; }

        /// <summary>조준 보조: 놓는 중이 아닐 때, 지금 꾹 눌렀다 떼면 나비가 갈 자리를 보여 줄지 / 그 자리 / 놓을 수 있는 자리인지.</summary>
        bool AimShown { get; }
        Vector3 AimPoint { get; }
        bool AimValid { get; }

        /// <summary>지금까지 놓은 나비 수(놓을 때마다 1씩, FE가 새로 놓인 나비를 알아챈다 — 던지는 모션·날아가는 연출).</summary>
        int PlacedCount { get; }

        /// <summary>다음 나비를 놓기까지 남은 재사용 대기(초).</summary>
        float PlaceCooldownRemaining { get; }

        /// <summary>지금 바라보는 방향·시야에 가장 맞는 나비(Butterflies 번호). 없으면 -1. E를 누르면 여기로 간다.</summary>
        int TargetIndex { get; }
    }

    /// <summary>
    /// 공격 키를 꾹 누르는 입력(강공격 모으기)을 다른 기능이 대신 쓰는지(예: 붉은나비 나비 놓기).
    /// true면 CharacterAttack은 강공격을 모으지 않는다(짧게 누르는 약공격은 그대로).
    /// </summary>
    public interface IAttackHoldOverride
    {
        bool OverridesHeavyHold { get; }
    }
}
