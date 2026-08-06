# Contract rules — what is accepted

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./contract-rules.fr.md)

The rules are not invented here: they are a port of the ones `System.Text.Json` applies to the
request body, so that every channel of your API accepts exactly the same vocabulary.

Everything below is the **raw text of an incoming request**, not a C# value — a route segment, a
query string value, a form field or a header. `?status=1` is the character `1`, not the integer.

## A fully annotated enum

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

## A partially annotated enum — rejected

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

`AllowPartialContracts` governs an **incomplete** contract, not the absence of one. An enum carrying
no attribute at all is skipped by the scan, and naming it in `AddEnum<T>()` is refused: adopting it
would change how an ordinary enum binds and serializes.

## A `[Flags]` enum

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
| `GET /tokens?perms=%20read,%20write%20` | ✅ — the value and each element are trimmed |
| `GET /tokens?perms=read,` | ✅ — one trailing comma is tolerated |
| `GET /tokens?perms=,read` | ❌ 400 — a leading or repeated comma is not |
| `GET /tokens?perms=read, bogus` | ❌ 400 — one unknown member rejects the whole value |
| `GET /tokens?perms=Read` | ❌ 400 |

Those whitespace and comma rules are not a choice made here — they were measured against
`System.Text.Json` and reproduced, down to the trailing comma. The same holds for a simple enum:
`?status=%20available%20` is accepted, because the body accepts `" available "`.

## Empty and absent values

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

## Which names can travel

Not every string `System.Text.Json` accepts survives every channel. Measured, channel by channel:

| In a name | route | query | header | form | body |
|---|:---:|:---:|:---:|:---:|:---:|
| `/` | **400** | ✅ | ✅ | ✅ | ✅ |
| CR, LF | ✅ | ✅ | **400** | ✅ | ✅ |
| outside printable ASCII | ✅ | ✅ | **refused by the client** | ✅ | ✅ |
| `?` `#` `&` `=` `+` `%` space tab `\` `"` | ✅ | ✅ | ✅ | ✅ | ✅ |

[`EMN0006`](rules/EMN0006.en.md) reports the first three at build time, as a warning — whether it
matters depends on the channels your API actually binds from.

## Configuration

By default the entry assembly is scanned and every enum carrying the attribute is registered.

```csharp
builder.Services
       .AddControllers()
       .AddEnumMemberNameBinding(options =>
       {
           options.ScanAssemblyContaining<ProductStatus>();  // scan another assembly
           options.AddEnum<ProductStatus>();                 // or register explicitly
           options.AllowPartialContracts = false;            // default
           options.ConfigureJsonSerialization = true;        // default
       });
```

`ConfigureJsonSerialization` registers a `JsonStringEnumConverter<T>` **per contract enum**, in both
the MVC and the `Http.Json` options. The global `JsonStringEnumConverter` factory is never installed,
so enums outside your contract keep their existing representation. Set it to `false` if you configure
`System.Text.Json` yourself.

## Verified, not declared

Apart from the two rows called out above, none of this is asserted by hand. The test suite runs every
candidate input through both `JsonSerializer` and a live HTTP request, and requires the two outcomes
to be identical. If .NET changes its matching rules, the build fails.
