# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The package version is independent of the .NET version it targets.

## [Unreleased]

### Added

- `AddEnumMemberNameBinding()` on `IMvcBuilder`: route values, query strings, form fields and
  headers accept the enum member names declared with `[JsonStringEnumMemberName]`.
- `EnumMemberNameConverter`, a `TypeConverter` driven by the native attribute. ASP.NET Core resolves
  simple-type binders through `TypeDescriptor`, so no model binder is replaced and nullable enums,
  headers and form fields are covered by construction.
- Start-up validation of every registered contract, raising `EnumContractException` for duplicate
  public names, names with surrounding whitespace, and commas inside a `[Flags]` member name.
- `[Flags]` support: comma-separated lists, matching `System.Text.Json`.
- A parity test suite that uses `JsonSerializer` itself as the oracle — for each candidate input,
  the HTTP outcome must equal the body outcome.
- Roslyn analyzers, shipped inside the package under `analyzers/dotnet/cs`, so a contract mistake is
  a build error rather than a start-up exception: `EMN0001` duplicate public name, `EMN0002` unusable
  public name, `EMN0003` incomplete contract, `EMN0004` comma in a `[Flags]` name, `EMN0005` a public
  name shadowing another member's C# name — which leaves that member answering to every casing of its
  name except its own. All five are errors. An enum that declares no contract is never analysed.
- CI checks that fail the build if the produced package does not declare its
  `Microsoft.AspNetCore.App` framework reference, or does not ship the analyzers.
- `AspNetCore.EnumMemberNameBinding.OpenApi`, a companion package whose schema transformer makes the
  generated document describe what the server accepts: an explicit `string` type, the declared public
  names, and — for `[Flags]` enums, which ASP.NET Core documents with no value at all — a regular
  expression covering comma-separated combinations. Its tests assert document/runtime coherence by
  replaying every advertised value against the running server.
- The companion raises the floor of `Microsoft.OpenApi` to 2.11.0. `Microsoft.AspNetCore.OpenApi`
  10.0.x resolves 2.0.0, which carries advisory GHSA-v5pm-xwqc-g5wc.

- `EnumMemberNames.GetPublicName(Enum)`, for generating links. ASP.NET Core formats route values
  without consulting `TypeDescriptor`, so a link built from an enum value carries the C# name and the
  binder refuses it. That gap cannot be closed from a `TypeConverter`; it is documented and this is
  the way round it.

### Changed

- A partially annotated enum is now **rejected by default**, at build time by `EMN0003` and at
  start-up by `EnumContractException`. A member without `[JsonStringEnumMemberName]` answers to its
  C# name, which puts an internal identifier into the public contract — the opposite of the point.
  `EnumMemberNameBindingOptions.AllowPartialContracts` opts back in for enums you do not own, and
  restores behaviour identical to `System.Text.Json`.

### Fixed

- **`EMN0005` missed most of the shape it exists to catch.** The analyzer compared a declared public
  name against another member's C# name ordinally, while the runtime looks those names up
  case-insensitively — so `[JsonStringEnumMemberName("blue")]` next to a `Blue` member went
  unreported. Both now compare case-insensitively.
- **The shadowing check did not exist at run time at all**, although the documentation claimed every
  analyzer rule was also enforced at start-up. `EnumContract` now rejects it, including when
  `AllowPartialContracts` is set: the collision is an ambiguity, not a policy choice.
- **Whitespace handling diverged from `System.Text.Json` in three ways.** A value was matched without
  being trimmed, so `" available "` and `" read "` were refused where the request body accepts them;
  and a trailing comma in a `[Flags]` list was refused where the body tolerates one. The behaviour was
  characterized against `JsonSerializer` and reproduced, and the whole matrix is now in the parity
  suite.
- **The `[Flags]` pattern in the OpenAPI document excluded forms the binder accepts** — leading and
  trailing whitespace, and the trailing comma. The document advertised a stricter contract than the
  server honoured.
- **The five analyzer help links pointed at pages that did not exist**, so the IDE link led to a 404.
  Every rule now has a page under `docs/rules`, and a test fails if a rule and its page ever diverge.
- **Writing a `[Flags]` combination diverged from `System.Text.Json`.** The decomposition ran in
  declaration order, while the serializer sorts members topologically so that a combination covering
  several bits wins over its constituents. `7` was written `read, write, delete` where the serializer
  writes `read_write, delete`, and an sbyte flags enum ordered its members differently again. Two
  independent shapes were enough to rule out the two obvious tie-breaking rules, so combinations are
  now handed to the serializer itself rather than imitated — parity by construction. A declared
  member is still answered from the cache, so only combinations pay for it.
- **The `[Flags]` OpenAPI pattern used escapes that ECMA-262 rejects.** `Regex.Escape` escapes
  whitespace and `#`, producing `\ ` and `\#`; neither is a valid identity escape in the dialect a
  JSON Schema `pattern` is read with, so a strict consumer would reject the whole pattern. Only
  syntax characters are escaped now, and a test rejects any other escape.
- **No entry point carried its trimming or Native AOT constraint.** The trim and AOT analyzers are
  now enabled on both packages, which surfaced nine diagnostics that nothing reported before. Every
  public entry point is annotated with `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, so a
  consumer compiling for either gets an accurate warning instead of a silent failure at run time.
  The two interface implementations that cannot carry the attributes suppress them explicitly, next
  to a constructor that does.
- **The README showed the previous `[Flags]` pattern**, before surrounding whitespace and the
  trailing comma were allowed. Corrected, and a test now compares the documented pattern against the
  one the transformer emits.
- **Registering the same enum twice stacked a new `TypeDescriptor` provider each time.** A type is
  now registered once per process, while validation still runs on every call so a second
  registration with stricter options still fails. Covered by tests that host several applications
  side by side.

- `Microsoft.AspNetCore.OpenApi` and minimal API serialization read `Http.Json.JsonOptions`, while
  MVC reads `Mvc.JsonOptions`. Only the latter was configured, so every contract enum was described
  as an integer in the generated document. Both are now configured, still one converter per contract
  type.

### Known limitations

- **Minimal APIs are not covered.** Their parameter binding uses neither MVC model binders nor
  `TypeDescriptor`; it requires a `static TryParse`/`BindAsync` on the bound type, which cannot be
  added to an `enum`. This is a platform-level constraint, not an implementation gap.
- **An empty value on a nullable enum parameter binds `null`** rather than being rejected, where
  `System.Text.Json` rejects `""`. ASP.NET Core resolves it before any `TypeConverter` is consulted.
  A test pins the behaviour.
- **Not compatible with trimming or Native AOT.** `TypeDescriptor` and the assembly scan rely on
  reflection. The public entry point is annotated accordingly rather than silently suppressing the
  warnings.
- Registration must happen at start-up: ASP.NET Core caches the model binder built for a type on
  first use.
