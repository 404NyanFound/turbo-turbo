using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DV.Simulation.Cars;
using DV.ThingTypes;

using TurboTurbo.Profiles;
using TurboTurbo.Runtime;

using UnityEngine;

namespace TurboTurbo.DevUI;

/// <summary>
/// Dumps the details needed to write a Controller.ConfigureEngine entry for the player's train.
/// </summary>
internal static class PlayerTrainInspector
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("playerTrain");

    public static void DumpTarget()
    {
        var car = PlayerManager.Car ?? PlayerManager.LastLoco;
        if (car == null)
        {
            Log.Info("no player car to dump");
            return;
        }

        Dump(car);
    }

    public static void Dump(TrainCar car)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"car: {car.name} (id={car.DisplayId()}, type={car.carType})");

        if (car.carLivery == null)
        {
            sb.AppendLine("livery: none");
        }
        else
        {
            DumpLivery(sb, car.carLivery);
        }

        var systems = car.GetComponentsInChildren<ParticleSystem>(true);
        sb.AppendLine($"particle systems: {systems.Length}");
        foreach (var ps in systems)
        {
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var texture = renderer != null && renderer.sharedMaterial != null
                ? renderer.sharedMaterial.mainTexture
                : null;
            sb.AppendLine($"- {ps.name} path={HierarchyPath(car.transform, ps.transform)} " +
                $"localPos={ps.transform.localPosition} localRot={ps.transform.localEulerAngles} " +
                $"emission={ps.emission.enabled} texture={(texture != null ? texture.name : "none")} " +
                $"startSpeed={main.startSpeed.constantMax} startLifetime={main.startLifetime.constantMax}");
        }

        DumpSim(sb, car);

        Log.Info(sb.ToString());
    }

    public static void DumpAllLiveries()
    {
        var liveries = DVObjectModel.current != null ? DVObjectModel.current.Liveries : null;
        if (liveries == null)
        {
            Log.Info("no object model for livery dump");
            return;
        }

        Log.Info($"=== all liveries ({liveries.Count}) ===");
        foreach (var livery in liveries)
        {
            if (livery == null) continue;

            var sb = new StringBuilder();
            DumpLivery(sb, livery);
            Log.Info(sb.ToString().TrimEnd());
        }
    }

    private static void DumpLivery(StringBuilder sb, TrainCarLivery livery)
    {
        var parent = livery.parentType;
        
        sb.AppendLine($"livery: id={livery.id} parent={(parent != null ? parent.id : "?")} " +
            $"v1={livery.v1} modded={!Enum.IsDefined(typeof(TrainCarType), livery.v1)} " +
            $"loco={(parent != null && CarTypes.IsLocomotive(livery))} hidden={livery.isHidden} " +
            $"license={(livery.requiredLicense != null ? livery.requiredLicense.id : "none")}");

        sb.AppendLine($"profile(normal): {DescribeResolution(livery.id)}");
    }

    private static string DescribeResolution(string liveryId)
    {
        var resolution = ProfileService.Resolve(liveryId, ResolutionMode.Normal);
        var winner = resolution.Winner;
        if (winner == null) return "none";

        var entry = winner.Value;
        var source = string.IsNullOrEmpty(entry.SourceId) ? entry.Tier.ToString() : $"{entry.Tier}/{entry.SourceId}";
        return resolution.Disabled ? $"disabled {source}" : source;
    }

    private static void DumpSim(StringBuilder sb, TrainCar car)
    {
        var sim = car.GetComponent<SimController>();
        if (sim == null || sim.SimulationFlow == null)
        {
            sb.AppendLine("sim: not bound (no SimController/SimulationFlow)");
            return;
        }

        var result = DieselEngineBinder.TryBind(sim.SimulationFlow);
        if (!result.IsSuccess)
        {
            sb.AppendLine($"sim: {result.Error}");
            return;
        }

        sb.AppendLine($"ports: {result.Value.Describe()}");
    }

    private static string HierarchyPath(Transform root, Transform transform)
    {
        var names = new List<string>();
        for (var current = transform; current != null && current != root; current = current.parent)
        {
            names.Add(current.name);
        }
        names.Reverse();
        return string.Join("/", names);
    }
}
