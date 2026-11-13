using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 맹인 E 찌르기(MindsEye_Thrust) 동안 지팡이(MindsEye_Prop)를 손에서 정면으로 곧게 세운다:
    /// 지팡이 윗끝을 쥔 채(손 자리) 지팡이 끝이 찌르는 방향(ICharacterSkillState.Direction)을 똑바로 보게 돌려, 지팡이 길이만큼 길게 뻗는다.
    /// 준비 동안 서서히 세우고, 찌르는 동안 유지, 거두는 동안 원래 쥔 모양으로 돌아간다. 다른 때는 손 아래 원래 자리 그대로.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 맹인만). 연출 전용(판정은 BE CaneThrustSkill).
    /// </summary>
    [DisallowMultipleComponent]
    public class CaneThrustPose : MonoBehaviour
    {
        private const string SkillId = "MindsEye_Thrust";
        private const string CaneName = "MindsEye_Prop";

        private ICharacterSkillState skill;
        private Transform cane;
        private Vector3 restPos; private Quaternion restRot;
        private Vector3 gripLocal, tipLocal; // 지팡이 로컬: 쥘 끝(손 쪽 끝) / 지팡이 끝(손에서 먼 끝)

        private void LateUpdate()
        {
            if (skill == null) TryGetComponent(out skill);
            if (cane == null && !Find()) return;
            cane.SetLocalPositionAndRotation(restPos, restRot); // 매 프레임 원래 자리에서 시작(애니메이션이 소품을 움직이지 않으므로)
            float w = Weight();
            if (w <= 0f) return;

            Vector3 dir = skill.Direction.sqrMagnitude > 1e-4f ? skill.Direction.normalized : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 hand = cane.parent.position;
            Vector3 axisNow = cane.TransformDirection(tipLocal - gripLocal).normalized;          // 지금 지팡이 방향(쥔 끝 → 끝)
            Quaternion turn = Quaternion.FromToRotation(axisNow, dir);
            Quaternion targetRot = turn * cane.rotation;
            // 쥔 끝을 손에 둔다 → 지팡이 전체가 손에서 앞으로 뻗는다.
            Vector3 gripOffset = targetRot * Vector3.Scale(gripLocal, cane.lossyScale);
            Vector3 targetPos = hand - gripOffset;
            cane.SetPositionAndRotation(Vector3.Lerp(cane.position, targetPos, w), Quaternion.Slerp(cane.rotation, targetRot, w));
        }

        private float Weight()
        {
            if (skill == null || skill.State != SkillState.Casting || skill.SkillId != SkillId) return 0f;
            float p = Mathf.Clamp01(skill.MotionPhaseProgress);
            return skill.MotionPhase switch
            {
                AttackPhase.Windup => Mathf.SmoothStep(0f, 1f, p),
                AttackPhase.Active => 1f,
                AttackPhase.Recovery => Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(p * 1.6f)),
                _ => 0f,
            };
        }

        private bool Find()
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim == null) return false;
            foreach (var r in anim.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name != CaneName || !r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
                cane = r.transform;
                restPos = cane.localPosition; restRot = cane.localRotation;
                // 긴 축 양 끝 중 손(부모 뼈)에 가까운 쪽 = 쥘 끝, 먼 쪽 = 지팡이 끝.
                Bounds b = mf.sharedMesh.bounds;
                Vector3 e = b.extents;
                Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
                float half = Vector3.Dot(e, axis);
                Vector3 a = b.center + axis * half, c = b.center - axis * half;
                Vector3 handLocal = cane.InverseTransformPoint(cane.parent.position);
                bool aNear = (a - handLocal).sqrMagnitude <= (c - handLocal).sqrMagnitude;
                gripLocal = aNear ? a : c; tipLocal = aNear ? c : a;
                return true;
            }
            return false;
        }

        private void OnDisable()
        {
            if (cane != null) cane.SetLocalPositionAndRotation(restPos, restRot);
        }
    }
}
