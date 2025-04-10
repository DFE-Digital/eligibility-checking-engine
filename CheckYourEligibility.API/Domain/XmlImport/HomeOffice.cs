using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace CheckYourEligibility.API.Gateways.XmlImport;

[ExcludeFromCodeCoverage(Justification = "xml class")]
[XmlRoot(ElementName = "Header")]
public class HMRCXml
{
    [XmlElement(ElementName = "OriginatingSystemID")]
    public string OriginatingSystemID { get; set; } = string.Empty;

    [XmlElement(ElementName = "ReceivingSystemID")]
    public string ReceivingSystemID { get; set; } = string.Empty;

    [XmlElement(ElementName = "CreationDate")]
    public int CreationDate { get; set; }

    [XmlElement(ElementName = "CreationTime")]
    public int CreationTime { get; set; }

    [XmlElement(ElementName = "RecordCount")]
    public int RecordCount { get; set; }

    [XmlElement(ElementName = "FileSequenceNumber")]
    public int FileSequenceNumber { get; set; }
}

[ExcludeFromCodeCoverage(Justification = "xml class")]
[XmlRoot(ElementName = "EligiblePerson")]
public class HMRCEligiblePerson
{
    [XmlElement(ElementName = "DataType")] public int DataType { get; set; }

    [XmlElement(ElementName = "NINO")] public string NINO { get; set; } = string.Empty;

    [XmlElement(ElementName = "DateOfBirth")]
    public int DateOfBirth { get; set; }

    [XmlElement(ElementName = "Surname")] public string Surname { get; set; } = string.Empty;
}

[ExcludeFromCodeCoverage(Justification = "xml class")]
[XmlRoot(ElementName = "EligiblePersons")]
public class HMRCEligiblePersons
{
    [XmlElement(ElementName = "EligiblePerson")]
    public List<HMRCEligiblePerson> EligiblePerson { get; set; } = new List<HMRCEligiblePerson>();
}

[ExcludeFromCodeCoverage(Justification = "xml class")]
[XmlRoot(ElementName = "FreeSchoolMealsHMRC")]
public class HMRCFreeSchoolMealsHMRC
{
    [XmlElement(ElementName = "Header")] public HMRCXml Header { get; set; } 

    [XmlElement(ElementName = "EligiblePersons")]
    public HMRCEligiblePersons EligiblePersons { get; set; }

    [XmlAttribute(AttributeName = "schemaLocation")]
    public string SchemaLocation { get; set; } = string.Empty;

    [XmlAttribute(AttributeName = "n1")] public string N1 { get; set; } = string.Empty;

    [XmlAttribute(AttributeName = "xmlns")]
    public string Xmlns { get; set; } = string.Empty;

    [XmlAttribute(AttributeName = "xsi")] public string Xsi { get; set; } = string.Empty;

    [XmlText] public string Text { get; set; } = string.Empty;
}