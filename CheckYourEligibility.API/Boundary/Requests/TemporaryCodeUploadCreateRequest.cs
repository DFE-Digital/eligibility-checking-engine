namespace CheckYourEligibility.API.Boundary.Requests
{
	/// <summary>
	/// Request for importing Temporary Codes from a file
	/// </summary>
	public class TemporaryCodeUploadCreateRequest
	{
		/// <summary>
		/// The CSV or JSON file containing application data
		/// </summary>
		public required IFormFile File { get; set; }
		public string? Password { get; set; }
	}

	/// <summary>
	/// Data structure representing a single Temporary Code record
	/// </summary>
	public class TemporaryCodeBulkImportData
	{
		/// <summary>
		/// Presumably some sort of index or identifier for the record? int?
		/// </summary>
		public required int Number { get; set; }

		/// <summary>
		/// Eligibility Code
		/// </summary>
		public required string EligibilityCode { get; set; }

		/// <summary>
		/// Presumably a name of the person who assigned the code? string?
		/// </summary>
		public required string AssignedBy { get; set; }

		/// <summary>
		/// Validity Start Date.
		/// </summary>
		public required DateTime ValidityStartDate { get; set; }

		/// <summary>
		/// Validity End Date.
		/// </summary>
		public required DateTime ValidityEndDate { get; set; }

		/// <summary>
		/// The parent's national insurance number (NINO)
		/// </summary>
		public required string ParentNINO { get; set; }

		/// <summary>
		/// Parent's date of birth
		/// </summary>
		public required DateTime? ParentDOB { get; set; }

		/// <summary>
		/// Parent's first name
		/// </summary>
		public required string ParentForename { get; set; }

		/// <summary>
		/// Parent's last name
		/// </summary>
		public required string ParentSurname { get; set; }

		/// <summary>
		/// Child's first name
		/// </summary>
		public required string ChildForename { get; set; }

		/// <summary>
		/// Child's surname
		/// </summary>
		public required string ChildSurname { get; set; }

		/// <summary>
		/// Child's date of birth
		/// </summary>
		public required DateTime ChildDOB { get; set; }

		/// <summary>
		/// Child's postcode
		/// </summary>
		public required string? ChildPostcode { get; set; }

		/// <summary>
		/// Partner's national insurance number (NINO).
		/// </summary>
		public string? PartnerNINO { get; set; }

		/// <summary>
		/// Partner's date of birth.
		/// </summary>
		public DateTime? PartnerDOB { get; set; }

		/// <summary>
		/// Partner's first name.
		/// </summary>
		public string? PartnerForename { get; set; }

		/// <summary>
		/// Partner's last name.
		/// </summary>
		public string? PartnerSurname { get; set; }

		/// <summary>
		/// Submission date
		/// </summary>
		public DateTime SubmissionDate { get; set; }
	}
}
