#!/usr/bin/env bash
#
# The checker that guards the release has to be guarded itself, and this one more than most: it runs
# once, at the moment nothing can be taken back, and the failure it exists to catch is silent. A
# version of it that always answered "published" would have let 1.0.0-beta.1 through exactly as
# --skip-duplicate did.
#
# Runs offline. NUGET_FLATCONTAINER points at a file:// tree laid out the way the flat container
# lays out its own, so "the package is there" and "the package is not there" are both real answers
# from the code under test rather than from a stub of it.
#
# Usage: tools/packaging/verify-published.test.sh

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
checker="$here/verify-published.sh"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

feed="$work/feed"
id_main="reefact.aspnetcore.enummembernamebinding"
id_companion="reefact.aspnetcore.enummembernamebinding.openapi"
version="1.0.0-beta.2"

publish() {
    local id="$1"

    mkdir -p "$feed/$id/$version"
    printf 'not really a package\n' > "$feed/$id/$version/$id.$version.nupkg"
}

export NUGET_FLATCONTAINER="file://$feed"
export VERIFY_TIMEOUT=1
export VERIFY_INTERVAL=1

failed=0
# `expect` in tests/PackageSmokeTest/run.sh takes what/expected/actual and this takes
# what/actual/expected, which the names are here to make visible: the call reads
# `check "…" "$status" 1`, so the value under test comes before the one it must equal.
check() {
    local what="$1" actual="$2" expected="$3"

    if [[ "$actual" == "$expected" ]]; then return; fi

    printf 'FAIL: %s — expected exit %s, got %s\n' "$what" "$expected" "$actual"
    failed=1
}

# Nothing published yet: both missing.
set +e
"$checker" "$version" Reefact.AspNetCore.EnumMemberNameBinding Reefact.AspNetCore.EnumMemberNameBinding.OpenApi >/dev/null 2>&1
status=$?
set -e
check "neither package on the feed" "$status" 1

# The half-published case, which is the one a rerun exists for and the one an "any package answered"
# check would call a success.
publish "$id_main"
set +e
"$checker" "$version" Reefact.AspNetCore.EnumMemberNameBinding Reefact.AspNetCore.EnumMemberNameBinding.OpenApi >/dev/null 2>&1
status=$?
set -e
check "only the main package on the feed" "$status" 1

# Both there.
publish "$id_companion"
set +e
"$checker" "$version" Reefact.AspNetCore.EnumMemberNameBinding Reefact.AspNetCore.EnumMemberNameBinding.OpenApi >/dev/null 2>&1
status=$?
set -e
check "both packages on the feed" "$status" 0

# A different version of an id that is otherwise present — the case an index.json check would pass,
# since the index answers for the package rather than for the version asked about.
set +e
"$checker" "9.9.9-nope" Reefact.AspNetCore.EnumMemberNameBinding >/dev/null 2>&1
status=$?
set -e
check "a version that was never pushed" "$status" 1

# Usage.
set +e
"$checker" >/dev/null 2>&1
status=$?
"$checker" "$version" >/dev/null 2>&1
status_no_ids=$?
set -e
check "no arguments" "$status" 2
check "a version but no package id" "$status_no_ids" 2

if [[ "$failed" -ne 0 ]]; then exit 1; fi

echo "ok — missing, half-missing, present, wrong-version and usage all answered as they must."
