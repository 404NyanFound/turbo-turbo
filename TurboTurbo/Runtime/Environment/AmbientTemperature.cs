using UnityEngine;

namespace TurboTurbo.Runtime.Environment;

/// <summary>
/// Ambient temperature seen by the simulation. The game object periodically polls its sources,
/// preferring DV Seasons and otherwise approximating from the game's weather system, and writes
/// the resulting temperature to its static <see cref="Kelvin"/> and <see cref="Celsius"/> properties.
/// </summary>
internal sealed class AmbientTemperature : MonoBehaviour
{
    private const float UpdateIntervalSeconds = 10f;

    private readonly SeasonsReader _seasons = new();
    private readonly BuiltinWeatherReader _builtin = new();

    private float _nextUpdateAt;

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
    /// If not called before reading a temperature, its value may be stale by up to <see cref="UpdateIntervalSeconds"/>
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
        if (Time.unscaledTime < _nextUpdateAt) return;
        _nextUpdateAt = Time.unscaledTime + UpdateIntervalSeconds;

        RefreshInternal();
    }

    private void RefreshInternal()
    {
        if (_seasons.TryBind())
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
    }
}
