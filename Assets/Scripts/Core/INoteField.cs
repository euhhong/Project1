using System.Collections.Generic;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 안토니오가 놓은 붉은 음표들과 놓는 중인 자리, 두 음표를 잇는 전류선(읽기 전용). FE가 음표·미리보기·전류선·남은 시간을 그린다.
    /// 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface INoteField
    {
        /// <summary>좌클릭을 꾹 눌러 놓을 자리를 고르는 중.</summary>
        bool Placing { get; }

        Vector3 PreviewPoint { get; }

        /// <summary>미리보기 자리에 놓을 수 있는지(땅 위만).</summary>
        bool PreviewValid { get; }

        /// <summary>미리보기 자리가 몸 근처라 떼면 놓기가 취소되는지.</summary>
        bool PreviewCancels { get; }

        /// <summary>놓은 음표 자리(땅 위 점, 월드). 0개, 1개(둘째를 기다림), 2개(전류선으로 이어짐).</summary>
        IReadOnlyList<Vector3> Notes { get; }

        /// <summary>두 음표가 전류선으로 이어졌는지(이때 선·음표에 닿으면 맞는다).</summary>
        bool Linked { get; }

        /// <summary>첫 음표를 놓은 뒤 둘째를 놓을 수 있는 남은 시간(초). 지나면 첫 음표가 사라진다.</summary>
        float PendingTimeLeft { get; }

        float PendingDuration { get; }

        /// <summary>음표·전류선이 떠 있는 높이(땅에서, m).</summary>
        float NoteHeight { get; }

        /// <summary>전류선에 마지막으로 맞은 자리와 시각(Time.time). 없으면 시각 음수.</summary>
        Vector3 LastHitPoint { get; }

        float LastHitTime { get; }

        /// <summary>마지막으로 음표를 놓은 시각(Time.time). 없으면 음수. FE가 놓은 뒤 잠깐 바이올린을 켠다.</summary>
        float LastPlaceTime { get; }
    }
}
