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

    public static TheoryData<string> Pages {
        get {
            TheoryData<string> data = new();
            foreach (string page in MarkdownPages()) {
                data.Add(page);
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
            string   relative = parts[0];
            string   resolved = Path.GetFullPath(Path.Combine(RepositoryRoot.FullName, Path.GetDirectoryName(page) ?? string.Empty, relative));

            Assert.True(File.Exists(resolved) || Directory.Exists(resolved), $"{page} links to '{target}', which does not exist.");

            if (parts.Length == 2 && File.Exists(resolved)) {
                Assert.Contains(parts[1], AnchorsOf(resolved), StringComparer.Ordinal);
            }
        }
    }

    /// <summary>
    /// A relative link works on GitHub and is dead on nuget.org, which renders the same file. The
    /// mistake is invisible until someone clicks it from the package page.
    /// </summary>
    [Fact]
    public void the_readme_links_to_this_repository_absolutely() {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot.FullName, "README.md"));

        foreach (Match match in Link.Matches(source)) {
            string target = match.Groups["target"].Value;
            if (target.StartsWith("http", StringComparison.Ordinal) || target.StartsWith('#')) { continue; }

            Assert.Fail($"README.md links to '{target}' relatively; the NuGet package page needs {BlobPrefix}…");
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
