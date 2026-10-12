using UnityEngine;

namespace TurboTurbo.Runtime.Environment;

/// <summary>
/// Generates a smooth pseudo-random wind speed. Wind speed in DV's weather system
/// is static, so we have to roll our own.
/// </summary>
internal static class BuiltinWindProfile
{
    /// <summary>Mean wind speed [m/s].</summary>
    public const float MeanSpeed = 2.5f;

    /// <summary>Half-range of the speed swing around <see cref="MeanSpeed"/>.</summary>
    public const float Amplitude = 2.5f;

    /// <summary>How quickly the speed wanders; smaller is slower.</summary>
    public const float NoiseScale = 0.02f;

    /// <summary>
    /// Evaluates the wind speed [m/s] for a given point in time.
    /// </summary>
    public static float Evaluate(float timeSeconds)
    {
        var t = timeSeconds * NoiseScale;
        var noise = 0.65f * ValueNoise(t) + 0.35f * ValueNoise(t * 2.3f + 11.7f);
        return MeanSpeed + (noise - 0.5f) * 2f * Amplitude;
    }

    /// <summary>
    /// Converts a compass wind direction [degrees] to a world-space unit vector.
    /// This matches DV's logic.
    /// </summary>
    public static Vector3 WorldDirection(float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        return new Vector3(-Mathf.Sin(radians), 0f, -Mathf.Cos(radians));
    }

    private static float ValueNoise(float x)
    {
        var i = Mathf.FloorToInt(x);
        var f = x - i;
        var u = f * f * (3f - 2f * f);
        return Mathf.Lerp(Hash(i), Hash(i + 1), u);
    }

    private static float Hash(int x)
    {
        unchecked
        {
            var h = (uint)x * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }
}
