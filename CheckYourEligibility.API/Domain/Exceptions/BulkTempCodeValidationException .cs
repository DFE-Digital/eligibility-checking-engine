using CheckYourEligibility.API.Domain.Validation;

namespace CheckYourEligibility.API.Domain.Exceptions
{
	public class BulkTempCodeValidationException : Exception
	{
		public List<ImportTempCodeValidationError> Errors { get; }
		public BulkTempCodeValidationException(List<ImportTempCodeValidationError> errors)
			: base("Validation failed.")
		{
			Errors = errors;
		}
	}
}