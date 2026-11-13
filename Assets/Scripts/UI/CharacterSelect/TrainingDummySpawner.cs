using Project1.Presentation;
using Project1.Systems;
using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 테스트용 훈련 상대: 게임을 시작하면 맵의 벽(Wall) 옆에 조금 떨어진 자리에 가만히 서 있는 감시자 하나를 세운다.
    /// - 움직이지 않고, 체력·게이지는 없다(맞으면 피격 모션만 — 공격·스킬 판정과 이펙트를 확인하는 용도).
    /// - 몸(CharacterController) + 누구인지(CharacterIdentity, 팀 없음) + 모델(맞는 판정 Hurtbox 포함) + 피격 모션.
    /// 씬을 고치지 않고 실행 중에 만든다. 벽이 없는 씬이거나 이미 있으면 만들지 않는다.
    /// </summary>
    public static class TrainingDummySpawner
    {
        public const string DummyName = "TrainingDummy";
        private const string WallName = "Wall";
        private const string ModelId = "Hunter_HellEmber"; // 공장장 모델
        private const float GapFromWall = 2f;                // 벽 끝에서 옆으로 떨어진 거리(m)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            var wall = GameObject.Find(WallName);
            if (wall == null || GameObject.Find(DummyName) != null) return;
            var roster = CharacterRoster.Load();
            var entry = roster != null ? roster.entries.Find(e => e.id == ModelId) : null;
            if (entry == null || entry.model == null) return;

            // 벽의 오른쪽 끝에서 옆으로 GapFromWall 떨어진 곳, 벽과 같은 줄(벽 앞면 쪽을 봄)
            Vector3 p = wall.transform.position + wall.transform.right * (wall.transform.lossyScale.x * 0.5f + GapFromWall);
            if (Physics.Raycast(p + Vector3.up * 10f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
            else p.y = 0f;

            var go = new GameObject(DummyName);
            go.SetActive(false);
            go.transform.SetPositionAndRotation(p + Vector3.up * 0.02f, Quaternion.LookRotation(-wall.transform.forward, Vector3.up));
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2.06f; cc.radius = 0.4f; cc.center = new Vector3(0f, 1.11f, 0f);
            go.AddComponent<CharacterIdentity>();
            go.AddComponent<CharacterHitReactionAnimator>();
            go.SetActive(true);
            CharacterModelSwapper.Swap(go, entry.model, entry.scale, entry.gameController);
        }
    }
}
