using CRC.Api.Security;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CRC.Api.Swagger;

/// <summary>
/// Adds API key header to Swagger UI for authenticated endpoints.
/// </summary>
public sealed class SwaggerApiKeyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Swagger should describe the API key header so testing is easy.
        operation.Parameters ??= new List<OpenApiParameter>();

        // Avoid duplicates.
        if (operation.Parameters.Any(p => string.Equals(p.Name, "X-Api-Key", StringComparison.OrdinalIgnoreCase)))
            return;

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Api-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Public API key",
            Schema = new OpenApiSchema { Type = "string" }
        });

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Too Many Requests" });
    }
}
