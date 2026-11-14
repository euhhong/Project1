using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 캐릭터 모델을 임포트할 때 필요 없는 부분(서브메시)을 지운다. 원본 FBX는 그대로 두고, 임포트 결과에서만 지운다.
    /// - 루키노: 목둘레의 펼쳐진 목도리(목도리도마뱀 같은 회색 막, Luchino_Body의 "10 - Default" 재질). 루키노는 목도리도마뱀이 아니다(사용자 결정).
    ///   Luchino_Head의 같은 재질(턱 아래 세로 판)도 지운다.
    /// 지운 부분의 정점은 메시에 남지만 그려지지 않고, 판정 상자 계산(BodyPartColliderPostprocessor)도 그리는 삼각형의 정점만 쓴다.
    /// 판정 상자보다 먼저 돌도록 순서를 앞에 둔다.
    /// </summary>
    public class ModelPartRemover : AssetPostprocessor
    {
        private static readonly (string character, string renderer, string material)[] Removed =
        {
            ("Hunter_Luchino", "Luchino_Body", "10 - Default"),
            ("Hunter_Luchino", "Luchino_Head", "10 - Default"), // 턱 아래로 늘어져 머리에 꽂힌 것처럼 보이던 세로 판(같은 목도리의 남은 조각)
        };

        public override uint GetVersion() => 2;
        public override int GetPostprocessOrder() => -10;

        private void OnPostprocessModel(GameObject root)
        {
            string path = assetPath.Replace('\\', '/');
            foreach (var (character, rendererName, materialName) in Removed)
            {
                if (!path.Contains($"/{character}/")) continue;
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.name != rendererName || smr.sharedMesh == null) continue;
                    var mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length && i < smr.sharedMesh.subMeshCount; i++)
                    {
                        if (mats[i] == null || mats[i].name != materialName) continue;
                        smr.sharedMesh.SetTriangles(Array.Empty<int>(), i);
                        Debug.Log($"[FE] {character}: {rendererName}의 '{materialName}' 부분을 지웠습니다.");
                    }
                }
            }
        }
    }
}
