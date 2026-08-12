using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reefact.AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// The generated pattern is read by other tools than .NET's regular expression engine, so checking it
/// with that engine alone only proves internal consistency. These tests also check the dialect.
/// </summary>
[Collection(nameof(OpenApiCollection))]
public sealed class PatternInteroperabilityTests(OpenApiTestApi api) {

    /// <summary>
    /// A JSON Schema <c>pattern</c> is an ECMA-262 regular expression. There, only a syntax character
    /// may follow a backslash: <c>\ </c> or <c>\#</c>, which <see cref="Regex.Escape" /> produces, are
    /// invalid identity escapes and a strict engine — JavaScript in unicode mode, for instance —
    /// rejects the entire pattern.
    /// </summary>
    [Fact]
    public void the_pattern_uses_only_escapes_that_ecma_262_accepts() {
        const string EscapableInEcma262 = @"^$\.*+?()[]{}|/dDsSwWbBnrtfv0123456789ckpPux";

        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;

        for (int index = 0; index < pattern.Length - 1; index++) {
            if (pattern[index] != '\\') { continue; }

            char escaped = pattern[index + 1];
            Check.WithCustomMessage($"'\\{escaped}' is not a valid identity escape in ECMA-262; pattern was: {pattern}")
                 .That(EscapableInEcma262.Contains(escaped, StringComparison.Ordinal)).IsTrue();
            index++;
        }
    }

    [Fact]
    public void the_pattern_is_a_valid_regular_expression() {
        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;

        Exception? failure = Record.Exception(() => Regex.IsMatch("a+b", pattern));

        Check.That(failure).IsNull();
    }

    [Theory]
    [InlineData("a+b")]
    [InlineData("c.d")]
    [InlineData("read write")]
    [InlineData("e|f")]
    [InlineData("(g)")]
    [InlineData("h#i")]
    [InlineData("[j]")]
    [InlineData("k-l")]
    [InlineData("a+b, c.d")]
    [InlineData("read write, [j]")]
    public void a_name_full_of_regex_syntax_is_matched_literally(string value) {
        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;

        Check.That(value).Matches(pattern);
    }

    /// <summary>If the special characters were not escaped, these would match.</summary>
    [Theory]
    [InlineData("aab")]     // a+b as "one or more a"
    [InlineData("aXb")]     // c.d as "any character"
    [InlineData("e")]       // e|f as an alternation
    [InlineData("g")]       // (g) as a group
    [InlineData("j")]       // [j] as a character class
    public void an_input_that_only_matches_the_unescaped_form_is_rejected(string value) {
        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;

        Check.That(value).Not.Matches(pattern);
    }

    [Theory]
    [InlineData("a+b")]
    [InlineData("c.d")]
    [InlineData("read write")]
    [InlineData("h#i")]
    [InlineData("[j]")]
    [InlineData("a+b, read write")]
    public async Task everything_the_pattern_advertises_is_accepted_by_the_server(string value) {
        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;
        Check.That(value).Matches(pattern);

        using HttpResponseMessage response = await api.Client.GetAsync("/tricky?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("aXb")]
    [InlineData("bogus")]
    [InlineData("A+B")]
    public async Task everything_the_pattern_excludes_is_refused_by_the_server(string value) {
        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;
        Check.That(value).Not.Matches(pattern);

        using HttpResponseMessage response = await api.Client.GetAsync("/tricky?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A member left unannotated keeps its C# name, which the binder matches ignoring case — so the
    /// pattern has to admit every casing of it, while a declared name stays ordinal. Writing both
    /// halves the same way made the document refuse five values the server binds.
    /// </summary>
    [Theory]
    [InlineData("Delete")]
    [InlineData("delete")]
    [InlineData("DELETE")]
    [InlineData("dElEtE")]
    [InlineData("read,Delete")]
    [InlineData("read, delete")]
    [InlineData("read,DELETE")]
    public async Task every_casing_of_an_unannotated_name_is_advertised_and_accepted(string value) {
        string pattern = api.Schema(nameof(MixedScopes)).GetProperty("pattern").GetString()!;
        Check.WithCustomMessage($"the document excludes '{value}'; pattern was: {pattern}").That(value).Matches(pattern);

        using HttpResponseMessage response = await api.Client.GetAsync("/mixed?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    /// <summary>
    /// The other half, without which the theory above passes on a pattern that admits everything: a
    /// declared name is matched ordinally, so a miscased one is excluded by the document and refused
    /// by the server.
    /// </summary>
    [Theory]
    [InlineData("Read")]
    [InlineData("READ")]
    [InlineData("Write")]
    public async Task a_miscased_declared_name_is_excluded_and_refused(string value) {
        string pattern = api.Schema(nameof(MixedScopes)).GetProperty("pattern").GetString()!;
        Check.WithCustomMessage($"the document admits '{value}'; pattern was: {pattern}").That(value).Not.Matches(pattern);

        using HttpResponseMessage response = await api.Client.GetAsync("/mixed?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The whitespace the pattern admits is exactly the whitespace the binder trims, over every
    /// <see cref="char" /> rather than over a sample.
    /// </summary>
    /// <remarks>
    /// <c>\s</c> was what the pattern used, and it is not that set: read as ECMA-262 it takes U+FEFF,
    /// which <see cref="char.IsWhiteSpace(char)" /> does not, and leaves U+0085, which it does. Both
    /// were reachable — a value opening on U+FEFF was advertised and answered 400, one opening on
    /// U+0085 was excluded and bound.
    /// <para>
    /// Checked with .NET's engine, which is sound here for once: the class the pattern now carries is
    /// written as explicit code points, so no dialect can read it differently. What is dialect-specific
    /// is only that <c>\uXXXX</c> is a valid escape, which
    /// <see cref="the_pattern_uses_only_escapes_that_ecma_262_accepts" /> already holds.
    /// </para>
    /// </remarks>
    [Fact]
    public void the_pattern_admits_exactly_the_whitespace_the_binder_trims() {
        string pattern = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;

        List<string> divergences = [];

        for (int code = 0; code <= char.MaxValue; code++) {
            char character = (char)code;
            bool trimmed   = char.IsWhiteSpace(character);
            bool admitted  = Regex.IsMatch(character + "read", pattern);

            if (trimmed != admitted) { divergences.Add($"U+{code:X4}: trimmed={trimmed}, admitted={admitted}"); }
        }

        Check.WithCustomMessage($"the pattern and String.Trim disagree on {divergences.Count} code point(s): {string.Join(", ", divergences.Take(8))}")
             .That(divergences).IsEmpty();
    }

    /// <summary>
    /// The pattern names the whitespace it admits rather than deferring to <c>\s</c>.
    /// </summary>
    /// <remarks>
    /// The test above cannot see the difference, which is the whole reason this one exists: .NET's
    /// <c>\s</c> agrees with <c>Trim</c> on the two code points ECMA-262 disagrees on, so a pattern
    /// that went back to the shorthand would satisfy every check this suite can run with its own
    /// engine while advertising a U+FEFF the server refuses and excluding a U+0085 it binds. Whose
    /// meaning <c>\s</c> carries is the reader's business, not the binder's, and that is what can be
    /// asserted from here.
    /// </remarks>
    [Fact]
    public void the_pattern_does_not_defer_its_whitespace_to_the_dialect() {
        string pattern = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;

        Check.WithCustomMessage($"the pattern leans on \\s, whose meaning is the reading engine's rather than the binder's; pattern was: {pattern}")
             .That(pattern.Contains(@"\s", StringComparison.Ordinal)).IsFalse();
    }

    [Fact]
    public void the_document_stays_valid_json_with_such_names() {
        string document = api.Document.GetRawText();

        using JsonDocument reparsed = JsonDocument.Parse(document);

        Check.That(reparsed.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(nameof(Tricky)).GetProperty("type").GetString()).IsEqualTo("string");
    }

}
