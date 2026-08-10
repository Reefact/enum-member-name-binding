using System.Net;
using System.Text.Json;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Everything the binding does around the parse: the messages it leaves in <c>ModelState</c>, and
/// the shapes MVC hands it that are not one value for one parameter.
/// </summary>
/// <remarks>
/// Characterization, in the strict sense — none of this is a decision taken here. It is what
/// ASP.NET Core's own <c>SimpleTypeModelBinder</c> and <c>EnumTypeModelBinder</c> produce, and this
/// package binds through a replacement for them. Writing down what the originals answered is the
/// only way to know the replacement stayed faithful, so these were recorded against the previous
/// <c>TypeConverter</c> implementation before the binder existed, and must keep answering the same.
/// </remarks>
[Collection(nameof(TestApiCollection))]
public sealed class ModelBindingBehaviourTests {

    private readonly TestApi _api;

    public ModelBindingBehaviourTests(TestApi api) {
        _api = api;
    }

    /// <summary>
    /// Not the converter's own sentence. ASP.NET Core discards the message of a
    /// <see cref="FormatException" /> raised while converting and writes its own, so the rich
    /// "allowed values are…" text never reaches the client — only the log.
    /// </summary>
    [Fact]
    public async Task an_unknown_value_is_reported_in_the_words_aspnet_core_chooses() {
        Check.That(await ErrorFor("/status/query?value=bogus", "value")).IsEqualTo("The value 'bogus' is not valid.");
    }

    /// <summary>
    /// A different sentence, from a different place: an empty value never reaches the parse at all,
    /// so it is reported as a value that must not be null rather than as one that failed to convert.
    /// </summary>
    [Fact]
    public async Task an_empty_required_value_is_reported_in_different_words_again() {
        Check.That(await ErrorFor("/status/query?value=", "value")).IsEqualTo("The value '' is invalid.");
    }

    /// <summary>
    /// A third sentence, and the reason all three are pinned: which one a client receives depends on
    /// where the enum sits, and a binder is free to get that wrong without failing anything else.
    /// </summary>
    [Fact]
    public async Task the_same_value_reached_as_a_property_names_the_property() {
        Check.That(await ErrorFor("/status/model?Value=OutOfStock", "Value")).IsEqualTo("The value 'OutOfStock' is not valid for Value.");
    }

    [Fact]
    public async Task an_array_of_contract_enums_binds_element_by_element() {
        Check.That(await Bound("/status/array?value=available&value=discontinued")).IsEqualTo("Available|Discontinued");
    }

    /// <summary>
    /// A repeated key is joined with a comma for a <c>[Flags]</c> enum and only the first value is
    /// read for any other — ASP.NET Core's rule, decided from the metadata before this package sees
    /// anything. The two are asserted together because the difference between them is the rule.
    /// </summary>
    [Fact]
    public async Task a_repeated_key_is_combined_for_a_flags_enum_and_not_for_another() {
        Check.That(await Bound("/permissions/query?value=read&value=write")).IsEqualTo("Read, Write");
        Check.That(await Bound("/status/query?value=available&value=discontinued")).IsEqualTo(nameof(ProductStatus.Available));
    }

    private async Task<string> Bound(string url) {
        using HttpResponseMessage response = await _api.Client.GetAsync(url, TestContext.Current.CancellationToken);

        Check.WithCustomMessage($"{url} answered {(int)response.StatusCode}.").That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("value").GetString()!;
    }

    private async Task<string> ErrorFor(string url, string key) {
        using HttpResponseMessage response = await _api.Client.GetAsync(url, TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("errors").GetProperty(key).EnumerateArray().Single().GetString()!;
    }

}
