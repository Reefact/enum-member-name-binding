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
