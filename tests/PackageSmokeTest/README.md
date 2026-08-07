# Package smoke test

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./README.fr.md)

Everything under `tests/` reaches the library through a `ProjectReference`. That proves the code
works. It does not prove the *package* works, and those are different claims: a `ProjectReference`
never exercises the framework reference, the placement of the analyzers inside the `.nupkg`, the
`lib/` layout, the MSBuild assets, or whether somebody with their own project defaults can compile
against any of it.

This directory starts from `dotnet pack` and ends at an HTTP response:

```
source → pack → NuGet restore → consumer compile → consumer analyzer → Kestrel → request
```

Run it with `tests/PackageSmokeTest/run.sh`. It is wired into both workflows.

## Why these are applications and not tests

`AddEnumMemberNameBinding()` with no options — the call the README leads with — scans
`Assembly.GetEntryAssembly()`. Under a test host that is `testhost.dll`, so no xUnit test in this
repository can reach that code path, and the existing suites all pass explicit types instead. Only a
real process entered through its own `Main` exercises it, which is why `Consumer` is an application.

## What is here

| | |
|---|---|
| `Consumer/` | A transcription of the README: zero-option registration, a contract enum, an enum with no contract, and the OpenAPI companion. Referenced by package, never by project. |
| `InvalidContract/` | **Meant not to compile.** A partial contract, so that `EMN0003` has to arrive from the analyzer inside the `.nupkg`. |
| `Directory.Build.props` | Empty, and load-bearing — it stops the fixtures inheriting the repository's build settings so they resemble a stranger's project. |
| `NuGet.config` | `<clear />` plus the run-local feed, so nothing else can serve the package under test. |
| `.work/` | Generated: the feed, an isolated NuGet package directory, and logs. Deleted at the start of every run. |

## The three things that would make this lie

A package smoke test that quietly stops testing the current bits is worse than none, so freshness has
three independent guarantees: `.work/` is deleted on every run; `NUGET_PACKAGES` points inside it, so
the machine's global cache cannot serve a stale extraction; and the package is packed as
`0.0.0-smoke`, a version that is never published and therefore cannot come from nuget.org. The run
then asserts that the version resolved from the local feed rather than assuming it.

The `EMN0003` assertion is deliberately positive — the build must fail, *and* the failure must name
that rule. "No diagnostic appeared" is also what you get when the analyzer never loaded, so a check
written that way would go green at the exact moment packaging broke.
