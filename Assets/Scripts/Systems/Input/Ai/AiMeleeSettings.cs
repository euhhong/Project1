using System;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>AI 근접 전투 조절 값(AiBotInput 인스펙터에 보임, AiMeleeTactics가 읽음).</summary>
    [Serializable]
    public class AiMeleeSettings
    {
        [Header("거리")]
        [Tooltip("상대가 이 거리(m) 안이면 근접 교전(여러 공격 커맨드를 섞어 씀). 밖이면 다가가거나 원거리 스킬.")]
        [Min(1f)] public float engageRadius = 5f;
        [Tooltip("이보다 멀면 달려서 다가간다(m, 스태미나가 있을 때).")]
        [Min(0f)] public float runDistance = 6f;
        [Tooltip("내 평타가 닿는 거리 추정(m): 생존자 / 감시자. 상대 사거리 추정에도 같은 값을 쓴다.")]
        [Min(0.5f)] public float survivorReach = 1.6f;
        [Min(0.5f)] public float hunterReach = 2.0f;

        [Header("사람 같은 반응")]
        [Tooltip("상대 동작을 알아채기까지 걸리는 시간(초, 매번 이 사이 무작위).")]
        [Min(0f)] public float reactionMin = 0.15f;
        [Min(0f)] public float reactionMax = 0.3f;
        [Tooltip("한 동작을 마치고 다음 동작을 고르기까지 쉬는 시간(초, 이 사이 무작위).")]
        [Min(0f)] public float decisionGapMin = 0.05f;
        [Min(0f)] public float decisionGapMax = 0.3f;
        [Tooltip("같은 동작을 연달아 고를 때 가중치 배율(작을수록 다양하게).")]
        [Range(0f, 1f)] public float repeatPenalty = 0.4f;

        [Header("교전 가중치 (클수록 자주)")]
        [Min(0f)] public float lightWeight = 3f;
        [Min(0f)] public float heavyWeight = 1.3f;
        [Min(0f)] public float runJumpWeight = 1.2f;
        [Min(0f)] public float strafeWeight = 1.5f;
        [Min(0f)] public float backOffWeight = 0.9f;
        [Min(0f)] public float feintWeight = 1f;
        [Min(0f)] public float skillWeightScale = 1f;
        [Tooltip("돌진형(전위·광대) 스킬이 준비됐을 때 들락날락 유도를 고르는 가중치.")]
        [Min(0f)] public float baitWeight = 4f;
        [Tooltip("스킬을 쓴 뒤 다시 고르기까지 최소 시간(초).")]
        [Min(0f)] public float skillGap = 2.5f;

        [Header("상대 공격에 대한 반응 (확률 가중치)")]
        [Tooltip("생존자: 정면에서 상대 공격 준비를 보면 주먹 막기(꾹) → 지나가면 떼서 돌려차기.")]
        [Min(0f)] public float parryReact = 0.5f;
        [Min(0f)] public float backOffReact = 0.35f;
        [Min(0f)] public float dodgeReact = 0.25f;
        [Tooltip("감시자: 맞받아 치기.")]
        [Min(0f)] public float tradeReact = 0.25f;

        [Header("돌진형 유도 → 박치기 (AiChargeBaiter)")]
        [Range(0f, 1f)] public float chargeOnWhiff = 0.85f;
        [Range(0f, 1f)] public float chargeOnOutOfRangeSwing = 0.7f;
        [Range(0f, 1f)] public float chargeOnParry = 0.6f;
        [Range(0f, 1f)] public float chargeOnSkillBusy = 0.8f;
        [Range(0f, 1f)] public float chargeAfterDodge = 0.6f;
        [Tooltip("틈이 없을 때 멀리서 그냥 박기까지 기다리는 시간(초, 이 사이 무작위).")]
        [Min(0f)] public float chargePatienceMin = 2.5f;
        [Min(0f)] public float chargePatienceMax = 5f;
        [Tooltip("돌진 중 시야를 돌리는 최대 속도(도/초). 작을수록 옆으로 피하면 빗나간다.")]
        [Min(0f)] public float chargeTurnRate = 100f;
    }
}
