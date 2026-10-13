using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class CombustionXml
{
    internal const float DefaultRatedExhaustTempK = 760f;
    internal const float DefaultTauChamber = 25f;
    internal const float DefaultTauEngine = 400f;
    internal const float DefaultTauCooldownOpen = 65f;
    internal const float DefaultTauCooldownClosed = 1300f;
    internal const float DefaultChamberGainK = 220f;
    internal const float DefaultPolytropicExponent = 1.34f;
    internal const float DefaultIgnitionTempK = 540f;
    internal const float DefaultIgnitionSpread = 1.5f;
    internal const float DefaultLateBurnWork = 0.5f;
    internal const float DefaultQuenchFraction = 0.15f;
    internal const float DefaultCrankingFuelPerStroke = 0.25f;

    [DefaultValue(DefaultRatedExhaustTempK)]
    public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;

    [DefaultValue(DefaultTauChamber)]
    public float TauChamber { get; set; } = DefaultTauChamber;

    [DefaultValue(DefaultTauEngine)]
    public float TauEngine { get; set; } = DefaultTauEngine;

    [DefaultValue(DefaultTauCooldownOpen)]
    public float TauCooldownOpen { get; set; } = DefaultTauCooldownOpen;

    [DefaultValue(DefaultTauCooldownClosed)]
    public float TauCooldownClosed { get; set; } = DefaultTauCooldownClosed;

    [DefaultValue(DefaultChamberGainK)]
    public float ChamberGainK { get; set; } = DefaultChamberGainK;

    [DefaultValue(DefaultPolytropicExponent)]
    public float PolytropicExponent { get; set; } = DefaultPolytropicExponent;

    [DefaultValue(DefaultIgnitionTempK)]
    public float IgnitionTempK { get; set; } = DefaultIgnitionTempK;

    [DefaultValue(DefaultIgnitionSpread)]
    public float IgnitionSpread { get; set; } = DefaultIgnitionSpread;

    [DefaultValue(DefaultLateBurnWork)]
    public float LateBurnWork { get; set; } = DefaultLateBurnWork;

    [DefaultValue(DefaultQuenchFraction)]
    public float QuenchFraction { get; set; } = DefaultQuenchFraction;

    [DefaultValue(DefaultCrankingFuelPerStroke)]
    public float CrankingFuelPerStroke { get; set; } = DefaultCrankingFuelPerStroke;
}
