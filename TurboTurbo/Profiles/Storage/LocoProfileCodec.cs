using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

using JetBrains.Annotations;

using TurboTurbo.Profiles.Storage.Current;

namespace TurboTurbo.Profiles.Storage;

/// <summary>Converts between <c>LocoProfile</c> elements and the current persistence schema.</summary>
internal static class LocoProfileCodec
{
    public const string ElementName = "LocoProfile";

    private static readonly XmlSerializer Serializer = new(typeof(LocoProfileXml));
    private static readonly XmlSerializerNamespaces NoNamespaces = new([XmlQualifiedName.Empty]);

    public static XElement ToElement(LocoProfile profile) => ToElement(ProfileMapper.ToXml(profile));

    public static XElement ToElement(LocoProfileXml xml)
    {
        var document = new XDocument();
        using (var writer = document.CreateWriter())
        {
            Serializer.Serialize(writer, xml, NoNamespaces);
        }

        var element = document.Root;
        element.Remove();
        return element;
    }

    /// <summary>Deserializes an element that is already in the current schema.</summary>
    public static LocoProfileXml FromElement(XElement element)
    {
        using var reader = element.CreateReader();
        return (LocoProfileXml)Serializer.Deserialize(reader);
    }

    [CanBeNull]
    public static string LiveryId(XElement element) => (string)element.Element("LiveryId");
}
