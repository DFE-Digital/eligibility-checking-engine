using CheckYourEligibility.Core.Domain.Enums;
using Newtonsoft.Json;

namespace CheckYourEligibility.Core.Boundary.Responses;

public class EligibilityCheckReportResponse
{
  public EligibilityCheckReportResponseItem Data { get; set; }
}

public class EligibilityCheckReportResponseItem
{
   public string ReportID {get;set;}
   public string Status {get;set;}
}