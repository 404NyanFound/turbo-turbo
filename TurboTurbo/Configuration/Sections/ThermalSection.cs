using System;

using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class ThermalSection
{
    public static Section Build(EngineSimulationHost host, Action onToggle)
    {
        var c = host.Profile.Combustion;
        var section = new Section("Thermal tracking") { OnToggle = onToggle };

        section.AddFloat("Chamber tau",
            "Combustion chamber surface thermal lag [s]. How quickly the surfaces (crowns, cylinders, etc) react to a change in engine power.\n" +
            "Lower this to make a cold engine's combustion clean up more quickly once it fires.",
            1f, 90f, false, () => c.TauChamber, v => { c.TauChamber = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block tau",
            "Combustion chamber to engine block thermal lag [s]. How quickly the chamber surfaces feed heat into the engine block.\n" +
            "Lower this to make the engine block respond more quickly to a change in chamber temperature.",
            10f, 1200f, false, () => c.TauEngine, v => { c.TauEngine = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block cooldown (open)",
            "Block-to-ambient cooldown lag [s] with the thermostat open.\n" +
            "Maximum cooling rate when the engine block is being cooled at full capacity. Lower = more cooling.",
            30f, 1200f, false, () => c.TauCooldownOpen, v => { c.TauCooldownOpen = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Block cooldown (closed)",
            "Block-to-ambient cooldown lag [s] with the thermostat shut.\n" +
            "Ambient cooling rate when the engine block is not being actively cooled. Lower = more cooling.",
            300f, 7200f, false, () => c.TauCooldownClosed, v => { c.TauCooldownClosed = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Chamber gain",
            "Combustion chamber surface temperature rise above the block at full power [K].\n" +
            "Raise this for hotter chamber surfaces, which help ignition and clear cold smoke sooner.",
            0f, 400f, false, () => c.ChamberGainK, v => { c.ChamberGainK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Polytropic exponent",
            "Exponent shaping the compression temperature rise at rated speed with warm walls.\n" +
            "Should stay at or below the specific heat ratio.",
            1.2f, 1.4f, false, () => c.PolytropicExponent, v => { c.PolytropicExponent = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Ignition temp",
            "End-of-compression temperature [K] at which half of the injected fuel ignites in time, at idle speed.\n" +
            "Raise this for a harder cold start, with more white smoke that takes longer to clear.",
            400f, 750f, false, () => c.IgnitionTempK, v => { c.IgnitionTempK = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Ignition spread",
            "Width of the transition from misfiring to clean combustion.\n" +
            "Higher values give a more gradual transition, with a longer tail of light smoke.",
            0.2f, 4f, false, () => c.IgnitionSpread, v => { c.IgnitionSpread = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Late burn work",
            "Work produced by fuel that misses the ignition window and burns late, relative to a clean burn.\n" +
            "Lower values make the governor inject more fuel into a cold engine, and send more heat into the exhaust.",
            0f, 1f, false, () => c.LateBurnWork, v => { c.LateBurnWork = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Quench fraction",
            "Fraction of the fuel that misses the ignition window and never burns at all.\n" +
            "This is what leaves the stack as white smoke, so it directly scales cold-start smoke.",
            0f, 1f, false, () => c.QuenchFraction, v => { c.QuenchFraction = v; c.Validate(); }, TweakGrade.Developer);
        section.AddFloat("Cranking fuel",
            "Fuel injected per stroke while the starter cranks the engine, where 1 is a full rack and 0.1 an idle stroke.\n" +
            "This affects how much fuel vapour is emitted out of the exhaust stack.",
            0f, 1f, false, () => c.CrankingFuelPerStroke, v => { c.CrankingFuelPerStroke = v; c.Validate(); }, TweakGrade.Developer);

        return section;
    }
}
