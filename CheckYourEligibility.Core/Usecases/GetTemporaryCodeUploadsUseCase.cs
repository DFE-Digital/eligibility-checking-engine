using CheckYourEligibility.Core.Boundary.Responses;
using CheckYourEligibility.Core.Gateways.Interfaces;

namespace CheckYourEligibility.Core.UseCases
{
	public interface IGetTemporaryCodeUploadsUseCase
	{
		Task<TemporaryCodeUploadSearchResponse> Execute(TemporaryCodeUploadsSearchRequest request);
	}

	public class GetTemporaryCodeUploadsUseCase : IGetTemporaryCodeUploadsUseCase
	{
		private readonly ITemporaryCodeUpload _gateway;

		public GetTemporaryCodeUploadsUseCase(ITemporaryCodeUpload gateway)
		{
			_gateway = gateway;
		}

		public async Task<TemporaryCodeUploadSearchResponse> Execute(TemporaryCodeUploadsSearchRequest request)
		{
			var response = await _gateway.GetTemporaryCodeUploads(request);

			return new TemporaryCodeUploadSearchResponse
			{
				PageNumber = response.PageNumber,
				PageSize = response.PageSize,
				TotalNumberOfRecords = response.TotalNumberOfRecords,
				Data = response.Data
			};
		}
	}
}
