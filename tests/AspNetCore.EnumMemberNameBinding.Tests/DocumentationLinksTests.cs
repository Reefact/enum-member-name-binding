using System.Text;
using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// The README is also the NuGet package page, where a relative link is dead: it must point at
/// github.com. The pages under <c>docs</c> are only ever read on GitHub, so they link relatively.
/// Both forms have to resolve — a documentation split is exactly where links rot.
/// </summary>
public sealed class DocumentationLinksTests {

    private const string BlobPrefix = "https://github.com/Reefact/enum-member-name-binding/blob/main/";

    private static readonly DirectoryInfo RepositoryRoot = FindRepositoryRoot();
    private static readonly Regex        Link           = new(@"\[[^\]]*\]\((?<target>[^)\s]+)\)", RegexOptions.Compiled);
    private static readonly Regex        Heading        = new(@"^(?<level>#{1,6})\s+(?<text>.+)$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex        Fence          = new(@"^```(?<tag>\w*)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>The front page is the one pair that does not follow the file-suffix convention.</summary>
    private const string EnglishFrontPage = "README.md";
    private const string FrenchFrontPage  = "docs/README.fr.md";

    public static TheoryData<string> Pages {
        get {
            TheoryData<string> data = new();
            foreach (string page in MarkdownPages()) {
                data.Add(page);
            }

            return data;
        }
    }

    public static TheoryData<string, string> TranslationPairs {
        get {
            TheoryData<string, string> data = new();
            // A pair is discovered from both sides, so that an orphan in either language is caught.
            foreach ((string english, string french) in Pairs().Distinct()) {
                data.Add(english, french);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public void every_link_to_a_file_of_this_repository_resolves(string page) {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot.FullName, page));

        foreach (Match match in Link.Matches(source)) {
            string target = match.Groups["target"].Value;
            if (target.StartsWith(BlobPrefix, StringComparison.Ordinal)) {
                target = target[BlobPrefix.Length..];
            } else if (target.StartsWith("http", StringComparison.Ordinal) || target.StartsWith('#')) {
                continue;
            }

            string[] parts    = target.Split('#', 2);
            string   relative = Uri.UnescapeDataString(parts[0]);
            string   resolved = Path.GetFullPath(Path.Combine(RepositoryRoot.FullName, Path.GetDirectoryName(page) ?? string.Empty, relative));

            Assert.True(File.Exists(resolved) || Directory.Exists(resolved), $"{page} links to '{target}', which does not exist.");

            if (parts.Length == 2 && File.Exists(resolved)) {
                Assert.Contains(Uri.UnescapeDataString(parts[1]), AnchorsOf(resolved), StringComparer.Ordinal);
            }
        }
    }

    /// <summary>
    /// A relative link works on GitHub and is dead on nuget.org, which renders the same file. The
    /// mistake is invisible until someone clicks it from the package page.
    /// </summary>
    [Fact]
    public void the_readme_links_to_this_repository_absolutely() {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot.FullName, EnglishFrontPage));

        foreach (Match match in Link.Matches(source)) {
            string target = match.Groups["target"].Value;
            if (target.StartsWith("http", StringComparison.Ordinal) || target.StartsWith('#')) { continue; }

            Assert.Fail($"{EnglishFrontPage} links to '{target}' relatively; the NuGet package page needs {BlobPrefix}…");
        }
    }

    /// <summary>Every page is bilingual, so a page added in one language only is a build failure.</summary>
    [Theory]
    [MemberData(nameof(TranslationPairs))]
    public void a_page_exists_in_both_languages(string english, string french) {
        Assert.True(File.Exists(Path.Combine(RepositoryRoot.FullName, english)), $"{french} has no English counterpart at {english}.");
        Assert.True(File.Exists(Path.Combine(RepositoryRoot.FullName, french)), $"{english} has no French counterpart at {french}.");
    }

    /// <summary>The reader must be able to switch language from wherever they landed.</summary>
    [Theory]
    [MemberData(nameof(TranslationPairs))]
    public void each_page_offers_the_other_language(string english, string french) {
        AssertSwitchesTo(english, Path.GetFileName(french));
        AssertSwitchesTo(french, Path.GetFileName(english));
    }

    /// <summary>
    /// Prose is translated, code is not restructured. Comments and string literals inside a snippet
    /// may well be translated, so the contents are not compared — but a snippet dropped or added on
    /// one side means the two pages no longer describe the same thing.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslationPairs))]
    public void a_translation_keeps_the_same_snippets(string english, string french) {
        string[] left  = FenceTagsOf(english);
        string[] right = FenceTagsOf(french);

        Assert.Equal(left, right);
    }

    [Fact]
    public void every_page_under_docs_declares_its_language() {
        foreach (string page in MarkdownPages().Where(page => page.StartsWith("docs/", StringComparison.Ordinal))) {
            Assert.True(page.EndsWith(".en.md", StringComparison.Ordinal) || page.EndsWith(".fr.md", StringComparison.Ordinal),
                        $"{page} carries no language suffix; pages under docs are named Xxx.en.md and Xxx.fr.md.");
        }
    }

    private static void AssertSwitchesTo(string page, string counterpart) {
        string header = string.Join('\n', File.ReadLines(Path.Combine(RepositoryRoot.FullName, page)).Take(6));

        Assert.True(header.Contains(counterpart, StringComparison.Ordinal),
                    $"{page} does not offer a link to {counterpart} in its language header.");
    }

    private static string[] FenceTagsOf(string page) {
        string        source = File.ReadAllText(Path.Combine(RepositoryRoot.FullName, page));
        List<string>  tags   = [];
        bool          inside = false;

        foreach (Match fence in Fence.Matches(source)) {
            if (!inside) { tags.Add(fence.Groups["tag"].Value); }
            inside = !inside;
        }

        return [.. tags];
    }

    private static IEnumerable<(string English, string French)> Pairs() {
        yield return (EnglishFrontPage, FrenchFrontPage);

        foreach (string page in MarkdownPages().Where(page => page.EndsWith(".en.md", StringComparison.Ordinal))) {
            yield return (page, page[..^".en.md".Length] + ".fr.md");
        }

        foreach (string page in MarkdownPages().Where(page => page.EndsWith(".fr.md", StringComparison.Ordinal) && page != FrenchFrontPage)) {
            yield return (page[..^".fr.md".Length] + ".en.md", page);
        }
    }

    private static IEnumerable<string> MarkdownPages() {
        return Directory.EnumerateFiles(RepositoryRoot.FullName, "*.md", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(RepositoryRoot.FullName, path).Replace('\\', '/'))
                        .Where(path => !path.StartsWith("bin/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal))
                        .Where(path => !path.StartsWith("obj/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
                        .Order();
    }

    /// <summary>GitHub's heading slug: lower case, punctuation dropped, spaces hyphenated.</summary>
    private static HashSet<string> AnchorsOf(string file) {
        HashSet<string> anchors = new(StringComparer.Ordinal);

        foreach (Match heading in Heading.Matches(File.ReadAllText(file))) {
            StringBuilder slug = new();
            foreach (char character in heading.Groups["text"].Value.Trim()) {
                if (char.IsLetterOrDigit(character)) { slug.Append(char.ToLowerInvariant(character)); } else if (character is ' ' or '-') { slug.Append('-'); }
            }

            anchors.Add(slug.ToString());
        }

        return anchors;
    }

    private static DirectoryInfo FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "rules"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

}
