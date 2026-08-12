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
# which is what makes it usable from CI. `--fix` rewrites what it can, then reports what it could
# not and exits 1 for it as well — the file it leaves behind is one the check run and the hook both
# refuse, so answering 0 would send a developer to commit a tree CI is about to reject. Exit 2 is
# a third answer, and separate on purpose: it says the checker is broken rather than the tree, which
# a CI log reading exit 1 would take for an ordinary dirty tree.

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
# A wrapped suppression whose `)]` arrives with a member after it is neither of those two and
# produces no record at all: joining up to that line would fold the member onto the attribute, so
# closesLine tells the two apart and this one is passed over in silence. A trailing `//` comment is
# not a member and does not buy that silence — reading it as one hid a genuinely wrapped suppression
# from both modes, which is the one outcome indistinguishable from a clean tree.
#
# Which line closes the attribute is decided with the strings taken off the line first, so a `)]`
# written inside a Justification is not mistaken for the closing bracket. Read as one, it made the
# opening line look like a closing line too, and a genuinely wrapped suppression was passed over
# without a word — the same quiet answer a clean tree gives.
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

        # The line with the interior of its double-quoted strings taken out, which is what the two
        # bracket tests below have to read. A `)]` inside a Justification closes no attribute, and
        # counting it as one made a genuinely wrapped suppression vanish from both modes at once —
        # the site opens and appears to close on the same line, so nothing is reported and nothing is
        # rewritten, which is what a clean tree looks like.
        #
        # Not a C# lexer, and it does not need to be: a raw string never arrives, since the `"""`
        # fences below skip it, and the doubled quote of a verbatim string closes and reopens, which
        # ends in the same place. What it deliberately does not do is cut the line at a `//`. That
        # would read a Justification citing a URL as a comment, take the real `)]` away with it, and
        # turn a suppression already on one line into a wrapped one — whose forward scan is the thing
        # that deleted two members the last time it was let loose. So a `)]` written inside a comment
        # is still read as a close, and joins the shapes below that this admits it cannot read: the
        # comment would swallow the rest of the joined line, so there is no rewrite to offer anyway.
        function code(line,   out, i, character, inString) {
            out      = ""
            inString = 0

            for (i = 1; i <= length(line); i++) {
                character = substr(line, i, 1)

                if (inString) {
                    if (character == "\\") { i++; continue }
                    if (character == "\"") { inString = 0 }
                    continue
                }

                if (character == "\"") { inString = 1; continue }

                out = out character
            }

            return out
        }

        # Anywhere on the line rather than at its end, because what this answers is whether the
        # brackets of the attribute close here — not whether the line stops there. Anchored, it read a
        # suppression already written on one line but carrying a trailing comment as a wrapped one,
        # and --fix then joined it to everything up to the next `)]`: two members deleted, folded
        # behind the `//` where they no longer compile, and "Rewrote 1 site(s)." with exit 0.
        function closesAttribute(line) {
            return code(line) ~ /\)\]/
        }

        # And whether it closes with nothing but a comment after it, which is what makes a wrapped
        # site one this can rewrite: joining up to a line that carries a member as well would fold
        # that member onto the attribute. The mirror of `[Fact, SuppressMessage(` — where the line
        # the attribute opens on carries something else — and unreadable for the same reason.
        #
        # A `//` is not something else. It runs to the end of the line, so nothing can be hiding
        # behind it and the join is safe — where reading it as one made a wrapped suppression
        # carrying a trailing comment vanish from both modes at once: "Nothing to report." and exit
        # 0, which is what a clean tree says. A `/*` stays unread, because a member can follow its
        # close on the same line and telling that apart is parsing C# again.
        function closesLine(line) {
            return code(line) ~ /\)\][ \t]*$/ || code(line) ~ /\)\][ \t]*\/\//
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
                        if (closesAttribute(lines[j])) { closed = closesLine(lines[j]) ? 1 : 2; break }
                    }

                    if (closed == 0) { print i "\tunclosed\t"; continue }

                    # Closes on a line that carries code as well. Skipped so nothing below looks
                    # inside it, and reported by nothing: there is no rewrite to name, and saying so
                    # would be a complaint with no answer to it.
                    if (closed == 2) { i = j; continue }

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

# An action neither `case` below recognises means scan() learned to emit one and only one of the
# two was told. Without this the run stays green while the site is silently skipped — success
# reported for work not done, which this repository has already been caught by three times. Exit 2
# rather than 1, so a CI log cannot read a broken checker as an ordinary dirty tree.
bug() {
    local message="$1"

    printf 'lint-layout: %s\n' "$message" >&2
    exit 2
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
        *)           bug "unknown action '$action' reported at $file:$line" ;;
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
        *)
            bug "unknown action '$action' to apply at $file:$line"
            ;;
    esac

    mv "$file.tmp" "$file"
}

reported=0
declined=0

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

    # What --fix could not rewrite is still a violation, and the tree it leaves behind is the one
    # the check run, the pre-commit hook and the CI style job all refuse. Scanned again rather than
    # remembered from the loop above, so this says what the file is now instead of what the fixer
    # believed it was doing — and so a site the five-pass cap ran out on is named too.
    while IFS=$'\t' read -r line action collapsed end; do
        [[ -n "$line" ]] || continue

        report_of "$file" "$line" "$action" "$collapsed"
        declined=$((declined + 1))
    done <<< "$(scan "$file")"
done

if [[ "$fix" -eq 0 ]]; then
    if [[ "$reported" -eq 0 ]]; then
        echo "Nothing to report."
        exit 0
    fi

    printf '\n%d site(s). Run tools/style/lint-layout.sh --fix to rewrite them.\n' "$reported"
    exit 1
fi

if [[ "$reported" -eq 0 && "$declined" -eq 0 ]]; then
    echo "Nothing to report."
    exit 0
fi

if [[ "$reported" -gt 0 ]]; then
    printf '\nRewrote %d site(s).\n' "$reported"
fi

# Non-zero for the same reason the check run is: the tree still violates the rule. Printing the
# rewrites and stopping at 0 told a developer the file was clean while the same script in check mode
# exited 1 on it — a fourth green run over work not done, in the repository that keeps a section
# about the other three.
if [[ "$declined" -gt 0 ]]; then
    printf '\n%d site(s) left alone; --fix does not rewrite them.\n' "$declined"
    exit 1
fi

exit 0
