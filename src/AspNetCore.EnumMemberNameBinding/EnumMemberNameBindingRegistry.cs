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
    /// <see cref="TypeDescriptor.AddAttributes(Type, Attribute[])" /> mutates process-wide state and
    /// stacks a new provider on every call, so a type is only ever registered once. Several hosts in
    /// one process — a test suite, most often — then share one registration instead of piling up.
    /// </summary>
    /// <remarks>
    /// A lock rather than a concurrent dictionary, because the requirement is not merely that the
    /// converter be installed once: no caller may return before the installation has completed. A
    /// host that started serving while another was still registering would resolve the stock
    /// converter and cache a model binder built on it, permanently, for that host. Registration
    /// happens once at start-up, so the cost of the lock is irrelevant.
    /// </remarks>
    private static readonly Lock         Gate       = new();
    private static readonly HashSet<Type> Registered = [];

    /// <summary>
    /// Resolves the enums covered by <paramref name="options" />, validates each contract and
    /// registers the converter. Every contract is validated here, at startup — an invalid one throws
    /// <see cref="EnumContractException" /> before the application serves its first request.
    /// </summary>
    internal static IReadOnlyList<Type> Register(EnumMemberNameBindingOptions options) {
        List<Type> registered = [];

        foreach (Type enumType in Discover(options)) {
            EnumContract contract = EnumContract.For(enumType);

            // Validated on every call, so a second registration with stricter options still fails.
            if (!options.AllowPartialContracts && contract.UnannotatedMembers.Count > 0) {
                throw new EnumContractException(enumType, [BuildPartialContractProblem(contract)]);
            }

            lock (Gate) {
                if (Registered.Add(enumType)) {
                    TypeDescriptor.AddAttributes(enumType, new TypeConverterAttribute(typeof(EnumMemberNameConverter)));
                }
            }

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

    private static string BuildPartialContractProblem(EnumContract contract) {
        string members = string.Join(", ", contract.UnannotatedMembers.Select(static m => $"'{m}'"));
        string plural  = contract.UnannotatedMembers.Count == 1 ? " carries" : " carry";

        return $"{members}{plural} no [JsonStringEnumMemberName], so the C# name becomes part of the " +
               "public contract of the API. Annotate every member, or set " +
               $"{nameof(EnumMemberNameBindingOptions)}.{nameof(EnumMemberNameBindingOptions.AllowPartialContracts)} " +
               "if the enum is not yours to annotate.";
    }

    private static IEnumerable<Type> Discover(EnumMemberNameBindingOptions options) {
        HashSet<Type> seen = [];

        foreach (Type explicitType in options.EnumTypes) {
            if (!explicitType.IsEnum) {
                throw new ArgumentException($"'{explicitType.FullName}' is not an enum.", nameof(options));
            }

            if (seen.Add(explicitType)) { yield return explicitType; }
        }

        IEnumerable<Assembly> assemblies = options.Assemblies;

        if (options.Assemblies.Count == 0 && options.EnumTypes.Count == 0) {
            Assembly? entry = Assembly.GetEntryAssembly();
            assemblies = entry is null ? Array.Empty<Assembly>() : new[] { entry };
        }

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
