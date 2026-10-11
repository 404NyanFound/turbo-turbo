using UnityEngine;

namespace TurboTurbo.Runtime.Environment;

/// <summary>
/// Approximates ambient air temperature from the built-in time of day and weather signals.
/// Models a general warm summer day.
/// </summary>
internal static class BuiltinTemperatureProfile
{
    /// <summary>Daily mean temperature.</summary>
    public const float MeanCelsius = 22f;

    /// <summary>Amplitude of time-of-day based temperature swings.</summary>
    public const float SwingCelsius = 8f;

    /// <summary>Clock hour of the daily maximum.</summary>
    public const float PeakHour = 16f;

    /// <summary>Fraction by which full cloud cover damps time-based temperature swings.</summary>
    public const float CloudSwingDamping = 0.6f;

    /// <summary>Cooling at full rain.</summary>
    public const float RainCoolingCelsius = 3f;

    /// <summary>Cooling at full wind.</summary>
    public const float WindCoolingCelsius = 2f;

    /// <summary>Extra wind cooling multiplier when it is also raining.</summary>
    public const float WindRainCoupling = 1f;

    /// <summary>Cooling at full thunder.</summary>
    public const float ThunderCoolingCelsius = 1.5f;

    /// <summary>
    /// Evaluates the ambient temperature in Celsius.
    /// </summary>
    public static float Evaluate(float hour, float cloud, float rain, float wind, float thunder)
    {
        var rainCooling = Mathf.Clamp01(rain) * RainCoolingCelsius;
        var windCooling = Mathf.Clamp01(wind) * WindCoolingCelsius * (1 + WindRainCoupling * rain);
        var thunderCooling = Mathf.Clamp01(thunder) * ThunderCoolingCelsius;

        var swing = SwingCelsius * (1f - CloudSwingDamping * cloud);
        var diurnal = swing * Mathf.Cos((hour - PeakHour) * Mathf.PI / 12f);

        var temperature = MeanCelsius + diurnal - rainCooling - windCooling - thunderCooling;
        return Mathf.Clamp(temperature, -20, 40); // sanity check
    }
}
