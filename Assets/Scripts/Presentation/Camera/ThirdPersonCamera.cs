using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 3인칭 뒤쪽 카메라. 캐릭터의 시야 값(ICharacterState.LookYaw/LookPitch)을 읽기만 해서
    /// 캐릭터 등 뒤에 자리 잡고 캐릭터를 바라본다. 마우스는 직접 읽지 않는다.
    /// 메인 카메라에 붙인다. target이 비어 있으면 씬에서 ICharacterState를 가진 첫 캐릭터를 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Tooltip("따라갈 캐릭터 루트. 비워 두면 자동으로 찾는다.")]
        [SerializeField] private Transform target;

        [Header("자리")]
        [Tooltip("캐릭터 발에서 바라볼 지점까지의 높이(m).")]
        [SerializeField] private float pivotHeight = 1.4f;
        [Tooltip("바라볼 지점에서 카메라까지의 거리(m).")]
        [SerializeField] private float distance = 3.5f;
        [Tooltip("시야 상태가 없을 때(임시 연결) 쓰는 상하 각도(도).")]
        [SerializeField] private float fallbackPitch = 15f;

        [Header("감쇠 (초, 0이면 즉시)")]
        [SerializeField] private float followSmoothTime = 0.08f;
        [SerializeField] private float rotationSmoothTime = 0.05f;

        private ICharacterState state;
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private float yaw, pitch, yawVelocity, pitchVelocity;
        private bool initialized;

        private void LateUpdate()
        {
            if (!EnsureTarget()) return;

            // BE-5가 있으면 시야 값을, 없으면 캐릭터 회전을 쓴다(임시 연결).
            float targetYaw = state != null ? state.LookYaw : target.eulerAngles.y;
            float targetPitch = state != null ? state.LookPitch : fallbackPitch;
            Vector3 targetPivot = target.position + Vector3.up * pivotHeight;

            if (!initialized)
            {
                yaw = targetYaw; pitch = targetPitch; pivot = targetPivot;
                initialized = true;
            }
            else
            {
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, rotationSmoothTime);
                pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
                pivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity, followSmoothTime);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }

        private bool EnsureTarget()
        {
            if (target != null)
            {
                if (state == null) state = target.GetComponent<ICharacterState>();
                return true;
            }

            foreach (var behaviour in FindObjectsByType<MonoBehaviour>())
            {
                if (behaviour is ICharacterState found)
                {
                    state = found;
                    target = behaviour.transform;
                    initialized = false;
                    return true;
                }
            }
            return false;
        }
    }
}
