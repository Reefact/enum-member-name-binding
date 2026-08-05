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
- A CI check that fails the build if the produced package does not declare its
  `Microsoft.AspNetCore.App` framework reference.

### Known limitations

- **Minimal APIs are not covered.** Their parameter binding uses neither MVC model binders nor
  `TypeDescriptor`; it requires a `static TryParse`/`BindAsync` on the bound type, which cannot be
  added to an `enum`. This is a platform-level constraint, not an implementation gap.
- **OpenAPI documents are not yet corrected.** On .NET 10 the generated document advertises contract
  names for query and route parameters that stock ASP.NET Core rejects; from .NET 11 it will
  advertise C# names instead, which this library makes wrong in the other direction. A companion
  package is planned.
- **Not compatible with trimming or Native AOT.** `TypeDescriptor` and the assembly scan rely on
  reflection. The public entry point is annotated accordingly rather than silently suppressing the
  warnings.
- Registration must happen at start-up: ASP.NET Core caches the model binder built for a type on
  first use.
