using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AspNetCore.EnumMemberNameBinding.Analyzers;

/// <summary>
/// Reports enum contracts that cannot be applied unambiguously, at compile time rather than at
/// application start-up.
/// </summary>
/// <remarks>
/// Only enums that declare a contract — at least one member carrying
/// <c>[JsonStringEnumMemberName]</c> — are analysed. Every other enum is ignored entirely.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumContractAnalyzer : DiagnosticAnalyzer {

    private const string Category      = "ApiContract";
    private const string AttributeName = "System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute";
    private const string FlagsName     = "System.FlagsAttribute";
    private const string HelpBase      = "https://github.com/Reefact/enum-member-name-binding/blob/main/docs/rules/";

    /// <summary>EMN0001 — two members declare the same public name.</summary>
    public static readonly DiagnosticDescriptor DuplicatePublicName = new(
        "EMN0001",
        "Two enum members declare the same public name",
        "Members '{0}' and '{1}' both declare the public name '{2}'; a public name must identify exactly one member",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        "Two members declaring the same public name make the value impossible to resolve unambiguously.",
        HelpBase + "EMN0001.md");

    /// <summary>EMN0002 — the public name is empty or padded with whitespace.</summary>
    public static readonly DiagnosticDescriptor InvalidPublicName = new(
        "EMN0002",
        "The public name is not usable",
        "Member '{0}' declares the public name '{1}', which {2}",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        "An empty name, or a name padded with whitespace, cannot be sent reliably over HTTP.",
        HelpBase + "EMN0002.md");

    /// <summary>EMN0003 — the enum declares a contract but some members are not annotated.</summary>
    public static readonly DiagnosticDescriptor IncompleteContract = new(
        "EMN0003",
        "The enum contract is incomplete",
        "Member '{0}' of '{1}' declares no public name, so its C# name '{0}' becomes part of the public API contract",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        "Once an enum declares a contract, every member must declare its public name. A member left "
      + "unannotated answers to its C# name, which puts an internal identifier in the public contract "
      + "and makes renaming it a breaking change — exactly what declaring a contract is meant to prevent.",
        HelpBase + "EMN0003.md");

    /// <summary>EMN0004 — a [Flags] public name contains a comma.</summary>
    public static readonly DiagnosticDescriptor CommaInFlagsName = new(
        "EMN0004",
        "A [Flags] public name contains a comma",
        "Member '{0}' declares the public name '{1}'; a comma separates values in a [Flags] enum and cannot appear inside a name",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        "A comma is the separator for combined values, so a name containing one cannot be parsed back.",
        HelpBase + "EMN0004.md");

    /// <summary>EMN0005 — a public name shadows the C# name of another member.</summary>
    public static readonly DiagnosticDescriptor PublicNameShadowsAnotherMember = new(
        "EMN0005",
        "A public name shadows the C# name of another member",
        "Member '{0}' declares the public name '{1}', which is also the C# name of member '{2}'; "
      + "the value '{1}' resolves to '{0}', so '{2}' is only reachable through a different casing",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true,
        "A declared public name is matched before an unannotated member's C# name, and case-sensitively. "
      + "The shadowed member therefore answers to every casing of its name except its own, which no "
      + "reader of the enum can guess. This matters most when EMN0003 has been turned off to allow "
      + "partial contracts, where it is the only remaining protection.",
        HelpBase + "EMN0005.md");

    /// <summary>EMN0006 — the public name cannot travel on every input channel.</summary>
    public static readonly DiagnosticDescriptor NameIsNotPortable = new(
        "EMN0006",
        "The public name cannot travel on every input channel",
        "Member '{0}' declares the public name '{1}', which contains {2} and is refused on {3}",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        "The promise of one contract on every channel only holds for names every channel can carry. "
      + "A slash is refused inside a route segment, and a line break or a character outside printable "
      + "ASCII is refused in a header. Reported as a warning rather than an error because the failure "
      + "depends on the channels an API actually binds from: a name refused only in a header is "
      + "harmless in an API that never binds one.",
        HelpBase + "EMN0006.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DuplicatePublicName, InvalidPublicName, IncompleteContract, CommaInFlagsName,
        PublicNameShadowsAnotherMember, NameIsNotPortable);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context) {
        if (context is null) { return; }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(start => {
            INamedTypeSymbol? attribute = start.Compilation.GetTypeByMetadataName(AttributeName);
            if (attribute is null) { return; }

            INamedTypeSymbol? flags = start.Compilation.GetTypeByMetadataName(FlagsName);
            start.RegisterSymbolAction(symbol => Analyze(symbol, attribute, flags), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attributeType, INamedTypeSymbol? flagsType) {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Enum) { return; }

        List<Member> members = [];
        foreach (IFieldSymbol field in type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue)) {
            AttributeData? data = field.GetAttributes()
                                       .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
            string? name = data?.ConstructorArguments.Length > 0 ? data.ConstructorArguments[0].Value as string : null;
            members.Add(new Member(field, data, name));
        }

        if (members.Count == 0 || members.All(m => m.Attribute is null)) { return; }

        bool isFlags = flagsType is not null
                    && type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, flagsType));

        Dictionary<string, Member> declared = new(System.StringComparer.Ordinal);

        foreach (Member member in members) {
            if (member.Attribute is null) {
                context.ReportDiagnostic(Diagnostic.Create(IncompleteContract, member.Field.Locations.FirstOrDefault(),
                                                           member.Field.Name, type.Name));
                continue;
            }

            Location location = LocationOf(member.Attribute) ?? member.Field.Locations.FirstOrDefault() ?? Location.None;
            string? name = member.PublicName;

            if (string.IsNullOrEmpty(name)) {
                context.ReportDiagnostic(Diagnostic.Create(InvalidPublicName, location, member.Field.Name, name, "is empty"));
                continue;
            }

            if (char.IsWhiteSpace(name![0]) || char.IsWhiteSpace(name[name.Length - 1])) {
                context.ReportDiagnostic(Diagnostic.Create(InvalidPublicName, location, member.Field.Name, name,
                                                           "has leading or trailing whitespace"));
                continue;
            }

            if (isFlags && name.IndexOf(',') >= 0) {
                context.ReportDiagnostic(Diagnostic.Create(CommaInFlagsName, location, member.Field.Name, name));
                continue;
            }

            if (declared.TryGetValue(name, out Member existing)) {
                context.ReportDiagnostic(Diagnostic.Create(DuplicatePublicName, location, existing.Field.Name, member.Field.Name, name));
                continue;
            }

            declared.Add(name, member);

            if (FindUnportableCharacter(name) is var (description, channel)) {
                context.ReportDiagnostic(Diagnostic.Create(NameIsNotPortable, location, member.Field.Name, name, description, channel));
            }

            // Case-insensitive, because that is how the runtime looks up an unannotated member's C#
            // name. An ordinal comparison here would let [JsonStringEnumMemberName("blue")] Red sit
            // next to a Blue member unreported, which is the very shape this rule exists to catch.
            Member? shadowed = members.FirstOrDefault(m => m.Attribute is null
                                                        && string.Equals(m.Field.Name, name, System.StringComparison.OrdinalIgnoreCase));
            if (shadowed is not null) {
                context.ReportDiagnostic(Diagnostic.Create(PublicNameShadowsAnotherMember, location,
                                                           member.Field.Name, name, shadowed.Field.Name));
            }
        }
    }

    /// <summary>
    /// The characters a public name cannot contain, established by sending each of them over every
    /// channel rather than from the specifications: a slash is refused inside a route segment, and a
    /// line break or a non-ASCII character is refused in a header. Other control characters travelled
    /// intact in that measurement, but RFC 9110 forbids them in a field value, so they are reported
    /// too — on the standard rather than on the observation.
    /// </summary>
    private static (string Description, string Channel)? FindUnportableCharacter(string name) {
        foreach (char character in name) {
            if (character == '/') { return ("a slash", "a route segment"); }
            if (character is '\r' or '\n') { return ("a line break", "a header"); }
            if (character > '\u007e') { return ("a character outside printable ASCII", "a header"); }
            if (character < '\u0020') { return ("a control character", "a header"); }
        }

        return null;
    }

    private static Location? LocationOf(AttributeData attribute) {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax) {
            return syntax.ArgumentList?.Arguments.FirstOrDefault()?.GetLocation() ?? syntax.GetLocation();
        }

        return null;
    }

    private sealed class Member(IFieldSymbol field, AttributeData? attribute, string? publicName) {

        public IFieldSymbol Field { get; } = field;

        public AttributeData? Attribute { get; } = attribute;

        public string? PublicName { get; } = publicName;

    }

}
