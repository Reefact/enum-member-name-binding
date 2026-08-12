#!/usr/bin/env bash
#
# Verify that the packages a release just pushed are really on the feed.
#
# WHY THIS EXISTS. `dotnet nuget push --skip-duplicate` exits 0 on any 409, and 409 is not only
# "you already sent this". On 2026-08-12 the v1.0.0-beta.1 release pushed both packages, was
# answered 409 Conflict on each, printed "already exists at feed", exited 0 — and nuget.org has
# never held either package. The workflow went green, a GitHub release was created, and nothing was
# published. Every step said what it was supposed to say; the only thing nobody asked was whether
# the feed had the packages.
#
# The flag itself is right and stays: a rerun after a partial failure must be able to send the
# package that did not go, and without it the first duplicate stops the command before reaching it.
# What was missing is the question after it.
#
# Usage:
#   verify-published.sh <version> <package-id> [package-id ...]
#
# Environment:
#   NUGET_FLATCONTAINER   base URL, default https://api.nuget.org/v3-flatcontainer
#                         (a file:// URL works, which is how the test runs offline)
#   VERIFY_TIMEOUT        seconds to keep asking, default 900
#   VERIFY_INTERVAL       seconds between attempts, default 15
#
# Exit status: 0 every package is retrievable, 1 at least one is not, 2 usage error.

set -euo pipefail

BASE="${NUGET_FLATCONTAINER:-https://api.nuget.org/v3-flatcontainer}"
TIMEOUT="${VERIFY_TIMEOUT:-900}"
INTERVAL="${VERIFY_INTERVAL:-15}"

version="${1:-}"
shift || true

if [[ -z "$version" || "$#" -eq 0 ]]; then
    printf 'verify-published: usage: verify-published.sh <version> <package-id> [package-id ...]\n' >&2
    exit 2
fi

# The flat container indexes by lowercase id and version, and serves the package itself at a path
# built from both. Asking for the .nupkg rather than for index.json is deliberate: the index lists
# the versions a package has, so a stale one answers 200 while saying nothing about this version.
url_for() {
    local id="$1" ver="$2"

    id="$(printf '%s' "$id" | tr '[:upper:]' '[:lower:]')"
    ver="$(printf '%s' "$ver" | tr '[:upper:]' '[:lower:]')"

    printf '%s/%s/%s/%s.%s.nupkg' "$BASE" "$id" "$ver" "$id" "$ver"
}

# curl's own exit status rather than %{http_code}, so http and file:// answer the same way: -f
# turns a 4xx into a failure, and file:// has no status line to read at all — it reports a missing
# file through the exit status and would leave %{http_code} at 000 for a file that is there.
is_published() {
    local id="$1" ver="$2"

    curl -fsS -L --max-time 30 -o /dev/null "$(url_for "$id" "$ver")" >/dev/null 2>&1
}

deadline=$(( $(date +%s) + TIMEOUT ))
missing=""

# Indexing is not instant, so this waits rather than asking once — but it waits for an answer, and
# reports the packages still missing when the deadline passes rather than assuming they arrived.
while : ; do
    missing=""
    for id in "$@"; do
        if ! is_published "$id" "$version"; then
            missing="$missing $id"
        fi
    done

    [[ -n "$missing" ]] || break
    [[ "$(date +%s)" -lt "$deadline" ]] || break

    printf 'verify-published: still waiting for%s at %s — retrying in %ss\n' "$missing" "$version" "$INTERVAL"
    sleep "$INTERVAL"
done

if [[ -n "$missing" ]]; then
    printf '\nverify-published: not on the feed after %ss:%s\n' "$TIMEOUT" "$missing" >&2
    printf 'The push reported success. The feed does not have the package, so it was refused and\n' >&2
    printf 'the refusal was swallowed — re-run the push without --skip-duplicate to read why.\n' >&2
    exit 1
fi

printf 'verify-published: %s is on the feed for%s\n' "$version" "$(printf ' %s' "$@")"
