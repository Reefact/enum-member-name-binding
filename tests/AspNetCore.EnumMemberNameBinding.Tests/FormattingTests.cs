using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Writing a value back out — numeric aliases and composite <c>[Flags]</c> values in particular.
/// </summary>
[Collection(nameof(TestApiCollection))]
public sealed class FormattingTests {

    private static readonly JsonSerializerOptions Oracle = new() {
        Converters = { new JsonStringEnumConverter<WithZero>(namingPolicy: null, allowIntegerValues: false) }
    };

    private readonly TestApi _api;

    public FormattingTests(TestApi api) {
        _api = api;
    }

    public enum Aliased {

        [JsonStringEnumMemberName("first")] First = 1,
        [JsonStringEnumMemberName("uno")]   Uno   = 1,
        [JsonStringEnumMemberName("second")] Second = 2

    }

    [Flags]
    public enum Composite {

        [JsonStringEnumMemberName("read")]  Read  = 1,
        [JsonStringEnumMemberName("write")] Write = 2,
        [JsonStringEnumMemberName("all")]   All   = 3

    }

    [Flags]
    public enum WithZero {

        [JsonStringEnumMemberName("none")]  None  = 0,
        [JsonStringEnumMemberName("read")]  Read  = 1,
        [JsonStringEnumMemberName("write")] Write = 2

    }

    [Fact]
    public void an_alias_is_written_with_the_first_declared_name() {
        EnumContract contract = EnumContract.For(typeof(Aliased));

        Check.That(contract.Format(Aliased.First)).IsEqualTo("first");
        Check.That(contract.Format(Aliased.Uno)).IsEqualTo("first");
    }

    [Fact]
    public void both_alias_names_are_read_back_to_the_same_value() {
        EnumContract contract = EnumContract.For(typeof(Aliased));

        Check.That(contract.TryParse("first", out object? first)).IsTrue();
        Check.That(contract.TryParse("uno", out object? uno)).IsTrue();
        Check.That(uno).IsEqualTo(first);
        Check.That(first).IsEqualTo(Aliased.First);
    }

    [Fact]
    public void a_combination_without_a_name_of_its_own_is_written_as_a_list() {
        EnumContract contract = EnumContract.For(typeof(WithZero));

        Check.That(contract.Format(WithZero.Read | WithZero.Write)).IsEqualTo("read, write");
    }

    [Fact]
    public void a_combination_that_has_its_own_name_is_written_with_it() {
        EnumContract contract = EnumContract.For(typeof(Composite));

        Check.That(contract.Format(Composite.Read | Composite.Write)).IsEqualTo("all");
        Check.That(contract.Format(Composite.All)).IsEqualTo("all");
    }

    [Fact]
    public void a_zero_member_is_written_with_its_name() {
        Check.That(EnumContract.For(typeof(WithZero)).Format(WithZero.None)).IsEqualTo("none");
    }

    [Fact]
    public void a_value_carrying_an_undeclared_bit_has_no_public_name() {
        Check.That(EnumContract.For(typeof(WithZero)).Format((WithZero)8)).IsNull();
        Check.That(EnumContract.For(typeof(WithZero)).Format(WithZero.Read | (WithZero)8)).IsNull();
    }

    [Theory]
    [InlineData(WithZero.Read)]
    [InlineData(WithZero.Read | WithZero.Write)]
    [InlineData(WithZero.None)]
    public void what_is_written_can_be_read_back(WithZero value) {
        EnumContract contract = EnumContract.For(typeof(WithZero));

        string? written = contract.Format(value);

        Check.That(written).IsNotNull();
        Check.That(contract.TryParse(written!, out object? read)).IsTrue();
        Check.That(read).IsEqualTo(value);
    }

    /// <summary>
    /// What is written must also be what <c>System.Text.Json</c> writes, or a response body and a
    /// generated link would disagree.
    /// </summary>
    [Theory]
    [InlineData(WithZero.None)]
    [InlineData(WithZero.Read)]
    [InlineData(WithZero.Read | WithZero.Write)]
    public void what_is_written_matches_what_system_text_json_writes(WithZero value) {
        string expected = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, Oracle))!;

        Check.That(EnumContract.For(typeof(WithZero)).Format(value)).IsEqualTo(expected);
    }

    /// <summary>
    /// What the binder puts on the wire and what it takes back off it are the same vocabulary. Read
    /// off the contract rather than over HTTP, because writing a name is not something any channel
    /// does on the way out — a response body is System.Text.Json's business and a link is the
    /// caller's, through <see cref="EnumMemberNames.GetPublicName" />.
    /// </summary>
    [Fact]
    public void a_declared_name_is_written_and_read_back_by_the_same_contract() {
        EnumContract contract = EnumContract.For(typeof(ProductStatus));

        Check.That(contract.Format(ProductStatus.OutOfStock)).IsEqualTo("out_of_stock");
        Check.That(contract.TryParse("out_of_stock", out object? parsed)).IsTrue();
        Check.That(parsed).IsEqualTo(ProductStatus.OutOfStock);
    }

    /// <summary>
    /// A value the contract cannot name is answered with <see langword="null" /> rather than with the
    /// number, so a caller can tell "no public name" from a public name that happens to be numeric.
    /// </summary>
    [Fact]
    public void a_flags_value_carrying_an_undeclared_bit_is_not_named() {
        Check.That(EnumContract.For(typeof(Permissions)).Format((Permissions)64)).IsNull();
    }

    /// <summary>
    /// Characterizes a real gap: ASP.NET Core formats a route value with the value's own
    /// <c>ToString()</c>, so a link built from the enum value carries the C# name and this very API
    /// answers 400 to it. No binder is reached on the way out, hence
    /// <see cref="EnumMemberNames.GetPublicName" />.
    /// </summary>
    [Fact]
    public async Task a_link_built_from_the_enum_value_carries_the_csharp_name_and_is_refused() {
        string link = await ReadLink("/status/link-raw");

        Check.That(link).IsEqualTo("/status/route/OutOfStock");

        using HttpResponseMessage response = await _api.Client.GetAsync(link, TestContext.Current.CancellationToken);
        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task a_link_built_from_the_public_name_is_correct() {
        Check.That(await ReadLink("/status/link")).IsEqualTo("/status/route/out_of_stock");
    }

    [Fact]
    public void the_public_name_of_a_non_contract_enum_is_null() {
        Check.That(EnumMemberNames.GetPublicName(PlainPriority.High)).IsNull();
        Check.That(EnumMemberNames.GetPublicName(ProductStatus.OutOfStock)).IsEqualTo("out_of_stock");
    }

    [Fact]
    public async Task a_link_built_from_the_public_name_is_accepted_back_by_the_binder() {
        string link = await ReadLink("/status/link");

        using HttpResponseMessage response = await _api.Client.GetAsync(link, TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument bound = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Check.That(bound.RootElement.GetProperty("value").GetString()).IsEqualTo(nameof(ProductStatus.OutOfStock));
    }

    [Fact]
    public async Task a_composite_flags_link_round_trips() {
        string link = await ReadLink("/permissions/link");

        Check.That(link).Contains("read");
        Check.That(link).Contains("write");

        using HttpResponseMessage response = await _api.Client.GetAsync(link, TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument bound = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Check.That(bound.RootElement.GetProperty("value").GetString()).IsEqualTo("Read, Write");
    }

    private async Task<string> ReadLink(string url) {
        using HttpResponseMessage response = await _api.Client.GetAsync(url, TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("value").GetString()!;
    }

}
