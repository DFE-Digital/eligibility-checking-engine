using CheckYourEligibility.API.Domain.Enums;
using Newtonsoft.Json;

public class EligibilityCheckReportResponse
{
  public EligibilityCheckReportResponseItem Data { get; set; }
}

public class EligibilityCheckReportResponseItem
{
   public string ReportID {get;set;}
   public string Status {get;set;}
}