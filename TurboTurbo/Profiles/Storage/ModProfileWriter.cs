using System.IO;
using System.Linq;
using System.Xml.Linq;

using JetBrains.Annotations;

namespace TurboTurbo.Profiles.Storage;

/// <summary>
/// Writes or removes a <c>LocoProfile</c> in a mod's <c>TurboConfig.xml</c>.
/// </summary>
internal static class ModProfileWriter
{
    /// <summary>
    /// Writes <paramref name="profile"/> into <paramref name="modPath"/>, returning null if there was no error.
    /// </summary>
    internal static Error? Write(LocoProfile profile, string modPath)
    {
        var element = LocoProfileCodec.ToElement(profile);

        var file = TurboConfigCodec.PathIn(modPath);
        var loadResult = File.Exists(file) ? TurboConfigCodec.Load(file) : TurboConfigCodec.CreateEmpty();
        if (!loadResult.IsSuccess)
        {
            return loadResult.Error;
        }

        var profiles = TurboConfigCodec.GetOrCreateProfiles(loadResult.Value);
        var existing = Find(profiles, profile.LiveryId);
        if (existing != null)
        {
            existing.ReplaceWith(element);
        }
        else
        {
            profiles.Add(element);
        }

        return TurboConfigCodec.Save(loadResult.Value, file);
    }

    /// <summary>Serializes a single profile as a standalone <c>LocoProfile</c> fragment.</summary>
    internal static string SerializeFragment(LocoProfile profile) => LocoProfileCodec.ToElement(profile).ToString();

    /// <summary>
    /// Removes the profile for <paramref name="liveryId"/> from <paramref name="modPath"/>,
    /// returning null if there was no error.
    /// </summary>
    internal static Error? Delete(string liveryId, string modPath)
    {
        var file = TurboConfigCodec.PathIn(modPath);
        if (!File.Exists(file)) return new Error($"no {TurboConfigCodec.FileName} in the target mod");

        var result = TurboConfigCodec.Load(file);
        if (!result.IsSuccess) return result.Error;

        var document = result.Value;
        var existing = Find(TurboConfigCodec.GetOrCreateProfiles(document), liveryId);
        if (existing == null) return null;

        existing.Remove();
        return TurboConfigCodec.Save(document, file);
    }

    [CanBeNull]
    private static XElement Find(XElement profiles, string liveryId) =>
        profiles.Elements(LocoProfileCodec.ElementName).FirstOrDefault(e => LocoProfileCodec.LiveryId(e) == liveryId);
}
