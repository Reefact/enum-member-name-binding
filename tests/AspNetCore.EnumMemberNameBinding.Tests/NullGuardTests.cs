using System.ComponentModel;
using System.Reflection;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Every member on the boundary of a type refuses a null argument it declared non-null.
/// </summary>
/// <remarks>
/// Discovered by reflection rather than listed, so a member written tomorrow is covered too. A
/// <c>string?</c> parameter is skipped: accepting null is then its contract.
/// </remarks>
public sealed class NullGuardTests {

    private static readonly Dictionary<Type, Func<object>> Instances = new() {
        [typeof(EnumContract)]             = () => EnumContract.For(typeof(ProductStatus)),
        [typeof(EnumMemberNameConverter)]  = () => new EnumMemberNameConverter(typeof(ProductStatus)),
    };

    private static readonly Dictionary<Type, object> Values = new() {
        [typeof(Type)]                          = typeof(ProductStatus),
        [typeof(string)]                        = "available",
        [typeof(object)]                        = ProductStatus.Available,
        [typeof(IReadOnlyList<string>)]          = new[] { "a problem" },
        [typeof(EnumMemberNameBindingOptions)]  = new EnumMemberNameBindingOptions(),
        [typeof(Enum)]                          = ProductStatus.Available,
    };

    private static readonly Dictionary<string, string> Unexercisable = new(StringComparer.Ordinal) {
        ["AddEnumMemberNameBinding"] =
            "Takes an IMvcBuilder, whose only real implementation comes from a configured service "
          + "collection; the guard is covered by the extension's own tests instead.",
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

    [Theory]
    [MemberData(nameof(GuardedParameters))]
    public void a_non_nullable_reference_parameter_is_refused_when_null(string identity) {
        (MethodBase member, ParameterInfo parameter) =
            BoundaryParameters().Single(p => $"{p.Member.DeclaringType!.Name}.{Describe(p.Member)}({p.Parameter.Name})" == identity);

        if (Unexercisable.ContainsKey(member.Name)) { return; }

        object?[] arguments = [.. member.GetParameters().Select(p => p.Position == parameter.Position ? null : ValueFor(p))];

        Exception thrown = Assert.ThrowsAny<Exception>(() => Invoke(member, arguments));

        Exception actual = thrown is TargetInvocationException wrapper ? wrapper.InnerException! : thrown;

        Assert.True(actual is ArgumentNullException,
                    $"{identity} answered a null with {actual.GetType().Name} instead of ArgumentNullException. "
                  + $"Add ArgumentNullException.ThrowIfNull({parameter.Name}) at the top of the member.");

        Assert.Equal(parameter.Name, ((ArgumentNullException)actual).ParamName);
    }

    [Fact]
    public void the_boundary_is_not_empty() {
        Assert.True(BoundaryParameters().Count >= 12,
                    $"Only {BoundaryParameters().Count} boundary parameters were discovered; the reflection filter is probably wrong.");
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
