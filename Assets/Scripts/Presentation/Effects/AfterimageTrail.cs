using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 잔상: 스킬이 잔상을 켜면(ICharacterSkillState.ShowAfterimage, 예: 용병 초고속 도약) 일정 간격으로
    /// 지금 자세를 그대로 굳힌 반투명 복사본을 남기고, 복사본은 점점 옅어지며 사라진다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout). 모델이 바뀌어도 그때의 SkinnedMeshRenderer를 찾아 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AfterimageTrail : MonoBehaviour
    {
        [Tooltip("잔상을 남기는 간격(초).")]
        [SerializeField, Min(0.01f)] private float interval = 0.035f;
        [Tooltip("잔상이 사라지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.05f)] private float fadeTime = 0.3f;
        [Tooltip("잔상 색. 모델 모양 그대로가 아니라 푸른 그림자처럼 보이게 어둡고 반투명한 파랑.")]
        [SerializeField] private Color color = new(0.08f, 0.25f, 0.9f, 0.45f);

        private ICharacterSkillState skill;
        private float nextAt;
        private static Material material;

        private void Awake() => skill = GetComponent<ICharacterSkillState>();

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<ICharacterSkillState>();
            if (skill == null || !skill.ShowAfterimage) return;
            if (Time.time < nextAt) return;
            nextAt = Time.time + interval;
            Snapshot();
        }

        private void Snapshot()
        {
            var parts = new List<(Mesh mesh, Matrix4x4 matrix)>();
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || smr.sharedMesh == null) continue;
                var mesh = new Mesh { name = "Afterimage" };
                // 크기를 빼고 굽는다: 이 모델들은 렌더러 크기가 400배라 크기를 넣으면 메시가 0으로 줄어든다. 크기를 빼면 실제 크기로 나온다.
                smr.BakeMesh(mesh, false);
                // 잔상 재질(Sprites/Default)은 정점 색을 곱하므로 흰색으로 채운다(굳힌 메시에는 정점 색이 없다).
                var white = new Color32[mesh.vertexCount];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = white;
                parts.Add((mesh, Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one)));
            }
            if (parts.Count > 0) AfterimageGhost.Create(parts, fadeTime, color, GetMaterial());
        }

        private static Material GetMaterial()
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "Afterimage", renderQueue = 3050 };
            return material;
        }
    }
}
