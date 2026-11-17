using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 캐릭터 한 명의 효과음(FE): SfxPlayer가 캐릭터마다 저절로 붙인다.
    /// 휘두르기(공격 시작) · 점프 · 착지 · 기절(0.8초 넘게) · 스킬 시작 · 판자 쓰러짐(IPalletUser) · 거울 들어감·나옴(IMirrorTraveler).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSfx : MonoBehaviour
    {
        private const float PalletCrashDelay = 0.38f; // 판자를 밀고 넘어가 땅에 닿을 즈음(Pallet.dropDelay + 넘어가는 시간)

        private CharacterAttack attack;
        private CharacterMotor motor;
        private CharacterSkill skill;
        private CharacterStun stun;
        private IPalletUser pallet;
        private IMirrorTraveler mirror;
        private int palletCount, travelCount;
        private bool wasEntering;
        private float palletCrashAt = -1f, lastLand;

        private Vector3 Body => transform.position + Vector3.up;

        private void Start()
        {
            if (TryGetComponent(out attack)) attack.AttackStarted += OnAttack;
            if (TryGetComponent(out motor)) { motor.Jumped += OnJump; motor.Landed += OnLand; }
            if (TryGetComponent(out skill)) skill.SkillStarted += OnSkill;
            pallet = GetComponent<IPalletUser>();
            mirror = GetComponent<IMirrorTraveler>();
            if (pallet != null) palletCount = pallet.DropCount;
            if (mirror != null) travelCount = mirror.TravelCount;
        }

        private void OnDestroy()
        {
            if (attack != null) attack.AttackStarted -= OnAttack;
            if (motor != null) { motor.Jumped -= OnJump; motor.Landed -= OnLand; }
            if (skill != null) skill.SkillStarted -= OnSkill;
            if (stun != null) stun.Stunned -= OnStunned;
        }

        private void Update()
        {
            if (stun == null && TryGetComponent(out stun)) stun.Stunned += OnStunned; // 기절은 처음 맞을 때 붙는다
            if (pallet == null) pallet = GetComponent<IPalletUser>();
            if (mirror == null) mirror = GetComponent<IMirrorTraveler>();
            if (pallet != null && pallet.DropCount != palletCount) { palletCount = pallet.DropCount; palletCrashAt = Time.time + PalletCrashDelay; }
            if (palletCrashAt > 0f && Time.time >= palletCrashAt) { palletCrashAt = -1f; if (!Overrides("PalletDrop")) SfxPlayer.Play("pallet", transform.position + transform.forward * 1.2f + Vector3.up * 0.5f, 1f); }
            if (mirror != null)
            {
                if (mirror.Entering && !wasEntering) SfxPlayer.Play("mirror_in", Body, 0.9f, 0.03f);
                wasEntering = mirror.Entering;
                if (mirror.TravelCount != travelCount) { travelCount = mirror.TravelCount; SfxPlayer.Play("mirror_out", Body, 0.9f, 0.03f); }
            }
        }

        private void OnAttack(CharacterAttackState s)
        {
            if (s == CharacterAttackState.HeavyCharge || s == CharacterAttackState.None) return; // 모으기 시작은 소리 없음(휘두를 때만)
            if (TryGetComponent(out CharacterSoundSet own) && own.OverridesSwing) return; // 캐릭터 전용 휘두르기 소리가 있으면 그것만
            // 휘두르기(헛스윙) 소리 = 피격음(hit_char) 앞의 '휙'만 잘라 낸 것(swing_attack — 뒤의 맞는 부분은 뺌)
            SfxPlayer.Play("swing_attack", Body, 0.7f, 0.08f, s == CharacterAttackState.Heavy ? 0.85f : 1f);
        }
        private void OnJump() { if (!Overrides("Jump")) SfxPlayer.Play("jump", transform.position, 0.35f, 0.12f); }
        private void OnLand()
        {
            if (Time.time - lastLand < 0.25f) return; // 계단·턱에서 잇달아 나지 않게
            lastLand = Time.time;
            if (Overrides("Land")) return; // 캐릭터 전용 착지 소리(재앙의 도마뱀)
            SfxPlayer.Play("land", transform.position, 0.45f, 0.1f);
        }
        private bool Overrides(string kind) => TryGetComponent(out CharacterSoundSet own) && own.Has(kind);
        private void OnSkill() => SfxPlayer.Play("skill", Body, 0.7f, 0.05f);
        private void OnStunned(float seconds) { } // 기절 소리는 병아리 삐약 소리(StunChickSound)만 — 예전 일반 기절음(stun, 삐이이)은 쓰지 않음
    }
}
