namespace CheckYourEligibility.API.Boundary.Responses
{
	public class TemporaryCodeUploadResponse
	{
		public string Filename { get; set; }
		public int NumberOfCodes { get; set; }
		public string FirstCode { get; set; }
		public string LastCode { get; set; }
		public DateTime DateImported { get; set; }
		public string ImportedBy { get; set; }
	}
}
