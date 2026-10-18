using System;

using Shouldly;

using TurboTurbo;
using TurboTurbo.Modeling;

using Xunit;

namespace TurboTurboTests
{
    public class CombustionModelTests
    {
        private float _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK;
        private readonly TurboCharger.Settings _chargerSettings = new TurboCharger.Settings();

        // shortcut for creating an engine that always provides perfect compression,
        // used for tests that don't consider the thermal model
        private readonly CombustionModel.Settings _perfectCompression = new CombustionModel.Settings
        {
            IgnitionTempK = 1f,
        };

        private readonly ExhaustVelocitySettings _velocitySettings = new ExhaustVelocitySettings();
        private float _governor;
        private float _fuelNorm;
        private float _rpmNorm = 1f;

        private CombustionModel CreateModel() => CreateModel(_perfectCompression);

        private CombustionModel CreateModel(CombustionModel.Settings combustion)
        {
            return new CombustionModel(() => _governor, () => _fuelNorm, () => _rpmNorm,
                () => _ambientTemperatureK, new TurboCharger(_chargerSettings), combustion, _velocitySettings);
        }

        [Fact]
        public void Settings_CopyConstructor_IsIndependent()
        {
            var template = new TurboCharger.Settings();
            var clone = new TurboCharger.Settings(template);

            clone.LambdaCalibration.ShouldBe(template.LambdaCalibration);
            clone.LambdaCalibration = 0.5f;
            template.LambdaCalibration.ShouldNotBe(0.5f);
        }

        // ------------------------------------------------------------
        // charge
        // ------------------------------------------------------------

        [Fact]
        public void Charge_AtZeroBoost_IsUnity()
        {
            var model = CreateModel();
            _fuelNorm = 0.4f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            // Charge = 1 + k x boost -> at boost 0 this is 1.0
            model.Charge.ShouldBe(1f, tolerance: 0.001f);
        }

        [Fact]
        public void Charge_AtFullBoost_Is_1_Plus_BoostChargeMultiplier()
        {
            var model = CreateModel();

            // hold full load long enough for boost to reach equilibrium
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            model.Boost.ShouldBe(1f, tolerance: 0.01f);
            model.Charge.ShouldBe(1f + _chargerSettings.BoostChargeMultiplier, tolerance: 0.01f);
        }

        // ------------------------------------------------------------
        // fuel
        // ------------------------------------------------------------

        [Fact]
        public void FuelPerStroke_DividesFuelNormByRpm()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 0.5f;
            model.Tick(0.016f, engineOn: true);

            model.FuelPerStroke.ShouldBe(1f, tolerance: 0.001f);
        }

        // ------------------------------------------------------------
        // boost dynamics
        // ------------------------------------------------------------

        [Fact]
        public void Boost_FirstTick_MatchesFirstOrderLagWithTauUp()
        {
            var model = CreateModel();

            // clean partial load: fuel per stroke 0.4, tau = TauUp = 3, delta 1s:
            // boost = 0.4 x (1 - e^(-1/3)) = 0.1134
            _fuelNorm = 0.4f;
            _rpmNorm = 1f;
            model.Tick(1f, engineOn: true);

            model.Boost.ShouldBe(0.4f * (1f - (float)Math.Exp(-1f / 3f)), tolerance: 0.001f);
        }

        [Fact]
        public void Boost_Rises_Monotonically_UnderSustainedFullLoad()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;

            var last = 0f;
            for (var i = 0; i < 50; i++)
            {
                model.Tick(0.1f, engineOn: true);
                model.Boost.ShouldBeGreaterThanOrEqualTo(last);
                last = model.Boost;
            }
        }

        [Fact]
        public void Boost_Decays_AfterFuelDrops()
        {
            var model = CreateModel();

            // spool up first
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }
            var peak = model.Boost;

            // cut fuel: boost must bleed off through TauDown
            _fuelNorm = 0f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(0.5f, engineOn: true);
                model.Boost.ShouldBeLessThan(peak);
            }
        }

        // ------------------------------------------------------------
        // surge detection
        // ------------------------------------------------------------

        [Fact]
        public void Surge_Detected_OnSharpGovernorDropAtHighBoost()
        {
            var model = CreateModel();

            // spool boost above 0.75: 5 ticks of 1s at full load
            _fuelNorm = 1f;
            _governor = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }
            model.Boost.ShouldBeGreaterThan(0.75f);

            // slam governor to 0.69 in one 16ms frame: rate = 0.31 / 0.016 = 19.4/s
            // beats the 15/s threshold, and the 16ms decay leaves boost above
            // the 0.75 gate.
            _governor = 0.69f;
            model.Tick(0.016f, engineOn: true);
            model.SurgeThisTick.ShouldBeTrue();
        }

        [Fact]
        public void NoSurge_WhenGovernorDropIsGradual()
        {
            var model = CreateModel();

            _fuelNorm = 1f;
            _governor = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            // ease down to 0.69 over 20 frames: per-frame rate ~1/s, far
            // below the threshold even though the total drop matches
            for (var i = 0; i < 20; i++)
            {
                _governor = 1f - 0.31f * (i + 1) / 20f;
                model.Tick(0.016f, engineOn: true);
                model.SurgeThisTick.ShouldBeFalse();
            }
        }

        [Fact]
        public void NoSurge_WhenGovernorIsSteady()
        {
            var model = CreateModel();

            _fuelNorm = 1f;
            _governor = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 5; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            model.SurgeThisTick.ShouldBeFalse();
        }

        // ------------------------------------------------------------
        // exhaust state
        // ------------------------------------------------------------

        [Fact]
        public void CombustionSettings_CopyConstructor_IsIndependent()
        {
            var template = new CombustionModel.Settings();
            var clone = new CombustionModel.Settings(template);

            clone.RatedExhaustTempK.ShouldBe(template.RatedExhaustTempK);
            clone.RatedExhaustTempK = 123f;
            template.RatedExhaustTempK.ShouldNotBe(123f);
            clone.TauChamber.ShouldBe(template.TauChamber);
            clone.ChamberGainK += 10f;
            template.ChamberGainK.ShouldNotBe(clone.ChamberGainK);
            clone.IgnitionTempK.ShouldBe(template.IgnitionTempK);
        }

        [Fact]
        public void CombustionSettings_Validate_FloorsRatedExhaustTemp()
        {
            var settings = new CombustionModel.Settings { RatedExhaustTempK = 100f };
            settings.Validate();

            settings.RatedExhaustTempK.ShouldBeGreaterThan(PhysicsConstants.ReferenceAmbientK);
        }

        [Fact]
        public void MassFlow_IsChargeTimesRpm()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 0.5f;
            model.Tick(0.016f, engineOn: true);

            // first tick: Charge = 1 (zero boost), rpm = 0.5
            model.MassFlow.ShouldBe(0.5f, tolerance: 0.001f);
        }

        [Fact]
        public void GasTemperature_Lean_UsesSpecificHeatRelease()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.GasTemperature.ShouldBe(789.178f, tolerance: 0.01f);
        }

        [Fact]
        public void GasDensity_Falls_AsTemperatureRises()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.GasDensity.ShouldBe(
                PhysicsConstants.ReferenceAirDensity * PhysicsConstants.ReferenceAmbientK / 789.178f,
                tolerance: 0.001f);
        }

        [Fact]
        public void GasTemperature_ManifoldReachesRatedTemp_AtFullPower()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            model.Charge.ShouldBe(model.Charger.RatedCharge, tolerance: 0.01f);
            var manifoldK = model.GasTemperature + model.TurbineTemperatureDropK;
            manifoldK.ShouldBe(CombustionModel.Settings.DefaultRatedExhaustTempK, tolerance: 0.5f);
        }

        [Fact]
        public void TurbineDrop_IsZero_WithoutBoost()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            // first tick: Charge = 1 (no boost), so the gas is not expanded by a turbine
            model.TurbineTemperatureDropK.ShouldBe(0f, tolerance: 0.001f);
        }

        [Fact]
        public void TurbineDrop_AtFullPower_MatchesTurbineExpansion()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            // manifold 760 K expanded across the charge pressure ratio, gamma = 1.4 -> exponent 0.2857,
            // by a turbine that extracts 78% of the isentropic drop
            var expectedDrop = CombustionModel.Settings.DefaultRatedExhaustTempK * 0.78f *
                               (1f - (float)Math.Pow(model.Charger.RatedCharge, -0.28571));
            model.TurbineTemperatureDropK.ShouldBe(expectedDrop, tolerance: 0.5f);
        }

        [Fact]
        public void GasTemperature_TracksAmbientInput()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK - 20f;
            model.Tick(0.016f, engineOn: true);

            // gain is fixed, so EGT shifts down by the same 20 K the ambient input dropped
            model.GasTemperature.ShouldBe(789.178f - 20f, tolerance: 0.01f);
        }

        [Fact]
        public void ExhaustEnergy_IsScaledByCombustionEfficiency()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.ExhaustEnergy.ShouldBe(1f / 1.41f, tolerance: 0.001f);
            model.Overfuel.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void ExhaustEnergy_IsHigher_WhenLean()
        {
            var model = CreateModel();
            _fuelNorm = 0.3f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            // Lambda > 1, so burn = 1 and energy is the fuel rate itself
            model.ExhaustEnergy.ShouldBe(0.3f, tolerance: 0.001f);
        }

        // ------------------------------------------------------------
        // thermal tracking
        // ------------------------------------------------------------

        [Fact]
        public void Ignition_FarTooCold_NothingIgnites()
        {
            var model = CreateModel(new CombustionModel.Settings { IgnitionTempK = 2000f });
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            model.Tick(0.016f, engineOn: true);

            model.BurnFractionTemp.ShouldBe(0f);
            model.BurnFraction.ShouldBe(0f);
        }

        [Fact]
        public void Ignition_WarmEngine_BurnsEverything()
        {
            var model = CreateModel(new CombustionModel.Settings());
            model.SetTemperatureK(CombustionModel.Settings.ThermostatFullOpenK);
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            model.Tick(0.016f, engineOn: true);

            model.BurnFractionTemp.ShouldBe(1f);
            model.UnburnedFuel.ShouldBe(0f);
            model.InjectedPerStroke.ShouldBe(model.FuelPerStroke, tolerance: 1e-5f);
        }

        [Fact]
        public void Ignition_HotterCompression_WidensMargin()
        {
            var cold = CreateModel(new CombustionModel.Settings());
            var warm = CreateModel(new CombustionModel.Settings());
            warm.SetTemperatureK(CombustionModel.Settings.ThermostatFullOpenK);
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            cold.Tick(0.016f, engineOn: true);
            warm.Tick(0.016f, engineOn: true);

            warm.CompressionTempK.ShouldBeGreaterThan(cold.CompressionTempK);
            warm.IgnitionMargin.ShouldBeGreaterThan(cold.IgnitionMargin);
        }

        [Fact]
        public void Governor_Misfiring_InjectsExtraFuelToMeetDemand()
        {
            var settings = new CombustionModel.Settings { IgnitionTempK = 600f };
            var model = CreateModel(settings);
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            model.Tick(0.016f, engineOn: true);

            model.BurnFractionTemp.ShouldBeInRange(0.15f, 0.95f);
            // the extra fuel makes up exactly for the work lost to late burning
            (model.InjectedPerStroke * model.WorkEfficiency).ShouldBe(model.FuelPerStroke, tolerance: 1e-4f);
            var missed = model.InjectedPerStroke * (1f - model.BurnFractionTemp) * _rpmNorm;
            model.UnburnedFuel.ShouldBe(missed * settings.QuenchFraction, tolerance: 1e-5f);
            model.LateBurnedFuel.ShouldBe(missed * (1f - settings.QuenchFraction), tolerance: 1e-5f);
        }

        [Fact]
        public void Governor_RackLimit_CapsInjection()
        {
            var settings = new CombustionModel.Settings { IgnitionTempK = 2000f };
            var model = CreateModel(settings);
            _fuelNorm = 0.2f;
            _rpmNorm = 0.3f;
            model.Tick(0.016f, engineOn: true);

            model.InjectedPerStroke.ShouldBe(1f);
            model.UnburnedFuel.ShouldBe(settings.QuenchFraction * _rpmNorm, tolerance: 1e-5f);
        }

        [Fact]
        public void Governor_NothingIgnitesInTime_StillBurnsMostFuelLate()
        {
            var settings = new CombustionModel.Settings { IgnitionTempK = 2000f };
            var model = CreateModel(settings);
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            model.Tick(0.016f, engineOn: true);

            model.BurnFractionTemp.ShouldBe(0f);
            model.WorkEfficiency.ShouldBe((1f - settings.QuenchFraction) * settings.LateBurnWork, tolerance: 1e-5f);
            model.LateBurnedFuel.ShouldBeGreaterThan(model.UnburnedFuel);
        }

        [Fact]
        public void Thermal_MisfiringEngine_SendsMoreHeatIntoTheExhaust()
        {
            // late burning produces less work, so the governor injects more, and the lost work heats the exhaust
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;

            var cold = CreateModel(new CombustionModel.Settings { IgnitionTempK = 2000f });
            cold.Tick(0.016f, engineOn: true);

            var warm = CreateModel(_perfectCompression);
            warm.Tick(0.016f, engineOn: true);

            cold.ExhaustEnergy.ShouldBeGreaterThan(warm.ExhaustEnergy);
            cold.UnburnedFuel.ShouldBeGreaterThan(warm.UnburnedFuel);
        }

        [Fact]
        public void Cranking_NothingIgnitesInTime_InjectsFixedAmount()
        {
            var model = CreateModel(new CombustionModel.Settings { IgnitionTempK = 2000f });
            _fuelNorm = 0f;
            _rpmNorm = 0.1f;
            model.Tick(0.016f, engineOn: false, cranking: true);

            model.InjectedPerStroke.ShouldBe(CombustionModel.Settings.DefaultCrankingFuelPerStroke, tolerance: 1e-5f);
        }

        [Fact]
        public void Thermal_WarmsUp_AndRecoversCombustionEfficiency()
        {
            var cold = new CombustionModel.Settings();
            var model = CreateModel(cold);
            _fuelNorm = 1f;
            _rpmNorm = 1f;

            for (var i = 0; i < 20000; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            model.BurnFractionTemp.ShouldBe(1f, tolerance: 0.01f);
            model.ChamberTempK.ShouldBeGreaterThan(model.EngineTempK);
            model.EngineTempK.ShouldBeGreaterThan(CombustionModel.Settings.ThermostatOpenK);
        }

        [Fact]
        public void Thermostat_OpensAcrossBand_AsBlockWarms()
        {
            var model = CreateModel(new CombustionModel.Settings());
            _fuelNorm = 1f;
            _rpmNorm = 1f;

            model.Tick(0.1f, engineOn: true);
            model.ThermostatOpen.ShouldBe(0f);

            for (var i = 0; i < 20000; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            model.EngineTempK.ShouldBeGreaterThan(CombustionModel.Settings.ThermostatOpenK);
            model.ThermostatOpen.ShouldBeGreaterThan(0.9f);
        }

        [Fact]
        public void Thermal_Validate_ClampsGainsAndThresholds()
        {
            var settings = new CombustionModel.Settings
            {
                TauChamber = 500f,
                TauEngine = 100f,
                ChamberGainK = -1f,
                TauCooldownOpen = -5f,
                TauCooldownClosed = -5f,
                PolytropicExponent = 3f,
                IgnitionTempK = -10f,
                IgnitionSpread = 0f,
            };

            settings.Validate();

            settings.TauEngine.ShouldBeGreaterThan(settings.TauChamber);
            settings.ChamberGainK.ShouldBeGreaterThan(0f);
            settings.TauCooldownOpen.ShouldBeGreaterThan(0f);
            settings.TauCooldownClosed.ShouldBeGreaterThan(0f);
            settings.PolytropicExponent.ShouldBeInRange(1.01f, PhysicsConstants.SpecificHeatRatio);
            settings.IgnitionTempK.ShouldBeGreaterThan(0f);
            settings.IgnitionSpread.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void Thermal_UnclampedCylinderGain_KeepsWallFinite()
        {
            var settings = new CombustionModel.Settings { ChamberGainK = 100000f };
            settings.Validate();

            settings.ChamberGainK.ShouldBeGreaterThan(0f);

            // an unvalidated absurd gain must still keep the wall finite at runtime
            var model = CreateModel(new CombustionModel.Settings { ChamberGainK = 100000f });
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 100; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            float.IsNaN(model.ChamberTempK).ShouldBeFalse();
            model.ChamberTempK.ShouldBeGreaterThan(0f);
        }

        // ------------------------------------------------------------
        // intake
        // ------------------------------------------------------------

        [Fact]
        public void Intake_AtZeroBoostAndColdBlock_EqualsAmbient()
        {
            var model = CreateModel();
            _fuelNorm = 0f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.IntakeTempK.ShouldBe(_ambientTemperatureK, tolerance: 0.01f);
        }

        [Fact]
        public void Intake_RisesUnderBoost()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                model.Tick(0.1f, engineOn: true);
            }

            model.Boost.ShouldBeGreaterThan(0.9f);
            model.IntakeTempK.ShouldBeGreaterThan(_ambientTemperatureK);
        }

        [Fact]
        public void Intake_WarmBlockAtIdle_WarmsAboveAmbient()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 6000; i++)
            {
                model.Tick(0.5f, engineOn: true);
            }

            _fuelNorm = 0.01f;
            _rpmNorm = 0.3f;
            for (var i = 0; i < 200; i++)
            {
                model.Tick(0.5f, engineOn: true);
            }

            model.Boost.ShouldBeLessThan(0.05f);
            model.EngineTempK.ShouldBeGreaterThan(_ambientTemperatureK);
            model.IntakeTempK.ShouldBeGreaterThan(_ambientTemperatureK);
        }

        // ------------------------------------------------------------
        // wall calibration and ordering
        // ------------------------------------------------------------

        [Fact]
        public void Wall_Settles_CylinderGainAboveBlock_AtRatedPoint()
        {
            var model = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 40000; i++)
            {
                model.Tick(0.5f, engineOn: true);
            }

            (model.ChamberTempK - model.EngineTempK)
                .ShouldBe(CombustionModel.Settings.DefaultChamberGainK, tolerance: 5f);
        }

        [Fact]
        public void Compression_ColderAmbient_IsLowerAtSettledIdle()
        {
            _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK - 40f;
            var cold = CreateModel();
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            for (var i = 0; i < 7200; i++)
            {
                cold.Tick(1f, engineOn: true);
            }

            _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK;
            var warm = CreateModel();
            for (var i = 0; i < 7200; i++)
            {
                warm.Tick(1f, engineOn: true);
            }

            warm.CompressionTempK.ShouldBeGreaterThan(cold.CompressionTempK);
        }

        [Fact]
        public void Compression_FullLoad_IsHigherThanIdle()
        {
            var idle = CreateModel();
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            for (var i = 0; i < 7200; i++)
            {
                idle.Tick(1f, engineOn: true);
            }

            var full = CreateModel();
            _fuelNorm = 1f;
            _rpmNorm = 1f;
            for (var i = 0; i < 7200; i++)
            {
                full.Tick(1f, engineOn: true);
            }

            full.CompressionTempK.ShouldBeGreaterThan(idle.CompressionTempK);
        }

        private float ExponentAt(float rpm, float wallK)
        {
            var model = CreateModel(new CombustionModel.Settings());
            model.SetTemperatureK(wallK);
            _rpmNorm = rpm;
            _fuelNorm = 0f;
            model.Tick(0.001f, engineOn: true);
            return model.CompressionExponent;
        }

        [Fact]
        public void Exponent_WarmWallsAtRatedSpeed_IsRatedExponent()
        {
            ExponentAt(1f, CombustionModel.Settings.ThermostatFullOpenK)
                .ShouldBe(new CombustionModel.Settings().PolytropicExponent, tolerance: 1e-4f);
        }

        [Fact]
        public void Exponent_LowerSpeed_IsLower()
        {
            var warmK = CombustionModel.Settings.ThermostatFullOpenK;
            var cranking = ExponentAt(0.1f, warmK);
            var idle = ExponentAt(0.3f, warmK);
            var full = ExponentAt(1f, warmK);

            idle.ShouldBeLessThan(full);
            cranking.ShouldBeLessThan(idle);
        }

        [Fact]
        public void Exponent_ColdWalls_IsLower()
        {
            var warm = ExponentAt(1f, CombustionModel.Settings.ThermostatFullOpenK);
            var cold = ExponentAt(1f, PhysicsConstants.ReferenceAmbientK - 40f);

            cold.ShouldBe(warm - CombustionModel.ColdWallExponentDrop, tolerance: 1e-4f);
        }

        [Fact]
        public void Exponent_Standstill_IsNotBelowOne()
        {
            ExponentAt(0f, PhysicsConstants.ReferenceAmbientK - 40f).ShouldBeGreaterThanOrEqualTo(1f);
        }

        [Fact]
        public void Cranking_ColdEngine_CompressionIsInRealisticRange()
        {
            // a large diesel cranked from cold at 20 C reaches roughly 540..620 K at the end of compression
            var model = CreateModel(new CombustionModel.Settings());
            _rpmNorm = 0.1f;
            _fuelNorm = 0f;
            for (var i = 0; i < 50; i++)
            {
                model.Tick(0.1f, engineOn: false, cranking: true);
            }

            model.CompressionTempK.ShouldBeInRange(500f, 620f);
        }

        [Fact]
        public void ColdStart_IdleAtReference_WarmsUntilNoTemperaturePenalty()
        {
            var model = CreateModel();
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            for (var i = 0; i < 1800; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            model.BurnFractionTemp.ShouldBe(1f, tolerance: 0.01f);
        }

        [Fact]
        public void WarmTurboIdle_ColdAmbient_StaysNearlyPerfect()
        {
            _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK - 40f;
            var model = CreateModel();
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            for (var i = 0; i < 21600; i++)
            {
                model.Tick(1f, engineOn: true);
            }

            model.BurnFractionTemp.ShouldBeGreaterThanOrEqualTo(0.99f);
        }

        [Fact]
        public void WarmNaturallyAspiratedIdle_ColdAmbient_HasLessIgnitionMarginThanTurbo()
        {
            // without a warm aftercooler, cold intake air keeps a naturally aspirated engine closer to misfiring
            _ambientTemperatureK = PhysicsConstants.ReferenceAmbientK - 40f;
            var charger = new AtmosphericCharger(new AtmosphericCharger.Settings
            {
                EtaPeak = 0.86f,
                ChokeK = 0.285f,
                LambdaCalibration = 0.525f,
            });
            var aspirated = new CombustionModel(() => _governor, () => _fuelNorm, () => _rpmNorm,
                () => _ambientTemperatureK, charger, new CombustionModel.Settings(), _velocitySettings);
            var turbo = CreateModel(new CombustionModel.Settings());
            _fuelNorm = 0.03f;
            _rpmNorm = 0.3f;
            for (var i = 0; i < 21600; i++)
            {
                aspirated.Tick(1f, engineOn: true);
                turbo.Tick(1f, engineOn: true);
            }

            aspirated.IgnitionMargin.ShouldBeLessThan(turbo.IgnitionMargin);
        }

        // ------------------------------------------------------------
        // exhaust velocity
        // ------------------------------------------------------------

        [Fact]
        public void VelocitySettings_CopyConstructor_IsIndependent()
        {
            var template = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 7f };
            var clone = new ExhaustVelocitySettings(template);

            clone.ExhaustVelocityCoefficient.ShouldBe(7f);
            clone.ExhaustVelocityCoefficient = 3f;
            template.ExhaustVelocityCoefficient.ShouldBe(7f);
        }

        [Fact]
        public void VelocitySettings_Validate_FloorsCoefficient()
        {
            var settings = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 0f };
            settings.Validate();

            settings.ExhaustVelocityCoefficient.ShouldBeGreaterThan(0f);
        }

        [Fact]
        public void VelocitySettings_Calculate_IsCoefficientTimesFlowOverDensity()
        {
            var settings = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 4f };

            settings.Calculate(3f, 1.5f).ShouldBe(8f, tolerance: 0.001f);
        }

        [Fact]
        public void VelocitySettings_Calculate_FloorsAtMinimum()
        {
            var settings = new ExhaustVelocitySettings { ExhaustVelocityCoefficient = 4f };

            settings.Calculate(0f, 1f).ShouldBe(ExhaustVelocitySettings.MinExhaustVelocity, tolerance: 0.001f);
        }

        [Fact]
        public void ExhaustVelocity_IsCoefficientTimesFlowOverDensity()
        {
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.ExhaustVelocity.ShouldBe(
                _velocitySettings.ExhaustVelocityCoefficient * model.MassFlow / model.GasDensity,
                tolerance: 0.001f);
        }

        [Fact]
        public void ExhaustVelocity_ScalesWithCoefficient()
        {
            _velocitySettings.ExhaustVelocityCoefficient = 8f;
            var model = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            model.Tick(0.016f, engineOn: true);

            model.ExhaustVelocity.ShouldBe(8f * model.MassFlow / model.GasDensity, tolerance: 0.001f);
        }

        [Fact]
        public void ExhaustVelocity_IsHigher_WithMoreBoostCharge()
        {
            var low = CreateModel();
            _fuelNorm = 0.5f;
            _rpmNorm = 1f;
            for (var i = 0; i < 600; i++)
            {
                low.Tick(0.1f, engineOn: true);
            }

            _chargerSettings.BoostChargeMultiplier = 3f;
            var high = CreateModel();
            for (var i = 0; i < 600; i++)
            {
                high.Tick(0.1f, engineOn: true);
            }

            high.ExhaustVelocity.ShouldBeGreaterThan(low.ExhaustVelocity);
        }

        // ------------------------------------------------------------
        // cranking
        // ------------------------------------------------------------

        [Fact]
        public void Cranking_WithoutGameFuel_InjectsCrankingFuelPerStroke()
        {
            var model = CreateModel();
            _fuelNorm = 0f;
            _rpmNorm = 0.1f;
            model.Tick(0.016f, engineOn: false, cranking: true);

            model.FuelPerStroke.ShouldBe(_perfectCompression.CrankingFuelPerStroke, tolerance: 0.001f);
            model.FuelNorm.ShouldBe(_perfectCompression.CrankingFuelPerStroke * _rpmNorm, tolerance: 0.001f);
        }

        [Fact]
        public void NotCranking_WithoutGameFuel_InjectsNothing()
        {
            var model = CreateModel();
            _fuelNorm = 0f;
            _rpmNorm = 0.1f;
            model.Tick(0.016f, engineOn: false);

            model.FuelPerStroke.ShouldBe(0f);
            model.FuelNorm.ShouldBe(0f);
        }

        [Fact]
        public void Cranking_WithMoreGameFuel_UsesGameFuel()
        {
            var model = CreateModel();
            _rpmNorm = 0.1f;
            _fuelNorm = _perfectCompression.CrankingFuelPerStroke * _rpmNorm * 2f;
            model.Tick(0.016f, engineOn: true, cranking: true);

            model.FuelNorm.ShouldBe(_fuelNorm, tolerance: 0.001f);
        }

        [Fact]
        public void Cranking_ColdEngine_LeavesFuelUnburned()
        {
            var model = CreateModel(new CombustionModel.Settings());
            _fuelNorm = 0f;
            _rpmNorm = 0.1f;
            model.Tick(0.016f, engineOn: false, cranking: true);

            model.BurnFractionTemp.ShouldBeLessThan(1f);
            model.ExhaustEnergy.ShouldBeGreaterThan(0f);
        }
    }
}
