# Règles du contrat — ce qui est accepté

🌍 **Langues :**  
🇬🇧 [English](./contract-rules.en.md) | 🇫🇷 Français (ce fichier)

Les règles ne sont pas inventées ici : elles portent celles que `System.Text.Json` applique au corps
de la requête, afin que tous les canaux de votre API acceptent exactement le même vocabulaire.

Tout ce qui suit est le **texte brut d'une requête entrante**, pas une valeur C# — un segment de
route, une valeur de chaîne de requête, un champ de formulaire ou un en-tête. `?status=1`, c'est le
caractère `1`, pas l'entier.

## Une énumération totalement annotée

```csharp
public enum ProductStatus
{
    [JsonStringEnumMemberName("available")]    Available,
    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("discontinued")] Discontinued
}
```

```csharp
[HttpGet("/products")]
public IActionResult Search([FromQuery] ProductStatus status) => Ok(status);
```

| Requête | Résultat |
|---|---|
| `GET /products?status=out_of_stock` | ✅ `ProductStatus.OutOfStock` |
| `GET /products?status=available` | ✅ `ProductStatus.Available` |
| `GET /products?status=OutOfStock` | ❌ 400 — le nom C# d'un membre annoté n'est pas un nom public |
| `GET /products?status=OUT_OF_STOCK` | ❌ 400 — un nom déclaré est comparé en respectant la casse |
| `GET /products?status=1` | ❌ 400 — une valeur numérique n'est jamais acceptée |
| `GET /products?status=999` | ❌ 400 |
| `GET /products?status=` | ❌ 400 — voir [valeurs vides et absentes](#valeurs-vides-et-absentes) |
| `GET /products` (aucune valeur) | ⚠️ 200 `Available` — voir [valeurs vides et absentes](#valeurs-vides-et-absentes) |

## Une énumération partiellement annotée — refusée

Un attribut **remplace** le nom d'un membre, il n'ajoute pas un alias. Un membre laissé sans
annotation répond donc à son nom C#, et cet identifiant interne devient silencieusement une partie de
votre contrat public — l'exact opposé de la raison pour laquelle vous avez déclaré un contrat. Oublier
un membre est une erreur, pas un choix.

<!-- emn:allow=EMN0003 -->
```csharp
public enum Shipping
{
    [JsonStringEnumMemberName("express")] Express,
    Standard                                        // EMN0003 : erreur de compilation
}
```

Cela échoue deux fois, aussi tôt que possible : l'analyseur signale **`EMN0003` à la compilation**, et
si l'énumération atteint quand même l'exécution sans être annotée — depuis un assembly compilé sans
l'analyseur, par exemple — l'enregistrement lève `EnumContractException` au démarrage plutôt que de
servir un contrat qui fuit.

Si l'énumération ne vous appartient pas, activez-le explicitement :

<!-- emn:skip -->
```csharp
.AddEnumMemberNameBinding(options => options.AllowPartialContracts = true);
```

Le comportement correspond alors exactement à celui de `System.Text.Json` :

| Requête | Résultat |
|---|---|
| `GET /orders?mode=express` | ✅ `Shipping.Express` |
| `GET /orders?mode=Express` | ❌ 400 — annoté, donc seul `express` est public |
| `GET /orders?mode=Standard` | ✅ `Shipping.Standard` — pas d'attribut, le nom C# est le nom public |
| `GET /orders?mode=standard` | ✅ `Shipping.Standard` — un nom non annoté est comparé sans tenir compte de la casse |

`AllowPartialContracts` régit un contrat **incomplet**, pas l'absence de contrat. Une énumération qui
ne porte aucun attribut est ignorée par le scan, et la nommer dans `AddEnum<T>()` est refusé :
l'adopter changerait la façon dont une énumération ordinaire est liée et sérialisée.

## Une énumération `[Flags]`

```csharp
[Flags]
public enum Permissions
{
    [JsonStringEnumMemberName("read")]   Read   = 1,
    [JsonStringEnumMemberName("write")]  Write  = 2,
    [JsonStringEnumMemberName("delete")] Delete = 4
}
```

| Requête | Résultat |
|---|---|
| `GET /tokens?perms=read` | ✅ `Read` |
| `GET /tokens?perms=read, write` | ✅ `Read \| Write` |
| `GET /tokens?perms=read,write` | ✅ idem — l'espace est facultatif |
| `GET /tokens?perms=%20read,%20write%20` | ✅ — la valeur et chaque élément sont détourés |
| `GET /tokens?perms=read,` | ✅ — une virgule finale est tolérée |
| `GET /tokens?perms=,read` | ❌ 400 — une virgule en tête ou répétée ne l'est pas |
| `GET /tokens?perms=read, bogus` | ❌ 400 — un seul membre inconnu rejette toute la valeur |
| `GET /tokens?perms=Read` | ❌ 400 |

Ces règles d'espaces et de virgules ne sont pas un choix fait ici — elles ont été mesurées face à
`System.Text.Json` puis reproduites, jusqu'à la virgule finale. Il en va de même pour une énumération
simple : `?status=%20available%20` est accepté, parce que le corps accepte `" available "`.

## Valeurs vides et absentes

| Paramètre | Requête | Résultat |
|---|---|---|
| `ProductStatus` | `?status=out_of_stock` | ✅ `OutOfStock` |
| `ProductStatus` | `?status=` | ❌ 400 |
| `ProductStatus` | aucune valeur | ⚠️ 200, lie `Available` — le premier membre |
| `ProductStatus?` | `?status=` | ⚠️ 200, lie `null` |
| `ProductStatus?` | aucune valeur | ✅ 200, lie `null` |
| `[FromHeader] ProductStatus` | `X-Status:` vide | ❌ 400 |

Deux lignes méritent l'attention, et **aucune des deux n'est introduite par ce paquet** :

- **Une valeur absente sur un paramètre non nullable lie le premier membre au lieu d'échouer.** C'est
  le comportement natif d'ASP.NET Core pour les types valeur ; un test vérifie qu'une énumération que
  ce paquet ne touche jamais se comporte de façon identique. Utilisez `ProductStatus?` ou `[Required]`
  si vous voulez un 400.
- **Une valeur vide sur un paramètre nullable lie `null`,** là où `System.Text.Json` rejette `""`.
  ASP.NET Core la traite comme une valeur absente avant que le moindre `TypeConverter` ne soit
  consulté ; c'est donc hors de portée d'ici.

Les deux sont couvertes par des tests, afin qu'elles restent visibles plutôt que de devenir du
folklore.

## Quels noms peuvent voyager

Toutes les chaînes que `System.Text.Json` accepte ne survivent pas à tous les canaux. Mesuré, canal
par canal :

| Dans un nom | route | requête | en-tête | formulaire | corps |
|---|:---:|:---:|:---:|:---:|:---:|
| `/` | **400** | ✅ | ✅ | ✅ | ✅ |
| CR, LF | ✅ | ✅ | **400** | ✅ | ✅ |
| hors ASCII imprimable | ✅ | ✅ | **refusé par le client** | ✅ | ✅ |
| `?` `#` `&` `=` `+` `%` espace tabulation `\` `"` | ✅ | ✅ | ✅ | ✅ | ✅ |

[`EMN0006`](rules/EMN0006.fr.md) signale les trois premiers à la compilation, en avertissement — que
cela compte ou non dépend des canaux depuis lesquels votre API lie réellement.

## Configuration

Par défaut, l'assembly d'entrée est scanné et chaque énumération portant l'attribut est enregistrée.

```csharp
builder.Services
       .AddControllers()
       .AddEnumMemberNameBinding(options =>
       {
           options.ScanAssemblyContaining<ProductStatus>();  // scanner un autre assembly
           options.AddEnum<ProductStatus>();                 // ou enregistrer explicitement
           options.AllowPartialContracts = false;            // défaut
           options.ConfigureJsonSerialization = true;        // défaut
       });
```

`ConfigureJsonSerialization` enregistre un `JsonStringEnumConverter<T>` **par énumération sous
contrat**, à la fois dans les options MVC et dans celles de `Http.Json`. La fabrique globale
`JsonStringEnumConverter` n'est jamais installée : les énumérations hors de votre contrat conservent
leur représentation existante. Mettez-le à `false` si vous configurez `System.Text.Json` vous-même.

## Vérifié, pas déclaré

Hormis les deux lignes signalées plus haut, rien de tout cela n'est affirmé à la main. La suite de
tests fait passer chaque entrée candidate à la fois par `JsonSerializer` et par une vraie requête
HTTP, et exige que les deux résultats soient identiques. Si .NET change ses règles de correspondance,
le build casse.
