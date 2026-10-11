using System;

using UnityEngine;

namespace TurboTurbo.Modeling;

/// <summary>
/// Self-contained model of a diesel engine: fueling and overfueling
/// driven by the air charge supplied by its <see cref="ICharger"/>.
/// </summary>
public sealed class CombustionModel
{
    /// <summary>Configuration constants for the exhaust-state quantities. Defaults give a reasonable starting point.</summary>
    public sealed class Settings
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

        /// <summary>Block temperature [K] where the radiator thermostat begins to open.</summary>
        public const float ThermostatOpenK = 65f + PhysicsConstants.KelvinOffset;

        /// <summary>Block temperature [K] where the radiator thermostat is fully open.</summary>
        public const float ThermostatFullOpenK = 85f + PhysicsConstants.KelvinOffset;

        /// <summary>
        /// Rated exhaust gas temperature [K] at the exhaust mouth at full power. Note that this is a target value;
        /// transient conditions may overshoot it.
        /// </summary>
        public float RatedExhaustTempK { get; set; } = DefaultRatedExhaustTempK;
        public float TauCylinder { get; set; } = DefaultTauCylinder;
        public float TauEngine { get; set; } = DefaultTauEngine;
        public float TauCooldownOpen { get; set; } = DefaultTauCooldownOpen;
        public float TauCooldownClosed { get; set; } = DefaultTauCooldownClosed;
        public float CylinderGainK { get; set; } = DefaultCylinderGainK;
        public float PolytropicExponent { get; set; } = DefaultPolytropicExponent;
        public float CompressorEfficiency { get; set; } = DefaultCompressorEfficiency;
        public float CompressionHeatLoss { get; set; } = DefaultCompressionHeatLoss;
        public float InductionWallHeating { get; set; } = DefaultInductionWallHeating;
        public float ResidualGasFraction { get; set; } = DefaultResidualGasFraction;
        public float ColdCompressionK { get; set; } = DefaultColdCompressionK;
        public float WarmCompressionK { get; set; } = DefaultWarmCompressionK;

        /// <summary>Floor on the temperature efficiency factor at the coldest extreme (0..1].</summary>
        public float MinBurnFractionAtCold { get; set; } = DefaultMinBurnFractionAtCold;

        public Settings()
        {
        }

        public Settings(Settings other)
        {
            RatedExhaustTempK = other.RatedExhaustTempK;
            TauCylinder = other.TauCylinder;
            TauEngine = other.TauEngine;
            TauCooldownOpen = other.TauCooldownOpen;
            TauCooldownClosed = other.TauCooldownClosed;
            CylinderGainK = other.CylinderGainK;
            PolytropicExponent = other.PolytropicExponent;
            CompressorEfficiency = other.CompressorEfficiency;
            CompressionHeatLoss = other.CompressionHeatLoss;
            InductionWallHeating = other.InductionWallHeating;
            ResidualGasFraction = other.ResidualGasFraction;
            ColdCompressionK = other.ColdCompressionK;
            WarmCompressionK = other.WarmCompressionK;
            MinBurnFractionAtCold = other.MinBurnFractionAtCold;
        }

        public void Validate()
        {
            RatedExhaustTempK = Mathf.Max(PhysicsConstants.ReferenceAmbientK + 0.01f, RatedExhaustTempK);
            TauCylinder = Mathf.Max(0.01f, TauCylinder);
            TauEngine = Mathf.Max(TauCylinder + 0.01f, TauEngine);
            TauCooldownOpen = Mathf.Max(0.01f, TauCooldownOpen);
            TauCooldownClosed = Mathf.Max(TauCooldownOpen, TauCooldownClosed);
            CylinderGainK = Mathf.Max(0.01f, CylinderGainK);
            PolytropicExponent = Mathf.Clamp(PolytropicExponent, 1.01f, PhysicsConstants.SpecificHeatRatio);
            CompressorEfficiency = Mathf.Clamp(CompressorEfficiency, 0.05f, 1f);
            CompressionHeatLoss = Mathf.Clamp(CompressionHeatLoss, 0f, 0.6f);
            InductionWallHeating = Mathf.Clamp01(InductionWallHeating);
            ResidualGasFraction = Mathf.Clamp(ResidualGasFraction, 0f, 0.2f);
            ColdCompressionK = Mathf.Max(0f, ColdCompressionK);
            WarmCompressionK = Mathf.Max(ColdCompressionK + 0.01f, WarmCompressionK);
            MinBurnFractionAtCold = Mathf.Clamp(MinBurnFractionAtCold, 0.01f, 1f);
        }
    }

    private readonly ExhaustVelocitySettings _velocity;

    private readonly Func<float> _governorNorm;
    private readonly Func<float> _fuelNorm;
    private readonly Func<float> _rpmNorm;
    private readonly Func<float> _ambientTemperatureK;

    // isentropic expansion exponent of intake/exhaust gas
    private const float IsentropicExponent =
        (PhysicsConstants.SpecificHeatRatio - 1f) / PhysicsConstants.SpecificHeatRatio;

    // Woschni's model: heat flux scales with (rpm * pressure)^0.8, cycle time with rpm^-1
    private const float WoschniChargeExponent = 0.8f;
    private const float WoschniRelExponent = -0.2f;

    // weight of the end-of-compression temperature in the cycle-mean gas temperature
    private const float GasMeanCompressionWeight = 0.5f;

    private const float MaxCompressionHeatLossFraction = 0.6f;
    private const float WallConductanceCeiling = 0.95f;

    public ICharger Charger { get; }

    public Settings Tuning { get; }

    /// <summary>Current charger boost pressure ratio [0..1], 0 when naturally aspirated.</summary>
    public float Boost { get; private set; }

    /// <summary>Raw clamped governor (rack) command read this tick.</summary>
    public float GovernorNorm { get; private set; }

    /// <summary>Raw clamped fuel consumption per unit time read this tick.</summary>
    public float FuelNorm { get; private set; }

    /// <summary>Normalized engine speed read this tick.</summary>
    public float RpmNorm { get; private set; }

    /// <summary>Fuel consumption per engine stroke, derived from <see cref="FuelNorm"/> and <see cref="RpmNorm"/>.</summary>
    public float FuelPerStroke { get; private set; }

    /// <summary>Per-stroke cylinder charge supplied by the charger.</summary>
    public float Charge { get; private set; }

    /// <summary>
    /// Air-fuel ratio proxy. Calibrated so that 1 means perfectly mixed. Below 1 is rich, above 1 is lean.
    /// </summary>
    public float Lambda { get; private set; }

    /// <summary>Overfueling amount [0..1]: portion of fuel consumption that has no air left to burn.</summary>
    public float Overfuel { get; private set; }

    /// <summary>Exhaust mass-flow proxy (per-stroke charge times engine speed).</summary>
    public float MassFlow { get; private set; }

    /// <summary>Exhaust gas temperature [K] at the exhaust mouth.</summary>
    public float GasTemperature { get; private set; }

    /// <summary>Exhaust gas density [kg/m^3] at the exhaust mouth.</summary>
    public float GasDensity { get; private set; }

    /// <summary>Temperature the turbine extracts before the exhaust reaches the mouth [K].</summary>
    public float TurbineTemperatureDropK { get; private set; }

    /// <summary>
    /// Normalized exhaust heat-release rate [0..1]: mass flow weighted by combustion efficiency.
    /// </summary>
    public float ExhaustEnergy { get; private set; }

    /// <summary>Exhaust plume speed [m/s] at the exhaust mouth.</summary>
    public float ExhaustVelocity { get; private set; }

    /// <summary>Temperature [K] of the cylinder walls.</summary>
    public float CylinderTempK { get; private set; }

    /// <summary>Temperature [K] of the engine block and oil.</summary>
    public float EngineTempK { get; private set; }

    /// <summary>Intake charge temperature [K] after compressor heating and manifold pickup.</summary>
    public float IntakeTempK { get; private set; }

    /// <summary>End-of-compression gas temperature [K].</summary>
    public float CompressionTempK { get; private set; }

    /// <summary>Exhaust manifold temperature [K], also used as the next tick's residual gas temperature.</summary>
    public float ManifoldTempK { get; private set; }

    /// <summary>Thermostat opening fraction [0..1]: 1 = fully open.</summary>
    public float ThermostatOpen { get; private set; }

    /// <summary>Air-limited combustion efficiency factor [0..1].</summary>
    public float BurnFractionAir { get; private set; }

    /// <summary>Temperature-limited combustion efficiency factor [0..1].</summary>
    public float BurnFractionTemp { get; private set; }

    /// <summary>Combined combustion efficiency factor [0..1].</summary>
    public float BurnFraction { get; private set; }

    /// <summary>True on the tick where the charger reported a surge. Reset on the next tick.</summary>
    public bool SurgeThisTick { get; private set; }

    public CombustionModel(Func<float> governorNorm, Func<float> fuelNorm, Func<float> rpmNorm,
        Func<float> ambientTemperatureK, ICharger charger, Settings settings, ExhaustVelocitySettings velocity,
        float? initialTemperatureK = null)
    {
        Charger = charger ?? throw new ArgumentNullException(nameof(charger));
        Tuning = settings ?? throw new ArgumentNullException(nameof(settings));
        _velocity = velocity ?? throw new ArgumentNullException(nameof(velocity));
        _governorNorm = governorNorm ?? throw new ArgumentNullException(nameof(governorNorm));
        _fuelNorm = fuelNorm ?? throw new ArgumentNullException(nameof(fuelNorm));
        _rpmNorm = rpmNorm ?? throw new ArgumentNullException(nameof(rpmNorm));
        _ambientTemperatureK = ambientTemperatureK ?? throw new ArgumentNullException(nameof(ambientTemperatureK));

        var initial = Mathf.Max(1f, initialTemperatureK ?? _ambientTemperatureK());
        CylinderTempK = initial;
        EngineTempK = initial;
    }

    /// <summary>
    /// Advances the simulation by <paramref name="delta"/> seconds
    /// </summary>
    public void Tick(float delta, bool engineOn)
    {
        var governor = Mathf.Clamp01(_governorNorm());
        var fuel = Mathf.Clamp01(_fuelNorm());
        var rpm = Mathf.Clamp01(_rpmNorm());

        var s = Tuning;

        var fuelPerStroke = fuel / Mathf.Max(0.01f, rpm);

        var ambient = Mathf.Max(1f, _ambientTemperatureK());

        GovernorNorm = governor;
        FuelNorm = fuel;
        RpmNorm = rpm;
        FuelPerStroke = fuelPerStroke;
        Charge = Charger.Charge;
        MassFlow = Charge * rpm; // fuel mass is so small relative to air mass as to be negligible

        var chargeFull = Charger.ChargeAtFullPower;
        // I'm quite sure this isn't exactly right: chargeFull is < 1 for NA engines, because charge drops as RPM -> 1
        // we should investigate consumers to figure out whether they really want charge normalised to max charge, or
        // to full-power charge, because these are not the same.
        var flowNorm = MassFlow / chargeFull;

        var calibration = Charger.LambdaCalibration;
        Lambda = Charge / (calibration * Mathf.Max(0.01f, fuelPerStroke));
        Overfuel = Mathf.Max(0f, fuelPerStroke - Charge / calibration);

        // the intake warms up inflowing air though compression and/or heat exchange
        IntakeTempK = IntakeTemperature(ambient, EngineTempK, Charge, flowNorm);
        // intake air mixes with residual gases and exchanges heat with the cylinder
        var trappedTemperature = TrappedTemperature(s, IntakeTempK, CylinderTempK, ManifoldTempK);
        // the mixture is then compressed, heating it up to the ignition point
        CompressionTempK = CompressionTemperature(trappedTemperature, flowNorm, CylinderTempK);

        // heavy overfueling reduces combustion efficiency.
        // simplified model for now; in reality, burn fraction already starts dropping before lambda reaches 1, as
        // non-perfect mixing causes local rich pockets to start to form in the cylinder even while global lambda is still lean
        BurnFractionAir = Mathf.Min(1f, Lambda);
        // low compression temperature also reduces combustion efficiency
        BurnFractionTemp = Mathf.Lerp(s.MinBurnFractionAtCold, 1f,
            MathExt.Smoothstep(s.ColdCompressionK, s.WarmCompressionK, CompressionTempK));
        BurnFraction = BurnFractionAir * BurnFractionTemp;

        // dynamically derive temperature gain based on rated exhaust temperature at load.
        // this makes it easier to alter temperature behaviour of the engine: just say what the rated exhaust gas
        // temperature at exhaust manifold should be, and the rest follows.
        var tempGainK = (s.RatedExhaustTempK - PhysicsConstants.ReferenceAmbientK) * chargeFull;
        var combustionRiseK = tempGainK * fuelPerStroke * BurnFraction / Charge;

        // the wall sits at a conductance-weighted mean of the cycle-mean gas temperature and the block
        var gasMeanK = GasMeanTemperature(trappedTemperature, CompressionTempK, combustionRiseK);
        // there is really no need to recompute this every frame, but that requires invalidation when settings change.
        // in practice, it's just a handful of ops * locos / frame, so it's not a big deal to recompute
        var wallRatio = WallConductanceRatio(chargeFull);
        var wallConductance = rpm > 0f
            ? wallRatio * Mathf.Pow(flowNorm, WoschniChargeExponent)
            : 0f;
        var cylinderTarget = (wallConductance * gasMeanK + EngineTempK) / (wallConductance + 1f);
        CylinderTempK += (cylinderTarget - CylinderTempK) * (1f - Mathf.Exp(-delta / s.TauCylinder));

        // the thermostat governs engine block cooling rate
        ThermostatOpen = MathExt.Smoothstep(Settings.ThermostatOpenK, Settings.ThermostatFullOpenK, EngineTempK);
        var cooldownTau = Mathf.Lerp(s.TauCooldownClosed, s.TauCooldownOpen, ThermostatOpen);

        // hot cylinders warm up the block, heat flows out of the block into the environment
        var engineTau = (s.TauEngine * cooldownTau) / (s.TauEngine + cooldownTau);
        var engineTarget = (CylinderTempK * cooldownTau + ambient * s.TauEngine)
                           / (s.TauEngine + cooldownTau);
        EngineTempK += (engineTarget - EngineTempK) * (1f - Mathf.Exp(-delta / engineTau));

        // we store this because it becomes the next tick's residual gas temperature
        ManifoldTempK = ambient + combustionRiseK;
        var turbinePressureRatio = Mathf.Max(1f, Charge);
        TurbineTemperatureDropK = ManifoldTempK *
                                  (1f - Mathf.Pow(turbinePressureRatio, -IsentropicExponent));

        GasTemperature = ManifoldTempK - TurbineTemperatureDropK;
        GasDensity = PhysicsConstants.ReferenceAirDensity * ambient / Mathf.Max(1f, GasTemperature);
        ExhaustEnergy = MassFlow * (ManifoldTempK - ambient) / tempGainK;
        ExhaustVelocity = _velocity.Calculate(MassFlow, GasDensity);

        Charger.Tick(delta, Overfuel, rpm, governor, ExhaustEnergy, engineOn);

        Boost = Charger.Boost;
        SurgeThisTick = Charger.Surging;
    }

    private float IntakeTemperature(float ambientK, float blockK, float charge, float flowNorm)
    {
        var s = Tuning;
        // air heats up from compression
        var pressureRatio = Mathf.Max(1f, charge);
        var compressorK = ambientK * (1f + (Mathf.Pow(pressureRatio, IsentropicExponent) - 1f) / s.CompressorEfficiency);

        // and exchanges heat with the manifold
        var flow = Mathf.Max(1e-3f, flowNorm);
        var effectiveness = 1f - Mathf.Exp(-Charger.IntakeNtu * Mathf.Pow(flow, WoschniRelExponent));

        return compressorK + effectiveness * (blockK - compressorK);
    }

    private static float TrappedTemperature(Settings s, float intakeK, float wallK, float residualK)
    {
        // air exchanges heat with the cylinder walls
        var warmed = intakeK + s.InductionWallHeating * (wallK - intakeK);
        // and mixes with the residual gases
        return (1f - s.ResidualGasFraction) * warmed + s.ResidualGasFraction * residualK;
    }

    private float CompressionTemperature(float trappedK, float flowNorm, float wallK)
    {
        var s = Tuning;
        var adiabaticK = trappedK * Mathf.Pow(Charger.CompressionRatio, s.PolytropicExponent - 1f);

        var flow = Mathf.Max(1e-3f, flowNorm);
        var lossFraction = Mathf.Min(MaxCompressionHeatLossFraction,
            s.CompressionHeatLoss * Mathf.Pow(flow, WoschniRelExponent));
        return adiabaticK - lossFraction * (adiabaticK - wallK);
    }

    private static float GasMeanTemperature(float trappedK, float compressionK, float riseK)
        => trappedK + GasMeanCompressionWeight * (compressionK - trappedK) + riseK;

    private float WallConductanceRatio(float chargeFull)
    {
        var s = Tuning;
        var blockK = Settings.ThermostatFullOpenK;
        var wallK = blockK + s.CylinderGainK;
        var riseK = s.RatedExhaustTempK - PhysicsConstants.ReferenceAmbientK;
        var intakeK = IntakeTemperature(PhysicsConstants.ReferenceAmbientK, blockK, chargeFull, 1f);
        var trappedK = TrappedTemperature(s, intakeK, wallK, s.RatedExhaustTempK);
        var compressionK = CompressionTemperature(trappedK, 1f, wallK);
        var gasMeanK = GasMeanTemperature(trappedK, compressionK, riseK);
        var x = Mathf.Clamp(s.CylinderGainK / Mathf.Max(0.01f, gasMeanK - blockK), 0f, WallConductanceCeiling);
        return x / (1f - x);
    }
}
