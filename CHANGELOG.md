# Changelog

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](docs/for-users/CHANGELOG.fr.md)

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The package version is independent of the .NET version it targets.

## [Unreleased]

The public surface is the one 1.0.0 will carry: it was read symbol by symbol and committed as a
baseline, so this pre-release is not a draft of the API. What it exercises is the publication
itself — trusted publishing, the build-provenance attestation, and how the packages actually look
once nuget.org has received and re-signed them. A version number cannot be taken back, so the
first one to make the trip is one that costs nothing to burn.

### Added

- `AddEnumMemberNameBinding()` on `IMvcBuilder`: route values, query strings, form fields and
  headers accept the enum member names declared with `[JsonStringEnumMemberName]`.
- Binding through a model binder registered on the application's own `MvcOptions`, inserted
  immediately ahead of the provider ASP.NET Core uses for enums — not at the front, which would take
  `[FromBody]` away from `System.Text.Json`. Everything the registration configures lives in that
  application's container, so a second application hosted in the same process is untouched whether
  it starts before or after. The binder is an implementation detail: `AddEnumMemberNameBinding()` is
  the only supported way in.
- A committed public API baseline for both packages, so a change to the published surface is a
  reviewed diff rather than a side effect. The surface was read symbol by symbol before this
  release and deliberately kept to what a consumer needs: 19 entries in the main package, 2 in the
  companion.
- The registration is configurable through `EnumMemberNameBindingOptions`: `AddEnum<TEnum>()` names
  a contract explicitly and `ScanAssemblyContaining<T>()` points the scan somewhere other than the
  entry assembly, with `EnumTypes` and `Assemblies` underneath as escape hatches for a caller
  holding a `Type` or an `Assembly` at run time. `AllowPartialContracts` accepts an enum annotated
  in part, and `ConfigureJsonSerialization` declines the `System.Text.Json` half of the
  registration for an application that configures its converters itself. Naming anything at all is
  taken as "scan nothing else", so the entry assembly is a default rather than an addition.
- Start-up validation of every registered contract, raising `EnumContractException` for duplicate
  public names, names with surrounding whitespace, and commas inside a member name.
- Registration is all or nothing, on both paths — an explicit list and the assembly scan. Every
  contract is resolved and validated before anything at all is configured: a list naming one good
  contract and one malformed one would otherwise leave the good one wired up behind an exception that
  reads as though nothing had happened. The refusal names `options`, the parameter the caller actually
  wrote, rather than a local the implementation unpacks the list into.
- Argument guards on every public and internal boundary, so a null the signature forbids raises
  `ArgumentNullException` naming the parameter rather than a `NullReferenceException` from further
  in. A nullable annotation binds only the callers that opted into it — not one compiled with
  nullable disabled, and not a value arriving through reflection, dependency injection or a
  deserializer. `TryParse` is the one whose answer changes rather than its message: `null` reached
  `AsSpan()`, which yields an empty span, so a broken signature was reported exactly like the empty
  string — a value a caller may legitimately send.
- Comma-separated lists, matching `System.Text.Json` — on every enum and not only on a `[Flags]`
  one, because neither `Enum.Parse` nor `System.Text.Json` looks at the attribute before splitting.
  Refusing them would have made a registered enum stricter than the same enum left alone.
- A parity test suite that uses `JsonSerializer` itself as the oracle — for each candidate input,
  the HTTP outcome must equal the body outcome. It runs over every underlying type an `enum` can
  have and not `int` alone, because the parse widens each member to `ulong`, ORs them and narrows
  the result back: sign extension and the top bit are precisely what that arithmetic can lose, and
  an `int` enum with no negative member exercises neither.
- `EMN0006`, reporting a public name at least one channel cannot carry. The forbidden set was
  established by sending each character over all five channels against a running server, not read off
  a specification: a slash is refused inside a route segment, and a line break or a character outside
  printable ASCII is refused in a header. `?`, `#`, `&`, `=`, `+`, `%`, space, tab, backslash and
  quote all travel intact. A warning rather than an error, because whether it bites depends on the
  channels an API actually binds from — the message names the character and the channel that refuses
  it. The measurement itself is pinned by tests.
- Roslyn analyzers, shipped inside the package under `analyzers/dotnet/cs`, so a contract mistake is
  a build error rather than a start-up exception: `EMN0001` duplicate public name, `EMN0002` unusable
  public name, `EMN0003` incomplete contract, `EMN0004` comma in a public name, `EMN0005` a public
  name shadowing another member's C# name — which leaves that member answering to every casing of its
  name except its own. Those five are errors; `EMN0006` above is the one warning, because a
  portability limit depends on the channels an API actually binds from, whereas the other five report
  an ambiguity that is wrong on every channel. An enum that declares no contract is never analysed.
- A check on what is inside both published packages, run by CI and again by the release from one
  shared script. It fails the build if the main package does not declare its
  `Microsoft.AspNetCore.App` framework reference or does not ship the analyzers, and if the
  companion does not depend on the main package, does not ship the `build/*.targets` a consumer
  cannot do without, or declares a `Microsoft.OpenApi` below the floor that avoids
  GHSA-v5pm-xwqc-g5wc. The companion was previously verified by nothing.
- A package smoke test, run on both SDKs in CI and again as the last gate before publishing. It packs
  into a local feed, compiles an application that consumes the result by `PackageReference`, and
  drives it over HTTP — covering the ground a `ProjectReference` skips entirely: the framework
  reference, the analyzers' place inside the package, the MSBuild assets, and whether a project with
  its own defaults can compile against any of it. It is also the only thing that exercises the call
  the README leads with, since `AddEnumMemberNameBinding()` with no options scans the entry assembly,
  which under a test host is the test host. A second fixture is meant *not* to compile, so `EMN0003`
  has to arrive from the analyzer inside the `.nupkg`; the assertion is positive, because "no
  diagnostic appeared" is also what a missing analyzer looks like.
- A documentation test suite: every C# sample under `docs/` and in the README is compiled against the
  shipped packages, and the analyzers are then run over the result. Documentation is the one thing
  nothing executes, so a renamed option or a sample written from memory reads perfectly and stays
  wrong until a newcomer finds out. A sample that deliberately shows a mistake declares the rule it
  demonstrates, and an allowance that no longer fires fails too — a page saying "this is what
  `EMN0001` looks like", above code that no longer trips it, has stopped being an example.
- `AspNetCore.EnumMemberNameBinding.OpenApi`, a companion package whose one entry point,
  `AddEnumMemberNames()` on `OpenApiOptions`, installs a schema transformer that makes the
  generated document describe what the server accepts: an explicit `string` type, the declared public
  names, and — for `[Flags]` enums, which ASP.NET Core documents with no value at all — a regular
  expression covering comma-separated combinations. It describes the enums the application
  registered and no others — carrying the attribute is not the same as being covered, and an enum
  nobody registered binds by its C# names and serializes as a number, so announcing its declared
  names would be wrong about the query string and the body at once. Used without the main package it
  has no registration to consult and describes every contract enum, which is the shape an application
  serializing through its own converters wants. Its tests assert document/runtime coherence by
  replaying every advertised value against the running server.
- The companion raises the floor of `Microsoft.OpenApi` to 2.11.0. `Microsoft.AspNetCore.OpenApi`
  10.0.x resolves 2.0.0, which carries advisory GHSA-v5pm-xwqc-g5wc.
- An icon on both packages, so they are recognisable on nuget.org instead of appearing behind the
  default placeholder. The smoke test checks each package for both halves of it — that the `.nuspec`
  declares an icon, and that the file it names is really inside — since keeping the include without
  the property produces a perfectly valid package that nuget.org still shows grey.
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

- **Of two unannotated members differing only by case, one was unreachable.** The C# names were held
  in a single case-insensitive dictionary, so `Read` and `read` collided and the second was dropped —
  the token naming it exactly then resolved to the first. `System.Text.Json` matches the exact
  spelling before falling back, so the query string and the request body answered the same word with
  two different values, on an enum registered with `AllowPartialContracts`. The exact name is now
  matched first, and only a casing matching none of them exactly falls back — to the member the
  serializer picks, which is the one first in `Enum.GetNames` order and neither the first declared
  nor the lowest-valued. Declared names are unaffected and stay case-sensitive.
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
- **Two members sharing a numeric value could be written under two different names in one
  application.** The value-to-name map was built in declaration order, and `System.Text.Json` builds
  it in `Enum.GetNames` order — which sorts by the binary value and, among members sharing one, does
  not keep the order they were written in. So a response body said `shipped` while a link built with
  `EnumMemberNames.GetPublicName` for the same value said `in_transit`. The map is now read off
  `Enum.GetNames`, so the two agree by construction; seven shapes were measured against
  `JsonSerializer` and three of them disagreed under the old rule. Reading is unaffected — both names
  always parsed back to the same value — and the declaration order the OpenAPI document and the
  "allowed values" sentence are built from is unchanged.
- **A `[Flags]` contract enum bound a combination ASP.NET Core refuses**, which is the one thing this
  package promises never to do. The binder took `[Flags]` for an exemption from the undefined-value
  check and answered yes without asking, on the reasoning that a value built by OR-ing declared
  members decomposes into them by construction. It does not: two declared composites that overlap can
  cover a bit no single member supplies, so an enum declaring `3` and `6` bound `read_write,write_delete`
  as `7` where the same enum left alone answers 400. It now runs the test `EnumTypeModelBinder` runs —
  a value that decomposes prints its members' names, one that does not prints its number back — and
  the parity suite pins it with an untouched enum of the same shape as the control.
- **The `[Flags]` pattern in the OpenAPI document excluded forms the binder accepts** — leading and
  trailing whitespace, and the trailing comma. The document advertised a stricter contract than the
  server honoured.
- **The analyzer help links pointed at pages that did not exist**, so the IDE link led to a 404.
  Every rule now has a page under `docs/for-users/rules`, and a test fails if a rule and its page ever diverge.
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
  entry point is annotated, so a consumer compiling for either gets an accurate warning instead of a
  silent failure at run time. The two interface implementations that cannot carry the attributes
  suppress them explicitly, next to a constructor that does.
  The two constraints are applied **separately**, since they are not the same constraint: reading an
  enum's metadata needs reflection but generates no code. `GetPublicNames`, `IsFlagsContract`, the
  MVC registration and the whole OpenAPI package therefore carry `[RequiresUnreferencedCode]` only;
  `GetPublicName`, the `[Flags]` formatting path and the construction of the generic JSON converters
  carry both. A consumer is told about dynamic code only on a path that actually generates some.
- **The README showed the previous `[Flags]` pattern**, before surrounding whitespace and the
  trailing comma were allowed. Corrected, and a test now compares the documented pattern against the
  one the transformer emits.
- **Registering the same enum twice stacked a second registration each time.** One binder provider
  is now installed per application however many times the method is called, while validation still
  runs on every call so a second registration with stricter options still fails. Covered by tests
  that host several applications side by side, opted in and opted out.
- **The documented one-package install of the OpenAPI companion did not compile.**
  `Microsoft.AspNetCore.OpenApi` enables the interceptor namespace its XML comment generator writes
  into, and it does so through MSBuild build assets, which NuGet does not flow transitively. A
  consumer who took the companion and nothing else — exactly what `docs/for-users/openapi.en.md` instructs —
  therefore inherited the generator without the property that makes its output legal, and their build
  failed with CS9137 inside generated code they never wrote. Referencing
  `Microsoft.AspNetCore.OpenApi` directly also cured it, and most consumers will already have done
  so, which is why nothing in this repository noticed. The companion now ships that property itself,
  from a `.targets` and not a `.props`: NuGet imports the former below the consumer's project body
  and the latter above it, so a consumer who assigns `InterceptorsNamespaces` rather than appending
  to it would silently overwrite a `.props` and get CS9137 back. Microsoft ships it from a `.targets`
  for that reason, and their package survives that assignment where a `.props` version of ours was
  measured not to. It enables the one namespace Microsoft enables and nothing more. Found by the
  package smoke test on the first run of its life, and the consumer fixture now makes that
  assignment itself so the distinction cannot be lost again.
- `Microsoft.AspNetCore.OpenApi` and minimal API serialization read `Http.Json.JsonOptions`, while
  MVC reads `Mvc.JsonOptions`. Only the latter was configured, so every contract enum was described
  as an integer in the generated document. Both are now configured, still one converter per contract
  type.
- A `JsonStringEnumConverter` the application had registered before `AddEnumMemberNameBinding()`
  decided what a contract enum accepted in the request body. `System.Text.Json` takes the first
  converter in the list whose `CanConvert` answers true, and this package appended its own; the stock
  converter's default is `allowIntegerValues: true`, so a body of `{"status": 1}` was accepted while
  `?status=1` answered 400 — the exact divergence the package exists to remove, reintroduced by an
  application asking for string enums. The converters are now inserted at the head of both option
  objects. That also settles the order: one registered *after* this call is appended and lands behind
  them either way, so the vocabulary no longer depends on which of the two calls was written first.
  An application that does want its own converter for a contract enum still declines this half with
  `ConfigureJsonSerialization`, which leaves the binding in place.

### Documentation

- The README was split. It had grown to a length nobody reads before adopting a package, so the front
  page now carries the problem, the installation, one example, the channel table, the guarantees and
  the two limitations worth knowing before adopting — the rest moved, unabridged, to
  `docs/for-users/contract-rules.en.md`, `docs/for-users/analyzers.en.md`,
  `docs/for-users/openapi.en.md` and `docs/for-users/limitations.en.md`.
  The README is also the NuGet package page, where a relative link is dead, so it links to GitHub
  absolutely; a test fails on a relative one, and on any link — in any page — that points at a file
  or a heading that does not exist.
- The documentation is now bilingual, following the convention used across Reefact projects: every
  page exists as `Xxx.en.md` and `Xxx.fr.md` under `docs/for-users`, each opening with a link to its
  counterpart. The README keeps its name and its place, since NuGet renders it; its French version is
  `README.fr.md`, and the changelog follows the same rule. Tests fail on a page that exists in
  only one language, on a page that does not offer the other one, and on a translation whose
  structure no longer lines up with the original — words are translated, sections, bullets, table
  rows and snippets are neither dropped nor added. That last one is what catches an entry appended to
  one changelog and not the other. The analyzers' help links point at the English rule pages, which
  are the canonical ones.

- The pages are filed by who reads them: `docs/for-users` for the documentation a consumer reads, and
  `docs/for-maintainers` for the decision records. The split is what the suites read too — the
  compile contract covers `for-users` and nothing else, so a maintainer page written tomorrow is out
  of it without anyone remembering to exclude it. The analyzers' help links moved with the rule pages
  they point at, to `docs/for-users/rules/`.

- Every documentation folder carries an index — the `README.md` GitHub renders when someone opens
  it — so navigating the tree never depends on guessing a file name. An index is the one page whose
  whole job is to be complete and the one nothing else would notice going stale, so a test holds
  each one to the folder it speaks for: a page added beside an index that does not list it fails the
  build. The front page's French twin moved to the repository root, beside the English one, because
  `README.fr.md` under `docs/for-users` was occupying the name that folder's own index needed.

### Known limitations

- **Minimal APIs are not covered.** Their parameter binding uses neither MVC model binders nor
  `TypeDescriptor`; it requires a `static TryParse`/`BindAsync` on the bound type, which cannot be
  added to an `enum`. This is a platform-level constraint, not an implementation gap.
- **An empty value on a nullable enum parameter binds `null`** rather than being rejected, where
  `System.Text.Json` rejects `""`. ASP.NET Core settles an empty value before any parse is reached.
  A test pins the behaviour.
- **A combination naming no member binds in the body and nowhere else.**
  `"out_of_stock,discontinued"` is `(ProductStatus)3`, which `System.Text.Json` accepts and
  ASP.NET Core's own binder refuses on a non-`[Flags]` enum — including on an enum this package never
  touches, which is why closing it would be the wrong direction. Characterized, control included.
- **A contract enum parameter writes none of the binder's own log records.** ASP.NET Core's
  `SimpleTypeModelBinder` is handed an `ILoggerFactory` and logs its attempt and its result; the
  binder installed here takes no logger, so such a parameter is quiet at `Debug` where every other
  one is not. Only those records are missing — the `ParameterBinder` trace around them belongs to
  ASP.NET Core and is untouched, so a log still shows the parameter was bound and validated. They are
  written through `MvcCoreLoggerExtensions`, which is `internal`, so reproducing them would mean a
  lookalike under this package's own category and event ids: parity in appearance and none in fact.
  Both halves are measured.
- **Not compatible with trimming or Native AOT.** Resolving a contract and the assembly scan rely on
  reflection. The public entry point is annotated accordingly rather than silently suppressing the
  warnings.
- Registration must happen at start-up: ASP.NET Core caches the model binder built for a type on
  first use.
