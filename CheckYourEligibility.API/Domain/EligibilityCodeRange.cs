using CheckYourEligibility.API.Domain.Enums.WorkingFamilies;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class EligibilityCodeRange
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int EligibilityCodeRangeId { get; init; }

    public EligibilityCodeType Name { get; init; }

    public long StartRange { get; init; }

    public long EndRange { get; init; }

    public long NextAvailableCode { get; set; }
}
