namespace CheckYourEligibility.API.Domain;

public class DernOverlapException : Exception
{
    public string IncomingEventId { get; }
    public string Dern { get; }
    public DateTime IncomingStart { get; }
    public DateTime IncomingEnd { get; }
    public List<OverlapDetail> Overlaps { get; }

    public DernOverlapException(
        string incomingEventId,
        string dern,
        DateTime incomingStart,
        DateTime incomingEnd,
        List<OverlapDetail> overlaps)
        : base(BuildMessage(incomingEventId, dern, incomingStart, incomingEnd, overlaps))
    {
        IncomingEventId = incomingEventId;
        Dern = dern;
        IncomingStart = incomingStart;
        IncomingEnd = incomingEnd;
        Overlaps = overlaps;
    }

    private static string BuildMessage(
        string incomingEventId, string dern,
        DateTime incomingStart, DateTime incomingEnd,
        List<OverlapDetail> overlaps)
    {
        var first = overlaps[0];
        return $"The validity dates supplied ({incomingStart:yyyy-MM-dd} and {incomingEnd:yyyy-MM-dd}) " +
               $"overlap with another event ({first.EligibilityEventId}) " +
               $"for the same DERN ({dern} dates {first.ValidityStartDate:yyyy-MM-dd} and {first.ValidityEndDate:yyyy-MM-dd})";
    }
}

public class OverlapDetail
{
    public string EligibilityEventId { get; set; } = string.Empty;
    public string Dern { get; set; } = string.Empty;
    public DateTime ValidityStartDate { get; set; }
    public DateTime ValidityEndDate { get; set; }
    public string Message { get; set; } = "The validity dates supplied overlap with another event for the same DERN";
}
