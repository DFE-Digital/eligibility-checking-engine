using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CheckYourEligibility.API.Filters;

/// <summary>
/// Swagger DocumentFilter that removes the "type" property from all request body examples.
/// Because the project uses Swashbuckle.AspNetCore.Newtonsoft, examples are serialized as raw
/// JSON values rather than structured OpenApiObject trees. To reliably strip the "type" field,
/// we intercept the serialized example JSON and remove it via regex.
/// This filter only affects Swagger documentation — zero impact on runtime behavior.
/// </summary>
public class RemoveTypeFromRequestExamplesFilter : IDocumentFilter
{
    private static readonly Regex TypePropertyRegex = new(
        @",?\s*""type""\s*:\s*""[^""]*""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LeadingCommaRegex = new(
        @"(?<=\{)\s*,",
        RegexOptions.Compiled);

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var pathItem in swaggerDoc.Paths.Values)
        {
            foreach (var operation in pathItem.Operations.Values)
            {
                if (operation.RequestBody?.Content == null) continue;

                foreach (var mediaType in operation.RequestBody.Content.Values)
                {
                    ProcessExample(mediaType);
                }
            }
        }
    }

    private static void ProcessExample(OpenApiMediaType mediaType)
    {
        // Handle the singular Example property
        if (mediaType.Example != null)
        {
            mediaType.Example = StripTypeFromOpenApiAny(mediaType.Example);
        }

        // Handle the Examples dictionary
        if (mediaType.Examples != null)
        {
            foreach (var kvp in mediaType.Examples)
            {
                if (kvp.Value?.Value != null)
                {
                    kvp.Value.Value = StripTypeFromOpenApiAny(kvp.Value.Value);
                }
            }
        }
    }

    private static Microsoft.OpenApi.Any.IOpenApiAny StripTypeFromOpenApiAny(Microsoft.OpenApi.Any.IOpenApiAny example)
    {
        // Get the raw JSON string representation
        string json;
        using (var sw = new System.IO.StringWriter())
        {
            var writer = new Microsoft.OpenApi.Writers.OpenApiJsonWriter(sw);
            example.Write(writer, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
            json = sw.ToString();
        }

        // Remove "type":"..." entries (handles both ,"type":"X" and "type":"X",)
        var cleaned = TypePropertyRegex.Replace(json, "");
        // Fix any leading commas left after removal (e.g. {, "field":...})
        cleaned = LeadingCommaRegex.Replace(cleaned, "");

        if (cleaned == json) return example; // Nothing changed

        // Parse back to IOpenApiAny
        var node = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JToken>(cleaned);
        return ConvertJTokenToOpenApi(node);
    }

    private static Microsoft.OpenApi.Any.IOpenApiAny ConvertJTokenToOpenApi(Newtonsoft.Json.Linq.JToken token)
    {
        switch (token.Type)
        {
            case Newtonsoft.Json.Linq.JTokenType.Object:
                var obj = new Microsoft.OpenApi.Any.OpenApiObject();
                foreach (var prop in (Newtonsoft.Json.Linq.JObject)token)
                {
                    obj[prop.Key] = ConvertJTokenToOpenApi(prop.Value);
                }
                return obj;

            case Newtonsoft.Json.Linq.JTokenType.Array:
                var arr = new Microsoft.OpenApi.Any.OpenApiArray();
                foreach (var item in (Newtonsoft.Json.Linq.JArray)token)
                {
                    arr.Add(ConvertJTokenToOpenApi(item));
                }
                return arr;

            case Newtonsoft.Json.Linq.JTokenType.String:
                return new Microsoft.OpenApi.Any.OpenApiString(token.ToObject<string>());

            case Newtonsoft.Json.Linq.JTokenType.Integer:
                return new Microsoft.OpenApi.Any.OpenApiInteger((int)token.ToObject<long>());

            case Newtonsoft.Json.Linq.JTokenType.Float:
                return new Microsoft.OpenApi.Any.OpenApiDouble(token.ToObject<double>());

            case Newtonsoft.Json.Linq.JTokenType.Boolean:
                return new Microsoft.OpenApi.Any.OpenApiBoolean(token.ToObject<bool>());

            case Newtonsoft.Json.Linq.JTokenType.Null:
                return new Microsoft.OpenApi.Any.OpenApiNull();

            default:
                return new Microsoft.OpenApi.Any.OpenApiString(token.ToString());
        }
    }
}
