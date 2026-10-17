using TurboTurbo.Effects;
using TurboTurbo.Modeling;

using System.Linq;
using UnityEngine;

namespace TurboTurbo.WorkBench
{
    public class ShimmerBench : MonoBehaviour
    {
        [Header("Total exhaust energy")]
        [Range(0f, 1f)] public float exhaustEnergy = 1f;

        [Header("Background scroll speed (uv/s)")]
        public float backgroundScroll = 0.03f;

        [Header("Emitter placement")]
        public Vector3 exhaustOffset = new Vector3(0f, 0.2f, -0.05f);

        [Header("Exhaust smoke emitter")]
        public bool smokeEnabled = true;
        [Range(0.3f, 2f)] public float lambda = 1.2f;
        [Range(0f, 1f)] public float rpmNorm = 0.5f;
        [Range(0f, 100f)] public float idleEmissionRate = 15f;
        [Range(0f, 300f)] public float fullEmissionRate = 75f;

        [Header("Smoke shader")]
        [Range(0.01f, 2f)] public float texKnee = 0.15f;
        public bool atlasShoulder = true;

        [Header("Engine load signals")]
        public bool engineOn = true;
        [Range(0f, 3f)] public float vapour;
        [Range(0f, 3f)] public float massFlow = 1f;
        public float gasDensity = 0.5f;

        [Header("Movement speed")]
        [Range(0f, 8f)] public float moveSpeed = 1.5f;

        [Header("Wind (world m/s)")]
        public Vector3 wind;

        private float _lastIdleRate = float.NaN;
        private float _lastFullRate = float.NaN;

        private Renderer _background;
        private ShimmerParticles _particleEmitter;
        private SmokeParticles _smokeBench;
        private GameObject _frame;
        private WindFrame _windFrame;

        private void Start()
        {
            // this lets us move the whole bench setup so particles stay behind
            _frame = new GameObject("ReferenceFrame");

            GameObject cam = new GameObject("BenchCamera");
            Camera camComp = cam.AddComponent<Camera>();
            camComp.backgroundColor = new Color(0.15f, 0.2f, 0.3f);
            camComp.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;
            camComp.depthTextureMode |= DepthTextureMode.Depth;
            cam.tag = "MainCamera";
            cam.transform.SetParent(_frame.transform, false);

            Texture2D checker = MakeChecker(512, 16);
            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            bg.transform.position = new Vector3(0f, 0f, 10f);
            bg.transform.localScale = new Vector3(14f, 12f, 1f);
            Material bgMat = new Material(Shader.Find("Unlit/Texture"));
            bgMat.mainTexture = checker;
            bg.GetComponent<Renderer>().sharedMaterial = bgMat;
            _background = bg.GetComponent<Renderer>();
            bg.transform.SetParent(_frame.transform, false);

            var loco = GameObject.Find("LocoDE6");
            if (loco != null) loco.transform.SetParent(_frame.transform, true);

            var vanillaExhaust = loco != null
                ? loco.GetComponentsInChildren<ParticleSystem>(true)
                      .FirstOrDefault(ps => ps.name == "ExhaustEngineSmoke")
                : null;
            if (vanillaExhaust == null)
            {
                Debug.LogError("[ShimmerBench] no LocoDE6 with 'ExhaustEngineSmoke' in the scene, emitters not created");
                return;
            }

            var vanillaAtlas = vanillaExhaust.GetComponent<ParticleSystemRenderer>()?.sharedMaterial?.mainTexture;

            _windFrame = WindFrame.Create(null);

            var go = new GameObject("ShimmerParticles");
            _particleEmitter = go.AddComponent<ShimmerParticles>();
            _particleEmitter.shader = Shader.Find("TurboTurbo/HeatShimmer");
            _particleEmitter.windFrame = _windFrame;
            _particleEmitter.Configure();
            ExhaustPlacement.PlaceAt(go.transform, vanillaExhaust.transform.position,
                _frame.transform, exhaustOffset);

            if (smokeEnabled)
            {
                var smokeGo = new GameObject("SmokeParticles");
                _smokeBench = smokeGo.AddComponent<SmokeParticles>();
                _smokeBench.shader = Shader.Find("TurboTurbo/Smoke");
                _smokeBench.atlas = vanillaAtlas;
                _smokeBench.windFrame = _windFrame;
                SmokeParticles.IdleEmissionRate = idleEmissionRate;
                SmokeParticles.FullEmissionRate = fullEmissionRate;
                _smokeBench.Configure();
                _lastIdleRate = idleEmissionRate;
                _lastFullRate = fullEmissionRate;
                ExhaustPlacement.PlaceAt(smokeGo.transform, vanillaExhaust.transform.position,
                    _frame.transform, exhaustOffset);
            }
        }

        private void Update()
        {
            if (_smokeBench != null)
            {
                // the density encoding is derived from the emission rates, so rebuild when they change
                if (!Mathf.Approximately(_lastIdleRate, idleEmissionRate)
                    || !Mathf.Approximately(_lastFullRate, fullEmissionRate))
                {
                    SmokeParticles.IdleEmissionRate = idleEmissionRate;
                    SmokeParticles.FullEmissionRate = fullEmissionRate;
                    _smokeBench.Configure();
                    _lastIdleRate = idleEmissionRate;
                    _lastFullRate = fullEmissionRate;
                }

                SmokeParticles.TexKnee = texKnee;
                SmokeParticles.TexShoulder = atlasShoulder;
                _smokeBench.ApplyTexKnee();
            }

            var signals = new ExhaustSignalSet
            {
                Lambda = lambda,
                RpmNorm = rpmNorm,
                ExhaustEnergy = exhaustEnergy,
                Vapour = vapour,
                MassFlow = massFlow,
                GasDensity = gasDensity,
                EngineOn = engineOn,
                LocoVelocity = Vector3.right * moveSpeed,
            };
            if (_windFrame != null)
            {
                _windFrame.wind = wind;
            }
            signals.Apply(_smokeBench, _particleEmitter);

            Material bg = _background != null ? _background.sharedMaterial : null;
            if (bg != null && bg.mainTexture != null)
            {
                bg.mainTextureOffset = new Vector2(Time.time * backgroundScroll, 0f);
            }

            if (moveSpeed > 0.001f)
            {
                _frame.transform.position += Vector3.right * (moveSpeed * Time.deltaTime);
            }
        }

        private static Texture2D MakeChecker(int size, int cells)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[size * size];
            int cell = size / cells;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool on = ((x / cell) + (y / cell)) % 2 == 0;
                    float v = on ? 0.95f : 0.1f;
                    px[y * size + x] = new Color(v, v, v, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
