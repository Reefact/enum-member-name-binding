# Contributing

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](docs/CONTRIBUTING.fr.md)

Thank you for considering a contribution. What follows is mostly a description of what the build
already enforces, so that nothing here is a surprise at review time: almost every rule below is
checked by a test, a workflow or a repository ruleset rather than by a reader's goodwill.

## Getting set up

The SDK floor is declared in `global.json`; anything newer rolls forward.

```sh
git clone https://github.com/Reefact/enum-member-name-binding.git
cd enum-member-name-binding
git config core.hooksPath .githooks
dotnet build -c Release
```

The `core.hooksPath` line enables two hooks: `commit-msg`, which checks a message before it is
recorded, and `pre-commit`, which checks the staged C# against the style rule below. A hook cannot
install itself, which is why this is a step rather than magic. Skipping it costs nothing at commit
time and costs a rewritten history later, when CI runs the same checks.

## Building and testing

```sh
dotnet build -c Release          # warnings are errors here, so a warning fails the build
dotnet test -c Release
tests/PackageSmokeTest/run.sh    # packs, publishes, runs, and calls a real HTTP endpoint
```

The smoke test is slower than the rest, and worth the wait whenever packaging, the analyzers or a
public entry point changed. It starts at `dotnet pack` and ends at an HTTP response, so it is the
only check that would notice a package that compiles and does not work.

CI runs the build and the tests on two SDKs — the floor from `global.json` and the latest 10.0.x —
because a disagreement between analyzer versions belongs here rather than in a consumer's build.

## Coding style

Most of it lives in `.editorconfig`, which your editor already reads. One rule it cannot express: an
`if` whose whole body is a single exit — `return`, `throw`, `continue`, `break` — is written on one
line.

```csharp
if (string.IsNullOrEmpty(name)) { return Problem.EmptyName(memberName); }
if (isFlags && name.Contains(',')) { return Problem.CommaInFlagsName(memberName, name); }
```

The guard and what it does are one thought, and three lines make the reader assemble it. A run of
them is one block, so consecutive guards take no blank line between them — the height the one-line
form saves is the whole point of it. Anything less trivial than a bare exit keeps the multi-line
form, and so does a guard that would run past 140 characters collapsed, since beyond that width the
one line stops being the easier read. That is a ceiling rather than an exemption: a guard too wide
to fit is usually one whose condition wants a name.

`tools/style/lint-single-line-exits.sh` reports what does not follow this, `--fix` rewrites it, and
CI runs it without the flag. So this is checked rather than remembered.

## Branches

Branch from `main`, named `<author>/<description>`:

```sh
git switch -c jane/flags-enum-pattern main
```

`main` takes no direct push, no force push and no merge commit. It moves only through a pull
request, squashed or rebased, so its history stays linear.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/), enforced by
`tools/commit-lint/lint-commit-message.sh` — the one script both the hook and CI run, so the two
cannot drift apart:

| Rule | Shape |
| --- | --- |
| Header | `<type>[(scope)][!]: <description>` |
| Type | `feat`, `fix`, `build`, `chore`, `ci`, `docs`, `perf`, `refactor`, `revert`, `style`, `test` |
| Scope | optional; lowercase kebab-case, naming an area rather than a file |
| Description | imperative and lowercase, with no trailing period |
| Length | 72 characters, which is where GitHub truncates its commit list |
| Breaking change | `!` in the header **and** a `BREAKING CHANGE:` footer carrying the migration |

Only the header is validated. Bodies are prose and are left alone — write them for the reader who
asks why, six months from now, rather than what.

```sh
git log -1 --format=%B | tools/commit-lint/lint-commit-message.sh -
```

## Pull requests

Fill in the template. Its checklist is enforced by tests rather than trusted, and the two sections
below are the ones that catch people out. Review threads must be resolved before merge, and `CI`
is the required check: a single job that fails if any leg of the build matrix did.

## The public API

Both packable projects carry a committed baseline. Changing the public surface fails the build
until the same change is written into the `PublicAPI.Unshipped.txt` beside the project. That is
the point rather than a chore: the surface moves in a diff someone reviewed, never as a side
effect of an edit made for another reason.

## Documentation is bilingual

Every page exists in English and in French, and the pair is compared structurally: the same number
of headings, bullets and table rows, and the same sequence of code-fence languages. Updating one
side only fails the suite, deliberately — a translation that quietly falls behind is worse than an
absent one, because it is still believed.

The English README is also the NuGet package page, where a relative link is dead, so its links
into this repository are absolute. Pages under `docs/` are only ever read on GitHub and link
relatively.

## Analyzer findings

A finding from Roslyn or SonarQube is a claim about the code, and [CLAUDE.md](CLAUDE.md) sets out
the six ways to answer one — from fixing it to leaving it visible as known debt. Two are worth
knowing before you reach for them: a suppression always carries a `Justification` saying why the
rule's premise does not hold at that site, and an `.editorconfig` exclusion is a decision to raise
first, because it also silences the rule everywhere obeying it would have been right.

## Reporting a vulnerability

Not here. A suspected vulnerability goes through a private advisory rather than a public issue,
discussion or pull request — see [SECURITY.md](SECURITY.md).
