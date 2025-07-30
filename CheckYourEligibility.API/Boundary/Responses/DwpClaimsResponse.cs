namespace CheckYourEligibility.API.Boundary.Responses;

public class DwpClaimsResponse
{
    public Jsonapi jsonapi { get; set; }
    public List<Datum> data { get; set; }
    public Links links { get; set; }
}

public class AssessmentAttributes
{
    public int takeHomePay { get; set; }
}

public class Attributes
{
    public string benefitType { get; set; }
    public List<Award> awards { get; set; }
    public string guid { get; set; }
    public string startDate { get; set; }
    public string endDate { get; set; }
    public string decisionDate { get; set; }
    public string status { get; set; }
    public List<PaymentStatus> paymentStatus { get; set; }
    public WarningDetails warningDetails { get; set; }
    public List<Child> children { get; set; }
}

public class Award
{
    public int amount { get; set; }
    public string endDate { get; set; }
    public string endReason { get; set; }
    public string startDate { get; set; }
    public string status { get; set; }
    public List<AwardComponent> awardComponents { get; set; }
    public AssessmentAttributes assessmentAttributes { get; set; }
}

public class AwardComponent
{
    public int componentAwardAmount { get; set; }
    public string subType { get; set; }
    public string type { get; set; }
    public string componentRate { get; set; }
    public string payabilityStatus { get; set; }
}

public class Child
{
    public string effectiveStartDate { get; set; }
    public string fullName { get; set; }
    public string dateOfBirth { get; set; }
}

public class Datum
{
    public Attributes attributes { get; set; }
    public string id { get; set; }
    public string type { get; set; }
}

public class Jsonapi
{
    public string version { get; set; }
}

public class Links
{
    public string self { get; set; }
}

public class PaymentStatus
{
    public string paymentSuspensionReason { get; set; }
    public string type { get; set; }
    public string startDate { get; set; }
}

public class Warning
{
    public string id { get; set; }
    public string detail { get; set; }
}

public class WarningDetails
{
    public List<Warning> warnings { get; set; }
}