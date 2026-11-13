using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 따라가는 캐릭터 머리 위에 천장(지붕·위층 바닥)이 있는지 보고 '실내 정도'(0 = 야외, 1 = 실내)를 부드럽게 바꾼다.
    /// ThirdPersonCamera가 이 값으로 실내 구도(가깝게·낮게)와 야외 구도를 섞는다. 카메라에 없으면 ThirdPersonCamera가 붙인다.
    /// 캐릭터·판정·트리거는 천장으로 치지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraIndoorSensor : MonoBehaviour
    {
        [Tooltip("캐릭터 발에서 위로 이 높이 안에 천장이 있으면 실내로 본다(m).")]
        [SerializeField, Min(1f)] private float ceilingCheckHeight = 9f;
        [Tooltip("천장 검사 지점을 캐릭터 둘레에 이만큼 벌려 여러 개 본다(m). 문틀 밑을 지날 때 깜빡이지 않게.")]
        [SerializeField, Min(0f)] private float sampleSpread = 0.8f;
        [Tooltip("실내↔야외로 바뀌는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.01f)] private float blendTime = 0.4f;

        private static readonly Vector3[] Offsets =
            { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private float velocity;

        /// <summary>0 = 야외, 1 = 실내(부드럽게 바뀐 값).</summary>
        public float Indoor01 { get; private set; }

        /// <summary>매 프레임 카메라가 부른다. 처음 한 번은 snap = true로 바로 맞춘다.</summary>
        public void Tick(Transform target, bool snap)
        {
            float goal = target != null ? CoveredRatio(target) : 0f;
            Indoor01 = snap ? goal : Mathf.SmoothDamp(Indoor01, goal, ref velocity, blendTime);
        }

        private float CoveredRatio(Transform target)
        {
            int covered = 0;
            foreach (var o in Offsets)
            {
                Vector3 from = target.position + Vector3.up * 0.5f + target.rotation * o * sampleSpread;
                if (HasCeiling(from, target)) covered++;
            }
            // 5곳 중 3곳 이상 덮여 있으면 실내
            return covered >= 3 ? 1f : 0f;
        }

        private bool HasCeiling(Vector3 from, Transform target)
        {
            int n = Physics.RaycastNonAlloc(from, Vector3.up, hits, ceilingCheckHeight, CameraObstacles.Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!CameraObstacles.IsIgnored(hits[i].collider, target)) return true;
            return false;
        }
    }
}
