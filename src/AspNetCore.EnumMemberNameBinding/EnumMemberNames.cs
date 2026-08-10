using System.Diagnostics.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Reads the public names an enum declares through <c>[JsonStringEnumMemberName]</c>.
/// </summary>
/// <remarks>
/// Exposed so that companion packages — OpenAPI document generation in particular — can describe
/// exactly the vocabulary the binder accepts, without duplicating the resolution rules.
/// </remarks>
public static class EnumMemberNames {

    /// <summary>
    /// Returns the public names of <paramref name="enumType" /> in declaration order, or
    /// <see langword="null" /> when the type declares no contract.
    /// </summary>
    /// <param name="enumType">An enum type. Nullable enum types are unwrapped.</param>
    /// <exception cref="EnumContractException">The declared contract is ambiguous or malformed.</exception>
    /// <remarks>
    /// The returned list is never written through, and a test holds the current implementation to
    /// returning an immutable one. That concrete type is not part of the contract: what is promised
    /// is <see cref="IReadOnlyList{T}" />, and a caller must not depend on the runtime type behind
    /// it. The interface was kept rather than narrowed at v1 deliberately — it is the idiomatic
    /// public shape, it matches <see cref="EnumContractException.Problems" />, and this method is
    /// called once per enum at start-up or document generation, where the boxing is free.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    public static IReadOnlyList<string>? GetPublicNames(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);

        Type underlying = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!underlying.IsEnum) { return null; }

        EnumContract contract = EnumContract.For(underlying);

        return contract.IsContract ? contract.PublicNames : null;
    }

    /// <summary>
    /// Returns the public name of <paramref name="value" />, or <see langword="null" /> when it has
    /// none — an undeclared value, or an enum that declares no contract.
    /// </summary>
    /// <param name="value">An enum value. A combination of <c>[Flags]</c> members is rendered as a
    /// comma-separated list, exactly as <c>System.Text.Json</c> writes it.</param>
    /// <remarks>
    /// Use this when generating links. ASP.NET Core formats a route value with the value's own
    /// <c>ToString()</c> and nothing this package installs, so a link built from the enum value
    /// itself carries the C# name and the binder will refuse it:
    /// <code>
    /// // produces /products/OutOfStock, which this same API answers 400 to
    /// links.GetPathByAction(context, "ByStatus", "Products", new { status = ProductStatus.OutOfStock });
    ///
    /// // produces /products/out_of_stock
    /// links.GetPathByAction(context, "ByStatus", "Products",
    ///                       new { status = EnumMemberNames.GetPublicName(ProductStatus.OutOfStock) });
    /// </code>
    /// </remarks>
    /// <exception cref="EnumContractException">The declared contract is ambiguous or malformed.</exception>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    [RequiresDynamicCode(TrimmingMessages.DynamicCode)]
    public static string? GetPublicName(Enum value) {
        ArgumentNullException.ThrowIfNull(value);

        EnumContract contract = EnumContract.For(value.GetType());

        return contract.IsContract ? contract.Format(value) : null;
    }

    /// <summary>
    /// The public names a value can also reach under a different casing — the C# names of the
    /// members carrying no <c>[JsonStringEnumMemberName]</c>.
    /// </summary>
    /// <remarks>
    /// Internal, and for the OpenAPI companion: it has to describe the vocabulary the binder accepts,
    /// and half of that vocabulary is case-insensitive while the other half is not. Reading it from
    /// here rather than re-deriving it there is the same reason <see cref="GetPublicNames" /> exists
    /// — the resolution rules live in one place, and a companion that reimplemented them would
    /// describe an API that drifts from the one being served.
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    internal static IReadOnlyList<string> GetNamesMatchedIgnoringCase(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);

        Type underlying = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!underlying.IsEnum) { return []; }

        return EnumContract.For(underlying).UnannotatedMembers;
    }

    /// <summary>
    /// Whether <paramref name="enumType" /> is a contract enum that also carries <c>[Flags]</c>.
    /// </summary>
    /// <remarks>
    /// Not whether it accepts comma-separated combinations: a combination is accepted on every enum,
    /// matching <c>System.Text.Json</c>, which splits before it looks at the attribute. What
    /// <c>[Flags]</c> decides is that the values the application will bind are an open set rather
    /// than the declared members alone — which is why a document can describe one with a pattern,
    /// and why <see cref="GetPublicName" /> has a combination to write.
    /// <para>
    /// Open, not unbounded: a combination decomposing into no declared member is refused off the
    /// request body exactly as an undefined value is on any other enum. Members that are overlapping
    /// composites can produce one — see <c>docs/for-users/limitations.en.md</c>.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    public static bool IsFlagsContract(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);

        Type underlying = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!underlying.IsEnum) { return false; }

        EnumContract contract = EnumContract.For(underlying);

        return contract.IsContract && contract.IsFlags;
    }

}
