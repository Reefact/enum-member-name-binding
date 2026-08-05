using Microsoft.CodeAnalysis;

namespace AspNetCore.EnumMemberNameBinding.Analyzers.Tests;

public sealed class EnumContractAnalyzerTests {

    private const string Using = "using System;\nusing System.Text.Json.Serialization;\n";

    [Fact]
    public async Task an_enum_without_any_attribute_is_ignored_entirely() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Priority { Low, Normal, High }
            """);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task a_fully_annotated_enum_reports_nothing() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("available")]    Available,
                [JsonStringEnumMemberName("out_of_stock")] OutOfStock
            }
            """);

        Assert.Empty(ids);
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

        Assert.Equal(["EMN0003", "EMN0003"], diagnostics.Select(d => d.Id));
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.Contains("Standard", diagnostics[0].GetMessage(), StringComparison.Ordinal);
        Assert.Contains("public API contract", diagnostics[0].GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Economy", diagnostics[1].GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EMN0001_reports_a_duplicated_public_name() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("same")] First,
                [JsonStringEnumMemberName("same")] Second
            }
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("EMN0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("'same'", diagnostic.GetMessage(), StringComparison.Ordinal);
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

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("EMN0002", diagnostic.Id);
        Assert.Contains(expected, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EMN0004_reports_a_comma_inside_a_flags_name() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            [Flags]
            public enum Scopes {
                [JsonStringEnumMemberName("read,write")] ReadWrite = 1
            }
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("EMN0004", diagnostic.Id);
    }

    [Fact]
    public async Task a_comma_is_allowed_in_a_non_flags_name() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("a,b")] Only
            }
            """);

        Assert.Empty(ids);
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

        Assert.Contains(diagnostics, d => d.Id == "EMN0005" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task a_public_name_colliding_with_nothing_is_not_reported() {
        IReadOnlyList<string> ids = await AnalyzerHarness.IdsAsync(Using + """
            public enum Colour {
                [JsonStringEnumMemberName("crimson")] Red,
                [JsonStringEnumMemberName("azure")]   Blue
            }
            """);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task EMN0005_reports_a_public_name_that_shadows_another_member() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Colour {
                [JsonStringEnumMemberName("Blue")] Red,
                Blue
            }
            """);

        Assert.Contains(diagnostics, d => d.Id == "EMN0005" && d.Severity == DiagnosticSeverity.Error);

        string message = diagnostics.First(d => d.Id == "EMN0005").GetMessage();
        Assert.Contains("'Red'", message, StringComparison.Ordinal);
        Assert.Contains("'Blue'", message, StringComparison.Ordinal);
        Assert.Contains("casing", message, StringComparison.Ordinal);
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

        Assert.Equal(["EMN0003", "EMN0005"], diagnostics.Select(d => d.Id).Order());
        Assert.All(diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public async Task the_diagnostic_points_at_the_declared_name_not_the_whole_enum() {
        IReadOnlyList<Diagnostic> diagnostics = await AnalyzerHarness.AnalyzeAsync(Using + """
            public enum Status {
                [JsonStringEnumMemberName("same")] First,
                [JsonStringEnumMemberName("same")] Second
            }
            """);

        string flagged = diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan);

        Assert.Equal("\"same\"", flagged);
    }

}
