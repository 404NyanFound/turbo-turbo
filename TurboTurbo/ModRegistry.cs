using System.Collections.Generic;
using System.Linq;

using UnityModManagerNet;

namespace TurboTurbo;

/// <summary>
/// Lookup helpers over the UMM mod list.
/// </summary>
internal static class ModRegistry
{
    /// <summary>
    /// Finds a mod by its ID. Returns null if the mod is not present.
    /// </summary>
    public static UnityModManager.ModEntry Find(string modId)
    {
        if (string.IsNullOrEmpty(modId)) return null;
        return UnityModManager.modEntries?.FirstOrDefault(e => e.Info.Id == modId);
    }

    public static string DisplayName(string modId) =>
        Find(modId)?.Info.DisplayName ?? modId;

    public static List<UnityModManager.ModEntry> EligibleTargets(string excludeId)
    {
        if (UnityModManager.modEntries == null) return [];

        return UnityModManager.modEntries
            .Where(mod => mod.Enabled && mod.Info.Id != excludeId)
            .Where(mod => !string.IsNullOrEmpty(mod.Path))
            .ToList();
    }
}
