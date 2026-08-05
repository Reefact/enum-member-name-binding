namespace AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Thrown when an enum annotated with <c>[JsonStringEnumMemberName]</c> declares a contract that
/// cannot be applied unambiguously. Raised at startup, never on a request.
/// </summary>
public sealed class EnumContractException : InvalidOperationException {

    internal EnumContractException(Type enumType, IReadOnlyList<string> problems)
        : base(BuildMessage(enumType, problems)) {
        EnumType = enumType;
        Problems = problems;
    }

    /// <summary>The enum type whose contract is invalid.</summary>
    public Type EnumType { get; }

    /// <summary>One entry per detected problem.</summary>
    public IReadOnlyList<string> Problems { get; }

    private static string BuildMessage(Type enumType, IReadOnlyList<string> problems) {
        string details = string.Join(Environment.NewLine, problems.Select(static p => "  - " + p));

        return $"The enum contract declared on '{enumType.FullName}' is invalid:{Environment.NewLine}{details}";
    }

}
