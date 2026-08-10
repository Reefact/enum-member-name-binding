# Limitations

🌍 **Langues :**  
🇬🇧 [English](./limitations.en.md) | 🇫🇷 Français (ce fichier)

Tout ce qui suit est mesuré et épinglé par un test, afin qu'une limitation reste visible plutôt que
de devenir du folklore.

## Les paramètres des Minimal APIs ne sont pas pris en charge

Leur liaison n'utilise ni les model binders de MVC ni `TypeDescriptor` : elle exige un
`static TryParse` ou un `BindAsync` sur le type lié, ce qu'on ne peut pas ajouter à une `enum`. Aucun
paquet tiers ne peut combler cela sans abandonner `enum`. Si vous en avez besoin aujourd'hui,
enveloppez l'énumération dans un `readonly record struct` implémentant `IParsable<T>`.

Les *réponses* des Minimal APIs, elles, sont couvertes : un endpoint qui retourne une énumération sous
contrat écrit son nom public, parce que le paquet principal configure `Http.Json.JsonOptions` en plus
des options MVC. C'est le côté entrée qui est hors de portée.

## La génération de liens n'utilise pas le nom public

ASP.NET Core formate les valeurs de route sans consulter `TypeDescriptor` : un lien construit à partir
de la valeur d'énumération elle-même porte le nom C# — et cette API même y répond 400 :

```csharp
// /products/OutOfStock  →  400
links.GetPathByAction(context, "ByStatus", "Products", new { status = ProductStatus.OutOfStock });

// /products/out_of_stock
links.GetPathByAction(context, "ByStatus", "Products",
                      new { status = EnumMemberNames.GetPublicName(ProductStatus.OutOfStock) });
```

`EnumMemberNames.GetPublicName` rend également une combinaison `[Flags]` sous forme de liste séparée
par des virgules, et retourne `null` pour une énumération qui ne déclare aucun contrat. Les deux
formes sont couvertes par des tests, y compris le 400 sur la première.

## Une valeur vide sur un paramètre d'énumération nullable lie `null`

Là où `System.Text.Json` rejette `""`, ASP.NET Core résout une valeur vide comme une valeur absente
avant que le moindre `TypeConverter` ne soit consulté ; c'est donc hors de portée d'ici. Voir
[valeurs vides et absentes](contract-rules.fr.md#valeurs-vides-et-absentes), qui couvre aussi l'autre
ligne à connaître : une valeur absente sur un paramètre **non nullable** lie le premier membre au lieu
d'échouer. Aucun de ces deux comportements n'est introduit par ce paquet — un test vérifie qu'une
énumération qu'il ne touche jamais se comporte de façon identique.

## Une combinaison qui ne nomme aucun membre est refusée hors du corps

Une virgule sépare les valeurs sur toutes les énumérations : `System.Text.Json` lit donc
`"out_of_stock,discontinued"` comme `1 | 2` et renvoie `(ProductStatus)3`, une valeur qu'aucun membre
ne déclare. Tous les autres canaux répondent 400 à la même entrée :

```csharp
// corps   {"Value":"out_of_stock,discontinued"}  →  200, (ProductStatus)3
// requête ?value=out_of_stock,discontinued       →  400
```

L'`EnumTypeModelBinder` d'ASP.NET Core refuse de lier une valeur non déclarée à une énumération sans
`[Flags]`, quel que soit le convertisseur qui l'a produite — les `[Flags]` en sont dispensées, ce qui
explique que `read,write` se lie. Ce n'est pas atteignable d'ici et, surtout, le corriger serait une
erreur : une énumération que ce paquet ne touche jamais est refusée de la même façon, donc une
énumération sous contrat qui accepterait `3` sur une query string serait *plus* permissive qu'une
ordinaire. La suite de parité épingle les deux moitiés, témoin compris.

Les combinaisons qui nomment bien un membre sont acceptées, sur tous les canaux — voir
[règles de contrat](contract-rules.fr.md#une-virgule-sépare-les-valeurs-sur-toutes-les-énumérations).

## Tous les noms ne voyagent pas sur tous les canaux

Une barre oblique ne peut pas traverser un segment de route, et un saut de ligne ou un caractère hors
ASCII imprimable ne peut pas traverser un en-tête. [`EMN0006`](rules/EMN0006.fr.md) le signale à la
compilation ; la mesure est dans
[règles du contrat](contract-rules.fr.md#quels-noms-peuvent-voyager).

## OpenAPI nécessite le paquet compagnon

ASP.NET Core a clos la demande correspondante en *not planned*
([dotnet/aspnetcore#68065](https://github.com/dotnet/aspnetcore/issues/68065)), et .NET 11 se mettra à
annoncer les noms C# pour les paramètres hors corps — cet écart devrait donc se creuser, pas se
réduire. Voir [OpenAPI](openapi.fr.md).

## Incompatible avec le trimming et Native AOT

`TypeDescriptor` et le scan d'assembly reposent sur la réflexion. Chaque point d'entrée est annoté
plutôt que de voir ses avertissements supprimés : un consommateur qui compile pour l'un ou l'autre
obtient ainsi un avertissement exact au lieu d'un échec silencieux à l'exécution.

Les deux contraintes sont appliquées séparément, car ce ne sont pas les mêmes : lire les métadonnées
d'une énumération exige de la réflexion mais ne génère aucun code. `GetPublicNames`, `IsFlagsContract`,
l'enregistrement MVC et tout le paquet OpenAPI portent donc `[RequiresUnreferencedCode]` seul ;
`GetPublicName`, le chemin de formatage `[Flags]` et la construction des convertisseurs JSON
génériques portent les deux.

## Appelez-le au démarrage

ASP.NET Core met en cache le model binder qu'il construit pour un type à la première utilisation : un
enregistrement effectué après la première requête n'a donc aucun effet.
