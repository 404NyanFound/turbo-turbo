using System;
using System.Collections.Generic;

using TurboTurbo.Effects;
using TurboTurbo.Runtime;

namespace TurboTurbo.Configuration.Sections;

internal static class SmokeEmitterSection
{
    public static Section Build(EngineSimulationHost host, Action onRequiresReconfigure, Action onToggle)
    {
        var smokes = new List<SmokeParticles>();
        foreach (var e in host.Exhausts)
        {
            smokes.Add(e.Smoke);
        }

        if (smokes.Count == 0) return null;

        var section = new Section("Smoke emitter", onRequiresReconfigure) { OnToggle = onToggle };
        var f = smokes[0];
        section.AddFloat("Start size", "Smoke plume diameter [m] at emission.\n" +
                                       "Change this so the smoke visually matches the width of the exhaust pipe.",
            0.1f, 3f, true, () => f.tuning.startSize, v => { foreach (var s in smokes) s.tuning.startSize = v; }, TweakGrade.Basic);
        section.AddFloat("Plume size", "Smoke plume diameter [m] after five seconds in still air.",
            0.5f, 15f, true, () => f.tuning.plumeSize, v => { foreach (var s in smokes) s.tuning.plumeSize = v; }, TweakGrade.Basic);
        section.AddFloat("Size curve shape", "Exponent shaping how the smoke plume grows over time.\n" +
                                             " * 1 grows at a constant rate (linear).\n" +
                                             " * Below 1 expands quickly, then slows.",
            0.1f, 1f, true, () => f.tuning.growthExponent, v => { foreach (var s in smokes) s.tuning.growthExponent = v; });
        section.AddFloat("Size variance", "Random variance in particle size:\n" +
                                          " * 0 means all particles are the exact same size as similarly-aged neighbours.\n" +
                                          " * 1 means all particles can be anywhere between half as small and twice as big relative to their neighbours.",
            0f, 1f, true, () => f.tuning.startSizeVariance, v => { foreach (var s in smokes) s.tuning.startSizeVariance = v; }, TweakGrade.Basic);

        // Developer-grade: some of these are better suited to global settings, but they live here for now.
        section.AddFloat("Drag", "Strength of air drag on the particles.",
            0f, 3f, true, () => f.tuning.drag, v => { foreach (var s in smokes) s.tuning.drag = v; }, TweakGrade.Developer);
        section.AddFloat("Buoyancy", "Constant upward drift [m/s].",
            -1f, 2f, true, () => f.tuning.buoyancy, v => { foreach (var s in smokes) s.tuning.buoyancy = v; }, TweakGrade.Developer);
        section.AddFloat("Max angular velocity", "Max random spin speed [deg/s], sign-randomized per particle.",
            0f, 90f, false, () => f.tuning.angularVelocityMax, v => { foreach (var s in smokes) s.tuning.angularVelocityMax = v; }, TweakGrade.Developer);
        section.AddFloat("Turbulence min strength", "Turbulence noise field strength at standstill.",
            0f, 2.5f, false, () => f.tuning.turbulenceStrengthMin, v => { foreach (var s in smokes) s.tuning.turbulenceStrengthMin = v; }, TweakGrade.Developer);
        section.AddFloat("Turbulence max strength", "Turbulence noise field strength at full dispersion speed.",
            0f, 2.5f, false, () => f.tuning.turbulenceStrengthMax, v => { foreach (var s in smokes) s.tuning.turbulenceStrengthMax = v; }, TweakGrade.Developer);
        section.AddFloat("Turbulence frequency", "Turbulence noise field frequency (lower = larger cells).",
            0.05f, 2f, true, () => f.tuning.turbulenceFrequency, v => { foreach (var s in smokes) s.tuning.turbulenceFrequency = v; }, TweakGrade.Developer);
        section.AddFloat("Turbulence scroll speed", "Turbulence noise field scroll speed.",
            0f, 3f, true, () => f.tuning.turbulenceScrollSpeed, v => { foreach (var s in smokes) s.tuning.turbulenceScrollSpeed = v; }, TweakGrade.Developer);
        section.AddFloat("Light saturation", "Environmental light color saturation on the smoke. Lower values limit environmental coloring.",
            0f, 1f, false, () => f.tuning.lightSaturation, v => { foreach (var s in smokes) s.SetLightSaturation(v); }, TweakGrade.Developer);
        section.AddFloat("Max shadow floor", "Adjusts shadow intensity based on smoke color. Higher values reduce shadow intensity on light-colored smoke.",
            0f, 1f, false, () => f.tuning.maxShadowFloor, v => { foreach (var s in smokes) s.SetMaxShadowFloor(v); }, TweakGrade.Developer);
        section.AddFloat("Max dispersion speed", "Locomotive speed [m/s] at which dispersion reaches full strength.",
            1f, 30f, true, () => f.tuning.speedNormMax, v => { foreach (var s in smokes) s.tuning.speedNormMax = v; }, TweakGrade.Developer);
        section.AddFloat("Dispersion lifetime multiplier", "Particle lifetime multiplier at full dispersion.",
            0f, 1f, true, () => f.tuning.speedLifetimeScale, v => { foreach (var s in smokes) s.tuning.speedLifetimeScale = v; }, TweakGrade.Developer);
        section.AddFloat("Dispersion growth multiplier", "Particle growth rate multiplier at full dispersion.",
            1f, 5f, true, () => f.tuning.speedGrowthScale, v => { foreach (var s in smokes) s.tuning.speedGrowthScale = v; }, TweakGrade.Developer);
        section.AddFloat("Dispersion jitter", "Extra emission jitter [m/s] at full dispersion.",
            0f, 2f, false, () => f.tuning.speedJitter, v => { foreach (var s in smokes) s.tuning.speedJitter = v; }, TweakGrade.Developer);
        return section;
    }
}
