#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


using System;
using System.Xml.Serialization;
using Microsoft.CST.OpenSource.Contracts;

namespace Microsoft.CST.OpenSource.Model.Metadata
{
    /// <summary>
    /// A class to represent Package Metadata for an XML-based package version.
    /// </summary>
    [XmlRoot(ElementName = "entry", Namespace = "http://www.w3.org/2005/Atom")]
    public class NuGetV2PackageVersionMetadata : IManagerPackageVersionMetadata
    {
        [XmlElement(ElementName = "id", Namespace = "http://www.w3.org/2005/Atom")]
        public string Id { get; set; }

        [XmlElement(ElementName = "title", Namespace = "http://www.w3.org/2005/Atom")]
        public string Title { get; set; }

        [XmlElement(ElementName = "updated", Namespace = "http://www.w3.org/2005/Atom")]
        public DateTime Updated { get; set; }

        [XmlElement(ElementName = "author", Namespace = "http://www.w3.org/2005/Atom")]
        public Author Author { get; set; }

        [XmlElement(ElementName = "content", Namespace = "http://www.w3.org/2005/Atom")]
        public Content Content { get; set; }

        [XmlElement(ElementName = "properties", Namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata")]
        public Properties Properties { get; set; }

        [XmlIgnore]
        public string Name => Properties?.Id;

        [XmlIgnore]
        public string Version => Properties?.Version;
    }

    public class Author
    {
        [XmlElement(ElementName = "name", Namespace = "http://www.w3.org/2005/Atom")]
        public string Name { get; set; }
    }

    public class Content
    {
        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "src")]
        public string Source { get; set; }
    }

    [XmlType(Namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices")]
    public class Properties
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Authors { get; set; }

        public string Description { get; set; }

        public int DownloadCount { get; set; }

        public string GalleryDetailsUrl { get; set; }

        public string ProjectUrl { get; set; }

        public DateTime Published { get; set; }

        public string Tags { get; set; }

        public string Owners { get; set; }
    }
}