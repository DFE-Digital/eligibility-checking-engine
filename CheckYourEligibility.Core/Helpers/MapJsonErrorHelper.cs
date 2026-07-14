using System.Text.Json;

namespace CheckYourEligibility.Core.Helpers
{
    public static class MapJsonErrorHelper
    {
        public static string MapJsonError(JsonException ex)
        {
            var baseMessage = "The uploaded file could not be processed. Please check the file format and try again.";

            var errorMessage = ex.Message.ToLowerInvariant();

            if (errorMessage.Contains("invalid start of a property name") ||
                errorMessage.Contains("expected a '\"'"))
            {
                return baseMessage + 
                    " If submitting data programmatically, ensure text values use double quotes (\") rather than single quotes (').";
            }

            return baseMessage;
        }
    }
}