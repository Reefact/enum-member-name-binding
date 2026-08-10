using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.Documentation.Tests;

/// <summary>
/// Every documentation page is reachable from the index of its section.
/// </summary>
/// <remarks>
/// <para>
/// An index is the one page in this repository whose whole job is to be complete, and the one that
/// nothing else would notice going stale: a page added beside it still renders, still links
/// correctly, still translates — it is simply never mentioned, and a reader who navigates rather
/// than searches never learns it exists. So the index is checked against the directory it speaks
/// for, in both languages.
/// </para>
/// <para>
/// A section is a folder carrying a <c>README.md</c> — the only name GitHub renders when someone
/// opens a folder, which is what makes an index worth writing rather than a file nobody clicks. A
/// page belongs to the nearest one above it, so a folder with no index of its own is spoken for by
/// its parent: <c>for-maintainers</c> lists the records under <c>adr</c> directly, and would stop
/// having to the day <c>adr</c> grew an index of its own.
/// </para>
/// </remarks>
public sealed partial class IndexPagesTests {

    private const string EnglishIndex = "README.md";
    private const string FrenchIndex = "README.fr.md";

    private static readonly DirectoryInfo Root = DocumentationCorpus.RepositoryRoot;

    [GeneratedRegex(@"\]\((?<target>[^)\s#]+)")]
    private static partial Regex Link();

    public static TheoryData<string> Indexes {
        get {
            TheoryData<string> data = new();
            foreach (string index in DocumentationPages().Where(IsIndex).Order(StringComparer.Ordinal)) {
                data.Add(index);
            }

            return data;
        }
    }

    /// <summary>The entry point has to exist, or every path into the documentation starts by guessing.</summary>
    [Fact]
    public void the_documentation_root_carries_an_index_in_both_languages() {
        Check.WithCustomMessage("docs/README.md is missing; it is what GitHub renders when someone opens docs.")
             .That(File.Exists(Path.Combine(Root.FullName, "docs", EnglishIndex))).IsTrue();
        Check.WithCustomMessage("docs/README.fr.md is missing; the index is a page like any other and exists in both languages.")
             .That(File.Exists(Path.Combine(Root.FullName, "docs", FrenchIndex))).IsTrue();
    }

    [Theory]
    [MemberData(nameof(Indexes))]
    public void an_index_links_every_page_it_speaks_for(string index) {
        ArgumentNullException.ThrowIfNull(index);

        string       section = Directory(index);
        List<string> missing = [];

        foreach (string page in DocumentationPages().Where(page => !IsIndex(page) && IsFrench(page) == IsFrench(index) && SectionOf(page) == section)) {
            if (!Links(index).Contains(page, StringComparer.Ordinal)) { missing.Add(page); }
        }

        foreach (string subsection in Subsections(section)) {
            string subindex = Join(subsection, IsFrench(index) ? FrenchIndex : EnglishIndex);
            if (!Links(index).Contains(subindex, StringComparer.Ordinal)) { missing.Add(subindex); }
        }

        Check.WithCustomMessage($"{index} does not link {string.Join(", ", missing)}, which sit in the section it speaks for.")
             .That(missing).IsEmpty();
    }

    /// <summary>The sections directly below this one, which the index links instead of their contents.</summary>
    private static IEnumerable<string> Subsections(string section) {
        return DocumentationPages().Where(page => Path.GetFileName(page) == EnglishIndex)
                                   .Select(Directory)
                                   .Where(directory => directory != section && SectionOf(Join(directory, EnglishIndex)) == section)
                                   .Order(StringComparer.Ordinal);
    }

    /// <summary>The nearest folder at or above this page that carries an index, minus the page's own.</summary>
    private static string SectionOf(string page) {
        string directory = Directory(page);
        if (IsIndex(page)) { directory = Parent(directory); }

        while (directory.Length > 0) {
            if (File.Exists(Path.Combine(Root.FullName, directory, EnglishIndex))) { return directory; }
            directory = Parent(directory);
        }

        return string.Empty;
    }

    private static string[] Links(string page) {
        string directory = Directory(page);

        return [.. Link().Matches(File.ReadAllText(Path.Combine(Root.FullName, page)))
                         .Select(match => match.Groups["target"].Value)
                         .Where(target => !target.StartsWith("http", StringComparison.Ordinal))
                         .Select(target => Path.GetRelativePath(Root.FullName, Path.GetFullPath(Path.Combine(Root.FullName, directory, target))).Replace('\\', '/'))];
    }

    private static IEnumerable<string> DocumentationPages() {
        return System.IO.Directory.EnumerateFiles(Path.Combine(Root.FullName, "docs"), "*.md", SearchOption.AllDirectories)
                                  .Select(path => Path.GetRelativePath(Root.FullName, path).Replace('\\', '/'))
                                  .Where(path => !path.Split('/').Any(segment => segment.StartsWith('.') || segment is "bin" or "obj"));
    }

    private static bool IsIndex(string page) {
        return Path.GetFileName(page) is EnglishIndex or FrenchIndex;
    }

    private static bool IsFrench(string page) {
        return page.EndsWith(".fr.md", StringComparison.Ordinal);
    }

    private static string Directory(string page) {
        return page.Contains('/', StringComparison.Ordinal) ? page[..page.LastIndexOf('/')] : string.Empty;
    }

    private static string Parent(string directory) {
        return Directory(directory);
    }

    private static string Join(string directory, string name) {
        return directory.Length == 0 ? name : directory + "/" + name;
    }

}
