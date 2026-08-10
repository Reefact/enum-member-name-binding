#!/usr/bin/env bash
#
# The checker guards the codebase, so something has to guard the checker. Not ceremony: it has
# shipped two bugs already, one in each direction. It tested for `else\b`, and `\b` is a backspace
# in awk rather than a word boundary, so every `if`/`else` in the repository was reported as
# collapsible. Then it skipped any body containing a brace — meant for nested blocks, and in fact
# every interpolated string, so five guards were never reported at all.
#
# Neither shows in a green build. The shapes a checker must stay silent about, and the ones it must
# not miss, are both invisible in its output, so they are named here instead.
#
# Usage: tools/style/lint-single-line-exits.test.sh

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
checker="$here/lint-single-line-exits.sh"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# One method per case, and only two of them are violations. Every other one is a shape the checker
# has to leave alone, where a false positive would rewrite correct code.
cat > "$work/Fixture.cs" <<'FIXTURE'
class Fixture {
    int Collapsible(int x) {
        if (x > 0) {
            return 1;
        }

        return 0;
    }

    int SeparatedByABlankLine(int x) {
        if (x > 0) { return 1; }

        if (x < 0) { return -1; }

        return 0;
    }

    int GuardThenWork(int x) {
        if (x > 0) { return 1; }

        int doubled = x * 2;

        return doubled;
    }

    int CarriesAnElse(int x) {
        if (x > 0) {
            return 1;
        }
        else {
            return 2;
        }
    }

    int CarriesAnElseIf(int x) {
        if (x > 0) {
            return 1;
        }
        else if (x < 0) {
            return 2;
        }

        return 0;
    }

    int TwoStatements(int x) {
        if (x > 0) {
            Log();
            return 1;
        }

        return 0;
    }

    int BodyIsNotAnExit(int x) {
        if (x > 0) {
            x++;
        }

        return x;
    }

    string ConditionOverTwoLines(int x) {
        if (x > 0
            && x < 10) {
            return "y";
        }

        return "n";
    }

    string TooWideCollapsed(int x) {
        if (x > 0 && x < 10 && x != 5 && x != 7 && x != 9 && x != 11 && x != 13 && x != 15 && x != 17 && x != 19) {
            return "a value that pushes the collapsed line past the ceiling";
        }

        return "n";
    }

    string InsideARawString() {
        return """
            if (x > 0) {
                return 1;
            }
            """;
    }

    void Log() { }
}
FIXTURE

# An interpolated string in the body, which the second bug made invisible. Kept in its own file so
# the heredoc above can stay unexpanded.
cat > "$work/Interpolated.cs" <<FIXTURE
class Interpolated {
    void Check(string name) {
        if (name.Length == 0) {
            throw new System.ArgumentException(\$"'{name}' is empty.", nameof(name));
        }
    }
}
FIXTURE

expected="Fixture.cs:3:collapse Fixture.cs:12:unblank Interpolated.cs:3:collapse"

summarise() {
    "$checker" "$work/Fixture.cs" "$work/Interpolated.cs" 2>&1 || true
}

# Not sorted: the checker walks the files it was given in order and each one top to bottom, so the
# sequence is part of what is being asserted.
actual="$(summarise \
    | sed -n -e 's|^.*/\([A-Za-z]*\.cs\):\([0-9]*\): an if whose.*$|\1:\2:collapse|p' \
             -e 's|^.*/\([A-Za-z]*\.cs\):\([0-9]*\): blank line.*$|\1:\2:unblank|p' \
    | tr '\n' ' ')"
actual="${actual% }"

if [ "$actual" != "$expected" ]; then
    echo "FAIL: reported sites do not match."
    echo "  expected: $expected"
    echo "  actual:   ${actual:-nothing}"
    summarise
    exit 1
fi

# The fixer has to produce what the checker printed, and leave every other case untouched.
before="$(wc -l < "$work/Fixture.cs")"
"$checker" --fix "$work/Fixture.cs" "$work/Interpolated.cs" > /dev/null
after="$(wc -l < "$work/Fixture.cs")"

if [ "$((before - after))" -ne 3 ]; then
    echo "FAIL: --fix removed $((before - after)) lines from Fixture.cs, expected 3"
    exit 1
fi

if ! grep -q '^        if (x > 0) { return 1; }$' "$work/Fixture.cs"; then
    echo "FAIL: --fix did not write the collapsed line"
    exit 1
fi

if ! grep -q "ArgumentException(\$\"'{name}' is empty.\", nameof(name)); }$" "$work/Interpolated.cs"; then
    echo "FAIL: --fix did not collapse the guard whose body holds an interpolated string"
    exit 1
fi

if ! "$checker" "$work/Fixture.cs" "$work/Interpolated.cs" > /dev/null; then
    echo "FAIL: the checker still reports something after --fix"
    exit 1
fi

echo "ok — three sites reported, rewritten, and clean afterwards."
