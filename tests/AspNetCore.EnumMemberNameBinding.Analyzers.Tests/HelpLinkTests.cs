using Microsoft.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

/// <summary>
/// Every rule advertises a help link, and an IDE will offer it. A link to a page that does not exist
/// is worse than no link at all. The link points at the English page, which is the canonical one;
/// the reader switches language from there.
/// </summary>
public sealed class HelpLinkTests {

    private static readonly DirectoryInfo RepositoryRoot = FindRepositoryRoot();
    private static readonly string[]      Languages      = ["en", "fr"];

    public static TheoryData<string> Descriptors {
        get {
            TheoryData<string> data = new();
            foreach (DiagnosticDescriptor descriptor in new EnumContractAnalyzer().SupportedDiagnostics) {
                data.Add(descriptor.Id);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Descriptors))]
    public void the_help_link_points_at_a_page_that_exists(string id) {
        DiagnosticDescriptor descriptor = new EnumContractAnalyzer().SupportedDiagnostics.Single(d => d.Id == id);

        Assert.False(string.IsNullOrWhiteSpace(descriptor.HelpLinkUri), $"{id} advertises no help link.");
        Assert.EndsWith($"/docs/rules/{id}.en.md", descriptor.HelpLinkUri, StringComparison.Ordinal);

        string page = Path.Combine(RepositoryRoot.FullName, "docs", "rules", id + ".en.md");
        Assert.True(File.Exists(page), $"{id} links to {descriptor.HelpLinkUri}, but {page} does not exist.");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void every_rule_is_documented_and_every_page_documents_a_rule(string language) {
        string[] documented = [.. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.FullName, "docs", "rules"), $"*.{language}.md")
                                            .Select(path => Path.GetFileName(path)[..^$".{language}.md".Length])
                                            .Order()];
        string[] declared = [.. new EnumContractAnalyzer().SupportedDiagnostics.Select(d => d.Id).Order()];

        Assert.Equal(declared, documented);
    }

    /// <summary>A rule page in one language and not the other is a half-finished translation.</summary>
    [Fact]
    public void no_rule_page_exists_in_only_one_language() {
        foreach (string path in Directory.EnumerateFiles(Path.Combine(RepositoryRoot.FullName, "docs", "rules"), "*.md")) {
            string name = Path.GetFileName(path);
            Assert.True(Languages.Any(language => name.EndsWith($".{language}.md", StringComparison.Ordinal)),
                        $"{name} carries no language suffix; rule pages are named EMNxxxx.en.md and EMNxxxx.fr.md.");
        }
    }

    private static DirectoryInfo FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "rules"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

}
