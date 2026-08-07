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

**No required status check yet.** The check to require is a single aggregate job named `CI`, and it
does not exist yet — see `.github/ci-cd-plan.md` §2.1. Requiring the current per-matrix check names
(`build (SDK 10.0.100)`, `build (SDK 10.0.x)`) would be the very trap that job exists to avoid: the
day the matrix changes, the required name disappears and every pull request blocks forever waiting
for a check that will never report. Add the rule once the aggregate job is on `main`:

```json
{
  "type": "required_status_checks",
  "parameters": {
    "strict_required_status_checks_policy": true,
    "required_status_checks": [{ "context": "CI" }]
  }
}
```

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
