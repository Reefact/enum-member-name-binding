# Rulesets

The branch and tag protection of this repository, kept as files rather than as settings somebody
remembers configuring. GitHub can export and import a ruleset as JSON, so the rules are reviewable
in a diff like everything else, and a repository restored from this clone can be protected again by
importing two files.

## Importing

*Settings* → *Rules* → *Rulesets* → *New ruleset* → *Import a ruleset*, once per file. The import
form shows the parsed rules before saving, which is the moment to add a bypass actor (see below).

## `main.json`

Blocks deletion and force-push, requires a linear history, and — the point of the exercise —
requires a pull request. `main` is written only by merge from a reviewed branch.

**Zero required approvals**, deliberately. GitHub does not let anyone approve their own pull
request, so on a single-maintainer repository any non-zero count would block every pull request and
force a bypass on each one, which is worse than no rule at all. What carries the weight instead is
`required_review_thread_resolution`: an unresolved comment blocks the merge. Raise the count to 1
the day a second contributor appears.

**One required status check: `CI`.** Requiring a pull request without requiring a green one is half a
rule — it stops the direct push and still lets a red branch merge, which is the hole the exercise
was meant to close.

`CI` is a single aggregate job in `ci.yml` whose only work is to read the build matrix's collective
result. Requiring it, rather than the per-matrix names `build (SDK 10.0.100)` and
`build (SDK 10.0.x)`, is what keeps the rule from becoming a trap: a required check that names a job
nobody runs never reports, and every pull request then sits at *Expected — waiting for status* with
no way out but an admin bypass. Adding an SDK or an operating system to the matrix must not require
anyone to remember this file.

`strict_required_status_checks_policy` additionally refuses a branch that is behind `main`, so a
change is proved against the tip it will land on rather than against whatever `main` was when the
branch was cut.

**Editing an already-imported ruleset.** Importing does not link the file to the rule — it copies it
once. A change here reaches GitHub only by editing the live ruleset to match, or by deleting it and
importing again.

**Linear history** means no merge commits: land pull requests with rebase, or with squash when the
branch carries a single intention. This suits a repository whose history is already linear and whose
commits are each a coherent unit worth keeping — squashing a multi-commit branch would destroy that
granularity, so rebase is the default and squash the exception. Drop the rule if you would rather
preserve branches through merge commits.

**No bypass actor** is listed, so the rules apply to everyone including the owner. Add *Repository
admin* in the import form if you want an escape hatch for an incident; leaving it empty is the
stricter and more honest setting, since an owner who can always bypass is an owner who eventually
does.

## `release-tags.json`

Blocks deletion and update of any `v*` tag.

Small, and the most easily forgotten rule in the repository. The Git tag is the single source of
truth for the published version: `Directory.Build.props` derives `Version` from nothing else, and
`release.yml` passes `${GITHUB_REF_NAME#v}` to `dotnet pack`. A tag deleted and recreated on another
commit would publish a different artifact under a version number nuget.org has already served — and
nuget.org never lets a version be republished. Creation stays open, since that is how a release
happens.
