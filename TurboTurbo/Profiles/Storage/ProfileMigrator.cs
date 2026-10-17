using System;
using System.Linq;
using System.Xml.Linq;

namespace TurboTurbo.Profiles.Storage;

/// <summary>
/// Upgrades a stored <c>LocoProfile</c> element to the current persistence schema before it is
/// deserialized.
/// </summary>
internal static class ProfileMigrator
{
    private static readonly Action<XElement>[] Steps =
    [
        V1ToV2,
    ];

    /// <summary>
    /// Returns a new <see cref="XElement"/> instance in which all profile settings have been
    /// migrated to the latest version.
    /// </summary>
    public static XElement Migrate(XElement profile)
    {
        var version = (int?)profile.Element("Version") ?? 1;
        if (version is < 1 or >= ProfileSchema.CurrentVersion) return profile;

        var migrated = new XElement(profile);
        for (; version < ProfileSchema.CurrentVersion; version++)
        {
            Steps[version - 1](migrated);
        }

        var versionElement = migrated.Element("Version");
        if (versionElement != null) versionElement.Value = version.ToString();
        else migrated.AddFirst(new XElement("Version", version));
        return migrated;
    }

    private static void V1ToV2(XElement profile)
    {
        // boost is derived from exhaust enthalpy now
        Drop(profile, "TurboCharger", "RpmBoostExponent");

        // the stack model replaced the wet stack accumulator, and WetStackMaxAlpha changed meaning with it
        Drop(profile, "Smoke",
            "WetStackFillHeat", "WetStackReleaseHeat", "WetStackFillRate", "WetStackReleaseRate",
            "WetStackMistStrength", "WetStackMaxAlpha");

        // lifetime and emission rates became global settings, and plume growth no longer scales with lifetime
        Drop(profile, "SmokeEmitter", "lifetime", "idleEmissionRate", "fullEmissionRate", "sizeOverLifetimeEnd");
        Rename(profile, "SmokeEmitter", "turbulenceStrength", "turbulenceStrengthMax");
        Rename(profile, "SmokeEmitter", "sizeOverLifetimeExponent", "growthExponent");

        // exhaust speed is derived from mass flow now, so fixed speeds have no equivalent
        Drop(profile, "Velocity", "Idle", "FullLoad");
    }

    private static void Drop(XElement profile, string block, params string[] names)
    {
        profile.Element(block)?
            .Elements()
            .Where(e => names.Contains(e.Name.LocalName))
            .Remove();
    }

    private static void Rename(XElement profile, string block, string from, string to)
    {
        var element = profile.Element(block)?.Element(from);
        element?.Name = to;
    }
}
