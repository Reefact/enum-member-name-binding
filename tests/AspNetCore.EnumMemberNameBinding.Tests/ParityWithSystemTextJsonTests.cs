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

    public static TheoryData<string> StatusInputs => new() {
        "available", "out_of_stock", "discontinued",
        "OutOfStock", "outofstock", "OUT_OF_STOCK", "Out_Of_Stock",
        "0", "1", "999", "-1",
        "unknown", "null",
        " available", "available ", " available ", "avail able",
        // A comma separates values on every enum, not only on a [Flags] one: System.Text.Json splits
        // before it looks at the type, exactly as Enum.Parse does. The combinations here are the ones
        // whose result is a declared member; the one whose result is not has a test of its own below.
        "available,", "available, ", "available,out_of_stock", " available , out_of_stock ",
        ",available", "available,,discontinued", "available,unknown"
    };

    public static TheoryData<string> PartialInputs => new() {
        "one", "One", "Two", "two", "TWO", "unknown",
        "one,", "one,Two", "One,two"
    };

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

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadBoundValue(response)).IsEqualTo("<null>");
    }

    [Fact]
    public async Task the_request_body_still_honours_the_contract() {
        using StringContent content = new("""{"Value":"out_of_stock"}""", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _api.Client.PostAsync("/status/body", content, TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadBoundValue(response)).IsEqualTo(nameof(ProductStatus.OutOfStock));
    }

    /// <summary>
    /// The one input the body accepts and no other channel does: a combination whose result names no
    /// declared member. It is the single documented hole in the parity above, and it is the
    /// platform's rather than this package's — ASP.NET Core's <c>EnumTypeModelBinder</c> refuses to
    /// bind an undefined value to a non-<c>[Flags]</c> enum, whichever converter produced it.
    /// </summary>
    /// <remarks>
    /// The control is what makes that claim a measurement: <c>PlainPriority</c> is an enum this
    /// package never touches, and <c>Normal,High</c> is refused there for exactly the same reason.
    /// Should the platform ever drop that check, this test fails and the limitation page is wrong.
    /// See <see href="https://github.com/Reefact/enum-member-name-binding/blob/main/docs/for-users/limitations.en.md" />.
    /// </remarks>
    [Fact]
    public async Task a_combination_naming_no_member_is_the_one_thing_only_the_body_accepts() {
        const string Contract = "out_of_stock,discontinued";
        const string Control  = "Normal,High";

        Check.That(DeserializeWithSystemTextJson<ProductStatus>(Contract)).IsEqualTo((ProductStatus)3);

        using HttpResponseMessage contract = await _api.Client.GetAsync("/status/query?value=" + Uri.EscapeDataString(Contract), TestContext.Current.CancellationToken);
        using HttpResponseMessage control = await _api.Client.GetAsync("/plain/query?value=" + Uri.EscapeDataString(Control), TestContext.Current.CancellationToken);

        Check.That(contract.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        Check.WithCustomMessage("an enum this package never touches must be refused for the same reason.")
             .That(control.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The same rule on a <c>[Flags]</c> enum, which reads as though it were exempt and is not.
    /// <see cref="Enum.IsDefined(Type, object)" /> cannot answer for a combination, so
    /// <c>EnumTypeModelBinder</c> compares the value's own text against its underlying number
    /// instead, and refuses the one that prints the number back. Two declared composites that
    /// overlap reach it: <c>3 | 6</c> is <c>7</c>, which decomposes into neither.
    /// </summary>
    /// <remarks>
    /// The control is the whole test. <c>PlainScopes</c> is the same shape untouched by this
    /// package and is refused for the same reason, so a contract enum binding <c>7</c> here would be
    /// more permissive than an ordinary one — which is what this suite exists to rule out.
    /// <para>
    /// The binder answered <see langword="true" /> for every <c>[Flags]</c> value until this test
    /// was written, and nothing caught it: every <c>[Flags]</c> fixture in the suite declared atoms,
    /// where the branch cannot be reached because each combination decomposes by construction. The
    /// reasoning in the binder said exactly that, and took it for a property of all enums.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task a_flags_combination_naming_no_member_is_refused_too() {
        const string Contract = "read_write,write_delete";
        const string Control  = "ReadWrite,WriteDelete";

        Check.WithCustomMessage("the body is the channel that still accepts it, which is what makes this a divergence rather than a rule.")
             .That(DeserializeWithSystemTextJson<Scopes>(Contract)).IsEqualTo((Scopes)7);

        using HttpResponseMessage contract = await _api.Client.GetAsync("/scopes/query?value=" + Uri.EscapeDataString(Contract), TestContext.Current.CancellationToken);
        using HttpResponseMessage control = await _api.Client.GetAsync("/plain-scopes/query?value=" + Uri.EscapeDataString(Control), TestContext.Current.CancellationToken);

        Check.That(contract.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        Check.WithCustomMessage("an enum this package never touches must be refused for the same reason.")
             .That(control.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The other half, without which the test above also passes on a binder that refuses every
    /// combination: a value that does decompose is still bound — on the enum whose members are
    /// composites, and on the one whose members are atoms.
    /// </summary>
    [Fact]
    public async Task a_flags_combination_that_does_name_members_still_binds() {
        using HttpResponseMessage composite = await _api.Client.GetAsync("/scopes/query?value=read_write", TestContext.Current.CancellationToken);
        using HttpResponseMessage atoms = await _api.Client.GetAsync("/permissions/query?value=" + Uri.EscapeDataString("read,write"), TestContext.Current.CancellationToken);

        Check.That(composite.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadBoundValue(composite)).IsEqualTo(nameof(Scopes.ReadWrite));

        Check.That(atoms.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadBoundValue(atoms)).IsEqualTo("Read, Write");
    }

    [Fact]
    public async Task a_rejected_value_produces_a_validation_error_not_a_default_value() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query?value=bogus", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Check.That(body).Contains("\"errors\"");
        Check.That(body).Contains("value");
    }

    private async Task AssertParity<TEnum>(string input, string url) where TEnum : struct, Enum {
        using HttpResponseMessage response = await _api.Client.GetAsync(url, TestContext.Current.CancellationToken);
        await AssertMatchesJson<TEnum>(input, response);
    }

    private static async Task AssertMatchesJson<TEnum>(string input, HttpResponseMessage response) where TEnum : struct, Enum {
        TEnum? expected = DeserializeWithSystemTextJson<TEnum>(input);

        if (expected is null) {
            Check.WithCustomMessage($"System.Text.Json rejects '{input}' for {typeof(TEnum).Name}, but the HTTP channel answered " + $"{(int)response.StatusCode} with '{await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}'.")
                 .That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

            return;
        }

        Check.WithCustomMessage($"System.Text.Json accepts '{input}' for {typeof(TEnum).Name} as '{expected}', but the HTTP channel " + $"answered {(int)response.StatusCode} with '{await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}'.")
             .That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        Check.That(await ReadBoundValue(response)).IsEqualTo(expected.Value.ToString());
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
