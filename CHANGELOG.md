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
  name shadowing another member's C# name. An enum that declares no contract is never analysed.
- CI checks that fail the build if the produced package does not declare its
  `Microsoft.AspNetCore.App` framework reference, or does not ship the analyzers.
- `AspNetCore.EnumMemberNameBinding.OpenApi`, a companion package whose schema transformer makes the
  generated document describe what the server accepts: an explicit `string` type, the declared public
  names, and — for `[Flags]` enums, which ASP.NET Core documents with no value at all — a regular
  expression covering comma-separated combinations. Its tests assert document/runtime coherence by
  replaying every advertised value against the running server.
- The companion raises the floor of `Microsoft.OpenApi` to 2.11.0. `Microsoft.AspNetCore.OpenApi`
  10.0.x resolves 2.0.0, which carries advisory GHSA-v5pm-xwqc-g5wc.

### Changed

- A partially annotated enum is now **rejected by default**, at build time by `EMN0003` and at
  start-up by `EnumContractException`. A member without `[JsonStringEnumMemberName]` answers to its
  C# name, which puts an internal identifier into the public contract — the opposite of the point.
  `EnumMemberNameBindingOptions.AllowPartialContracts` opts back in for enums you do not own, and
  restores behaviour identical to `System.Text.Json`.

### Fixed

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
