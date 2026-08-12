using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Diagnostics.CodeAnalysis;

using DiagnosticCatalog.NetAnalyzers;

namespace Reefact.AspNetCore.EnumMemberNameBinding.Tests;

public enum Portability {

    [JsonStringEnumMemberName("plain")]         Plain,
    [JsonStringEnumMemberName("with/slash")]    Slash,
    [JsonStringEnumMemberName("with\nlf")]      LineFeed,
    [JsonStringEnumMemberName("withéacc")] NonAscii,
    [JsonStringEnumMemberName("with?question")] Question,
    [JsonStringEnumMemberName("with#hash")]     Hash,
    [JsonStringEnumMemberName("with&amp")]      Ampersand,
    [JsonStringEnumMemberName("with%percent")]  Percent,
    [JsonStringEnumMemberName("with space")]    Space,
    [JsonStringEnumMemberName("with\ttab")]     Tab

}

[ApiController]
public sealed class PortabilityController : ControllerBase {

    [HttpGet("/portability/route/{value}")] public IActionResult R([FromRoute] Portability value) => Ok(new { value = value.ToString() });
    [HttpGet("/portability/query")]         public IActionResult Q([FromQuery] Portability value) => Ok(new { value = value.ToString() });
    [HttpGet("/portability/header")]        public IActionResult H([FromHeader(Name = "X-V")] Portability value) => Ok(new { value = value.ToString() });
    [HttpPost("/portability/form")]         public IActionResult F([FromForm] Portability value) => Ok(new { value = value.ToString() });
    [HttpPost("/portability/body")]
    [SuppressMessage(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = SuppressionJustification.CA1062.ArgumentSuppliedByTheFramework)]
    public IActionResult B([FromBody] Payload payload) => Ok(new { value = payload.Value.ToString() });

    public sealed class Payload {

        public Portability Value { get; set; }

    }

}

/// <summary>
/// The evidence behind EMN0006. The promise of one contract on every channel only holds for names
/// every channel can carry, and this pins which ones can — measured, not read off a specification.
/// </summary>
public sealed class ChannelPortabilityTests : IAsyncLifetime {

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services
               .AddControllers()
               .AddApplicationPart(typeof(PortabilityController).Assembly)
               .AddEnumMemberNameBinding(options => options.AddEnum<Portability>());

        _app = builder.Build();
        _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    public async ValueTask DisposeAsync() {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    /// <summary>
    /// Names EMN0006 leaves alone: every channel carries them.
    /// <para>
    /// The tab is here because EMN0006 reported it for a while, on the grounds that RFC 9110 forbids
    /// control characters in a field value. It does not forbid this one —
    /// <c>field-content = field-vchar [ 1*( SP / HTAB / field-vchar ) field-vchar ]</c> admits it
    /// exactly where a space is admitted — and the measurement below is what settles it either way.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("plain", nameof(Portability.Plain))]
    [InlineData("with?question", nameof(Portability.Question))]
    [InlineData("with#hash", nameof(Portability.Hash))]
    [InlineData("with&amp", nameof(Portability.Ampersand))]
    [InlineData("with%percent", nameof(Portability.Percent))]
    [InlineData("with space", nameof(Portability.Space))]
    [InlineData("with\ttab", nameof(Portability.Tab))]
    public async Task a_portable_name_binds_on_every_channel(string name, string expected) {
        Check.That(await Route(name)).IsEqualTo(expected);
        Check.That(await Query(name)).IsEqualTo(expected);
        Check.That(await Header(name)).IsEqualTo(expected);
        Check.That(await Form(name)).IsEqualTo(expected);
        Check.That(await Body(name)).IsEqualTo(expected);
    }

    [Fact]
    public async Task a_slash_is_refused_in_a_route_segment_and_nowhere_else() {
        const string Name = "with/slash";
        string expected = nameof(Portability.Slash);

        using HttpResponseMessage route = await _client.GetAsync("/portability/route/" + Uri.EscapeDataString(Name), TestContext.Current.CancellationToken);
        Check.That(route.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        Check.That(await Query(Name)).IsEqualTo(expected);
        Check.That(await Header(Name)).IsEqualTo(expected);
        Check.That(await Form(Name)).IsEqualTo(expected);
        Check.That(await Body(Name)).IsEqualTo(expected);
    }

    [Fact]
    public async Task a_line_break_is_refused_in_a_header_and_nowhere_else() {
        const string Name = "with\nlf";
        string expected = nameof(Portability.LineFeed);

        using HttpRequestMessage request = new(HttpMethod.Get, "/portability/header");
        request.Headers.TryAddWithoutValidation("X-V", Name);
        using HttpResponseMessage header = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Check.That(header.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        Check.That(await Route(Name)).IsEqualTo(expected);
        Check.That(await Query(Name)).IsEqualTo(expected);
        Check.That(await Form(Name)).IsEqualTo(expected);
        Check.That(await Body(Name)).IsEqualTo(expected);
    }

    /// <summary>
    /// The client refuses to put it on the wire, so the request never reaches the server — which is
    /// worse than a 400, not better.
    /// </summary>
    [Fact]
    public async Task a_non_ascii_name_cannot_even_be_sent_in_a_header() {
        const string Name = "withéacc";
        string expected = nameof(Portability.NonAscii);

        using HttpRequestMessage request = new(HttpMethod.Get, "/portability/header");
        request.Headers.TryAddWithoutValidation("X-V", Name);
        Check.ThatCode(() => _client.SendAsync(request, TestContext.Current.CancellationToken)).Throws<HttpRequestException>();

        Check.That(await Route(Name)).IsEqualTo(expected);
        Check.That(await Query(Name)).IsEqualTo(expected);
        Check.That(await Form(Name)).IsEqualTo(expected);
        Check.That(await Body(Name)).IsEqualTo(expected);
    }

    private async Task<string> Route(string name)  => await Read(await _client.GetAsync("/portability/route/" + Uri.EscapeDataString(name), TestContext.Current.CancellationToken));
    private async Task<string> Query(string name)  => await Read(await _client.GetAsync("/portability/query?value=" + Uri.EscapeDataString(name), TestContext.Current.CancellationToken));
    private async Task<string> Form(string name)   => await Read(await _client.PostAsync("/portability/form", new FormUrlEncodedContent([new KeyValuePair<string, string>("value", name)]), TestContext.Current.CancellationToken));

    private async Task<string> Body(string name) {
        string json = JsonSerializer.Serialize(new Dictionary<string, string> { ["Value"] = name });
        using StringContent content = new(json, Encoding.UTF8, "application/json");

        return await Read(await _client.PostAsync("/portability/body", content, TestContext.Current.CancellationToken));
    }

    private async Task<string> Header(string name) {
        using HttpRequestMessage request = new(HttpMethod.Get, "/portability/header");
        request.Headers.TryAddWithoutValidation("X-V", name);

        return await Read(await _client.SendAsync(request, TestContext.Current.CancellationToken));
    }

    private static async Task<string> Read(HttpResponseMessage response) {
        using (response) {
            Check.WithCustomMessage($"expected success, got {(int)response.StatusCode}").That(response.IsSuccessStatusCode).IsTrue();
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            return document.RootElement.GetProperty("value").GetString()!;
        }
    }

}
