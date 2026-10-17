using UnityEngine;

namespace TurboTurbo.Modeling
{
    /// <summary>
    /// Calculates particle budgets
    /// </summary>
    public static class ParticleBudget
    {
        /// <summary>Most particles emitted in a single frame, so a long frame cannot release a runaway burst.</summary>
        public const int MaxBurst = 30;

        /// <summary>
        /// Capacity for emission at up to <paramref name="maxRate"/> [1/s] of particles living up to
        /// <paramref name="maxLifetime"/> [s]. The live count can never exceed what was emitted within one maximum
        /// lifetime; one burst on top absorbs rounding in the per-frame emission.
        /// </summary>
        public static int Capacity(float maxRate, float maxLifetime)
        {
            return Mathf.CeilToInt(Mathf.Max(0f, maxRate) * Mathf.Max(0f, maxLifetime)) + MaxBurst;
        }
    }
}
