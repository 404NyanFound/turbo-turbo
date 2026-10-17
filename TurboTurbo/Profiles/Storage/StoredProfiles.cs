using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace TurboTurbo.Profiles.Storage;

/// <summary>
/// Persisted user profiles, kept as raw <c>LocoProfile</c> elements so older schema versions
/// survive deserialization intact and can be migrated, and profiles that fail to load are written
/// back unchanged.
/// </summary>
public sealed class StoredProfiles : IXmlSerializable
{
    public List<XElement> Items { get; } = new();

    /// <summary>Replaces the stored element with the same livery, or adds it.</summary>
    internal void Put(XElement element)
    {
        var index = Items.FindIndex(e => LocoProfileCodec.LiveryId(e) == LocoProfileCodec.LiveryId(element));
        if (index >= 0) Items[index] = element;
        else Items.Add(element);
    }

    internal void Remove(string liveryId) => Items.RemoveAll(e => LocoProfileCodec.LiveryId(e) == liveryId);

    XmlSchema IXmlSerializable.GetSchema() => null;

    void IXmlSerializable.ReadXml(XmlReader reader)
    {
        var empty = reader.IsEmptyElement;
        reader.ReadStartElement();
        if (empty) return;

        while (reader.MoveToContent() == XmlNodeType.Element)
        {
            Items.Add((XElement)XNode.ReadFrom(reader));
        }
        reader.ReadEndElement();
    }

    void IXmlSerializable.WriteXml(XmlWriter writer)
    {
        foreach (var item in Items)
        {
            item.WriteTo(writer);
        }
    }
}
