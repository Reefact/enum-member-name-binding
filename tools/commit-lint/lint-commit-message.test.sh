#!/usr/bin/env bash
#
# The commit linter had no test, and shipped the one bug a linter can ship without anyone noticing:
# it exited 0 on headers its own grammar rejects. The grammar check is a single regular expression,
# and everything after it is four diagnostics that name a part of the header — a missing colon, an
# unknown type, a malformed scope, a capitalised description. None of the four is a catch-all, so a
# header wrong in a fifth way accumulated no error, fell through to `exit 0`, and both the hook and
# CI called it conforming. `feat: 1 add the thing` is the shortest example.
#
# Nothing about that is visible in a green build — which is the argument for this file rather than
# for a more careful reading of that one. What a checker must refuse is invisible in its output.
#
# The status is asserted, and so is the sentence: a catch-all wide enough to cover every fault would
# keep this file green while telling every author the same unhelpful thing, so each case names the
# diagnostic it must earn.
#
# Usage: tools/commit-lint/lint-commit-message.test.sh

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
linter="$here/lint-commit-message.sh"

cases=0
failures=0

# label, mode (hook or ci), expected status, a sentence the output must contain — empty when the
# case is expected to pass — and the message itself, which may carry a body.
expect() {
    local label="$1" mode="$2" status="$3" fragment="$4" message="$5"
    local output actual

    cases=$((cases + 1))

    set +e
    if [[ "$mode" == "ci" ]]; then
        output="$(printf '%s\n' "$message" | "$linter" --ci - 2>&1)"
    else
        output="$(printf '%s\n' "$message" | "$linter" - 2>&1)"
    fi
    actual=$?
    set -e

    if [[ "$actual" -ne "$status" ]]; then
        printf 'FAIL: %s — exited %s, expected %s\n%s\n\n' "$label" "$actual" "$status" "$output"
        failures=$((failures + 1))

        return 0
    fi

    if [[ -n "$fragment" ]] && ! grep -qF -- "$fragment" <<< "$output"; then
        printf 'FAIL: %s — the status was right, but nothing said "%s"\n%s\n\n' "$label" "$fragment" "$output"
        failures=$((failures + 1))
    fi
}

# The three that exited 0. Each carries a colon, a known type, no scope and a lowercase description,
# so every one of the four diagnostics looked away — and the grammar rejects all three anyway: two
# because the description does not start on a letter, one because the colon does not follow the type.
expect 'a description starting on a digit' hook 1 "expected '<type>[(scope)][!]: <description>'" 'feat: 1 add the thing'
expect 'a description starting on a dash'  hook 1 "expected '<type>[(scope)][!]: <description>'" 'feat: -add the thing'
expect 'a space before the colon'          hook 1 "expected '<type>[(scope)][!]: <description>'" 'feat : add the thing'

# And the four that did fire, so the catch-all above sits behind them rather than in front: a header
# whose fault has a name has to keep earning that name, not the general sentence.
expect 'nothing that looks like a header' hook 1 'nothing looks like'              'add the thing'
expect 'an unknown type'                  hook 1 "unknown type 'nope'"             'nope: add the thing'
expect 'a capitalised description'        hook 1 'starts with a capital'           'feat: Add the thing'
expect 'a scope that is not kebab-case'   hook 1 "the scope 'Bad_Scope'"           'feat(Bad_Scope): add the thing'

# The two header rules outside the grammar.
expect 'a trailing period'    hook 1 'must not end with a period' 'feat: add the thing.'
expect 'a header past the ceiling' hook 1 'keep it within 72' \
    'feat: add a description long enough to run past the seventy-two character ceiling'

# Conforming, in each shape the convention admits. A checker that refuses everything passes every
# test above and is no more usable than one that refuses nothing.
expect 'the plain shape'      hook 0 '' 'feat: add the thing'
expect 'a scope'              hook 0 '' 'fix(style): stop deleting methods'
expect 'a scope in kebab-case' hook 0 '' 'ci(commit-lint): guard the linter with a test'
expect 'a body under a blank line' hook 0 '' 'docs: say what it does

And a body, which is prose and is left alone: 1, -, and a Capital.'
expect 'a breaking change announced twice' hook 0 '' 'refactor!: change the shape

BREAKING CHANGE: it moved.'

# The body and footer rules, which are the ones whose mistake is silent rather than visible.
expect 'a body glued to the header' hook 1 'leave a blank line' 'feat: add the thing
straight into the body'
expect 'a bang with no footer' hook 1 "needs a 'BREAKING CHANGE:' footer" 'refactor!: change the shape'
expect 'a footer with no bang' hook 1 "needs a '!' before the colon" 'refactor: change the shape

BREAKING CHANGE: it moved.'
expect 'a footer spelled with a hyphen' hook 1 'must read exactly' 'refactor!: change the shape

BREAKING-CHANGE: it moved.'

# The two exemptions, and the one that depends on which side is asking. A generated merge message
# holds to no convention; an autosquash placeholder is waiting for a rebase the hook must not break,
# and by CI that rebase should have happened.
expect 'a merge commit'                     hook 0 '' 'Merge branch main into a feature branch'
expect 'an autosquash placeholder, in the hook' hook 0 '' 'fixup! feat: add the thing'
expect 'an autosquash placeholder, in CI'   ci   1 'squash this autosquash placeholder' 'fixup! feat: add the thing'
expect 'a conforming message in CI'         ci   0 '' 'feat: add the thing'

if [[ "$failures" -gt 0 ]]; then
    printf '\n%d of %d case(s) failed.\n' "$failures" "$cases"
    exit 1
fi

printf 'ok — %d cases: refused for the stated reason, exempted where the convention says so.\n' "$cases"
