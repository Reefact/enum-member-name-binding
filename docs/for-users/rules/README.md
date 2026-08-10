# Diagnostics

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](README.fr.md)

One page per rule, and it is where the diagnostic's help link points — so a reader usually arrives
here from a build error rather than from this index. Each page says what the rule catches, why it
matters, how to fix it, and whether suppressing it is ever right.

An enum that declares no `[JsonStringEnumMemberName]` at all is never analysed, so adding this
package to an existing solution lights up nothing it has nothing to do with. The narrative version
of the table below is in [Analyzers](../analyzers.en.md).

| Rule | Severity | What it catches |
|---|---|---|
| [EMN0001](EMN0001.en.md) | Error | Two enum members declare the same public name |
| [EMN0002](EMN0002.en.md) | Error | The public name is empty or padded with whitespace |
| [EMN0003](EMN0003.en.md) | Error | The enum contract is incomplete |
| [EMN0004](EMN0004.en.md) | Error | A `[Flags]` public name contains a comma |
| [EMN0005](EMN0005.en.md) | Error | A public name shadows the C# name of another member |
| [EMN0006](EMN0006.en.md) | Warning | The public name cannot travel on every input channel |
