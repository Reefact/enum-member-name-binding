using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Reefact.AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

/// <summary>
/// Annotated, and deliberately never registered. Carrying <c>[JsonStringEnumMemberName]</c> is not
/// the same as being covered: an application chooses which enums it adopts, and this one is the
/// counter-example the document must leave alone.
/// </summary>
public enum Unregistered {

    [JsonStringEnumMemberName("public_name")]  InternalName,
    [JsonStringEnumMemberName("other_public")] OtherInternal

}

[ApiController]
public sealed class UnregisteredController : ControllerBase {

    [HttpGet("/unregistered")]
    public IActionResult Get([FromQuery] Unregistered value) => Ok(new { value = value.ToString() });

    [HttpGet("/unregistered/serialized")]
    public IActionResult Serialized() => Ok(new { value = Unregistered.InternalName });

}

/// <summary>
/// The document describes the enums this application registered, and only those.
/// </summary>
/// <remarks>
/// The package used to key off the attribute alone, which reads as though it should be the same
/// thing and is not. An enum that carries the attribute but was never registered binds by its C#
/// names and serializes as a number, so a schema announcing <c>"type": "string"</c> and its declared
/// names described an endpoint that does not exist — and a client generated from that document sends
/// requests the server answers 400 to. That is worse than no document at all, which is why this is a
/// suite of its own rather than a case in <c>SchemaTransformerTests</c>.
/// </remarks>
public sealed class UnregisteredEnumTests : IAsyncLifetime {

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public JsonElement Document { get; private set; }

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services
               .AddControllers()
               .AddApplicationPart(typeof(UnregisteredController).Assembly)
               // Named explicitly, so the scan never runs and `Unregistered` stays out.
               .AddEnumMemberNameBinding(options => options.AddEnum<OrderState>());

        builder.Services.AddOpenApi(options => options.AddEnumMemberNames());

        _app = builder.Build();
        _app.MapControllers();
        _app.MapOpenApi();
        await _app.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        Document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json")).RootElement.Clone();
    }

    public async ValueTask DisposeAsync() {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The registered enum is asserted alongside, because a transformer that described nothing at
    /// all would pass the first assertion on its own.
    /// </summary>
    [Fact]
    public void an_enum_the_application_did_not_register_is_left_untouched() {
        JsonElement unregistered = Schema(nameof(Unregistered));

        Check.That(unregistered.GetProperty("type").GetString()).IsEqualTo("integer");
        Check.That(unregistered.TryGetProperty("enum", out _)).IsFalse();

        Check.That(Schema(nameof(OrderState)).GetProperty("type").GetString()).IsEqualTo("string");
    }

    /// <summary>
    /// The reason the row above matters: what the server does with that enum is the stock behaviour,
    /// on both sides at once. It reads C# names and writes numbers, so a string schema would have
    /// been wrong about the query string and about the body.
    /// </summary>
    [Fact]
    public async Task the_server_binds_and_writes_that_enum_the_stock_way() {
        Check.That(await Status("public_name")).IsEqualTo(HttpStatusCode.BadRequest);
        Check.That(await Status(nameof(Unregistered.InternalName))).IsEqualTo(HttpStatusCode.OK);

        Check.That(await _client.GetStringAsync("/unregistered/serialized", TestContext.Current.CancellationToken))
             .IsEqualTo("""{"value":0}""");
    }

    private async Task<HttpStatusCode> Status(string value) {
        using HttpResponseMessage response = await _client.GetAsync("/unregistered?value=" + Uri.EscapeDataString(value), TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private JsonElement Schema(string name) => Document.GetProperty("components").GetProperty("schemas").GetProperty(name);

}

/// <summary>
/// The companion used on its own, by an application that registers its converters itself and never
/// calls <c>AddEnumMemberNameBinding</c>.
/// </summary>
/// <remarks>
/// There is no registration record to consult here, and a missing record is not an empty one: the
/// two cannot be known to disagree, and the document is still worth correcting — ASP.NET Core would
/// otherwise emit the values without declaring a type. This is the supported shape for an
/// application whose enums travel in the body only, and it is what keeps the filter above from
/// meaning "the main package is required".
/// </remarks>
public sealed class CompanionOnItsOwnTests : IAsyncLifetime {

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public JsonElement Document { get; private set; }

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services
               .AddControllers()
               .AddApplicationPart(typeof(UnregisteredController).Assembly)
               .AddJsonOptions(json => json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<Unregistered>(namingPolicy: null, allowIntegerValues: false)));

        builder.Services.AddOpenApi(options => options.AddEnumMemberNames());

        _app = builder.Build();
        _app.MapControllers();
        _app.MapOpenApi();
        await _app.StartAsync();

        _client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        Document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json")).RootElement.Clone();
    }

    public async ValueTask DisposeAsync() {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void a_contract_enum_is_still_described_when_there_is_no_record_to_consult() {
        JsonElement schema = Document.GetProperty("components").GetProperty("schemas").GetProperty(nameof(Unregistered));

        Check.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
        Check.That(schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString())).ContainsExactly("public_name", "other_public");
    }

}
