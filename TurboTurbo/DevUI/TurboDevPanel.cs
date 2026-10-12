using TurboTurbo.Assets;
using TurboTurbo.Configuration;
using TurboTurbo.Runtime;
using TurboTurbo.Runtime.Environment;
using TurboTurbo.WorkBench;

using UnityEngine;

namespace TurboTurbo.DevUI;

internal sealed class TurboDevPanel : MonoBehaviour
{
    private int _selected;
    private readonly Logger _log = Log.ForContext("devpanel");

    private string _diagLine = "orchestrator: ?\nspawner: ?";
    private float _diagTimer;

    private float _densityFalloff = SmokeParticles.DensityFalloff;
    private float _softParticlesFade = SmokeParticles.SoftParticlesFade;
    private float _fadeInSeconds = SmokeParticles.FadeInSeconds;
    private float _texKnee = SmokeParticles.TexKnee;
    private bool _texShoulder = SmokeParticles.TexShoulder;
    private float _maxShadowFloor = SmokeParticles.Settings.DefaultMaxShadowFloor;
    private float _forceTempC = 20f;

    private TurboTooltipLayer _tooltip;

    public Rect WindowRect { get; private set; } = new(20f, 20f, 540f, 180f);

    public static TurboDevPanel Create(Rect initialRect)
    {
        var go = new GameObject(Naming.Create("DevPanel"));
        DontDestroyOnLoad(go);
        var panel = go.AddComponent<TurboDevPanel>();
        panel._tooltip = go.AddComponent<TurboTooltipLayer>();
        panel.WindowRect = initialRect;
        go.AddComponent<WindowBlocker>().Track(() => panel.WindowRect);
        return panel;
    }

    private void OnEnable()
    {
        PickDefaultTarget();
    }

    private void Update()
    {
        RefreshDiagnostics();
    }

    private void OnGUI()
    {
        WindowRect = GUILayout.Window(GetInstanceID(), WindowRect, DrawWindow, "TurboTurbo Dev UI");
    }

    private void DrawWindow(int id)
    {
        DrawTargetSelector();
        GUILayout.Space(6f);
        DrawDiagnostics();
        GUILayout.Space(6f);
        DrawAmbient();
        GUILayout.Space(6f);
        DrawTelemetry();
        GUILayout.Space(4f);
        DrawForceTemperature();
        GUILayout.Space(4f);
        DrawDensityFalloff();
        GUILayout.Space(4f);
        DrawSoftParticlesFade();
        GUILayout.Space(4f);
        DrawFadeInSeconds();
        GUILayout.Space(4f);
        DrawMaxShadowFloor();
        GUILayout.Space(4f);
        DrawTexShoulder();
        GUILayout.Space(4f);
        DrawTexKnee();
        GUILayout.Space(4f);
        DrawDiagnosticsButtons();

        // GUI.tooltip is only populated during repaint; capture then, so
        // other event passes don't overwrite it.
        if (Event.current.type == EventType.Repaint)
        {
            _tooltip.Tooltip = GUI.tooltip;
        }

        GUI.DragWindow();
    }

    private void RefreshDiagnostics()
    {
        _diagTimer -= Time.deltaTime;
        if (_diagTimer > 0f) return;
        _diagTimer = 1f;

        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null)
        {
            _diagLine = "orchestrator: no instance\nspawner: ?";
            return;
        }

        var assets = ModAssets.ShadersValid && GameAssets.SmokeAtlas != null ? "ok" : "lost";
        _diagLine = $"orchestrator: alive (id {orchestrator.GetInstanceID()}); assets: {assets}\n" +
                    orchestrator.DescribeDiagnostics();
    }

    private void DrawDiagnosticsButtons()
    {
        if (GUILayout.Button("inspect car (dump particle systems to log)"))
        {
            DumpParticleSystems();
        }
        if (GUILayout.Button("dump player train to log"))
        {
            PlayerTrainInspector.DumpTarget();
        }
        if (GUILayout.Button("dump all liveries to log"))
        {
            PlayerTrainInspector.DumpAllLiveries();
        }
    }

    private void DumpParticleSystems()
    {
        var host = CurrentHost();
        if (host == null)
        {
            _log.Warn("no car targeted for inspection");
            return;
        }

        // the host lives on the car root, so this always finds the TrainCar
        var car = host.GetComponent<TrainCar>();
        ParticleSystemInspector.Dump(car);
    }

    private EngineSimulationHost CurrentHost()
    {
        var hosts = Orchestrator.Instance.Hosts;
        return hosts.Count == 0 ? null : hosts[_selected];
    }

    private void PickDefaultTarget()
    {
        var hosts = Orchestrator.Instance.Hosts;
        _selected = 0;
        for (var i = 0; i < hosts.Count; i++)
        {
            if (hosts[i].TrainCar == PlayerManager.Car)
            {
                _selected = i;
                break;
            }
        }
    }

    private void DrawTargetSelector()
    {
        var hosts = Orchestrator.Instance.Hosts;
        if (hosts.Count == 0)
        {
            GUILayout.Label("no loco tracked");
            return;
        }

        if (_selected >= hosts.Count) _selected = hosts.Count - 1;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(26f)))
        {
            _selected = (_selected + hosts.Count - 1) % hosts.Count;
        }
        GUILayout.Label($"{_selected + 1}/{hosts.Count}   {hosts[_selected].CarId}");
        if (GUILayout.Button(">", GUILayout.Width(26f)))
        {
            _selected = (_selected + 1) % hosts.Count;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawDiagnostics()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(_diagLine);
        GUILayout.EndVertical();
    }

    private static void DrawAmbient()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(new GUIContent(
                $"Ambient: {AmbientTemperature.Celsius:0.0} C   ({AmbientTemperature.Kelvin:0} K)   source: {SourceLabel(AmbientTemperature.Source)}",
                "Ambient temperature driving intake, gas density and stack cooling."));
        GUILayout.EndVertical();
    }

    private static string SourceLabel(AmbientSource source) => source switch
    {
        AmbientSource.Seasons => "DV Seasons",
        AmbientSource.Builtin => "builtin",
        _ => "reference"
    };

    private void DrawTelemetry()
    {
        var hosts = Orchestrator.Instance.Hosts;
        if (hosts.Count == 0) return;

        var host = hosts[_selected];

        GUILayout.BeginVertical(GUI.skin.box);

        if (!host.Bound)
        {
            GUILayout.Label($"{host.CarId}: sim not bound yet");
            GUILayout.EndVertical();
            return;
        }

        var m = host.CombustionModel;
        GUILayout.Label($"{host.CarId}   engineOn: {host.EngineOn}");
        GUILayout.Label($"governor {m.GovernorNorm:0.000}   fuel/time {m.FuelNorm:0.000}   fuel/stroke {m.FuelPerStroke:0.000}");
        GUILayout.Label($"charge {m.Charge:0.000}   boost {m.Boost:0.000}");
        GUILayout.Label($"lambda {m.Lambda:0.000}   rpm {m.RpmNorm:0.000}");
        GUILayout.Label($"exhaustEnergy {m.ExhaustEnergy:0.000}   massFlow {m.MassFlow:0.000}");
        GUILayout.Label($"EGT (man) {m.ManifoldTempK - PhysicsConstants.KelvinOffset:0.0}   " +
                        $"(dt) {m.TurbineTemperatureDropK:0.0}   " +
                        $"(exh) {m.GasTemperature - PhysicsConstants.KelvinOffset:0.0} C");
        GUILayout.Label($"intake {m.IntakeTempK - PhysicsConstants.KelvinOffset:0.0} C   " +
                        $"compression {m.CompressionTempK - PhysicsConstants.KelvinOffset:0.0} C");
        GUILayout.Label($"gasDensity {m.GasDensity:0.000}");
        GUILayout.Label($"burn air {m.BurnFractionAir:0.000}   burn temp {m.BurnFractionTemp:0.000}   burn {m.BurnFraction:0.000}");
        GUILayout.Label($"cylTemp {m.CylinderTempK - PhysicsConstants.KelvinOffset:0.0} C   blockTemp {m.EngineTempK - PhysicsConstants.KelvinOffset:0.0} C   thermostat {m.ThermostatOpen:0.00}");
        GUILayout.Label($"exhaustVelocity {m.ExhaustVelocity:0.00} m/s");
        GUILayout.Label($"absSpeed {host.AbsSpeed:0.0} m/s ({host.AbsSpeed * PhysicsConstants.MpsToKmh:0.0} km/h)");

        var stack = host.Stack;
        GUILayout.Label($"stackWall {stack.ExhaustWallTempK - PhysicsConstants.KelvinOffset:0.0} C   " +
                        $"wetStack {stack.WetStack:0.00}   vapour {stack.Vapour:0.00}");
        GUILayout.Label($"deposit {stack.DepositRate:0.000}/s   slip {stack.SlipRate:0.000}/s   " +
                        $"evap {stack.EvaporateRate:0.000}/s   overflow {stack.Overflow:0.000}/s");

        for (var i = 0; i < host.Exhausts.Count; i++)
        {
            var e = host.Exhausts[i];
            GUILayout.Label($"exhaust {i}: smoke {e.Smoke.ParticleCount} p, " +
                            $"shimmer {e.Shimmer.ParticleCount} p");
        }

        GUILayout.EndVertical();
    }

    private void DrawForceTemperature()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Force temperature: {_forceTempC:0} C",
                "Sets the engine block, cylinder and stack wall to this temperature."),
            GUILayout.Width(170f));
        _forceTempC = GUILayout.HorizontalSlider(_forceTempC, -20f, 80f);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("apply to current loco"))
        {
            ApplyForceTemperature();
        }
        GUILayout.EndVertical();
    }

    private void ApplyForceTemperature()
    {
        var host = CurrentHost();
        host?.SetTemperaturesK(PhysicsConstants.KelvinOffset + _forceTempC);
    }

    private void DrawDensityFalloff()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Density falloff: {_densityFalloff:0.00}",
                "Exponent of the size-based density falloff."),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_densityFalloff, 0.5f, 4f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _densityFalloff))
        {
            _densityFalloff = next;
            SmokeParticles.DensityFalloff = next;
            ApplyDensityFalloff();
        }
        GUILayout.EndVertical();
    }

    private void ApplyDensityFalloff()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.ApplyDensityFalloff();
            }
        }
    }

    private void DrawSoftParticlesFade()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Soft particles: {_softParticlesFade:0.00} m",
                "Distance over which smoke fades out as it approaches opaque geometry."),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_softParticlesFade, 0f, 1f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _softParticlesFade))
        {
            _softParticlesFade = next;
            SmokeParticles.SoftParticlesFade = next;
            ApplySoftParticlesFade();
        }
        GUILayout.EndVertical();
    }

    private void ApplySoftParticlesFade()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.ApplySoftParticlesFade();
            }
        }
    }

    private void DrawFadeInSeconds()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Fade-in: {_fadeInSeconds:0.00} s",
                "Absolute time for a newly spawned smoke particle to reach full opacity, independent of lifetime."),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_fadeInSeconds, 0f, 0.25f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _fadeInSeconds))
        {
            _fadeInSeconds = next;
            SmokeParticles.FadeInSeconds = next;
            ApplyFadeInSeconds();
        }
        GUILayout.EndVertical();
    }

    private void ApplyFadeInSeconds()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.ApplyFadeInSeconds();
            }
        }
    }

    private void DrawMaxShadowFloor()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Shadow floor: {_maxShadowFloor:0.00}",
                "Upper bound for the shadow ramp floor"),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_maxShadowFloor, 0f, 1f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _maxShadowFloor))
        {
            _maxShadowFloor = next;
            ApplyMaxShadowFloor();
        }
        GUILayout.EndVertical();
    }

    private void ApplyMaxShadowFloor()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.SetMaxShadowFloor(_maxShadowFloor);
            }
        }
    }

    private void DrawTexShoulder()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        var next = GUILayout.Toggle(_texShoulder, new GUIContent(
            "Enable soft shoulder"));
        if (next != _texShoulder)
        {
            _texShoulder = next;
            SmokeParticles.TexShoulder = next;
            ApplyTexKnee();
        }
        GUILayout.EndVertical();
    }

    private void DrawTexKnee()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent($"Atlas knee: {_texKnee:0.00}",
                "Soft shoulder lifting the mid-grey atlas toward white before the shadow ramp. Smaller = stronger."),
            GUILayout.Width(170f));
        var next = GUILayout.HorizontalSlider(_texKnee, 0.01f, 2f);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(next, _texKnee))
        {
            _texKnee = next;
            SmokeParticles.TexKnee = next;
            ApplyTexKnee();
        }
        GUILayout.EndVertical();
    }

    private void ApplyTexKnee()
    {
        var orchestrator = Orchestrator.Instance;
        if (orchestrator == null) return;

        foreach (var host in orchestrator.Hosts)
        {
            foreach (var e in host.Exhausts)
            {
                e.Smoke?.ApplyTexKnee();
            }
        }
    }
}
