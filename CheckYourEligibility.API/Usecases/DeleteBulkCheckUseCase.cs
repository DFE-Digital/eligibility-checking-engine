using System;
using CheckYourEligibility.API.Boundary.Requests;
using CheckYourEligibility.API.Boundary.Responses;
using CheckYourEligibility.API.Domain.Exceptions;
using CheckYourEligibility.API.Gateways.Interfaces;
using CheckYourEligibility.API.UseCases;
using Microsoft.EntityFrameworkCore;

namespace CheckYourEligibility.API.Usecases
{

    /// <summary>
    ///     Interface for creating or updating a user.
    /// </summary>
    public interface IDeleteBulkCheckUseCase
    {
        /// <summary>
        ///     Execute the use case.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<CheckEligibilityBulkDeleteResponse> Execute(string groupId);
    }

    public class DeleteBulkCheckUseCase : IDeleteBulkCheckUseCase
    {
        private readonly ICheckEligibility _checkGateway;
        private readonly ILogger<DeleteBulkCheckUseCase> _logger;

        public DeleteBulkCheckUseCase(ICheckEligibility checkGateway, ILogger<DeleteBulkCheckUseCase> logger)
        {
            _checkGateway = checkGateway;
            _logger = logger;
        }

        public async Task<CheckEligibilityBulkDeleteResponse> Execute(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) throw new ValidationException(null, "Invalid Request, group ID is required.");

            _logger.LogInformation($"Deleting EligibilityChecks for GroupId: {groupId}");
            return await _checkGateway.DeleteByGroup(groupId);
        }
    }
}
