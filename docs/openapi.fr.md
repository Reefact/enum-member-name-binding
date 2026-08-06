# OpenAPI

🌍 **Langues :**  
🇬🇧 [English](./openapi.en.md) | 🇫🇷 Français (ce fichier)

```
dotnet add package AspNetCore.EnumMemberNameBinding.OpenApi
```

```csharp
using Microsoft.AspNetCore.OpenApi;

builder.Services.AddOpenApi(options => options.AddEnumMemberNames());
```

## Pourquoi un paquet compagnon est nécessaire

Laissé à lui-même, ASP.NET Core décrit chaque énumération d'une application MVC comme un simple
entier, parce que `Microsoft.AspNetCore.OpenApi` lit `Http.Json.JsonOptions` tandis que MVC lit les
siennes. Le paquet principal configure désormais les deux, et le compagnon corrige ce qu'il reste :

| Schéma | ASP.NET Core seul | Avec le compagnon |
|---|---|---|
| `ProductStatus` | `{"type":"integer"}` | `{"type":"string","enum":["available","out_of_stock","discontinued"]}` |
| `Permissions` (`[Flags]`) | `{"type":"integer"}` | `{"type":"string","pattern":"^\\s*(read\|write\|delete)(\\s*,\\s*(read\|write\|delete))*\\s*,?\\s*$"}` |
| `PlainPriority` (sans contrat) | `{"type":"integer"}` | `{"type":"integer"}` — intact |

Deux détails valent d'être connus. ASP.NET Core émet les valeurs d'énumération **sans déclarer de
type**, ce que le compagnon corrige. Et pour une énumération `[Flags]`, il n'émet délibérément aucune
valeur : une liste fermée ne peut pas exprimer les combinaisons, alors le compagnon émet à la place
une expression régulière, à la fois précise et vérifiable par une machine.

## Le motif `[Flags]`

Le motif couvre exactement ce que le binder accepte, espaces et virgule finale compris — voir
[règles du contrat](contract-rules.fr.md#une-énumération-flags). Il est écrit dans le dialecte
ECMA-262 avec lequel un `pattern` de JSON Schema est lu : seuls les caractères de syntaxe sont
échappés. `Regex.Escape` produirait `\ ` et `\#`, qui ne sont pas des échappements d'identité valides
dans ce dialecte et feraient rejeter le motif entier par un consommateur strict. Un test rejette tout
autre échappement.

Un nom public contenant un métacaractère d'expression régulière est échappé et correspond quand même
littéralement ; un nom contenant une espace est reconnu tel quel, puisque seuls les séparateurs autour
des virgules sont souples.

## Vérifié face au serveur en fonctionnement

La suite de tests vérifie directement la cohérence document/exécution, plutôt que de confronter le
document à lui-même : chaque valeur que le document annonce est envoyée au serveur en fonctionnement
et doit être acceptée, et chaque valeur qu'il exclut doit être rejetée. Le motif `[Flags]` documenté
est compilé et rejoué de la même façon.

## Plancher de version

Le compagnon relève le plancher de `Microsoft.OpenApi` à 2.11.0. `Microsoft.AspNetCore.OpenApi` 10.0.x
résout 2.0.0, qui porte l'avis de sécurité
[GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

## Trimming et Native AOT

Tout le paquet est annoté `[RequiresUnreferencedCode]` : lire les métadonnées d'une énumération exige
de la réflexion. Il ne porte pas `[RequiresDynamicCode]`, car décrire un schéma ne génère aucun code.
