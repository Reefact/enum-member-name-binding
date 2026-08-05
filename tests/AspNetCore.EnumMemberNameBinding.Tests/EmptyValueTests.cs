using System.Net;
using System.Text.Json;

namespace AspNetCore.EnumMemberNameBinding.Tests;

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
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query?value=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A documented divergence from <c>System.Text.Json</c>, which rejects <c>""</c>.
    /// ASP.NET Core's <c>SimpleTypeModelBinder</c> treats an empty value for a nullable type as an
    /// absent value before any <c>TypeConverter</c> is consulted, so this is not reachable from here.
    /// </summary>
    [Fact]
    public async Task an_empty_query_value_binds_to_null_for_a_nullable_enum() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query-nullable?value=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("<null>", document.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task an_empty_header_is_rejected() {
        using HttpRequestMessage request = new(HttpMethod.Get, "/status/header");
        request.Headers.TryAddWithoutValidation("X-Status", string.Empty);
        using HttpResponseMessage response = await _api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A non-nullable enum parameter with no value at all binds to the first member and returns 200.
    /// That is stock ASP.NET Core behaviour for value types, not something this package introduces —
    /// the control below proves it on an enum the package never touches. Use <c>TEnum?</c> or
    /// <c>[Required]</c> to get a 400.
    /// </summary>
    [Fact]
    public async Task an_absent_value_binds_the_default_member_exactly_as_it_does_without_this_package() {
        using HttpResponseMessage contract = await _api.Client.GetAsync("/status/query");
        using HttpResponseMessage control = await _api.Client.GetAsync("/plain/query");

        Assert.Equal(HttpStatusCode.OK, control.StatusCode);
        Assert.Equal(nameof(PlainPriority.Low), await ReadValue(control));

        Assert.Equal(HttpStatusCode.OK, contract.StatusCode);
        Assert.Equal(nameof(ProductStatus.Available), await ReadValue(contract));
    }

    [Fact]
    public async Task an_absent_value_is_not_an_empty_value() {
        using HttpResponseMessage response = await _api.Client.GetAsync("/status/query-nullable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<null>", await ReadValue(response));
    }

    private static async Task<string> ReadValue(HttpResponseMessage response) {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("value").GetString()!;
    }

}
