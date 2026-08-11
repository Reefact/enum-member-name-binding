using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// The documentation shows the pattern the transformer emits. It drifted once already, so it is
/// checked against what the code actually produces rather than trusted — in every language, since a
/// translated page is one more copy that can go stale.
/// </summary>
[Collection(nameof(OpenApiCollection))]
public sealed partial class ReadmeIsAccurateTests(OpenApiTestApi api) {

    [Theory]
    [InlineData("openapi.en.md")]
    [InlineData("openapi.fr.md")]
    public void the_documented_flags_pattern_is_the_one_the_transformer_emits(string page) {
        // Scopes declares read, write and delete — the same three names the documentation illustrates.
        string produced = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;
        string source   = File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, "docs", "for-users", page));

        Match documented = DocumentedPattern().Match(source);
        Check.WithCustomMessage($"docs/for-users/{page} no longer shows a pattern; either restore it or drop this test.")
             .That(documented.Success).IsTrue();

        // The page carries the pattern inside a fenced JSON snippet, so the backslashes are doubled
        // as JSON requires and nothing else is escaped. This said "in a markdown table, so ... pipes
        // are escaped for the table"; neither page has ever been a table, the pipes of
        // (read|write|delete) are bare, and the \| replacement that sentence justified could not
        // fire on any input the regex above admits.
        string unescaped = documented.Groups["pattern"].Value.Replace(@"\\", @"\", StringComparison.Ordinal);

        Check.That(unescaped).IsEqualTo(produced);
    }

    [GeneratedRegex(@"""pattern"":""(?<pattern>[^""]+)""")]
    private static partial Regex DocumentedPattern();

    private static DirectoryInfo FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

}
