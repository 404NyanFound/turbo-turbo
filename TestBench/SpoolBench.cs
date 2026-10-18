using System;
using System.Collections.Generic;

using Spectre.Console;
using Spectre.Console.Rendering;

using TurboTurbo.Modeling;

namespace TestBench
{
    /// <summary>
    /// Reports how the turbocharger spools up after a throttle step, and how long the engine runs overfuelled while it
    /// does.
    /// <para>
    /// A throttle step is scripted as the governor saturating: fuel per stroke sits at 1 while rpm ramps linearly to
    /// its new target, then drops to the step's settled fuel per stroke.
    /// </para>
    /// </summary>
    public sealed class SpoolBench
    {
        public const float Dt = 0.02f;

        private const float WarmUpS = 120f;
        private const float WindowS = 60f;

        private const float IdleRpm = 0.3f;
        private const float IdleFps = 0.1f;

        // DV's ThrottleGammaPowerConversion defaults: notch 0 is off and notch 1 holds idle rpm, the notches above it
        // step the goal rpm evenly up to max-power rpm, and goal power follows (notch / 7)^gamma
        private const int NotchCount = 8;
        private const float NotchGamma = 1.2f;
        private const float MaxPowerRpm = 1f;

        private static readonly (string Name, float FromRpm, float FromFps, float ToRpm, float ToFps)[] Steps =
        {
            ("idle -> cruise", IdleRpm, IdleFps, 0.7f, 0.50f),
            ("idle -> lug", IdleRpm, IdleFps, 0.6f, 1.00f),
            ("idle -> full", IdleRpm, IdleFps, 1.0f, 1.00f),
            ("cruise -> full", 0.7f, 0.50f, 1.0f, 1.00f),
            NotchStep(1, 2),
        };

        private static readonly (string Name, Action<TurboCharger.Settings> Configure)[] Profiles =
        {
            // the DE6 runs the default configuration
            ("DE6", _ => { }),
            // mirrors the DH4 turbocharger settings in StockConfiguration
            ("DH4", t =>
            {
                t.LambdaCalibration = 1.36f;
                t.TauUp = 2;
                t.TauDown = 1;
            }),
        };

        private readonly Options _options;

        public SpoolBench(Options options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public IRenderable Report(float ambientK)
        {
            var table = new Table()
                .RoundedBorder()
                .Title("Throttle steps  " +
                       $"[grey]ambient {BenchCommon.ToC(ambientK):0.#} C, idle -> rated rpm in {_options.RampS:0.#}s, " +
                       $"dt {Dt}s[/]");
            table.AddColumn(Render.Name("profile"));
            table.AddColumn(Render.Name("step"));
            foreach (var h in new[] { "boost", "t50", "t90", "overfuel", "soot", "min lambda" })
            {
                table.AddColumn(Render.Num(h));
            }

            for (var p = 0; p < Profiles.Length; p++)
            {
                var (profileName, configure) = Profiles[p];
                if (p > 0) table.AddEmptyRow();

                foreach (var step in Steps)
                {
                    var charger = new TurboCharger.Settings();
                    configure(charger);
                    charger.TauUp *= _options.SpoolScale;

                    var label = $"{profileName} [grey](tau {charger.TauUp:0.##}s)[/]";
                    AddRow(table, label, step.Name, Run(charger, step, ambientK));
                }
            }

            var (n1Rpm, n1Fps) = Notch(1);
            var (n2Rpm, n2Fps) = Notch(2);
            var notes = new Markup(
                "[grey]boost: start -> end of the " + $"{WindowS:0}s window. " +
                "t50/t90: time to 50%/90% of that rise. overfuel: time with lambda below 1. " +
                "soot: integral of overfuel * rpm dt, a soot-mass proxy. " +
                $"notch 1 -> 2: rpm {n1Rpm:0.00} -> {n2Rpm:0.00}, fuel per stroke {n1Fps:0.00} -> {n2Fps:0.00}, " +
                "under traction load at DV's default notch mapping.[/]");

            return new Rows(table, notes);
        }

        private static void AddRow(Table table, string profile, string step, Result r)
        {
            table.AddRow(
                profile,
                step,
                $"{r.StartBoost:0.00} -> {r.EndBoost:0.00}",
                Seconds(r.T50),
                Seconds(r.T90),
                Seconds(r.OverfuelS),
                $"{r.Soot:0.000}",
                r.MinLambda < 1f ? $"[yellow]{r.MinLambda:0.00}[/]" : $"{r.MinLambda:0.00}");
        }

        private static (string, float, float, float, float) NotchStep(int from, int to)
        {
            var (fromRpm, fromFps) = Notch(from);
            var (toRpm, toFps) = Notch(to);
            return ($"notch {from} -> {to}", fromRpm, fromFps, toRpm, toFps);
        }

        /// <summary>
        /// Settled rpm and fuel per stroke at a throttle notch, with the traction load absorbing the goal power. DV's
        /// engine maps the governor output linearly onto fuel per stroke (0.1 to 1) and onto torque, so a constant
        /// torque curve gives fuel per stroke = idle + (1 - idle) * power / rpm.
        /// </summary>
        private static (float Rpm, float Fps) Notch(int notch)
        {
            var rpmPerNotch = (MaxPowerRpm - IdleRpm) / (NotchCount - 2);
            var rpm = notch <= 1 ? IdleRpm : IdleRpm + rpmPerNotch * (notch - 1);
            var power = (float)Math.Pow((float)notch / (NotchCount - 1), NotchGamma);
            var fps = Math.Min(1f, IdleFps + (1f - IdleFps) * power / rpm);
            return (rpm, fps);
        }

        private static string Seconds(float s) => s < 0f ? "[dim]n/a[/]" : $"{s:0.00}s";

        private Result Run(TurboCharger.Settings chargerSettings,
            (string Name, float FromRpm, float FromFps, float ToRpm, float ToFps) step, float ambientK)
        {
            var copy = new TurboCharger.Settings(chargerSettings);
            copy.Validate();
            var combustion = new CombustionModel.Settings();
            combustion.Validate();

            var rpm = step.FromRpm;
            var fps = step.FromFps;
            var model = new CombustionModel(() => 0f, () => rpm * fps, () => rpm, () => ambientK,
                new TurboCharger(copy), combustion, new ExhaustVelocitySettings());

            // a warm engine holding the starting point, so the step starts from a settled boost and chamber
            model.SetTemperatureK(CombustionModel.Settings.ThermostatFullOpenK);
            for (var t = 0f; t < WarmUpS; t += Dt)
            {
                model.Tick(Dt, engineOn: true);
            }

            var rampRate = (1f - IdleRpm) / Math.Max(Dt, _options.RampS);
            var result = new Result { StartBoost = model.Boost, MinLambda = float.MaxValue };
            var boost = new List<float>();

            for (var t = 0f; t < WindowS; t += Dt)
            {
                var direction = Math.Sign(step.ToRpm - rpm);
                rpm = direction == 0
                    ? step.ToRpm
                    : direction > 0
                        ? Math.Min(step.ToRpm, rpm + rampRate * Dt)
                        : Math.Max(step.ToRpm, rpm - rampRate * Dt);
                // the governor holds the rack open until the engine reaches its new speed
                fps = rpm < step.ToRpm ? 1f : step.ToFps;

                model.Tick(Dt, engineOn: true);

                boost.Add(model.Boost);
                if (model.Overfuel > 0f) result.OverfuelS += Dt;
                result.Soot += model.Overfuel * rpm * Dt;
                result.MinLambda = Math.Min(result.MinLambda, model.Lambda);
            }

            result.EndBoost = model.Boost;
            result.T50 = Rise(boost, result.StartBoost, result.EndBoost, 0.5f);
            result.T90 = Rise(boost, result.StartBoost, result.EndBoost, 0.9f);
            return result;
        }

        private static float Rise(List<float> series, float start, float end, float fraction)
        {
            if (end <= start) return -1f;

            var threshold = start + fraction * (end - start);
            for (var i = 0; i < series.Count; i++)
            {
                if (series[i] >= threshold) return (i + 1) * Dt;
            }

            return -1f;
        }

        public sealed class Options
        {
            public const float DefaultRampS = 4f;

            /// <summary>Time [s] for the engine to accelerate from idle to rated speed with the rack fully open.</summary>
            public float RampS { get; set; } = DefaultRampS;

            /// <summary>Multiplier on each profile's spool-up time constant.</summary>
            public float SpoolScale { get; set; } = 1f;

            public void Validate()
            {
                RampS = Math.Max(Dt, RampS);
                SpoolScale = Math.Max(0.01f, SpoolScale);
            }
        }

        private sealed class Result
        {
            public float StartBoost;
            public float EndBoost;
            public float T50;
            public float T90;
            public float OverfuelS;
            public float Soot;
            public float MinLambda;
        }
    }
}
