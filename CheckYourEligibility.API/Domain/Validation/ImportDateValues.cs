namespace CheckYourEligibility.API.Domain.Validation
{
	/// <summary>
	/// Provides constants for import date values to differentiate between empty and invalid dates.
	/// </summary>
	public static class ImportDateValues
	{
		public static readonly DateTime EmptyDate = DateTime.MinValue;
		public static readonly DateTime InvalidDate = DateTime.MinValue.AddDays(1);
	}
}
