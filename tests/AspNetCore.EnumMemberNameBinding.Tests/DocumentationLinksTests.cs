using System.Text;
using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// The README is also the NuGet package page, where a relative link is dead: it must point at
/// github.com. The pages under <c>docs</c> are only ever read on GitHub, so they link relatively.
/// Both forms have to resolve — a documentation split is exactly where links rot.
/// </summary>
public sealed partial class DocumentationLinksTests {

    private const string BlobPrefix = "https://github.com/Reefact/enum-member-name-binding/blob/main/";

    private static readonly DirectoryInfo RepositoryRoot = FindRepositoryRoot();
    // Source-generated rather than constructed: the pattern is compiled at build time, so a mistake
    // in one is a compile error rather than a first-use exception, and RegexOptions.Compiled becomes
    // unnecessary — the generator emits the matcher itself.
    [GeneratedRegex(@"\[[^\]]*\]\((?<target>[^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^(?<level>#{1,6})\s+(?<text>.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^```(?<tag>\w*)\s*$", RegexOptions.Multiline)]
    private static partial Regex Fence();

    [GeneratedRegex(@"^```.*?^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencedCode();

    private const string EnglishFrontPage = "README.md";

    /// <summary>
    /// The pages GitHub and NuGet expect at a fixed name keep it, and their French version sits
    /// beside them; every other page follows the file-suffix convention from inside <c>docs</c>.
    /// </summary>
    private static readonly (string English, string French)[] RootPages = [
        (EnglishFrontPage, "docs/README.fr.md"),
        ("CHANGELOG.md", "docs/CHANGELOG.fr.md"),
        // GitHub only offers "Report a vulnerability" when it finds the policy at one of a few fixed
        // paths, of which the repository root is one; its translation follows the others into docs.
        ("SECURITY.md", "docs/SECURITY.fr.md"),
        // GitHub renders a directory's README.md and nothing else, so this one stays where it is.
        ("tests/PackageSmokeTest/README.md", "tests/PackageSmokeTest/README.fr.md")
    ];

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

        foreach (Match match in Link().Matches(source)) {
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

        foreach (Match match in Link().Matches(source)) {
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
    /// Words are translated, structure is not. The contents are deliberately not compared — comments
    /// and string literals inside an example are translated too — but a snippet, a section, a bullet
    /// or a table row present on one side and not the other means the two pages no longer say the
    /// same thing. This is what catches an entry appended to one changelog and not the other.
    /// </summary>
    [Theory]
    [MemberData(nameof(TranslationPairs))]
    public void a_translation_keeps_the_same_structure(string english, string french) {
        Assert.Equal(FenceTagsOf(english), FenceTagsOf(french));

        foreach ((string what, Regex pattern) in Structure) {
            Assert.True(Count(english, pattern) == Count(french, pattern),
                        $"{english} has {Count(english, pattern)} {what} and {french} has {Count(french, pattern)}.");
        }
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

    [GeneratedRegex(@"^#{1,6} ", RegexOptions.Multiline)]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"^- ", RegexOptions.Multiline)]
    private static partial Regex BulletLine();

    [GeneratedRegex(@"^\|", RegexOptions.Multiline)]
    private static partial Regex TableRowLine();

    private static readonly (string What, Regex Pattern)[] Structure = [
        ("headings", HeadingLine()),
        ("bullets", BulletLine()),
        ("table rows", TableRowLine())
    ];

    private static int Count(string page, Regex pattern) {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot.FullName, page));

        return pattern.Count(FencedCode().Replace(source, string.Empty));
    }

    private static string[] FenceTagsOf(string page) {
        string        source = File.ReadAllText(Path.Combine(RepositoryRoot.FullName, page));
        List<string>  tags   = [];
        bool          inside = false;

        foreach (Match fence in Fence().Matches(source)) {
            if (!inside) { tags.Add(fence.Groups["tag"].Value); }
            inside = !inside;
        }

        return [.. tags];
    }

    private static IEnumerable<(string English, string French)> Pairs() {
        foreach ((string english, string french) in RootPages) {
            yield return (english, french);
        }

        foreach (string page in MarkdownPages().Where(page => page.EndsWith(".en.md", StringComparison.Ordinal))) {
            yield return (page, page[..^".en.md".Length] + ".fr.md");
        }

        string[] rooted = [.. RootPages.Select(pair => pair.French)];
        foreach (string page in MarkdownPages().Where(page => page.EndsWith(".fr.md", StringComparison.Ordinal) && !rooted.Contains(page))) {
            yield return (page[..^".fr.md".Length] + ".en.md", page);
        }
    }

    /// <summary>
    /// The pages this repository writes, and only those. Build output is skipped, and so is anything
    /// under a dot-directory — the package smoke test unpacks a NuGet cache into <c>.work</c>, which
    /// is full of other people's READMEs carrying other people's dead links.
    /// </summary>
    private static IEnumerable<string> MarkdownPages() {
        return Directory.EnumerateFiles(RepositoryRoot.FullName, "*.md", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(RepositoryRoot.FullName, path).Replace('\\', '/'))
                        .Where(path => !path.Split('/').Any(segment => segment.StartsWith('.') || segment is "bin" or "obj"))
                        .Order();
    }

    /// <summary>GitHub's heading slug: lower case, punctuation dropped, spaces hyphenated.</summary>
    private static HashSet<string> AnchorsOf(string file) {
        HashSet<string> anchors = new(StringComparer.Ordinal);

        foreach (Match heading in Heading().Matches(File.ReadAllText(file))) {
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
