#!/usr/bin/env bash
#
# The extractor decides what a release says it contains, and it runs once, inside a workflow, at the
# one moment nothing can be taken back. Every way it can be wrong is silent: a section that comes
# back empty reads exactly like a release with nothing to report, and a section that bleeds into the
# next one reads exactly like a thorough entry. Neither shows in a green run.
#
# So the shapes it must return, and the ones it must refuse, are named here instead.
#
# Usage: tools/release-notes/changelog-section.test.sh

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
extractor="$here/changelog-section.sh"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Deliberately awkward: a released version whose number is a prefix of a pre-release of the same
# number, an [Unreleased] section above them both, and a fenced block containing a line that starts
# with '## ' — which is a heading to anything reading line by line, and is not one.
cat > "$work/CHANGELOG.md" <<'CHANGELOG'
# Changelog

All notable changes are documented here.

## [Unreleased]

### Added

- Something not released yet.

## [1.0.0] - 2026-09-01

### Added

- The first stable release.

```sh
## not a heading, this is a shell comment inside a fence
echo hello
```

### Fixed

- A thing that was broken.

## [1.0.0-rc.1] - 2026-08-20

### Added

- The release candidate.

## [0.9.0]

- An entry with no subsection.

## [0.8.0] - 2026-07-01

## [0.7.0] - 2026-06-01

- Reachable only if 0.8.0 stopped at the right place.
CHANGELOG

fail() {
    echo "FAIL: $1"
    exit 1
}

# --- what it must return -----------------------------------------------------------------------

actual="$("$extractor" 1.0.0 "$work/CHANGELOG.md")"

[[ "$actual" == *"The first stable release."* ]] \
    || fail "1.0.0 did not return its own body"
[[ "$actual" == *"A thing that was broken."* ]] \
    || fail "1.0.0 stopped at the '### Fixed' subsection instead of the next version"
[[ "$actual" == *'## not a heading, this is a shell comment inside a fence'* ]] \
    || fail "the fenced line starting with '## ' was dropped"
[[ "$actual" != *"The release candidate."* ]] \
    || fail "1.0.0 bled into 1.0.0-rc.1"
[[ "$actual" != *"Something not released yet."* ]] \
    || fail "1.0.0 returned the [Unreleased] section"
[[ "$actual" != *"[1.0.0]"* ]] \
    || fail "the version heading itself was included; the release title already carries it"

# A prefix must not match a longer name, in either direction.
rc="$("$extractor" 1.0.0-rc.1 "$work/CHANGELOG.md")"
[[ "$rc" == *"The release candidate."* ]] || fail "1.0.0-rc.1 did not return its own body"
[[ "$rc" != *"The first stable release."* ]] || fail "1.0.0-rc.1 returned the 1.0.0 body"

# A truncated version is a version nobody wrote a section for, and must be refused rather than
# resolved to whichever heading happens to start with it. The pair above does not cover this on its
# own: those two names differ in a way that ordering alone can hide.
if "$extractor" 1.0 "$work/CHANGELOG.md" > /dev/null 2>&1; then
    fail "'1.0' matched a heading it is merely a prefix of"
fi

# The heading with no date form, and a section whose last line is the last line of the file.
[[ "$("$extractor" 0.9.0 "$work/CHANGELOG.md")" == "- An entry with no subsection." ]] \
    || fail "0.9.0 was not returned exactly, trimmed of its blank lines"
[[ "$("$extractor" 0.7.0 "$work/CHANGELOG.md")" == "- Reachable only if 0.8.0 stopped at the right place." ]] \
    || fail "the last section of the file was not returned"

first_line="$("$extractor" 1.0.0 "$work/CHANGELOG.md" | sed -n '1p')"
last_line="$("$extractor" 1.0.0 "$work/CHANGELOG.md" | sed -n '$p')"
[[ -n "$first_line" ]] || fail "the section begins with a blank line"
[[ -n "$last_line" ]] || fail "the section ends with a blank line"

# --- what it must refuse -----------------------------------------------------------------------

# An absent version. This is the one that matters: it is what a tag pushed before the changelog was
# written looks like, and the release must not proceed with empty notes.
if "$extractor" 2.0.0 "$work/CHANGELOG.md" > /dev/null 2>&1; then
    fail "a version with no section was accepted"
fi
# Captured rather than piped into grep: the extractor exits non-zero here by design, and under
# `pipefail` that status would fail the pipeline whatever grep found.
absent_message="$("$extractor" 2.0.0 "$work/CHANGELOG.md" 2>&1 || true)"
[[ "$absent_message" == *"2.0.0"* ]] \
    || fail "the refusal does not name the version it could not find"

# A heading that exists and says nothing. Green, and the release note would be empty.
if "$extractor" 0.8.0 "$work/CHANGELOG.md" > /dev/null 2>&1; then
    fail "a section with an empty body was accepted"
fi

# [Unreleased] is a heading like any other to a line-by-line reader, and must never be published.
if "$extractor" Unreleased "$work/CHANGELOG.md" > /dev/null 2>&1; then
    fail "[Unreleased] was accepted as a release"
fi

# Usage errors are distinct from a missing section, so a workflow can tell "you called it wrong"
# from "the changelog is not ready".
if "$extractor" 1.0.0 "$work/nope.md" > /dev/null 2>&1; then
    fail "a missing changelog file was accepted"
fi
set +e
"$extractor" 1.0.0 "$work/nope.md" > /dev/null 2>&1
missing_status=$?
"$extractor" "" "$work/CHANGELOG.md" > /dev/null 2>&1
empty_arg_status=$?
"$extractor" 2.0.0 "$work/CHANGELOG.md" > /dev/null 2>&1
absent_status=$?
set -e
[[ "$missing_status" -eq 2 ]] || fail "a missing changelog file exited $missing_status, expected 2"
[[ "$empty_arg_status" -eq 2 ]] || fail "an empty version argument exited $empty_arg_status, expected 2"
[[ "$absent_status" -eq 1 ]] || fail "an absent section exited $absent_status, expected 1"

echo "ok — four sections returned, six refused, and the fence was not mistaken for a heading."
