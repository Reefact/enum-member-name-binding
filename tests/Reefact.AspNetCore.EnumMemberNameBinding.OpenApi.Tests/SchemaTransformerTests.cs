using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Reefact.AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

[Collection(nameof(OpenApiCollection))]
public sealed class SchemaTransformerTests(OpenApiTestApi api) {

    [Fact]
    public void a_contract_enum_is_typed_as_a_string() {
        Check.That(api.Schema(nameof(OrderState)).GetProperty("type").GetString()).IsEqualTo("string");
    }

    [Fact]
    public void a_contract_enum_advertises_its_public_names() {
        string[] values = [.. api.Schema(nameof(OrderState)).GetProperty("enum").EnumerateArray().Select(v => v.GetString()!)];

        Check.That(values).ContainsExactly("pending", "shipped", "cancelled");
    }

    [Fact]
    public void a_plain_enum_is_left_untouched() {
        JsonElement schema = api.Schema(nameof(PlainLevel));

        Check.That(schema.GetProperty("type").GetString()).IsEqualTo("integer");
        Check.That(schema.TryGetProperty("enum", out _)).IsFalse();
    }

    [Fact]
    public void a_flags_enum_advertises_a_pattern_instead_of_a_closed_set() {
        JsonElement schema = api.Schema(nameof(Scopes));

        Check.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
        Check.That(schema.TryGetProperty("enum", out _)).IsFalse();

        string pattern = schema.GetProperty("pattern").GetString()!;
        Check.That("read").Matches(pattern);
        Check.That("read, write").Matches(pattern);
        Check.That("read,write").Matches(pattern);
        Check.That("bogus").Not.Matches(pattern);
        Check.That("Read").Not.Matches(pattern);
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

        Check.That(Regex.IsMatch(value, pattern)).IsEqualTo(expected);
    }

    [Fact]
    public void a_flags_enum_explains_the_combination_syntax() {
        Check.That(api.Schema(nameof(Scopes)).GetProperty("description").GetString()!.ToUpperInvariant()).Contains("COMMA");
    }

    /// <summary>
    /// A description already written — by the application, by another transformer, or by the enum's
    /// own XML comments — is kept, and the combination sentence follows it rather than replacing it.
    /// </summary>
    [Fact]
    public void a_description_already_written_is_kept_and_continued() {
        string description = api.Schema(nameof(Tricky)).GetProperty("description").GetString()!;

        Check.That(description).StartsWith(OpenApiTestApiBase.DescribedElsewhere.TrimEnd() + " One or more of:");
    }

    [Fact]
    public void every_parameter_of_a_contract_enum_points_at_the_corrected_schema() {
        foreach (string path in new[] { "/orders", "/orders/{state}" }) {
            JsonElement parameter = api.Document.GetProperty("paths").GetProperty(path)
                                       .GetProperty("get").GetProperty("parameters").EnumerateArray().First();

            Check.That(parameter.GetProperty("schema").GetProperty("$ref").GetString()).IsEqualTo("#/components/schemas/OrderState");
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

            Check.WithCustomMessage($"the document advertises '{advertised}' but the query string rejected it.")
                 .That(query.StatusCode).IsEqualTo(HttpStatusCode.OK);
            Check.WithCustomMessage($"the document advertises '{advertised}' but the route rejected it.")
                 .That(route.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    /// <summary>
    /// A single token naming no member is refused, and the document does not advertise it either.
    /// </summary>
    /// <remarks>
    /// Named for what it holds rather than for the converse of the test above, which would not be
    /// true. A non-<c>[Flags]</c> schema stays a closed list, so it deliberately under-promises: the
    /// server also accepts the comma-separated combinations whose result is a declared member, and
    /// the list cannot express them. The test below pins that asymmetry; the reasoning is in
    /// <c>docs/for-users/openapi.en.md</c>.
    /// </remarks>
    [Theory]
    [InlineData("Pending")]
    [InlineData("PENDING")]
    [InlineData("0")]
    [InlineData("bogus")]
    public async Task a_value_naming_no_member_is_rejected_and_never_advertised(string value) {
        string[] advertised = [.. api.Schema(nameof(OrderState)).GetProperty("enum").EnumerateArray().Select(v => v.GetString()!)];
        Check.That(advertised).Not.Contains(value);

        using HttpResponseMessage response = await api.Client.GetAsync("/orders?state=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The under-promise, held as a measurement rather than as a paragraph of documentation.
    /// </summary>
    /// <remarks>
    /// A comma separates values on every enum, so <c>"pending,shipped"</c> is <c>0 | 1</c>, which is a
    /// declared member and therefore binds. The closed list names only the members themselves. This
    /// direction is the safe one — a client that sends only what the document advertises is never
    /// refused — and it is why the schema is not a pattern: on an ordinary enum a pattern would
    /// advertise the combinations that resolve to no member at all, which the server answers 400.
    /// </remarks>
    [Theory]
    [InlineData("pending,shipped")]
    [InlineData("pending, shipped")]
    [InlineData("shipped,shipped")]
    public async Task a_combination_resolving_to_a_declared_member_binds_though_it_is_not_advertised(string value) {
        string[] advertised = [.. api.Schema(nameof(OrderState)).GetProperty("enum").EnumerateArray().Select(v => v.GetString()!)];
        Check.That(advertised).Not.Contains(value);

        using HttpResponseMessage response = await api.Client.GetAsync("/orders?state=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.WithCustomMessage($"'{value}' was refused, so the document no longer under-promises — it disagrees.")
             .That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
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
        Check.That(value).Matches(pattern);

        using HttpResponseMessage response = await api.Client.GetAsync("/tokens?scopes=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    /// <summary>
    /// A declared name carrying a comma is advertised as one value, and the server reads it as one.
    /// </summary>
    /// <remarks>
    /// The shape is only legal off <c>[Flags]</c>, and only because the serializer looks the whole
    /// value up as a name before splitting — this package refused it outright until that was
    /// measured. Held here because the closed list is where it becomes visible to a client: the
    /// document says <c>news,world</c>, and a reader that split on commas would send two values the
    /// server would answer 400 to. The two constituents are declared beside it, so the reading that
    /// wins is the one being asserted rather than the only one available.
    /// </remarks>
    [Theory]
    [InlineData("news,world", "NewsWorld")]
    [InlineData("news", "News")]
    public async Task a_declared_name_carrying_a_comma_is_advertised_and_bound_as_one_value(string value, string expected) {
        string[] advertised = [.. api.Schema(nameof(Topic)).GetProperty("enum").EnumerateArray().Select(v => v.GetString()!)];
        Check.That(advertised).Contains("news,world");

        using HttpResponseMessage response = await api.Client.GetAsync("/topics?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Contains(expected);
    }

    /// <summary>
    /// The same two words with a space between them are the combination, and the combination names no
    /// member — so the reading really does turn on the exact spelling rather than on the comma.
    /// </summary>
    /// <remarks>
    /// Refused for the reason every combination naming no member is refused outside the body, which
    /// predates this shape: <c>news | world</c> is 3 and no member holds 3. See
    /// <c>docs/for-users/limitations.en.md</c>. The document does not advertise it either, so the
    /// closed list stays honest.
    /// </remarks>
    [Fact]
    public async Task the_same_words_spelled_as_a_combination_are_not_that_name() {
        string[] advertised = [.. api.Schema(nameof(Topic)).GetProperty("enum").EnumerateArray().Select(v => v.GetString()!)];
        Check.That(advertised).Not.Contains("news, world");

        using HttpResponseMessage response = await api.Client.GetAsync("/topics?value=" + Uri.EscapeDataString("news, world"), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("Read")]
    [InlineData("read, bogus")]
    public async Task a_value_rejected_by_the_flags_pattern_is_rejected_by_the_server(string value) {
        string pattern = api.Schema(nameof(Scopes)).GetProperty("pattern").GetString()!;
        Check.WithCustomMessage($"'{value}' unexpectedly matches the advertised pattern.").That(Regex.IsMatch(value, pattern)).IsFalse();

        using HttpResponseMessage response = await api.Client.GetAsync("/tokens?scopes=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
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

        Check.That(schema.TryGetProperty("type", out _)).IsFalse();
        Check.That(schema.TryGetProperty("enum", out _)).IsTrue();
    }

    [Fact]
    public void stock_aspnetcore_documents_no_value_at_all_for_a_flags_enum() {
        JsonElement schema = api.Schema(nameof(Scopes));

        Check.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
        Check.That(schema.TryGetProperty("enum", out _)).IsFalse();
        Check.That(schema.TryGetProperty("pattern", out _)).IsFalse();
    }

}
