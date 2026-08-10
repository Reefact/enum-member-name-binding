#!/usr/bin/env bash
#
# Four rules about where a statement is allowed to break, all of them the same complaint: a line
# that is not a thought, leaving the reader to assemble one.
#
#   1. An `if` whose whole body is one exit — return, throw, continue, break — is written on one
#      line. The guard and what it does are one thought.
#   2. A run of those takes no blank line between them, which would give back the height the
#      one-line form had just saved.
#   3. A declaration does not break after the `=` when its value fits beside the name, because the
#      two halves are then a name with no value and a value with no name.
#   4. A suppression attribute is written on one line, at any width. A member usually carries more
#      than one, and wrapped at the comma they interleave into a paragraph where two of them differ
#      by one token somewhere in the middle — so comparing them becomes a diff done by eye, which is
#      how a duplicate survives being looked at.
#
#     if (string.IsNullOrEmpty(name)) { return Problem.EmptyName(memberName); }
#     if (isFlags && name.Contains(',')) { return Problem.CommaInFlagsName(memberName, name); }
#
#     internal const string Reflection = "…is not compatible with trimming.";
#
#     [UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = …)]
#
# Anything less trivial than a bare exit keeps the multi-line form, and so does a value that needs
# more than one line: this only ever looks at the shapes it can decide, and does not try the rest.
#
# Rule 4 is the one with no ceiling, and that is deliberate rather than forgotten. The other three
# stop where the single line stops being the easier read, because they are logic. A suppression is
# not read for its logic — it is scanned for which rule, and skimmed for why — so wrapping it saves
# a reader nothing and costs them the comparison.
#
# Usage:
#   tools/style/lint-layout.sh [--fix] [path ...]
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

# The same idea for a declaration, at a different number, because a declaration carries one `=` and
# the eye goes straight to it while a guard carries a condition and an action. Calibrated on this
# repository rather than derived from anything: the widest value that is a single literal or a
# single call measures 154, and the one site above this has a real seam to break at instead — a
# fluent call, where the break belongs between the calls rather than between name and value.
readonly MAX_VALUE_WIDTH=160

fix=0
paths=()

for argument in "$@"; do
    case "$argument" in
        --fix) fix=1 ;;
        *)     paths+=("$argument") ;;
    esac
done

if [[ "${#paths[@]}" -eq 0 ]]; then
    while IFS= read -r file; do paths+=("$file"); done < <(git ls-files '*.cs' | grep -Ev '/(obj|bin)/')
fi

# Prints one record per site: line number, what to do, and the replacement where there is one.
#
# collapse — three consecutive lines, `if (...) {`, one exit, `}`, with the brace on the same line
# as the condition, so a condition split over several lines is never touched. The `}` must be alone
# on its line, which is what rules out an `if` that carries an `else`.
#
# unblank — a blank line with a single-line exit on either side of it.
#
# joinvalue — a line ending in `=` whose next line completes the statement, where the two joined
# still fit. A value spread over several lines is never touched, since the second line would not
# end the statement.
#
# suppression — a line opening a suppression attribute without closing it, running to the first line
# that ends in `)]`. This record carries a fourth field, the last line of the site, because unlike
# the other three the span is not fixed. `unclosed` is the same site with no closing bracket found:
# reported and left alone, since a file that unbalanced is not one a line-joiner should be editing.
scan() {
    local file="$1"

    awk -v max="$MAX_WIDTH" -v valueMax="$MAX_VALUE_WIDTH" '
        function isExit(line) {
            return line ~ /^[ \t]*if \(.*\) \{ *(return|throw|continue|break)([ \t(].*)?; *\}[ \t]*$/
        }

        # The name can arrive qualified — `[System.Diagnostics.CodeAnalysis.SuppressMessage(` — and
        # with the suffix C# lets an attribute drop. Matching only the bare short name is how this
        # rule first walked past a wrapped suppression and reported a clean tree.
        function opensSuppression(line) {
            return line ~ /^[ \t]*\[[ \t]*((assembly|module)[ \t]*:[ \t]*)?(global[ \t]*::[ \t]*)?([A-Za-z_][A-Za-z0-9_]*[ \t]*\.[ \t]*)*(Unconditional)?SuppressMessage(Attribute)?[ \t]*\(/
        }

        function closesAttribute(line) {
            return line ~ /\)\][ \t]*$/
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

                # First, and it swallows its own lines, so nothing below looks inside a suppression.
                if (opensSuppression(lines[i]) && !closesAttribute(lines[i])) {
                    collapsed = lines[i]
                    sub(/[ \t]*$/, "", collapsed)

                    closed = 0
                    for (j = i + 1; j <= NR; j++) {
                        continuation = lines[j]
                        sub(/^[ \t]*/, "", continuation)
                        sub(/[ \t]*$/, "", continuation)

                        collapsed = collapsed " " continuation
                        if (closesAttribute(lines[j])) { closed = 1; break }
                    }

                    if (!closed) { print i "\tunclosed\t"; continue }

                    print i "\tsuppression\t" collapsed "\t" j
                    i = j
                    continue
                }

                if (i + 2 <= NR && isExit(lines[i]) && lines[i + 1] ~ /^[ \t]*$/ && isExit(lines[i + 2])) {
                    print (i + 1) "\tunblank\t"
                    continue
                }

                if (i + 1 <= NR && lines[i] ~ /[^=!<>+*\/%&|^-] =[ \t]*$/ && lines[i] !~ /^[ \t]*\/\// \
                                && lines[i + 1] ~ /;[ \t]*$/ && lines[i + 1] !~ /^[ \t]*$/) {
                    head = lines[i]
                    sub(/[ \t]*$/, "", head)

                    value = lines[i + 1]
                    sub(/^[ \t]*/, "", value)
                    sub(/[ \t]*$/, "", value)

                    joined = head " " value
                    if (length(joined) <= valueMax) {
                        print i "\tjoinvalue\t" joined
                        continue
                    }
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
    ' "$file"
}

# The same four values in the same order as apply() below. Named rather than read as $1..$4
# because the two are called one after the other in the same loop, and two sibling helpers taking
# file, line and action in two different orders is a bug waiting for whoever edits one of them.
report_of() {
    local file="$1" line="$2" action="$3" collapsed="$4"

    case "$action" in
        collapse)    printf '%s:%s: an if whose body is a lone exit belongs on one line\n    %s\n' "$file" "$line" "$collapsed" ;;
        unblank)     printf '%s:%s: blank line between two one-line guards; a run of them is one block\n' "$file" "$line" ;;
        joinvalue)   printf '%s:%s: this value fits beside its name; do not break after the =\n    %s\n' "$file" "$line" "$collapsed" ;;
        suppression) printf '%s:%s: a suppression belongs on one line, so a duplicate is seen rather than read\n    %s\n' "$file" "$line" "$collapsed" ;;
        unclosed)    printf '%s:%s: a suppression belongs on one line; this one never closes\n' "$file" "$line" ;;
    esac
}

# One site at a time, back to front, so an earlier rewrite cannot move the lines a later one names.
apply() {
    local file="$1" line="$2" action="$3" collapsed="$4" end="${5:-}"

    case "$action" in
        # Reported, never rewritten: without a closing bracket there is no end to join up to, and
        # guessing one would eat the rest of the file. Non-zero so the caller can tell a site it
        # declined from one it changed.
        unclosed) return 1 ;;
        suppression)
            COLLAPSED="$collapsed" awk -v target="$line" -v end="$end" '
                NR == target                { print ENVIRON["COLLAPSED"]; next }
                NR > target && NR <= end    { next }
                { print }
            ' "$file" > "$file.tmp"
            ;;
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
        joinvalue)
            COLLAPSED="$collapsed" awk -v target="$line" '
                NR == target     { print ENVIRON["COLLAPSED"]; next }
                NR == target + 1 { next }
                { print }
            ' "$file" > "$file.tmp"
            ;;
    esac

    mv "$file.tmp" "$file"
}

reported=0

for file in "${paths[@]}"; do
    [[ -f "$file" ]] || continue

    if [[ "$fix" -eq 0 ]]; then
        sites="$(scan "$file")"
        [[ -n "$sites" ]] || continue

        while IFS=$'\t' read -r line action collapsed end; do
            report_of "$file" "$line" "$action" "$collapsed"
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
        [[ -n "$sites" ]] || break

        # A site the fixer declines — an unterminated suppression — is reported by every pass and
        # rewritten by none, so what is counted is what actually changed, and a pass that changes
        # nothing ends the loop. Counting sites instead would spin to the cap and claim five
        # rewrites of a file it never touched.
        rewritten=0
        while IFS=$'\t' read -r line action collapsed end; do
            apply "$file" "$line" "$action" "$collapsed" "$end" || continue
            rewritten=$((rewritten + 1))
        done <<< "$(printf '%s\n' "$sites" | sort -t$'\t' -k1,1nr)"

        fixed=$((fixed + rewritten))
        [[ "$rewritten" -gt 0 ]] || break
    done

    if [[ "$fixed" -gt 0 ]]; then
        reported=$((reported + fixed))
        printf 'fixed %s\n' "$file"
    fi
done

if [[ "$reported" -eq 0 ]]; then
    echo "Nothing to report."
    exit 0
fi

if [[ "$fix" -eq 1 ]]; then
    printf '\nRewrote %d site(s).\n' "$reported"
    exit 0
fi

printf '\n%d site(s). Run tools/style/lint-layout.sh --fix to rewrite them.\n' "$reported"
exit 1
