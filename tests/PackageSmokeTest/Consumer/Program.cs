using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

// This file is a transcription of the README, kept deliberately close to it. The registration below
// is the zero-option form the front page advertises, exercised here against the packed package
// rather than against a project reference — which is what this fixture is for.
//
// It is not the only place that form is exercised: EntryAssemblyScanTests reaches it too, because
// xUnit v3 generates the entry point into the test assembly, so Assembly.GetEntryAssembly() there is
// the test assembly and not a host. This comment said the opposite for as long as it existed.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddControllers()
       .AddEnumMemberNameBinding();

builder.Services.AddOpenApi(options => options.AddEnumMemberNames());

WebApplication app = builder.Build();
app.MapControllers();
app.MapOpenApi();
app.Run();

/// <summary>The enum from the README, with the names the README promises.</summary>
public enum ProductStatus {

    [JsonStringEnumMemberName("available")]    Available,
    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("discontinued")] Discontinued

}

/// <summary>
/// Declares no contract, so the library must leave it entirely alone: it still binds by its C# name
/// and is still documented as an integer. A registration that quietly took over every enum in the
/// assembly would pass every other check in this file.
/// </summary>
public enum PlainPriority {

    Low,
    High

}

[ApiController]
[Route("products")]
public sealed class ProductsController : ControllerBase {

    [HttpGet("{status}")]
    public IActionResult ByStatus([FromRoute] ProductStatus status) => Ok(new { status = status.ToString() });

    [HttpGet]
    public IActionResult Search([FromQuery] ProductStatus? status) => Ok(new { status = status?.ToString() });

    [HttpPost]
    public IActionResult Create([FromBody] Product product) => Ok(new { status = product.Status.ToString() });

    public sealed class Product {

        public ProductStatus Status { get; set; }

    }

}

[ApiController]
[Route("priorities")]
public sealed class PrioritiesController : ControllerBase {

    [HttpGet]
    public IActionResult Search([FromQuery] PlainPriority priority) => Ok(new { priority = priority.ToString() });

}
