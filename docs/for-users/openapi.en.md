# OpenAPI

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./openapi.fr.md)

```
dotnet add package AspNetCore.EnumMemberNameBinding.OpenApi
```

```csharp
builder.Services.AddOpenApi(options => options.AddEnumMemberNames());
```

## Why a companion package is needed

Left alone, ASP.NET Core describes every enum of an MVC application as a plain integer, because
`Microsoft.AspNetCore.OpenApi` reads `Http.Json.JsonOptions` while MVC reads its own. The main
package now configures both, and the companion corrects what remains:

| Schema | ASP.NET Core alone | With the companion |
|---|---|---|
| `ProductStatus` | `{"type":"integer"}` | `{"type":"string","enum":["available","out_of_stock","discontinued"]}` |
| `Permissions` (`[Flags]`) | `{"type":"integer"}` | `{"type":"string","pattern":"^\\s*(read\|write\|delete)(\\s*,\\s*(read\|write\|delete))*\\s*,?\\s*$"}` |
| `PlainPriority` (no contract) | `{"type":"integer"}` | `{"type":"integer"}` — untouched |

Two details worth knowing. ASP.NET Core emits enum values **without declaring a type**, which the
companion fixes. And for a `[Flags]` enum it deliberately emits no value at all — a closed list
cannot express combinations, so the companion emits a regular expression instead, which is both
precise and machine-checkable.

## Only the enums this application registered

The companion describes an enum when `AddEnumMemberNameBinding` registered it, not merely because it
carries `[JsonStringEnumMemberName]`. The two read as the same thing and are not: an annotated enum
nobody registered binds by its C# names and serializes as a number, so announcing its declared names
as a string would be wrong about the query string *and* about the body, and a client generated from
that document would send requests the server answers 400 to.

Used on its own — by an application that registers its own `JsonStringEnumConverter<T>` and never
calls `AddEnumMemberNameBinding` — there is no registration to consult, and every contract enum is
described as before. A missing record is not an empty one: what the record rules out is the case
where one exists and the enum is not in it, which is the only case where document and server can be
known to disagree.

## The `[Flags]` pattern

The pattern covers exactly what the binder accepts, whitespace and trailing comma included — see
[contract rules](contract-rules.en.md#a-flags-enum). It is written in the ECMA-262 dialect a JSON Schema
`pattern` is read with, so only syntax characters are escaped: `Regex.Escape` would produce `\ ` and
`\#`, which are not valid identity escapes there and would make a strict consumer reject the whole
pattern. A test rejects any other escape.

A public name containing a regular-expression metacharacter is escaped and still matches literally;
one containing a space is matched as written, since only the separators around commas are flexible.

## Verified against the running server

The test suite asserts document/runtime coherence directly rather than checking the document against
itself: every value the document advertises is sent to the running server and must be accepted, and
every value it excludes must be rejected. The documented `[Flags]` pattern is compiled and replayed
the same way.

One deliberate asymmetry. A non-`[Flags]` schema stays a closed `enum` list, so it does not advertise
the comma-separated combinations the server also accepts —
[`available,out_of_stock`](contract-rules.en.md#a-comma-separates-values-on-every-enum) binds and is
not in the list. That is the direction worth being wrong in: the list is the vocabulary a client
should generate from, and a combination is a legacy shape of `Enum.Parse` rather than something an
API means to offer. Advertising it as a pattern, as `[Flags]` requires, would push every generated
client towards a free-text field where an enumeration is what the endpoint is for.

## Version floor

The companion raises the floor of `Microsoft.OpenApi` to 2.11.0. `Microsoft.AspNetCore.OpenApi`
10.0.x resolves 2.0.0, which carries advisory
[GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

## Trimming and Native AOT

The whole package is annotated `[RequiresUnreferencedCode]`: reading an enum's metadata needs
reflection. It carries no `[RequiresDynamicCode]`, because describing a schema generates no code.
