# Analyzers

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./analyzers.fr.md)

The package ships Roslyn analyzers — no extra install. A contract mistake is a build error in your
editor, not an exception found when the application starts.

| ID | Severity | Reported when |
|---|---|---|
| [`EMN0001`](rules/EMN0001.en.md) | Error | Two members declare the same public name |
| [`EMN0002`](rules/EMN0002.en.md) | Error | A public name is empty, or has leading or trailing whitespace |
| [`EMN0003`](rules/EMN0003.en.md) | Error | A contract enum leaves some members unannotated |
| [`EMN0004`](rules/EMN0004.en.md) | Error | A `[Flags]` public name contains a comma |
| [`EMN0005`](rules/EMN0005.en.md) | Error | A public name is also the C# name of another member |
| [`EMN0006`](rules/EMN0006.en.md) | Warning | A public name cannot travel on every input channel |

**An enum carrying no `[JsonStringEnumMemberName]` at all is never analysed.** The rules only apply
once you have declared a contract, so adding this package to an existing solution does not light up
enums it has nothing to do with.

## EMN0005, the one worth reading twice

```csharp
public enum Colour
{
    [JsonStringEnumMemberName("Blue")] Red,   // EMN0005
    Blue
}
```

A declared public name is matched first and case-sensitively, while an unannotated member's C# name
is matched case-insensitively. So `?colour=Blue` binds to **`Red`**, while `?colour=blue` and
`?colour=BLUE` bind to `Blue`. The member answers to every casing of its name except its own — which
no reader of that enum would ever guess.

The casing of the declared name changes nothing, so the rule ignores case:
`[JsonStringEnumMemberName("blue")]` next to a `Blue` member is the mirror image of the same trap and
is reported too.

It only fires next to `EMN0003`, since it needs an unannotated member. That is precisely why it is
its own rule: turn `EMN0003` off to allow partial contracts and `EMN0005` is the only protection
left, so it is an error rather than a warning — and unlike `EMN0003`, it is still enforced at
start-up even when `AllowPartialContracts` is set.

## Why EMN0006 is only a warning

Unlike the others, it is not an ambiguity: the contract is well defined, it simply cannot cross one
particular channel. Whether that matters depends on the channels your API binds from — a French
public name is a fine choice for an API that never reads a header. The message names the character
and the channel that refuses it, so the call can be made per name.

## Configuring severities

The analyzers cannot see your runtime configuration, so if you deliberately use
`AllowPartialContracts`, turn `EMN0003` off — and keep `EMN0005` on:

```ini
[*.cs]
dotnet_diagnostic.EMN0003.severity = none
```

## At start-up too

Every rule except `EMN0006` is also enforced when the application starts, for enums that reach the
runtime from an assembly built without the analyzers. `EnumContractException` then names the type,
every problem and the expected fix.

Two members may share the same numeric value as long as their public names differ.
