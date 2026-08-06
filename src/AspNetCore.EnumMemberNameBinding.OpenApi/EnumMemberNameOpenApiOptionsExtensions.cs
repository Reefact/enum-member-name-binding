using System.Diagnostics.CodeAnalysis;

using AspNetCore.EnumMemberNameBinding.OpenApi;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Registration of enum member name correction on <see cref="OpenApiOptions" />.
/// </summary>
public static class EnumMemberNameOpenApiOptionsExtensions {

    /// <summary>
    /// Makes the generated document describe contract enums with the names the server actually
    /// accepts on every channel.
    /// </summary>
    /// <param name="options">The OpenAPI document options.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenApi(options => options.AddEnumMemberNames());
    /// </code>
    /// </example>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    public static OpenApiOptions AddEnumMemberNames(this OpenApiOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        options.AddSchemaTransformer(new EnumMemberNameSchemaTransformer());

        return options;
    }

}
