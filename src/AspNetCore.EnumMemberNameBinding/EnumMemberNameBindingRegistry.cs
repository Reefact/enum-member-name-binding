using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Discovers contract enums and installs the <see cref="EnumMemberNameConverter" /> for each of them.
/// </summary>
internal static class EnumMemberNameBindingRegistry {

    /// <summary>
    /// Resolves the enums covered by <paramref name="options" />, validates each contract and
    /// registers the converter. Every contract is validated here, at startup — an invalid one throws
    /// <see cref="EnumContractException" /> before the application serves its first request.
    /// </summary>
    internal static IReadOnlyList<Type> Register(EnumMemberNameBindingOptions options) {
        List<Type> registered = [];

        foreach (Type enumType in Discover(options)) {
            EnumContract.For(enumType);
            TypeDescriptor.AddAttributes(enumType, new TypeConverterAttribute(typeof(EnumMemberNameConverter)));
            registered.Add(enumType);
        }

        return registered;
    }

    /// <summary>Builds the <c>System.Text.Json</c> converter for a single enum type.</summary>
    [RequiresDynamicCode("Constructs JsonStringEnumConverter<T> for the enum type at run time.")]
    internal static JsonConverter CreateJsonConverter(Type enumType) {
        Type converterType = typeof(JsonStringEnumConverter<>).MakeGenericType(enumType);

        return (JsonConverter)Activator.CreateInstance(converterType, null, false)!;
    }

    private static IEnumerable<Type> Discover(EnumMemberNameBindingOptions options) {
        HashSet<Type> seen = [];

        foreach (Type explicitType in options.EnumTypes) {
            if (!explicitType.IsEnum) {
                throw new ArgumentException($"'{explicitType.FullName}' is not an enum.", nameof(options));
            }

            if (seen.Add(explicitType)) { yield return explicitType; }
        }

        IEnumerable<Assembly> assemblies = options.Assemblies.Count > 0 || options.EnumTypes.Count > 0
            ? options.Assemblies
            : [.. new[] { Assembly.GetEntryAssembly() }.OfType<Assembly>()];

        foreach (Assembly assembly in assemblies.Distinct()) {
            foreach (Type type in GetLoadableTypes(assembly)) {
                if (!type.IsEnum || !seen.Add(type)) { continue; }

                if (EnumContract.For(type).IsContract) { yield return type; }
                else { seen.Remove(type); }
            }
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
        try {
            return assembly.GetTypes();
        } catch (ReflectionTypeLoadException ex) {
            return ex.Types.OfType<Type>();
        }
    }

}
