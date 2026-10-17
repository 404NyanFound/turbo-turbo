using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.Current;

public sealed class SmokeEmitterXml
{
    internal const float DefaultStartSize = 0.39f;
    internal const float DefaultStartSizeVariance = 0.29f;
    internal const float DefaultPlumeSize = 6.4f;
    internal const float DefaultGrowthExponent = 0.65f;
    internal const float DefaultBuoyancy = 0.1f;
    internal const float DefaultDrag = 0.6f;
    internal const float DefaultAngularVelocityMax = 20f;
    internal const float DefaultSpeedNormMax = 15f;
    internal const float DefaultSpeedLifetimeScale = 0.3f;
    internal const float DefaultSpeedGrowthScale = 2.2f;
    internal const float DefaultSpeedJitter = 0.5f;
    internal const float DefaultTurbulenceStrengthMin = 0.125f;
    internal const float DefaultTurbulenceStrengthMax = 1.25f;
    internal const float DefaultTurbulenceFrequency = 0.5f;
    internal const float DefaultTurbulenceScrollSpeed = 0f;
    internal const float DefaultLightSaturation = 0.35f;
    internal const float DefaultMaxShadowFloor = 0.65f;

    [DefaultValue(DefaultStartSize)]
    public float startSize = DefaultStartSize;

    [DefaultValue(DefaultStartSizeVariance)]
    public float startSizeVariance = DefaultStartSizeVariance;

    [DefaultValue(DefaultPlumeSize)]
    public float plumeSize = DefaultPlumeSize;

    [DefaultValue(DefaultGrowthExponent)]
    public float growthExponent = DefaultGrowthExponent;

    [DefaultValue(DefaultBuoyancy)]
    public float buoyancy = DefaultBuoyancy;

    [DefaultValue(DefaultDrag)]
    public float drag = DefaultDrag;

    [DefaultValue(DefaultAngularVelocityMax)]
    public float angularVelocityMax = DefaultAngularVelocityMax;

    [DefaultValue(DefaultSpeedNormMax)]
    public float speedNormMax = DefaultSpeedNormMax;

    [DefaultValue(DefaultSpeedLifetimeScale)]
    public float speedLifetimeScale = DefaultSpeedLifetimeScale;

    [DefaultValue(DefaultSpeedGrowthScale)]
    public float speedGrowthScale = DefaultSpeedGrowthScale;

    [DefaultValue(DefaultSpeedJitter)]
    public float speedJitter = DefaultSpeedJitter;

    [DefaultValue(DefaultTurbulenceStrengthMin)]
    public float turbulenceStrengthMin = DefaultTurbulenceStrengthMin;

    [DefaultValue(DefaultTurbulenceStrengthMax)]
    public float turbulenceStrengthMax = DefaultTurbulenceStrengthMax;

    [DefaultValue(DefaultTurbulenceFrequency)]
    public float turbulenceFrequency = DefaultTurbulenceFrequency;

    [DefaultValue(DefaultTurbulenceScrollSpeed)]
    public float turbulenceScrollSpeed = DefaultTurbulenceScrollSpeed;

    [DefaultValue(DefaultLightSaturation)]
    public float lightSaturation = DefaultLightSaturation;

    [DefaultValue(DefaultMaxShadowFloor)]
    public float maxShadowFloor = DefaultMaxShadowFloor;
}
