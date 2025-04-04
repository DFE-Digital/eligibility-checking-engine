// Ignore Spelling: Fsm

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json;

namespace CheckYourEligibility.API.Gateways;

public class BaseGateway
{
    private readonly TelemetryClient _telemetry;

    public BaseGateway()
    {
        _telemetry = new TelemetryClient();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GetCurrentMethod()
    {
        var st = new StackTrace();
        var sf = st.GetFrame(1);

        return sf?.GetMethod()?.Name ?? string.Empty;
    }

    protected void TrackMetric(string name, double value)
    {
        _telemetry.TrackMetric(name, value);
    }

    protected void LogApiEvent<t1, t2>(string className, t1 data, t2 response, [CallerMemberName] string name = "",
        string message = "")
    {
        var guid = Guid.NewGuid().ToString();
        var jsonString = JsonConvert.SerializeObject(data);
        var responseData = JsonConvert.SerializeObject(response);
        _telemetry.TrackEvent($"API {name} event",
            new Dictionary<string, string>
            {
                { "LogId", guid },
                { "Class", className },
                { "Method", name },
                { "Data", jsonString },
                { "Response", responseData },
                { "Message", message }
            });
    }

    [ExcludeFromCodeCoverage]
    protected async Task LogApiError(HttpResponseMessage task, string method, string uri, string data)
    {
        var guid = Guid.NewGuid().ToString();
        if (task.Content != null)
        {
            var jsonString = await task.Content.ReadAsStringAsync();
            _telemetry.TrackEvent($"API {method} failure",
                new Dictionary<string, string>
                {
                    { "LogId", guid },
                    { "Response Code", task.StatusCode.ToString() },
                    { "Address", uri },
                    { "Request Data", data },
                    { "Response Data", jsonString }
                });
        }
        else
        {
            _telemetry.TrackEvent($"API Failure:-{method}",
                new Dictionary<string, string> { { "LogId", guid }, { "Address", uri } });
        }

        throw new Exception(
            $"API Failure:-{method} , your issue has been logged please use the following reference:- {guid}");
    }

    [ExcludeFromCodeCoverage]
    protected async Task LogApiError(HttpResponseMessage task, string method, string uri)
    {
        var guid = Guid.NewGuid().ToString();


        if (task.Content != null)
        {
            var jsonString = await task.Content.ReadAsStringAsync();
            _telemetry.TrackEvent($"API {method} failure",
                new Dictionary<string, string>
                {
                    { "LogId", guid },
                    { "Address", uri },
                    { "Response Code", task.StatusCode.ToString() },
                    { "Data", jsonString }
                });
        }
        else
        {
            _telemetry.TrackEvent($"API {method} failure",
                new Dictionary<string, string> { { "LogId", guid }, { "Address", uri } });
        }
    }
}