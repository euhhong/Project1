using System.Collections.Generic;
using Project1.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 맵 네 구석의 순간이동 전신거울(BG): 깎인 담장 모서리를 등지고 맵 가운데를 본다. 대각선 반대 거울끼리 짝(북서↔남동, 북동↔남서).
    /// 거울 모델(VARCO CornerMirror) + 충돌 상자 + 거울 면(반짝이는 판, 거울 앞 반사 프로브) + MirrorPortal(BE: 걸어 들어가면 짝 거울로).
    /// 거울 앞(들어가는 자리·나오는 자리)에는 흩뿌리기 소품이 놓이지 않게 MirrorSpots에 남긴다.
    /// </summary>
    public static partial class MapDresser
    {
        // (가운데 x, z, 짝) — 담장 모서리(x·z ±48, 6m 깎임) 안쪽
        private static readonly (float x, float z, string pair)[] CornerMirrors =
        {
            (-44.2f, 44.2f, "A"), (44.2f, -44.2f, "A"),
            (44.2f, 44.2f, "B"), (-44.2f, -44.2f, "B"),
        };
        private const float MirrorHeight = 3.6f, MirrorWidth = 1.6f, MirrorDepth = 0.55f;
        private const string MirrorGlassMatPath = "Assets/Environment/Materials/M_MirrorGlass.mat";

        /// <summary>거울과 그 앞(들어가는·나오는 자리) — 흩뿌리기가 피한다.</summary>
        private static readonly List<Vector3> MirrorSpots = new();

        private static void BuildMirrors(Transform g)
        {
            MirrorSpots.Clear();
            var root = new GameObject($"{ModelPrefix}CornerMirrors").transform;
            root.SetParent(g, false);
            var glassMat = GlassMaterial();
            int n = 0;
            foreach (var c in CornerMirrors)
            {
                var p = Ground(new Vector3(c.x, 0f, c.z));
                Vector3 face = new Vector3(-Mathf.Sign(c.x), 0f, -Mathf.Sign(c.z)).normalized; // 맵 가운데 쪽
                float yaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                var mirror = new GameObject($"Mirror_{c.pair}_{(c.z > 0 ? "N" : "S")}{(c.x > 0 ? "E" : "W")}");
                mirror.transform.SetParent(root, false);
                mirror.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
                // 모델: 거울 자리(폭 × 높이 × 두께)에 맞춤 — 없으면 상자
                var model = Place("CornerMirror", mirror.transform, Quaternion.identity, Vector3.zero, new Vector3(MirrorWidth, MirrorHeight, MirrorDepth), false, false, $"{ModelPrefix}CornerMirror");
                if (model == null)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = "MirrorFrame"; box.transform.SetParent(mirror.transform, false);
                    box.transform.localPosition = new Vector3(0f, MirrorHeight / 2f, 0f);
                    box.transform.localScale = new Vector3(MirrorWidth, MirrorHeight, MirrorDepth);
                    Object.DestroyImmediate(box.GetComponent<Collider>());
                }
                var col = mirror.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, MirrorHeight / 2f, 0f);
                col.size = new Vector3(MirrorWidth, MirrorHeight, MirrorDepth);
                // 거울 면: 앞에 붙인 반짝이는 판(액자 안쪽 크기) + 이 판만 비추는 반사 프로브(처음 한 번 찍음)
                var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glass.name = "MirrorGlass";
                Object.DestroyImmediate(glass.GetComponent<Collider>());
                glass.transform.SetParent(mirror.transform, false);
                glass.transform.localPosition = new Vector3(0f, MirrorHeight * GlassCenterY, MirrorDepth * GlassFront);
                glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 쿼드 앞면(-Z)이 거울 앞(+Z)을 보게
                glass.transform.localScale = new Vector3(MirrorWidth * GlassWidth, MirrorHeight * GlassHeight, 1f);
                var gr = glass.GetComponent<MeshRenderer>();
                gr.sharedMaterial = glassMat;
                gr.shadowCastingMode = ShadowCastingMode.Off;
                var probeGo = new GameObject("MirrorProbe");
                probeGo.transform.SetParent(mirror.transform, false);
                probeGo.transform.localPosition = new Vector3(0f, 1.6f, 1.2f);
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.resolution = 128;
                probe.size = new Vector3(MirrorWidth + 0.6f, MirrorHeight + 0.5f, 3f);
                probe.center = new Vector3(0f, 0f, -1.2f);
                probe.importance = 2;
                probe.boxProjection = true;
                // 순간이동(BE)
                mirror.AddComponent<MirrorPortal>().Configure(c.pair);
                GameObjectUtility.SetStaticEditorFlags(mirror, StaticEditorFlags.BatchingStatic);
                MirrorSpots.Add(p); MirrorSpots.Add(p + face * 1.4f); MirrorSpots.Add(p + face * 2.8f);
                n++;
            }
            Physics.SyncTransforms();
            Debug.Log($"[BG] 순간이동 거울 {n}개(맵 구석, 대각선 짝)");
        }

        // 거울 면 판: 모델 액자 안쪽(모델 크기 대비 비율) — CornerMirror 모델을 보고 맞춘 값
        private const float GlassWidth = 0.46f, GlassHeight = 0.7f, GlassCenterY = 0.54f, GlassFront = 0.3f;

        private static Material GlassMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MirrorGlassMatPath);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_MirrorGlass" };
            m.SetColor("_BaseColor", new Color(0.62f, 0.66f, 0.72f));
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 0.96f);
            AssetDatabase.CreateAsset(m, MirrorGlassMatPath);
            return m;
        }
    }
}
