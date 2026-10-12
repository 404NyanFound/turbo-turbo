using DV.WeatherSystem;

using TurboTurbo.Configuration;

using UnityEngine;

namespace TurboTurbo.Runtime.Environment;

/// <summary>
/// Ambient temperature seen by the simulation. The game object periodically polls its sources,
/// preferring DV Seasons and otherwise approximating from the game's weather system, and writes
/// the resulting temperature to its static <see cref="Kelvin"/> and <see cref="Celsius"/> properties.
/// </summary>
internal sealed class AmbientTemperature : MonoBehaviour
{
    private const float TemperatureUpdateIntervalSeconds = 10f;

    /// <summary>Wind only reads the game's own weather signal, so it can be polled far more often.</summary>
    private const float WindUpdateIntervalSeconds = 0.25f;

    private readonly SeasonsReader _seasons = new();
    private readonly BuiltinWeatherReader _builtin = new();

    private float _nextTemperatureUpdateAt;
    private float _nextWindUpdateAt;

    public static AmbientTemperature Instance { get; private set; }

    /// <summary>Source of the currently applied ambient temperature.</summary>
    public static AmbientSource Source { get; private set; } = AmbientSource.None;

    /// <summary>Ambient temperature [K].</summary>
    public static float Kelvin { get; set; } = PhysicsConstants.ReferenceAmbientK;

    /// <summary>Ambient temperature [°C].</summary>
    public static float Celsius
    {
        get => Kelvin - PhysicsConstants.KelvinOffset;
        set => Kelvin = value + PhysicsConstants.KelvinOffset;
    }

    /// <summary>Source of the currently applied ambient wind.</summary>
    public static AmbientSource WindSource { get; private set; } = AmbientSource.None;

    /// <summary>Wind speed [m/s].</summary>
    public static float WindSpeed { get; set; }

    /// <summary>Compass wind direction [degrees].</summary>
    public static float WindDirectionDegrees { get; set; }

    /// <summary>Wind velocity [m/s].</summary>
    public static Vector3 Wind => BuiltinWindProfile.WorldDirection(WindDirectionDegrees) * WindSpeed;

    /// <summary>When true, <see cref="RefreshWind"/> leaves the wind untouched.</summary>
    public static bool WindRefreshPaused { get; set; }

    static AmbientTemperature()
    {
        UnloadWatcher.UnloadRequested += OnUnload;
    }

    public static AmbientTemperature Create()
    {
        var go = new GameObject(Naming.Create("AmbientTemperature"));
        DontDestroyOnLoad(go);
        var ambient = go.AddComponent<AmbientTemperature>();
        Instance = ambient;
        return ambient;
    }

    /// <summary>
    /// Polls the configured temperature source now, ensuring that temperatures are fresh.
    /// If not called before reading a temperature, its value may be stale by up to <see cref="TemperatureUpdateIntervalSeconds"/>
    /// plus the current frame's deltaTime. Note that, due to external integrations, a refresh cannot be guaranteed
    /// to be cheap, so avoid it if you don't need accuracy to the second.
    /// </summary>
    public static void Refresh()
    {
        if (Instance != null)
        {
            Instance.RefreshInternal();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextTemperatureUpdateAt)
        {
            _nextTemperatureUpdateAt = Time.unscaledTime + TemperatureUpdateIntervalSeconds;
            RefreshTemperature();
        }

        if (Time.unscaledTime >= _nextWindUpdateAt)
        {
            _nextWindUpdateAt = Time.unscaledTime + WindUpdateIntervalSeconds;
            RefreshWind();
        }
    }

    private void RefreshInternal()
    {
        RefreshTemperature();
        RefreshWind();
    }

    private void RefreshTemperature()
    {
        if (SettingsStore.Current.UseSeasons && _seasons.TryBind())
        {
            var seasons = _seasons.GetCelsius();
            if (seasons.IsSuccess)
            {
                Apply(seasons.Value, AmbientSource.Seasons);
                return;
            }
        }

        var builtin = _builtin.GetCelsius();
        if (builtin.IsSuccess) Apply(builtin.Value, AmbientSource.Builtin);
    }

    /// <summary>
    /// Refreshes the ambient wind. Direction always comes from the game; speed comes from the
    /// game signal only while something overrides it, and is otherwise generated. Does nothign
    /// if refresh is paused.
    /// </summary>
    private static void RefreshWind()
    {
        if (WindRefreshPaused) return;

        var driver = WeatherDriver.Instance;
        if (driver == null) return;

        var degrees = FiniteOr(driver.WindDirection.CurrentValue, WindDirectionDegrees);

        var speed = BuiltinWindProfile.Evaluate(Time.unscaledTime);
        var source = AmbientSource.Builtin;
        if (driver.todAnimation != null && driver.WindSpeed.IsOverridden && driver.WindSpeed.CurrentValue > 5)
        {
            // Seasons will override the wind speed during a blizzard, we should respect it
            speed = Mathf.Clamp(FiniteOr(driver.WindSpeed.CurrentValue, speed), 0, 10);
            source = AmbientSource.Game;
        }

        WindDirectionDegrees = degrees;
        WindSpeed = Mathf.Max(0f, speed);
        WindSource = source;
    }

    /// <summary>Returns <paramref name="value"/>, or <paramref name="fallback"/> if it is NaN or infinite.</summary>
    private static float FiniteOr(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

    private static void Apply(float celsius, AmbientSource source)
    {
        Celsius = celsius;
        Source = source;
    }

    private static void OnUnload()
    {
        if (Instance == null)
        {
            return;
        }

        Log.ForContext("ambient-temp").Info($"unloaded, reset ambient temperature to {PhysicsConstants.ReferenceAmbientK} K");
        Kelvin = PhysicsConstants.ReferenceAmbientK;
        Source = AmbientSource.None;

        WindSpeed = 0f;
        WindDirectionDegrees = 0f;
        WindSource = AmbientSource.None;
    }
}
