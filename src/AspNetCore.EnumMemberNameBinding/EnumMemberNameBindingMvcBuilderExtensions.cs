using System.Diagnostics.CodeAnalysis;

using AspNetCore.EnumMemberNameBinding;

// IDE0130 is right everywhere else and wrong here: the namespace is chosen, for the reason on the
// class below, and it will never match the folder. A pragma rather than [SuppressMessage] because
// the finding is reported on the namespace declaration, which no attribute reaches — measured, not
// assumed: the attribute leaves it standing.
#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registration of enum member name binding on an <see cref="IMvcBuilder" />.
/// </summary>
/// <remarks>
/// Placed in <c>Microsoft.Extensions.DependencyInjection</c> rather than in this library's own
/// namespace, and deliberately: it is where <see cref="IMvcBuilder" /> itself is declared, so an
/// extension of it belongs with what it extends. The Web SDK imports that namespace implicitly —
/// it is how <c>AddControllers</c> resolves — so
/// <c>builder.Services.AddControllers().AddEnumMemberNameBinding()</c> compiles with no
/// <c>using</c> the caller had to guess. Every registration entry point in these two packages sits
/// here for the same reason.
/// </remarks>
public static class EnumMemberNameBindingMvcBuilderExtensions {

    /// <summary>
    /// Makes route values, query strings, form fields and headers accept the enum member names
    /// declared with <c>[JsonStringEnumMemberName]</c> — the same vocabulary the request body
    /// already accepts.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <param name="configure">Optional configuration. By default the entry assembly is scanned.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="EnumContractException">An enum declares an ambiguous or malformed contract.</exception>
    /// <remarks>
    /// Must be called during application start-up. ASP.NET Core caches the model binder it builds
    /// for a given type on first use, so a converter registered after the first request has no effect.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    public static IMvcBuilder AddEnumMemberNameBinding(this IMvcBuilder builder, Action<EnumMemberNameBindingOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(builder);

        EnumMemberNameBindingOptions options = new();
        configure?.Invoke(options);

        IReadOnlyList<Type> contractEnums = EnumMemberNameBindingRegistry.Register(options);

        if (!options.ConfigureJsonSerialization) { return builder; }
        if (contractEnums.Count == 0) { return builder; }

        builder.AddJsonOptions(json => {
            foreach (Type enumType in contractEnums) {
                json.JsonSerializerOptions.Converters.Add(EnumMemberNameBindingRegistry.CreateJsonConverter(enumType));
            }
        });

        // MVC and the rest of the stack read two different option objects. Microsoft.AspNetCore.OpenApi
        // and minimal API serialization use Http.Json.JsonOptions, so configuring only the MVC one
        // leaves the generated OpenAPI document describing every contract enum as an integer.
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(json => {
            foreach (Type enumType in contractEnums) {
                json.SerializerOptions.Converters.Add(EnumMemberNameBindingRegistry.CreateJsonConverter(enumType));
            }
        });

        return builder;
    }

}
