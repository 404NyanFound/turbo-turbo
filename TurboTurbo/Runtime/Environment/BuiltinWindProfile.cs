using System;

using UnityEngine;

namespace TurboTurbo.Runtime.Environment;

/// <summary>
/// Generates a smooth pseudo-random wind speed. Wind speed in DV's weather system
/// is static, so we have to roll our own.
/// 
/// This is a simpler model that follows the Rayleigh distribution that real wind
/// adheres to, but it has no correlation with weather. Because of that, we cut it
/// off quit early so that it doesn't look out of place on a sunny day in the game.
/// </summary>
internal static class BuiltinWindProfile
{
    /// <summary>Mean wind speed [m/s] of the full distribution.</summary>
    public const float ReferenceMeanSpeed = 2.5f;

    /// <summary>Maximum wind speed [m/s] produced.</summary>
    public const float MaxSpeed = 5f;

    /// <summary>Time [s] between independent values of the base wind layer.</summary>
    public const float SlowPeriodSeconds = 600f;

    /// <summary>Time [s] between independent values of the gust layer.</summary>
    public const float FastPeriodSeconds = 12f;

    /// <summary>Number of entries in <see cref="NoiseQuantiles"/>.</summary>
    private const int QuantileCount = 129;

    /// <summary>Scales the Rayleigh distribution so it has a mean of <see cref="ReferenceMeanSpeed"/>.</summary>
    private static readonly float RayleighScale = ReferenceMeanSpeed * 2f / Mathf.Sqrt(Mathf.PI);

    /// <summary>Share of the Rayleigh distribution that falls below the set maximum speed.</summary>
    private static readonly float MaxSpeedProbability =
        1f - Mathf.Exp(-(MaxSpeed / RayleighScale) * (MaxSpeed / RayleighScale));

    /// <summary>
    /// Evenly spaced quantiles of noise (which isn't uniformly distributed), so it can be
    /// flattened before being shaped into a wind speed.
    /// </summary>
    private static readonly float[] NoiseQuantiles = BuildNoiseQuantiles();

    /// <summary>
    /// Evaluates the wind speed [m/s] for a given point in time.
    /// </summary>
    public static float Evaluate(float timeSeconds)
    {
        var u = Flatten(Noise(timeSeconds));

        return RayleighScale * Mathf.Sqrt(-Mathf.Log(1f - u * MaxSpeedProbability));
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

    /// <summary>A slow and a fast layer of value noise in [0, 1], evaluated at a time [s].</summary>
    private static float Noise(float timeSeconds)
    {
        return 0.65f * ValueNoise(timeSeconds / SlowPeriodSeconds)
            + 0.35f * ValueNoise(timeSeconds / FastPeriodSeconds + 11.7f);
    }

    /// <summary>Maps noise to [0, 1] with a roughly uniform distribution, keeping it smooth and monotonic.</summary>
    private static float Flatten(float noise)
    {
        var index = Array.BinarySearch(NoiseQuantiles, noise);
        if (index < 0) index = ~index - 1;
        index = Mathf.Clamp(index, 0, QuantileCount - 2);

        var lo = NoiseQuantiles[index];
        var hi = NoiseQuantiles[index + 1];
        var f = Mathf.Clamp01((noise - lo) / Mathf.Max(1e-9f, hi - lo));
        return (index + f) / (QuantileCount - 1);
    }

    private static float[] BuildNoiseQuantiles()
    {
        // stepping by the golden ratio of a slow cell samples many noise cells at well-spread offsets
        const int sampleCount = 16384;
        const float step = 0.618034f * SlowPeriodSeconds;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = Noise(i * step);
        }

        Array.Sort(samples);

        var quantiles = new float[QuantileCount];
        for (var k = 0; k < QuantileCount; k++)
        {
            quantiles[k] = samples[Mathf.RoundToInt(k * (sampleCount - 1f) / (QuantileCount - 1))];
        }

        // pin the ends to the full noise range, so rare values beyond the samples still map into [0, 1]
        quantiles[0] = 0f;
        quantiles[QuantileCount - 1] = 1f;
        return quantiles;
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
