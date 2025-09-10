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
        /// <param name="groupId">The group ID of the bulk check to delete</param>
        /// <param name="allowedLocalAuthorityIds">List of allowed local authority IDs for the user (0 means admin access to all)</param>
        /// <returns></returns>
        Task<CheckEligibilityBulkDeleteResponse> Execute(string groupId, IList<int> allowedLocalAuthorityIds);
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

        public async Task<CheckEligibilityBulkDeleteResponse> Execute(string groupId, IList<int> allowedLocalAuthorityIds)
        {
            if (string.IsNullOrEmpty(groupId)) 
                throw new ValidationException(null, "Invalid Request, group ID is required.");

            // First, get the bulk check to validate ownership
            var bulkCheck = await _checkGateway.GetBulkCheck(groupId);
            
            if (bulkCheck == null)
            {
                throw new NotFoundException($"Bulk check with ID {groupId} not found.");
            }

            // Check if user has permission to delete this bulk check
            if (!allowedLocalAuthorityIds.Contains(0)) // Not an admin
            {
                if (!bulkCheck.LocalAuthorityId.HasValue || 
                    !allowedLocalAuthorityIds.Contains(bulkCheck.LocalAuthorityId.Value))
                {
                    throw new ValidationException(null, $"Access denied. You can only delete bulk checks for your assigned local authority.");
                }
            }

            _logger.LogInformation($"Deleting EligibilityChecks for GroupId: {groupId?.Replace(Environment.NewLine, "")}");
            return await _checkGateway.DeleteByGroup(groupId);
        }
    }
}
