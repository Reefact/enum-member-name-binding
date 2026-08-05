# AspNetCore.EnumMemberNameBinding

**One enum contract, honoured on every input channel — not just the request body.**

Since .NET 9, `System.Text.Json` lets you give an enum member an explicit public name:

```csharp
using System.Text.Json.Serialization;

public enum ProductStatus
{
    [JsonStringEnumMemberName("available")]    Available,
    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("discontinued")] Discontinued
}
```

That name is honoured in the **request body**, and nowhere else. ASP.NET Core binds route values,
query strings, form fields and headers through `System.ComponentModel`, which has never heard of
`System.Text.Json`. So the same API answers:

```
POST /products   {"status":"out_of_stock"}   → 200
GET  /products?status=out_of_stock           → 400
GET  /products?status=OutOfStock             → 200   ← your internal C# name, now part of your public contract
```

This package closes that gap.

## Install

```
dotnet add package AspNetCore.EnumMemberNameBinding
```

Requires **.NET 10** and ASP.NET Core MVC (controllers).

## Use

One line, at start-up:

```csharp
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddControllers()
       .AddEnumMemberNameBinding();

var app = builder.Build();
app.MapControllers();
app.Run();
```

That is all. Nothing to annotate beyond the `[JsonStringEnumMemberName]` attributes you already have.

```csharp
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("products")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet("{status}")]
    public IActionResult ByStatus([FromRoute] ProductStatus status) => Ok(status);

    [HttpGet]
    public IActionResult Search([FromQuery] ProductStatus? status) => Ok(status);
}
```

```
GET /products/out_of_stock       → 200
GET /products?status=out_of_stock → 200
GET /products?status=OutOfStock   → 400   { "errors": { "status": ["The value 'OutOfStock' is not valid."] } }
```

## What is accepted

The rules are not invented here: they are a port of the ones `System.Text.Json` applies to the
request body, so that every channel of your API accepts exactly the same vocabulary.

| Input | Accepted | Why |
|---|:--:|---|
| `out_of_stock` | ✅ | the declared public name |
| `OutOfStock` | ❌ | the C# name of an **annotated** member is not a public name |
| `OUT_OF_STOCK` | ❌ | a declared name matches case-sensitively |
| `1`, `999`, `-1` | ❌ | numeric values are never accepted |
| `Two` / `two` | ✅ | a member **without** the attribute keeps its C# name, matched case-insensitively |
| `read, write` | ✅ | `[Flags]` enums accept a comma-separated list |

This is verified rather than declared: the test suite runs every candidate input through both
`JsonSerializer` and a live HTTP request, and asserts the two outcomes are identical. If .NET
changes its matching rules, the build fails.

## Covered channels

| Channel | Covered |
|---|:--:|
| Route values | ✅ |
| Query strings | ✅ |
| Form fields | ✅ |
| Headers (`[FromHeader]`) | ✅ |
| Nullable enums (`TEnum?`) | ✅ |
| Request body | ✅ (by `System.Text.Json`) |
| Minimal APIs | ❌ — see *Limitations* |

An enum that carries no `[JsonStringEnumMemberName]` is **left completely alone**: same binding,
same validation, same JSON wire format as without this package. Enabling the library never changes
behaviour you did not ask it to change.

## Configuration

By default the entry assembly is scanned and every enum carrying the attribute is registered.

```csharp
builder.Services
       .AddControllers()
       .AddEnumMemberNameBinding(options =>
       {
           options.ScanAssemblyContaining<ProductStatus>();  // scan another assembly
           options.AddEnum<ProductStatus>();                 // or register explicitly
           options.ConfigureJsonSerialization = true;        // default
       });
```

`ConfigureJsonSerialization` registers a `JsonStringEnumConverter<T>` **per contract enum**. The
global `JsonStringEnumConverter` factory is never installed, so enums outside your contract keep
their existing representation. Set it to `false` if you configure `System.Text.Json` yourself.

## Contract validation

A malformed contract fails at start-up with an `EnumContractException` naming the type, every
problem and the expected fix — never at request time:

- two members declaring the same public name;
- a name that is empty or has leading/trailing whitespace;
- a comma inside the name of a `[Flags]` member.

Two members may share the same numeric value as long as their public names differ.

## Limitations

**Minimal APIs are not supported.** Their parameter binding uses neither MVC model binders nor
`TypeDescriptor`: it requires a `static TryParse` or `BindAsync` on the bound type, which cannot be
added to an `enum`. No third-party package can close this without abandoning `enum`. If you need it
today, wrap the enum in a `readonly record struct` implementing `IParsable<T>`.

**OpenAPI documents are not corrected yet.** On .NET 10 the generated document advertises the
contract names for query and route parameters — which stock ASP.NET Core rejects
([dotnet/aspnetcore#68065](https://github.com/dotnet/aspnetcore/issues/68065), closed as *not
planned*). From .NET 11 it will advertise C# names instead, which this package makes wrong the other
way round. A companion package is planned.

**Not compatible with trimming or Native AOT.** `TypeDescriptor` and the assembly scan rely on
reflection. `AddEnumMemberNameBinding` is annotated with `[RequiresDynamicCode]` and
`[RequiresUnreferencedCode]` rather than having the warnings suppressed.

**Call it at start-up.** ASP.NET Core caches the model binder it builds for a type on first use, so
a registration made after the first request has no effect.

## Relationship to `Reefact.JsonEnumValueBinding`

This package supersedes `Reefact.JsonEnumValueBinding`, which predates .NET 9 and carried its own
`[JsonEnumValue]` attribute and its own JSON converter. Both are now redundant: the platform owns
the attribute and the serialization. Migration is an attribute rename —
`[JsonEnumValue("x")]` becomes `[JsonStringEnumMemberName("x")]` — plus swapping
`AddJsonEnumValueBinding()` for `AddEnumMemberNameBinding()`.

## Licence

Apache-2.0.
