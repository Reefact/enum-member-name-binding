# AspNetCore.EnumMemberNameBinding

🌍 **Langues :**  
🇬🇧 [English](../README.md) | 🇫🇷 Français (ce fichier)

**Un seul contrat d'énumération, honoré sur tous les canaux d'entrée — pas seulement dans le corps de
la requête.**

Depuis .NET 9, `System.Text.Json` permet de donner à un membre d'énumération un nom public explicite :

```csharp
using System.Text.Json.Serialization;

public enum ProductStatus
{
    [JsonStringEnumMemberName("available")]    Available,
    [JsonStringEnumMemberName("out_of_stock")] OutOfStock,
    [JsonStringEnumMemberName("discontinued")] Discontinued
}
```

Ce nom est honoré dans le **corps de la requête**, et nulle part ailleurs. ASP.NET Core lie les
valeurs de route, les chaînes de requête, les champs de formulaire et les en-têtes via
`System.ComponentModel`, qui n'a jamais entendu parler de `System.Text.Json`. La même API répond donc :

```
POST /products   {"status":"out_of_stock"}   → 200
GET  /products?status=out_of_stock           → 400
GET  /products?status=OutOfStock             → 200   ← votre nom C# interne, désormais dans votre contrat public
```

Ce paquet comble cet écart.

## Installation

```
dotnet add package AspNetCore.EnumMemberNameBinding
```

Requiert **.NET 10** et ASP.NET Core MVC (contrôleurs).

## Utilisation

Une ligne, au démarrage :

```csharp
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddControllers()
       .AddEnumMemberNameBinding();

var app = builder.Build();
app.MapControllers();
app.Run();
```

C'est tout. Rien à annoter au-delà des attributs `[JsonStringEnumMemberName]` que vous avez déjà.

```csharp
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("products")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet("{status}")]
    public IActionResult ByStatus([FromRoute] ProductStatus status) => Ok(status);

    [HttpGet]
    public IActionResult Search([FromQuery] ProductStatus? status) => Ok(status);
}
```

```
GET /products/out_of_stock        → 200
GET /products?status=out_of_stock → 200
GET /products?status=OutOfStock   → 400   { "errors": { "status": ["The value 'OutOfStock' is not valid."] } }
```

## Canaux couverts

| Canal | Couvert |
|---|:--:|
| Valeurs de route | ✅ |
| Chaînes de requête | ✅ |
| Champs de formulaire | ✅ |
| En-têtes (`[FromHeader]`) | ✅ |
| Énumérations nullables (`TEnum?`) | ✅ |
| Corps de la requête | ✅ (par `System.Text.Json`) |
| Document OpenAPI | ✅ avec le [paquet compagnon](./openapi.fr.md) |
| Réponses des Minimal APIs | ✅ |
| Paramètres des Minimal APIs | ❌ — [contrainte de la plateforme](./limitations.fr.md#les-paramètres-des-minimal-apis-ne-sont-pas-pris-en-charge) |

## Garanties

- **Le même vocabulaire partout.** Les règles de correspondance ne sont pas inventées ici : elles
  portent celles que `System.Text.Json` applique au corps de la requête — jusqu'aux espaces et à la
  virgule finale d'une liste `[Flags]`. Chaque règle a été mesurée face à `JsonSerializer`, jamais
  lue dans une spécification.
- **Vérifié, pas déclaré.** La suite de tests fait passer chaque entrée candidate à la fois par
  `JsonSerializer` et par une vraie requête HTTP, et exige que les deux résultats soient identiques.
  Si .NET change ses règles de correspondance, le build casse.
- **Rien d'autre ne change.** Une énumération qui ne porte aucun `[JsonStringEnumMemberName]` est
  **laissée totalement intacte** : même liaison, même validation, même format JSON qu'en l'absence du
  paquet. La fabrique globale `JsonStringEnumConverter` n'est jamais installée — un convertisseur est
  enregistré par énumération sous contrat.
- **Les erreurs sont des erreurs de compilation.** Des analyseurs Roslyn sont livrés dans le paquet,
  sans installation supplémentaire : un nom public en double, un contrat incomplet ou un nom qui
  masque le nom C# d'un autre membre sont signalés dans votre éditeur, pas découverts au démarrage.
  Les énumérations qui ne déclarent aucun contrat ne sont jamais analysées.
- **La validation est préservée.** Une valeur inconnue ou numérique donne un 400, exactement comme le
  corps la refuserait.

## Limitations

Les deux à connaître avant d'adopter le paquet :

- **Les paramètres des Minimal APIs ne sont pas pris en charge.** Leur liaison exige un
  `static TryParse` ou un `BindAsync` sur le type lié, ce qu'on ne peut pas ajouter à une `enum` —
  c'est une contrainte de la plateforme, pas un manque d'implémentation. Les réponses, *elles*, sont
  couvertes.
- **La génération de liens n'utilise pas le nom public.** ASP.NET Core formate les valeurs de route
  sans consulter `TypeDescriptor` : un lien construit à partir de la valeur d'énumération porte le nom
  C#, et cette API même y répond 400. `EnumMemberNames.GetPublicName(value)` est le contournement.

La liste complète — valeurs vides, portabilité par canal, trimming et Native AOT, et pourquoi
l'enregistrement doit avoir lieu au démarrage — est dans [limitations](./limitations.fr.md).

## Documentation

- [Règles du contrat](./contract-rules.fr.md) — ce qui est accepté, requête par requête :
  énumérations totalement et partiellement annotées, `[Flags]`, valeurs vides et absentes, quels noms
  peuvent voyager sur quel canal, et les options de configuration.
- [Analyseurs](./analyzers.fr.md) — `EMN0001` à `EMN0006`, pourquoi `EMN0005` mérite deux lectures,
  et comment configurer les sévérités.
- [OpenAPI](./openapi.fr.md) — le paquet compagnon, le motif `[Flags]`, et comment la cohérence
  document/exécution est vérifiée.
- [Limitations](./limitations.fr.md)
- [Changelog](../CHANGELOG.md)

## Relation avec `Reefact.JsonEnumValueBinding`

Ce paquet remplace `Reefact.JsonEnumValueBinding`, antérieur à .NET 9, qui portait son propre attribut
`[JsonEnumValue]` et son propre convertisseur JSON. Les deux sont désormais redondants : la plateforme
possède l'attribut et la sérialisation. La migration se résume à un renommage d'attribut —
`[JsonEnumValue("x")]` devient `[JsonStringEnumMemberName("x")]` — plus le remplacement de
`AddJsonEnumValueBinding()` par `AddEnumMemberNameBinding()`.

## Licence

Apache-2.0.
