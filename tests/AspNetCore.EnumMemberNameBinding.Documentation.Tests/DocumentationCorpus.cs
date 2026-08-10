using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.Documentation.Tests;

/// <summary>
/// The pages whose C# samples this suite holds to a contract, read off the working tree.
/// </summary>
/// <remarks>
/// <para>
/// Scope is the documentation a <em>consumer</em> reads: the front page, and everything under
/// <c>docs/for-users</c>.
/// </para>
/// <para>
/// The maintainer documentation is out, as a body rather than page by page, because the exclusion is
/// a property of what those pages ARE. A decision record quotes C# to carry an argument — a shape, a
/// signature, the line a decision turns on — and is meant to outlive the code it quotes; holding it
/// to a contract written for teaching material would make a record answer to the thing it records.
/// That is now said by the directory a page is filed under rather than by a rule naming
/// <c>adr</c>, which is what the split into <c>for-users</c> and <c>for-maintainers</c> bought: a
/// maintainer page written tomorrow, in a folder created tomorrow, is out without anyone
/// remembering to exclude it.
/// </para>
/// <para>
/// <c>CONTRIBUTING</c>, <c>SECURITY</c> and <c>CHANGELOG</c> are filed under <c>for-users</c>,
/// beside the front page whose fate they share — GitHub renders all four from fixed names at the
/// repository root, and only their translations could move. They are still out of the compile
/// contract, and named one by one because that is what they are: three specific pages about this
/// repository rather than about the library, whose samples reach for <c>Problem</c>,
/// <c>TrimRule</c> and other internals a consumer cannot. They stay under the link and translation
/// contracts, which do apply to them.
/// </para>
/// <para>
/// Everything else under <c>for-users</c> is in scope by construction rather than by enumeration, so
/// a page created tomorrow is held to the contract from the day it exists — its author meets it
/// while writing rather than never.
/// </para>
/// </remarks>
internal static partial class DocumentationCorpus {

    /// <summary>The front page, which GitHub and NuGet both render from this fixed name.</summary>
    /// <remarks>
    /// Its translation sits beside it rather than under <c>docs</c>, unlike the other three root
    /// pages: <c>README.md</c> is the name GitHub renders for a directory, so leaving a translation
    /// of the front page under <c>docs/for-users</c> would have occupied the one name that folder's
    /// own index needs.
    /// </remarks>
    private const string FrontPage = "README.md";
    private const string FrenchFrontPage = "README.fr.md";

    /// <summary>
    /// The translations that live under <c>docs</c> only because GitHub insists their English
    /// original sits at the repository root. They document the repository, not the library.
    /// </summary>
    private static readonly string[] RelocatedRootTranslations = ["docs/for-users/CHANGELOG.fr.md", "docs/for-users/CONTRIBUTING.fr.md", "docs/for-users/SECURITY.fr.md"];

    private static readonly Lazy<IReadOnlyList<DocumentationPage>> LazyPages = new(ReadPages);

    /// <summary>Every page in scope, ordered by repository-relative path.</summary>
    public static IReadOnlyList<DocumentationPage> Pages => LazyPages.Value;

    public static DirectoryInfo RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>The in-scope pages that carry at least one C# sample, as xUnit theory data.</summary>
    public static TheoryData<string> PagesWithSamples {
        get {
            TheoryData<string> data = new();
            foreach (DocumentationPage page in Pages.Where(page => page.Samples.Count > 0)) {
                data.Add(page.RelativePath);
            }

            return data;
        }
    }

    /// <summary>The in-scope pairs, English first, as xUnit theory data.</summary>
    public static TheoryData<string, string> TranslationPairs {
        get {
            TheoryData<string, string> data = new();
            foreach (DocumentationPage page in Pages.Where(page => !page.RelativePath.EndsWith(".fr.md", StringComparison.Ordinal))) {
                data.Add(page.RelativePath, TwinOf(page.RelativePath));
            }

            return data;
        }
    }

    public static DocumentationPage Page(string relativePath) {
        return Pages.Single(page => page.RelativePath == relativePath);
    }

    /// <summary>
    /// Where a page's translation belongs.
    /// </summary>
    /// <remarks>
    /// Most pages carry their language in the file name. The exceptions are the pages GitHub renders
    /// from a fixed name — the front page, and the index of every documentation folder — which keep
    /// the bare <c>README.md</c> and pair with the <c>README.fr.md</c> beside them.
    /// </remarks>
    public static string TwinOf(string relativePath) {
        ArgumentNullException.ThrowIfNull(relativePath);

        if (relativePath.EndsWith(".en.md", StringComparison.Ordinal)) { return relativePath[..^".en.md".Length] + ".fr.md"; }
        if (relativePath.EndsWith(".md", StringComparison.Ordinal) && !relativePath.EndsWith(".fr.md", StringComparison.Ordinal)) { return relativePath[..^".md".Length] + ".fr.md"; }

        // Going the other way, the stem alone does not say which of the two spellings the English
        // page uses — `analyzers.en.md` carries its language, `README.md` cannot — so the twin is the
        // one that exists. Both are returned as a path rather than a promise: whether it is there is
        // what the pairing test asserts.
        if (relativePath.EndsWith(".fr.md", StringComparison.Ordinal)) {
            string stem = relativePath[..^".fr.md".Length];

            return File.Exists(Path.Combine(RepositoryRoot.FullName, stem + ".en.md")) ? stem + ".en.md" : stem + ".md";
        }

        throw new ArgumentException($"{relativePath} is not a documentation page, so it has no twin.", nameof(relativePath));
    }

    private static bool IsInScope(string relativePath) {
        if (relativePath is FrontPage or FrenchFrontPage) { return true; }
        if (!relativePath.StartsWith("docs/for-users/", StringComparison.Ordinal)) { return false; }

        return !RelocatedRootTranslations.Contains(relativePath, StringComparer.Ordinal);
    }

    private static IReadOnlyList<DocumentationPage> ReadPages() {
        IEnumerable<string> relative = Directory.EnumerateFiles(RepositoryRoot.FullName, "*.md", SearchOption.AllDirectories)
                                                .Select(path => Path.GetRelativePath(RepositoryRoot.FullName, path).Replace('\\', '/'))
                                                .Where(path => !path.Split('/').Any(segment => segment.StartsWith('.') || segment is "bin" or "obj"))
                                                .Where(IsInScope);

        return [.. relative.Order(StringComparer.Ordinal).Select(path => DocumentationPage.Read(path, RepositoryRoot.FullName))];
    }

    private static DirectoryInfo FindRepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "for-users", "rules"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

}

/// <summary>How a fenced sample is turned into a compilation unit.</summary>
/// <remarks>
/// The shape is inferred, not declared: each wrapping is parsed in turn and the first that produces
/// no syntax error wins. Declaring it would mean a marker on nearly every block in both languages,
/// to say something the parser can already tell — and the order below is unambiguous, because a
/// sample that parses at namespace level is never also a run of statements.
/// </remarks>
internal enum SampleShape {

    /// <summary>Types, placed at namespace level. The common case.</summary>
    Declarations,

    /// <summary>Members of a controller — an action shown without the class around it.</summary>
    ClassMember,

    /// <summary>Members of an enum — one entry shown without the enum around it.</summary>
    EnumMember,

    /// <summary>A run of statements, wrapped in a method body.</summary>
    Statements

}

/// <summary>One fenced C# block of a page, with whatever <c>emn:</c> marker preceded it.</summary>
/// <param name="Content">The fenced text, without the fences themselves.</param>
/// <param name="StartLine">The 1-based line of the opening fence, so a failure points at the page.</param>
/// <param name="Skipped">The sample opts out of the compile contract.</param>
/// <param name="AllowedRuleIds">The rules this sample is EXPECTED to trip, because it shows the mistake.</param>
/// <param name="Marker">The raw marker, compared against the twin page so an opt-out cannot be translated away.</param>
internal sealed record CodeFence(string Content, int StartLine, bool Skipped, IReadOnlyList<string> AllowedRuleIds, string Marker);

/// <summary>A documentation page and the C# samples it carries.</summary>
internal sealed partial record DocumentationPage(string RelativePath, string AbsolutePath, IReadOnlyList<CodeFence> Samples) {

    /// <summary>
    /// The two markers a page may carry, each on the line immediately above the fence it governs.
    /// </summary>
    /// <remarks>
    /// Neither carries a reason, deliberately: the marker is compared against the twin page, and a
    /// reason written in prose would be translated and stop matching. Why a sample opts out, or
    /// which mistake it demonstrates, is what the page around it is already saying — in both
    /// languages, to the reader, which is where that explanation belongs.
    /// </remarks>
    [GeneratedRegex(@"^<!--\s*emn:(?<directive>skip|allow=(?<ids>EMN\d{4}(?:\s*,\s*EMN\d{4})*))\s*-->$")]
    private static partial Regex Marker();

    [GeneratedRegex(@"^```(?<tag>[^\s`]*)\s*$")]
    private static partial Regex Fence();

    public static DocumentationPage Read(string relativePath, string repositoryRoot) {
        ArgumentNullException.ThrowIfNull(relativePath);

        string   absolute = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string[] lines    = File.ReadAllLines(absolute);

        List<CodeFence> samples = [];
        for (int index = 0; index < lines.Length; index++) {
            Match opening = Fence().Match(lines[index]);
            if (!opening.Success) { continue; }

            int closing = index + 1;
            while (closing < lines.Length && !Fence().IsMatch(lines[closing])) { closing++; }

            if (opening.Groups["tag"].Value == "csharp") {
                samples.Add(ReadSample(lines, index, closing));
            }

            index = closing;
        }

        return new DocumentationPage(relativePath, absolute, samples);
    }

    private static CodeFence ReadSample(string[] lines, int openingIndex, int closingIndex) {
        string content = string.Join('\n', lines[(openingIndex + 1)..Math.Min(closingIndex, lines.Length)]);
        Match  marker  = MarkerAbove(lines, openingIndex);

        if (!marker.Success) { return new CodeFence(content, openingIndex + 1, Skipped: false, [], string.Empty); }

        string directive = marker.Groups["directive"].Value;
        if (directive == "skip") { return new CodeFence(content, openingIndex + 1, Skipped: true, [], marker.Value); }

        string[] ids = [.. marker.Groups["ids"].Value.Split(',').Select(id => id.Trim())];

        return new CodeFence(content, openingIndex + 1, Skipped: false, ids, marker.Value);
    }

    /// <summary>
    /// The marker governing a fence sits on the line above it, blank lines allowed in between — a
    /// marker further away would be governing something the reader cannot tell.
    /// </summary>
    private static Match MarkerAbove(string[] lines, int openingIndex) {
        int above = openingIndex - 1;
        while (above >= 0 && lines[above].Trim().Length == 0) { above--; }

        return above < 0 ? Match.Empty : Marker().Match(lines[above].Trim());
    }

}
