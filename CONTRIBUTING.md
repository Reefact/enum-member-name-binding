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

Most of it lives in `.editorconfig`, which your editor already reads. Three rules it cannot express,
all the same complaint: a line that is not a thought, leaving the reader to assemble one.

An `if` whose whole body is a single exit — `return`, `throw`, `continue`, `break` — is written on
one line, and a run of them is one block with no blank line between them:

```csharp
if (string.IsNullOrEmpty(name)) { return Problem.EmptyName(memberName); }
if (isFlags && name.Contains(',')) { return Problem.CommaInFlagsName(memberName, name); }
```

A declaration does not break after the `=` when its value fits beside the name:

```csharp
internal const string Reflection = "Enum member name binding reads enum metadata reflectively…";
```

And a suppression attribute is written on one line, at any width:

```csharp
[UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = SuppressionJustification.IL2026.RequirementCarriedByConstructor)]
```

The guard and what it does are one thought; a name and its value are another. Anything less trivial
than a bare exit keeps the multi-line form, and so does a value that genuinely needs more than one
line — a concatenation, a long initializer. Width is a ceiling for those two, 140 characters for a
guard and 160 for a value, and a ceiling rather than an exemption: something too wide to fit is
usually something that wants a name of its own.

A suppression has no ceiling, and the exception is deliberate. A member usually carries more than
one, and wrapped at the comma they interleave into a paragraph where two of them differ by one token
somewhere in the middle — telling them apart is then a diff done by eye, which is how a duplicate
survives being looked at. The other two rules stop where the single line stops being the easier
read, because they are logic; a suppression is scanned for which rule and skimmed for why, never
read for its logic, so wrapping it saves nothing and costs the comparison.

`tools/style/lint-layout.sh` reports what does not follow this, `--fix` rewrites it, and CI runs it
without the flag — running the checker's own test first, so that "nothing to report" means
something. This is checked rather than remembered. It stays silent on one shape it cannot read: a
suppression sharing its brackets with another attribute, as in `[Fact, SuppressMessage(...)]`,
where knowing what to join means parsing C#.

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

## Decisions worth a record

A choice that outlives the pull request that made it — a library, a convention, a trade taken with
eyes open — is written down under [`docs/adr`](docs/adr), one file per decision, in both languages
like every other page. What the record is for is the reasoning: the alternatives that were real at
the time, and what the decision costs, so that a reader who disagrees can see what they would be
overturning. The first one is [NFluent for test assertions](docs/adr/0001-nfluent-for-test-assertions.en.md).

## Reporting a vulnerability

Not here. A suspected vulnerability goes through a private advisory rather than a public issue,
discussion or pull request — see [SECURITY.md](SECURITY.md).
