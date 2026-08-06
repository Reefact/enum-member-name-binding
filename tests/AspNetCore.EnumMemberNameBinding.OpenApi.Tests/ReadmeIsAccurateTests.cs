using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// The README shows the pattern the transformer emits. It drifted once already, so it is checked
/// against what the code actually produces rather than trusted.
/// </summary>
[Collection(nameof(OpenApiCollection))]
public sealed class ReadmeIsAccurateTests(OpenApiTestApi api) {

    [Fact]
    public void the_documented_flags_pattern_is_the_one_the_transformer_emits() {
        // Scopes declares read, write and delete — the same three names the README illustrates.
        string produced = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;
        string readme = File.ReadAllText(Path.Combine(FindRepositoryRoot().FullName, "README.md"));

        Match documented = Regex.Match(readme, @"""pattern"":""(?<pattern>[^""]+)""");
        Assert.True(documented.Success, "the README no longer shows a pattern; either restore it or drop this test.");

        // The README carries the pattern inside a JSON snippet in a markdown table, so backslashes
        // are doubled and pipes are escaped for the table.
        string unescaped = documented.Groups["pattern"].Value.Replace(@"\\", @"\", StringComparison.Ordinal)
                                                             .Replace(@"\|", "|", StringComparison.Ordinal);

        Assert.Equal(produced, unescaped);
    }

    private static DirectoryInfo FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

}
