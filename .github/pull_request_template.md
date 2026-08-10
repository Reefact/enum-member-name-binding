<!--
  Write this pull request in ENGLISH, like the commits and the code.

  Title: name the whole change. A pull request carrying one intention mirrors its commit
  header (type(scope): description); one carrying several takes a short descriptive title.
  Issue links go under "Related issues", not in the title.

  Fill in what applies and delete what does not. Do not invent anything, and tick a testing
  box only for something actually run.
-->

## Summary

<!-- One or two sentences: what does this change, and why. -->

## Type of change

* [ ] Bug fix
* [ ] New feature
* [ ] Breaking change to the public API
* [ ] Refactoring
* [ ] Analyzer / diagnostic change
* [ ] Tests
* [ ] Documentation
* [ ] Build / CI / tooling

## Changes

<!-- The concrete changes, as bullets. Factual. -->

*

## Testing

<!-- Tick only what was run. If something was not, say so and why. -->

* [ ] `dotnet build -c Release` — clean, warnings are errors here
* [ ] `dotnet test -c Release`
* [ ] `tests/PackageSmokeTest/run.sh` — required when packaging, the analyzers, or a public
      entry point changed: it is the only check that starts from `dotnet pack` and ends at an
      HTTP response

## Public API

<!--
  Both packable projects carry a committed baseline. A change to the public surface fails the
  build until the same change updates PublicAPI.Unshipped.txt beside the project — which is the
  point: the surface moves in a reviewed diff, never as a side effect.
-->

* [ ] No change to the public surface
* [ ] The surface changed and the baseline was updated in the same commit

## Documentation

<!--
  Pages under docs/ are checked structurally: every link must resolve, and a translated page
  must carry the same headings, bullets and table rows as its counterpart. A page updated on
  one side only fails the suite.
-->

* [ ] README / `docs/` updated
* [ ] The French counterpart was updated to match
* [ ] `CHANGELOG.md` and `docs/for-users/CHANGELOG.fr.md` both updated
* [ ] No documentation change required

## Related issues

<!-- e.g. Closes #123 -->
