using System;
using System.Globalization;

using UnityEngine;

namespace TurboTurbo.Modeling
{
    public class ExhaustSmokeModel
    {
        public sealed class Settings
        {
            internal const float DefaultDensity = 150f;

            // we use hex here for easy comparison with stored values, as those are hex-serialized too
            internal const string DefaultColorIdleHazeHex = "9E9678FF";
            internal const string DefaultColorCleanBurnHex = "737373FF";
            internal const string DefaultColorHeavySootHex = "0D0D0DFF";
            internal const string DefaultColorWetStackHex = "FFFFF2FF";
            internal const string DefaultColorOilBurnHex = "7085D9FF";

            public Color ColorIdleHaze = ColorHex.Parse(DefaultColorIdleHazeHex);
            public Color ColorCleanBurn = ColorHex.Parse(DefaultColorCleanBurnHex);
            public Color ColorHeavySoot = ColorHex.Parse(DefaultColorHeavySootHex);
            public Color ColorWetStack = ColorHex.Parse(DefaultColorWetStackHex);
            public Color ColorOilBurn = ColorHex.Parse(DefaultColorOilBurnHex);

            public float Density = DefaultDensity;

            internal const float DefaultCleanMinHeatAlpha = 0.002f;
            internal const float DefaultCleanMaxHeatAlpha = 0.08f;
            internal const float DefaultCleanBurnHeat = 0.2f;
            internal const float DefaultSootOnsetLambda = 1.3f;
            internal const float DefaultSootOpaqueLambda = 1.05f;
            internal const float DefaultSootCurveExponent = 2f;
            internal const float DefaultSootIncreaseTau = 0.08f;
            internal const float DefaultSootDecreaseTau = 0.4f;
            internal const float DefaultSootMaxAlpha = 0.95f;
            internal const float DefaultSootPowerFloor = 0.1f;
            internal const float DefaultSootPowerExponent = 1f;
            internal const float DefaultWetStackMaxAlpha = 0.4f;
            internal const float DefaultOilTintStrength = 0.3f;
            internal const float DefaultOilRpmExponent = 2.5f;

            public float CleanMinHeatAlpha = DefaultCleanMinHeatAlpha;

            public float CleanMaxHeatAlpha = DefaultCleanMaxHeatAlpha;

            public float CleanBurnHeat = DefaultCleanBurnHeat;

            public float SootOnsetLambda = DefaultSootOnsetLambda;

            public float SootOpaqueLambda = DefaultSootOpaqueLambda;

            public float SootCurveExponent = DefaultSootCurveExponent;

            public float SootIncreaseTau = DefaultSootIncreaseTau;

            public float SootDecreaseTau = DefaultSootDecreaseTau;

            public float SootMaxAlpha = DefaultSootMaxAlpha;

            public float SootPowerFloor = DefaultSootPowerFloor;

            public float SootPowerExponent = DefaultSootPowerExponent;

            public float WetStackMaxAlpha = DefaultWetStackMaxAlpha;

            public float OilTintStrength = DefaultOilTintStrength;

            public float OilRpmExponent = DefaultOilRpmExponent;

            public Settings()
            {
            }

            public Settings(Settings other)
            {
                Density = other.Density;

                ColorIdleHaze = other.ColorIdleHaze;
                ColorCleanBurn = other.ColorCleanBurn;
                ColorHeavySoot = other.ColorHeavySoot;
                ColorWetStack = other.ColorWetStack;
                ColorOilBurn = other.ColorOilBurn;

                CleanMinHeatAlpha = other.CleanMinHeatAlpha;
                CleanMaxHeatAlpha = other.CleanMaxHeatAlpha;
                CleanBurnHeat = other.CleanBurnHeat;

                SootOnsetLambda = other.SootOnsetLambda;
                SootOpaqueLambda = other.SootOpaqueLambda;
                SootCurveExponent = other.SootCurveExponent;
                SootIncreaseTau = other.SootIncreaseTau;
                SootDecreaseTau = other.SootDecreaseTau;
                SootMaxAlpha = other.SootMaxAlpha;
                SootPowerFloor = other.SootPowerFloor;
                SootPowerExponent = other.SootPowerExponent;

                WetStackMaxAlpha = other.WetStackMaxAlpha;

                OilTintStrength = other.OilTintStrength;
                OilRpmExponent = other.OilRpmExponent;
            }

            /// <summary>
            /// Validates the model: any settings that violate invariants are adjusted.
            /// </summary>
            public void Validate()
            {
                const float epsilon = 0.01f;

                Density = Mathf.Max(0f, Density);

                CleanMinHeatAlpha = Mathf.Clamp01(CleanMinHeatAlpha);
                CleanMaxHeatAlpha = Mathf.Clamp01(CleanMaxHeatAlpha);
                SootMaxAlpha = Mathf.Clamp01(SootMaxAlpha);
                SootPowerFloor = Mathf.Clamp01(SootPowerFloor);
                WetStackMaxAlpha = Mathf.Clamp01(WetStackMaxAlpha);
                OilTintStrength = Mathf.Clamp01(OilTintStrength);

                CleanBurnHeat = Mathf.Max(epsilon, CleanBurnHeat);
                SootCurveExponent = Mathf.Max(epsilon, SootCurveExponent);
                SootPowerExponent = Mathf.Max(epsilon, SootPowerExponent);
                SootIncreaseTau = Mathf.Max(epsilon, SootIncreaseTau);
                SootDecreaseTau = Mathf.Max(epsilon, SootDecreaseTau);
                OilRpmExponent = Mathf.Max(epsilon, OilRpmExponent);

                SootOnsetLambda = Mathf.Max(SootOnsetLambda, SootOpaqueLambda + epsilon);
                SootOpaqueLambda = Mathf.Min(SootOpaqueLambda, SootOnsetLambda - epsilon);
            }
        }

        private float _soot;

        public Settings Tuning { get; set; } = new Settings();

        public Color Color { get; private set; } = Color.clear;

        /// <summary>
        /// Total particulate mass produced per unit time (sum of all exhaust fractions).
        /// </summary>
        public float ParticulateMass { get; private set; }

        /// <summary>
        /// Upper bound of <see cref="ParticulateMass"/> derived from the current tuning.
        /// As long as the tuning does not change, ParticulateMass is guaranteed to never exceed this.
        /// </summary>
        public float MaxParticulateMass => Tuning.Density
            * (Tuning.CleanMaxHeatAlpha + Tuning.WetStackMaxAlpha + Tuning.SootMaxAlpha);

        public void Update(float lambda, float rpmNorm, float heat, bool engineOn, float delta, float vapour = 0f)
        {
            var s = Tuning;

            if (!engineOn)
            {
                _soot = 0f;
                Color = Color.clear;
                ParticulateMass = 0f;
                return;
            }

            rpmNorm = Mathf.Clamp01(rpmNorm);
            heat = Mathf.Clamp01(heat);

            var flowColorFactor = Mathf.InverseLerp(0f, s.CleanBurnHeat, heat);
            var baseColor = Color.Lerp(s.ColorIdleHaze, s.ColorCleanBurn, flowColorFactor);

            var baseAlpha = Mathf.Lerp(s.CleanMinHeatAlpha, s.CleanMaxHeatAlpha, heat);

            var oilFactor = Mathf.Clamp01(s.OilTintStrength * Mathf.Pow(rpmNorm, s.OilRpmExponent));
            baseColor = Color.Lerp(baseColor, s.ColorOilBurn, oilFactor);

            var sootTarget = Mathf.InverseLerp(s.SootOnsetLambda, s.SootOpaqueLambda, lambda);
            sootTarget = Mathf.Pow(sootTarget, s.SootCurveExponent);

            // the governor is instant, so lambda can cross the whole sooty band in one
            // frame; ease soot in/out so opacity cannot snap between 0 and 1
            var sootTau = sootTarget > _soot ? s.SootIncreaseTau : s.SootDecreaseTau;
            _soot += (sootTarget - _soot) * (1f - Mathf.Exp(-delta / sootTau));
            var sootPower = Mathf.Pow(heat, s.SootPowerExponent);
            var sootAlpha = s.SootMaxAlpha * _soot * Mathf.Lerp(s.SootPowerFloor, 1f, sootPower);

            var wetAlpha = s.WetStackMaxAlpha * vapour;
            var totalWeight = baseAlpha + wetAlpha + sootAlpha;
            var finalColor = (baseColor * baseAlpha
                              + s.ColorWetStack * wetAlpha
                              + s.ColorHeavySoot * sootAlpha) / totalWeight;

            // unused as ParticulateMass encodes density, but set it to 1 for good practice
            finalColor.a = 1f;

            Color = finalColor;
            ParticulateMass = s.Density * totalWeight;
        }
    }

    public static class ExhaustPlacement
    {
        /// <summary>Parents an emitter to the car and orients it for smoke emission.</summary>
        public static void AttachTo(Transform emitter, Transform parent)
        {
            emitter.SetParent(parent, worldPositionStays: false);
            emitter.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        public static void PlaceAt(Transform emitter, Vector3 exhaustPosition, Transform parent, Vector3 offset)
        {
            // probably not the easiest way, but hey, it seems to work even under the heaviest of derailments.
            // if ever you wanted to test if the exhaust emits in the right direction even when the loco is upside down
            // boy have I got you covered
            AttachTo(emitter, parent);
            emitter.localPosition = parent.InverseTransformPoint(exhaustPosition) + offset;
        }
    }

    /// <summary>
    /// Exhaust plume speed range shared by a host's smoke and shimmer emitters.
    /// </summary>
    public sealed class ExhaustVelocitySettings
    {
        internal const float DefaultExhaustVelocityCoefficient = 5f;
        internal const float DefaultMinExhaustVelocity = 1f;

        /// <summary>Lower bound [m/s] applied to the calculated exhaust plume speed.</summary>
        public static float MinExhaustVelocity = DefaultMinExhaustVelocity;

        /// <summary>
        /// Exhaust velocity coefficient [m/s per unit mass flow per unit gas density].
        /// Can be adjusted to represent a wider/narrower exhaust mouth resulting in lower/higher exhaust velocity.
        /// </summary>
        public float ExhaustVelocityCoefficient = DefaultExhaustVelocityCoefficient;

        public ExhaustVelocitySettings()
        {
        }

        public ExhaustVelocitySettings(ExhaustVelocitySettings other)
        {
            ExhaustVelocityCoefficient = other.ExhaustVelocityCoefficient;
        }

        public void Validate()
        {
            ExhaustVelocityCoefficient = Mathf.Max(0.01f, ExhaustVelocityCoefficient);
        }

        /// <summary>Exhaust plume speed [m/s] from the current mass flow and gas density, floored at <see cref="MinExhaustVelocity"/>.</summary>
        public float Calculate(float massFlow, float gasDensity)
        {
            var speed = ExhaustVelocityCoefficient * massFlow / Mathf.Max(0.0001f, gasDensity);
            return Mathf.Max(MinExhaustVelocity, speed);
        }
    }

    /// <summary>
    /// Particle growth curve shared by a host's smoke and shimmer emitters.
    /// </summary>
    public static class ParticleCurves
    {
        // bakes the growth shape into a multi-key curve; exponent 1 is linear
        public static AnimationCurve BakeSizeCurve(float startSize, float endSize, float exponent)
        {
            var ratio = Mathf.Max(0.01f, endSize / Mathf.Max(0.01f, startSize));
            exponent = Mathf.Clamp(exponent, 0.05f, 4f);

            const int segments = 16;
            var times = new float[segments + 1];
            var values = new float[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                times[i] = t;
                values[i] = Mathf.LerpUnclamped(1f, ratio, Mathf.Pow(t, exponent));
            }

            var keys = new Keyframe[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var lo = Mathf.Max(0, i - 1);
                var hi = Mathf.Min(segments, i + 1);
                var slope = (values[hi] - values[lo]) / (times[hi] - times[lo]);
                keys[i] = new Keyframe(times[i], values[i], slope, slope);
            }

            return new AnimationCurve(keys);
        }
    }

    /// <summary>
    /// Size multiplier for particles that grow by <c>growth · (age / ReferenceAge)^p</c> in absolute time.
    /// Unity evaluates particle growth over normalised age, so this type helps us abstract over that implementation
    /// detail, allowing us to generate a correctly shaped curve by simply passing in the right configuration.
    /// This implementation also restricts the speed at which the growth coefficient can shrink, to ensure that
    /// shrinking the curve can never result in shrinking the absolute size of particles, as that would look weird.
    /// For our purposes, this limit should hardly ever apply anyway, the only moment where this might occur is if a
    /// heavily smoking locomotive comes to a sudden standstill.
    /// This implementation also contains some optimisations to ensure we don't rewrite the curve more than necessary,
    /// by only updating when the new curve shape starts to deviate too much from the existing one,
    /// and updating its keys in-place when possible.
    /// </summary>
    public sealed class GrowthCurve
    {
        private const int Segments = 16;

        // relative change in the coefficient below which the keys are left alone
        private const float Tolerance = 0.005f;

        private readonly float[] _shape = new float[Segments + 1];
        private readonly float[] _slope = new float[Segments + 1];

        // coefficient after rate limiting; the keys follow it once it moves beyond the tolerance
        private float _coefficient;
        private float _appliedCoefficient;

        /// <param name="referenceAge">Particle age [s] at which the growth passed to <see cref="SetGrowth"/> is reached.</param>
        public GrowthCurve(float referenceAge)
        {
            ReferenceAge = Mathf.Max(0.01f, referenceAge);
            Resample(1f);
        }

        public AnimationCurve Curve { get; } = new AnimationCurve();

        /// <summary>Particle age [s] at which the growth is reached; the exponent shapes growth around it.</summary>
        public float ReferenceAge { get; }

        /// <summary>Growth exponent <c>p</c>, clamped to a sane range; 1 is linear, and the initial value.</summary>
        public float Exponent { get; private set; }

        /// <summary>
        /// Moves the curve towards <paramref name="growth"/>, the size multiplier minus one at
        /// <see cref="ReferenceAge"/>, for particles of nominal <paramref name="lifetime"/> [s].
        /// Returns true when the keys changed, so the curve needs to be assigned again.
        /// </summary>
        /// <remarks>
        /// The curve applies to all live particles, so a falling coefficient shrinks them. Size
        /// <c>1 + C·(age/L)^p</c> keeps growing while <c>C</c> falls by at most <c>p/age</c> per second
        /// (logarithmically), so falls are limited to the rate that particles living up to
        /// <paramref name="maxLifetime"/> [s] tolerate. Rises apply immediately.
        /// </remarks>
        public bool SetGrowth(float growth, float lifetime, float maxLifetime, float dt)
        {
            var target = Mathf.Max(0f, growth) * Mathf.Pow(Mathf.Max(0f, lifetime) / ReferenceAge, Exponent);
            if (target >= _coefficient)
            {
                _coefficient = target;
            }
            else
            {
                var decay = Mathf.Exp(-Exponent / Mathf.Max(0.01f, maxLifetime) * dt);
                _coefficient = Mathf.Max(target, _coefficient * decay);
            }

            if (Mathf.Abs(_coefficient - _appliedCoefficient) <= Tolerance * _appliedCoefficient) return false;

            _appliedCoefficient = _coefficient;
            for (var i = 0; i <= Segments; i++)
            {
                Curve.MoveKey(i, MakeKey(i, _appliedCoefficient));
            }
            return true;
        }

        /// <summary>Makes the next <see cref="SetGrowth"/> jump straight to its target instead of limiting a fall.</summary>
        public void Reset()
        {
            // reset to zero means the next time we call SetGrowth, the target coefficient is guaranteed to be bigger,
            // so it forces an immediate jump
            _coefficient = 0f;
        }

        /// <summary>Resamples the shape for a new exponent and rebuilds the keys at the current coefficient.</summary>
        public void SetExponent(float exponent)
        {
            exponent = Mathf.Clamp(exponent, 0.05f, 4f);
            // strict equality is actually fine here, it's just a shortcut that skips resampling
            // in case the exponent remained exactly the same
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (exponent != Exponent) Resample(exponent);
        }

        private void Resample(float exponent)
        {
            Exponent = exponent;

            for (var i = 0; i <= Segments; i++)
            {
                _shape[i] = Mathf.Pow((float)i / Segments, exponent);
            }

            for (var i = 0; i <= Segments; i++)
            {
                var lo = Mathf.Max(0, i - 1);
                var hi = Mathf.Min(Segments, i + 1);
                _slope[i] = (_shape[hi] - _shape[lo]) * Segments / (hi - lo);
            }

            var keys = new Keyframe[Segments + 1];
            for (var i = 0; i <= Segments; i++)
            {
                keys[i] = MakeKey(i, _appliedCoefficient);
            }
            Curve.keys = keys;
        }

        private Keyframe MakeKey(int i, float coefficient)
        {
            var slope = coefficient * _slope[i];
            return new Keyframe((float)i / Segments, 1f + coefficient * _shape[i], slope, slope);
        }
    }

    /// <summary>
    /// Hex color codec for XML serialization, where a Color cannot be a
    /// [DefaultValue] constant. Pure managed so it also works outside the game
    /// (ColorUtility's parse is a native ECall). Unparseable input yields magenta.
    /// </summary>
    internal static class ColorHex
    {
        public static Color Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;

            if (hex[0] == '#') hex = hex.Substring(1);
            if ((hex.Length != 6 && hex.Length != 8)
                || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return Color.magenta;
            }

            if (hex.Length == 6)
            {
                return new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f,
                    (value & 0xFF) / 255f, 1f);
            }

            return new Color(((value >> 24) & 0xFF) / 255f, ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f);
        }

        public static string Format(Color color)
        {
            var r = (byte)Math.Round(Mathf.Clamp(color.r, 0f, 1f) * 255f);
            var g = (byte)Math.Round(Mathf.Clamp(color.g, 0f, 1f) * 255f);
            var b = (byte)Math.Round(Mathf.Clamp(color.b, 0f, 1f) * 255f);
            var a = (byte)Math.Round(Mathf.Clamp(color.a, 0f, 1f) * 255f);
            return $"{r:X2}{g:X2}{b:X2}{a:X2}";
        }
    }
}
