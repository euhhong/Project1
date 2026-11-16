using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면의 3D 무대: 멀리 떨어진 곳에 캐릭터를 옆으로 늘어놓고, 카메라가 고른 캐릭터 앞으로 미끄러져 간다(넘길 때 슉슉).
    /// 카메라는 화면이 아니라 RenderTexture(Texture)에 그리고, UI가 그 그림을 모델 창에 보여 준다. 실행 중에만 만든다.
    /// </summary>
    public class CharacterSelectStage : IDisposable
    {
        private static readonly Vector3 Origin = new(10000f, 0f, 10000f);
        private const float Spacing = 3.5f;
        private const float CameraDistance = 7.5f, CameraHeight = 2.2f, LookHeight = 1.5f;
        private static readonly string[] HiddenInSelect = { "Gamekeeper_Grab_Weapon" };
        public static readonly Color BackgroundColor = new(0.05f, 0.065f, 0.09f);

        private readonly GameObject root;
        private readonly Camera camera;
        private readonly Material floorMaterial;
        private readonly List<GameObject> lineup = new();
        private int focus;
        private Vector3 velocity;

        public RenderTexture Texture { get; }

        public CharacterSelectStage(Vector2Int textureSize)
        {
            root = new GameObject("CharacterSelectStage");
            root.transform.position = Origin;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(8f, 1f, 2f);
            floor.transform.localPosition = new Vector3(-Spacing * 4f, 0f, 0f);
            UnityEngine.Object.Destroy(floor.GetComponent<Collider>());
            var floorRenderer = floor.GetComponent<Renderer>();
            floorMaterial = floorRenderer.material; // 기본 재질의 복사본 — 어둡게 칠한다
            floorMaterial.color = new Color(0.1f, 0.11f, 0.14f);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.7f;
            light.color = new Color(0.88f, 0.92f, 1f);

            Texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "CharacterSelectModel" };
            Texture.Create();

            var camGo = new GameObject("SelectCamera");
            camGo.transform.SetParent(root.transform, false);
            camera = camGo.AddComponent<Camera>();
            camera.fieldOfView = 35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.targetTexture = Texture;
        }

        /// <summary>한 진영의 캐릭터를 늘어놓고 focus 앞에 카메라를 바로 둔다.</summary>
        public void Show(List<CharacterRoster.Entry> entries, int focusIndex)
        {
            foreach (var m in lineup) UnityEngine.Object.Destroy(m);
            lineup.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var m = UnityEngine.Object.Instantiate(e.model, root.transform);
                m.name = e.id;
                m.transform.localPosition = new Vector3(-i * Spacing, 0f, 0f); // 카메라가 앞에서 보므로 -X 쪽이 화면 오른쪽
                m.transform.localRotation = Quaternion.identity; // 모델은 +Z(카메라 쪽)를 본다
                m.transform.localScale = Vector3.one * e.scale;
                // 판정 상자는 선택 화면에서 필요 없다.
                foreach (var c in m.GetComponentsInChildren<Collider>()) c.enabled = false;
                // 선택 화면에서는 들지 않는 소품(예: 사냥터지기 그랩)을 감춘다. 게임에서는 그대로 든다.
                foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                    if (Array.IndexOf(HiddenInSelect, r.name) >= 0) r.enabled = false;
                var anim = m.GetComponent<Animator>();
                if (anim != null) { anim.runtimeAnimatorController = e.selectController; anim.applyRootMotion = false; }
                lineup.Add(m);
            }
            focus = Mathf.Clamp(focusIndex, 0, Mathf.Max(lineup.Count - 1, 0));
            camera.transform.position = CameraTarget();
            velocity = Vector3.zero;
            Aim();
        }

        /// <summary>카메라가 focus 캐릭터 앞으로 미끄러져 간다(사이 캐릭터를 지나치며 슉).</summary>
        public void Focus(int index) => focus = Mathf.Clamp(index, 0, Mathf.Max(lineup.Count - 1, 0));

        /// <summary>지금 캐릭터를 제자리에서 돌린다(모델 창을 끌 때).</summary>
        public void Rotate(float degrees)
        {
            if (focus < lineup.Count) lineup[focus].transform.Rotate(0f, degrees, 0f, Space.Self);
        }

        public void Tick()
        {
            var cam = camera.transform;
            cam.position = Vector3.SmoothDamp(cam.position, CameraTarget(), ref velocity, 0.12f);
            Aim();
        }

        private Vector3 CameraTarget() => Origin + new Vector3(-focus * Spacing, CameraHeight, CameraDistance);

        private void Aim()
        {
            var cam = camera.transform;
            cam.LookAt(new Vector3(cam.position.x, Origin.y + LookHeight, Origin.z));
        }

        public void Dispose()
        {
            if (camera != null) camera.targetTexture = null;
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(floorMaterial);
            Texture.Release();
            UnityEngine.Object.Destroy(Texture);
        }
    }
}
