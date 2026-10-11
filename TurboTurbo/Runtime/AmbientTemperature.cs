namespace TurboTurbo.Runtime;

/// <summary>
/// Ambient temperature seen by the simulation.
/// </summary>
internal static class AmbientTemperature
{
    /// <summary>Ambient temperature [K].</summary>
    public static float Kelvin { get; set; } = PhysicsConstants.ReferenceAmbientK;

    /// <summary>Ambient temperature [°C].</summary>
    public static float Celsius
    {
        get => Kelvin - PhysicsConstants.KelvinOffset;
        set => Kelvin = value + PhysicsConstants.KelvinOffset;
    }
}
