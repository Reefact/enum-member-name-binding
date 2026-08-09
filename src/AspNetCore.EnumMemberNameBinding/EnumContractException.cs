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
    /// <remarks>
    /// Human-readable prose, deliberately, and not a machine-readable surface: the wording may change
    /// in any release, and nothing here should be parsed or branched on. Code that needs to reason
    /// about a specific defect has the analyzer diagnostics — EMN0001 to EMN0006 — which carry stable
    /// identifiers and fire at build time, where the mistake is cheaper to see. This exception is
    /// fail-fast: it is raised while the application starts, and the realistic response to it is to
    /// read the message and fix the enum.
    /// </remarks>
    public IReadOnlyList<string> Problems { get; }

    private static string BuildMessage(Type enumType, IReadOnlyList<string> problems) {
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(problems);

        string details = string.Join(Environment.NewLine, problems.Select(static p => "  - " + p));

        return $"The enum contract declared on '{enumType.FullName}' is invalid:{Environment.NewLine}{details}";
    }

}
