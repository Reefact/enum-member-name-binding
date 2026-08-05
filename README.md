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

Everything below is the **raw text of an incoming request**, not a C# value — a route segment, a
query string value, a form field or a header. `?status=1` is the three characters `?`, `s`… and the
character `1`, not the integer.

### A fully annotated enum

```csharp
public enum ProductStatus
{
    [JsonStringEnumMemberName("available")]    Available,
    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("discontinued")] Discontinued
}
```

```csharp
[HttpGet("/products")]
public IActionResult Search([FromQuery] ProductStatus status) => Ok(status);
```

| Request | Result |
|---|---|
| `GET /products?status=out_of_stock` | ✅ `ProductStatus.OutOfStock` |
| `GET /products?status=available` | ✅ `ProductStatus.Available` |
| `GET /products?status=OutOfStock` | ❌ 400 — the C# name of an annotated member is not a public name |
| `GET /products?status=OUT_OF_STOCK` | ❌ 400 — a declared name matches case-sensitively |
| `GET /products?status=1` | ❌ 400 — a numeric value is never accepted |
| `GET /products?status=999` | ❌ 400 |
| `GET /products?status=` | ❌ 400 — see *empty and absent values* below |
| `GET /products` (no value) | ⚠️ 200 `Available` — see *empty and absent values* below |

### A partially annotated enum

An attribute **replaces** a member's name, it does not add an alias. A member without the attribute
keeps its C# name — which is exactly what `System.Text.Json` does.

```csharp
public enum Shipping
{
    [JsonStringEnumMemberName("express")] Express,
    Standard                                        // no attribute
}
```

| Request | Result |
|---|---|
| `GET /orders?mode=express` | ✅ `Shipping.Express` |
| `GET /orders?mode=Express` | ❌ 400 — annotated, so only `express` is public |
| `GET /orders?mode=Standard` | ✅ `Shipping.Standard` — no attribute, the C# name is the public name |
| `GET /orders?mode=standard` | ✅ `Shipping.Standard` — an unannotated name matches case-insensitively |

### A `[Flags]` enum

```csharp
[Flags]
public enum Permissions
{
    [JsonStringEnumMemberName("read")]   Read   = 1,
    [JsonStringEnumMemberName("write")]  Write  = 2,
    [JsonStringEnumMemberName("delete")] Delete = 4
}
```

| Request | Result |
|---|---|
| `GET /tokens?perms=read` | ✅ `Read` |
| `GET /tokens?perms=read, write` | ✅ `Read \| Write` |
| `GET /tokens?perms=read,write` | ✅ same — the space is optional |
| `GET /tokens?perms=read, delete` | ✅ `Read \| Delete` |
| `GET /tokens?perms=read, bogus` | ❌ 400 — one unknown member rejects the whole value |
| `GET /tokens?perms=Read` | ❌ 400 |

### Empty and absent values

| Parameter | Request | Result |
|---|---|---|
| `ProductStatus` | `?status=out_of_stock` | ✅ `OutOfStock` |
| `ProductStatus` | `?status=` | ❌ 400 |
| `ProductStatus` | no value at all | ⚠️ 200, binds `Available` — the first member |
| `ProductStatus?` | `?status=` | ⚠️ 200, binds `null` |
| `ProductStatus?` | no value at all | ✅ 200, binds `null` |
| `[FromHeader] ProductStatus` | `X-Status:` empty | ❌ 400 |

Two rows deserve attention, and **neither is introduced by this package**:

- **An absent value on a non-nullable parameter binds the first member instead of failing.** This is
  stock ASP.NET Core behaviour for value types; a test asserts that an enum this package never
  touches behaves identically. Use `ProductStatus?` or `[Required]` if you want a 400.
- **An empty value on a nullable parameter binds `null`,** where `System.Text.Json` rejects `""`.
  ASP.NET Core treats it as an absent value before any `TypeConverter` is consulted, so it is out of
  reach from here.

Both are covered by tests, so they stay visible rather than becoming folklore.

### Verified, not declared

Apart from that one row, none of this is asserted by hand. The test suite runs every candidate input
through both `JsonSerializer` and a live HTTP request, and requires the two outcomes to be identical.
If .NET changes its matching rules, the build fails.

## Covered channels

| Channel | Covered |
|---|:--:|
| Route values | ✅ |
| Query strings | ✅ |
| Form fields | ✅ |
| Headers (`[FromHeader]`) | ✅ |
| Nullable enums (`TEnum?`) | ✅ |
| Request body | ✅ (by `System.Text.Json`) |
| OpenAPI document | ✅ with the companion package — see below |
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

## OpenAPI

```
dotnet add package AspNetCore.EnumMemberNameBinding.OpenApi
```

```csharp
using Microsoft.AspNetCore.OpenApi;

builder.Services.AddOpenApi(options => options.AddEnumMemberNames());
```

Left alone, ASP.NET Core describes every enum of an MVC application as a plain integer, because
`Microsoft.AspNetCore.OpenApi` reads `Http.Json.JsonOptions` while MVC reads its own. The main
package now configures both, and the companion corrects what remains:

| Schema | ASP.NET Core alone | With the companion |
|---|---|---|
| `ProductStatus` | `{"type":"integer"}` | `{"type":"string","enum":["available","out_of_stock","discontinued"]}` |
| `Permissions` (`[Flags]`) | `{"type":"integer"}` | `{"type":"string","pattern":"^(read\|write\|delete)(\\s*,\\s*(read\|write\|delete))*$"}` |
| `PlainPriority` (no contract) | `{"type":"integer"}` | `{"type":"integer"}` — untouched |

Two details worth knowing. ASP.NET Core emits enum values **without declaring a type**, which the
companion fixes. And for a `[Flags]` enum it deliberately emits no value at all — a closed list
cannot express combinations, so the companion emits a regular expression instead, which is both
precise and machine-checkable.

The test suite asserts document/runtime coherence directly: every value the document advertises is
sent to the running server and must be accepted, and every value it excludes must be rejected.

 Their parameter binding uses neither MVC model binders nor
`TypeDescriptor`: it requires a `static TryParse` or `BindAsync` on the bound type, which cannot be
added to an `enum`. No third-party package can close this without abandoning `enum`. If you need it
today, wrap the enum in a `readonly record struct` implementing `IParsable<T>`.

**OpenAPI needs the companion package.** ASP.NET Core has closed the corresponding issue as *not
planned* ([dotnet/aspnetcore#68065](https://github.com/dotnet/aspnetcore/issues/68065)), and .NET 11
will start advertising C# names for non-body parameters — so this divergence is expected to widen,
not shrink.

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
