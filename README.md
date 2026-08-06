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
GET /products/out_of_stock        → 200
GET /products?status=out_of_stock → 200
GET /products?status=OutOfStock   → 400   { "errors": { "status": ["The value 'OutOfStock' is not valid."] } }
```

## Covered channels

| Channel | Covered |
|---|:--:|
| Route values | ✅ |
| Query strings | ✅ |
| Form fields | ✅ |
| Headers (`[FromHeader]`) | ✅ |
| Nullable enums (`TEnum?`) | ✅ |
| Request body | ✅ (by `System.Text.Json`) |
| OpenAPI document | ✅ with the [companion package](https://github.com/Reefact/enum-member-name-binding/blob/main/docs/openapi.md) |
| Minimal API responses | ✅ |
| Minimal API parameters | ❌ — [platform-level constraint](https://github.com/Reefact/enum-member-name-binding/blob/main/docs/limitations.md#minimal-api-parameters-are-not-supported) |

## Guarantees

- **The same vocabulary everywhere.** The matching rules are not invented here, they are a port of
  the ones `System.Text.Json` applies to the request body — down to the whitespace and the trailing
  comma of a `[Flags]` list. Every rule was measured against `JsonSerializer`, never read off a
  specification.
- **Verified, not declared.** The test suite runs every candidate input through both `JsonSerializer`
  and a live HTTP request and requires the two outcomes to be identical. If .NET changes its matching
  rules, the build fails.
- **Nothing else changes.** An enum that carries no `[JsonStringEnumMemberName]` is left completely
  alone: same binding, same validation, same JSON wire format as without this package. The global
  `JsonStringEnumConverter` factory is never installed — one converter is registered per contract
  enum.
- **Mistakes are build errors.** Roslyn analyzers ship inside the package, no extra install: a
  duplicate public name, an incomplete contract or a name that shadows another member's C# name is
  reported in your editor, not discovered at start-up. Enums that declare no contract are never
  analysed.
- **Validation is preserved.** An unknown or numeric value is a 400, exactly as the body would
  refuse it.

## Limitations

The two worth knowing before you adopt it:

- **Minimal API parameters are not supported.** Their binding requires a `static TryParse` or
  `BindAsync` on the bound type, which cannot be added to an `enum` — a platform-level constraint,
  not an implementation gap. Responses *are* covered.
- **Link generation does not use the public name.** ASP.NET Core formats route values without
  consulting `TypeDescriptor`, so a link built from the enum value carries the C# name and this very
  API answers 400 to it. `EnumMemberNames.GetPublicName(value)` is the way round.

The full list — empty values, channel portability, trimming and Native AOT, and why registration must
happen at start-up — is in
[limitations](https://github.com/Reefact/enum-member-name-binding/blob/main/docs/limitations.md).

## Documentation

- [Contract rules](https://github.com/Reefact/enum-member-name-binding/blob/main/docs/contract-rules.md)
  — what is accepted, request by request: fully and partially annotated enums, `[Flags]`, empty and
  absent values, which names can travel on which channel, and the configuration options.
- [Analyzers](https://github.com/Reefact/enum-member-name-binding/blob/main/docs/analyzers.md)
  — `EMN0001` to `EMN0006`, why `EMN0005` is worth reading twice, and how to configure severities.
- [OpenAPI](https://github.com/Reefact/enum-member-name-binding/blob/main/docs/openapi.md)
  — the companion package, the `[Flags]` pattern, and how document/runtime coherence is verified.
- [Limitations](https://github.com/Reefact/enum-member-name-binding/blob/main/docs/limitations.md)
- [Changelog](https://github.com/Reefact/enum-member-name-binding/blob/main/CHANGELOG.md)

## Relationship to `Reefact.JsonEnumValueBinding`

This package supersedes `Reefact.JsonEnumValueBinding`, which predates .NET 9 and carried its own
`[JsonEnumValue]` attribute and its own JSON converter. Both are now redundant: the platform owns
the attribute and the serialization. Migration is an attribute rename —
`[JsonEnumValue("x")]` becomes `[JsonStringEnumMemberName("x")]` — plus swapping
`AddJsonEnumValueBinding()` for `AddEnumMemberNameBinding()`.

## Licence

Apache-2.0.
