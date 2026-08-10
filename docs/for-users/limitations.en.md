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

ASP.NET Core formats a route value with the value's own `ToString()`, so a link built from the enum
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
any parse is reached, so it is out of reach from here. See
[empty and absent values](contract-rules.en.md#empty-and-absent-values), which also covers the other
row worth knowing: an absent value on a **non-nullable** parameter binds the first member instead of
failing. Neither behaviour is introduced by this package — a test asserts that an enum it never
touches behaves identically.

## A combination naming no member is refused outside the body

A comma separates values on every enum, so `System.Text.Json` reads
`"out_of_stock,discontinued"` as `1 | 2` and hands back `(ProductStatus)3`, a value no member
declares. Every other channel answers 400 to the same input:

```csharp
// body   {"Value":"out_of_stock,discontinued"}  →  200, (ProductStatus)3
// query  ?value=out_of_stock,discontinued       →  400
```

ASP.NET Core's `EnumTypeModelBinder` refuses to bind an undefined value, whichever converter
produced it, and `[Flags]` is not an exemption: `Enum.IsDefined` cannot answer for a combination, so
it compares the value's own text against its underlying number instead and refuses the one that
prints the number back. `read,write` binds because `1 | 2` decomposes into `Read, Write`, not
because the attribute waives the check. This is not reachable from here and, more to the point,
closing it would be wrong: an enum this package never touches is refused the same way, so a contract
enum accepting `3` on a query string would be *more* permissive than an ordinary one. The parity
suite pins both halves, control included.

The `[Flags]` half has one consequence worth naming. An enum whose declared members are composites
that overlap can OR to a value decomposing into none of them — `3 | 6` is `7` on an enum declaring
only `3` and `6` — and that value is refused off the body exactly as an undeclared combination is on
an ordinary enum. The OpenAPI pattern does not know it: it describes every comma-separated list of
declared names, so for that shape alone the document promises a combination the server answers 400
to. Declaring the individual bits as members, rather than only overlapping composites, avoids it.

Combinations that do name a member are accepted, on every channel — see
[contract rules](contract-rules.en.md#a-comma-separates-values-on-every-enum).

## Not every name travels on every channel

A slash cannot cross a route segment, and a line break or a character outside printable ASCII cannot
cross a header. [`EMN0006`](rules/EMN0006.en.md) reports it at build time; the measurement is in
[contract rules](contract-rules.en.md#which-names-can-travel).

## OpenAPI needs the companion package

ASP.NET Core has closed the corresponding issue as *not planned*
([dotnet/aspnetcore#68065](https://github.com/dotnet/aspnetcore/issues/68065)), and .NET 11 will
start advertising C# names for non-body parameters — so this divergence is expected to widen, not
shrink. See [OpenAPI](openapi.en.md).

## The binder writes none of ASP.NET Core's model-binding records

`SimpleTypeModelBinder` is handed an `ILoggerFactory` and logs its own attempt and result. The binder
this package installs takes no logger, so at `Debug` a contract enum parameter is quiet where every
other parameter is not:

```text
# a plain enum, bound by ASP.NET Core
…ModelBinding.ParameterBinder                :: Attempting to bind parameter 'value' …
…ModelBinding.Binders.SimpleTypeModelBinder  :: Attempting to bind parameter 'value' …
…ModelBinding.Binders.SimpleTypeModelBinder  :: Done attempting to bind parameter 'value'.
…ModelBinding.ParameterBinder                :: Done attempting to bind parameter 'value'.

# a contract enum, bound by this package — the two middle records are absent
…ModelBinding.ParameterBinder                :: Attempting to bind parameter 'value' …
…ModelBinding.ParameterBinder                :: Done attempting to bind parameter 'value'.
```

Only the binder's own records are missing. The `ParameterBinder` trace around it belongs to ASP.NET
Core and is untouched, so a log still shows that the parameter was bound and validated — and a
failure is untouched too, reaching `ModelState` and the response exactly as any other one does.

A limit rather than a decision deferred: those records are written through `MvcCoreLoggerExtensions`,
which is `internal` to `Microsoft.AspNetCore.Mvc.Core`. What could be written instead is a lookalike
under this package's own category and event ids, which a log filter aimed at ASP.NET Core's would not
pick up — parity in appearance and none in fact.

## Not compatible with trimming or Native AOT

Resolving a contract and scanning an assembly rely on reflection. Every entry point is annotated rather than
having the warnings suppressed, so a consumer compiling for either gets an accurate warning instead
of a silent failure at run time.

The two constraints are applied separately, since they are not the same constraint: reading an enum's
metadata needs reflection but generates no code. `GetPublicNames`, `IsFlagsContract` and the whole
OpenAPI package carry `[RequiresUnreferencedCode]` only; `GetPublicName`, the `[Flags]` formatting
path, the construction of the generic JSON converters — and therefore `AddEnumMemberNameBinding`
itself, which reaches them — carry both.

## Call it at start-up

Registration configures the container, so it belongs before `WebApplicationBuilder.Build()`. After
that the service collection is read-only and the call throws — having recorded nothing, which is the
point: a failed call never leaves the application binding an enum it named. Later still, ASP.NET Core
has cached the model binder it built for a type on first use, so a parameter already bound would not
see a new registration even if one could be made.
