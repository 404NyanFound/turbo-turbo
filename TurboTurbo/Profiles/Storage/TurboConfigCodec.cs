using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TurboTurbo.Profiles.Storage;

/// <summary>
/// Source of truth for a mod's <c>TurboConfig.xml</c>
/// </summary>
internal static class TurboConfigCodec
{
    public const string FileName = "TurboConfig.xml";

    private const string RootName = "TurboConfig";
    private const string ProfilesName = "LocoProfiles";

    public static string PathIn(string modPath) => Path.Combine(modPath, FileName);

    public static XDocument CreateEmpty() => new(new XElement(RootName, new XElement(ProfilesName)));

    /// <summary>
    /// Reads <paramref name="file"/>, failing when it is unreadable or not a TurboConfig document. A file
    /// without a root element, such as an empty one, reads as an empty TurboConfig.
    /// </summary>
    public static Result<XDocument> Load(string file)
    {
        XDocument document;
        try
        {
            if (!HasRootElement(file)) return CreateEmpty();
            document = XDocument.Load(file);
        }
        catch (Exception e)
        {
            return new Error($"could not read {file}: {e.Message}");
        }

        if (document.Root.Name != RootName) return new Error($"{file} is not a {FileName}");
        return document;
    }

    /// <summary>Writes <paramref name="document"/> to <paramref name="file"/>, returning null if there was no error.</summary>
    public static Error? Save(XDocument document, string file)
    {
        try
        {
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using var writer = XmlWriter.Create(file, settings);
            document.Save(writer);
        }
        catch (Exception e)
        {
            return new Error($"could not write {file}: {e.Message}");
        }

        return null;
    }

    /// <summary>The profile elements of a document from <see cref="Load"/> or <see cref="CreateEmpty"/>.</summary>
    public static List<XElement> Profiles(XDocument document) =>
        document.Root.Element(ProfilesName)?.Elements(LocoProfileCodec.ElementName).ToList() ?? new List<XElement>();

    /// <summary>
    /// The profile container of a document from <see cref="Load"/> or <see cref="CreateEmpty"/>, added when
    /// missing.
    /// </summary>
    public static XElement GetOrCreateProfiles(XDocument document)
    {
        var profiles = document.Root.Element(ProfilesName);
        if (profiles == null)
        {
            profiles = new XElement(ProfilesName);
            document.Root.Add(profiles);
        }

        return profiles;
    }

    // XDocument.Load throws when there is no root element, and its message is the only way to tell
    // that apart from other malformed XML, so check up front instead
    private static bool HasRootElement(string file)
    {
        using var reader = XmlReader.Create(file, new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment });
        return reader.MoveToContent() != XmlNodeType.None;
    }
}
