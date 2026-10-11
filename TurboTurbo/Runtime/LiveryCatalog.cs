using System;
using System.Collections.Generic;
using System.Linq;

using DV;
using DV.Localization;
using DV.ThingTypes;

namespace TurboTurbo.Runtime;

/// <summary>
/// Enumerates the liveries the game knows about, including ones added by other mods.
/// These can be targeted when creating or editing a profile.
/// </summary>
internal static class LiveryCatalog
{
    // these locos have no diesel exhaust to simulate, so they are excluded from the catalog.
    // this won't exclude modded vehicles, but these are harder to check when we don't have a TrainCar object yet.
    private static readonly HashSet<string> ExcludedLiveries = new(StringComparer.Ordinal)
    {
        "HandCar",
        "LocoMicroshunter",
        "LocoS060",
        "LocoS282A",
    };
    
    internal record struct LiveryInfo(string Id, string TypeId, string DisplayName, string LiveryName, bool IsModded, bool IsLoco, bool IsHidden)
    {
        public static LiveryInfo FromLivery(TrainCarLivery livery)
        {
            return new LiveryInfo(
                livery.id,
                livery.parentType != null ? livery.parentType.id : "",
                ResolveName(livery),
                livery.name,
                !Enum.IsDefined(typeof(TrainCarType), livery.v1),
                livery.parentType != null && CarTypes.IsLocomotive(livery),
                livery.isHidden);
        }
    }

    internal static List<LiveryInfo> GetLiveries()
    {
        var model = DVObjectModel.current ? DVObjectModel.current : Globals.G.Types;
        if (model == null || model.Liveries == null) return [];

        return model.Liveries
            .Where(livery => livery != null)
            .Select(LiveryInfo.FromLivery)
            .Where(livery => livery is { IsLoco: true, IsHidden: false })
            .Where(livery => !ExcludedLiveries.Contains(livery.Id))
            .OrderBy(livery => livery.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static string ResolveName(TrainCarLivery livery)
    {
        if (!string.IsNullOrEmpty(livery.localizationKey) && LocalizationAPI.HasTranslation(livery.localizationKey))
            return LocalizationAPI.L(livery.localizationKey);

        return livery.parentType != null ? livery.parentType.id : livery.id;
    }
}
