using UnityEngine;

namespace TurboTurbo.Modeling
{
    /// <summary>
    /// Size multiplier for particles that grow by <c>growth · (age / ReferenceAge)^p</c> in absolute time.
    /// Unity evaluates particle growth over normalised age, so this type helps us abstract over that implementation
    /// detail, allowing us to generate a correctly shaped curve by simply passing in the right configuration.
    /// This implementation also restricts the speed at which the growth coefficient can shrink, to ensure that
    /// shrinking the curve can never result in shrinking the absolute size of particles, as that would look weird.
    /// For our purposes, this limit should hardly ever apply anyway, the only moment where this might occur is if a
    /// heavily smoking locomotive comes to a sudden standstill.
    /// The implementation also contains some optimisations to ensure we don't rewrite the curve more than necessary,
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
        /// <paramref name="lifetime"/> [s] tolerate. Rises apply immediately.
        /// </remarks>
        public bool SetGrowth(float growth, float lifetime, float dt)
        {
            var target = Mathf.Max(0f, growth) * Mathf.Pow(Mathf.Max(0f, lifetime) / ReferenceAge, Exponent);
            if (target >= _coefficient)
            {
                _coefficient = target;
            }
            else
            {
                var decay = Mathf.Exp(-Exponent / Mathf.Max(0.01f, lifetime) * dt);
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
}
