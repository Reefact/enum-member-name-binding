global using Xunit;

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EnumMemberNameBinding.OpenApi.Tests;

public enum OrderState {

    [JsonStringEnumMemberName("pending")]   Pending,
    [JsonStringEnumMemberName("shipped")]   Shipped,
    [JsonStringEnumMemberName("cancelled")] Cancelled

}

[Flags]
public enum Scopes {

    [JsonStringEnumMemberName("read")]   Read   = 1,
    [JsonStringEnumMemberName("write")]  Write  = 2,
    [JsonStringEnumMemberName("delete")] Delete = 4

}

/// <summary>
/// A <c>[Flags]</c> contract with one member left unannotated, which keeps its C# name. The two
/// halves of the vocabulary are then matched differently — a declared name ordinally, a C# name
/// ignoring case — and a pattern that wrote them the same way described neither.
/// </summary>
[Flags]
public enum MixedScopes {

    [JsonStringEnumMemberName("read")]  Read  = 1,
    [JsonStringEnumMemberName("write")] Write = 2,
    Delete = 4

}

public enum PlainLevel {

    Low,
    High

}

/// <summary>Names full of characters that mean something to a regular expression engine.</summary>
[Flags]
public enum Tricky {

    [JsonStringEnumMemberName("a+b")]        Plus    = 1,
    [JsonStringEnumMemberName("c.d")]        Dot     = 2,
    [JsonStringEnumMemberName("read write")] Spaced  = 4,
    [JsonStringEnumMemberName("e|f")]        Pipe    = 8,
    [JsonStringEnumMemberName("(g)")]        Parens  = 16,
    [JsonStringEnumMemberName("h#i")]        Hash    = 32,
    [JsonStringEnumMemberName("[j]")]        Bracket = 64,
    [JsonStringEnumMemberName("k-l")]        Dash    = 128

}

[ApiController]
public sealed class OrdersController : ControllerBase {

    [HttpGet("/orders")]
    public IActionResult ByState([FromQuery] OrderState state) => Ok(new { value = state.ToString() });

    [HttpGet("/orders/{state}")]
    public IActionResult ByRoute([FromRoute] OrderState state) => Ok(new { value = state.ToString() });

    [HttpGet("/tokens")]
    public IActionResult ByScopes([FromQuery] Scopes scopes) => Ok(new { value = scopes.ToString() });

    [HttpGet("/levels")]
    public IActionResult ByLevel([FromQuery] PlainLevel level) => Ok(new { value = level.ToString() });

    [HttpGet("/tricky")]
    public IActionResult ByTricky([FromQuery] Tricky value) => Ok(new { value = value.ToString() });

    [HttpGet("/mixed")]
    public IActionResult ByMixedScopes([FromQuery] MixedScopes value) => Ok(new { value = value.ToString() });

}

/// <summary>Boots the API with the OpenAPI companion enabled (or not, for characterization).</summary>
public abstract class OpenApiTestApiBase(bool withTransformer) : IAsyncLifetime {

    /// <summary>
    /// The description <see cref="Tricky" /> already carries by the time this package sees it. The
    /// trailing space is deliberate: what the two produce together has to read as one sentence
    /// followed by another, however the first one was punctuated.
    /// </summary>
    public const string DescribedElsewhere = "Documented by another transformer. ";

    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public JsonElement Document { get; private set; }

    public async ValueTask InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services
               .AddControllers()
               .AddApplicationPart(typeof(OrdersController).Assembly)
               // Partial contracts are opted into for MixedScopes alone; every other enum here
               // annotates every member, so the switch changes nothing for them.
               .AddEnumMemberNameBinding(options => {
                    options.AddEnum<OrderState>().AddEnum<Scopes>().AddEnum<Tricky>().AddEnum<MixedScopes>();
                    options.AllowPartialContracts = true;
                });

        if (withTransformer) {
            builder.Services.AddOpenApi(options => {
                // Registered first, so this package's transformer meets a schema whose description is
                // already written — the case where it has to append rather than write. An application
                // does this with its own transformer, or through the XML comments of the enum.
                options.AddSchemaTransformer((schema, context, _) => {
                    if (context.JsonTypeInfo.Type == typeof(Tricky)) { schema.Description = DescribedElsewhere; }

                    return Task.CompletedTask;
                });
                options.AddEnumMemberNames();
            });
        } else {
            builder.Services.AddOpenApi();
        }

        _app = builder.Build();
        _app.MapControllers();
        _app.MapOpenApi();
        await _app.StartAsync();

        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
        Document = JsonDocument.Parse(await Client.GetStringAsync("/openapi/v1.json")).RootElement.Clone();
    }

    public async ValueTask DisposeAsync() {
        Client?.Dispose();
        if (_app is not null) {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        // Required by CA1816 rather than by this type: xUnit v3's IAsyncLifetime extends
        // IAsyncDisposable, and this class is a base, so a derived type introducing a finalizer
        // would otherwise have to re-implement disposal just to suppress it.
        GC.SuppressFinalize(this);
    }

    public JsonElement Schema(string name) => Document.GetProperty("components").GetProperty("schemas").GetProperty(name);

}

public sealed class OpenApiTestApi() : OpenApiTestApiBase(withTransformer: true);

public sealed class WithoutTransformer() : OpenApiTestApiBase(withTransformer: false);

[CollectionDefinition(nameof(OpenApiCollection))]
public sealed class OpenApiCollection : ICollectionFixture<OpenApiTestApi>;

[CollectionDefinition(nameof(StockOpenApiCollection))]
public sealed class StockOpenApiCollection : ICollectionFixture<WithoutTransformer>;
