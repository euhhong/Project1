using System.Collections.Generic;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 카우보이가 올가미를 조준하는 동안(E 꾹) 지금 던지면 물 판자에 흰 외곽선(FE). GrabAimLine이 내 캐릭터일 때만 매 프레임 Set을 부른다.
    /// 캐릭터 흰 윤곽선(CharacterOutline)과 같은 셰이더로 판자 몸(Board) 아래 메시를 따라 그린다. 다른 판자로 바뀌거나 조준을 그만두면 지운다.
    /// </summary>
    public static class PalletAimOutline
    {
        private const string LineName = "~PalletAimOutline";
        private const float WidthPixels = 2.4f;

        private static Pallet current;
        private static readonly List<Renderer> lines = new();
        private static Material material;

        /// <summary>외곽선을 그릴 판자(null = 지움).</summary>
        public static void Set(Pallet pallet)
        {
            if (pallet == current && (pallet == null || lines.Count > 0)) return;
            Clear();
            current = pallet;
            if (pallet == null) return;
            if (material == null)
            {
                var shader = Resources.Load<Shader>("CharacterOutline");
                if (shader == null) return;
                material = new Material(shader) { name = "PalletAimOutline", enableInstancing = true };
                material.SetColor("_OutlineColor", Color.white);
                material.SetFloat("_OutlineWidth", WidthPixels);
            }
            foreach (var r in pallet.Board.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.gameObject.name.StartsWith("~") || !r.enabled) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                var line = OutlineMeshCopy.Create(r, LineName, mats);
                if (line != null) lines.Add(line);
            }
        }

        private static void Clear()
        {
            foreach (var l in lines) if (l != null) Object.Destroy(l.gameObject);
            lines.Clear();
            current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() { lines.Clear(); current = null; material = null; }
    }
}
