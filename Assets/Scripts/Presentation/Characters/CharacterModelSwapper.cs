using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 캐릭터 루트의 모델(Animator가 있는 자식)을 다른 모델로 바꾼다. 루트의 게임 스크립트는 그대로 둔다.
    /// - 새 모델은 "Model" 이름으로 루트 바로 아래에 넣고, Animator Controller를 넣는다.
    /// - CharacterController 높이·반지름·중심을 새 모델 크기에 맞춘다.
    /// 모델을 찾는 스크립트들(CharacterAnimatorDriver 등)은 Animator가 바뀌면 스스로 다시 찾는다.
    /// Hitbox 목록은 BE AttackHitDetector가 자식 변경을 감지해 다시 모은다.
    /// </summary>
    public static class CharacterModelSwapper
    {
        public static GameObject Swap(GameObject root, GameObject modelPrefab, float scale, RuntimeAnimatorController controller)
        {
            // 옛 모델: 바로 끄고 지운다(같은 프레임에 새 Animator만 찾히도록).
            foreach (var old in root.GetComponentsInChildren<Animator>(true))
            {
                if (old.transform == root.transform) continue;
                old.gameObject.SetActive(false);
                old.transform.SetParent(null, false);
                Object.Destroy(old.gameObject);
            }

            var model = Object.Instantiate(modelPrefab, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;
            var animator = model.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }
            FitController(root, model, animator);
            return model;
        }

        /// <summary>캐릭터 키는 Humanoid 머리 높이로 잰다(모자·꼬리 같은 소품 크기에 흔들리지 않게).</summary>
        private static void FitController(GameObject root, GameObject model, Animator animator)
        {
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) return;
            float rootScale = Mathf.Max(root.transform.lossyScale.y, 0.0001f);
            float height;
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null)
            {
                // 머리 뼈는 머리 아래쪽이라 머리 크기만큼 조금 더한다.
                height = (head.position.y - root.transform.position.y) * 1.12f / rootScale;
            }
            else
            {
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return;
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                height = (b.max.y - root.transform.position.y) / rootScale;
            }
            height = Mathf.Max(height, 0.5f);
            cc.height = height;
            cc.radius = Mathf.Clamp(height * 0.18f, 0.2f, height * 0.5f);
            cc.center = new Vector3(0f, height * 0.5f + cc.skinWidth, 0f);
        }
    }
}
