namespace Reefact.AspNetCore.EnumMemberNameBinding;

/// <summary>
/// Thrown when an enum annotated with <c>[JsonStringEnumMemberName]</c> declares a contract that
/// cannot be applied unambiguously.
/// </summary>
/// <remarks>
/// Raised wherever the contract is first resolved, which for an application registering its enums is
/// start-up: <c>AddEnumMemberNameBinding</c> resolves and validates every one of them before it
/// configures anything.
/// <para>
/// The OpenAPI companion resolves a contract as well, while it writes the document — and under
/// <c>MapOpenApi</c> that is a request. An application using the companion on its own, without
/// <c>AddEnumMemberNameBinding</c>, therefore meets this on <c>/openapi/v1.json</c> rather than at
/// start-up. That configuration is supported and the analyzers do not close it: NuGet does not flow
/// analyzer assets transitively, so a malformed enum declared in a consumer's own assembly survives a
/// clean build. This summary said "never on a request" and was wrong about it.
/// </para>
/// </remarks>
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
    /// fail-fast: it is raised the first time the contract is resolved, and the realistic response to
    /// it is to read the message and fix the enum rather than to handle it.
    /// </remarks>
    public IReadOnlyList<string> Problems { get; }

    private static string BuildMessage(Type enumType, IReadOnlyList<string> problems) {
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(problems);

        string details = string.Join(Environment.NewLine, problems.Select(static p => "  - " + p));

        return $"The enum contract declared on '{enumType.FullName}' is invalid:{Environment.NewLine}{details}";
    }

}
