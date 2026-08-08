using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// The core promise of this library: every input channel accepts exactly the vocabulary
/// <c>System.Text.Json</c> accepts in the request body.
/// </summary>
/// <remarks>
/// These tests do not assert hand-written expectations — they use <see cref="JsonSerializer" />
/// itself as the oracle. If .NET changes its enum matching rules, these tests fail, which is the
/// point: parity is verified, never declared.
/// </remarks>
[Collection(nameof(TestApiCollection))]
public sealed class ParityWithSystemTextJsonTests {

    private readonly TestApi _api;

    public ParityWithSystemTextJsonTests(TestApi api) {
        _api = api;
    }

    // Built with an object initializer rather than a collection expression: the latter trips CA1825
    // on the 10.0.100 analyzers, which is the SDK floor declared in global.json and the one CI uses.
    [SuppressMessage("Style", "IDE0028:Simplify collection initialization",
                     Justification = "The collection expression this rule asks for trips CA1825 on the "
                                     + "10.0.100 analyzers — the SDK floor in global.json, and one of the two "
                                     + "CI legs, where a warning is an error. The object initializer is what "
                                     + "keeps that leg green.")]
    public static TheoryData<string> StatusInputs => new() {
        "available", "out_of_stock", "discontinued",
        "OutOfStock", "outofstock", "OUT_OF_STOCK", "Out_Of_Stock",
        "0", "1", "999", "-1",
        "unknown", "null",
        " available", "available ", " available ", "avail able"
    };

    [SuppressMessage("Style", "IDE0028:Simplify collection initialization",
                     Justification = "The collection expression this rule asks for trips CA1825 on the "
                                     + "10.0.100 analyzers — the SDK floor in global.json, and one of the two "
                                     + "CI legs, where a warning is an error. The object initializer is what "
                                     + "keeps that leg green.")]
    public static TheoryData<string> PartialInputs => new() { "one", "One", "Two", "two", "TWO", "unknown" };

    [SuppressMessage("Style", "IDE0028:Simplify collection initialization",
                     Justification = "The collection expression this rule asks for trips CA1825 on the "
                                     + "10.0.100 analyzers — the SDK floor in global.json, and one of the two "
                                     + "CI legs, where a warning is an error. The object initializer is what "
                                     + "keeps that leg green.")]
    public static TheoryData<string> PermissionInputs => new() {
        "read", "write", "read, write", "read,write", "read, delete", "read, write, delete",
        "Read", "read, bogus", "bogus", "3",
        // Whitespace and comma handling, characterized against System.Text.Json rather than assumed.
        " read", "read ", " read ", " read,write", "read,write ", " read, write ",
        "read , write", "read,  write", "read,", "read,write,", ",read", "read,,write", "read, ,write", "read,,"
    };

    [Theory]
    [MemberData(nameof(StatusInputs))]
    public async Task query_string_accepts_exactly_what_the_body_accepts(string input) {
        await AssertParity<ProductStatus>(input, "/status/query?value=" + Uri.EscapeDataString(input));
    }

    [Theory]
    [MemberData(nameof(StatusInputs))]
    public async Task route_value_accepts_exactly_what_the_body_accepts(string input) {
        await AssertParity<ProductStatus>(input, "/status/route/" + Uri.EscapeDataString(input));
    }

    [Theory]
    [MemberData(nameof(StatusInputs))]
    public async Task nullable_query_string_accepts_exactly_what_the_body_accepts(string input) {
        await AssertParity<ProductStatus>(input, "/status/query-nullable?value=" + Uri.EscapeDataString(input));
    }

    [Theory]
    [MemberData(nameof(StatusInputs))]
    public async Task header_accepts_exactly_what_the_body_accepts(string input) {
        using HttpRequestMessage request = new(HttpMethod.Get, "/status/header");
        request.Headers.TryAddWithoutValidation("X-Status", input);
        using HttpResponseMessage response = await _api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertMatchesJson<ProductStatus>(input, response);
    }

    [Theory]
    [MemberData(nameof(StatusInputs))]
    public async Task form_field_accepts_exactly_what_the_body_accepts(string input) {
        using FormUrlEncodedContent content = new([new KeyValuePair<string, string>("value", input)]);
        using HttpResponseMessage response = await _api.Client.PostAsync("/status/form", content, TestContext.Current.CancellationToken);

        await AssertMatchesJson<ProductStatus>(input, response);
    }

    [Theory]
    [MemberData(nameof(PartialInputs))]
    public async Task partially_annotated_enum_follows_the_same_rules_as_the_body(string input) {
        await AssertParity<PartiallyAnnotated>(input, "/partial/query?value=" + Uri.EscapeDataString(input));
    }

    [Theory]
    [MemberData(nameof(PermissionInputs))]
    public async Task flags_enum_follows_the_same_rules_as_the_body(string input) {
        await AssertParity<Permissions>(input, "/permissions/query?value=" + Uri.EscapeDataString(input));
    }

    [Fact]
    public async Task an_absent_nullable_value_binds_to_null() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query-nullable", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<null>", await ReadBoundValue(response));
    }

    [Fact]
    public async Task the_request_body_still_honours_the_contract() {
        using StringContent content = new("""{"Value":"out_of_stock"}""", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _api.Client.PostAsync("/status/body", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nameof(ProductStatus.OutOfStock), await ReadBoundValue(response));
    }

    [Fact]
    public async Task a_rejected_value_produces_a_validation_error_not_a_default_value() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query?value=bogus", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"errors\"", body, StringComparison.Ordinal);
        Assert.Contains("value", body, StringComparison.Ordinal);
    }

    private async Task AssertParity<TEnum>(string input, string url) where TEnum : struct, Enum {
        using HttpResponseMessage response = await _api.Client.GetAsync(url, TestContext.Current.CancellationToken);
        await AssertMatchesJson<TEnum>(input, response);
    }

    private static async Task AssertMatchesJson<TEnum>(string input, HttpResponseMessage response) where TEnum : struct, Enum {
        TEnum? expected = DeserializeWithSystemTextJson<TEnum>(input);

        if (expected is null) {
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                        $"System.Text.Json rejects '{input}' for {typeof(TEnum).Name}, but the HTTP channel answered " +
                        $"{(int)response.StatusCode} with '{await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}'.");

            return;
        }

        Assert.True(response.StatusCode == HttpStatusCode.OK,
                    $"System.Text.Json accepts '{input}' for {typeof(TEnum).Name} as '{expected}', but the HTTP channel " +
                    $"answered {(int)response.StatusCode} with '{await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}'.");

        Assert.Equal(expected.Value.ToString(), await ReadBoundValue(response));
    }

    /// <summary>The oracle: what does System.Text.Json make of this string?</summary>
    private static TEnum? DeserializeWithSystemTextJson<TEnum>(string input) where TEnum : struct, Enum {
        try {
            return JsonSerializer.Deserialize<TEnum>(JsonSerializer.Serialize(input), Oracle<TEnum>.Options);
        } catch (JsonException) {
            return null;
        }
    }

    private static class Oracle<TEnum> where TEnum : struct, Enum {

        internal static readonly JsonSerializerOptions Options = new() {
            Converters = { new JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false) }
        };

    }

    private static async Task<string> ReadBoundValue(HttpResponseMessage response) {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("value").GetString()
            ?? throw new InvalidOperationException("No bound value in the response.");
    }

}
