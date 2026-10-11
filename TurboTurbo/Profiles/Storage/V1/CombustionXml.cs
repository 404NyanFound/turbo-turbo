using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class CombustionXml
{
    internal const float DefaultRatedExhaustTempK = 760f;
    internal const float DefaultTauCylinder = 10f;
    internal const float DefaultTauEngine = 80f;
    internal const float DefaultTauCooldownOpen = 65f;
    internal const float DefaultTauCooldownClosed = 1300f;
    internal const float DefaultCylinderGainK = 60f;
    internal const float DefaultPolytropicExponent = 1.35f;
    internal const float DefaultCompressorEfficiency = 0.75f;
    internal const float DefaultCompressionHeatLoss = 0.15f;
    internal const float DefaultInductionWallHeating = 0.15f;
    internal const float DefaultResidualGasFraction = 0.04f;
    internal const float DefaultColdCompressionK = 620f;
    internal const float DefaultWarmCompressionK = 760f;
    internal const float DefaultMinBurnFractionAtCold = 0.85f;

    [DefaultValue(DefaultRatedExhaustTempK)]
    public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;

    [DefaultValue(DefaultTauCylinder)]
    public float TauCylinder { get; set; } = DefaultTauCylinder;

    [DefaultValue(DefaultTauEngine)]
    public float TauEngine { get; set; } = DefaultTauEngine;

    [DefaultValue(DefaultTauCooldownOpen)]
    public float TauCooldownOpen { get; set; } = DefaultTauCooldownOpen;

    [DefaultValue(DefaultTauCooldownClosed)]
    public float TauCooldownClosed { get; set; } = DefaultTauCooldownClosed;

    [DefaultValue(DefaultCylinderGainK)]
    public float CylinderGainK { get; set; } = DefaultCylinderGainK;

    [DefaultValue(DefaultPolytropicExponent)]
    public float PolytropicExponent { get; set; } = DefaultPolytropicExponent;

    [DefaultValue(DefaultCompressorEfficiency)]
    public float CompressorEfficiency { get; set; } = DefaultCompressorEfficiency;

    [DefaultValue(DefaultCompressionHeatLoss)]
    public float CompressionHeatLoss { get; set; } = DefaultCompressionHeatLoss;

    [DefaultValue(DefaultInductionWallHeating)]
    public float InductionWallHeating { get; set; } = DefaultInductionWallHeating;

    [DefaultValue(DefaultResidualGasFraction)]
    public float ResidualGasFraction { get; set; } = DefaultResidualGasFraction;

    [DefaultValue(DefaultColdCompressionK)]
    public float ColdCompressionK { get; set; } = DefaultColdCompressionK;

    [DefaultValue(DefaultWarmCompressionK)]
    public float WarmCompressionK { get; set; } = DefaultWarmCompressionK;

    [DefaultValue(DefaultMinBurnFractionAtCold)]
    public float MinBurnFractionAtCold { get; set; } = DefaultMinBurnFractionAtCold;
}
