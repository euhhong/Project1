using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 거울 순간이동 연출(FE): 거울로 들어가기 시작하면(IMirrorTraveler.Entering) 몸 둘레에서 보랏빛 은색 리본(MirrorRibbon)이 앞의 거울로 빨려 들어가고,
    /// 반대 거울에서 나오면(TravelCount가 늘면) 거울 앞에서 리본이 바깥으로 퍼진다. 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DisallowMultipleComponent]
    public class MirrorTravelEffect : MonoBehaviour
    {
        private IMirrorTraveler traveler;
        private bool wasEntering;
        private int lastCount;

        private void Start()
        {
            traveler = GetComponent<IMirrorTraveler>();
            if (traveler != null) lastCount = traveler.TravelCount;
        }

        private void Update()
        {
            if (traveler == null) return;
            bool entering = traveler.Entering;
            if (entering && !wasEntering) Burst(inward: true); // 몸이 거울을 보고 들어간다 → 리본이 몸 둘레에서 앞(거울)으로 모인다
            if (traveler.TravelCount != lastCount) { lastCount = traveler.TravelCount; Burst(inward: false); } // 나옴: 몸 앞(거울 바깥)으로 퍼짐
            wasEntering = entering;
        }

        private void Burst(bool inward)
        {
            Vector3 center = transform.position + Vector3.up * 1.1f, fwd = transform.forward;
            for (int i = 0; i < MirrorRibbon.Count; i++)
            {
                float a = i / (float)MirrorRibbon.Count * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                Vector3 ring = new Vector3(Mathf.Cos(a), Random.Range(-0.9f, 0.9f), Mathf.Sin(a)) * 0.55f;
                if (inward) MirrorRibbon.Spawn(center + ring, (fwd * 1.6f - ring * 1.2f) * 2.6f); // 몸 둘레 → 앞의 거울로 빨려 듦
                else MirrorRibbon.Spawn(center + fwd * 0.4f, (fwd * 0.9f + ring) * 4f);            // 거울 앞 → 바깥으로 퍼짐
            }
        }
    }
}
