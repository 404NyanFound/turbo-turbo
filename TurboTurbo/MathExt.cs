using UnityEngine;

namespace TurboTurbo;

/// <summary>Reusable math helpers.</summary>
internal static class MathExt
{
    /// <summary>
    /// Inverse-lerps <paramref name="x"/> between <paramref name="edge0"/>
    /// and <paramref name="edge1"/>, returning a value in [0..1]
    /// </summary>
    public static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Mathf.Clamp01(Mathf.InverseLerp(edge0, edge1, x));
        return t * t * (3f - 2f * t);
    }
}
