using CheckYourEligibility.Core.Domain;

namespace CheckYourEligibility.Core.Boundary.Responses
{
	public class TemporaryCodeUploadSearchResponse
	{
		public int PageNumber { get; set; }
		public int PageSize { get; set; }
		public int TotalNumberOfRecords { get; set; }
		public IEnumerable<TemporaryCodeUploadResponse> Data { get; set; }
	}
}
