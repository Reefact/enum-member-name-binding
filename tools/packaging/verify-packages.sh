#!/usr/bin/env bash
#
# Verify what is inside the packages, before anyone can install them.
#
# The single source of truth shared by the CI build (.github/workflows/ci.yml) and the release
# (.github/workflows/release.yml), so the two can never drift apart — the same arrangement the
# commit-message check uses, and for the same reason: a check that exists twice is a check that is
# eventually only true once.
#
# What this is FOR. Every test in this repository reaches the libraries through a ProjectReference,
# which never opens a .nupkg. The previous generation of this library shipped for years without its
# ASP.NET Core framework reference because the package was assembled by a hand-written .nuspec, and
# nothing looked inside. The package smoke test covers the same ground far more deeply, by compiling
# a consumer and driving it over HTTP; this runs first because it is seconds rather than minutes,
# and because it names the missing piece instead of reporting a build that failed somewhere.
#
# Usage:
#   verify-packages.sh [directory]        # defaults to ./artifacts

set -euo pipefail

ARTIFACTS="${1:-artifacts}"

MAIN_ID="Reefact.AspNetCore.EnumMemberNameBinding"
COMPANION_ID="Reefact.AspNetCore.EnumMemberNameBinding.OpenApi"

# The floor that keeps consumers off GHSA-v5pm-xwqc-g5wc: Microsoft.AspNetCore.OpenApi 10.0.x
# resolves Microsoft.OpenApi 2.0.0, which carries it. Raising the floor is only half the job — the
# half that is checked here is that the raise actually reaches the published .nuspec, since a
# dependency bump, a group update or a lost PackageReference would silently hand the advisory back.
OPENAPI_FLOOR="2.11.0"

failures=0

fail() {
    local message="$1"

    # stderr, not stdout: this is a failure, and the caller may be reading the pass lines.
    echo "::error::$message" >&2
    failures=1
}

pass() {
    local message="$1"

    printf '   ok   %s\n' "$message"
}

# A package id, not a prefix: the two ids share one, so a plain glob on the main id matches the
# companion too and would verify the wrong file.
#
# It reports nothing and only prints the path, deliberately. Its caller reads it through a command
# substitution, which is a subshell — an earlier version failed in here, and both the message and
# the failure flag died with the subshell, so a missing package printed nothing and exited 0. A
# verification that cannot fail is worse than none: this one was caught by deleting a package and
# watching the script pass.
package_at() {
    local id="$1"

    find "$ARTIFACTS" -maxdepth 1 -name "$id.[0-9]*.nupkg" -not -name '*.symbols.nupkg' -print -quit
}

nuspec_of() {
    local package="$1"

    unzip -p "$package" "*.nuspec"
}

echo "== $MAIN_ID"
main=$(package_at "$MAIN_ID")
if [[ -z "$main" ]]; then
    fail "no .nupkg for $MAIN_ID in $ARTIFACTS"
else
    if nuspec_of "$main" | grep -q 'frameworkReference name="Microsoft.AspNetCore.App"'; then
        pass "declares the Microsoft.AspNetCore.App framework reference"
    else
        fail "$MAIN_ID does not declare Microsoft.AspNetCore.App"
    fi

    if unzip -l "$main" | grep -q "analyzers/dotnet/cs/$MAIN_ID.Analyzers.dll"; then
        pass "ships the analyzers"
    else
        fail "$MAIN_ID does not ship the analyzers"
    fi
fi

echo "== $COMPANION_ID"
companion=$(package_at "$COMPANION_ID")
if [[ -z "$companion" ]]; then
    fail "no .nupkg for $COMPANION_ID in $ARTIFACTS"
else
    nuspec=$(nuspec_of "$companion")

    # Without this, a consumer who takes the companion alone gets the schema transformer and none of
    # the contract it reads.
    if grep -q "<dependency id=\"$MAIN_ID\"" <<<"$nuspec"; then
        pass "depends on $MAIN_ID"
    else
        fail "$COMPANION_ID does not depend on $MAIN_ID"
    fi

    # NuGet does not flow build assets transitively, so the companion carries its own .targets to
    # restore a property the consumer would otherwise lose. Packed under the wrong path, or not
    # packed at all, it is a file nobody imports and no build ever complains about.
    if unzip -l "$companion" | grep -q "build/$COMPANION_ID.targets"; then
        pass "ships its build/*.targets"
    else
        fail "$COMPANION_ID does not ship build/$COMPANION_ID.targets"
    fi

    declared=$(grep -oE '<dependency id="Microsoft.OpenApi" version="[^"]+"' <<<"$nuspec" | grep -oE '[0-9][^"]*' || true)
    if [[ -z "$declared" ]]; then
        fail "$COMPANION_ID declares no Microsoft.OpenApi dependency, so its floor cannot be read"
    elif [[ "$(printf '%s\n%s\n' "$OPENAPI_FLOOR" "$declared" | sort -V | head -1)" == "$OPENAPI_FLOOR" ]]; then
        pass "declares Microsoft.OpenApi $declared, at or above the $OPENAPI_FLOOR floor"
    else
        fail "$COMPANION_ID declares Microsoft.OpenApi $declared, below the $OPENAPI_FLOOR floor that avoids GHSA-v5pm-xwqc-g5wc"
    fi
fi

echo
echo "Packages verified:"
find "$ARTIFACTS" -maxdepth 1 -name '*.nupkg' -printf '   %f\n' | sort

exit "$failures"
