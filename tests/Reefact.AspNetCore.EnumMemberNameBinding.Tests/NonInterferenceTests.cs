using System.Net;
using System.Text.Json;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>
/// Enabling the library must not change the behaviour of enums that declare no contract.
/// This is the regression suite for the failure mode that makes a binding library dangerous:
/// silently altering an application it was not asked to touch.
/// </summary>
[Collection(nameof(TestApiCollection))]
public sealed class NonInterferenceTests {

    private readonly TestApi _api;

    public NonInterferenceTests(TestApi api) {
        _api = api;
    }

    [Theory]
    [InlineData("High", "High")]
    [InlineData("high", "High")]
    [InlineData("HIGH", "High")]
    [InlineData("1", "Normal")]
    public async Task a_plain_enum_keeps_the_stock_binding_behaviour(string input, string expected) {
        using HttpResponseMessage response = await _api.Client.GetAsync("/plain/query?value=" + input, TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadValue(response)).IsEqualTo(expected);
    }

    private static async Task<string> ReadValue(HttpResponseMessage response) {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("value").GetString()!;
    }

    /// <summary>
    /// Registering an enum must not make it stricter than an enum left alone. A comma separates
    /// values on any enum — `Enum.Parse` has always split on it, so stock ASP.NET Core accepts
    /// `Low,Normal` on a query string — and a contract enum has to accept the same shape, spelled
    /// with the declared names.
    /// </summary>
    /// <remarks>
    /// The two halves are asserted together on purpose. Each alone reads as a fact about one enum;
    /// side by side they are the comparison, which is the thing that must not drift.
    /// </remarks>
    [Fact]
    public async Task a_contract_enum_accepts_a_comma_list_exactly_as_an_untouched_enum_does() {
        using HttpResponseMessage control = await _api.Client.GetAsync("/plain/query?value=Low,Normal", TestContext.Current.CancellationToken);
        using HttpResponseMessage contract = await _api.Client.GetAsync("/status/query?value=available,out_of_stock", TestContext.Current.CancellationToken);

        Check.That(control.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadValue(control)).IsEqualTo(nameof(PlainPriority.Normal));

        Check.That(contract.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(await ReadValue(contract)).IsEqualTo(nameof(ProductStatus.OutOfStock));
    }

    [Theory]
    [InlineData("999")]
    [InlineData("bogus")]
    public async Task a_plain_enum_keeps_the_stock_validation(string input) {
        using HttpResponseMessage response = await _api.Client.GetAsync("/plain/query?value=" + input, TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task a_plain_enum_keeps_its_numeric_wire_format() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/plain/serialized", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // The global JsonStringEnumConverter factory is never installed, so a non-contract enum
        // is still written as a number.
        Check.That(document.RootElement.GetProperty("value").ValueKind).IsEqualTo(JsonValueKind.Number);
        Check.That(document.RootElement.GetProperty("value").GetInt32()).IsEqualTo((int)PlainPriority.High);
    }

    [Fact]
    public async Task a_contract_enum_is_written_with_its_public_name_by_a_minimal_api_too() {
        // Minimal APIs, and the OpenAPI document generator, read Http.Json.JsonOptions rather than
        // the MVC options. Configuring only the latter would leave this endpoint writing a number.
        using HttpResponseMessage response = await _api.Client.GetAsync("/minimal/contract-serialized", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Check.That(document.RootElement.GetProperty("value").GetString()).IsEqualTo("out_of_stock");
    }

    [Fact]
    public async Task a_plain_enum_keeps_its_numeric_wire_format_in_a_minimal_api_too() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/minimal/plain-serialized", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Check.That(document.RootElement.GetProperty("value").ValueKind).IsEqualTo(JsonValueKind.Number);
    }

    [Fact]
    public async Task a_contract_enum_is_written_with_its_public_name() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/serialized", TestContext.Current.CancellationToken);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Check.That(document.RootElement.GetProperty("value").GetString()).IsEqualTo("out_of_stock");
    }

}
