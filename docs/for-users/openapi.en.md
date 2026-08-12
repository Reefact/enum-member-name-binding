# OpenAPI

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./openapi.fr.md)

```
dotnet add package Reefact.AspNetCore.EnumMemberNameBinding.OpenApi
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
| `Permissions` (`[Flags]`) | `{"type":"integer"}` | `{"type":"string"}`, plus the `pattern` [below](#the-flags-pattern) |
| `PlainPriority` (no contract) | `{"type":"integer"}` | `{"type":"integer"}` — untouched |

Two details worth knowing. ASP.NET Core emits enum values **without declaring a type**, which the
companion fixes. And for a `[Flags]` enum it deliberately emits no value at all — a closed list
cannot express combinations, so the companion emits a regular expression instead, which is both
precise and machine-checkable.

A third, less visible. One component describes the enum wherever it appears, so a nullable *use* of
it has to be expressed somewhere. A nullable property is emitted as a `oneOf` of a null schema and a
reference, which leaves the component alone; a nullable collection element — `List<ProductStatus?>`
— is not wrapped, and ASP.NET Core puts a JSON null among the component's own values instead. The
companion keeps that null and types the schema `["null","string"]`. A schema admitting no null gains
neither.

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

One thing changes place in that configuration. `AddEnumMemberNameBinding` resolves every contract
at start-up, so a malformed one — two members sharing a public name, say — raises
`EnumContractException` before the first request. The companion resolves a contract while it writes
the document instead, which under `MapOpenApi` is a request: the application starts, and
`/openapi/v1.json` answers 500 with the same message. The analyzers do not close the gap either,
since NuGet does not flow analyzer assets transitively, so an enum declared in your own assembly is
analysed and one arriving through a package reference is not. Registering the enums as well moves
the failure back to start-up.

## The `[Flags]` pattern

```json
{"type":"string","pattern":"^[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*(read|write|delete)([\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*,[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*(read|write|delete))*[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*,?[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*$"}
```

The pattern covers exactly what the binder accepts, whitespace and trailing comma included — see
[contract rules](contract-rules.en.md#a-flags-enum). One shape falls outside that, and only one: an
enum declaring overlapping composites, where a list the pattern admits can decompose into no member
and be refused with a 400 — see
[limitations](limitations.en.md#a-combination-naming-no-member-is-refused-outside-the-body).

Two things make it long, and both are the difference between describing the server and describing
something close to it. The whitespace class is written out rather than as `\s`, because a pattern is
read as ECMA-262, where `\s` includes U+FEFF and excludes U+0085 — while the binder trims with
`char.IsWhiteSpace`, which is the other way round on both. And a member left unannotated keeps its C#
name, which the binder matches ignoring case, so it appears as `[Dd][Ee][Ll][Ee][Tt][Ee]`; a declared
name is matched ordinally and appears as written. A class holds every character the binder treats as
equal, which for an ASCII letter is its two forms and is not always only that: the two forms are not
the same set as `OrdinalIgnoreCase` equality, and writing them as though they were was wrong in both
directions on a handful of code points.

It is written in the ECMA-262 dialect a JSON Schema `pattern` is read with, so only syntax characters
are escaped: `Regex.Escape` would produce `\ ` and `\#`, which are not valid identity escapes there
and would make a strict consumer reject the whole pattern. A test rejects any other escape.

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
not in the list. That is the direction worth being wrong in, and the alternative is not merely
uglier — it would be incorrect. A pattern is exact for a `[Flags]` enum because a combination of
declared members is, the one shape above aside, a value the server accepts. On an ordinary enum
only the combinations whose result names a declared member are accepted:
`out_of_stock,discontinued` is `1 | 2`, which names
none, and the server answers 400. A regular expression cannot tell those apart, so it would advertise
values that fail — over-promising, where the closed list under-promises. The list is also the
vocabulary a client generates from; a pattern would turn an enumeration into a free-text field in
every generated client.

## Version floor

The companion raises the floor of `Microsoft.OpenApi` to 2.11.0. `Microsoft.AspNetCore.OpenApi`
10.0.x resolves 2.0.0, which carries advisory
[GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

## Trimming and Native AOT

The whole package is annotated `[RequiresUnreferencedCode]`: reading an enum's metadata needs
reflection. It carries no `[RequiresDynamicCode]`, because describing a schema generates no code.
