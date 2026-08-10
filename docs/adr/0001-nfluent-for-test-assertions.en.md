# ADR 0001 — NFluent for test assertions

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0001-nfluent-for-test-assertions.fr.md)

**Status:** accepted — 2026-08-10  
**Scope:** the three test projects. The library itself takes no test dependency.

## Context

Assertions were written with xUnit's own `Assert`, 280 calls across 24 files. Nothing was wrong with
them; what they lack is a reading order. `Assert.Equal(expected, actual)` puts the expectation
first and the subject second, which is the reverse of how the sentence is spoken and the reverse of
every other call in the test. `Assert.Contains` swaps its two arguments depending on whether the
subject is a string or a collection, and both overloads compile with the arguments the wrong way
round when the two types happen to match — a test that then passes for the wrong reason.

This repository already treats a line that is not a thought as a defect worth a checker. An
assertion read backwards is the same complaint one level up.

## Decision

Test assertions are written with [NFluent](https://github.com/tpierrain/NFluent) 3.1.0.

xUnit stays: it discovers, runs and reports the tests, and `[Fact]`, `[Theory]` and the fixtures are
untouched. Only the assertion changes.

```csharp
Check.That(bound.Value).IsEqualTo("out_of_stock");
Check.That(exception.Problems).HasOneElementOnly();
Check.ThatCode(() => EnumContract.For(typeof(DuplicateNames))).Throws<EnumContractException>();
```

The subject comes first, then what is claimed about it.

## Alternatives

| Option | Why not |
| --- | --- |
| Keep `Assert` | No dependency, and the argument order stays reversed and inconsistent. |
| FluentAssertions | Version 8 moved to a paid licence for commercial use. A licence change in a test dependency is a poor thing to inherit. |
| AwesomeAssertions | The Apache-2.0 fork of FluentAssertions 7, and a plausible answer; it is young, and its long-term maintenance is the open question. |
| Shouldly | Comparable and healthy. NFluent was preferred by the maintainer; nothing here rules Shouldly out. |

## Consequences

Four of these were measured against this repository rather than read off documentation.

**NFluent 3.1.0 does not recognise xUnit v3.** It throws `NFluent.Kernel.FluentCheckException`
instead of `Xunit.Sdk.XunitException`. The test still fails, and the runner still reports it as a
failure with the full NFluent message — but the report carries the exception type name where it used
to read as a native assertion failure. Verified: forcing xUnit's assert assembly to load first does
not change the outcome, so this is NFluent looking for xUnit v2's assembly and not a load-order
accident.

**A check with no assertion passes silently.** `Check.That(value);` compiles, runs, asserts nothing,
and reports green — where `Assert.Equal` with a missing argument does not compile. This is the one
way this migration could have weakened the suite invisibly, so a test guards against it:
`AssertionStyleTests` reads the test sources and fails on a `Check.That` statement with nothing
chained.

**Capturing an exception is `.Value`.** `Assert.Throws<T>(...)` returned the exception; the NFluent
equivalent is `Check.ThatCode(...).Throws<T>().Value`, and it is what the sites asserting on
`exception.Problems` use.

**An async throw test becomes synchronous.** `Check.ThatAsyncCode` is marked obsolete in favour of
`ThatCode`, which accepts a `Func<Task>` and does not need awaiting. A test whose only `await` was
`Assert.ThrowsAsync` therefore loses its `async`, or the compiler reports CS1998 — which is an error
here, since warnings are errors.

**`CA1861` fires on an inline array** passed to a params-based check, so a collection expected value
is bound to a local first.

## The translation used

Applied uniformly, so a reader meeting one of these knows what it was.

| Was | Is |
| --- | --- |
| `Assert.Equal(e, a)` | `Check.That(a).IsEqualTo(e)` |
| `Assert.True(c)` / `Assert.False(c)` | `Check.That(c).IsTrue()` / `.IsFalse()` |
| `Assert.Null(x)` / `Assert.NotNull(x)` | `Check.That(x).IsNull()` / `.IsNotNull()` |
| `Assert.Contains(part, text)` | `Check.That(text).Contains(part)` |
| `Assert.DoesNotContain(part, text)` | `Check.That(text).Not.Contains(part)` |
| `Assert.Matches(p, s)` / `Assert.DoesNotMatch(p, s)` | `Check.That(s).Matches(p)` / `.Not.Matches(p)` |
| `Assert.Empty(xs)` / `Assert.NotEmpty(xs)` | `Check.That(xs).IsEmpty()` / `.Not.IsEmpty()` |
| `Assert.Single(xs)` | `Check.That(xs).HasOneElementOnly()` |
| `Assert.All(xs, x => …)` | `Check.That(xs).ContainsOnlyElementsThatMatch(x => …)` |
| `Assert.IsType<T>(x)` / `Assert.IsNotType<T>(x)` | `Check.That(x).IsInstanceOf<T>()` / `.IsNotInstanceOf<T>()` |
| `Assert.Same(a, b)` | `Check.That(b).IsSameReferenceAs(a)` |
| `Assert.Throws<T>(() => …)` | `Check.ThatCode(() => …).Throws<T>()` |
| `T e = Assert.Throws<T>(() => …)` | `T e = Check.ThatCode(() => …).Throws<T>().Value` |
| `Assert.ThrowsAny<Exception>(() => …)` | `Check.ThatCode(() => …).ThrowsAny()` |
| `await Assert.ThrowsAsync<T>(() => …)` | `Check.ThatCode(() => …).Throws<T>()` |

`Assert.Fail` has no NFluent equivalent and stays. It is not an assertion about a value; it is a
branch that should not have been reached.

## How this was verified

Not by a green suite: a migration that turns assertions into no-ops is green too. Five mutations
were introduced into the library — an uppercased formatted name, a downgraded analyzer severity, an
OpenAPI schema typed as integer, a changed separator in `AllowedValues`, and flags parsing made to
fail — and the set of test cases that noticed each one was recorded before the migration and
compared with the set after. The five sets are identical.

The instrument needed fixing twice, and both faults read as a result rather than as a fault. Its
extraction matched only test names made of word characters, so every `[Theory]` case was invisible
and the first mutation appeared to be caught by 7 tests where it is really caught by 27. Then it ran
the three test projects at once, whose output shares one stream, and one reading silently lost a
single line — which is indistinguishable from an assertion that stopped noticing, and cost a
detour to tell apart. One project at a time, and three consecutive readings agree.
