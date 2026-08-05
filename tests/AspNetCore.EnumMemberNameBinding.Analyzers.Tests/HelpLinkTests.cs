using Microsoft.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

/// <summary>
/// Every rule advertises a help link, and an IDE will offer it. A link to a page that does not exist
/// is worse than no link at all.
/// </summary>
public sealed class HelpLinkTests {

    private static readonly DirectoryInfo RepositoryRoot = FindRepositoryRoot();

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
        Assert.EndsWith($"/docs/rules/{id}.md", descriptor.HelpLinkUri, StringComparison.Ordinal);

        string page = Path.Combine(RepositoryRoot.FullName, "docs", "rules", id + ".md");
        Assert.True(File.Exists(page), $"{id} links to {descriptor.HelpLinkUri}, but {page} does not exist.");
    }

    [Fact]
    public void every_rule_is_documented_and_every_page_documents_a_rule() {
        string[] documented = [.. Directory.EnumerateFiles(Path.Combine(RepositoryRoot.FullName, "docs", "rules"), "*.md")
                                            .Select(Path.GetFileNameWithoutExtension)
                                            .OfType<string>()
                                            .Order()];
        string[] declared = [.. new EnumContractAnalyzer().SupportedDiagnostics.Select(d => d.Id).Order()];

        Assert.Equal(declared, documented);
    }

    private static DirectoryInfo FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "rules"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

}
