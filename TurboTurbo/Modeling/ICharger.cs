namespace TurboTurbo.Modeling;

public enum ChargerKind
{
    Turbo,
    Atmospheric,
}

/// <summary>Charger abstraction: allows the CombustionModel to be reused for both N/A and turbocharged engines.</summary>
public interface ICharger
{
    float Charge { get; }

    /// <summary>
    /// Rated charge at full power. This is a theoretical number intended for calibration; charge under actual simulation
    /// circumstances may deviate from this.
    /// </summary>
    float ChargeAtFullPower { get; }

    /// <summary>Volume compression ratio of the cylinders.</summary>
    float CompressionRatio { get; }

    /// <summary>Manifold/aftercooler heat-transfer effectiveness at rated flow.</summary>
    float IntakeNtu { get; }

    float Boost { get; }
    bool Surging { get; }
    float LambdaCalibration { get; }

    void Tick(float delta, float overfuel, float rpmNorm, float governorNorm,
        float exhaustEnergy, bool engineOn);

    ICharger Clone();
}
