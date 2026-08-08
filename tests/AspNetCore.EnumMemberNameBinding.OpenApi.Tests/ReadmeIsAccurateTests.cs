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
        string source   = File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, "docs", page));

        Match documented = DocumentedPattern().Match(source);
        Assert.True(documented.Success, $"docs/{page} no longer shows a pattern; either restore it or drop this test.");

        // The page carries the pattern inside a JSON snippet in a markdown table, so backslashes
        // are doubled and pipes are escaped for the table.
        string unescaped = documented.Groups["pattern"].Value.Replace(@"\\", @"\", StringComparison.Ordinal)
                                                             .Replace(@"\|", "|", StringComparison.Ordinal);

        Assert.Equal(produced, unescaped);
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
