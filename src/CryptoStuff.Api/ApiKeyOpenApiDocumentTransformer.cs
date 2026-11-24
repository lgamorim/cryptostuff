using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CryptoStuff.Api;

/// <summary>
/// Declares the <c>X-Api-Key</c> header scheme in the OpenAPI document and
/// requires it on every operation the document lists.
/// </summary>
internal sealed class ApiKeyOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiKeyAuthentication.SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthentication.HeaderName,
        };

        // Every operation in the document is a protected one: the open routes
        // (health checks, this document, the Scalar UI) are never in it.
        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ApiKeyAuthentication.SchemeName, document)] = [],
        };
        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>()))
        {
            operation.Security ??= [];
            operation.Security.Add(requirement);
        }

        return Task.CompletedTask;
    }
}
