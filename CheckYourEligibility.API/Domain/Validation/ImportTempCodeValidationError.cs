namespace CheckYourEligibility.API.Domain.Validation
{
	public class ImportTempCodeValidationError
	{
		public int RowNumber { get; set; }
		public string EligibilityCode { get; set; } = string.Empty;
		public List<string> ErrorMessages { get; set; } = new();
	}
}
