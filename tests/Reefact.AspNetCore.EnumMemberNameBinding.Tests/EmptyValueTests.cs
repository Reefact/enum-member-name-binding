using System.Net;
using System.Text.Json;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// A present-but-empty value is its own case: it is neither a valid name nor an absent value.
/// </summary>
[Collection(nameof(TestApiCollection))]
public sealed class EmptyValueTests {

    private readonly TestApi _api;

    public EmptyValueTests(TestApi api) {
        _api = api;
    }

    [Fact]
    public async Task an_empty_query_value_is_rejected_for_a_required_enum() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query?value=", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A documented divergence from <c>System.Text.Json</c>, which rejects <c>""</c>.
    /// An empty value for a nullable type is settled as an absent one before any parse is reached,
    /// which is ASP.NET Core's rule and is reproduced rather than chosen, so this is out of reach.
    /// </summary>
    [Fact]
    public async Task an_empty_query_value_binds_to_null_for_a_nullable_enum() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query-nullable?value=", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Check.That(document.RootElement.GetProperty("value").GetString()).IsEqualTo("<null>");
    }

    [Fact]
    public async Task an_empty_header_is_rejected() {
        using HttpRequestMessage request = new(HttpMethod.Get, "/status/header");
        request.Headers.TryAddWithoutValidation("X-Status", string.Empty);
        using HttpResponseMessage response = await _api.Client.SendAsync(request, TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A non-nullable enum parameter with no value at all binds to the first member and returns 200.
    /// That is stock ASP.NET Core behaviour for value types, not something this package introduces —
    /// the control below proves it on an enum the package never touches. Use <c>TEnum?</c> or
    /// <c>[Required]</c> to get a 400.
    /// </summary>
    [Fact]
    public async Task an_absent_value_binds_the_default_member_exactly_as_it_does_without_this_package() {
        using HttpResponseMessage contract = await _api.Client.GetAsync("/status/query", TestContext.Current.CancellationToken);
        using HttpResponseMessage control = await _api.Client.GetAsync("/plain/query", TestContext.Current.CancellationToken);

        Check.That(control.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadValue(control)).IsEqualTo(nameof(PlainPriority.Low));

        Check.That(contract.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadValue(contract)).IsEqualTo(nameof(ProductStatus.Available));
    }

    [Fact]
    public async Task an_absent_value_is_not_an_empty_value() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query-nullable", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadValue(response)).IsEqualTo("<null>");
    }

    /// <summary>
    /// The contract's own answer to a value that is present but blank, whitespace included: it names
    /// no member, so nothing is parsed.
    /// </summary>
    /// <remarks>
    /// Asserted on the contract rather than over a channel, because no channel can deliver this case.
    /// An empty value is settled before any parse is reached — which is what the two tests above
    /// characterize. The behaviour still has to be right for the day something else calls it.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void a_blank_value_names_no_member(string value) {
        Check.That(EnumContract.For(typeof(ProductStatus)).TryParse(value, out object? parsed)).IsFalse();
        Check.That(parsed).IsNull();
    }

    private static async Task<string> ReadValue(HttpResponseMessage response) {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("value").GetString()!;
    }

}
