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
        [Tooltip("캐릭터 발에서 바라볼 지점(어깨~머리)까지의 높이(m).")]
        [SerializeField] private float pivotHeight = 1.3f;
        [Tooltip("바라볼 지점에서 카메라까지의 거리(m).")]
        [SerializeField] private float distance = 4.2f;
        [Tooltip("시야 상하 각도에 더해 카메라를 더 내려다보게 하는 기본 각도(도). 시야 Pitch 0일 때 이 각도로 내려다본다.")]
        [SerializeField] private float pitchOffset = 32f;
        [Tooltip("카메라 상하 각도 제한(도). + = 내려다봄.")]
        [SerializeField] private float minCameraPitch = -10f;
        [SerializeField] private float maxCameraPitch = 75f;
        [Tooltip("시야 상태가 없을 때(임시 연결) 쓰는 시야 상하 각도(도).")]
        [SerializeField] private float fallbackPitch = 0f;

        [Header("구도")]
        [Tooltip("켜면 캐릭터 발이 항상 화면 아래쪽 일정 높이에 오도록 카메라 시선 각도를 맞춘다.")]
        [SerializeField] private bool frameFeet = true;
        [Tooltip("발이 놓일 화면 높이(0 = 맨 아래, 1 = 맨 위).")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float feetViewportY = 0.07f;

        [Header("감쇠 (초, 0이면 즉시)")]
        [SerializeField] private float followSmoothTime = 0.08f;
        [SerializeField] private float rotationSmoothTime = 0.05f;

        private ICharacterState state;
        private Camera cam;
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private float yaw, pitch, yawVelocity, pitchVelocity;
        private bool initialized;

        private void LateUpdate()
        {
            if (!EnsureTarget()) return;

            // BE-5가 있으면 시야 값을, 없으면 캐릭터 회전을 쓴다(임시 연결).
            float targetYaw = state != null ? state.LookYaw : target.eulerAngles.y;
            float lookPitch = state != null ? state.LookPitch : fallbackPitch;
            float targetPitch = Mathf.Clamp(lookPitch + pitchOffset, minCameraPitch, maxCameraPitch);
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

            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 position = pivot - orbit * Vector3.forward * distance;
            Quaternion rotation = frameFeet ? FrameFeet(position) : orbit;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>카메라 자리는 그대로 두고, 발이 화면 feetViewportY 높이에 오도록 상하 시선만 맞춘다.</summary>
        private Quaternion FrameFeet(Vector3 position)
        {
            if (cam == null) cam = GetComponent<Camera>();
            float halfFov = (cam != null ? cam.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad;

            Vector3 feet = pivot - Vector3.up * pivotHeight;
            Vector3 toFeet = feet - position;
            float horizontal = new Vector2(toFeet.x, toFeet.z).magnitude;
            float feetDown = Mathf.Atan2(-toFeet.y, Mathf.Max(horizontal, 0.01f)) * Mathf.Rad2Deg;
            // 화면 중앙에서 발까지 내려가야 하는 각도
            float offset = Mathf.Atan((0.5f - feetViewportY) * 2f * Mathf.Tan(halfFov)) * Mathf.Rad2Deg;
            return Quaternion.Euler(feetDown - offset, yaw, 0f);
        }

        /// <summary>감쇠 없이 바로 제자리로 옮긴다. 에디터에서 시작 화면을 맞출 때도 쓴다.</summary>
        public void SnapNow()
        {
            initialized = false;
            LateUpdate();
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
