using System;
using System.Reflection;

using UnityModManagerNet;

namespace TurboTurbo.Runtime.Environment;

/// <summary>
/// Reads ambient air temperature from DV Seasons. Reading private members from another mod is generally fragile,
/// so this reader is quite conservative and will no longer operate if it detects a failure. This means that its
/// public members are safe to call, though it is still prudent to not call <see cref="GetCelsius"/> too often,
/// as it relies on undocumented internal APIs in Seasons that could change, throw, or cost more than expected.
/// </summary>
internal sealed class SeasonsReader
{
    private const string ModId = "DVSeasons";
    private const string MainTypeName = "DVSeasons.Mod.Main";

    private readonly Logger _log = Log.ForContext("weather/seasons");

    private UnityModManager.ModEntry _mod;
    private FieldInfo _runtimeField;
    private PropertyInfo _hasTemperatureProperty;
    private PropertyInfo _currentStateProperty;
    private PropertyInfo _celsiusProperty;
    private bool _loggedSuccess;

    /// <summary>True once every member has been resolved.</summary>
    public bool IsBound { get; private set; }

    /// <summary>True when binding failed in a way that will not recover this session.</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>
    /// Attempts to bind to DVSeasons. No-op if binding has already succeeded or failed.
    /// If binding fails, the reader latches into a failed state, to avoid repeatedly executing
    /// expensive reflection operations that may throw.
    /// </summary>
    public bool TryBind()
    {
        if (IsBound) return true;
        if (IsDisabled) return false;

        _mod ??= ModRegistry.Find(ModId);
        var assembly = _mod?.Assembly;
        if (assembly == null)
        {
            Disable("not installed");
            return false;
        }

        try
        {
            return BindMembers(assembly);
        }
        catch (Exception e)
        {
            Disable("reflecting into DVSeasons threw", e);
            return false;
        }
    }

    /// <summary>
    /// Reads the current air temperature in Celsius. Will return a failure if the temperature
    /// could not be read, which will generally happen right after loading a new save, as Seasons
    /// has not initialised itself yet.
    /// </summary>
    public Result<float> GetCelsius()
    {
        if (IsDisabled) return new Error("DVSeasons integration is disabled");
        if (!IsBound) return new Error("DVSeasons is not bound");

        try
        {
            var runtime = _runtimeField.GetValue(null);
            if (runtime == null) return new Error("DVSeasons runtime is not running");

            if (!(bool)_hasTemperatureProperty.GetValue(runtime))
            {
                return new Error("DVSeasons weather temperature is not ready");
            }

            var state = _currentStateProperty.GetValue(runtime);
            if (state == null) return new Error("DVSeasons state is not available");

            var value = (float)_celsiusProperty.GetValue(state);
            if (!_loggedSuccess)
            {
                _loggedSuccess = true;
                _log.Info($"successfully bound, current temperature: {value:0.0}°C");
            }

            return value;
        }
        catch (Exception e)
        {
            Disable("exception while trying to read DVSeasons temperature", e);
            return new Error("exception while trying to read DVSeasons temperature");
        }
    }

    private bool BindMembers(Assembly assembly)
    {
        var main = assembly.GetType(MainTypeName);
        if (main == null)
        {
            Disable($"main type '{MainTypeName}' not found");
            return false;
        }

        _runtimeField = main.GetField("runtime", BindingFlags.NonPublic | BindingFlags.Static);
        var runtimeType = _runtimeField?.FieldType;
        if (runtimeType == null)
        {
            Disable($"field '{MainTypeName}.runtime' is missing");
            return false;
        }

        _hasTemperatureProperty = runtimeType.GetProperty("HasWeatherTemperature");
        _currentStateProperty = runtimeType.GetProperty("CurrentState");
        var stateType = _currentStateProperty?.PropertyType;
        if (_hasTemperatureProperty == null || stateType == null)
        {
            Disable($"members of '{runtimeType.Name}' are missing");
            return false;
        }

        _celsiusProperty = stateType.GetProperty("TemperatureCelsius");
        if (_celsiusProperty == null)
        {
            Disable($"property '{stateType.Name}.TemperatureCelsius' is missing");
            return false;
        }

        IsBound = true;
        _log.Info("discovered temperature tracker in DVSeasons");
        return true;
    }

    private void Disable(string reason, Exception exception = null)
    {
        if (IsDisabled) return;

        IsDisabled = true;
        IsBound = false;
        _log.Info($"DVSeasons integration disabled: {reason}");
        if (exception != null) _log.Exception(exception);
    }
}
