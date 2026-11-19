using CheckYourEligibility.Core.Domain.Validation;

namespace CheckYourEligibility.Core.Boundary.Responses
{
	public class TemporaryCodeUploadCreateResponse
	{
		public int NumberOfCodes { get; set; }
		public List<ImportTempCodeValidationError> Errors { get; set; } = new List<ImportTempCodeValidationError>();
	}
}
