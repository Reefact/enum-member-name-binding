using System.Diagnostics.CodeAnalysis;

using AspNetCore.EnumMemberNameBinding;

using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration of enum member name binding on an <see cref="IMvcBuilder" />.
/// </summary>
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
    [RequiresDynamicCode("Enum member name binding resolves converters through reflection and is not compatible with Native AOT.")]
    [RequiresUnreferencedCode("Enum member name binding scans assemblies for enum types and is not compatible with trimming.")]
    public static IMvcBuilder AddEnumMemberNameBinding(this IMvcBuilder builder, Action<EnumMemberNameBindingOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(builder);

        EnumMemberNameBindingOptions options = new();
        configure?.Invoke(options);

        IReadOnlyList<Type> contractEnums = EnumMemberNameBindingRegistry.Register(options);

        if (options.ConfigureJsonSerialization && contractEnums.Count > 0) {
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
        }

        return builder;
    }

}
