# OpenAPI

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./openapi.fr.md)

```
dotnet add package AspNetCore.EnumMemberNameBinding.OpenApi
```

```csharp
using Microsoft.AspNetCore.OpenApi;

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

## Version floor

The companion raises the floor of `Microsoft.OpenApi` to 2.11.0. `Microsoft.AspNetCore.OpenApi`
10.0.x resolves 2.0.0, which carries advisory
[GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

## Trimming and Native AOT

The whole package is annotated `[RequiresUnreferencedCode]`: reading an enum's metadata needs
reflection. It carries no `[RequiresDynamicCode]`, because describing a schema generates no code.
