namespace CheckYourEligibility.API.Boundary.Responses;

public class DwpClaimsResponse
{
    public Jsonapi jsonapi { get; set; } = null!;
    public List<Datum> data { get; set; } = new List<Datum>();
    public Links links { get; set; } = null!;
}

public class AssessmentAttributes
{
    public int takeHomePay { get; set; }
}

public class Attributes
{
    public string benefitType { get; set; } = string.Empty;
    public List<Award> awards { get; set; } = new List<Award>();
    public string guid { get; set; } = string.Empty;
    public string startDate { get; set; } = string.Empty;
    public string decisionDate { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public List<PaymentStatus> paymentStatus { get; set; } = new List<PaymentStatus>();
    public WarningDetails warningDetails { get; set; } = null!;
    public List<Child> children { get; set; } = new List<Child>();
}

public class Award
{
    public int amount { get; set; }
    public string endDate { get; set; } = string.Empty;
    public string endReason { get; set; } = string.Empty;
    public string startDate { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public List<AwardComponent> awardComponents { get; set; } = new List<AwardComponent>();
    public AssessmentAttributes assessmentAttributes { get; set; } = null!;
}

public class AwardComponent
{
    public int componentAwardAmount { get; set; }
    public string subType { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public string componentRate { get; set; } = string.Empty;
    public string payabilityStatus { get; set; } = string.Empty;
}

public class Child
{
    public string effectiveStartDate { get; set; } = string.Empty;
    public string fullName { get; set; } = string.Empty;
    public string dateOfBirth { get; set; } = string.Empty;
}

public class Datum
{
    public Attributes attributes { get; set; } = null!;
    public string id { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
}

public class Jsonapi
{
    public string version { get; set; } = string.Empty;
}

public class Links
{
    public string self { get; set; } = string.Empty;
}

public class PaymentStatus
{
    public string paymentSuspensionReason { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public string startDate { get; set; } = string.Empty;
}

public class Warning
{
    public string id { get; set; } = string.Empty;
    public string detail { get; set; } = string.Empty;
}

public class WarningDetails
{
    public List<Warning> warnings { get; set; } = new List<Warning>();
}