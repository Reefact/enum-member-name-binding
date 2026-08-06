using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

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
            Assert.True(EscapableInEcma262.Contains(escaped, StringComparison.Ordinal),
                        $"'\\{escaped}' is not a valid identity escape in ECMA-262; pattern was: {pattern}");
            index++;
        }
    }

    [Fact]
    public void the_pattern_is_a_valid_regular_expression() {
        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;

        Exception? failure = Record.Exception(() => Regex.IsMatch("a+b", pattern));

        Assert.Null(failure);
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

        Assert.Matches(pattern, value);
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

        Assert.DoesNotMatch(pattern, value);
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
        Assert.Matches(pattern, value);

        using HttpResponseMessage response = await api.Client.GetAsync("/tricky?value=" + Uri.EscapeDataString(value));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("aXb")]
    [InlineData("bogus")]
    [InlineData("A+B")]
    public async Task everything_the_pattern_excludes_is_refused_by_the_server(string value) {
        string pattern = api.Schema(nameof(Tricky)).GetProperty("pattern").GetString()!;
        Assert.DoesNotMatch(pattern, value);

        using HttpResponseMessage response = await api.Client.GetAsync("/tricky?value=" + Uri.EscapeDataString(value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void the_document_stays_valid_json_with_such_names() {
        string document = api.Document.GetRawText();

        using JsonDocument reparsed = JsonDocument.Parse(document);

        Assert.Equal("string", reparsed.RootElement.GetProperty("components").GetProperty("schemas")
                                        .GetProperty(nameof(Tricky)).GetProperty("type").GetString());
    }

}
