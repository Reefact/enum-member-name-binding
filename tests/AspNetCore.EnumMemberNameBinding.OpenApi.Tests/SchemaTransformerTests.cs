using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

[Collection(nameof(OpenApiCollection))]
public sealed class SchemaTransformerTests(OpenApiTestApi api) {

    [Fact]
    public void a_contract_enum_is_typed_as_a_string() {
        Assert.Equal("string", api.Schema(nameof(OrderState)).GetProperty("type").GetString());
    }

    [Fact]
    public void a_contract_enum_advertises_its_public_names() {
        string[] values = [.. api.Schema(nameof(OrderState)).GetProperty("enum").EnumerateArray().Select(v => v.GetString()!)];

        Assert.Equal(["pending", "shipped", "cancelled"], values);
    }

    [Fact]
    public void a_plain_enum_is_left_untouched() {
        JsonElement schema = api.Schema(nameof(PlainLevel));

        Assert.Equal("integer", schema.GetProperty("type").GetString());
        Assert.False(schema.TryGetProperty("enum", out _));
    }

    [Fact]
    public void a_flags_enum_advertises_a_pattern_instead_of_a_closed_set() {
        JsonElement schema = api.Schema(nameof(Scopes));

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.False(schema.TryGetProperty("enum", out _));

        string pattern = schema.GetProperty("pattern").GetString()!;
        Assert.Matches(pattern, "read");
        Assert.Matches(pattern, "read, write");
        Assert.Matches(pattern, "read,write");
        Assert.DoesNotMatch(pattern, "bogus");
        Assert.DoesNotMatch(pattern, "Read");
    }

    /// <summary>
    /// The binder trims the value and tolerates one trailing comma, because System.Text.Json does.
    /// A pattern that excluded those forms would advertise a stricter contract than the server keeps.
    /// </summary>
    [Theory]
    [InlineData(" read", true)]
    [InlineData("read ", true)]
    [InlineData(" read, write ", true)]
    [InlineData("read,", true)]
    [InlineData("read, write,", true)]
    [InlineData(",read", false)]
    [InlineData("read,,write", false)]
    [InlineData("read, ,write", false)]
    public void the_flags_pattern_covers_the_whitespace_the_binder_accepts(string value, bool expected) {
        string pattern = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;

        Assert.Equal(expected, Regex.IsMatch(value, pattern));
    }

    [Fact]
    public void a_flags_enum_explains_the_combination_syntax() {
        Assert.Contains("comma", api.Schema(nameof(Scopes)).GetProperty("description").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void every_parameter_of_a_contract_enum_points_at_the_corrected_schema() {
        foreach (string path in new[] { "/orders", "/orders/{state}" }) {
            JsonElement parameter = api.Document.GetProperty("paths").GetProperty(path)
                                       .GetProperty("get").GetProperty("parameters").EnumerateArray().First();

            Assert.Equal("#/components/schemas/OrderState", parameter.GetProperty("schema").GetProperty("$ref").GetString());
        }
    }

}

/// <summary>
/// The point of the package: what the document promises is what the server does.
/// </summary>
[Collection(nameof(OpenApiCollection))]
public sealed class DocumentMatchesRuntimeTests(OpenApiTestApi api) {

    [Fact]
    public async Task every_advertised_value_is_accepted_by_the_server() {
        foreach (JsonElement value in api.Schema(nameof(OrderState)).GetProperty("enum").EnumerateArray()) {
            string advertised = value.GetString()!;

            using HttpResponseMessage query = await api.Client.GetAsync("/orders?state=" + Uri.EscapeDataString(advertised), TestContext.Current.CancellationToken);
            using HttpResponseMessage route = await api.Client.GetAsync("/orders/" + Uri.EscapeDataString(advertised), TestContext.Current.CancellationToken);

            Assert.True(query.StatusCode == HttpStatusCode.OK, $"the document advertises '{advertised}' but the query string rejected it.");
            Assert.True(route.StatusCode == HttpStatusCode.OK, $"the document advertises '{advertised}' but the route rejected it.");
        }
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("PENDING")]
    [InlineData("0")]
    [InlineData("bogus")]
    public async Task a_value_the_document_does_not_advertise_is_rejected(string value) {
        string[] advertised = [.. api.Schema(nameof(OrderState)).GetProperty("enum").EnumerateArray().Select(v => v.GetString()!)];
        Assert.DoesNotContain(value, advertised);

        using HttpResponseMessage response = await api.Client.GetAsync("/orders?state=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("read, write")]
    [InlineData("read,write")]
    [InlineData("read, write, delete")]
    [InlineData(" read ")]
    [InlineData(" read, write ")]
    [InlineData("read,")]
    [InlineData("read, write,")]
    public async Task every_value_matching_the_flags_pattern_is_accepted_by_the_server(string value) {
        string pattern = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;
        Assert.Matches(pattern, value);

        using HttpResponseMessage response = await api.Client.GetAsync("/tokens?scopes=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("Read")]
    [InlineData("read, bogus")]
    public async Task a_value_rejected_by_the_flags_pattern_is_rejected_by_the_server(string value) {
        string pattern = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;
        Assert.False(Regex.IsMatch(value, pattern), $"'{value}' unexpectedly matches the advertised pattern.");

        using HttpResponseMessage response = await api.Client.GetAsync("/tokens?scopes=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

}

/// <summary>
/// Characterizes what ASP.NET Core produces on its own, so the value added by this package stays
/// visible — and so a future platform change is caught rather than silently absorbed.
/// </summary>
[Collection(nameof(StockOpenApiCollection))]
public sealed class StockBehaviourTests(WithoutTransformer api) {

    [Fact]
    public void stock_aspnetcore_emits_the_values_without_declaring_a_type() {
        JsonElement schema = api.Schema(nameof(OrderState));

        Assert.False(schema.TryGetProperty("type", out _));
        Assert.True(schema.TryGetProperty("enum", out _));
    }

    [Fact]
    public void stock_aspnetcore_documents_no_value_at_all_for_a_flags_enum() {
        JsonElement schema = api.Schema(nameof(Scopes));

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.False(schema.TryGetProperty("enum", out _));
        Assert.False(schema.TryGetProperty("pattern", out _));
    }

}
