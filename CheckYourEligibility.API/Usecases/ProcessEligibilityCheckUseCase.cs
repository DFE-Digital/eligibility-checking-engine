using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Enums;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways;
using CheckYourEligibility.API.Gateways.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace CheckYourEligibility.API.UseCases;

/// <summary>
///     Interface for processing eligibility checks
/// </summary>
public interface IProcessEligibilityCheckUseCase
{
    /// <summary>
    ///     Execute the use case
    /// </summary>
    /// <param name="guid">The ID of the eligibility check</param>
    /// <returns>Processed eligibility check status</returns>
    Task<CheckEligibilityStatusResponse> Execute(string guid);
}

public class ProcessEligibilityCheckUseCase : IProcessEligibilityCheckUseCase
{
    private readonly IAudit _auditGateway;
    private readonly ICheckingEngine _checkingEngineGateway;
    private readonly IDbContextFactory<EligibilityCheckContext> _dbContextFactory;

    private readonly ILogger<ProcessEligibilityCheckUseCase> _logger;

    public ProcessEligibilityCheckUseCase(
        ICheckingEngine checkingEngineGateway,
        IAudit auditGateway,
        ILogger<ProcessEligibilityCheckUseCase> logger,
       IDbContextFactory<EligibilityCheckContext> dbContextFactory)
    {
        _dbContextFactory   = dbContextFactory;
        _checkingEngineGateway = checkingEngineGateway;
        _auditGateway = auditGateway;
        _logger = logger;
    }

    public async Task<CheckEligibilityStatusResponse> Execute(string guid)
    {
        if (string.IsNullOrEmpty(guid)) throw new ValidationException(null, "Invalid Request, check ID is required.");

        try
        {
            CheckEligibilityStatus? response = null;
      
            using (var dbContext = _dbContextFactory.CreateDbContext())
            {
                // pass dbContext
                var auditItemTemplate = _auditGateway.AuditDataGet(AuditType.Check, string.Empty);
                response = await _checkingEngineGateway.ProcessCheckAsync(guid, auditItemTemplate);
                await _auditGateway.CreateAuditEntry(AuditType.Check, guid, dbContext);
            }

            if (response == null)
            {
                _logger.LogWarning(
                    $"Eligibility check with ID {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")} not found");
                throw new NotFoundException(guid);
            }

        

            _logger.LogInformation(
                $"Processed eligibility check with ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}, status: {response.Value}");

            var resultResponse = new CheckEligibilityStatusResponse
            {
                Data = new StatusValue
                {
                    Status = response.Value.ToString()
                }
            };
            // When status is Queued For Processing, i.e. not error
               if (response.Value == CheckEligibilityStatus.queuedForProcessing)
                throw new ApplicationException("Eligibility check still queued for processing.");

            return resultResponse;
        }
        catch (ProcessCheckException ex)
        {
            _logger.LogError(ex,
                $"Error processing eligibility check with ID: {guid.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")}");
            throw new ValidationException(null, "Failed to process eligibility check.");
        }
    }
}