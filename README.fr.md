# AspNetCore.EnumMemberNameBinding

🌍 **Langues :**  
🇬🇧 [English](README.md) | 🇫🇷 Français (ce fichier)

|  |  |
| :-- | :-- |
| **Build** | [![ci](https://github.com/Reefact/enum-member-name-binding/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Reefact/enum-member-name-binding/actions/workflows/ci.yml) |
| **Qualité** | [![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=reefact_enum-member-name-binding&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=reefact_enum-member-name-binding) [![Couverture](https://sonarcloud.io/api/project_badges/measure?project=reefact_enum-member-name-binding&metric=coverage)](https://sonarcloud.io/summary/new_code?id=reefact_enum-member-name-binding) |
| **Sécurité** | [![codeql](https://github.com/Reefact/enum-member-name-binding/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/Reefact/enum-member-name-binding/actions/workflows/codeql.yml) [![OpenSSF Best Practices](https://www.bestpractices.dev/projects/14000/badge)](https://www.bestpractices.dev/projects/14000) [![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/Reefact/enum-member-name-binding/badge)](https://securityscorecards.dev/viewer/?uri=github.com/Reefact/enum-member-name-binding) |
| **Paquets** | [![NuGet](https://img.shields.io/nuget/vpre/AspNetCore.EnumMemberNameBinding?logo=nuget&label=EnumMemberNameBinding)](https://www.nuget.org/packages/AspNetCore.EnumMemberNameBinding) [![NuGet](https://img.shields.io/nuget/vpre/AspNetCore.EnumMemberNameBinding.OpenApi?logo=nuget&label=EnumMemberNameBinding.OpenApi)](https://www.nuget.org/packages/AspNetCore.EnumMemberNameBinding.OpenApi) ![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4) |
| **Projet** | [![License](https://img.shields.io/github/license/Reefact/enum-member-name-binding)](LICENSE) [![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-fe5196?logo=conventionalcommits&logoColor=white)](https://www.conventionalcommits.org) |

---

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

Requiert ASP.NET Core MVC (contrôleurs), sur une version de .NET prise en charge :

| Paquet | .NET |
|---|:--:|
| 1.x | 10 |

Prenez la dernière version — NuGet retient le framework cible correspondant à votre projet. La
version du paquet décrit la surface publique de cette bibliothèque : une nouvelle version de .NET
ajoute une ligne à ce tableau plutôt que de faire bouger le majeur.

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
| Document OpenAPI | ✅ avec le [paquet compagnon](./docs/for-users/openapi.fr.md) |
| Réponses des Minimal APIs | ✅ |
| Paramètres des Minimal APIs | ❌ — [contrainte de la plateforme](./docs/for-users/limitations.fr.md#les-paramètres-des-minimal-apis-ne-sont-pas-pris-en-charge) |

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
l'enregistrement doit avoir lieu au démarrage — est dans [limitations](./docs/for-users/limitations.fr.md).

## Documentation

- [Règles du contrat](./docs/for-users/contract-rules.fr.md) — ce qui est accepté, requête par requête :
  énumérations totalement et partiellement annotées, `[Flags]`, valeurs vides et absentes, quels noms
  peuvent voyager sur quel canal, et les options de configuration.
- [Analyseurs](./docs/for-users/analyzers.fr.md) — `EMN0001` à `EMN0006`, pourquoi `EMN0005` mérite deux lectures,
  et comment configurer les sévérités.
- [OpenAPI](./docs/for-users/openapi.fr.md) — le paquet compagnon, le motif `[Flags]`, et comment la cohérence
  document/exécution est vérifiée.
- [Limitations](./docs/for-users/limitations.fr.md)
- [Changelog](CHANGELOG.md)

## Relation avec `Reefact.JsonEnumValueBinding`

Ce paquet remplace `Reefact.JsonEnumValueBinding`, antérieur à .NET 9, qui portait son propre attribut
`[JsonEnumValue]` et son propre convertisseur JSON. Les deux sont désormais redondants : la plateforme
possède l'attribut et la sérialisation. La migration se résume à un renommage d'attribut —
`[JsonEnumValue("x")]` devient `[JsonStringEnumMemberName("x")]` — plus le remplacement de
`AddJsonEnumValueBinding()` par `AddEnumMemberNameBinding()`.

## Contribuer et obtenir de l'aide

- [Signaler un bogue ou demander une fonctionnalité](https://github.com/Reefact/enum-member-name-binding/issues)
  — le suivi des tickets. Une vulnérabilité présumée passe plutôt par le canal privé ci-dessous.
- [Contribuer](./docs/for-users/CONTRIBUTING.fr.md)
  — compiler et tester, le style de code, la grammaire des commits, et ce que porte une pull request.
- [Politique de sécurité](./docs/for-users/SECURITY.fr.md)
  — ce qui est dans le périmètre, et comment signaler une vulnérabilité en privé.

## Licence

Apache-2.0.
