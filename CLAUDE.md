# Working in this repository

Conventions that are decided rather than obvious, written down so they are followed rather than
rediscovered. Read this before changing anything; it is short on purpose.

## Analyzer and Sonar findings

A finding is a claim about the code. Five things can be done with one, and picking the right one is
the whole job — silencing a rule that is right, or obeying a rule that is wrong, both cost more than
they save.

**1. Fix it.** The default. The rule is right and the change is an improvement, so make it.

**2. Excuse the site, with `[SuppressMessage]`.** The rule is right in general and wrong *here*.
Put the attribute on the member and say why in `Justification` — the reason is then read where the
code is read. Prefer this to any broader form: it names one site, so the same rule reported anywhere
else still arrives.

**3. Excuse the site, with `[UnconditionalSuppressMessage]`.** Only for trimming and AOT findings
(`IL2026`, `IL3050` and their kind). It is not a synonym for the above: it survives into the IL
because the tool that reads it — the linker — runs after compilation, and *unconditional* is a
promise that the code is safe however the application is published. A wrong one fails at run time in
a trimmed build, not at build time. Its `Justification` must explain why the code stays correct once
trimmed, not why the warning is inconvenient.

**4. Decline the rule for a category, in `.editorconfig`.** Legitimate when a rule is wrong for a
whole class of code rather than one site — but it is a wider decision than it looks, because it also
stops the rule reporting where obeying it would have been right.

> **Ask the maintainer before doing this, and explain the trade rather than announcing it.** What
> the rule asks for, why it does not hold for this category, exactly which files stop being
> reported, and what is lost by not hearing it there any more.

**5. Ignore it in `.github/workflows/sonar.yml`.** Reserved for SonarQube's *own* analyses — the
shell and YAML rules — which no compiler setting or attribute can reach. A Roslyn diagnostic that
Sonar merely republishes (the `external_roslyn:` prefix) does not belong here: silenced there, the
build and every editor keep proposing the change, and only the report goes quiet.

**6. Leave it visible.** The rule is right, and acting on it now is not worth it. Better standing as
known debt than suppressed: a silenced finding is indistinguishable from one nobody ever had.

Whichever is chosen, the reason lives in the repository — in an attribute, a comment or a workflow —
and never as a click in the SonarQube UI, where a reader of this repository cannot see it and a
recreated project loses it.

## An `if` that only exits fits on one line

```csharp
if (string.IsNullOrEmpty(name)) { return Problem.EmptyName(memberName); }
if (isFlags && name.Contains(',')) { return Problem.CommaInFlagsName(memberName, name); }
```

A run of them is one block, so consecutive guards take no blank line between them. The body has to
be a single `return`, `throw`, `continue` or `break` — anything else keeps the multi-line form, and
so does a guard that would run past 140 characters collapsed — a ceiling, not an exemption: a guard
too wide to fit is usually one whose condition wants a name.

## A value that fits goes beside its name

```csharp
internal const string Reflection = "Enum member name binding reads enum metadata reflectively…";
```

Breaking after the `=` leaves a name with no value above a value with no name. Break there only
when the value genuinely needs more than one line — a concatenation, a long initializer — and never
merely because the whole declaration is wide. A declaration too wide even for that usually has a
seam of its own: break at the call, not between the name and what it is.

## A suppression fits on one line, however long that line is

```csharp
[UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = SuppressionJustification.IL2026.RequirementCarriedByConstructor)]
[UnconditionalSuppressMessage(TrimRule.IL3050.Category, TrimRule.IL3050.Id, Justification = SuppressionJustification.IL3050.RequirementCarriedByConstructor)]
```

A member usually carries more than one. Wrapped at the comma they interleave into a paragraph where
two suppressions differ by one token somewhere in the middle, and telling them apart becomes a diff
done by eye — which is how a duplicate survives being looked at. One per line turns that comparison
into a glance down the left edge. This rule was written the day the pair above was read as a
copy-paste of itself and reported as a duplicate.

The one rule here with no ceiling, and the exception is deliberate. The others stop where the single
line stops being the easier read, because they are logic. A suppression is not read for its logic —
it is scanned for which rule, and skimmed for why — so wrapping it saves a reader nothing and costs
them the comparison.

## The checker

`tools/style/lint-layout.sh` decides all four of the above, and CI runs it — the test first, then
the check, since a checker that reports nothing because it is broken reads exactly like a clean
tree. It has no exception mechanism on purpose: everything it reports already fits, so the only
answer is to rewrite it, and everything too wide it never reports at all.

Two shapes it admits it cannot read, and they are one question asked at either end of the attribute:
a suppression sharing its brackets with another one, as in `[Fact, SuppressMessage(...)]`, and a
wrapped suppression whose closing `)]` is followed by a member on the same line. Knowing where one
attribute ends, or what follows it, means parsing C#, so it stays silent rather than guessing.

`--fix` reports what it declines and exits non-zero for it, rather than printing what it rewrote and
stopping at 0 — a fixer that leaves a violation behind and answers like a clean tree sends a
developer to commit what the same script, in check mode, is about to refuse.

## An `if` wrapping the rest of a method becomes a guard

Invert it, so the case with nothing to do leaves at the top and the work sits at the method's own
indent.

```csharp
if (!options.ConfigureJsonSerialization) { return builder; }
if (contractEnums.Count == 0) { return builder; }

builder.AddJsonOptions(…);
```

Two guards rather than one negated conjunction: `!a || b == 0` is harder to read than either half
on its own.

What keeps this a judgement and not a rule is the tail. Inverting duplicates whatever followed the
block, which is free when that is `return;` or `return builder;` and not free when it is a real
call — `return base.ConvertTo(context, culture, value, destinationType);` written twice is two
things to keep in sync, and costs more than the indent it saves. Those stay as they are.

No check enforces this, deliberately: a checker would invert the second kind too.

## Verify, do not assume

Three integrations in this repository have reported success while doing nothing: `dotnet test`
printing "unable to find a datacollector" as an *informational* line and passing, a report uploaded
into a short-lived branch that project measures never read, and a scripted edit announcing a
replacement it had not made. All three had a green workflow.

So a check is not that a command exited zero, but that the thing it was supposed to produce exists
and says what it should. Read the artifact, query the API, count the findings.

## The pull request checklist is not decoration

`.github/pull_request_template.md` asks about the public API and about the French counterpart of any
documentation change. Both are enforced by tests that fail the build — the committed API baseline,
and `AspNetCore.EnumMemberNameBinding.Documentation.Tests`, which holds four contracts at once:
every link resolves, every page is paired with a translation of the same structure, every C# sample
compiles and obeys the rules this library ships, and every folder's index lists what is beside it.
Ticking those boxes without doing the work turns a failing build into a puzzle.
