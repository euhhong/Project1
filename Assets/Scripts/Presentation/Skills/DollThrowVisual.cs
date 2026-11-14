using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공장장 인형 던지기 연출(IDollThrow, BE DollThrow를 읽어 그린다).
    /// - 좌클릭을 꾹 누르는 동안: 던지면 인형이 날아갈 포물선 보조선 + 떨어질 자리에 이중 원과 위로 솟는 빛기둥. 원·빛기둥은 벽·바닥에 가려도 맵을 뚫고 또렷하게 보이고(ZTest Always),
    ///   포물선은 가려진 구간이 흐리게 비쳐 보인다. 못 던지는 곳 = 붉게. 떨어져 있는 인형의 바닥 원도 맵 너머에서 보인다.
    /// - 인형이 날아가는 동안: 지금 E로 바꾸면 떨어져 내릴 바닥(인형 바로 아래)에 작은 하늘색 원이 인형을 따라 움직인다(맵 너머에서도 보임, 아래가 바닥이 아니면 없음).
    /// - 던진 인형: 날아가는 동안은 작은 검은 구체 — 흐릿한 연기 막·속도 방향으로 늘어남·번지는 꼬리로 또렷한 공처럼 안 보이게(flyAsPuppet을 켜면 예전처럼 꼭두각시 실루엣), 땅에 닿아 멈춰 있으면 감시자보다 큰(키 ×1.25) 꼭두각시 인형 모델(Resources EvilPuppet — 팔을 내린 자세, 공장장 쪽을 봄) + 바닥에 옅은 원.
    /// - E로 자리 바꾸기를 기다리는 동안(SwapPending) 몸(모델)을 경련하듯 떤다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만). 그린 것은 캐릭터를 따라 움직이지 않게 따로 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DollThrowVisual : MonoBehaviour
    {
        [Tooltip("인형 구체 지름(m) — BE 판정 반지름(0.18)의 두 배 근처.")]
        [SerializeField, Min(0.05f)] private float dollSize = 0.36f;
        [SerializeField] private Color dollColor = new(0.04f, 0.03f, 0.03f, 1f);
        [Tooltip("포물선 보조선 굵기(m) / 색.")]
        [SerializeField, Min(0.005f)] private float arcWidth = 0.05f;
        [SerializeField] private Color arcColor = new(1f, 0.55f, 0.2f, 0.75f);
        [SerializeField] private Color invalidColor = new(1f, 0.25f, 0.2f, 0.6f);
        [Tooltip("떨어질 자리 원 반지름(m) / 색.")]
        [SerializeField, Min(0.05f)] private float ringRadius = 0.6f;
        [SerializeField] private Color ringColor = new(1f, 1f, 1f, 0.7f);
        [Tooltip("떨어질 자리에서 위로 솟는 빛기둥 높이(m) — 맵 너머에서도 보인다.")]
        [SerializeField, Min(0f)] private float beaconHeight = 3f;
        [Tooltip("날아가는 동안 지금 바꾸면 떨어져 내릴 바닥 표시: 원 반지름(m) / 색(떨어질 자리 원과 구분).")]
        [SerializeField, Min(0.05f)] private float dropRingRadius = 0.4f;
        [SerializeField] private Color dropColor = new(0.35f, 0.9f, 1f, 0.9f);
        [Tooltip("멈춰 있는 인형 모델(꼭두각시)의 키(m) / 팔을 내리는 정도(0 = T자세, 1 = 몸에 붙임).")]
        [SerializeField, Min(0.1f)] private float puppetHeight = 1f;
        [Tooltip("멈춰 있는 인형은 공장장(감시자) 키의 이 배율 — 감시자보다 크게. puppetHeight보다 작으면 puppetHeight.")]
        [SerializeField, Min(0.1f)] private float puppetOverOwner = 1.25f;
        [SerializeField, Range(0f, 1f)] private float puppetArmsDown = 0.85f;
        [Tooltip("날아가는 인형: 꼭두각시를 칙칙하고 어두운 실루엣으로(잘 안 보이게) — 키(m) / 색 / 구르는 빠르기(도/초).")]
        [SerializeField, Min(0.1f)] private float flyerHeight = 0.7f;
        [SerializeField] private Color flyerColor = new(0.07f, 0.065f, 0.06f, 1f);
        [SerializeField] private float flyerTumble = 420f;
        [Tooltip("날아가는 인형을 꼭두각시 실루엣으로 할지(끄면 작은 검은 구체 — 흐릿한 연기 막·속도 방향으로 늘어남·꼬리로 번져 보이게).")]
        [SerializeField] private bool flyAsPuppet = false;
        [Tooltip("날아가는 구체: 연기 막 크기(구체 대비) / 진하기, 속도에 따라 늘어나는 정도(m/s당), 꼬리 길이(초).")]
        [SerializeField, Min(1f)] private float hazeScale = 1.9f;
        [SerializeField, Range(0f, 1f)] private float hazeAlpha = 0.35f;
        [SerializeField, Min(0f)] private float stretchPerSpeed = 0.05f;
        [SerializeField, Min(0f)] private float trailTime = 0.22f;
        [Tooltip("자리 바꾸기를 기다리는 동안 몸을 떠는 세기(m) / 기울기(도) / 빠르기.")]
        [SerializeField, Min(0f)] private float shakeOffset = 0.05f, shakeAngle = 7f;
        [SerializeField, Min(1f)] private float shakeRate = 38f;
        private const string PuppetResource = "EvilPuppet/EvilPuppet";

        private IDollThrow doll;
        private Transform root;
        private LineRenderer arc, ring, restRing, ringInner, beacon, arcHidden, dropRing, dropLine;
        private static readonly RaycastHit[] DropHits = new RaycastHit[16];
        private static Material seeThroughMat;
        private GameObject ball, puppet, flyer;
        private TrailRenderer trail;
        private Transform haze;
        private Vector3 lastBallPos;
        private bool wasFlying;
        private Transform shakeT;
        private Vector3 shakeBasePos;
        private Quaternion shakeBaseRot;
        private Vector3 tumbleAxis = Vector3.right;
        private float tumble;
        private bool wasResting;
        private const int RingSegments = 40;

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        private void OnDisable()
        {
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (root != null) root.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            doll ??= GetComponent<IDollThrow>();
            if (doll == null) return;
            if (root == null) Build();

            // 겨누는 중: 포물선 + 떨어질 자리 원
            bool aiming = doll.Aiming && doll.AimArc.Count > 1;
            arc.gameObject.SetActive(aiming);
            ring.gameObject.SetActive(aiming);
            ringInner.gameObject.SetActive(aiming);
            beacon.gameObject.SetActive(aiming);
            arcHidden.gameObject.SetActive(aiming);
            if (aiming)
            {
                var pts = doll.AimArc;
                arc.positionCount = pts.Count;
                arcHidden.positionCount = pts.Count;
                for (int i = 0; i < pts.Count; i++) { arc.SetPosition(i, pts[i]); arcHidden.SetPosition(i, pts[i]); }
                Color c = doll.AimValid ? arcColor : invalidColor;
                arc.startColor = new Color(c.r, c.g, c.b, c.a * 0.4f); arc.endColor = c;
                arcHidden.startColor = new Color(c.r, c.g, c.b, 0.12f); arcHidden.endColor = new Color(c.r, c.g, c.b, 0.35f); // 가려진 구간은 흐리게
                // 떨어질 자리: 맵을 뚫고 보이는 또렷한 이중 원 + 위로 솟는 빛기둥(나비 놓기 미리보기처럼 확실히)
                Color rc = doll.AimValid ? ringColor : invalidColor;
                rc.a = 0.95f;
                ring.startColor = ring.endColor = rc;
                ringInner.startColor = ringInner.endColor = new Color(rc.r, rc.g, rc.b, 0.6f);
                float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.06f;
                SetRing(ring, doll.AimLanding, ringRadius * pulse, Time.time * 60f);
                SetRing(ringInner, doll.AimLanding, ringRadius * 0.45f / pulse, Time.time * -90f);
                beacon.SetPosition(0, doll.AimLanding + Vector3.up * 0.05f);
                beacon.SetPosition(1, doll.AimLanding + Vector3.up * beaconHeight);
                beacon.startColor = new Color(rc.r, rc.g, rc.b, 0.8f); beacon.endColor = new Color(rc.r, rc.g, rc.b, 0f);
            }

            // 인형: 날아가는 동안 칙칙한 실루엣(꼭두각시를 어둡게, 데굴데굴 구름), 멈춰 있으면 꼭두각시 모델 + 바닥 원. 모델이 없으면 구체.
            bool resting = doll.DollActive && !doll.DollFlying, flying = doll.DollActive && doll.DollFlying;
            ball.SetActive((flying && flyer == null) || (resting && puppet == null));
            UpdateBlur(flying && flyer == null);
            if (ball.activeSelf) ball.transform.position = doll.DollPosition;
            if (flyer != null)
            {
                if (flying && !flyer.activeSelf)
                {
                    // 던진 순간: 던지는 쪽을 보고, 몸 옆축으로 앞으로 구른다
                    tumble = 0f;
                    tumbleAxis = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                    if (tumbleAxis.sqrMagnitude < 1e-4f) tumbleAxis = Vector3.right;
                }
                flyer.SetActive(flying);
                if (flying)
                {
                    tumble += flyerTumble * Time.deltaTime;
                    Quaternion rot = Quaternion.AngleAxis(tumble, tumbleAxis) * Quaternion.LookRotation(Vector3.Cross(tumbleAxis, Vector3.up), Vector3.up);
                    flyer.transform.SetPositionAndRotation(doll.DollPosition - rot * (Vector3.up * flyerHeight * 0.5f), rot); // 몸 가운데가 인형 자리
                }
            }
            if (puppet != null)
            {
                puppet.SetActive(resting);
                if (resting)
                {
                    Vector3 feet = doll.DollPosition - Vector3.up * (dollSize * 0.5f);
                    if (!wasResting)
                    {
                        // 멈춘 순간 공장장 쪽을 본다(자리를 바꿔도 다음에 멈출 때 다시)
                        Vector3 toOwner = transform.position - feet; toOwner.y = 0f;
                        puppet.transform.rotation = toOwner.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toOwner.normalized, Vector3.up) : Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);
                    }
                    puppet.transform.position = feet;
                }
            }
            wasResting = resting;
            restRing.gameObject.SetActive(resting);
            if (resting) SetRing(restRing, doll.DollPosition - Vector3.up * (dollSize * 0.5f), ringRadius * 1.3f, Time.time * -40f); // 큰 인형 둘레
            UpdateDropMarker(flying);
            Shake();
        }

        private void Build()
        {
            root = new GameObject("HellEmberDoll").transform;
            arc = Line("AimArc", arcColor, false);
            arc.widthMultiplier = arcWidth;
            ring = Line("AimRing", ringColor, true);
            ring.positionCount = RingSegments;
            ring.widthMultiplier = 0.1f;
            SeeThrough(ring);                                   // 떨어질 자리: 벽·바닥에 가려도 맵을 뚫고 또렷하게
            ringInner = Line("AimRingInner", ringColor, true);
            ringInner.positionCount = RingSegments;
            ringInner.widthMultiplier = 0.05f;
            SeeThrough(ringInner);
            beacon = Line("AimBeacon", ringColor, false);        // 떨어질 자리에서 위로 솟는 빛기둥(멀리서도 보이게)
            beacon.positionCount = 2;
            beacon.widthMultiplier = 0.08f;
            SeeThrough(beacon);
            arcHidden = Line("AimArcThrough", arcColor, false);  // 포물선 중 가려진 구간: 흐리게 비쳐 보임
            arcHidden.widthMultiplier = arcWidth * 0.8f;
            SeeThrough(arcHidden);
            restRing = Line("DollRing", new Color(1f, 0.45f, 0.15f, 0.45f), true);
            restRing.positionCount = RingSegments;
            restRing.widthMultiplier = 0.08f;
            SeeThrough(restRing);                               // 떨어져 있는 인형 자리도 맵 너머에서 보이게
            dropRing = Line("DropRing", dropColor, true);         // 날아가는 동안: 지금 바꾸면 떨어져 내릴 바닥(인형 바로 아래) — 작은 하늘색 원
            dropRing.positionCount = RingSegments;
            dropRing.widthMultiplier = 0.06f;
            SeeThrough(dropRing);
            dropLine = Line("DropLine", dropColor, false);
            dropLine.positionCount = 2;
            dropLine.widthMultiplier = 0.025f;
            SeeThrough(dropLine);

            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Doll";
            Destroy(ball.GetComponent<Collider>()); // 보이기만(판정은 BE)
            ball.transform.SetParent(root, false);
            ball.transform.localScale = Vector3.one * dollSize;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard")) { color = dollColor };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            ball.GetComponent<MeshRenderer>().sharedMaterial = mat;
            ball.SetActive(false);
            float ownerHeight = TryGetComponent(out CharacterController occ) ? occ.height * transform.lossyScale.y : 2.7f;
            puppet = BuildPuppet("DollPuppet", Mathf.Max(puppetHeight, ownerHeight * puppetOverOwner), null); // 감시자보다 큰 인형
            var dull = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "DollSilhouette", color = flyerColor };
            if (dull.HasProperty("_Smoothness")) dull.SetFloat("_Smoothness", 0f);
            if (dull.HasProperty("_Metallic")) dull.SetFloat("_Metallic", 0f);
            if (flyAsPuppet) flyer = BuildPuppet("DollFlyer", flyerHeight, dull);
            BuildBlur();
        }

        /// <summary>인형 모델: 꼭두각시(Resources EvilPuppet, 키 height, 원점 = 발 사이 바닥), 팔을 T자세에서 내린다. silhouette이 있으면 그 한 색으로(날아가는 실루엣). 없으면 null(구체로 대신).</summary>
        private GameObject BuildPuppet(string name, float height, Material silhouette)
        {
            var prefab = Resources.Load<GameObject>(PuppetResource);
            if (prefab == null) return null;
            var go = Instantiate(prefab, root);
            go.name = name;
            if (silhouette != null) // 날아가는 인형: 무늬 없이 칙칙한 한 색(실루엣), 그림자도 흐리게
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = silhouette;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            // 보이기만(판정은 BE): 판정 컴포넌트(Collider가 있어야 하는 Hitbox·Hurtbox)를 먼저 바로 지운 뒤 Collider를 지운다.
            foreach (var hb in go.GetComponentsInChildren<Project1.Systems.Hitbox>(true)) DestroyImmediate(hb);
            foreach (var hb in go.GetComponentsInChildren<Project1.Systems.Hurtbox>(true)) DestroyImmediate(hb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            float h = 0f;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - go.transform.position.y);
            if (h > 1e-3f) go.transform.localScale = Vector3.one * (height / h);
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                LowerArm(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
                LowerArm(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
                animator.enabled = false; // 움직이는 모션 없이 그 자세로 서 있다
            }
            go.SetActive(false);
            return go;
        }

        /// <summary>T자세로 옆으로 뻗은 위팔을 아래로(puppetArmsDown만큼) 내린다 — 팔 전체가 같이 돈다.</summary>
        private void LowerArm(Animator animator, HumanBodyBones upper, HumanBodyBones lower)
        {
            Transform u = animator.GetBoneTransform(upper), l = animator.GetBoneTransform(lower);
            if (u == null || l == null) return;
            Vector3 along = l.position - u.position;
            if (along.sqrMagnitude < 1e-8f) return;
            Vector3 side = Vector3.ProjectOnPlane(along, Vector3.up).normalized;
            Vector3 want = Vector3.Slerp(side, (Vector3.down + side * 0.15f).normalized, puppetArmsDown);
            u.rotation = Quaternion.FromToRotation(along, want) * u.rotation;
        }

        /// <summary>날아가는 구체를 흐릿하게: 둘레에 반투명 연기 막(두 겹), 뒤로 번지는 꼬리.</summary>
        private void BuildBlur()
        {
            var smoke = new Material(Shader.Find("Sprites/Default"));
            haze = new GameObject("Haze").transform;
            haze.SetParent(ball.transform, false);
            foreach (var (scale, alpha) in new[] { (hazeScale, hazeAlpha), (hazeScale * 1.45f, hazeAlpha * 0.4f) })
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(haze, false);
                s.transform.localScale = Vector3.one * scale;
                var r = s.GetComponent<MeshRenderer>();
                r.sharedMaterial = smoke;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", new Color(dollColor.r + 0.03f, dollColor.g + 0.025f, dollColor.b + 0.02f, alpha));
                r.SetPropertyBlock(block);
            }
            trail = ball.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = dollSize * 1.3f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.material = smoke;
            trail.startColor = new Color(dollColor.r, dollColor.g, dollColor.b, 0.55f);
            trail.endColor = new Color(dollColor.r, dollColor.g, dollColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
        }

        /// <summary>날아가는 동안: 구체를 속도 방향으로 늘이고(번져 보임) 연기 막이 일렁이며, 꼬리를 남긴다. 던진 순간엔 지난 꼬리를 지운다.</summary>
        private void UpdateBlur(bool flyingBall)
        {
            if (trail == null) return;
            if (flyingBall && !wasFlying) { lastBallPos = doll.DollPosition; trail.Clear(); }
            trail.emitting = flyingBall;
            haze.gameObject.SetActive(flyingBall);
            if (flyingBall)
            {
                ball.transform.position = doll.DollPosition;
                Vector3 vel = Time.deltaTime > 1e-5f ? (doll.DollPosition - lastBallPos) / Time.deltaTime : Vector3.zero;
                float speed = vel.magnitude;
                ball.transform.rotation = speed > 0.1f ? Quaternion.LookRotation(vel / speed) : Quaternion.identity;
                ball.transform.localScale = new Vector3(dollSize, dollSize, dollSize * (1f + speed * stretchPerSpeed));
                float wobble = 1f + Mathf.Sin(Time.time * 23f) * 0.08f;
                haze.localScale = new Vector3(wobble, 1f / wobble, 1f);
            }
            else ball.transform.SetPositionAndRotation(ball.transform.position, Quaternion.identity);
            if (!flyingBall) ball.transform.localScale = Vector3.one * dollSize;
            lastBallPos = doll.DollPosition;
            wasFlying = flyingBall;
        }

        /// <summary>E를 눌러 자리 바꾸기를 기다리는 동안(SwapPending) 몸(모델)을 경련하듯 떤다 — 점점 세게. 끝나면 원래 자리로.</summary>
        private void Shake()
        {
            bool on = doll.SwapPending;
            if (on && shakeT == null)
            {
                var anim = GetComponentInChildren<Animator>();
                if (anim == null) return;
                shakeT = anim.transform;
                shakeBasePos = shakeT.localPosition; shakeBaseRot = shakeT.localRotation;
            }
            if (shakeT == null) return;
            if (!on)
            {
                shakeT.SetLocalPositionAndRotation(shakeBasePos, shakeBaseRot);
                shakeT = null;
                return;
            }
            float amp = Mathf.Lerp(0.5f, 1f, doll.SwapProgress);
            float t = Time.time * shakeRate;
            Vector3 off = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 0.6f, Mathf.PerlinNoise(t, 7.7f) - 0.5f) * (2f * shakeOffset * amp);
            Vector3 ang = new Vector3(Mathf.PerlinNoise(t, 3.3f) - 0.5f, Mathf.PerlinNoise(5.1f, t) - 0.5f, Mathf.PerlinNoise(t, 9.9f) - 0.5f) * (2f * shakeAngle * amp);
            shakeT.SetLocalPositionAndRotation(shakeBasePos + off, shakeBaseRot * Quaternion.Euler(ang));
        }

        /// <summary>
        /// 인형이 날아가는 동안: 지금 E로 자리를 바꾸면 공장장이 떨어져 내릴 바닥(인형 바로 아래로 쏴서 처음 닿는 걸을 수 있는 면)에 작은 하늘색 원 + 인형에서 내려오는 가는 선.
        /// 매 프레임 인형을 따라가고 맵 너머에서도 보인다. 아래가 바닥이 아니면(허공·맵 밖) 띄우지 않는다. 인형이 멈추면 사라진다(멈춘 인형 원이 대신).
        /// </summary>
        private void UpdateDropMarker(bool flying)
        {
            bool show = false;
            Vector3 ground = default;
            if (flying)
            {
                Vector3 from = doll.DollPosition;
                int n = Physics.RaycastNonAlloc(from, Vector3.down, DropHits, 200f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var h = DropHits[i];
                    if (h.distance >= best || h.collider is CharacterController || h.collider.GetComponentInParent<Project1.Systems.CharacterIdentity>() != null) continue;
                    if (h.collider.gameObject.layer == LayerMask.NameToLayer("Hurtbox") || h.collider.gameObject.layer == LayerMask.NameToLayer("Hitbox")) continue;
                    best = h.distance; ground = h.point; show = h.normal.y > 0.6f;
                }
            }
            dropRing.gameObject.SetActive(show);
            dropLine.gameObject.SetActive(show);
            if (!show) return;
            SetRing(dropRing, ground, dropRingRadius * (1f + Mathf.Sin(Time.time * 9f) * 0.08f), Time.time * 120f);
            dropLine.SetPosition(0, doll.DollPosition);
            dropLine.SetPosition(1, ground + Vector3.up * 0.04f);
            dropLine.startColor = new Color(dropColor.r, dropColor.g, dropColor.b, 0.05f);
            dropLine.endColor = new Color(dropColor.r, dropColor.g, dropColor.b, 0.6f);
        }

        /// <summary>벽·바닥·지형에 가려도 맵을 뚫고 보이게(깊이 검사 없이 맨 위에 그림 — 히트박스 디버그 선과 같은 Hidden/Internal-Colored, ZTest Always).</summary>
        private static void SeeThrough(LineRenderer lr)
        {
            if (seeThroughMat == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return; // 없으면 보통 선 그대로
                seeThroughMat = new Material(shader) { name = "DollMarkerSeeThrough", hideFlags = HideFlags.DontSave };
                seeThroughMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                seeThroughMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                seeThroughMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                seeThroughMat.SetInt("_ZWrite", 0);
                seeThroughMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                seeThroughMat.SetColor("_Color", Color.white);
                seeThroughMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            }
            lr.sharedMaterial = seeThroughMat;
        }

        private LineRenderer Line(string name, Color color, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = 0.06f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.gameObject.SetActive(false);
            return lr;
        }

        // 바닥 원, spin(도)만큼 돌린다.
        private static void SetRing(LineRenderer lr, Vector3 center, float radius, float spin)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments * 360f + spin) * Mathf.Deg2Rad;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * 0.04f);
            }
        }
    }
}
