using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

using TurboTurbo.Profiles.Storage.Current;

namespace TurboTurbo.Profiles.Storage;

internal static class ProfileLoader
{
    internal record struct ModSource(string Id, string ModName, bool Enabled, string Path);

    internal record struct ModProfile(LocoProfile Profile, string SourceId, string ModName);

    private static readonly Logger Log = TurboTurbo.Log.ForContext("profiles");

    internal static Dictionary<string, ModProfile> LoadModProfiles(IEnumerable<ModSource> mods, string ownId)
    {
        var loaded = new Dictionary<string, ModProfile>();
        foreach (var mod in mods)
        {
            var profiles = LoadConfig(mod, ownId);
            if (profiles == null) continue;
            MergeInto(loaded, mod, profiles);
        }

        Log.Info($"loaded {loaded.Count} loco profiles from mods");
        return loaded;
    }

    /// <summary>Loads the profiles of a single mod, for hot reload after authoring.</summary>
    internal static Dictionary<string, ModProfile> LoadModProfile(ModSource mod, string ownId)
    {
        var loaded = new Dictionary<string, ModProfile>();
        var profiles = LoadConfig(mod, ownId);
        if (profiles != null) MergeInto(loaded, mod, profiles);
        return loaded;
    }

    internal static Dictionary<string, LocoProfile> LoadUserProfiles(IEnumerable<XElement> stored)
    {
        var loaded = new Dictionary<string, LocoProfile>();
        if (stored == null) return loaded;
        foreach (var element in stored)
        {
            if (element == null) continue;

            var error = Import(element, out var profile);
            if (error != null)
            {
                Log.Warn($"skipping loco profile '{LocoProfileCodec.LiveryId(element)}': {error}");
                continue;
            }

            loaded[profile.LiveryId] = profile;
            Log.Info($"added loco profile for '{profile.LiveryId}' from user settings");
        }
        Log.Info($"loaded {loaded.Count} loco profiles from user settings");
        return loaded;
    }

    private static List<XElement> LoadConfig(ModSource mod, string ownId)
    {
        if (mod.Id == ownId) return null;
        if (!mod.Enabled) return null;
        var file = TurboConfigCodec.PathIn(mod.Path);
        if (!File.Exists(file)) return null;

        var document = TurboConfigCodec.Load(file);
        if (document.Error is { } error)
        {
            Log.Warn($"skipping {TurboConfigCodec.FileName} in '{mod.ModName}': {error}");
            return null;
        }
        return TurboConfigCodec.Profiles(document.Value);
    }

    private static void MergeInto(Dictionary<string, ModProfile> merged, ModSource source, List<XElement> profiles)
    {
        foreach (var element in profiles)
        {
            var error = Import(element, out var profile);
            if (error != null)
            {
                Log.Warn($"skipping loco profile '{LocoProfileCodec.LiveryId(element)}' from '{source.ModName}': {error}");
                continue;
            }

            if (merged.TryGetValue(profile.LiveryId, out var existing))
            {
                Log.Warn($"loco profile for '{profile.LiveryId}' from '{source.ModName}' overrides profile defined by " +
                         $"'{existing.ModName}' (change load order if this is not correct)");
            }
            else
            {
                Log.Info($"added loco profile for '{profile.LiveryId}' from '{source.ModName}'");
            }

            merged[profile.LiveryId] = new ModProfile(profile, source.Id, source.ModName);
        }
    }

    /// <summary>
    /// Migrates a stored profile to the current schema and maps it to a normalized runtime profile.
    /// Returns an error, or null when the profile is usable.
    /// </summary>
    private static Error? Import(XElement element, out LocoProfile profile)
    {
        profile = null;
        LocoProfileXml xml;
        try
        {
            xml = LocoProfileCodec.FromElement(ProfileMigrator.Migrate(element));
        }
        catch (Exception e)
        {
            return new Error($"malformed profile: {e.Message}");
        }
        if (xml.Version != ProfileSchema.CurrentVersion) return new Error($"unknown version {xml.Version}");

        profile = ProfileMapper.ToRuntime(xml);
        return profile.Normalize();
    }
}
