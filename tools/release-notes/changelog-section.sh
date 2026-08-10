#!/bin/sh
# Print the CHANGELOG body for one version, for `gh release create --notes-file`.
#
# The release workflow used to pass `--generate-notes`, which lists the pull-request titles merged
# since the previous tag. That is a version-control log wearing a different hat, and it is the one
# thing release notes are not allowed to be: a reader cannot tell from it whether to upgrade. The
# summary written for that purpose already exists, in CHANGELOG.md, so this reads it out.
#
# Usage:
#   changelog-section.sh <version> [changelog-path]     # path defaults to CHANGELOG.md
#
# Exit status: 0 the section was printed, 1 the changelog is not ready (no such section, or the
# section is empty), 2 usage error (no version given, file unreadable, or [Unreleased] asked for).
#
# The distinction between 1 and 2 is what lets the workflow say "write the changelog entry" rather
# than "something went wrong". Refusing an empty section is the point rather than pedantry: a
# heading with nothing under it publishes a release announcing nothing, and looks from the outside
# exactly like a release that had nothing to announce.

set -u

version="${1:-}"
changelog="${2:-CHANGELOG.md}"

if [ -z "$version" ]; then
  printf 'changelog-section: no version given\nusage: changelog-section.sh <version> [changelog-path]\n' >&2
  exit 2
fi

# Keep a Changelog keeps work in progress under [Unreleased]. Publishing it would announce a
# version's contents under a heading that means "not this version" — and since the name matches a
# real heading, nothing further down would notice.
case "$version" in
  [Uu]nreleased)
    printf 'changelog-section: [Unreleased] is not a release; pass the version being published\n' >&2
    exit 2
    ;;
  *) ;;
esac

if [ ! -r "$changelog" ]; then
  printf 'changelog-section: cannot read %s\n' "$changelog" >&2
  exit 2
fi

# Fenced blocks are tracked because a shell sample can open a line with '## ', which is a heading to
# anything reading line by line and is not one. Getting that wrong truncates a section at a comment.
section="$(
  awk -v want="$version" '
    BEGIN { fence = 0; inside = 0; found = 0; n = 0 }
    {
      if ($0 ~ /^(```|~~~)/) {
        fence = 1 - fence
        if (inside) { buffer[++n] = $0 }
        next
      }
      if (!fence && $0 ~ /^## /) {
        if (inside) { exit }
        heading = $0
        sub(/^## +/, "", heading)
        if (match(heading, /^\[[^]]*\]/)) {
          name = substr(heading, RSTART + 1, RLENGTH - 2)
          if (name == want) { inside = 1; found = 1 }
        }
        next
      }
      if (inside) { buffer[++n] = $0 }
    }
    END {
      if (!found) { exit 3 }
      first = 0
      last = 0
      for (i = 1; i <= n; i++) {
        if (buffer[i] ~ /[^[:space:]]/) {
          if (!first) { first = i }
          last = i
        }
      }
      if (!first) { exit 4 }
      for (i = first; i <= last; i++) { print buffer[i] }
    }
  ' "$changelog"
)"
status=$?

case "$status" in
  0) ;;
  3)
    printf 'changelog-section: %s has no "## [%s]" section\n' "$changelog" "$version" >&2
    printf 'Add the entry for this version before tagging it, moving what applies out of [Unreleased].\n' >&2
    exit 1
    ;;
  4)
    printf 'changelog-section: the "## [%s]" section of %s is empty\n' "$version" "$changelog" >&2
    printf 'A release whose notes say nothing is indistinguishable from one that changed nothing.\n' >&2
    exit 1
    ;;
  *)
    printf 'changelog-section: could not read %s (awk exited %s)\n' "$changelog" "$status" >&2
    exit 2
    ;;
esac

printf '%s\n' "$section"
