using CheckYourEligibility.Core.Domain.Validation;

namespace CheckYourEligibility.Core.Domain.Exceptions
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