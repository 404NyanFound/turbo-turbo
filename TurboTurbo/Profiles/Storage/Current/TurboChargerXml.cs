using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.Current;

public sealed class TurboChargerXml
{
    internal const float DefaultLambdaCalibration = 1.41f;
    internal const float DefaultBoostChargeMultiplier = 1.125f;
    internal const float DefaultTauUp = 3.0f;
    internal const float DefaultTauDown = 1.5f;
    internal const float DefaultMinSpoolTau = 0.5f;
    internal const float DefaultThermalK = 0.8f;
    internal const float DefaultSurgeRateThreshold = 15f;
    internal const float DefaultCompressionRatio = 13f;
    internal const float DefaultIntakeNtu = 1f;

    [DefaultValue(DefaultLambdaCalibration)]
    public float LambdaCalibration { get; set; } = DefaultLambdaCalibration;

    [DefaultValue(DefaultBoostChargeMultiplier)]
    public float BoostChargeMultiplier { get; set; } = DefaultBoostChargeMultiplier;

    [DefaultValue(DefaultTauUp)]
    public float TauUp { get; set; } = DefaultTauUp;

    [DefaultValue(DefaultTauDown)]
    public float TauDown { get; set; } = DefaultTauDown;

    [DefaultValue(DefaultMinSpoolTau)]
    public float MinSpoolTau { get; set; } = DefaultMinSpoolTau;

    [DefaultValue(DefaultThermalK)]
    public float ThermalK { get; set; } = DefaultThermalK;

    [DefaultValue(DefaultSurgeRateThreshold)]
    public float SurgeRateThreshold { get; set; } = DefaultSurgeRateThreshold;

    [DefaultValue(DefaultCompressionRatio)]
    public float CompressionRatio { get; set; } = DefaultCompressionRatio;

    [DefaultValue(DefaultIntakeNtu)]
    public float IntakeNtu { get; set; } = DefaultIntakeNtu;
}
