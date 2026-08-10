using System.Diagnostics.CodeAnalysis;

using AspNetCore.EnumMemberNameBinding.OpenApi;

using Microsoft.AspNetCore.OpenApi;

// IDE0130 is right everywhere else and wrong here: the namespace is chosen, for the reason on the
// class below, and it will never match the folder. A pragma rather than [SuppressMessage] because
// the finding is reported on the namespace declaration, which no attribute reaches — measured, not
// assumed: the attribute leaves it standing.
#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registration of enum member name correction on <see cref="OpenApiOptions" />.
/// </summary>
/// <remarks>
/// Placed in <c>Microsoft.Extensions.DependencyInjection</c> rather than in the namespace owning
/// <see cref="OpenApiOptions" />, because that is the namespace the caller already has. The Web SDK
/// imports it implicitly — it is how <c>AddOpenApi</c> itself resolves — while
/// <c>Microsoft.AspNetCore.OpenApi</c> is not implicitly imported, so the conventional placement
/// cost every consumer a <c>using</c> directive they had no way to guess, and cost this repository
/// one in each OpenAPI documentation page and in the package smoke test.
///
/// The method is <c>AddEnumMemberNames</c> and not <c>AddEnumMemberNameBinding</c>, which was
/// weighed at v1 and kept: this package binds nothing, it describes. <c>EnumMemberNameBinding</c>
/// names the registration that installs the binder; this one makes the generated document carry the
/// member names, and saying "binding" of it would be inaccurate for the sake of symmetry.
/// </remarks>
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
