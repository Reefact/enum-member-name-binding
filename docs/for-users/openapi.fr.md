# OpenAPI

🌍 **Langues :**  
🇬🇧 [English](./openapi.en.md) | 🇫🇷 Français (ce fichier)

```
dotnet add package Reefact.AspNetCore.EnumMemberNameBinding.OpenApi
```

```csharp
builder.Services.AddOpenApi(options => options.AddEnumMemberNames());
```

## Pourquoi un paquet compagnon est nécessaire

Laissé à lui-même, ASP.NET Core décrit chaque énumération d'une application MVC comme un simple
entier, parce que `Microsoft.AspNetCore.OpenApi` lit `Http.Json.JsonOptions` tandis que MVC lit les
siennes. Le paquet principal configure désormais les deux, et le compagnon corrige ce qu'il reste :

| Schéma | ASP.NET Core seul | Avec le compagnon |
|---|---|---|
| `ProductStatus` | `{"type":"integer"}` | `{"type":"string","enum":["available","out_of_stock","discontinued"]}` |
| `Permissions` (`[Flags]`) | `{"type":"integer"}` | `{"type":"string"}`, plus le `pattern` [ci-dessous](#le-motif-flags) |
| `PlainPriority` (sans contrat) | `{"type":"integer"}` | `{"type":"integer"}` — intact |

Deux détails valent d'être connus. ASP.NET Core émet les valeurs d'énumération **sans déclarer de
type**, ce que le compagnon corrige. Et pour une énumération `[Flags]`, il n'émet délibérément aucune
valeur : une liste fermée ne peut pas exprimer les combinaisons, alors le compagnon émet à la place
une expression régulière, à la fois précise et vérifiable par une machine.

Un troisième, moins visible. Un seul composant décrit l'énumération partout où elle apparaît : un
*usage* nullable doit donc s'exprimer quelque part. Une propriété nullable est émise sous la forme
d'un `oneOf` entre un schéma null et une référence, ce qui laisse le composant intact ; un élément de
collection nullable — `List<ProductStatus?>` — n'est pas encapsulé, et ASP.NET Core place à la place
un null JSON parmi les valeurs du composant lui-même. Le compagnon conserve ce null et type le schéma
`["null","string"]`. Un schéma qui n'admet aucun null ne gagne ni l'un ni l'autre.

## Uniquement les énumérations que cette application a enregistrées

Le compagnon décrit une énumération parce que `AddEnumMemberNameBinding` l'a enregistrée, pas
simplement parce qu'elle porte `[JsonStringEnumMemberName]`. Les deux se lisent comme la même chose
et ne le sont pas : une énumération annotée que personne n'a enregistrée se lie par ses noms C# et se
sérialise en nombre, donc annoncer ses noms déclarés comme une chaîne serait faux pour la query
string *et* pour le corps, et un client généré depuis ce document enverrait des requêtes auxquelles
le serveur répond 400.

Utilisé seul — par une application qui enregistre elle-même son `JsonStringEnumConverter<T>` et
n'appelle jamais `AddEnumMemberNameBinding` — il n'y a aucun enregistrement à consulter, et toutes
les énumérations sous contrat sont décrites comme avant. Un registre absent n'est pas un registre
vide : ce que le registre écarte, c'est le cas où il existe et où l'énumération n'y figure pas, le
seul où document et serveur peuvent être connus comme divergents.

Une chose change de place dans cette configuration. `AddEnumMemberNameBinding` résout chaque
contrat au démarrage : un contrat malformé — deux membres partageant un nom public, par exemple —
lève donc `EnumContractException` avant la première requête. Le compagnon, lui, résout un contrat
au moment d'écrire le document, ce qui sous `MapOpenApi` est une requête : l'application démarre,
et `/openapi/v1.json` répond 500 avec le même message. Les analyseurs ne comblent pas l'écart non
plus, NuGet ne propageant pas les assets d'analyseur de façon transitive : une énumération déclarée
dans votre propre assembly est analysée, une énumération arrivant par une référence de paquet ne
l'est pas. Enregistrer les énumérations en plus ramène l'échec au démarrage.

## Le motif `[Flags]`

```json
{"type":"string","pattern":"^[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*(read|write|delete)([\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*,[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*(read|write|delete))*[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*,?[\\u0009\\u000A\\u000B\\u000C\\u000D\\u0020\\u0085\\u00A0\\u1680\\u2000-\\u200A\\u2028\\u2029\\u202F\\u205F\\u3000]*$"}
```

Le motif couvre exactement ce que le binder accepte, espaces et virgule finale compris — voir
[règles du contrat](contract-rules.fr.md#une-énumération-flags). Une seule forme y échappe : une
énumération déclarant des composites qui se recouvrent, où une liste admise par le motif peut ne se
décomposer en aucun membre et être refusée par un 400 — voir
[limitations](limitations.fr.md#une-combinaison-qui-ne-nomme-aucun-membre-est-refusée-hors-du-corps).

Deux choses le rendent long, et toutes deux font la différence entre décrire le serveur et en décrire
une approximation. La classe d'espaces est écrite en toutes lettres plutôt que `\s`, car un motif est
lu comme de l'ECMA-262, où `\s` inclut U+FEFF et exclut U+0085 — alors que le binder détoure avec
`char.IsWhiteSpace`, qui fait l'inverse sur les deux. Et un membre laissé sans annotation garde son
nom C#, que le binder reconnaît sans tenir compte de la casse : il apparaît donc comme
`[Dd][Ee][Ll][Ee][Tt][Ee]`, là où un nom déclaré apparaît tel quel. Une classe contient tous les
caractères que le binder tient pour égaux, ce qui pour une lettre ASCII fait ses deux formes et ne se
limite pas toujours à cela : les deux formes ne sont pas le même ensemble que l'égalité au sens
d'`OrdinalIgnoreCase`, et les écrire comme si elles l'étaient était faux dans les deux sens sur une
poignée de code points.

Il est écrit dans le dialecte ECMA-262 avec lequel un `pattern` de JSON Schema est lu : seuls les
caractères de syntaxe sont échappés. `Regex.Escape` produirait `\ ` et `\#`, qui ne sont pas des
échappements d'identité valides dans ce dialecte et feraient rejeter le motif entier par un
consommateur strict. Un test rejette tout autre échappement.

Un nom public contenant un métacaractère d'expression régulière est échappé et correspond quand même
littéralement ; un nom contenant une espace est reconnu tel quel, puisque seuls les séparateurs autour
des virgules sont souples.

## Vérifié face au serveur en fonctionnement

La suite de tests vérifie directement la cohérence document/exécution, plutôt que de confronter le
document à lui-même : chaque valeur que le document annonce est envoyée au serveur en fonctionnement
et doit être acceptée, et chaque valeur qu'il exclut doit être rejetée. Le motif `[Flags]` documenté
est compilé et rejoué de la même façon.

Une asymétrie assumée. Le schéma d'une énumération sans `[Flags]` reste une liste `enum` fermée : il
n'annonce donc pas les combinaisons séparées par des virgules que le serveur accepte aussi —
[`available,out_of_stock`](contract-rules.fr.md#une-virgule-sépare-les-valeurs-sur-toutes-les-énumérations)
se lie et ne figure pas dans la liste. C'est le sens dans lequel il vaut mieux se tromper, et
l'alternative ne serait pas seulement moins élégante : elle serait fausse. Un motif est exact pour une
`[Flags]` parce qu'une combinaison de membres déclarés est, la forme ci-dessus mise à part, une
valeur que le serveur accepte. Sur une énumération ordinaire, seules le sont les combinaisons dont
le résultat nomme un membre déclaré :
`out_of_stock,discontinued` vaut `1 | 2`, qui n'en nomme aucun, et le serveur répond 400. Une
expression régulière ne peut pas distinguer les deux, elle annoncerait donc des valeurs qui échouent —
elle sur-promettrait, là où la liste fermée sous-promet. La liste est aussi le vocabulaire à partir
duquel un client se génère ; un motif transformerait l'énumération en champ texte libre dans chaque
client généré.

## Plancher de version

Le compagnon relève le plancher de `Microsoft.OpenApi` à 2.11.0. `Microsoft.AspNetCore.OpenApi` 10.0.x
résout 2.0.0, qui porte l'avis de sécurité
[GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc).

## Trimming et Native AOT

Tout le paquet est annoté `[RequiresUnreferencedCode]` : lire les métadonnées d'une énumération exige
de la réflexion. Il ne porte pas `[RequiresDynamicCode]`, car décrire un schéma ne génère aucun code.
