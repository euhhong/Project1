using System.Collections.Generic;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공장장 분신 겉모습(FE). 공장장(캐릭터 루트)에 붙어 있다가, 이 공장장이 분신을 불러내면(CloneEvents.Spawned):
    /// - 분신에 공장장과 같은 모델·모션을 넣는다(CharacterModelSwapper). 이동·공격·피격 모션 컴포넌트도 붙인다.
    /// - 온몸을 검은 바탕 + 타오르는 용암 균열로(LavaCloneLook). 상어 무기는 숨기고(판정도 끔),
    ///   오른손에 갈고리 낫(Resources/HellEmber_CloneScythe, Blender로 만든 모델)을 쥐여 주고 낫 날에 무기 판정(Hitbox_Weapon)을 단다.
    /// - 몸에서 불티가 조금씩 오르고, 사라질 때(CloneEvents.Despawning) 불티·연기가 터진다.
    /// 모델은 SetModel로 받는다(CharacterLoadout, 공장장만).
    /// </summary>
    [DisallowMultipleComponent]
    public class HellEmberCloneVisual : MonoBehaviour
    {
        private const string ScytheResource = "HellEmber_CloneScythe";

        private GameObject model;
        private float scale = 1f;
        private RuntimeAnimatorController controller;
        private CharacterIdentity identity;
        private static Material ironMat, bladeMat, edgeMat, wrapMat, emberMat;
        private readonly HashSet<GameObject> mine = new();

        public void SetModel(GameObject modelPrefab, float modelScale, RuntimeAnimatorController animatorController)
        {
            model = modelPrefab; scale = modelScale; controller = animatorController;
        }

        private void OnEnable()
        {
            CloneEvents.Spawned += OnSpawned;
            CloneEvents.Despawning += OnDespawning;
        }

        private void OnDisable()
        {
            CloneEvents.Spawned -= OnSpawned;
            CloneEvents.Despawning -= OnDespawning;
        }

        private void OnSpawned(GameObject clone, CharacterIdentity owner)
        {
            if (identity == null) identity = GetComponent<CharacterIdentity>();
            if (owner != identity || model == null) return;
            mine.Add(clone);
            CharacterModelSwapper.Swap(clone, model, scale, controller);
            Ensure<CharacterAnimatorDriver>(clone);
            Ensure<CharacterAttackAnimator>(clone);
            Ensure<CharacterBodyTurn>(clone);
            Ensure<CharacterHitReactionAnimator>(clone);

            var animator = clone.GetComponentInChildren<Animator>();
            var scythe = AttachScythe(clone, animator);
            var look = clone.AddComponent<LavaCloneLook>();
            look.Apply(scythe != null ? new HashSet<Renderer>(scythe.GetComponentsInChildren<Renderer>()) : null);
            AddEmbers(clone, animator);
            Burst(clone.transform.position + Vector3.up * 1f, 18); // 나타날 때 불티
        }

        private void OnDespawning(GameObject clone)
        {
            if (!mine.Remove(clone)) return;
            Burst(clone.transform.position + Vector3.up * 1f, 30);
        }

        /// <summary>상어 무기를 숨기고(판정도 끔) 오른손에 갈고리 낫을 쥐여 준다. 낫 날을 따라 무기 판정을 단다.</summary>
        private GameObject AttachScythe(GameObject clone, Animator animator)
        {
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null) return null;
            Vector3 palm = hand.position;
            var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            if (middle != null) palm = Vector3.Lerp(hand.position, middle.position, 0.5f);

            // 상어 무기: 쥔 방향(손바닥 → 무기 가운데)을 낫 자루 방향으로 쓰고 숨긴다
            Vector3 up = Vector3.up;
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.EndsWith("_Weapon")) continue;
                Vector3 d = r.bounds.center - palm;
                if (d.sqrMagnitude > 1e-4f) up = d.normalized;
                r.enabled = false;
                foreach (var hb in r.GetComponentsInChildren<Hitbox>(true)) hb.gameObject.SetActive(false);
            }
            var prefab = Resources.Load<GameObject>(ScytheResource);
            if (prefab == null) return null;

            // 받침: 위(+Y) = 자루(손잡이 → 날), 앞(+Z) = 날이 휘어 나가는 쪽. 모델은 날이 -Z로 나 있어 Y로 180도 돌려 넣는다.
            var pivot = new GameObject("CloneScythe").transform;
            pivot.SetParent(hand, false);
            Vector3 fwd = Vector3.ProjectOnPlane(clone.transform.forward, up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
            pivot.SetPositionAndRotation(palm, Quaternion.LookRotation(fwd.normalized, up));
            float s = hand.lossyScale.x;
            pivot.localScale = Vector3.one / Mathf.Max(s, 1e-5f); // 월드 크기 1(모델 그대로 1.13m)
            var mesh = Instantiate(prefab, pivot, false);
            mesh.name = "HellEmber_CloneScythe_Weapon";
            mesh.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * prefab.transform.localRotation;
            mesh.transform.localPosition = Vector3.zero;
            SetMaterials(mesh);

            // 무기 판정: 날(자루 끝에서 앞으로 휜 초승달)을 덮는 캡슐(받침 기준: 위 0.62~1.0m, 앞 0~0.6m)
            var hgo = new GameObject("Hitbox_Weapon");
            hgo.transform.SetParent(pivot, false);
            hgo.transform.localPosition = new Vector3(0f, 0.72f, 0.3f);
            hgo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var cap = hgo.AddComponent<CapsuleCollider>();
            cap.direction = 2; cap.radius = 0.2f; cap.height = 0.75f; cap.isTrigger = true;
            hgo.AddComponent<Hitbox>().SetSlot(Project1.Core.HitboxSlot.Weapon);
            return pivot.gameObject;
        }

        private static void SetMaterials(GameObject scythe)
        {
            if (ironMat == null)
            {
                ironMat = Lit("CloneScythe_Iron", new Color(0.035f, 0.032f, 0.03f), 0.55f, 0.9f, 0f);
                bladeMat = Lit("CloneScythe_Blade", new Color(0.05f, 0.04f, 0.035f), 0.65f, 0.95f, 0f);
                edgeMat = Lit("CloneScythe_HotEdge", new Color(0.9f, 0.18f, 0.05f), 0.5f, 0f, 5f);
                wrapMat = Lit("CloneScythe_Wrap", new Color(0.16f, 0.07f, 0.04f), 0.1f, 0f, 0f);
            }
            foreach (var r in scythe.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "";
                    mats[i] = n.Contains("HotEdge") ? edgeMat : n.Contains("Blade") ? bladeMat : n.Contains("Wrap") ? wrapMat : ironMat;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private static Material Lit(string name, Color c, float smooth, float metal, float glow)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (glow > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * glow); }
            return m;
        }

        /// <summary>몸(가슴 높이)에서 불티가 조금씩 오른다(분신을 따라다님).</summary>
        private static void AddEmbers(GameObject clone, Animator animator)
        {
            var go = new GameObject("CloneEmbers");
            Transform chest = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            go.transform.SetParent(chest != null ? chest : clone.transform, false);
            go.transform.localPosition = chest != null ? Vector3.zero : Vector3.up * 1.2f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.2f, 0.05f));
            main.gravityModifier = -0.15f;
            main.maxParticles = 120;
            var em = ps.emission; em.rateOverTime = 18f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.45f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.3f; noise.frequency = 1.5f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = EmberMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        /// <summary>한 자리에서 불티가 사방으로 터진다(나타날 때·사라질 때). 스스로 지워진다.</summary>
        private static void Burst(Vector3 at, int count)
        {
            var go = new GameObject("CloneBurst");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(0.9f, 0.15f, 0.05f));
            main.gravityModifier = 0.4f;
            main.loop = false;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.5f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = EmberMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Emit(count);
            Destroy(go, 1.5f);
        }

        private static Material EmberMaterial()
        {
            if (emberMat != null) return emberMat;
            emberMat = new Material(Shader.Find("Sprites/Default")) { name = "CloneEmber", mainTexture = MistBladeTrail.PuffTexture() };
            return emberMat;
        }

        private static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }
}
