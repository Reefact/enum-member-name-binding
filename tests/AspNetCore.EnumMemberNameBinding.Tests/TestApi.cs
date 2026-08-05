using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.EnumMemberNameBinding.Tests;

/// <summary>A fully annotated contract enum.</summary>
public enum ProductStatus {

    [JsonStringEnumMemberName("available")]    Available,
    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("discontinued")] Discontinued

}

/// <summary>Only one member is annotated — the other keeps its C# name, exactly as System.Text.Json does.</summary>
public enum PartiallyAnnotated {

    [JsonStringEnumMemberName("one")] One,
    Two

}

/// <summary>A [Flags] contract enum.</summary>
[Flags]
public enum Permissions {

    [JsonStringEnumMemberName("read")]  Read    = 1,
    [JsonStringEnumMemberName("write")] Write   = 2,
    [JsonStringEnumMemberName("delete")] Delete = 4

}

/// <summary>No attribute at all — must keep ASP.NET Core's stock behaviour.</summary>
public enum PlainPriority {

    Low,
    Normal,
    High

}

[ApiController]
public sealed class BindingController : ControllerBase {

    [HttpGet("/status/route/{value}")]
    public IActionResult StatusFromRoute([FromRoute] ProductStatus value) => Ok(new Bound(value.ToString()));

    [HttpGet("/status/query")]
    public IActionResult StatusFromQuery([FromQuery] ProductStatus value) => Ok(new Bound(value.ToString()));

    [HttpGet("/status/query-nullable")]
    public IActionResult StatusFromQueryNullable([FromQuery] ProductStatus? value) => Ok(new Bound(value?.ToString() ?? "<null>"));

    [HttpGet("/status/header")]
    public IActionResult StatusFromHeader([FromHeader(Name = "X-Status")] ProductStatus value) => Ok(new Bound(value.ToString()));

    [HttpPost("/status/form")]
    public IActionResult StatusFromForm([FromForm] ProductStatus value) => Ok(new Bound(value.ToString()));

    [HttpPost("/status/body")]
    public IActionResult StatusFromBody([FromBody] Payload payload) => Ok(new Bound(payload.Value.ToString()));

    [HttpGet("/partial/query")]
    public IActionResult PartialFromQuery([FromQuery] PartiallyAnnotated value) => Ok(new Bound(value.ToString()));

    [HttpGet("/permissions/query")]
    public IActionResult PermissionsFromQuery([FromQuery] Permissions value) => Ok(new Bound(value.ToString()));

    [HttpGet("/plain/query")]
    public IActionResult PlainFromQuery([FromQuery] PlainPriority value) => Ok(new Bound(value.ToString()));

    [HttpGet("/plain/serialized")]
    public IActionResult PlainSerialized() => Ok(new { value = PlainPriority.High });

    [HttpGet("/status/serialized")]
    public IActionResult StatusSerialized() => Ok(new { value = ProductStatus.OutOfStock });

    public sealed record Bound(string Value);

    public sealed class Payload {

        public ProductStatus Value { get; set; }

    }

}

/// <summary>Boots a real Kestrel host with the library enabled.</summary>
public sealed class TestApi : IAsyncLifetime {

    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services
               .AddControllers()
               .AddApplicationPart(typeof(BindingController).Assembly)
               // Registered explicitly: this assembly also holds the deliberately invalid enums
               // used by ContractValidationTests, which a scan would — correctly — reject.
               .AddEnumMemberNameBinding(options => options
                                             .AddEnum<ProductStatus>()
                                             .AddEnum<PartiallyAnnotated>()
                                             .AddEnum<Permissions>());

        _app = builder.Build();
        _app.MapControllers();

        // Minimal API endpoints serialize through Http.Json.JsonOptions, not the MVC options.
        _app.MapGet("/minimal/contract-serialized", () => new { value = ProductStatus.OutOfStock });
        _app.MapGet("/minimal/plain-serialized", () => new { value = PlainPriority.High });

        await _app.StartAsync();

        string address = _app.Urls.First();
        Client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync() {
        Client?.Dispose();
        if (_app is not null) {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

}

[CollectionDefinition(nameof(TestApiCollection))]
public sealed class TestApiCollection : ICollectionFixture<TestApi>;
