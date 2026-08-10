#!/usr/bin/env bash
#
# Proves the shipped .nupkg, not the source tree.
#
# Every other test in this repository reaches the library through a ProjectReference, which skips
# the whole of packaging: the framework reference, the analyzers' placement inside the package, the
# lib/ layout, and whether a consumer with their own defaults can even compile against it. This
# script starts from `dotnet pack` and ends at an HTTP response, so the chain it covers is
#
#   source -> pack -> NuGet restore -> consumer compile -> consumer analyzer -> Kestrel -> request
#
# It also covers the one call the README leads with and no test can reach:
# AddEnumMemberNameBinding() with no options scans Assembly.GetEntryAssembly(), which under a test
# host is testhost.dll. Only a real application entered through its own Main exercises it.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
WORK="$HERE/.work"

# The version is a sentinel, not the real one, and that is a safety property rather than a
# shortcut. 0.0.0-smoke is never published, so nuget.org cannot serve it — if the local feed were
# missing or misconfigured, restore fails loudly instead of quietly testing whatever the last
# release put on nuget.org.
VERSION="0.0.0-smoke"

# Freshness has three independent guarantees, because a stale pass here is worse than no test:
#   1. the work directory is deleted, so no feed or output survives a previous run;
#   2. NUGET_PACKAGES points inside it, so the machine's global cache cannot serve an older
#      extraction of this version — the classic way a package smoke test silently rots;
#   3. the sentinel version above means only our own feed can supply it.
rm -rf "$WORK"
mkdir -p "$WORK/feed"
export NUGET_PACKAGES="$WORK/packages"

FAILURES=0
APP_PID=""

cleanup() {
    if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi
}
trap cleanup EXIT

step()  { printf '\n\033[1m== %s\033[0m\n' "$1"; }
pass()  { printf '   \033[32mok\033[0m   %s\n' "$1"; }
fail()  { printf '   \033[31mFAIL\033[0m %s\n' "$1"; FAILURES=$((FAILURES + 1)); }

# Compares an actual value against an expected one. Both are printed on failure, because a smoke
# test that only says "assertion failed" costs more time than it saves.
expect() {
    local what="$1" expected="$2" actual="$3"
    if [[ "$actual" == "$expected" ]]; then
        pass "$what"
    else
        fail "$what"
        printf '        expected: %s\n        actual:   %s\n' "$expected" "$actual"
    fi
}

expect_contains() {
    local what="$1" needle="$2" haystack="$3"
    if printf '%s' "$haystack" | grep -qF -- "$needle"; then
        pass "$what"
    else
        fail "$what"
        printf '        expected to contain: %s\n' "$needle"
        printf '%s\n' "$haystack" | sed 's/^/        | /' | head -40
    fi
}

step "Pack the libraries into a local feed"
dotnet pack "$ROOT/EnumMemberNameBinding.slnx" -c Release -o "$WORK/feed" -p:Version="$VERSION" \
    > "$WORK/pack.log" 2>&1 || { cat "$WORK/pack.log"; exit 1; }
ls -1 "$WORK/feed"/*.nupkg | sed 's|.*/|   |'

step "Both packages carry their icon"
# PackageIcon is declared once in Directory.Build.props, but the file has to be included by each
# packable project, and only the second half is easy to forget when a package is added. Getting it
# wrong is not always loud: naming a file nothing packs fails the pack above with NU5046, while
# dropping the property and keeping the include packs a perfectly valid package that nuget.org
# shows behind the grey placeholder. Both halves are checked here, per package.
python3 - "$WORK/feed" <<'PY' || FAILURES=$((FAILURES + 1))
import glob, os, sys, zipfile
import xml.etree.ElementTree as ET

feed = sys.argv[1]
ok   = True

def check(label, condition, detail=""):
    global ok
    if condition:
        print(f"   \033[32mok\033[0m   {label}")
    else:
        ok = False
        print(f"   \033[31mFAIL\033[0m {label}")
        if detail:
            print(f"        {detail}")

for package in ("AspNetCore.EnumMemberNameBinding", "AspNetCore.EnumMemberNameBinding.OpenApi"):
    # The trailing [0-9] keeps the base package's glob from also matching the OpenApi one.
    found = glob.glob(os.path.join(feed, f"{package}.[0-9]*.nupkg"))
    if len(found) != 1:
        check(f"{package}: exactly one .nupkg in the feed", False, f"got {sorted(map(os.path.basename, found))}")
        continue

    with zipfile.ZipFile(found[0]) as nupkg:
        entries = nupkg.namelist()
        nuspec  = next((e for e in entries if e.lower() == f"{package}.nuspec".lower()), None)
        if nuspec is None:
            check(f"{package}: the .nupkg holds its .nuspec", False, f"got {entries}")
            continue
        # The nuspec carries a default namespace, so match on the local name rather than hardcode it.
        declared = next((e.text for e in ET.fromstring(nupkg.read(nuspec)).iter()
                         if e.tag.rpartition('}')[2] == "icon"), None)

    check(f"{package}: the .nuspec declares an icon", declared is not None)
    check(f"{package}: the declared icon is in the package",
          declared is not None and declared.replace("\\", "/") in entries,
          f"declares {declared!r}, package holds {sorted(e for e in entries if e.lower().endswith('.png'))}")

sys.exit(0 if ok else 1)
PY

step "Compile a consumer that references the package"
# No --no-restore: restore is part of what is being tested.
dotnet build "$HERE/Consumer/Consumer.csproj" -c Release -p:SmokePackageVersion="$VERSION" \
    > "$WORK/consumer-build.log" 2>&1 \
    || { fail "the consumer does not compile against the package"; cat "$WORK/consumer-build.log"; exit 1; }
pass "compiles"

# Proves the package came from the feed this run built. The sentinel version exists nowhere else,
# so its presence in the run-local package directory is the evidence.
if [[ -d "$NUGET_PACKAGES/aspnetcore.enummembernamebinding/$VERSION" ]]; then
    pass "resolved from the local feed, not a cache or nuget.org"
else
    fail "resolved from the local feed, not a cache or nuget.org"
fi

# A correct contract must draw no diagnostic at all. This is the other half of the EMN0003 check
# below: together they say the analyzer runs, and is silent when it should be.
if grep -qE '\bEMN[0-9]{4}\b' "$WORK/consumer-build.log"; then
    fail "a valid contract draws no analyzer diagnostic"
    grep -E '\bEMN[0-9]{4}\b' "$WORK/consumer-build.log" | sed 's/^/        | /'
else
    pass "a valid contract draws no analyzer diagnostic"
fi

step "The analyzer inside the package reports a broken contract"
# Asserted positively, and on the specific rule. "The build failed" alone would also be satisfied by
# a typo in the fixture, and "no diagnostic appeared" would be satisfied by the analyzer never
# loading — the exact failure this fixture exists to catch.
if dotnet build "$HERE/InvalidContract/InvalidContract.csproj" -c Release -p:SmokePackageVersion="$VERSION" \
       > "$WORK/invalid-build.log" 2>&1; then
    fail "the invalid contract compiled; the packaged analyzer did not run"
    cat "$WORK/invalid-build.log"
else
    pass "the build fails"
    expect_contains "the failure is EMN0003, from the packaged analyzer" \
                    "error EMN0003" "$(cat "$WORK/invalid-build.log")"
    expect_contains "the diagnostic links to its documentation page" \
                    "docs/for-users/rules/EMN0003.en.md" "$(cat "$WORK/invalid-build.log")"
fi

step "Start the consumer as a real application"
APP="$HERE/Consumer/bin/Release/net10.0/Consumer.dll"
[[ -f "$APP" ]] || { fail "the consumer was not produced at $APP"; exit 1; }

# Port 0 lets the OS choose, so parallel jobs on one runner cannot collide, and the chosen address
# is read back from Kestrel's own startup line rather than guessed.
dotnet "$APP" --urls 'http://127.0.0.1:0' > "$WORK/app.log" 2>&1 &
APP_PID=$!

BASE=""
for _ in $(seq 1 60); do
    if ! kill -0 "$APP_PID" 2>/dev/null; then
        fail "the application exited during start-up"
        cat "$WORK/app.log"
        exit 1
    fi
    # `|| true` is load-bearing under `set -o pipefail`: until the line appears grep exits 1, which
    # would otherwise abort the script here, silently, every single time.
    BASE="$(grep -oE 'Now listening on: http://127\.0\.0\.1:[0-9]+' "$WORK/app.log" 2>/dev/null | head -1 | sed 's/Now listening on: //' || true)"
    [[ -n "$BASE" ]] && break
    sleep 0.5
done

[[ -n "$BASE" ]] || { fail "the application never reported a listening address within 30s"; cat "$WORK/app.log"; exit 1; }
pass "listening on $BASE"

# Every request is bounded. A smoke test that can hang is a smoke test that will hang, in CI, at
# night, on somebody else's pull request.
get()    { curl -s -m 10 "$1"; }
status() { curl -s -m 10 -o /dev/null -w '%{http_code}' "$1"; }

step "The contract binds on every channel the README claims"
expect "route:  GET /products/out_of_stock"        '{"status":"OutOfStock"}' "$(get "$BASE/products/out_of_stock")"
expect "query:  GET /products?status=out_of_stock" '{"status":"OutOfStock"}' "$(get "$BASE/products?status=out_of_stock")"
expect "body:   POST /products"                    '{"status":"OutOfStock"}' \
       "$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -d '{"Status":"out_of_stock"}' "$BASE/products")"

step "The C# name is not part of the contract"
# The README prints this 400 explicitly. If the internal name still bound, every promise the
# library makes about renaming being safe would be void.
expect "query:  GET /products?status=OutOfStock -> 400" "400" "$(status "$BASE/products?status=OutOfStock")"
expect "route:  GET /products/OutOfStock -> 400"        "400" "$(status "$BASE/products/OutOfStock")"

step "An enum without a contract is left alone"
expect "query:  GET /priorities?priority=High"  '{"priority":"High"}' "$(get "$BASE/priorities?priority=High")"
expect "query:  GET /priorities?priority=low"   '{"priority":"Low"}'  "$(get "$BASE/priorities?priority=low")"

step "The OpenAPI companion describes what the server accepts"
DOC="$(get "$BASE/openapi/v1.json")"
printf '%s' "$DOC" > "$WORK/openapi.json"

python3 - "$WORK/openapi.json" <<'PY' || FAILURES=$((FAILURES + 1))
import json, sys

schemas = json.load(open(sys.argv[1]))["components"]["schemas"]
ok = True

def check(label, condition, detail=""):
    global ok
    if condition:
        print(f"   \033[32mok\033[0m   {label}")
    else:
        ok = False
        print(f"   \033[31mFAIL\033[0m {label}")
        if detail:
            print(f"        {detail}")

status = schemas.get("ProductStatus", {})
check("ProductStatus is a string", status.get("type") == "string", f"got {status.get('type')!r}")
check("ProductStatus advertises the public names",
      status.get("enum") == ["available", "out_of_stock", "discontinued"],
      f"got {status.get('enum')!r}")

# The companion must not touch an enum that declares no contract; ASP.NET Core documents it as an
# integer and it has to stay one.
priority = schemas.get("PlainPriority", {})
check("PlainPriority is left as an integer", priority.get("type") == "integer", f"got {priority.get('type')!r}")
check("PlainPriority advertises no public names", "enum" not in priority or priority.get("enum") is None,
      f"got {priority.get('enum')!r}")

sys.exit(0 if ok else 1)
PY

step "Result"
if (( FAILURES == 0 )); then
    printf '\033[32mThe published package behaves as documented.\033[0m\n'
else
    printf '\033[31m%s check(s) failed.\033[0m\n' "$FAILURES"
    exit 1
fi
