using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class ThermalSection
{
    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var c = host.Profile.Combustion;
        var section = new Section("Thermal tracking") { OnToggle = onToggle };

        section.AddFloat("Cylinder tau",
            "Combustion to cylinder thermal lag [s]. How quickly cylinder temperature reacts to a change in engine power.\n" +
            "Lower this to make the cylinders respond more quickly to a change in engine power.",
            1f, 30f, false, () => c.TauCylinder, v => { c.TauCylinder = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block tau",
            "Cylinder to engine block thermal lag [s]. How quickly the cylinders feed heat into the engine block.\n" +
            "Lower this to make the engine block respond more quickly to a change in cylinder temperature.",
            1f, 360f, false, () => c.TauEngine, v => { c.TauEngine = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block cooldown (open)",
            "Block-to-ambient cooldown lag [s] with the thermostat open.\n" +
            "Maximum cooling rate when the engine block is being cooled at full capacity. Lower = more cooling.",
            30f, 1200f, false, () => c.TauCooldownOpen, v => { c.TauCooldownOpen = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block cooldown (closed)",
            "Block-to-ambient cooldown lag [s] with the thermostat shut.\n" +
            "Ambient cooling rate when the engine block is not being actively cooled. Lower = more cooling.",
            300f, 7200f, false, () => c.TauCooldownClosed, v => { c.TauCooldownClosed = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cylinder gain",
            "Cylinder temperature rise above the block at full power [K].\n" +
            "Raise this to increase the temperature difference between cylinders and engine block at steady-state full power.",
            0f, 200f, false, () => c.CylinderGainK, v => { c.CylinderGainK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Polytropic exponent",
            "Exponent shaping the compression temperature rise. Should stay at or below the specific heat ratio.",
            1.2f, 1.4f, false, () => c.PolytropicExponent, v => { c.PolytropicExponent = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Compressor efficiency",
            "Isentropic efficiency of the compressor, which sets how much the intake air is heated under boost.\n" +
            "Has no effect while the pressure ratio is 1.",
            0.3f, 1f, false, () => c.CompressorEfficiency, v => { c.CompressorEfficiency = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Compression heat loss",
            "Fraction of the compression temperature head lost to the walls at the rated point.\n" +
            "Higher values make a cold engine lose more compression heat, delaying ignition.",
            0f, 0.6f, false, () => c.CompressionHeatLoss, v => { c.CompressionHeatLoss = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Induction wall heating",
            "Fraction of the wall-to-intake temperature difference gained by the charge during induction.\n" +
            "Higher values let hot walls warm the incoming air more, which helps cold smoke clear.",
            0f, 0.5f, false, () => c.InductionWallHeating, v => { c.InductionWallHeating = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Residual gas fraction",
            "Fraction of the trapped charge that is leftover exhaust gas. Hot residual raises the charge temperature.",
            0f, 0.2f, false, () => c.ResidualGasFraction, v => { c.ResidualGasFraction = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cold compression temp",
            "End-of-compression temperature [K] below which the full cold-temperature combustion penalty is applied.",
            300f, 800f, false, () => c.ColdCompressionK, v => { c.ColdCompressionK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Warm compression temp",
            "End-of-compression temperature [K] above which the cold-temperature penalty has completely disappeared.",
            400f, 900f, false, () => c.WarmCompressionK, v => { c.WarmCompressionK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cold burn floor",
            "Maximum reachable combustion efficiency when the full cold-temperature penalty is applied.",
            0f, 1f, false, () => c.MinBurnFractionAtCold, v => { c.MinBurnFractionAtCold = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cranking fuel",
            "Fuel injected per stroke while the starter cranks the engine, where 1 is a full rack and 0.1 an idle stroke.\n" +
            "This affects how much fuel vapour is emitted out of the exhaust stack.",
            0f, 1f, false, () => c.CrankingFuelPerStroke, v => { c.CrankingFuelPerStroke = v; c.Validate(); }, TweakGrade.Developer);

        return section;
    }
}
