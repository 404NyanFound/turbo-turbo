using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.Current;

public sealed class VelocityXml
{
    internal const float DefaultExhaustVelocityCoefficient = 5f;

    [DefaultValue(DefaultExhaustVelocityCoefficient)]
    public float ExhaustVelocityCoefficient = DefaultExhaustVelocityCoefficient;
}
