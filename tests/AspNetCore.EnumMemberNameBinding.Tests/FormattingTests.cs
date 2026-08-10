using System.ComponentModel;
using System.Globalization;
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

        Assert.Equal("first", contract.Format(Aliased.First));
        Assert.Equal("first", contract.Format(Aliased.Uno));
    }

    [Fact]
    public void both_alias_names_are_read_back_to_the_same_value() {
        EnumContract contract = EnumContract.For(typeof(Aliased));

        Assert.True(contract.TryParse("first", out object? first));
        Assert.True(contract.TryParse("uno", out object? uno));
        Assert.Equal(first, uno);
        Assert.Equal(Aliased.First, first);
    }

    [Fact]
    public void a_combination_without_a_name_of_its_own_is_written_as_a_list() {
        EnumContract contract = EnumContract.For(typeof(WithZero));

        Assert.Equal("read, write", contract.Format(WithZero.Read | WithZero.Write));
    }

    [Fact]
    public void a_combination_that_has_its_own_name_is_written_with_it() {
        EnumContract contract = EnumContract.For(typeof(Composite));

        Assert.Equal("all", contract.Format(Composite.Read | Composite.Write));
        Assert.Equal("all", contract.Format(Composite.All));
    }

    [Fact]
    public void a_zero_member_is_written_with_its_name() {
        Assert.Equal("none", EnumContract.For(typeof(WithZero)).Format(WithZero.None));
    }

    [Fact]
    public void a_value_carrying_an_undeclared_bit_has_no_public_name() {
        Assert.Null(EnumContract.For(typeof(WithZero)).Format((WithZero)8));
        Assert.Null(EnumContract.For(typeof(WithZero)).Format(WithZero.Read | (WithZero)8));
    }

    [Theory]
    [InlineData(WithZero.Read)]
    [InlineData(WithZero.Read | WithZero.Write)]
    [InlineData(WithZero.None)]
    public void what_is_written_can_be_read_back(WithZero value) {
        EnumContract contract = EnumContract.For(typeof(WithZero));

        string? written = contract.Format(value);

        Assert.NotNull(written);
        Assert.True(contract.TryParse(written, out object? read));
        Assert.Equal(value, read);
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

        Assert.Equal(expected, EnumContract.For(typeof(WithZero)).Format(value));
    }

    [Fact]
    public void the_type_converter_writes_the_public_name() {
        TypeConverter converter = new EnumMemberNameConverter(typeof(ProductStatus));

        Assert.Equal("out_of_stock", converter.ConvertToString(ProductStatus.OutOfStock));
        Assert.Equal(ProductStatus.OutOfStock, converter.ConvertFrom(null, CultureInfo.InvariantCulture, "out_of_stock"));
    }

    /// <summary>
    /// A value that is not a string is the base converter's business, and this one hands it over
    /// rather than inventing an answer. Asserted through a conversion only the base performs — an
    /// <see cref="Enum" /> array, which it combines — so it proves the hand-over happened rather
    /// than that something merely refused.
    /// </summary>
    [Fact]
    public void the_type_converter_defers_a_value_that_is_not_a_string() {
        TypeConverter converter = new EnumMemberNameConverter(typeof(ProductStatus));

        object? combined = converter.ConvertFrom(null, CultureInfo.InvariantCulture,
                                                 new Enum[] { ProductStatus.Available, ProductStatus.OutOfStock });

        Assert.Equal(ProductStatus.Available | ProductStatus.OutOfStock, combined);
        Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(null, CultureInfo.InvariantCulture, 1));
    }

    /// <summary>
    /// Characterizes a real gap: ASP.NET Core formats route values without consulting
    /// <c>TypeDescriptor</c>, so a link built from the enum value carries the C# name and this very
    /// API answers 400 to it. It cannot be corrected from a TypeConverter, hence
    /// <see cref="EnumMemberNames.GetPublicName" />.
    /// </summary>
    [Fact]
    public async Task a_link_built_from_the_enum_value_carries_the_csharp_name_and_is_refused() {
        string link = await ReadLink("/status/link-raw");

        Assert.Equal("/status/route/OutOfStock", link);

        using HttpResponseMessage response = await _api.Client.GetAsync(link, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task a_link_built_from_the_public_name_is_correct() {
        Assert.Equal("/status/route/out_of_stock", await ReadLink("/status/link"));
    }

    [Fact]
    public void the_public_name_of_a_non_contract_enum_is_null() {
        Assert.Null(EnumMemberNames.GetPublicName(PlainPriority.High));
        Assert.Equal("out_of_stock", EnumMemberNames.GetPublicName(ProductStatus.OutOfStock));
    }

    [Fact]
    public async Task a_link_built_from_the_public_name_is_accepted_back_by_the_binder() {
        string link = await ReadLink("/status/link");

        using HttpResponseMessage response = await _api.Client.GetAsync(link, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument bound = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(nameof(ProductStatus.OutOfStock), bound.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task a_composite_flags_link_round_trips() {
        string link = await ReadLink("/permissions/link");

        Assert.Contains("read", link, StringComparison.Ordinal);
        Assert.Contains("write", link, StringComparison.Ordinal);

        using HttpResponseMessage response = await _api.Client.GetAsync(link, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument bound = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Read, Write", bound.RootElement.GetProperty("value").GetString());
    }

    private async Task<string> ReadLink(string url) {
        using HttpResponseMessage response = await _api.Client.GetAsync(url, TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("value").GetString()!;
    }

}
