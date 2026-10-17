using System.Collections.Generic;

using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage.V1;
using TurboTurbo.WorkBench;

using UnityModManagerNet;

namespace TurboTurbo.Configuration;

public class Settings : UnityModManager.ModSettings
{
    public KeyBinding ToggleDevPanel = new KeyBinding();
    public KeyBinding OpenProfileEditor = new KeyBinding();

    public bool AuthoringMode;
    public string AuthoringTargetModId = "";

    public bool DeveloperMode;

    public bool UseSeasons = true;

    /// <summary>Nominal smoke particle lifetime [s].</summary>
    public float SmokeLifetime = SmokeParticles.DefaultLifetime;

    /// <summary>Smoke emission rate [p/s] at zero exhaust energy.</summary>
    public float SmokeIdleEmissionRate = SmokeParticles.DefaultIdleEmissionRate;

    /// <summary>Smoke emission rate [p/s] at full exhaust energy.</summary>
    public float SmokeFullEmissionRate = SmokeParticles.DefaultFullEmissionRate;

    public List<LocoProfileXml> LocoProfiles = new();

    public bool IsAuthoring => AuthoringMode && !string.IsNullOrEmpty(AuthoringTargetModId);

    internal ResolutionMode ResolutionMode => IsAuthoring ? ResolutionMode.Authoring : ResolutionMode.Normal;
}
