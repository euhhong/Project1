using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 캐릭터 모델에 원래 붙어 있던 소품(무기 메시)을 다른 모델로 바꿔 보여 준다(예: 루키노 무기 → 톱날 마체테).
    /// 원래 소품은 감추기만 한다(판정 상자 등은 원래 소품 기준 그대로). 새 모델은 Resources에서 읽어 원래 소품과 같은 뼈 아래에 둔다.
    /// 자리(손잡이 = 손바닥, 날 방향, 날 끝 방향, 크기)는 모델을 가져올 때 ModelPropFixer가 원래 소품에 맞춰 정해 넣는다.
    /// 새 모델 규칙: 원점 = 쥐는 곳, +Z = 날 끝 쪽, -Y = 날(베는 쪽).
    /// 원래 소품 오브젝트에 붙는다(직접 붙일 필요 없음). 선택 화면·게임 어디서 모델을 꺼내도 똑같이 바뀐다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PropModelOverride : MonoBehaviour
    {
        [Tooltip("Resources 안 경로(예: SerratedMachete/SerratedMachete).")]
        [SerializeField] private string resource;
        [Tooltip("새 모델 자리·방향·크기(원래 소품의 부모 뼈 기준).")]
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Quaternion localRotation = Quaternion.identity;
        [SerializeField] private float localScale = 1f;

        private GameObject model;

        public void Set(string resourcePath, Vector3 position, Quaternion rotation, float scale)
        {
            resource = resourcePath; localPosition = position; localRotation = rotation; localScale = scale;
        }

        private void Awake()
        {
            var prefab = string.IsNullOrEmpty(resource) ? null : Resources.Load<GameObject>(resource);
            if (prefab == null) return; // 못 찾으면 원래 소품 그대로
            if (TryGetComponent(out Renderer own)) own.enabled = false;
            model = Instantiate(prefab, transform.parent, false);
            model.name = prefab.name;
            model.transform.SetLocalPositionAndRotation(localPosition, localRotation * prefab.transform.localRotation); // FBX 축 맞춤은 그대로
            model.transform.localScale = prefab.transform.localScale * localScale;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        private void OnEnable() { if (model != null) model.SetActive(true); }
        private void OnDisable() { if (model != null) model.SetActive(false); }
        private void OnDestroy() { if (model != null) Destroy(model); }
    }
}
