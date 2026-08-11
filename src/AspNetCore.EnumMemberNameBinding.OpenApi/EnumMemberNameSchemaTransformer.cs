using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using DiagnosticCatalog.Trimming;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AspNetCore.EnumMemberNameBinding.OpenApi;

/// <summary>
/// Rewrites the schema of every contract enum this application registered, so the document describes
/// what the server accepts.
/// </summary>
/// <remarks>
/// Three things are corrected:
/// <list type="bullet">
///   <item>the schema is explicitly typed as a string — ASP.NET Core emits the enum values without a type;</item>
///   <item>the values are the declared public names, whichever names the platform chose to emit;</item>
///   <item>a <c>[Flags]</c> enum, for which ASP.NET Core deliberately emits no value at all, gets a
///         pattern and a description covering comma-separated combinations.</item>
/// </list>
/// </remarks>
/// <remarks>
/// Internal, deliberately. <c>AddEnumMemberNames()</c>
/// registers it through the instance overload of <c>AddSchemaTransformer</c>, which takes an
/// <see cref="IOpenApiSchemaTransformer" /> and is indifferent to the concrete type's
/// accessibility, so nothing requires it to be public. Publishing it would, on the other hand, bake
/// <see cref="OpenApiSchema" /> — a concrete class belonging to Microsoft.OpenApi, which reshaped
/// across 1.x to 2.x and has since introduced an <c>IOpenApiSchema</c> interface — into this
/// package's own versioning promise, handing a third party the power to force a major version here.
/// The package's entire public surface is <c>AddEnumMemberNames()</c>.
/// </remarks>
internal sealed class EnumMemberNameSchemaTransformer : IOpenApiSchemaTransformer {

    /// <summary>Creates the transformer.</summary>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    public EnumMemberNameSchemaTransformer() { }

    /// <inheritdoc />
    [UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = SuppressionJustification.IL2026.RequirementCarriedByConstructor)]
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        Type type = context.JsonTypeInfo.Type;
        if (!IsDescribable(context, type)) { return Task.CompletedTask; }

        IReadOnlyList<string>? names = EnumMemberNames.GetPublicNames(type);
        if (names is null || names.Count == 0) { return Task.CompletedTask; }

        bool nullable = AdmitsNull(schema);

        schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;

        if (EnumMemberNames.IsFlagsContract(type)) {
            // A combination is an open set, so it cannot be enumerated. A pattern describes it exactly.
            // A pattern says nothing about a value that is not a string, so the null the type admits
            // stays admitted.
            schema.Enum = null;
            schema.Pattern = BuildFlagsPattern(names, EnumMemberNames.GetNamesMatchedIgnoringCase(type));
            schema.Description = Append(schema.Description, FlagsCombination(names));

            return Task.CompletedTask;
        }

        List<JsonNode> values = [.. names.Select(static name => (JsonNode)JsonValue.Create(name))];

        // A JSON null is a null element here — the annotation does not admit one and the list holds
        // it anyway, which is exactly how the platform put it there before this replaced the list.
        if (nullable) { values.Add(null!); }

        schema.Enum = values;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether the schema ASP.NET Core built is one a <see langword="null" /> is valid against.
    /// </summary>
    /// <remarks>
    /// Read off what is being replaced, because the position that made it nullable is not visible from
    /// here — the same component describes the enum wherever it appears. A nullable property is
    /// emitted as <c>oneOf</c> of a null schema and a reference, so the component itself is untouched
    /// and there is nothing to preserve. A nullable element of a collection is not wrapped, and the
    /// platform expresses it by putting a null in the component's own <c>enum</c> instead.
    /// <para>
    /// That null was dropped along with the rest when the list was replaced, and the type stamped over
    /// it said <c>string</c>, so the document forbade a value the server accepts and echoes back —
    /// measured on <c>List&lt;TEnum?&gt;</c>, which answers 200 to <c>["available",null,"sold"]</c>.
    /// </para>
    /// </remarks>
    private static bool AdmitsNull(OpenApiSchema schema) {
        if (schema.Type is not null && (schema.Type.Value & JsonSchemaType.Null) != 0) { return true; }

        return schema.Enum is not null && schema.Enum.Any(static value => value is null || value.GetValueKind() == JsonValueKind.Null);
    }

    /// <summary>
    /// Whether this application asked for <paramref name="type" /> to be bound by its declared names.
    /// </summary>
    /// <remarks>
    /// Carrying <c>[JsonStringEnumMemberName]</c> is not the same as being covered, and treating the
    /// two as one is how a document comes to promise what the server refuses: an annotated enum that
    /// nobody registered binds by its C# names and serializes as a number, so describing it as a
    /// string with its declared names is wrong twice over, and a generated client sends requests that
    /// answer 400.
    /// <para>
    /// A missing record is not an empty one. This package is usable on its own — a minimal API that
    /// registers its own <c>JsonStringEnumConverter&lt;T&gt;</c> and never calls
    /// <c>AddEnumMemberNameBinding</c> has no record to consult, and its document is still worth
    /// correcting. What the record rules out is the case where one exists and this type is not in it,
    /// which is the only case where the two can be known to disagree.
    /// </para>
    /// </remarks>
    private static bool IsDescribable(OpenApiSchemaTransformerContext context, Type type) {
        EnumMemberNameBindingRegistrations? registered = context.ApplicationServices.GetService<EnumMemberNameBindingRegistrations>();
        if (registered is null) { return true; }

        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        return registered.Contains(underlying);
    }

    /// <summary>
    /// Describes the combinations a <c>[Flags]</c> enum accepts, which the pattern states exactly
    /// but unreadably. The one text in this solution whose reader is not a developer: it travels in
    /// the document, into whatever renders it.
    /// </summary>
    private static string FlagsCombination(IReadOnlyList<string> names) {
        return $"One or more of: {string.Join(", ", names)}. "
             + $"Combine several with a comma, for example \"{string.Join(", ", names.Take(2))}\".";
    }

    /// <summary>
    /// The whitespace the binder trims, written out rather than as <c>\s</c>.
    /// </summary>
    /// <remarks>
    /// <c>\s</c> is not that set. A JSON Schema pattern is read as ECMA-262, where <c>\s</c> is
    /// WhiteSpace plus LineTerminator — which includes U+FEFF, and excludes U+0085. The binder trims
    /// with <c>String.Trim</c>, which is <see cref="char.IsWhiteSpace(char)" />, and that is the other way
    /// round on both. So the document was wrong in both directions at once: it advertised a value
    /// opening on U+FEFF, which the server answers 400 to, and excluded one opening on U+0085,
    /// which the server binds.
    /// <para>
    /// Written as the twenty-five code points <see cref="char.IsWhiteSpace(char)" /> admits, which is
    /// a closed set — <c>the_pattern_admits_exactly_the_whitespace_the_binder_trims</c> enumerates
    /// every <c>char</c> against both. That test reads the pattern with .NET's own engine, which is
    /// sound once the class is explicit code points: no dialect reads those differently, where
    /// <c>\s</c> is precisely the thing they disagree about. It is also why the divergence was
    /// invisible here — the suite evaluated the pattern with <c>System.Text.RegularExpressions</c>,
    /// whose <c>\s</c> happens to agree with <c>Trim</c> on both, so only a consumer outside .NET
    /// could ever have seen it.
    /// </para>
    /// </remarks>
    private const string Whitespace = @"[\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]";

    private static string BuildFlagsPattern(IReadOnlyList<string> names, IReadOnlyList<string> ignoringCase) {
        string alternatives = string.Join('|', names.Select(name => Alternative(name, ignoringCase)));

        // Surrounding whitespace and a single trailing comma are accepted by the binder, because
        // System.Text.Json accepts them. A pattern that excluded them would advertise a contract
        // stricter than the one the server honours.
        return $"^{Whitespace}*({alternatives})({Whitespace}*,{Whitespace}*({alternatives}))*{Whitespace}*,?{Whitespace}*$";
    }

    /// <summary>
    /// One name as the pattern must read it: literally, or under any casing.
    /// </summary>
    /// <remarks>
    /// The two halves of the vocabulary are not matched the same way, and writing them the same way
    /// is what made the document refuse values the server binds. A declared name is matched
    /// ordinally, so it belongs in the pattern as written. A member left unannotated keeps its C#
    /// name, which is matched ignoring case — so <c>Delete</c> alone advertised a name of which the
    /// server also accepts <c>delete</c> and <c>DELETE</c>.
    /// </remarks>
    private static string Alternative(string name, IReadOnlyList<string> ignoringCase) {
        return ignoringCase.Contains(name, StringComparer.Ordinal) ? EscapeIgnoringCase(name) : EscapeForJsonSchema(name);
    }

    /// <summary>
    /// A name written so that every casing of it matches, which ECMA-262 has no flag for inside a
    /// pattern: each character becomes the class of every character the binder treats as equal to it.
    /// </summary>
    /// <remarks>
    /// The two forms of the character are not that class, and writing them as though they were got it
    /// wrong in both directions at once. Too wide on five code points: <c>ToLowerInvariant</c> maps
    /// U+212A KELVIN SIGN to <c>k</c>, so a member named with it advertised a plain <c>k</c> that
    /// <see cref="StringComparer.OrdinalIgnoreCase" /> refuses and the server answers 400 to — and the
    /// same for U+03F4, U+1E9E, U+2126 and U+212B. Too narrow on seventy-nine others, where two
    /// characters are equal without either being the other's case: U+00B5 MICRO SIGN and U+03BC GREEK
    /// SMALL MU, or the title-case <c>Ǆǅǆ</c> family.
    /// <para>
    /// Both fall out of one rule, measured over every <see cref="char" /> rather than reasoned about:
    /// two characters are equal under <c>OrdinalIgnoreCase</c> exactly when
    /// <see cref="char.ToUpperInvariant(char)" /> sends them to the same place. Grouping by that is
    /// therefore the class itself, and <c>the_pattern_admits_exactly_what_the_binder_accepts</c>
    /// holds the two against each other code point by code point.
    /// </para>
    /// <para>
    /// A character alone in its group is left outside a class deliberately, and not written as
    /// <c>[--]</c>: a name may contain a hyphen, and a hyphen inside a class is a range. That is also
    /// what keeps a class safe to write unescaped — the four characters a class would need escaped,
    /// <c>\</c>, <c>]</c>, <c>^</c> and <c>-</c>, are each alone in their group, which a test asserts
    /// rather than the reader taking on trust.
    /// </para>
    /// </remarks>
    private static string EscapeIgnoringCase(string name) {
        StringBuilder escaped = new(name.Length * 4);

        foreach (char character in name) {
            if (AnyCasing.Value.TryGetValue(character, out string? group)) {
                escaped.Append(group);
                continue;
            }

            Escape(escaped, character);
        }

        return escaped.ToString();
    }

    /// <summary>
    /// Every character that shares its group, mapped to the class naming the whole group. A character
    /// alone in its group is absent, and is written as a literal instead.
    /// </summary>
    /// <remarks>
    /// Built once, on the first <c>[Flags]</c> contract that has a member left unannotated — an
    /// application with none never pays for it. The pass is over all sixty-five thousand code points
    /// because the class of a character is its <em>preimage</em> under
    /// <see cref="char.ToUpperInvariant(char)" />, which cannot be read off the character alone.
    /// Ascending order inside each group is what makes the emitted pattern the same on every run.
    /// </remarks>
    internal static readonly Lazy<FrozenDictionary<char, string>> AnyCasing = new(BuildAnyCasing);

    private static FrozenDictionary<char, string> BuildAnyCasing() {
        Dictionary<char, List<char>> byUpper = [];

        for (int code = 0; code <= char.MaxValue; code++) {
            char character = (char)code;

            if (!byUpper.TryGetValue(char.ToUpperInvariant(character), out List<char>? group)) {
                byUpper[char.ToUpperInvariant(character)] = group = [];
            }

            group.Add(character);
        }

        Dictionary<char, string> classes = [];

        foreach (List<char> group in byUpper.Values) {
            if (group.Count == 1) { continue; }

            string written = "[" + new string([.. group]) + "]";
            foreach (char member in group) { classes[member] = written; }
        }

        return classes.ToFrozenDictionary();
    }

    /// <summary>
    /// Escapes for the regular expression dialect a JSON Schema <c>pattern</c> is read with, ECMA-262.
    /// </summary>
    /// <remarks>
    /// <see cref="Regex.Escape" /> is not usable here. It escapes whitespace and <c>#</c> — producing
    /// <c>\ </c> and <c>\#</c> — because of .NET's <c>IgnorePatternWhitespace</c> mode. Neither is a
    /// valid identity escape in ECMA-262, so a strict consumer such as a JavaScript engine in unicode
    /// mode rejects the whole pattern. Only the syntax characters are escaped; everything else,
    /// spaces included, is a literal in both dialects.
    /// </remarks>
    private static string EscapeForJsonSchema(string name) {
        StringBuilder escaped = new(name.Length + 8);
        foreach (char character in name) { Escape(escaped, character); }

        return escaped.ToString();
    }

    private static void Escape(StringBuilder escaped, char character) {
        const string SyntaxCharacters = @"^$\.*+?()[]{}|/";

        if (SyntaxCharacters.Contains(character, StringComparison.Ordinal)) { escaped.Append('\\'); }

        escaped.Append(character);
    }

    private static string Append(string? description, string addition) {
        return string.IsNullOrWhiteSpace(description) ? addition : description.TrimEnd() + " " + addition;
    }

}
