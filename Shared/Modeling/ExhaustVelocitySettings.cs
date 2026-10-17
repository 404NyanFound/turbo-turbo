using UnityEngine;

namespace TurboTurbo.Modeling
{
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
}
