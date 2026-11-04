using DocumentFormat.OpenXml.Office2010.Excel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CheckYourEligibility.API.Domain
{
	public class TemporaryCodeUpload
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public Guid TemporaryCodeUploadId { get; init; } = Guid.NewGuid();

		[Column(TypeName = "varchar(200)")]
		public string Filename { get; set; }

		public int NumberOfCodes { get; set; }

		[Column(TypeName = "varchar(12)")]
		public string FirstCode { get; set; }

		[Column(TypeName = "varchar(12)")]
		public string LastCode { get; set; }

		public DateTime DateImported { get; set; }

		public virtual User ImportedByUser { get; set; }

		[Column(TypeName = "nvarchar(450)")]
		public string ImportedBy { get; set; }

		public bool IsDeleted { get; set; }
	}
}
