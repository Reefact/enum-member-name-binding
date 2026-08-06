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
    /// Use this when generating links. ASP.NET Core formats route values without consulting
    /// <see cref="System.ComponentModel.TypeDescriptor" />, so a link built from the enum value
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
    public static string? GetPublicName(Enum value) {
        ArgumentNullException.ThrowIfNull(value);

        EnumContract contract = EnumContract.For(value.GetType());

        return contract.IsContract ? contract.Format(value) : null;
    }

    /// <summary>
    /// Whether <paramref name="enumType" /> is a contract enum that also carries <c>[Flags]</c>,
    /// and therefore accepts comma-separated combinations.
    /// </summary>
    public static bool IsFlagsContract(Type enumType) {
        ArgumentNullException.ThrowIfNull(enumType);

        Type underlying = Nullable.GetUnderlyingType(enumType) ?? enumType;
        if (!underlying.IsEnum) { return false; }

        EnumContract contract = EnumContract.For(underlying);

        return contract.IsContract && contract.IsFlags;
    }

}
