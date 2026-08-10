using Microsoft.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

public sealed class EnumContractAnalyzerTests {

    private const string Using = "using System;\nusing System.Text.Json.Serialization;\n";

    [Fact]
    public async Task an_enum_without_any_attribute_is_ignored_entirely() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Priority { Low, Normal, High }
            """);

        Check.That(ids).IsEmpty();
    }

    /// <summary>
    /// The analyzer is registered on every named type, and in any real compilation almost none of
    /// them are enums. Each one still reaches it, so leaving them alone is a behaviour and not an
    /// absence of one.
    /// </summary>
    /// <remarks>
    /// The class is written as the one shape that would be reported if the analyzer looked no
    /// further than the attribute: two constants declaring the same public name, which is EMN0001 on
    /// an enum. <c>[JsonStringEnumMemberName]</c> targets a field, and a constant is one, so nothing
    /// but the type check stands between this snippet and a diagnostic it must not earn.
    /// </remarks>
    [Fact]
    public async Task a_type_that_is_not_an_enum_is_left_alone() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public sealed class Status {
                [JsonStringEnumMemberName("same")] public const string Available = "a";
                [JsonStringEnumMemberName("same")] public const string Discontinued = "d";
                public string Name { get; set; } = "";
            }

            public interface IStatus { }

            public struct Shipping {
                [JsonStringEnumMemberName("same")] public const int First = 1;
                [JsonStringEnumMemberName("same")] public const int Second = 2;
            }
            """);

        Check.That(ids).IsEmpty();
    }

    /// <summary>An enum declaring no member at all declares no contract either.</summary>
    [Fact]
    public async Task an_enum_with_no_member_is_left_alone() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Nothing { }
            """);

        Check.That(ids).IsEmpty();
    }

    [Fact]
    public async Task a_fully_annotated_enum_reports_nothing() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("available")]    Available,
                [JsonStringEnumMemberName("out_of_stock")] OutOfStock
            }
            """);

        Check.That(ids).IsEmpty();
    }

    [Fact]
    public async Task EMN0003_reports_every_member_left_unannotated() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Shipping {
                [JsonStringEnumMemberName("express")] Express,
                Standard,
                Economy
            }
            """);

        Check.That(diagnostics.Select(d => d.Id)).ContainsExactly("EMN0003", "EMN0003");
        Check.That(diagnostics).ContainsOnlyElementsThatMatch(d => d.Severity == DiagnosticSeverity.Error);
        Check.That(diagnostics[0].GetMessage()).Contains("Standard");
        Check.That(diagnostics[0].GetMessage()).Contains("public API contract");
        Check.That(diagnostics[1].GetMessage()).Contains("Economy");
    }

    [Fact]
    public async Task EMN0001_reports_a_duplicated_public_name() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("same")] First,
                [JsonStringEnumMemberName("same")] Second
            }
            """);

        Check.That(diagnostics).HasOneElementOnly();
        Diagnostic diagnostic = diagnostics.Single();
        Check.That(diagnostic.Id).IsEqualTo("EMN0001");
        Check.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        Check.That(diagnostic.GetMessage()).Contains("'same'");
    }

    [Theory]
    [InlineData("\"\"", "is empty")]
    [InlineData("\" padded \"", "whitespace")]
    [InlineData("\"trailing \"", "whitespace")]
    public async Task EMN0002_reports_a_name_that_cannot_travel_over_http(string literal, string expected) {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + $$"""
            public enum Status {
                [JsonStringEnumMemberName({{literal}})] Only
            }
            """);

        Check.That(diagnostics).HasOneElementOnly();
        Diagnostic diagnostic = diagnostics.Single();
        Check.That(diagnostic.Id).IsEqualTo("EMN0002");
        Check.That(diagnostic.GetMessage()).Contains(expected);
    }

    [Fact]
    public async Task EMN0004_reports_a_comma_inside_a_flags_name() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            [Flags]
            public enum Scopes {
                [JsonStringEnumMemberName("read,write")] ReadWrite = 1
            }
            """);

        Check.That(diagnostics).HasOneElementOnly();
        Diagnostic diagnostic = diagnostics.Single();
        Check.That(diagnostic.Id).IsEqualTo("EMN0004");
    }

    [Fact]
    public async Task a_comma_is_allowed_in_a_non_flags_name() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("a,b")] Only
            }
            """);

        Check.That(ids).IsEmpty();
    }

    /// <summary>
    /// The runtime looks up an unannotated member's C# name case-insensitively, so the analyzer must
    /// compare the same way. An ordinal comparison let the lower-case form slip through unreported.
    /// </summary>
    [Theory]
    [InlineData("Blue")]
    [InlineData("blue")]
    [InlineData("BLUE")]
    public async Task EMN0005_ignores_casing_when_looking_for_the_collision(string declared) {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + $$"""
            public enum Colour {
                [JsonStringEnumMemberName("{{declared}}")] Red,
                Blue
            }
            """);

        Check.That(diagnostics).HasElementThatMatches(d => d.Id == "EMN0005" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task a_public_name_colliding_with_nothing_is_not_reported() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Colour {
                [JsonStringEnumMemberName("crimson")] Red,
                [JsonStringEnumMemberName("azure")]   Blue
            }
            """);

        Check.That(ids).IsEmpty();
    }

    [Fact]
    public async Task EMN0005_reports_a_public_name_that_shadows_another_member() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Colour {
                [JsonStringEnumMemberName("Blue")] Red,
                Blue
            }
            """);

        Check.That(diagnostics).HasElementThatMatches(d => d.Id == "EMN0005" && d.Severity == DiagnosticSeverity.Error);

        string message = diagnostics.First(d => d.Id == "EMN0005").GetMessage();
        Check.That(message).Contains("'Red'");
        Check.That(message).Contains("'Blue'");
        Check.That(message).Contains("casing");
    }

    /// <summary>
    /// EMN0005 only ever fires alongside EMN0003, so it earns its place in the ruleset by being the
    /// last protection left once EMN0003 is turned off to allow partial contracts.
    /// </summary>
    [Fact]
    public async Task EMN0005_survives_EMN0003_being_suppressed() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Colour {
                [JsonStringEnumMemberName("Blue")] Red,
                Blue
            }
            """);

        Check.That(diagnostics.Select(d => d.Id).Order()).ContainsExactly("EMN0003", "EMN0005");
        Check.That(diagnostics).ContainsOnlyElementsThatMatch(d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// The forbidden set was measured against a running server, channel by channel, not read off a
    /// specification. See docs/for-users/rules/EMN0006.en.md for the table.
    /// </summary>
    [Theory]
    [InlineData(@"news/world", "a slash", "a route segment")]
    [InlineData(@"line\nbreak", "a line break", "a header")]
    [InlineData(@"line\rbreak", "a line break", "a header")]
    [InlineData(@"\u00e9puise", "outside printable ASCII", "a header")]
    [InlineData(@"non\u00a0breaking", "outside printable ASCII", "a header")]
    [InlineData(@"bell\u0001", "a control character", "a header")]
    public async Task EMN0006_reports_a_name_a_channel_cannot_carry(string declared, string what, string channel) {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + $$"""
            public enum Section {
                [JsonStringEnumMemberName("{{declared}}")] Only
            }
            """);

        Check.That(diagnostics).HasOneElementOnly();
        Diagnostic diagnostic = diagnostics.Single();
        Check.That(diagnostic.Id).IsEqualTo("EMN0006");
        Check.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        Check.That(diagnostic.GetMessage()).Contains(what);
        Check.That(diagnostic.GetMessage()).Contains(channel);
    }

    /// <summary>Every one of these was measured to survive all five channels.</summary>
    [Theory]
    [InlineData("with?question")]
    [InlineData("with#hash")]
    [InlineData("with&amp")]
    [InlineData("with=equals")]
    [InlineData("with+plus")]
    [InlineData("with%percent")]
    [InlineData("with space")]
    // Written as they must appear inside the snippet's own string literal.
    [InlineData("with\\\\backslash")]
    [InlineData("with\\\"quote")]
    // A tab, which this rule reported for a while. RFC 9110 rules out the other control characters
    // but admits this one wherever it admits a space, and the measurement agrees, so it stays legal.
    // EMN0002 still rejects a name that begins or ends with one.
    [InlineData("with\\ttab")]
    public async Task a_name_every_channel_can_carry_is_not_reported(string declared) {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + $$"""
            public enum Section {
                [JsonStringEnumMemberName("{{declared}}")] Only
            }
            """);

        Check.That(ids).IsEmpty();
    }

    [Fact]
    public async Task the_diagnostic_points_at_the_declared_name_not_the_whole_enum() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("same")] First,
                [JsonStringEnumMemberName("same")] Second
            }
            """);

        string flagged = diagnostics[0].Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostics[0].Location.SourceSpan);

        Check.That(flagged).IsEqualTo("\"same\"");
    }

}
