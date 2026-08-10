#!/usr/bin/env bash
#
# An `if` whose whole body is one exit — return, throw, continue, break — is written on one line,
# and a run of them is written with nothing between:
#
#     if (string.IsNullOrEmpty(name)) { return Problem.EmptyName(memberName); }
#     if (isFlags && name.Contains(',')) { return Problem.CommaInFlagsName(memberName, name); }
#
# The guard and what it does are one thought, and three lines make the reader assemble it. Blank
# lines between consecutive guards give back the height the one-line form just saved, which was the
# point of it. Anything less trivial than a bare exit keeps the multi-line form, which is why this
# only ever looks at those four statements: it has no way to judge the rest, so it does not try.
#
# Usage:
#   tools/style/lint-single-line-exits.sh [--fix] [path ...]
#
# With no path, every tracked C# file outside obj/ and bin/. Exits 1 when something is reported,
# which is what makes it usable from CI. `--fix` rewrites the files instead of reporting.

set -euo pipefail

# A guard that would need more than this stays on three lines: past some width the one-line form
# stops being easier to read, which is the only reason for the rule. 140 rather than a rounder
# number because it sits just above the 99th percentile line in this repository, so the ceiling
# follows what the code already does instead of imposing a figure on it.
#
# It is a ceiling, not an exemption. A guard that does not fit is a guard whose line is too long,
# and the answer is usually to name what the condition says — which is how the three widest sites
# in the analyzer came to fit.
readonly MAX_WIDTH=140

fix=0
paths=()

for argument in "$@"; do
    case "$argument" in
        --fix) fix=1 ;;
        *)     paths+=("$argument") ;;
    esac
done

if [ "${#paths[@]}" -eq 0 ]; then
    while IFS= read -r file; do paths+=("$file"); done < <(git ls-files '*.cs' | grep -Ev '/(obj|bin)/')
fi

# Prints one record per site: line number, what to do, and the replacement where there is one.
#
# collapse — three consecutive lines, `if (...) {`, one exit, `}`, with the brace on the same line
# as the condition, so a condition split over several lines is never touched. The `}` must be alone
# on its line, which is what rules out an `if` that carries an `else`.
#
# unblank — a blank line with a single-line exit on either side of it.
scan() {
    awk -v max="$MAX_WIDTH" '
        function isExit(line) {
            return line ~ /^[ \t]*if \(.*\) \{ *(return|throw|continue|break)([ \t(].*)?; *\}[ \t]*$/
        }

        # A raw string literal can hold C# that is not this file s own code — the analyzer fixtures
        # do exactly that — so anything between """ fences is invisible here.
        {
            lines[NR] = $0
            fences = gsub(/"""/, "\"\"\"")
            raw[NR] = inRaw
            if (fences % 2 == 1) { inRaw = !inRaw }
        }

        END {
            for (i = 1; i <= NR; i++) {
                if (raw[i]) { continue }

                if (i + 2 <= NR && isExit(lines[i]) && lines[i + 1] ~ /^[ \t]*$/ && isExit(lines[i + 2])) {
                    print (i + 1) "\tunblank\t"
                    continue
                }

                if (i + 2 > NR) { continue }

                if (lines[i] !~ /^[ \t]*if \(.*\) \{[ \t]*$/) { continue }
                if (lines[i + 1] !~ /^[ \t]*(return|throw|continue|break)([ \t(].*)?;[ \t]*$/) { continue }

                # A lambda with a block body is a statement that happens to end in a semicolon, and
                # it is not the trivial exit this is about. Braces alone do not disqualify a body:
                # an interpolated string carries them, and $"'"'"'{type.FullName}'"'"' is not an enum." was
                # the reason five guards went unreported the first time.
                if (lines[i + 1] ~ /=>/) { continue }
                if (lines[i + 2] !~ /^[ \t]*\}[ \t]*$/) { continue }
                # Not \b: in awk that is a backspace, not a word boundary, so the guard it was
                # written as never fired and every if/else was reported.
                if (i + 3 <= NR && lines[i + 3] ~ /^[ \t]*else([^A-Za-z0-9_].*)?$/) { continue }

                opening = lines[i]
                sub(/[ \t]*$/, "", opening)

                body = lines[i + 1]
                sub(/^[ \t]*/, "", body)
                sub(/[ \t]*$/, "", body)

                collapsed = opening " " body " }"
                if (length(collapsed) > max) { continue }

                print i "\tcollapse\t" collapsed
            }
        }
    ' "$1"
}

report_of() {
    case "$2" in
        collapse) printf '%s:%s: an if whose body is a lone exit belongs on one line\n    %s\n' "$1" "$3" "$4" ;;
        unblank)  printf '%s:%s: blank line between two one-line guards; a run of them is one block\n' "$1" "$3" ;;
    esac
}

# One site at a time, back to front, so an earlier rewrite cannot move the lines a later one names.
apply() {
    local file="$1" line="$2" action="$3" collapsed="$4"

    case "$action" in
        collapse)
            COLLAPSED="$collapsed" awk -v target="$line" '
                NR == target     { print ENVIRON["COLLAPSED"]; next }
                NR == target + 1 { next }
                NR == target + 2 { next }
                { print }
            ' "$file" > "$file.tmp"
            ;;
        unblank)
            awk -v target="$line" 'NR != target' "$file" > "$file.tmp"
            ;;
    esac

    mv "$file.tmp" "$file"
}

reported=0

for file in "${paths[@]}"; do
    [ -f "$file" ] || continue

    if [ "$fix" -eq 0 ]; then
        sites="$(scan "$file")"
        [ -n "$sites" ] || continue

        while IFS=$'\t' read -r line action collapsed; do
            report_of "$file" "$action" "$line" "$collapsed"
            reported=$((reported + 1))
        done <<< "$sites"

        continue
    fi

    # Collapsing three lines into one can leave two guards with a blank line between them, which is
    # the other rule — so this runs again until the file stops changing rather than assuming one
    # pass is enough. The cap is a backstop against a rule that would undo another, not a budget.
    fixed=0
    for _ in 1 2 3 4 5; do
        sites="$(scan "$file")"
        [ -n "$sites" ] || break

        while IFS=$'\t' read -r line action collapsed; do
            apply "$file" "$line" "$action" "$collapsed"
            fixed=$((fixed + 1))
        done <<< "$(printf '%s\n' "$sites" | sort -t$'\t' -k1,1nr)"
    done

    if [ "$fixed" -gt 0 ]; then
        reported=$((reported + fixed))
        printf 'fixed %s\n' "$file"
    fi
done

if [ "$reported" -eq 0 ]; then
    echo "Nothing to report."
    exit 0
fi

if [ "$fix" -eq 1 ]; then
    printf '\nRewrote %d site(s).\n' "$reported"
    exit 0
fi

printf '\n%d site(s). Run tools/style/lint-single-line-exits.sh --fix to rewrite them.\n' "$reported"
exit 1
