using DV.WeatherSystem;

using UnityEngine;

namespace TurboTurbo.Runtime.Environment;

/// <summary>
/// Generates a temperature value based on the game's time of day and weather.
/// </summary>
internal sealed class BuiltinWeatherReader
{
    /// <summary>Wind speed [m/s] treated as the top of the normalisation range.</summary>
    private const float ReferenceWindSpeed = 7f;

    private readonly Logger _log = Log.ForContext("weather/builtin");

    private bool _loggedSuccess;

    /// <summary>
    /// Approximates the current air temperature in Celsius. Returns a failure when the weather system
    /// is not ready, which happens right after loading a save.
    /// </summary>
    public Result<float> GetCelsius()
    {
        if (UnloadWatcher.isUnloading) return new Error("unloading");

        var driver = WeatherDriver.Instance;
        if (driver == null) return new Error("builtin weather driver is not available");

        var manager = driver.manager;
        if (manager == null || manager.todSky == null) return new Error("builtin weather system is not ready");

        var hour = driver.TimeOfDayHours.CurrentValue;
        var rain = driver.RainValue.CurrentValue;
        var thunder = driver.ThunderValue.CurrentValue;
        var wind = driver.todAnimation != null ? driver.WindSpeed.CurrentValue : 0f;
        var cloud = CloudCoverage(driver);

        if (!IsFinite(hour) || !IsFinite(cloud) || !IsFinite(rain) || !IsFinite(wind) || !IsFinite(thunder))
        {
            return new Error("builtin weather signals are not finite");
        }

        var value = BuiltinTemperatureProfile.Evaluate(hour, cloud, rain, wind / ReferenceWindSpeed, thunder);
        if (!_loggedSuccess)
        {
            _loggedSuccess = true;
            _log.Info($"using builtin weather approximation, current temperature: {value:0.0}°C");
        }

        return value;
    }

    private static float CloudCoverage(WeatherDriver driver)
    {
        var low = driver.CurrentChungusState.currentLow;
        if (low != null)
        {
            return Mathf.Clamp01(Mathf.Max(low.OverallFogginess, low.cloudCoverage * low.cloudOpacity));
        }

        var sky = TOD_Sky.Instance;
        return sky != null ? Mathf.Clamp01(sky.Clouds.Coverage) : 0f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
