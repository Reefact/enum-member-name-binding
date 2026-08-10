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
# Usage: tools/style/lint-layout.test.sh

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
checker="$here/lint-layout.sh"

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

    // Rule 3: the value fits beside its name, so the break after the = is gratuitous.
    private const string Split =
        "a value short enough to sit on the line its name is on";

    // Left alone: joined this would pass the ceiling, and it has a seam of its own to break at.
    private static readonly string TooWide =
        Describe("a value long enough that joining it would run past the ceiling this rule keeps").Trim().ToUpperInvariant();

    // Left alone: the value itself needs more than one line, which is what the break is for.
    private const string Continued =
        "a first half that carries its own line "
      + "and a second half under it;";

    // Rule 4: wrapped at the comma, which is what makes two of them a diff done by eye.
    [SuppressMessage("Category", "RULE0001",
        Justification = "Wrapped over two lines.")]
    void WrappedSuppression() { }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "Wrapped over three lines.")]
    void WrappedFurther() { }

    // Qualified, and with the suffix C# lets an attribute drop. Matching only the bare short name
    // is how this rule first walked past a wrapped suppression and reported a clean tree.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Category", "RULE0002",
        Justification = "Written qualified.")]
    void Qualified() { }

    [SuppressMessageAttribute("Category", "RULE0003",
        Justification = "Written with the suffix.")]
    void WithTheSuffix() { }

    // Left alone: already on one line.
    [SuppressMessage("Category", "RULE0004", Justification = "Already one line.")]
    void OneLine() { }

    // Left alone: wrapped, and not a suppression.
    [Obsolete(
        "Wrapped, and not a suppression.")]
    void NotASuppression() { }

    // Left alone: shares its brackets, the one shape the checker admits it cannot read.
    [Fact, SuppressMessage("Category", "RULE0005",
        Justification = "Sharing its brackets with a second attribute.")]
    void SharesItsBrackets() { }

    // Left alone: a fixture's code, not this file's own.
    string ASuppressionInsideARawString() {
        return """
            [SuppressMessage("Category", "RULE0006",
                Justification = "Inside a raw string.")]
            """;
    }
}
FIXTURE

# An assembly-level suppression opens its line with a target rather than with the attribute name.
cat > "$work/Assembly.cs" <<'FIXTURE'
[assembly: SuppressMessage("Category", "RULE0007",
    Justification = "Assembly level, and still one line.")]
FIXTURE

# Nothing compiles to this, but the branch that decides between "rewrite it" and "say so and stop"
# is the branch that would otherwise eat the rest of a file — and, counted wrong, would have --fix
# report five rewrites of a file it never touched.
cat > "$work/Unterminated.cs" <<'FIXTURE'
class Unterminated {
    [SuppressMessage("Category", "RULE0008",
    void Broken() { }
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

expected="Fixture.cs:3:collapse Fixture.cs:12:unblank Fixture.cs:91:joinvalue \
Fixture.cs:104:suppression Fixture.cs:108:suppression Fixture.cs:115:suppression \
Fixture.cs:119:suppression Interpolated.cs:3:collapse Assembly.cs:1:suppression \
Unterminated.cs:2:unclosed"

summarise() {
    "$checker" "$work/Fixture.cs" "$work/Interpolated.cs" "$work/Assembly.cs" "$work/Unterminated.cs" 2>&1 || true
}

# Not sorted: the checker walks the files it was given in order and each one top to bottom, so the
# sequence is part of what is being asserted.
actual="$(summarise \
    | sed -n -e 's|^.*/\([A-Za-z]*\.cs\):\([0-9]*\): an if whose.*$|\1:\2:collapse|p' \
             -e 's|^.*/\([A-Za-z]*\.cs\):\([0-9]*\): blank line.*$|\1:\2:unblank|p' \
             -e 's|^.*/\([A-Za-z]*\.cs\):\([0-9]*\): this value fits.*$|\1:\2:joinvalue|p' \
             -e 's|^.*/\([A-Za-z]*\.cs\):\([0-9]*\): a suppression belongs on one line, so.*$|\1:\2:suppression|p' \
             -e 's|^.*/\([A-Za-z]*\.cs\):\([0-9]*\): a suppression belongs on one line; this one never closes$|\1:\2:unclosed|p' \
    | tr '\n' ' ')"
actual="${actual% }"

if [[ "$actual" != "$expected" ]]; then
    echo "FAIL: reported sites do not match."
    echo "  expected: $expected"
    echo "  actual:   ${actual:-nothing}"
    summarise
    exit 1
fi

# The fixer has to produce what the checker printed, and leave every other case untouched.
before="$(wc -l < "$work/Fixture.cs")"
unterminated_before="$(wc -l < "$work/Unterminated.cs")"
"$checker" --fix "$work/Fixture.cs" "$work/Interpolated.cs" "$work/Assembly.cs" "$work/Unterminated.cs" > /dev/null
after="$(wc -l < "$work/Fixture.cs")"

if [[ "$((before - after))" -ne 9 ]]; then
    echo "FAIL: --fix removed $((before - after)) lines from Fixture.cs, expected 9"
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

if ! grep -q '^    private const string Split = "a value short enough' "$work/Fixture.cs"; then
    echo "FAIL: --fix did not put the value back beside its name"
    exit 1
fi

if ! grep -q '^    \[SuppressMessage("Category", "RULE0001", Justification = "Wrapped over two lines.")\]$' "$work/Fixture.cs"; then
    echo "FAIL: --fix did not write the two-line suppression as one"
    exit 1
fi

if ! grep -q '^    \[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Wrapped over three lines.")\]$' "$work/Fixture.cs"; then
    echo "FAIL: --fix did not write the three-line suppression as one"
    exit 1
fi

if ! grep -q '^    \[System.Diagnostics.CodeAnalysis.SuppressMessage("Category", "RULE0002", Justification = "Written qualified.")\]$' "$work/Fixture.cs"; then
    echo "FAIL: --fix did not write the qualified suppression as one line"
    exit 1
fi

if ! grep -q '^    \[SuppressMessageAttribute("Category", "RULE0003", Justification = "Written with the suffix.")\]$' "$work/Fixture.cs"; then
    echo "FAIL: --fix did not write the suffixed suppression as one line"
    exit 1
fi

if ! grep -q '^    \[Obsolete($' "$work/Fixture.cs"; then
    echo "FAIL: --fix rewrote an attribute that is not a suppression"
    exit 1
fi

if ! grep -q '^    \[Fact, SuppressMessage("Category", "RULE0005",$' "$work/Fixture.cs"; then
    echo "FAIL: --fix rewrote a suppression sharing its brackets, which the checker does not read"
    exit 1
fi

if ! grep -q '^            \[SuppressMessage("Category", "RULE0006",$' "$work/Fixture.cs"; then
    echo "FAIL: --fix rewrote the suppression inside the raw string"
    exit 1
fi

if ! grep -q '^\[assembly: SuppressMessage("Category", "RULE0007", Justification = "Assembly level, and still one line.")\]$' "$work/Assembly.cs"; then
    echo "FAIL: --fix did not write the assembly-level suppression as one line"
    exit 1
fi

if [[ "$(wc -l < "$work/Unterminated.cs")" -ne "$unterminated_before" ]]; then
    echo "FAIL: --fix edited a file whose suppression never closes"
    exit 1
fi

if ! "$checker" "$work/Fixture.cs" "$work/Interpolated.cs" "$work/Assembly.cs" > /dev/null; then
    echo "FAIL: the checker still reports something after --fix"
    exit 1
fi

echo "ok — nine sites reported, one refused, rewritten, and clean afterwards."
