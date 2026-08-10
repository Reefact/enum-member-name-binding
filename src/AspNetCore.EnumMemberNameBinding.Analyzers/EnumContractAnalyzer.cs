using System.Collections.Immutable;

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
    private const string HelpBase      = "https://github.com/Reefact/enum-member-name-binding/blob/main/docs/for-users/rules/";

    /// <summary>EMN0001 — two members declare the same public name.</summary>
    public static readonly DiagnosticDescriptor DuplicatePublicName = Describe("EMN0001", DiagnosticSeverity.Error);

    /// <summary>EMN0002 — the public name is empty or padded with whitespace.</summary>
    public static readonly DiagnosticDescriptor InvalidPublicName = Describe("EMN0002", DiagnosticSeverity.Error);

    /// <summary>EMN0003 — the enum declares a contract but some members are not annotated.</summary>
    public static readonly DiagnosticDescriptor IncompleteContract = Describe("EMN0003", DiagnosticSeverity.Error);

    /// <summary>EMN0004 — a public name contains a comma.</summary>
    public static readonly DiagnosticDescriptor CommaInName = Describe("EMN0004", DiagnosticSeverity.Error);

    /// <summary>EMN0005 — a public name shadows the C# name of another member.</summary>
    public static readonly DiagnosticDescriptor PublicNameShadowsAnotherMember = Describe("EMN0005", DiagnosticSeverity.Error);

    /// <summary>EMN0006 — the public name cannot travel on every input channel.</summary>
    public static readonly DiagnosticDescriptor NameIsNotPortable = Describe("EMN0006", DiagnosticSeverity.Warning);

    /// <summary>
    /// A descriptor whose title, message, description and help link all derive from the rule id, so
    /// the id is written once and EMN0003 cannot end up carrying EMN0004's wording.
    /// </summary>
    private static DiagnosticDescriptor Describe(string id, DiagnosticSeverity severity) {
        return new DiagnosticDescriptor(
            id,
            GetText(id + "Title"),
            GetText(id + "Message"),
            Category, severity, isEnabledByDefault: true,
            GetText(id + "Description"),
            HelpBase + id + ".en.md");
    }

    private static LocalizableResourceString GetText(string key) {
        return new LocalizableResourceString(key, Resources.Manager, typeof(Resources));
    }

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        DuplicatePublicName, InvalidPublicName, IncompleteContract, CommaInName,
        PublicNameShadowsAnotherMember, NameIsNotPortable);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context) {
        if (context is null) { throw new ArgumentNullException(nameof(context)); }

        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(start => {
            INamedTypeSymbol? attribute = start.Compilation.GetTypeByMetadataName(AttributeName);
            if (attribute is null) { return; }

            start.RegisterSymbolAction(symbol => Analyze(symbol, attribute), SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attributeType) {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Enum) { return; }

        List<Member> members = CollectMembers(type, attributeType);

        if (members.Count == 0 || members.All(m => m.Attribute is null)) { return; }

        Dictionary<string, Member> declared = new(System.StringComparer.Ordinal);

        foreach (Member member in members) {
            if (member.Attribute is null) {
                context.ReportDiagnostic(Diagnostic.Create(IncompleteContract, member.Field.Locations.FirstOrDefault(),
                                                           member.Field.Name, type.Name));
                continue;
            }

            Location location = LocationOf(member.Attribute) ?? member.Field.Locations.FirstOrDefault() ?? Location.None;

            Diagnostic? rejection = Reject(member, declared, location);
            if (rejection is not null) {
                context.ReportDiagnostic(rejection);
                continue;
            }

            declared.Add(member.PublicName!, member);
            ReportAdvice(context, member, members, location);
        }
    }

    /// <summary>The members carrying a constant value, each paired with its attribute if it has one.</summary>
    private static List<Member> CollectMembers(INamedTypeSymbol type, INamedTypeSymbol attributeType) {
        List<Member> members = [];

        foreach (IFieldSymbol field in type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue)) {
            AttributeData? data = field.GetAttributes()
                                       .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
            string? name = data?.ConstructorArguments.Length > 0 ? data.ConstructorArguments[0].Value as string : null;
            members.Add(new Member(field, data, name));
        }

        return members;
    }

    /// <summary>
    /// The diagnostic that disqualifies this member's declared name, or <c>null</c> if the name
    /// stands.
    /// </summary>
    /// <remarks>
    /// The order is the logic and not a preference: each test assumes the ones before it passed — an
    /// empty name has no first character to inspect, and a name that is not yet claimed cannot be a
    /// duplicate. A member earns at most one of these, which is why the caller stops at the first.
    /// </remarks>
    private static Diagnostic? Reject(Member member, Dictionary<string, Member> declared, Location location) {
        string? name  = member.PublicName;
        string  field = member.Field.Name;

        // Spelled out rather than string.IsNullOrEmpty, whose netstandard2.0 declaration predates
        // [NotNullWhen(false)]: the call would leave name possibly-null, and each guard below it
        // would need a null-forgiving operator to compile. EnumContract, on net10.0, has the
        // annotation and calls it directly.
        if (name is null || name.Length == 0) { return At(InvalidPublicName, location, field, name, "is empty"); }
        if (IsPadded(name)) { return At(InvalidPublicName, location, field, name, "has leading or trailing whitespace"); }
        if (name.IndexOf(',') >= 0) { return At(CommaInName, location, field, name); }
        if (declared.TryGetValue(name, out Member owner)) { return At(DuplicatePublicName, location, owner.Field.Name, field, name); }

        return null;
    }

    private static bool IsPadded(string name) {
        return char.IsWhiteSpace(name[0]) || char.IsWhiteSpace(name[name.Length - 1]);
    }

    private static Diagnostic At(DiagnosticDescriptor rule, Location location, params object?[] arguments) {
        return Diagnostic.Create(rule, location, arguments);
    }

    /// <summary>
    /// The findings a valid name can still earn. Unlike <see cref="Reject" /> these do not disqualify
    /// anything and do not exclude each other, so both are reported when both apply.
    /// </summary>
    private static void ReportAdvice(SymbolAnalysisContext context, Member member, List<Member> members, Location location) {
        string name = member.PublicName!;

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

    /// <summary>
    /// The characters a public name cannot contain, established by sending each of them over every
    /// channel rather than from the specifications: a slash is refused inside a route segment, and a
    /// line break or a non-ASCII character is refused in a header. Other control characters travelled
    /// intact in that measurement, but RFC 9110 forbids them in a field value, so they are reported
    /// too — on the standard rather than on the observation.
    /// <para>
    /// A tab is the exception, and is deliberately not reported. RFC 9110 admits it alongside a
    /// space — <c>field-content = field-vchar [ 1*( SP / HTAB / field-vchar ) field-vchar ]</c> — so
    /// the standard that rules the other control characters out is the same one that lets this one
    /// through, and the measurement agrees. That grammar only admits it between two visible
    /// characters, and a name that begins or ends with one is already rejected by EMN0002.
    /// </para>
    /// </summary>
    private static (string Description, string Channel)? FindUnportableCharacter(string name) {
        foreach (char character in name) {
            if (character == '/') { return ("a slash", "a route segment"); }
            if (character is '\r' or '\n') { return ("a line break", "a header"); }
            if (character > '\u007e') { return ("a character outside printable ASCII", "a header"); }
            if (character < '\u0020' && character != '\u0009') { return ("a control character", "a header"); }
        }

        return null;
    }

    private static Location? LocationOf(AttributeData attribute) {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax) { return null; }

        return syntax.ArgumentList?.Arguments.FirstOrDefault()?.GetLocation() ?? syntax.GetLocation();
    }

    private sealed class Member(IFieldSymbol field, AttributeData? attribute, string? publicName) {

        public IFieldSymbol Field { get; } = field;

        public AttributeData? Attribute { get; } = attribute;

        public string? PublicName { get; } = publicName;

    }

}
