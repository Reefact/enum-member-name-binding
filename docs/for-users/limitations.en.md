# Limitations

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./limitations.fr.md)

Everything here is measured and pinned by a test, so that a limitation stays visible rather than
becoming folklore.

## Minimal API parameters are not supported

Their binding uses neither MVC model binders nor `TypeDescriptor`: it requires a `static TryParse` or
`BindAsync` on the bound type, which cannot be added to an `enum`. No third-party package can close
this without abandoning `enum`. If you need it today, wrap the enum in a `readonly record struct`
implementing `IParsable<T>`.

Minimal API *responses* are covered: an endpoint returning a contract enum writes its public name,
because the main package configures `Http.Json.JsonOptions` alongside the MVC options. It is the
input side that is out of reach.

## Link generation does not use the public name

ASP.NET Core formats route values without consulting `TypeDescriptor`, so a link built from the enum
value itself carries the C# name — and this very API answers 400 to it:

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

## An empty value on a nullable enum parameter binds `null`

Where `System.Text.Json` rejects `""`, ASP.NET Core resolves an empty value as an absent one before
any `TypeConverter` is consulted, so it is out of reach from here. See
[empty and absent values](contract-rules.en.md#empty-and-absent-values), which also covers the other
row worth knowing: an absent value on a **non-nullable** parameter binds the first member instead of
failing. Neither behaviour is introduced by this package — a test asserts that an enum it never
touches behaves identically.

## Not every name travels on every channel

A slash cannot cross a route segment, and a line break or a character outside printable ASCII cannot
cross a header. [`EMN0006`](rules/EMN0006.en.md) reports it at build time; the measurement is in
[contract rules](contract-rules.en.md#which-names-can-travel).

## OpenAPI needs the companion package

ASP.NET Core has closed the corresponding issue as *not planned*
([dotnet/aspnetcore#68065](https://github.com/dotnet/aspnetcore/issues/68065)), and .NET 11 will
start advertising C# names for non-body parameters — so this divergence is expected to widen, not
shrink. See [OpenAPI](openapi.en.md).

## Not compatible with trimming or Native AOT

`TypeDescriptor` and the assembly scan rely on reflection. Every entry point is annotated rather than
having the warnings suppressed, so a consumer compiling for either gets an accurate warning instead
of a silent failure at run time.

The two constraints are applied separately, since they are not the same constraint: reading an enum's
metadata needs reflection but generates no code. `GetPublicNames`, `IsFlagsContract`, the MVC
registration and the whole OpenAPI package carry `[RequiresUnreferencedCode]` only; `GetPublicName`,
the `[Flags]` formatting path and the construction of the generic JSON converters carry both.

## Call it at start-up

ASP.NET Core caches the model binder it builds for a type on first use, so a registration made after
the first request has no effect.
