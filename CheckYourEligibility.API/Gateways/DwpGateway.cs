using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.DWP;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Properties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Gateways;

public interface IDwpGateway
{
    Task<(StatusCodeResult,string)> GetCitizenClaims(string guid, string effectiveFromDate, string effectiveToDate,
        CheckEligibilityType type, string correaltionId);

    Task<CAPICitizenResponse> GetCitizen(CitizenMatchRequest requestBody, CheckEligibilityType type, string correlationId);
}

[ExcludeFromCodeCoverage]
public class DwpGateway : BaseGateway, IDwpGateway
{
    public const string decision_entitled = "decision_entitled";
    public const string statusInPayment = "in_payment";
    public const string awardStatusLive = "live";
    private readonly IConfiguration _configuration;
    private readonly string _DWP_ApiHost;
    private readonly string _DWP_ApiTokenUrl;
    private readonly string _DWP_ApiClientId;
    private readonly string _DWP_ApiSecret;
    private readonly X509Certificate2 _DWP_ApiCertificate;
    private readonly string _DWP_ApiAccessLevel;
    private readonly string _DWP_ApiContext;


    private readonly string _DWP_ApiInstigatingUserId;
    private readonly string _DWP_ApiPolicyId;

    private readonly Dictionary<CheckEligibilityType, double> _DWP_ApiUniversalCreditThreshold =
        new Dictionary<CheckEligibilityType, double>();

    private readonly double _DWP_UniversalCreditThreshhold_2;
    private readonly double _DWP_UniversalCreditThreshhold_3;
    private readonly HttpClient _httpClient;

    private readonly ILogger _logger;
    private readonly string _UseEcsforChecks;
    private bool _ran;

    public DwpGateway(ILoggerFactory logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceFsmCheckEligibility");
        _configuration = configuration;
        _UseEcsforChecks = _configuration["Dwp:UseEcsforChecks"];

        _DWP_ApiHost = _configuration["Dwp:ApiHost"];
        _DWP_ApiTokenUrl = _configuration["Dwp:ApiTokenUrl"];
        _DWP_ApiClientId = _configuration["Dwp:ApiClientId"];
        _DWP_ApiSecret = _configuration["Dwp:ApiSecret"];

        _httpClient = httpClient;

        if (_UseEcsforChecks!="true")
        {
            var privateKeyBytes = Convert.FromBase64String(_configuration["Dwp:ApiCertificate"]);
            _DWP_ApiCertificate = new X509Certificate2(privateKeyBytes, (string)null,
                X509KeyStorageFlags.MachineKeySet);

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(_DWP_ApiCertificate);
            handler.ServerCertificateCustomValidationCallback = ByPassCertErrorsForTestPurposesDoNotDoThisInTheWild;

            _httpClient = new HttpClient(handler);
        }

        _DWP_ApiInstigatingUserId = _configuration["Dwp:ApiInstigatingUserId"];
        _DWP_ApiPolicyId = _configuration["Dwp:ApiPolicyId"];      
        _DWP_ApiContext = _configuration["Dwp:ApiContext"];
        _DWP_ApiAccessLevel = _configuration["Dwp:ApiAccessLevel"];

        _DWP_ApiUniversalCreditThreshold[CheckEligibilityType.FreeSchoolMeals] =
            Convert.ToDouble(_configuration["Dwp:ApiUniversalCreditThreshold:FreeSchoolMeals"]);
        _DWP_ApiUniversalCreditThreshold[CheckEligibilityType.EarlyYearPupilPremium] =
            Convert.ToDouble(_configuration["Dwp:ApiUniversalCreditThreshold:EarlyYearPupilPremium"]);
        _DWP_ApiUniversalCreditThreshold[CheckEligibilityType.TwoYearOffer] =
            Convert.ToDouble(_configuration["Dwp:ApiUniversalCreditThreshold:TwoYearOffer"]);
    }

    private static bool ByPassCertErrorsForTestPurposesDoNotDoThisInTheWild(
        HttpRequestMessage httpRequestMsg,
        X509Certificate2 certificate,
        X509Chain x509Chain,
        SslPolicyErrors policyErrors)
    {
        return true;
    }

    #region Citizen Api Rest

    public async Task<(StatusCodeResult, string)> GetCitizenClaims(string guid, string effectiveFromDate, string effectiveToDate,
        CheckEligibilityType type, string correlationId)
    {
        var uri =
            $"{_DWP_ApiHost}/v2/citizens/{guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based";
        string token = await GetToken();
        string reason = string.Empty;
        try
        {
            _logger.LogInformation("Dwp claim before token");

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
            requestMessage.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            requestMessage.Headers.Add("context", GetContext(type));
            requestMessage.Headers.Add("access-level", _DWP_ApiAccessLevel);
            requestMessage.Headers.Add("correlation-id", correlationId);
            requestMessage.Headers.Add("instigating-user-id", _DWP_ApiInstigatingUserId);

            _logger.LogInformation("Dwp claim before request");
            var response = await _httpClient.SendAsync(requestMessage);
            _logger.LogInformation("Dwp claim after request");
            _logger.LogInformation("Dwp " + response.StatusCode.ToString());
            _logger.LogInformation("Dwp " + response.Content.ReadAsStringAsync().Result);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var claims = JsonConvert.DeserializeObject<DwpClaimsResponse>(jsonString);
                if (CheckBenefitEntitlement(guid, claims, type)) {
                  // ECS_Conflict helper logic to better track conflicts
                    reason =
                        "CAPI confirms citizen has benefit of type -" +
                        "employment_support_allowance_income_based" +
                        "or income_support, " +
                        "or job_seekers_allowance_income_based, " +
                        "or pensions_credit " +
                        "or universal_credit ";
                    return (new OkResult(), reason);

                }
                // ECS_Conflict helper logic to better track conflicts
                reason = "CAPI returned status 200, but no benefits found after using business logic.";
                return (new NotFoundResult(),reason);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // ECS_Conflict helper logic to better track conflicts
                reason = "CAPI did not find any data for this citizen.";
                return (new NotFoundResult(), reason);
            }

            string errorMessage = $"Get CAPI citizen claim failed. uri:-{_httpClient.BaseAddress}{uri} Response:- {response.StatusCode}";
            _logger.LogInformation(errorMessage);
            return (new InternalServerErrorResult(), errorMessage);
        }
        catch (Exception ex)
        {
            string errorMessage = $"ECE failed to POST to CAPI. uri:-{_httpClient.BaseAddress}{uri}";
            _logger.LogError(ex, errorMessage);
            return (new InternalServerErrorResult(), errorMessage);
        }
    }

    private string GetContext(CheckEligibilityType type)
    {
        switch (type)
        {
            case CheckEligibilityType.FreeSchoolMeals:
                return "DFE-FSM";

            case CheckEligibilityType.EarlyYearPupilPremium:
                return "DFE-EYPP";

            case CheckEligibilityType.TwoYearOffer:
                return "DFE-2EY";
        }

        return null;
    }

    public bool CheckBenefitEntitlement(string citizenId, DwpClaimsResponse claims, CheckEligibilityType type)
    {
        //If they have pensions credit and there is no end date
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.pensions_credit))
            return true;

        var universalCredit = claims.data.FirstOrDefault(x =>
            x.attributes.benefitType == DwpBenefitType.universal_credit.ToString()
        );
        // Check if there is UC entitlement
        if (universalCredit != null)
        {
            // Check there is at least 1 live UC award in the last 3 months
            var filterDate = DateTime.Now.AddMonths(-4);
            var liveAwards = universalCredit.attributes.awards.Where(x => x.status == awardStatusLive && DateTime.Parse(x.endDate) > filterDate);
            if (liveAwards != null && liveAwards.Count() > 0)
            {
                // Determine eligibility based on take home pay thresholds
                return CheckUniversalCreditBenefitType(citizenId, liveAwards, _DWP_ApiUniversalCreditThreshold[type]);
            }
        }
        
        // No pensions credit + (no UC or no live award within last 3 months)
        // Then check if any of the other claims are live and have no end date
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.job_seekers_allowance_income_based))
            return true;
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.employment_support_allowance_income_based))
            return true;
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.income_support))
            return true;
        
        return false;
    }

    private bool CheckUniversalCreditBenefitType(string citizenId, IEnumerable<Award> liveAwards, double threshold)
    {
        var entitled = false;
        if (liveAwards != null && liveAwards.Count() > 0)
        {
            var takeHomePay = 0.00;

            takeHomePay = liveAwards.OrderByDescending(w => w.startDate).Take(1).Sum(x => x.assessmentAttributes.takeHomePay);
            if (takeHomePay <= threshold) entitled = true;

            if (liveAwards.Count() > 1)
            {
                takeHomePay = liveAwards.OrderByDescending(w => w.startDate).Take(2)
                    .Sum(x => x.assessmentAttributes.takeHomePay);
                if (takeHomePay <= threshold * 2) entitled = true;
            }

            if (liveAwards.Count() > 2)
            {
                takeHomePay = liveAwards.OrderByDescending(w => w.startDate).Take(3)
                    .Sum(x => x.assessmentAttributes.takeHomePay);
                if (takeHomePay <= threshold * 3) entitled = true;
            }

            if (entitled)
            {
                _logger.LogInformation($"Dwp {DwpBenefitType.universal_credit} found for CitizenId:-{citizenId}");
                TrackMetric($"Dwp {DwpBenefitType.universal_credit} entitled", 1);
                return true;
            }
        }

        return false;
    }

    private bool CheckStandardBenefitType(string citizenId, DwpClaimsResponse claims, DwpBenefitType benefitType)
    {
        var benefit = claims.data.FirstOrDefault(x => x.attributes.benefitType == benefitType.ToString()
                                                      && x.attributes.endDate.IsNullOrEmpty());
        if (benefit != null)
        {
            _logger.LogInformation($"Dwp {benefitType} found for CitizenId:-{citizenId}");
            TrackMetric($"Dwp {benefitType} entitled", 1);

            return true;
        }

        return false;
    }

    public async Task<CAPICitizenResponse> GetCitizen(CitizenMatchRequest requestBody, CheckEligibilityType type, string correlationId)
    {
        var uri = $"{_DWP_ApiHost}/v2/citizens/match";
        // ECS_Conflict helper logic to better track conflicts
        CAPICitizenResponse citizenResponse = new();
        citizenResponse.CAPIEndpoint = string.Empty;
        citizenResponse.Guid = string.Empty;
        citizenResponse.CAPIEndpoint = "/v2/citizens/match";

        _logger.LogInformation($"Dwp before citizen token");
        string token = await GetToken();
        _logger.LogInformation($"Dwp token " + token);

        try
        {
            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content =
                    new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"),
                RequestUri = new Uri(uri)
            };

            requestMessage.Headers.Add("instigating-user-id", _DWP_ApiInstigatingUserId);
            requestMessage.Headers.Add("policy-id", _DWP_ApiPolicyId);
            requestMessage.Headers.Add("correlation-id", correlationId);
            requestMessage.Headers.Add("context", GetContext(type));
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            _logger.LogInformation($"Dwp before citizen request");
            var response = await _httpClient.SendAsync(requestMessage);
            _logger.LogInformation($"Dwp after citizen request");
            _logger.LogInformation("Dwp " + response.StatusCode.ToString());
            _logger.LogInformation($"Dwp response " + response.Content.ReadAsStringAsync().Result);

            // ECS_Conflict helper logic to better track conflicts
            citizenResponse.CAPIResponseCode = response.StatusCode;
          

            if (response.IsSuccessStatusCode)
            {
                var responseData =
                    JsonConvert.DeserializeObject<DwpMatchResponse>(response.Content.ReadAsStringAsync().Result);
                citizenResponse.Guid = responseData.Data.Id;
                return citizenResponse;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {

                citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.parentNotFound;
                citizenResponse.Reason = "No citizen found";
                return citizenResponse;
            }
            else if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                _logger.LogInformation("DWP Duplicate matches found");
                TrackMetric("DWP Duplicate Matches Found", 1);
                citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
                citizenResponse.Reason = "Unprocessable Entity - Possible conflict";
                return citizenResponse;
            }
            else {
                string errorMessage = $"CAPI failed getting citizen. uri:-{_httpClient.BaseAddress}{uri} Response:- {response.StatusCode}";
                _logger.LogInformation(errorMessage);
                citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
                citizenResponse.Reason = errorMessage;
                return citizenResponse;
            }            
        }
        catch (Exception ex)
        {
            string errorMessage = $"ECE failed making a requet to GET citizen. uri:-{_httpClient.BaseAddress}{uri}";
            _logger.LogError(ex, errorMessage);
            citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
            citizenResponse.Reason = errorMessage;
            return citizenResponse;
        }
    }

    private async Task<string?> GetToken()
    {
        var uri = $"{_DWP_ApiTokenUrl}";

        var parameters = new Dictionary<string, string>();
        parameters.Add("client_id", _DWP_ApiClientId);
        parameters.Add("client_secret", _DWP_ApiSecret);
        parameters.Add("grant_type", "client_credentials");

        var formData = new FormUrlEncodedContent(parameters);

        var response = await _httpClient.PostAsync(uri, formData);
        Console.WriteLine(response.Content.ReadAsStringAsync().Result);

        var responseData =
            JsonConvert.DeserializeObject<JwtBearer>(response.Content.ReadAsStringAsync().Result);
        return responseData.access_token;
    }

    #endregion
}

[ExcludeFromCodeCoverage]
public class InternalServerErrorResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status500InternalServerError;

    /// <summary>
    ///     Creates a new <see cref="BadRequestResult" /> instance.
    /// </summary>
    public InternalServerErrorResult()
        : base(DefaultStatusCode)
    {
    }
}

public class Jwt
{
    // Primary identifiers (OAuth2 standard names)
    public string? scope { get; set; }
    public string? grant_type { get; set; }

    public string client_id { get; set; }
    public string client_secret { get; set; }
}

public class JwtBearer
{
    // Primary identifiers (OAuth2 standard names)
    public string access_token { get; set; }
}
