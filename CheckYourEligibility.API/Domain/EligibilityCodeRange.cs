using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class EligibilityCodeRange
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int EligibilityCodeRangeId { get; init; }
    public long StartRange { get; init; }
    public long EndRange { get; init; }
    public long NextAvailableCode { get; set; }
    [Timestamp]
    public byte[] RowVersion { get; set; } = default!;

}