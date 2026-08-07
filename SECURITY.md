# Security Policy

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](docs/SECURITY.fr.md)

## Supported versions

Security fixes are made against the latest stable release. Upgrade to it before reporting, so that
a report describes something still present.

| Version | Supported |
| --- | --- |
| Latest stable release | Yes |
| Earlier releases | No |
| Pre-release versions | Best effort |

## Reporting a vulnerability

Please do not open a public issue, discussion or pull request for a suspected vulnerability. Use
GitHub's private reporting instead:

[Open a private security advisory](https://github.com/Reefact/enum-member-name-binding/security/advisories/new)

Include as much of the following as you have:

- the affected package and version;
- the environment where you observed it;
- what the vulnerability is, and what it lets someone do;
- the steps to reproduce it;
- a minimal proof of concept, if one is reasonable to write;
- any mitigation or workaround you already know of;
- whether it has been disclosed publicly anywhere.

Do not include secrets, personal data, access tokens, or anything belonging to a third party.

## What to expect

- an acknowledgement within 3 business days;
- an initial assessment within 7 business days;
- a status update at least every 14 days while it remains open;
- a fix and a coordinated disclosure within 90 days where that is reasonably possible.

Severity, complexity and the availability of a safe fix can move those dates. Any significant change
to them will be discussed with you rather than decided silently. Please keep the report confidential
until a fix or a mitigation is available.

## Scope

This library sits on the request path: it turns text arriving on a route, a query string, a form
field or a header into an enum value. Reports about that boundary are the ones most likely to
matter here. Examples of what qualifies:

- input that binds to a value the declared contract does not allow;
- a way to bypass the start-up validation of a contract;
- exposure of information a caller should not see;
- arbitrary or unintended code execution;
- a vulnerability in how the package is built, signed or published;
- a supply-chain weakness this project introduces.

Generally not security vulnerabilities:

- ordinary bugs with no security impact;
- feature requests and documentation mistakes;
- problems only reproducible on an unsupported version;
- vulnerabilities in dependencies that this package does not actually expose.

Anything in that second list is welcome as a public issue.

## Disclosure

Once a report is confirmed, a private advisory may be opened to coordinate the fix. After a fix or
a mitigation is available, an advisory may be published containing:

- what the vulnerability was and what it allowed;
- the affected and corrected versions;
- available mitigations or workarounds;
- upgrade instructions;
- a CVE identifier where one is appropriate;
- credit to the reporter, unless they prefer to stay anonymous.

Public disclosure should normally follow, not precede, a release that fixes the problem. There is no
paid bounty programme; researchers reporting in good faith are credited in the advisory.
