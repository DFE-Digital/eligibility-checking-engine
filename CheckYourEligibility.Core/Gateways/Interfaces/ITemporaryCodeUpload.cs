using CheckYourEligibility.Core.Boundary.Requests;
using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Gateways.Interfaces
{
	public interface ITemporaryCodeUpload
	{
		Task<TemporaryCodeUpload> CreateTemporaryCodeUpload(TemporaryCodeUploadCreateRequest request, string userID, int numberOfCodes, string firstCode, string lastCode);
		Task<TemporaryCodeUploadSearchResponse> GetTemporaryCodeUploads(TemporaryCodeUploadsSearchRequest request);
	}
}