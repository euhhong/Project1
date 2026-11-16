using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면의 3D 무대: 캐릭터를 옆으로 늘어놓고, 카메라가 고른 캐릭터 앞으로 미끄러져 간다(넘길 때 슉슉).
    /// 카메라는 배경이 투명한 RenderTexture(Texture)에 그리고, UI가 그 그림을 화면에 그대로 얹는다(따로 창 없이).
    /// 캐릭터 선택 씬의 "SelectStage" 오브젝트(카메라·조명 — 에디터에서 옮기거나 조정 가능)를 쓰고, 없으면 실행 중에 만든다(EnsureSceneObjects). 모델은 실행 중에만 늘어놓는다.
    /// </summary>
    public class CharacterSelectStage : IDisposable
    {
        public const string RootName = "SelectStage", CameraName = "SelectCamera", LightName = "Light", FillLightName = "FillLight", ScreenCameraName = "ScreenClearCamera";
        private readonly Vector3 Origin;
        private readonly bool ownsRoot;
        private const float Spacing = 6f; // 그림을 칸보다 넓게 그리므로(튀어나온 부분까지) 옆 캐릭터가 같이 보이지 않게 멀리
        private const float CameraDistance = 6.5f, CameraHeight = 1.9f, LookHeight = 1.0f, FieldOfView = 25f; // 살짝 위에서 내려다봄, 감시자(키 약 2.3m)까지 그림 높이에 꽉 차게
        /// <summary>모델이 보는 방향: 화면 왼쪽으로 30° 돌린 3/4 모습(제5인격 캐릭터 소개 각도).</summary>
        private const float ShowcaseYaw = 30f;
        private static readonly string[] HiddenInSelect = { "Gamekeeper_Grab_Weapon" };
        /// <summary>모델 그림에서 발이 닿는 높이(위에서부터 0~1). UI가 여기에 그림자를 깐다.</summary>
        public static readonly float FeetFromTop = 0.5f + 0.5f * Mathf.Tan(Mathf.Atan(CameraHeight / CameraDistance) - Mathf.Atan((CameraHeight - LookHeight) / CameraDistance)) / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

        private readonly GameObject root;
        private readonly Camera camera;
        private readonly List<GameObject> lineup = new();
        private int focus;
        private Vector3 velocity;

        public RenderTexture Texture { get; }

        /// <param name="slotHeightRatio">그림 높이 ÷ 모델 칸 높이. 칸보다 크게 그려 튀어나온 부분도 보이게 할 때 1보다 크다(모델 크기·발 위치는 그대로).</param>
        /// <param name="slotHeightRatio">그림 높이 ÷ 모델 칸 높이. 칸보다 크게 그려 튀어나온 부분도 보이게 할 때 1보다 크다(모델 크기·발 위치는 그대로).</param>
        /// <param name="sceneRoot">캐릭터 선택 씬의 SelectStage. 없으면 멀리 떨어진 곳에 새로 만든다.</param>
        public CharacterSelectStage(Vector2Int textureSize, float slotHeightRatio = 1f, Transform sceneRoot = null)
        {
            ownsRoot = sceneRoot == null;
            if (ownsRoot) { sceneRoot = new GameObject(RootName).transform; sceneRoot.position = new Vector3(10000f, 0f, 10000f); }
            root = sceneRoot.gameObject;
            Origin = sceneRoot.position;
            EnsureSceneObjects(sceneRoot);

            Texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "CharacterSelectModel" };
            Texture.Create();
            camera = sceneRoot.Find(CameraName).GetComponent<Camera>();
            camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * slotHeightRatio) * Mathf.Rad2Deg; // 칸 기준 화각을 그림 크기만큼 넓힘(모델 크기는 칸과 같게)
            camera.targetTexture = Texture;
        }

        /// <summary>
        /// 무대에 필요한 카메라·조명을 root 아래에 맞춘다(이미 있으면 그대로 두고 없는 것만 만든다). 에디터에서 캐릭터 선택 씬을 만들 때도 쓴다.
        /// - SelectCamera: 모델만 투명 배경 그림(RenderTexture)으로 찍는다. 화각·그림은 실행 중에 정한다.
        /// - Light / FillLight: 위 왼쪽에서 비추는 빛 + 앞에서 비추는 약한 보조광.
        /// - ScreenClearCamera: 아무것도 찍지 않고 화면만 지운다(화면에 그리는 카메라가 없으면 에디터 Game 창이 "No cameras rendering"을 UI 위에 띄움).
        /// </summary>
        public static void EnsureSceneObjects(Transform root)
        {
            T Child<T>(string name, out bool created) where T : Component
            {
                var t = root.Find(name);
                created = t == null;
                if (created) { t = new GameObject(name).transform; t.SetParent(root, false); }
                return t.TryGetComponent(out T c) ? c : t.gameObject.AddComponent<T>();
            }

            var light = Child<Light>(LightName, out bool newLight);
            if (newLight)
            {
                light.transform.localRotation = Quaternion.Euler(35f, -20f, 0f);
                light.type = LightType.Directional;
                light.intensity = 0.95f;
                light.color = new Color(0.88f, 0.92f, 1f);
            }

            var fill = Child<Light>(FillLightName, out bool newFill);
            if (newFill)
            {
                fill.transform.localRotation = Quaternion.Euler(10f, 160f, 0f);
                fill.type = LightType.Directional;
                fill.intensity = 0.45f;
                fill.color = new Color(0.8f, 0.86f, 1f);
                fill.shadows = LightShadows.None;
            }

            var cam = Child<Camera>(CameraName, out bool newCam);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear; // 배경 투명 — 화면 배경 위에 캐릭터만 얹힌다
            cam.allowHDR = false; // HDR 중간 버퍼는 알파가 없어 배경이 투명하지 않게 된다
            if (newCam) { cam.fieldOfView = FieldOfView; cam.transform.localPosition = new Vector3(0f, CameraHeight, CameraDistance); cam.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, LookHeight - CameraHeight, -CameraDistance)); }

            var screenCam = Child<Camera>(ScreenCameraName, out _);
            screenCam.cullingMask = 0;
            screenCam.clearFlags = CameraClearFlags.SolidColor;
            screenCam.backgroundColor = Color.black;
            screenCam.depth = -100f;
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
                m.transform.localRotation = Quaternion.Euler(0f, ShowcaseYaw, 0f); // 모델 +Z(얼굴) = 카메라 쪽에서 화면 왼쪽(+X)으로 돌린 3/4 모습
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
            if (ownsRoot) UnityEngine.Object.Destroy(root);
            else foreach (var m in lineup) if (m != null) UnityEngine.Object.Destroy(m);
            Texture.Release();
            UnityEngine.Object.Destroy(Texture);
        }
    }
}
