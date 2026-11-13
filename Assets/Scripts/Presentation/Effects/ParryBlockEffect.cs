using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 막기 이펙트(FE): 주먹 막기(BE FistParry)로 막아 낸 히트(HitEvents.HitBlocked)마다 막은 자리에서 하늘·흰 별이 "챙" 하고 터진다.
    /// 맞을 때의 효과(빨간 HitFlash·피격 모션)와 구별되게 차가운 색이고, 속도선은 때린 쪽에서 막은 자리로 뻗는다(PunchBurst).
    /// 플레이를 시작하면 저절로 켜진다(붙일 것 없음).
    /// </summary>
    public static class ParryBlockEffect
    {
        public static float Size = 0.9f;
        public static float Life = 0.25f;
        public static Color Outer = new(0.35f, 0.8f, 1f, 1f);
        public static Color Inner = new(1f, 1f, 1f, 1f);

        private static Material material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => HitEvents.HitBlocked -= OnBlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Subscribe()
        {
            HitEvents.HitBlocked -= OnBlocked;
            HitEvents.HitBlocked += OnBlocked;
        }

        private static void OnBlocked(HitResult hit)
        {
            // 속도선 출발점: 때린 쪽 가슴 높이(모르면 막은 자리 → 선 없음).
            Vector3 from = hit.Point;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (c.Id != hit.AttackerId) continue;
                from = new Vector3(c.transform.position.x, hit.Point.y, c.transform.position.z);
                break;
            }
            Vector3 toward = from - hit.Point;
            Vector3 fist = toward.sqrMagnitude > 1e-4f ? hit.Point + toward.normalized * 0.45f : hit.Point;
            PunchBurst.Create(hit.Point, fist, Size, Life, Outer, Inner, GetMaterial());
        }

        private static Material GetMaterial()
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "ParryBlockEffect", renderQueue = 3100 };
            return material;
        }
    }
}
