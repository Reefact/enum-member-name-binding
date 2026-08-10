using System.Text;
using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.Documentation.Tests;

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
    /// The three pages whose two halves do not sit side by side: GitHub only recognises them at the
    /// repository root, so the English original stays there and the translation joins the rest of
    /// the user documentation. Every other pair is found by name — <c>Xxx.en.md</c> beside
    /// <c>Xxx.fr.md</c>, or a <c>README.md</c> beside the <c>README.fr.md</c> in the same directory.
    /// </summary>
    private static readonly (string English, string French)[] RelocatedPairs = [
        ("CHANGELOG.md", "docs/for-users/CHANGELOG.fr.md"),
        // GitHub links "Contributing guidelines" from the issue and pull-request forms when it
        // finds this at the root; its translation follows the others into docs.
        ("CONTRIBUTING.md", "docs/for-users/CONTRIBUTING.fr.md"),
        // GitHub only offers "Report a vulnerability" when it finds the policy at one of a few fixed
        // paths, of which the repository root is one; its translation follows the others into docs.
        ("SECURITY.md", "docs/for-users/SECURITY.fr.md")
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

            Check.WithCustomMessage($"{page} links to '{target}', which does not exist.")
                 .That(File.Exists(resolved) || Directory.Exists(resolved)).IsTrue();

            if (parts.Length == 2 && File.Exists(resolved)) {
                Check.That(AnchorsOf(resolved)).Contains(Uri.UnescapeDataString(parts[1]));
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
        Check.WithCustomMessage($"{french} has no English counterpart at {english}.")
             .That(File.Exists(Path.Combine(RepositoryRoot.FullName, english))).IsTrue();
        Check.WithCustomMessage($"{english} has no French counterpart at {french}.")
             .That(File.Exists(Path.Combine(RepositoryRoot.FullName, french))).IsTrue();
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
        Check.That(FenceTagsOf(french)).IsEqualTo(FenceTagsOf(english));

        foreach ((string what, Regex pattern) in Structure) {
            Check.WithCustomMessage($"{english} has {Count(english, pattern)} {what} and {french} has {Count(french, pattern)}.")
                 .That(Count(english, pattern)).IsEqualTo(Count(french, pattern));
        }
    }

    /// <summary>
    /// A page under <c>docs</c> says which language it is in, with one exception that is not a
    /// loophole: a folder's index has to be called <c>README.md</c>, because that is the only name
    /// GitHub renders when someone opens the folder. Its French half is <c>README.fr.md</c>, which
    /// carries its language like everything else.
    /// </summary>
    [Fact]
    public void every_page_under_docs_declares_its_language() {
        foreach (string page in MarkdownPages().Where(page => page.StartsWith("docs/", StringComparison.Ordinal) && !IsIndexOrFrontPage(page))) {
            Check.WithCustomMessage($"{page} carries no language suffix; pages under docs are named Xxx.en.md and Xxx.fr.md, and only a folder index may be README.md.")
                 .That(page.EndsWith(".en.md", StringComparison.Ordinal) || page.EndsWith(".fr.md", StringComparison.Ordinal)).IsTrue();
        }
    }

    private static void AssertSwitchesTo(string page, string counterpart) {
        string header = string.Join('\n', File.ReadLines(Path.Combine(RepositoryRoot.FullName, page)).Take(6));

        Check.WithCustomMessage($"{page} does not offer a link to {counterpart} in its language header.")
             .That(header.Contains(counterpart, StringComparison.Ordinal)).IsTrue();
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
        foreach ((string english, string french) in RelocatedPairs) {
            yield return (english, french);
        }

        foreach (string page in MarkdownPages().Where(page => page.EndsWith(".en.md", StringComparison.Ordinal))) {
            yield return (page, page[..^".en.md".Length] + ".fr.md");
        }

        // A page GitHub renders from a fixed name — the front page, and the index of every folder —
        // cannot carry its language, so it pairs with the README.fr.md beside it.
        foreach (string page in MarkdownPages().Where(IsIndexOrFrontPage)) {
            yield return (page, page[..^".md".Length] + ".fr.md");
        }

        string[] relocated = [.. RelocatedPairs.Select(pair => pair.French)];
        foreach (string page in MarkdownPages().Where(page => page.EndsWith(".fr.md", StringComparison.Ordinal) && !relocated.Contains(page))) {
            string stem = page[..^".fr.md".Length];

            yield return (File.Exists(Path.Combine(RepositoryRoot.FullName, stem + ".en.md")) ? stem + ".en.md" : stem + ".md", page);
        }
    }

    private static bool IsIndexOrFrontPage(string page) {
        return Path.GetFileName(page) == "README.md";
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

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "for-users", "rules"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

}
