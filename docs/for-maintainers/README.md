# Maintainer documentation

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](README.fr.md)

Written for whoever changes this repository, which today means the decision records. A record says
what was decided, what it was weighed against, and what would overturn it — so a choice made once
does not have to be re-argued from memory every time it is questioned.

Two things about this section are deliberate. Its pages quote C# to carry an argument rather than to
teach, so they are the one body of documentation the compile contract does not cover; the reason
lives in `DocumentationCorpus`. And the conventions a contributor follows day to day are not here
but in [CONTRIBUTING](../../CONTRIBUTING.md), which GitHub renders from the repository root.

## Decision records

| Record | What it settles |
|---|---|
| [ADR 0001 — NFluent for test assertions](adr/0001-nfluent-for-test-assertions.en.md) | why assertions read as `Check.That(…)` while xUnit still discovers and runs the tests |
