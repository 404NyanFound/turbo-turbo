using UnityEngine;

namespace TurboTurbo.Modeling
{
    /// <summary>
    /// Particle growth curve shared by a host's smoke and shimmer emitters.
    /// </summary>
    public static class ParticleCurves
    {
        /// <summary>
        /// Bakes the growth curve into an animation curve.
        /// </summary>
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
}
