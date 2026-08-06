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
| `GET /products?status=` | ❌ 400 — see [empty and absent values](#empty-and-absent-values) |
| `GET /products` (no value) | ⚠️ 200 `Available` — see [empty and absent values](#empty-and-absent-values) |

### A partially annotated enum — rejected

An attribute **replaces** a member's name, it does not add an alias. So a member left unannotated
answers to its C# name, and that internal identifier silently becomes part of your public contract —
the exact opposite of why you declared a contract. Forgetting one member is a mistake, not a choice.

```csharp
public enum Shipping
{
    [JsonStringEnumMemberName("express")] Express,
    Standard                                        // EMN0003: build error
}
```

It fails twice, as early as possible: the analyzer reports **`EMN0003` at build time**, and if the
enum reaches the runtime unannotated anyway — from an assembly built without the analyzer, say —
registration throws `EnumContractException` at start-up rather than serving a leaky contract.

If the enum is not yours to annotate, opt in explicitly:

```csharp
.AddEnumMemberNameBinding(options => options.AllowPartialContracts = true);
```

The behaviour then matches `System.Text.Json` exactly:

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
| `GET /tokens?perms=%20read,%20write%20` | ✅ — the value and each element are trimmed |
| `GET /tokens?perms=read,` | ✅ — one trailing comma is tolerated |
| `GET /tokens?perms=,read` | ❌ 400 — a leading or repeated comma is not |
| `GET /tokens?perms=read, bogus` | ❌ 400 — one unknown member rejects the whole value |
| `GET /tokens?perms=Read` | ❌ 400 |

Those whitespace and comma rules are not a choice made here — they were measured against
`System.Text.Json` and reproduced, down to the trailing comma. The same holds for a simple enum:
`?status=%20available%20` is accepted, because the body accepts `" available "`.

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
| OpenAPI document | ✅ with the [companion package](#openapi) |
| Minimal API responses | ✅ |
| Minimal API parameters | ❌ — see [Limitations](#limitations) |

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

## Analyzers

The package ships Roslyn analyzers — no extra install. A contract mistake is a build error in your
editor, not an exception found when the application starts.

| ID | Severity | Reported when |
|---|---|---|
| `EMN0001` | Error | Two members declare the same public name |
| `EMN0002` | Error | A public name is empty, or has leading or trailing whitespace |
| `EMN0003` | Error | A contract enum leaves some members unannotated |
| `EMN0004` | Error | A `[Flags]` public name contains a comma |
| `EMN0005` | Error | A public name is also the C# name of another member |

`EMN0005` catches a genuinely nasty one:

```csharp
public enum Colour
{
    [JsonStringEnumMemberName("Blue")] Red,   // EMN0005
    Blue
}
```

A declared public name is matched first and case-sensitively, while an unannotated member's C# name
is matched case-insensitively. So `?colour=Blue` binds to **`Red`**, while `?colour=blue` and
`?colour=BLUE` bind to `Blue`. The member answers to every casing of its name except its own — which
no reader of that enum would ever guess.

The casing of the declared name changes nothing, so the rule ignores case: `[JsonStringEnumMemberName("blue")]`
next to a `Blue` member is the mirror image of the same trap and is reported too.

It only fires next to `EMN0003`, since it needs an unannotated member. That is precisely why it is
its own rule: turn `EMN0003` off to allow partial contracts and `EMN0005` is the only protection
left, so it is an error rather than a warning — and unlike `EMN0003`, it is still enforced at
start-up even when `AllowPartialContracts` is set.

Each rule has a page under [`docs/rules`](docs/rules), which is where the help link in your IDE goes.

**An enum carrying no `[JsonStringEnumMemberName]` at all is never analysed.** The rules only apply
once you have declared a contract, so adding this package to an existing solution does not light up
enums it has nothing to do with.

The analyzers cannot see your runtime configuration, so if you deliberately use
`AllowPartialContracts`, turn `EMN0003` off — and keep `EMN0005` on:

```ini
[*.cs]
dotnet_diagnostic.EMN0003.severity = none
```

Every rule is also enforced at start-up, for enums that reach the runtime from an assembly built
without the analyzers. `EnumContractException` then names the type, every problem and the expected
fix. Two members may share the same numeric value as long as their public names differ.

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

## Limitations

**Minimal API parameters are not supported.** Their binding uses neither MVC model binders nor
`TypeDescriptor`: it requires a `static TryParse` or `BindAsync` on the bound type, which cannot be
added to an `enum`. No third-party package can close this without abandoning `enum`. If you need it
today, wrap the enum in a `readonly record struct` implementing `IParsable<T>`.

Minimal API *responses* are covered: an endpoint returning a contract enum writes its public name,
because the main package configures `Http.Json.JsonOptions` alongside the MVC options. It is the
input side that is out of reach.

**Link generation does not use the public name.** ASP.NET Core formats route values without
consulting `TypeDescriptor`, so a link built from the enum value itself carries the C# name — and
this very API answers 400 to it:

```csharp
// /products/OutOfStock  →  400
links.GetPathByAction(context, "ByStatus", "Products", new { status = ProductStatus.OutOfStock });

// /products/out_of_stock
links.GetPathByAction(context, "ByStatus", "Products",
                      new { status = EnumMemberNames.GetPublicName(ProductStatus.OutOfStock) });
```

`EnumMemberNames.GetPublicName` renders a `[Flags]` combination as a comma-separated list too, and
returns `null` for an enum that declares no contract. Both forms are covered by tests, including the
400 on the first one.

**An empty value on a nullable enum parameter binds `null`** instead of being rejected, where
`System.Text.Json` rejects `""`. ASP.NET Core resolves it before any `TypeConverter` is consulted.
See [empty and absent values](#empty-and-absent-values).

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
