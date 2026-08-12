using System.Reflection;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Every member on the boundary of a type refuses a null argument it declared non-null.
/// </summary>
/// <remarks>
/// Discovered by reflection rather than listed, so a member written tomorrow is covered too. A
/// <c>string?</c> parameter is skipped: accepting null is then its contract.
/// </remarks>
public sealed class NullGuardTests {

    private static readonly Dictionary<Type, Func<object>> Instances = new() {
        [typeof(EnumContract)]                        = () => EnumContract.For(typeof(ProductStatus)),
        [typeof(EnumMemberNameModelBinder)]           = () => new EnumMemberNameModelBinder(EnumContract.For(typeof(ProductStatus))),
        [typeof(EnumMemberNameModelBinderProvider)]   = () => new EnumMemberNameModelBinderProvider(new EnumMemberNameBindingRegistrations()),
        [typeof(EnumMemberNameBindingRegistrations)]  = () => new EnumMemberNameBindingRegistrations(),
    };

    private static readonly Dictionary<Type, object> Values = new() {
        [typeof(Type)]                                = typeof(ProductStatus),
        [typeof(string)]                              = "available",
        [typeof(object)]                              = ProductStatus.Available,
        [typeof(IReadOnlyList<string>)]                = new[] { "a problem" },
        [typeof(IEnumerable<Type>)]                    = new[] { typeof(ProductStatus) },
        [typeof(EnumMemberNameBindingOptions)]        = new EnumMemberNameBindingOptions(),
        [typeof(EnumMemberNameBindingRegistrations)]  = new EnumMemberNameBindingRegistrations(),
        [typeof(EnumContract)]                        = EnumContract.For(typeof(ProductStatus)),
        [typeof(Enum)]                                = ProductStatus.Available,
    };

    public static TheoryData<string> GuardedParameters {
        get {
            TheoryData<string> data = new();
            foreach ((MethodBase member, ParameterInfo parameter) in BoundaryParameters()) {
                data.Add($"{member.DeclaringType!.Name}.{Describe(member)}({parameter.Name})");
            }

            return data;
        }
    }

    /// <summary>
    /// Every non-nullable reference parameter on a public or internal boundary answers a null with
    /// <see cref="ArgumentNullException" /> naming that parameter.
    /// </summary>
    /// <remarks>
    /// This theory carried an exemption list, and <c>AddEnumMemberNameBinding</c> was the one entry
    /// in it: the row was generated, returned before building an argument array, asserted nothing and
    /// reported green. The recorded reason — that an <c>IMvcBuilder</c> can only come from a
    /// configured service collection — was not true of what the row needs, which is a null. Removing
    /// the exemption made it pass as written, so the package's one public entry point had gone
    /// unguarded by anything for as long as the list existed.
    /// <para>
    /// CA1062 does hold a second line here, and it is worth being exact about what each covers: the
    /// analyzer refuses a boundary that validates nothing at all, and this row refuses one that
    /// validates with the wrong exception. Replacing the guard with an
    /// <c>InvalidOperationException</c> compiles clean and fails here — which is the mutation that
    /// was run before deleting the list.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(GuardedParameters))]
    public void a_non_nullable_reference_parameter_is_refused_when_null(string identity) {
        (MethodBase member, ParameterInfo parameter) = BoundaryParameters()
            .Single(p => $"{p.Member.DeclaringType!.Name}.{Describe(p.Member)}({p.Parameter.Name})" == identity);

        object?[] arguments = [.. member.GetParameters().Select(p => p.Position == parameter.Position ? null : ValueFor(p))];

        Exception thrown = Check.ThatCode(() => Invoke(member, arguments)).ThrowsAny().Value;

        Exception actual = thrown is TargetInvocationException wrapper ? wrapper.InnerException! : thrown;

        Check.WithCustomMessage($"{identity} answered a null with {actual.GetType().Name} instead of ArgumentNullException. " + $"Add ArgumentNullException.ThrowIfNull({parameter.Name}) at the top of the member.")
             .That(actual is ArgumentNullException).IsTrue();

        Check.That(((ArgumentNullException)actual).ParamName).IsEqualTo(parameter.Name);
    }

    [Fact]
    public void the_boundary_is_not_empty() {
        Check.WithCustomMessage($"Only {BoundaryParameters().Count} boundary parameters were discovered; the reflection filter is probably wrong.")
             .That(BoundaryParameters().Count).IsGreaterOrEqualThan(12);
    }

    private static object? ValueFor(ParameterInfo parameter) {
        Type type = parameter.ParameterType;
        if (type.IsByRef) { return null; }
        if (Values.TryGetValue(type, out object? value)) { return value; }
        if (type.IsValueType) { return Activator.CreateInstance(type); }

        return null;
    }

    private static void Invoke(MethodBase member, object?[] arguments) {
        if (member is ConstructorInfo constructor) {
            constructor.Invoke(arguments);

            return;
        }

        object? target = member.IsStatic ? null : Instances[member.DeclaringType!]();
        member.Invoke(target, arguments);
    }

    private static string Describe(MethodBase member) {
        return member is ConstructorInfo ? "ctor" : member.Name;
    }

    private static List<(MethodBase Member, ParameterInfo Parameter)> BoundaryParameters() {
        NullabilityInfoContext nullability = new();
        List<(MethodBase Member, ParameterInfo Parameter)> found = [];

        foreach (Type type in typeof(EnumContract).Assembly.GetTypes()) {
            if (IsCompilerGenerated(type)) { continue; }

            // A private nested type is inside its container's boundary, whatever its members
            // declare: only the containing type can reach it, and a type trusts itself. Reading the
            // member's own accessibility is not enough, since `internal` on a member of a private
            // type is still private in effect.
            if (type.IsNestedPrivate) { continue; }

            IEnumerable<MethodBase> members =
                type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Cast<MethodBase>()
                    .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));

            foreach (MethodBase member in members) {
                if (member.IsPrivate || member.IsFamily) { continue; }
                if (member.DeclaringType != type) { continue; }
                if (member is MethodInfo && member.IsSpecialName) { continue; }

                foreach (ParameterInfo parameter in member.GetParameters()) {
                    if (parameter.ParameterType.IsValueType || parameter.ParameterType.IsByRef) { continue; }
                    if (nullability.Create(parameter).WriteState != NullabilityState.NotNull) { continue; }

                    found.Add((member, parameter));
                }
            }
        }

        return found;
    }

    private static bool IsCompilerGenerated(Type type) {
        return type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false)
            || type.Name.Contains('<', StringComparison.Ordinal);
    }

}
