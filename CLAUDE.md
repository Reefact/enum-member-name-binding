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

`tools/style/lint-single-line-exits.sh` decides the cases that can be decided mechanically, and CI
runs it. It has no exception mechanism on purpose: everything it reports already fits, so the only
answer is to rewrite it — and everything too wide it never reports at all.

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
and the structural comparison in `DocumentationLinksTests`. Ticking them without doing them turns a
failing build into a puzzle.
