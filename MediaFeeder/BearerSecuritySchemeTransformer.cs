namespace MediaFeeder;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

///  <inheritdoc/>
internal sealed class BearerSecuritySchemeTransformer(Uri? oidcUrl) : IOpenApiDocumentTransformer
{
    ///  <inheritdoc/>
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var securitySchemes = new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal)
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OpenIdConnect,
                OpenIdConnectUrl = oidcUrl,
                Scheme = "bearer", // "bearer" refers to the header name here
                In = ParameterLocation.Header,
                BearerFormat = "Json Web Token",
            },
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = securitySchemes;

        // Apply it as a requirement for all operations
        foreach (var operation in document.Paths.Values.SelectMany(static path => path.Operations))
        {
            operation.Value.Security ??= [];
            operation.Value.Security.Add(
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
                }
            );
        }
    }
}
