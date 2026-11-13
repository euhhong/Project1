using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 3인칭 뒤쪽 카메라. 캐릭터의 시야 값(ICharacterState.LookYaw/LookPitch)을 읽기만 해서
    /// 캐릭터 등 뒤에 자리 잡고 캐릭터를 바라본다. 마우스는 직접 읽지 않는다.
    /// 메인 카메라에 붙인다. target이 비어 있으면 씬에서 ICharacterState를 가진 첫 캐릭터를 찾는다.
    /// 머리 위에 천장이 있으면(CameraIndoorSensor) 더 가깝고 낮은 실내 구도로 부드럽게 바꾸고,
    /// 캐릭터와 카메라 사이에 벽·천장이 끼면 카메라를 캐릭터 쪽으로 당긴다(벽 너머·옆방이 보이지 않게).
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

        [Header("실내 (머리 위에 천장이 있을 때, CameraIndoorSensor가 판단)")]
        [Tooltip("실내 구도를 쓴다. 끄면 실내에서도 위 '자리' 값 그대로.")]
        [SerializeField] private bool useIndoorFraming = true;
        [Tooltip("실내에서 바라볼 지점 높이(m).")]
        [SerializeField] private float indoorPivotHeight = 1.45f;
        [Tooltip("실내에서 카메라 거리(m). 가까울수록 벽에 덜 막힌다.")]
        [SerializeField] private float indoorDistance = 3.3f;
        [Tooltip("실내 기본 내려다보는 각도(도). 낮을수록 앞(문·복도)이 멀리 보인다.")]
        [SerializeField] private float indoorPitchOffset = 14f;
        [Tooltip("실내에서 발이 놓일 화면 높이.")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float indoorFeetViewportY = 0.12f;

        [Header("벽 피하기")]
        [Tooltip("캐릭터와 카메라 사이에 벽·천장이 끼면 카메라를 캐릭터 쪽으로 당긴다.")]
        [SerializeField] private bool avoidObstacles = true;
        [Tooltip("카메라 둘레 여유(m). near clip이 벽을 뚫지 않을 만큼.")]
        [SerializeField, Min(0.05f)] private float obstacleRadius = 0.3f;
        [Tooltip("아무리 당겨도 이 거리보다는 가깝게 오지 않는다(m).")]
        [SerializeField, Min(0.1f)] private float minObstacleDistance = 0.7f;
        [Tooltip("벽이 사라진 뒤 원래 거리로 돌아가는 시간(초). 당길 때는 바로 당긴다.")]
        [SerializeField, Min(0f)] private float obstacleReleaseTime = 0.25f;

        [Header("감쇠 (초, 0이면 즉시)")]
        [SerializeField] private float followSmoothTime = 0.08f;
        [SerializeField] private float rotationSmoothTime = 0.05f;

        private ICharacterState state;
        private Camera cam;
        private CameraIndoorSensor indoorSensor;
        private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private float yaw, pitch, yawVelocity, pitchVelocity;
        private float currentDistance, distanceVelocity;
        private bool initialized;

        // 이번 프레임 구도(야외·실내를 섞은 값)
        private float curPivotHeight, curFeetViewportY;

        private void LateUpdate()
        {
            if (!EnsureTarget()) return;

            // BE-5가 있으면 시야 값을, 없으면 캐릭터 회전을 쓴다(임시 연결).
            float targetYaw = state != null ? state.LookYaw : target.eulerAngles.y;
            float lookPitch = state != null ? state.LookPitch : fallbackPitch;
            // 야외 구도와 실내 구도를 섞는다.
            float indoor = SenseIndoor(!initialized);
            curPivotHeight = Mathf.Lerp(pivotHeight, indoorPivotHeight, indoor);
            curFeetViewportY = Mathf.Lerp(feetViewportY, indoorFeetViewportY, indoor);
            float wantDistance = Mathf.Lerp(distance, indoorDistance, indoor);
            float wantPitchOffset = Mathf.Lerp(pitchOffset, indoorPitchOffset, indoor);

            float targetPitch = Mathf.Clamp(lookPitch + wantPitchOffset, minCameraPitch, maxCameraPitch);
            Vector3 targetPivot = target.position + Vector3.up * curPivotHeight;

            if (!initialized)
            {
                yaw = targetYaw; pitch = targetPitch; pivot = targetPivot;
                currentDistance = wantDistance;
                initialized = true;
            }
            else
            {
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, rotationSmoothTime);
                pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
                pivot = Vector3.SmoothDamp(pivot, targetPivot, ref pivotVelocity, followSmoothTime);
            }

            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = -(orbit * Vector3.forward);
            // 시선 각도는 벽에 막히지 않았을 때 자리 기준으로 정한다(당겨져도 화면이 갑자기 아래로 꺾이지 않게).
            Vector3 freePosition = pivot + back * wantDistance;
            Quaternion rotation = frameFeet ? FrameFeet(freePosition) : orbit;

            float allowed = avoidObstacles ? ObstacleDistance(back, wantDistance) : wantDistance;
            if (allowed < currentDistance) { currentDistance = allowed; distanceVelocity = 0f; } // 막히면 바로 당긴다
            else currentDistance = Mathf.SmoothDamp(currentDistance, allowed, ref distanceVelocity, obstacleReleaseTime);

            transform.SetPositionAndRotation(pivot + back * currentDistance, rotation);
        }

        private float SenseIndoor(bool snap)
        {
            if (!useIndoorFraming) return 0f;
            if (indoorSensor == null && !TryGetComponent(out indoorSensor))
                indoorSensor = gameObject.AddComponent<CameraIndoorSensor>();
            indoorSensor.Tick(target, snap);
            return indoorSensor.Indoor01;
        }

        /// <summary>바라볼 지점에서 카메라 쪽으로 구를 쏘아 처음 닿는 벽·천장까지 거리(여유 뺌). 막힌 게 없으면 maxDistance.</summary>
        private float ObstacleDistance(Vector3 back, float maxDistance)
        {
            int n = Physics.SphereCastNonAlloc(pivot, obstacleRadius, back, obstacleHits, maxDistance,
                CameraObstacles.Mask, QueryTriggerInteraction.Ignore);
            float nearest = maxDistance;
            for (int i = 0; i < n; i++)
            {
                var h = obstacleHits[i];
                if (CameraObstacles.IsIgnored(h.collider, target)) continue;
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // 시작부터 겹친 것(바라볼 지점이 벽 속)은 무시
                nearest = Mathf.Min(nearest, h.distance);
            }
            return Mathf.Clamp(nearest, Mathf.Min(minObstacleDistance, maxDistance), maxDistance);
        }

        /// <summary>카메라 자리 position에서 발이 화면 curFeetViewportY 높이에 오도록 상하 시선만 맞춘다.</summary>
        private Quaternion FrameFeet(Vector3 position)
        {
            if (cam == null) cam = GetComponent<Camera>();
            float halfFov = (cam != null ? cam.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad;

            Vector3 feet = pivot - Vector3.up * curPivotHeight;
            Vector3 toFeet = feet - position;
            float horizontal = new Vector2(toFeet.x, toFeet.z).magnitude;
            float feetDown = Mathf.Atan2(-toFeet.y, Mathf.Max(horizontal, 0.01f)) * Mathf.Rad2Deg;
            // 화면 중앙에서 발까지 내려가야 하는 각도
            float offset = Mathf.Atan((0.5f - curFeetViewportY) * 2f * Mathf.Tan(halfFov)) * Mathf.Rad2Deg;
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
