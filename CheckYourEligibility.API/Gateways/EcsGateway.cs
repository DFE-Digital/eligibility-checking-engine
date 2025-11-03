using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Properties;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;

namespace CheckYourEligibility.API.Gateways;
public interface IEcsGateway
{
    public string UseEcsforChecks { get; }
    public string UseEcsforChecksWF { get; }
    Task<SoapCheckResponse?> EcsCheck(CheckProcessData eligibilityCheck, CheckEligibilityType eligibilityType, string laId);
    Task<SoapCheckResponse?> EcsWFCheck(CheckProcessData eligibilityCheck, string laId);
}

[ExcludeFromCodeCoverage]
public class EcsGateway : BaseGateway, IEcsGateway
{
    private readonly IConfiguration _configuration;
    private readonly string EcsHost;
    private readonly string EcsLAId;
    private readonly string EcsPassword;
    private readonly string EcsServiceVersion;
    private readonly string EcsSystemId;

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _UseEcsforChecks;
    private readonly string _UseEcsforChecksWF;

    string IEcsGateway.UseEcsforChecks => _UseEcsforChecks;

    string IEcsGateway.UseEcsforChecksWF => _UseEcsforChecksWF;
    private bool _ran;

     public EcsGateway(ILoggerFactory logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceFsmCheckEligibility");
        _configuration = configuration;
        _UseEcsforChecks = _configuration["Dwp:UseEcsforChecks"];
        _UseEcsforChecksWF = _configuration["Dwp:UseEcsforChecksWF"];

        _httpClient = httpClient;

        EcsHost = _configuration["Dwp:EcsHost"];
        EcsServiceVersion = _configuration["Dwp:EcsServiceVersion"];
        EcsLAId = _configuration["Dwp:EcsLAId"];
        EcsSystemId = _configuration["Dwp:EcsSystemId"];
        EcsPassword = _configuration["Dwp:EcsPassword"];
    }

    private async Task<SoapCheckResponse?> executeEcsCheck(string request)
    {
        try
        {
            var uri = EcsHost;
            if (!uri.Contains("https"))
                uri = $"https://{EcsHost}/fsm.lawebservice/20170701/OnlineQueryGateway.svc";

            var content = new StringContent(request, Encoding.UTF8, "text/xml");
            var soapResponse = new SoapCheckResponse();
            try
            {
                if (!_ran)
                {
                    _httpClient.DefaultRequestHeaders.Add("SOAPAction",
                        "http://www.dcsf.gov.uk/20090308/OnlineQueryService/SubmitSingleQuery");
                    _ran = true;
                }

                var response = await _httpClient.PostAsync(uri, content);
                if (response.IsSuccessStatusCode)
                {
                    var doc = XDocument.Parse(response.Content.ReadAsStringAsync().Result);
                    var namespacePrefix = doc.Root.GetNamespaceOfPrefix("s");
                    var elements = doc.Descendants(namespacePrefix + "Body").First().Descendants().Elements();
                    var xElement = elements.First(x => x.Name.LocalName == "EligibilityStatus");
                    soapResponse.Status = xElement.Value;
                    xElement = elements.First(x => x.Name.LocalName == "ErrorCode");
                    soapResponse.ErrorCode = xElement.Value;
                    xElement = elements.FirstOrDefault(x => x.Name.LocalName == "Qualifier");
                    soapResponse.Qualifier = xElement.Value;
                    xElement = elements.FirstOrDefault(x => x.Name.LocalName == "ValidityStartDate");
                    soapResponse.ValidityStartDate = xElement.Value;
                    xElement = elements.FirstOrDefault(x => x.Name.LocalName == "ValidityEndDate");
                    soapResponse.ValidityEndDate = xElement.Value;
                    xElement = elements.FirstOrDefault(x => x.Name.LocalName == "GracePeriodEndDate");
                    soapResponse.GracePeriodEndDate = xElement.Value;
                    xElement = elements.FirstOrDefault(x => x.Name.LocalName == "ParentSurname");
                    soapResponse.ParentSurname = xElement.Value;
                    _logger.LogInformation($"ECS soap response: {JsonConvert.SerializeObject(soapResponse)}");
                    return soapResponse;
                }

                _logger.LogError(
                    $"ECS check failed. uri:-{_httpClient.BaseAddress}{uri} Response:- {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ECS check failed. uri:-{_httpClient.BaseAddress}{uri}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ECS check failed.");
        }

        return null;
    }

    public async Task<SoapCheckResponse?> EcsCheck(CheckProcessData eligibilityCheck, CheckEligibilityType eligibilityType, string LaId)
    {
        
        var soapMessage = Resources.EcsSoapFsm;
        soapMessage = soapMessage.Replace("{{SystemId}}", EcsSystemId);
        soapMessage = soapMessage.Replace("{{Password}}", EcsPassword);
        soapMessage = soapMessage.Replace("{{LAId}}", string.IsNullOrEmpty(LaId) ? EcsLAId: LaId);
        soapMessage = soapMessage.Replace("{{ServiceVersion}}", EcsServiceVersion);
        soapMessage = soapMessage.Replace("<ns:Surname>WEB</ns:Surname>",
            $"<ns:Surname>{eligibilityCheck.LastName}</ns:Surname>");
        soapMessage = soapMessage.Replace("<ns:DateOfBirth>1967-03-07</ns:DateOfBirth>",
            $"<ns:DateOfBirth>{eligibilityCheck.DateOfBirth}</ns:DateOfBirth>");
        soapMessage = soapMessage.Replace("<ns:NiNo>NN668767B</ns:NiNo>",
            $"<ns:NiNo>{eligibilityCheck.NationalInsuranceNumber}</ns:NiNo>");

        if(eligibilityType==CheckEligibilityType.TwoYearOffer)
        {
            soapMessage = soapMessage.Replace("<ns:EligibilityCheckType>FSM</ns:EligibilityCheckType>",
                $"<ns:EligibilityCheckType>EY</ns:EligibilityCheckType>");
        }

        if(eligibilityType==CheckEligibilityType.EarlyYearPupilPremium)
        {
            soapMessage = soapMessage.Replace("<ns:EligibilityCheckType>FSM</ns:EligibilityCheckType>",
                $"<ns:EligibilityCheckType>EYPP</ns:EligibilityCheckType>");
        }
        string logSoapMessage = soapMessage.Replace(EcsPassword, "RemovedFromLogs");
        _logger.LogInformation($"ECS soap message generated: {logSoapMessage}");
        return await executeEcsCheck(soapMessage);
    }

    public async Task<SoapCheckResponse?> EcsWFCheck(CheckProcessData eligibilityCheck, string laId)
    {

        var soapMessage = Resources.EcsSoapWF;
        soapMessage = soapMessage.Replace("{{SystemId}}", EcsSystemId);
        soapMessage = soapMessage.Replace("{{Password}}", EcsPassword);
        soapMessage = soapMessage.Replace("{{LAId}}", string.IsNullOrEmpty(laId) ? EcsLAId : laId);
        soapMessage = soapMessage.Replace("{{ServiceVersion}}", EcsServiceVersion);
        soapMessage = soapMessage.Replace("{{DateOfBirth}}", eligibilityCheck.DateOfBirth);
        soapMessage = soapMessage.Replace("{{NiNo}}", eligibilityCheck.NationalInsuranceNumber);
        soapMessage = soapMessage.Replace("{{EligibilityCode}}", eligibilityCheck.EligibilityCode);

        if (!eligibilityCheck.LastName.IsNullOrEmpty())
        {
            soapMessage = soapMessage.Replace("<ns:Surname/>",
                $"<ns:Surname>{eligibilityCheck.LastName}</ns:Surname>");
        }
        string logSoapMessage = soapMessage.Replace(EcsPassword, "RemovedFromLogs");
        _logger.LogInformation($"ECS soap message generated: {logSoapMessage}");
        return await executeEcsCheck(soapMessage);
    }
}