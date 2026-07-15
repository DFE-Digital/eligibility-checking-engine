using CheckYourEligibility.API.Boundary.Requests.DWP;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Boundary.Responses.DWP;
using CheckYourEligibility.API.Domain;
using CheckYourEligibility.API.Domain.Constants;
using CheckYourEligibility.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CheckYourEligibility.API.Adapters;

public interface IDwpAdapter
{
    Task<CAPIClaimResponseBase> GetCitizenClaims(string guid, string effectiveFromDate, string effectiveToDate,
        CheckEligibilityType type, string correaltionId, EligibilityPolicy eligibilityPolicy);

    Task<CAPICitizenResponse> GetCitizen(CitizenMatchRequest requestBody, CheckEligibilityType type, string correlationId);
}

[ExcludeFromCodeCoverage]
public class DwpAdapter : IDwpAdapter
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

    private readonly double _DWP_UniversalCreditThreshhold_2;
    private readonly double _DWP_UniversalCreditThreshhold_3;
    private readonly HttpClient _httpClient;

    private readonly ILogger _logger;
    private readonly string _UseEcsforChecks;
    private bool _ran;
    private string _token;
    private DateTime _tokenExpiry;

    public DwpAdapter(ILoggerFactory logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger.CreateLogger("ServiceFsmCheckEligibility");
        _configuration = configuration;
        _UseEcsforChecks = _configuration["Dwp:UseEcsforChecks"];

        _DWP_ApiHost = _configuration["Dwp:ApiHost"];
        _DWP_ApiTokenUrl = _configuration["Dwp:ApiTokenUrl"];
        _DWP_ApiClientId = _configuration["Dwp:ApiClientId"];
        _DWP_ApiSecret = _configuration["Dwp:ApiSecret"];

        _httpClient = httpClient;

        _DWP_ApiInstigatingUserId = _configuration["Dwp:ApiInstigatingUserId"];
        _DWP_ApiPolicyId = _configuration["Dwp:ApiPolicyId"];
        _DWP_ApiContext = _configuration["Dwp:ApiContext"];
        _DWP_ApiAccessLevel = _configuration["Dwp:ApiAccessLevel"];

    }

    #region Citizen Api Rest

    public async Task<CAPIClaimResponseBase> GetCitizenClaims(string guid, string effectiveFromDate, string effectiveToDate,
        CheckEligibilityType type, string correlationId, EligibilityPolicy eligibilityPolicy)
    {
        var uri =
            $"{_DWP_ApiHost}/v2/citizens/{guid}/claims?benefitType=pensions_credit,universal_credit,employment_support_allowance_income_based,income_support,job_seekers_allowance_income_based";

        string reason = string.Empty;
        try
        {
            string token = await GetToken();

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
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Dwp claim after request");
            _logger.LogInformation("Dwp " + response.StatusCode.ToString());
            _logger.LogInformation("Dwp " + responseBody);


            var capiClaimResponse = new CAPIClaimResponseBase {
                ResponseCode = response.StatusCode,
                CAPIEndpoint = uri,
                RequestBody = string.Empty,
                ResponseBody = responseBody
            };
          
            // if claims found for citizen
            if (response.IsSuccessStatusCode)
            {               
                var claims = JsonConvert.DeserializeObject<DwpClaimsResponse>(responseBody);
                var (isEntitled, eligbilityTier) = CheckBenefitEntitlement(guid, claims, type, eligibilityPolicy);

                // if citizien is entitled
                if (isEntitled)
                {
                    capiClaimResponse.ResponseCode = HttpStatusCode.OK;
                    capiClaimResponse.EligibilityTier = eligbilityTier;

                    return capiClaimResponse;
                }

                // if citizen is not entitled
                // return status to notFound for claims
                // NotFound claim results get resolved to notEligible in the gateway
                capiClaimResponse.ResponseCode = HttpStatusCode.NotFound;
                capiClaimResponse.EligibilityTier = eligbilityTier;
              
                return capiClaimResponse;
  
            }

            // CAPI returns a non-successful code
            _logger.LogWarning("Get CAPI citizen claim failed. uri:-{Uri} Response:- {StatusCode}",
                _httpClient.BaseAddress + uri,
                response.StatusCode);

            long capiResponseCode = 0;
            var DwpErrorResponse = JsonConvert.DeserializeObject<DwpErrorResponse>(responseBody);
            if (DwpErrorResponse.Errors.Length > 0)
            {
                long.TryParse(DwpErrorResponse.Errors.FirstOrDefault().Code, out capiResponseCode);
            }

            return new CAPIClaimResponseBase
            {
                CAPIResponseCode = capiResponseCode,
                ResponseCode = response.StatusCode,
                CAPIEndpoint = uri,
                ResponseBody = responseBody
            };
        }
        catch (Exception ex)
        {
                   
            string errorMessage = $"ECE failed to POST to CAPI. uri:-{_httpClient.BaseAddress}{uri}";
            _logger.LogError(ex, errorMessage);

            return (new CAPIClaimResponseBase
            {
                CAPIEndpoint = uri,
                ResponseCode = HttpStatusCode.InternalServerError,
                ResponseBody = ex.Message              
            });
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
    //pass the policy for the type of the la here
    // if mat use default behaviour
    public (bool, EligibilityTier?) CheckBenefitEntitlement(string citizenId, DwpClaimsResponse claims, CheckEligibilityType type, EligibilityPolicy eligibilityPolicy)
    {
        //If they have pensions credit and there is no end date
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.pensions_credit))
            return GetEligibleResponseBasedOnEligibilityPolicy(eligibilityPolicy.EligibilityCriteria);

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
                (bool,EligibilityTier?) result = (false, null);

                if (CheckUniversalCreditBenefitType(citizenId, liveAwards, eligibilityPolicy.UniversalCreditThreshold))

                   result =  GetEligibleResponseBasedOnEligibilityPolicy(eligibilityPolicy.EligibilityCriteria);

                else if (eligibilityPolicy.EligibilityCriteria == EligibilityCriteria.expanded)
                    result  =  (true, EligibilityTier.expanded);

                // If not eligible and not expanded, return not eligible/null tier (standard)
                return result;
            }

        }

        // No pensions credit + (no UC or no live award within last 3 months)
        // Then check if any of the other claims are live and have no end date
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.job_seekers_allowance_income_based))
            return GetEligibleResponseBasedOnEligibilityPolicy(eligibilityPolicy.EligibilityCriteria);
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.employment_support_allowance_income_based))
            return GetEligibleResponseBasedOnEligibilityPolicy(eligibilityPolicy.EligibilityCriteria);
        if (CheckStandardBenefitType(citizenId, claims, DwpBenefitType.income_support))
            return GetEligibleResponseBasedOnEligibilityPolicy(eligibilityPolicy.EligibilityCriteria);

        return (false, null);
    }
    /// <summary>
    /// Maps tier according to policy criteria only,
    ///  if 'expanded' then tier is targetted,
    /// if (default) 'standard' then tier is null / empty response
    /// </summary>
    /// <param name="criteria"></param>
    /// <returns></returns>
    private (bool, EligibilityTier?) GetEligibleResponseBasedOnEligibilityPolicy(EligibilityCriteria criteria)
    {

        switch (criteria)
        {
            case EligibilityCriteria.expanded:
                return (true, EligibilityTier.targeted);
            default: return (true, null);

        }

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

            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to match a citizen against DWP records using the provided citizen details.
    /// Calls the CAPI /v2/citizens/match endpoint to retrieve a citizen GUID for successful matches.
    /// </summary>
    /// <param name="requestBody">The citizen match request containing identification details</param>
    /// <param name="type">The type of eligibility check (e.g., Free School Meals, Early Year Pupil Premium)</param>
    /// <param name="correlationId">Unique identifier for tracking this request across systems</param>
    /// <returns>A CAPICitizenResponse containing the match result and citizen GUID if successful</returns>
    public async Task<CAPICitizenResponse> GetCitizen(CitizenMatchRequest requestBody, CheckEligibilityType type, string correlationId)
    {
        const string endpoint = "/v2/citizens/match";
        var uri = $"{_DWP_ApiHost}{endpoint}";

        // Initialize response object with endpoint information
        var citizenResponse = new CAPICitizenResponse { CAPIEndpoint = endpoint };

        try
        {

            // Retrieve bearer token for CAPI authentication
            string token = await GetToken();
            // Build HTTP request with serialized citizen details
            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"),
                RequestUri = new Uri(uri)
            };

            // Add required CAPI headers for tracking and context
            requestMessage.Headers.Add("instigating-user-id", _DWP_ApiInstigatingUserId);
            requestMessage.Headers.Add("policy-id", _DWP_ApiPolicyId);
            requestMessage.Headers.Add("correlation-id", correlationId);
            requestMessage.Headers.Add("context", GetContext(type));
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            _logger.LogInformation("Sending citizen match request to CAPI");
            var response = await _httpClient.SendAsync(requestMessage);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Received response from CAPI: {response.StatusCode}");

            // Store response metadata for audit purposes
            citizenResponse.ResponseCode = response.StatusCode;
            citizenResponse.RequestBody = await  requestMessage.Content.ReadAsStringAsync();
            citizenResponse.ResponseBody = responseBody;
            // Handle successful match - extract citizen GUID from response
            if (response.IsSuccessStatusCode)
            {
                var responseData = JsonConvert.DeserializeObject<DwpMatchResponse>(citizenResponse.ResponseBody);
                citizenResponse.Guid = responseData.Data.Id;
                return citizenResponse;
            }

            long capiResponseCode = 0;
            var DwpErrorResponse = JsonConvert.DeserializeObject<DwpErrorResponse>(responseBody);
            if (DwpErrorResponse.Errors.Length > 0)
            {
                long.TryParse(DwpErrorResponse.Errors.FirstOrDefault().Code, out capiResponseCode);
            }
            // Handle no match found
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.parentNotFound;
                return citizenResponse;
            }

            // Handle duplicate/conflicting matches
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                _logger.LogInformation("Multiple citizen matches found - conflict detected");
                citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
                return citizenResponse;
            }

            // Handle unexpected error responses
            string errorMessage = $"CAPI failed to match citizen. URI: {uri} | Response: {response.StatusCode}";
            _logger.LogWarning(errorMessage);
            citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
            citizenResponse.Reason = errorMessage;
            return citizenResponse;
        }
        catch (Exception ex)
        {
            string errorMessage = $"Exception occurred while calling CAPI citizen match endpoint. URI: {uri}";
            _logger.LogError(ex, errorMessage);
            citizenResponse.CheckEligibilityStatus = CheckEligibilityStatus.error;
            citizenResponse.ResponseCode = HttpStatusCode.InternalServerError;
            citizenResponse.ResponseBody = ex.Message;
            return citizenResponse;
        }
    }

    private async Task<string?> GetToken()
    {
        if (_tokenExpiry > DateTime.Now.AddSeconds(5))
        {
            return _token;
        }

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
        _token = responseData.access_token;
        _tokenExpiry = DateTime.Now.AddSeconds(responseData.expires_in);

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
    public int expires_in { get; set; }
}