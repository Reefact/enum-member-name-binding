using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AspNetCore.EnumMemberNameBinding.OpenApi;

/// <summary>
/// Rewrites the schema of every contract enum so the document describes what the server accepts.
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
public sealed class EnumMemberNameSchemaTransformer : IOpenApiSchemaTransformer {

    /// <summary>Creates the transformer.</summary>
    [RequiresUnreferencedCode(TrimmingMessages.Reflection)]
    public EnumMemberNameSchemaTransformer() { }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The constructor carries the requirement; an instance cannot exist without it.")]
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        Type type = context.JsonTypeInfo.Type;
        IReadOnlyList<string>? names = EnumMemberNames.GetPublicNames(type);
        if (names is null || names.Count == 0) { return Task.CompletedTask; }

        schema.Type = JsonSchemaType.String;

        if (EnumMemberNames.IsFlagsContract(type)) {
            // A combination is an open set, so it cannot be enumerated. A pattern describes it exactly.
            schema.Enum = null;
            schema.Pattern = BuildFlagsPattern(names);
            schema.Description = Append(schema.Description,
                                        $"One or more of: {string.Join(", ", names)}. Combine several with a comma, for example \"{string.Join(", ", names.Take(2))}\".");

            return Task.CompletedTask;
        }

        schema.Enum = [.. names.Select(static name => (JsonNode)JsonValue.Create(name))];

        return Task.CompletedTask;
    }

    private static string BuildFlagsPattern(IReadOnlyList<string> names) {
        string alternatives = string.Join('|', names.Select(EscapeForJsonSchema));

        // Surrounding whitespace and a single trailing comma are accepted by the binder, because
        // System.Text.Json accepts them. A pattern that excluded them would advertise a contract
        // stricter than the one the server honours.
        return $"^\\s*({alternatives})(\\s*,\\s*({alternatives}))*\\s*,?\\s*$";
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
        const string SyntaxCharacters = @"^$\.*+?()[]{}|/";

        StringBuilder escaped = new(name.Length + 8);
        foreach (char character in name) {
            if (SyntaxCharacters.Contains(character, StringComparison.Ordinal)) { escaped.Append('\\'); }

            escaped.Append(character);
        }

        return escaped.ToString();
    }

    private static string Append(string? description, string addition) {
        return string.IsNullOrWhiteSpace(description) ? addition : description.TrimEnd() + " " + addition;
    }

}
