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
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    internal static IReadOnlyList<Type> Register(EnumMemberNameBindingOptions options) {
        List<Type> registered = [];

        foreach (Type enumType in Discover(options)) {
            EnumContract contract = EnumContract.For(enumType);

            // Validated on every call, so a second registration with stricter options still fails.
            if (!options.AllowPartialContracts && contract.UnannotatedMembers.Length > 0) {
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
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    internal static JsonConverter CreateJsonConverter(Type enumType) {
        Type converterType = typeof(JsonStringEnumConverter<>).MakeGenericType(enumType);

        return (JsonConverter)Activator.CreateInstance(converterType, null, false)!;
    }

    private static string BuildPartialContractProblem(EnumContract contract) {
        string members = string.Join(", ", contract.UnannotatedMembers.Select(static m => $"'{m}'"));
        string plural  = contract.UnannotatedMembers.Length == 1 ? " carries" : " carry";

        return $"{members}{plural} no [JsonStringEnumMemberName], so the C# name becomes part of the " +
               "public contract of the API. Annotate every member, or set " +
               $"{nameof(EnumMemberNameBindingOptions)}.{nameof(EnumMemberNameBindingOptions.AllowPartialContracts)} " +
               "if the enum is not yours to annotate.";
    }

    /// <summary>
    /// The enums covered by <paramref name="options" />: those named explicitly, then those found by
    /// scanning.
    /// </summary>
    /// <remarks>
    /// Deliberately not an iterator. Every explicit registration is checked here, before the first
    /// element is produced, so an invalid one is refused before a single converter is installed —
    /// <see cref="TypeDescriptor.AddAttributes(Type, Attribute[])" /> mutates process-wide state and
    /// cannot be undone, which makes "all or nothing" the only honest outcome. An iterator would
    /// defer these throws to the first <c>MoveNext</c>, and the caller would already have registered
    /// whatever came before the bad entry.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static IEnumerable<Type> Discover(EnumMemberNameBindingOptions options) {
        foreach (Type explicitType in options.EnumTypes) {
            RefuseUnlessContractEnum(explicitType, nameof(options));
        }

        return Enumerate(options);
    }

    /// <summary>Why an explicitly named type cannot be registered, as an exception.</summary>
    /// <param name="explicitType">The type named by the caller.</param>
    /// <param name="paramName">
    /// The name to report on <see cref="ArgumentException.ParamName" />. Passed in rather than taken
    /// from this method's own parameter: what a caller can act on is the argument they supplied,
    /// which is the options object, not the local this loop happens to unpack it into.
    /// </param>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static void RefuseUnlessContractEnum(Type explicitType, string paramName) {
        if (!explicitType.IsEnum) {
            throw new ArgumentException($"'{explicitType.FullName}' is not an enum.", paramName);
        }

        // Registering an enum that declares nothing would change how an ordinary enum binds and
        // serializes, which is exactly what this library promises never to do. Naming one
        // explicitly is a mistake worth reporting rather than a preference worth honouring.
        if (!EnumContract.For(explicitType).IsContract) {
            throw new EnumContractException(explicitType, [
                "no member carries [JsonStringEnumMemberName], so there is no contract to apply. "
              + "Registering it would change how an ordinary enum binds and serializes. Annotate its "
              + "members, or drop the registration."
            ]);
        }
    }

    /// <summary>
    /// The two discovery phases, in order: the explicit types, then whatever the scan adds. The
    /// <c>seen</c> set carries from one into the other, which is what keeps a type named explicitly
    /// from being yielded a second time by the scan.
    /// </summary>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static IEnumerable<Type> Enumerate(EnumMemberNameBindingOptions options) {
        HashSet<Type> seen = [];

        // Distinct() rather than filtering on what HashSet.Add returns. The two are equivalent —
        // `seen` starts empty, so Add is true exactly on a first occurrence — but one of them says
        // "each explicit type once" and the other says it as a side effect of remembering.
        foreach (Type explicitType in options.EnumTypes.Distinct()) {
            seen.Add(explicitType);

            yield return explicitType;
        }

        foreach (Assembly assembly in AssembliesToScan(options)) {
            foreach (Type type in GetLoadableTypes(assembly)) {
                if (!type.IsEnum || !seen.Add(type)) { continue; }

                if (EnumContract.For(type).IsContract) { yield return type; }
                else { seen.Remove(type); }
            }
        }
    }

    /// <summary>
    /// The assemblies to scan: those configured, or the entry assembly when nothing at all was
    /// configured — naming types or assemblies is taken as "scan nothing else".
    /// </summary>
    private static IEnumerable<Assembly> AssembliesToScan(EnumMemberNameBindingOptions options) {
        if (options.Assemblies.Count > 0 || options.EnumTypes.Count > 0) {
            return options.Assemblies.Distinct();
        }

        Assembly? entry = Assembly.GetEntryAssembly();

        return entry is null ? [] : [entry];
    }

    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
        try {
            return assembly.GetTypes();
        } catch (ReflectionTypeLoadException ex) {
            return ex.Types.OfType<Type>();
        }
    }

}
