using CheckYourEligibility.API.Domain.Validation;

namespace CheckYourEligibility.API.Boundary.Responses
{
	public class TemporaryCodeUploadCreateResponse
	{
		public int NumberOfCodes { get; set; }
		public List<ImportTempCodeValidationError> Errors { get; set; } = new List<ImportTempCodeValidationError>();
	}
}
