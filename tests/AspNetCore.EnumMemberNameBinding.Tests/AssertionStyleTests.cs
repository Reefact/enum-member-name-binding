namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// `Check.ThatCode(() => …);` with nothing chained compiles, runs, asserts nothing and reports
/// green. The xUnit form it replaced could not do that — `Assert.Throws` missing its lambda is a
/// compile error — so this is the one way the move to NFluent can weaken the suite without anything
/// turning red. Coverage does not notice either: the line was executed.
/// </summary>
/// <remarks>
/// Only `ThatCode`. The same hazard on `Check.That` is `NA0001`, which NFluent.Analyzer reports as a
/// compiler diagnostic — an error here, since warnings are — and that beats a test on every count:
/// it fires in the editor, on the line, before anything runs. Measured, though, rather than assumed
/// from the rule's wording: `Check.That(subject);` is reported and `Check.ThatCode(() => Boom());`
/// builds clean, which is the gap this fills. Should a later version of the analyzer close it, this
/// test is what should go.
/// <para>
/// Reads the test sources of every project under tests/ rather than this one, since the hazard
/// belongs to the library and not to a project — four of them today, and whatever is added next,
/// which is why the sentence names the directory rather than a count.
/// See docs/for-maintainers/adr/0001-nfluent-for-test-assertions.en.md.
/// </para>
/// </remarks>
public sealed class AssertionStyleTests {

    private static readonly string[] Openings = ["Check.ThatCode("];

    [Fact]
    public void every_thatcode_carries_an_assertion() {
        List<string> naked = [];

        foreach (string file in TestSources()) {
            string source = File.ReadAllText(file);
            string code   = Masked(source);

            foreach (string opening in Openings) {
                int at = code.IndexOf(opening, StringComparison.Ordinal);
                while (at >= 0) {
                    int subject = MatchingParenthesis(code, at + opening.Length - 1);
                    if (subject > 0 && NextMeaningful(code, subject + 1) != '.') {
                        naked.Add($"{Path.GetFileName(file)}:{LineOf(source, at)}");
                    }

                    at = code.IndexOf(opening, at + opening.Length, StringComparison.Ordinal);
                }
            }
        }

        Check.WithCustomMessage($"ThatCode with nothing chained asserts nothing: {string.Join(", ", naked)}")
             .That(naked).IsEmpty();
    }

    /// <summary>
    /// Every character inside a comment, a string or a character literal replaced by a space, so
    /// that what is left is code and only code, at the same offsets. It is what lets the search
    /// below run on plain text without mistaking this file's own examples for real calls.
    /// </summary>
    /// <remarks>
    /// Raw string literals are recognised at a fence of three quotes, which is what the analyzer
    /// fixtures use. A longer fence would end the literal early here; nothing in this repository
    /// writes one, and the test that guards this test would catch it becoming wrong.
    /// </remarks>
    private static string Masked(string source) {
        char[] code = source.ToCharArray();

        void Blank(int from, int to) {
            for (int i = from; i < to && i < code.Length; i++) {
                if (code[i] != '\n') { code[i] = ' '; }
            }
        }

        for (int i = 0; i < source.Length; i++) {
            if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/') {
                int end = source.IndexOf('\n', i);
                end = end < 0 ? source.Length : end;
                Blank(i, end);
                i = end;
            } else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*') {
                int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Blank(i, end);
                i = end - 1;
            } else if (source.AsSpan(i).StartsWith("\"\"\"", StringComparison.Ordinal)) {
                int end = source.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 3;
                Blank(i, end);
                i = end - 1;
            } else if (source[i] == '"') {
                bool verbatim = i > 0 && source[i - 1] == '@';
                int  end      = EndOfString(source, i, verbatim);
                Blank(i, end);
                i = end - 1;
            } else if (source[i] == '\'') {
                int end = i + 1;
                while (end < source.Length && source[end] != '\'') { end += source[end] == '\\' ? 2 : 1; }
                Blank(i, Math.Min(end + 1, source.Length));
                i = end;
            }
        }

        return new string(code);
    }

    private static int EndOfString(string source, int start, bool verbatim) {
        int i = start + 1;
        while (i < source.Length) {
            if (verbatim) {
                if (source[i] != '"') { i++; continue; }
                if (i + 1 < source.Length && source[i + 1] == '"') { i += 2; continue; }

                return i + 1;
            }

            if (source[i] == '\\') { i += 2; continue; }
            if (source[i] == '"') { return i + 1; }

            i++;
        }

        return source.Length;
    }

    private static int MatchingParenthesis(string code, int open) {
        int depth = 0;
        for (int i = open; i < code.Length; i++) {
            if (code[i] == '(') { depth++; }
            if (code[i] != ')') { continue; }

            depth--;
            if (depth == 0) { return i; }
        }

        return -1;
    }

    private static char NextMeaningful(string code, int from) {
        for (int i = from; i < code.Length; i++) {
            if (!char.IsWhiteSpace(code[i])) { return code[i]; }
        }

        return '\0';
    }

    private static int LineOf(string source, int index) {
        return source.Take(index).Count(character => character == '\n') + 1;
    }

    private static IEnumerable<string> TestSources() {
        DirectoryInfo tests = new(Path.Combine(RepositoryRoot().FullName, "tests"));

        return Directory.EnumerateFiles(tests.FullName, "*.cs", SearchOption.AllDirectories)
                        .Where(path => !path.Split(Path.DirectorySeparatorChar)
                                            .Any(segment => segment.StartsWith('.') || segment is "bin" or "obj"))
                        .Order();
    }

    private static DirectoryInfo RepositoryRoot() {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "for-users", "rules"))) {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }

}
