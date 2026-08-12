using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Resolves the contract enums an application asked for, and refuses the ones it cannot honour.
/// </summary>
internal static class EnumMemberNameBindingRegistry {

    /// <summary>
    /// Resolves the enums covered by <paramref name="options" /> and validates each contract. Every
    /// contract is validated here, at start-up — an invalid one throws
    /// <see cref="EnumContractException" /> before the application serves its first request.
    /// </summary>
    /// <remarks>
    /// All or nothing: if any enum is refused, the caller receives nothing to register. Resolving
    /// and refusing are both finished before a single type is returned, so a caller who reads the
    /// exception has nothing to undo — which is what "the registration did not happen" has to mean
    /// for it to be worth saying.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    internal static IReadOnlyList<Type> Register(EnumMemberNameBindingOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        List<Type> discovered = [.. Discover(options)];

        foreach (Type enumType in discovered) {
            RefuseIfContractIsPartial(enumType, options.AllowPartialContracts);
        }

        return discovered;
    }

    /// <summary>
    /// Refuses an enum that declares a name on some members only, unless the caller has said that is
    /// what they want.
    /// </summary>
    /// <remarks>
    /// Checked on every call rather than once per type, so a second registration with stricter
    /// options still fails: what is allowed is a property of the call, not of the enum.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    private static void RefuseIfContractIsPartial(Type enumType, bool allowPartialContracts) {
        if (allowPartialContracts) { return; }

        EnumContract contract = EnumContract.For(enumType);
        if (contract.UnannotatedMembers.Length == 0) { return; }

        throw new EnumContractException(enumType, [Problem.PartialContract(contract.UnannotatedMembers)]);
    }

    /// <summary>What this type says when it refuses to register an enum.</summary>
    private static class Problem {

        /// <summary>The plural agrees, so the sentence reads correctly for one member as for several.</summary>
        internal static string PartialContract(ImmutableArray<string> unannotatedMembers) {
            string members = string.Join(", ", unannotatedMembers.Select(static m => $"'{m}'"));
            string plural  = unannotatedMembers.Length == 1 ? " carries" : " carry";

            return $"{members}{plural} no [JsonStringEnumMemberName], so the C# name becomes part of the " +
                   "public contract of the API. Annotate every member, or set " +
                   $"{nameof(EnumMemberNameBindingOptions)}.{nameof(EnumMemberNameBindingOptions.AllowPartialContracts)} " +
                   "if the enum is not yours to annotate.";
        }

        internal static string NoContractToApply() {
            return "no member carries [JsonStringEnumMemberName], so there is no contract to apply. "
                 + "Registering it would change how an ordinary enum binds and serializes. Annotate its "
                 + "members, or drop the registration.";
        }

    }

    /// <summary>Builds the <c>System.Text.Json</c> converter for a single enum type.</summary>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    internal static JsonConverter CreateJsonConverter(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);

        Type converterType = typeof(JsonStringEnumConverter<>).MakeGenericType(enumType);

        return (JsonConverter)Activator.CreateInstance(converterType, null, false)!;
    }

    /// <summary>
    /// The enums covered by <paramref name="options" />: those named explicitly, then those found by
    /// scanning.
    /// </summary>
    /// <remarks>
    /// Deliberately not an iterator. Every explicit registration is checked here, before the first
    /// element is produced, which is what makes "all or nothing" true of the refusals decided at this
    /// step as well as of the ones decided per type. An iterator would defer these throws to the
    /// first <c>MoveNext</c>, and the caller would already be part-way through the list.
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
        if (!explicitType.IsEnum) { throw new ArgumentException($"'{explicitType.FullName}' is not an enum.", paramName); }

        // Registering an enum that declares nothing would change how an ordinary enum binds and
        // serializes, which is exactly what this library promises never to do. Naming one
        // explicitly is a mistake worth reporting rather than a preference worth honouring.
        if (!EnumContract.For(explicitType).IsContract) { throw new EnumContractException(explicitType, [Problem.NoContractToApply()]); }
    }

    /// <summary>
    /// The two discovery phases, in order: the explicit types, then whatever the scan adds. The
    /// <c>seen</c> set carries from one into the other, which is what keeps a type named explicitly
    /// from being yielded a second time by the scan.
    /// </summary>
    /// <remarks>
    /// An enum nested in a generic type is passed by, and that is not a policy about which contracts
    /// are worth registering: it is the one enum reflection will not let this look at.
    /// <c>Assembly.GetTypes()</c> hands it over in its open form — <c>Box`1+Colour</c> — where
    /// <see cref="Type.IsEnum" /> is true and <see cref="Type.ContainsGenericParameters" /> is true
    /// as well, and <c>FieldInfo.GetValue</c> on any member of it throws
    /// <see cref="ArgumentException" /> out of <c>Enum.InternalBoxEnum</c>. That happens in
    /// <c>EnumContract</c>'s constructor, before the contract is so much as looked at, so an enum
    /// nobody annotated and nobody wanted registered stopped the application booting with a message
    /// naming neither the type nor this package. A closed form is not affected — it carries no generic
    /// parameter — so a nested enum that does declare a contract is still registrable by naming it,
    /// through <c>AddEnum&lt;Crate&lt;int&gt;.State&gt;()</c>. <c>Box&lt;T&gt;.Colour</c> above is not,
    /// and never was: it declares no contract, which <c>RefuseUnlessContractEnum</c> answers for.
    /// </remarks>
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
                // ContainsGenericParameters before IsEnum would read the same and say less: what is
                // being passed by is an enum, and it is passed by for a reason peculiar to enums.
                if (!type.IsEnum || type.ContainsGenericParameters || !seen.Add(type)) { continue; }

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
        if (SomethingWasNamed(options)) { return options.Assemblies.Distinct(); }

        Assembly? entry = Assembly.GetEntryAssembly();

        return entry is null ? [] : [entry];
    }

    /// <summary>
    /// Whether the caller named anything at all — an assembly, or a type. Naming one is what turns
    /// the entry assembly from a default into something that was not asked for.
    /// </summary>
    private static bool SomethingWasNamed(EnumMemberNameBindingOptions options) {
        return options.Assemblies.Count > 0 || options.EnumTypes.Count > 0;
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
