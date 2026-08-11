# Contributing

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](docs/for-users/CONTRIBUTING.fr.md)

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

## A change arrives with its tests

Major new functionality lands with tests in the automated suite, in the same pull request. That much
is a policy rather than a check, because nothing can tell a feature from a refactoring — but the
parts around it are enforced: a change to the public surface fails the build until the committed
baseline records it, and a documentation sample that stops compiling fails like any other test.

Where the order can be chosen, write the test first. Not as a design method, and not TDD — the claim
is narrower than either. A regression test written after the fix has never been seen to fail, so
nothing establishes that it covers the bug rather than passing for some unrelated reason. Watching it
go red, and then watching the fix turn it green, is the only thing that does — the same argument as
the style job running the checker's own test before the checker. A fix therefore carries the test
that would have caught it, written before the fix wherever that is possible.

Wherever it is not — a test that cannot exist until the feature does — say so in the pull request
rather than dropping the step quietly.

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

`tools/style/lint-layout.sh` reports what does not follow this, `--fix` rewrites what it can and
names what it cannot — exiting non-zero for it, so a run that left a violation behind does not read
like a clean tree — and CI runs it without the flag, running the checker's own test first so that
"nothing to report" means something. This is checked rather than remembered. It stays silent on two
shapes it cannot read: a suppression sharing its brackets with another attribute, as in
`[Fact, SuppressMessage(...)]`, and a wrapped one whose closing `)]` carries a member after it.
Either way, knowing what to join means parsing C#. A trailing `//` comment is neither — it runs to
the end of the line, so nothing can hide behind it and the join is safe.

## Branches

Branch from `main`, named `<author>/<description>`:

```sh
git switch -c jane/flags-enum-pattern main
```

`main` takes no direct push, no force push and no merge commit. It moves only through a pull
request, squashed or rebased, so its history stays linear.

The merge commit is the half of that a contributor can trip over, so CI refuses one rather than
leaving it to be found at the merge button — where GitHub reports it as a rebase that cannot be
done, after review rather than before it. Bring `main` in by rebasing onto it:

```sh
git fetch origin main && git rebase origin/main
```

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

## Samples in the documentation are compiled

Every C# block under `docs/for-users` and in the two front pages is compiled against the shipped
packages, and the analyzers this library ships are then run over the result. The maintainer pages are
out of it, and as a body rather than one by one: a decision record quotes C# to carry an argument,
not to be pasted. A sample that no longer binds, or that
teaches code `EMN0005` would reject, fails the build like any other test — documentation is the one
thing nothing executes, so nothing else would ever notice.

Samples are fragments on purpose: an action without its controller, three statements without the
`Main` around them. Which wrapping a fragment needs is worked out by parsing it, so nothing has to
be declared. Two things do, each on the line above the fence and in both languages:

- `<!-- emn:allow=EMN0003 -->` — the sample shows the mistake deliberately, and the rule must fire.
  An allowance that no longer fires fails too, because the example has stopped being one.
- `<!-- emn:skip -->` — the sample is not code anybody could compile, such as a fragment of a call
  chain shown to point at a single line. It has to genuinely not compile: an opt-out that is no
  longer needed fails as well.

## Analyzer findings

A finding from Roslyn or SonarQube is a claim about the code, and [CLAUDE.md](CLAUDE.md) sets out
the six ways to answer one — from fixing it to leaving it visible as known debt. Two are worth
knowing before you reach for them: a suppression always carries a `Justification` saying why the
rule's premise does not hold at that site, and an `.editorconfig` exclusion is a decision to raise
first, because it also silences the rule everywhere obeying it would have been right.

## Decisions worth a record

A choice that outlives the pull request that made it — a library, a convention, a trade taken with
eyes open — is written down under [`docs/for-maintainers/adr`](docs/for-maintainers/adr), one file per decision, in both languages
like every other page. What the record is for is the reasoning: the alternatives that were real at
the time, and what the decision costs, so that a reader who disagrees can see what they would be
overturning. The first one is [NFluent for test assertions](docs/for-maintainers/adr/0001-nfluent-for-test-assertions.en.md).

## Reporting a vulnerability

Not here. A suspected vulnerability goes through a private advisory rather than a public issue,
discussion or pull request — see [SECURITY.md](SECURITY.md).
