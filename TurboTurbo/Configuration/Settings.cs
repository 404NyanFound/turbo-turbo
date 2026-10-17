using JetBrains.Annotations;

using TurboTurbo.Profiles;
using TurboTurbo.Profiles.Storage;
using TurboTurbo.WorkBench;

using UnityModManagerNet;

namespace TurboTurbo.Configuration;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
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

    public StoredProfiles LocoProfiles = new();

    public bool IsAuthoring => AuthoringMode && !string.IsNullOrEmpty(AuthoringTargetModId);

    internal ResolutionMode ResolutionMode => IsAuthoring ? ResolutionMode.Authoring : ResolutionMode.Normal;
}
