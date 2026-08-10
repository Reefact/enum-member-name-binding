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

ASP.NET Core formate une valeur de route avec le `ToString()` de la valeur elle-même : un lien
construit à partir de l'énumération porte donc le nom C# — et cette API même y répond 400 :

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
avant qu'aucune analyse ne soit atteinte ; c'est donc hors de portée d'ici. Voir
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

L'`EnumTypeModelBinder` d'ASP.NET Core refuse de lier une valeur non déclarée, quel que soit le
convertisseur qui l'a produite, et `[Flags]` n'est pas une dispense : `Enum.IsDefined` ne sait pas
répondre pour une combinaison, il compare donc le texte de la valeur à son nombre sous-jacent et
refuse celle qui renvoie le nombre. `read,write` se lie parce que `1 | 2` se décompose en
`Read, Write`, non parce que l'attribut lèverait le contrôle. Ce n'est pas atteignable d'ici et,
surtout, le corriger serait une erreur : une énumération que ce paquet ne touche jamais est refusée
de la même façon, donc une énumération sous contrat qui accepterait `3` sur une query string serait
*plus* permissive qu'une ordinaire. La suite de parité épingle les deux moitiés, témoin compris.

La moitié `[Flags]` a une conséquence qui mérite d'être nommée. Une énumération dont les membres
déclarés sont des composites qui se recouvrent peut produire, par OU, une valeur ne se décomposant
en aucun d'eux — `3 | 6` vaut `7` sur une énumération ne déclarant que `3` et `6` — et cette valeur
est refusée hors du corps, exactement comme une combinaison non déclarée sur une énumération
ordinaire. Le motif OpenAPI l'ignore : il décrit toute liste de noms déclarés séparés par des
virgules, si bien que pour cette forme-là seule, le document promet une combinaison à laquelle le
serveur répond 400. Déclarer les bits individuels comme membres, plutôt que des composites qui se
recouvrent uniquement, l'évite.

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

## Le binder n'écrit aucun des enregistrements de model binding d'ASP.NET Core

`SimpleTypeModelBinder` reçoit un `ILoggerFactory` et journalise sa propre tentative et son résultat.
Le binder installé par ce paquet ne prend aucun logger : en `Debug`, un paramètre d'énumération sous
contrat est donc muet là où tous les autres paramètres ne le sont pas :

```text
# une énumération ordinaire, liée par ASP.NET Core
…ModelBinding.ParameterBinder                :: Attempting to bind parameter 'value' …
…ModelBinding.Binders.SimpleTypeModelBinder  :: Attempting to bind parameter 'value' …
…ModelBinding.Binders.SimpleTypeModelBinder  :: Done attempting to bind parameter 'value'.
…ModelBinding.ParameterBinder                :: Done attempting to bind parameter 'value'.

# une énumération sous contrat, liée par ce paquet — les deux lignes du milieu manquent
…ModelBinding.ParameterBinder                :: Attempting to bind parameter 'value' …
…ModelBinding.ParameterBinder                :: Done attempting to bind parameter 'value'.
```

Seuls les enregistrements du binder lui-même manquent. Le trace de `ParameterBinder` qui l'entoure
appartient à ASP.NET Core et reste intact : un journal montre donc toujours que le paramètre a été lié
puis validé — et un échec reste intact lui aussi, atteignant `ModelState` et la réponse exactement
comme n'importe quel autre.

Une limite, et non une décision remise à plus tard : ces enregistrements passent par
`MvcCoreLoggerExtensions`, qui est `internal` à `Microsoft.AspNetCore.Mvc.Core`. Ce qu'on pourrait
écrire à la place serait un sosie sous la catégorie et les identifiants d'événement de ce paquet —
qu'un filtre de journalisation visant ceux d'ASP.NET Core ne ramasserait pas. Une parité d'apparence,
et aucune en fait.

## Incompatible avec le trimming et Native AOT

Résoudre un contrat et scanner un assembly reposent sur la réflexion. Chaque point d'entrée est annoté
plutôt que de voir ses avertissements supprimés : un consommateur qui compile pour l'un ou l'autre
obtient ainsi un avertissement exact au lieu d'un échec silencieux à l'exécution.

Les deux contraintes sont appliquées séparément, car ce ne sont pas les mêmes : lire les métadonnées
d'une énumération exige de la réflexion mais ne génère aucun code. `GetPublicNames`, `IsFlagsContract`
et tout le paquet OpenAPI portent donc `[RequiresUnreferencedCode]` seul ; `GetPublicName`, le chemin
de formatage `[Flags]`, la construction des convertisseurs JSON génériques — et donc
`AddEnumMemberNameBinding` lui-même, qui les atteint — portent les deux.

## Appelez-le au démarrage

L'enregistrement configure le conteneur : il a donc sa place avant `WebApplicationBuilder.Build()`.
Passé ce point, la collection de services est en lecture seule et l'appel lève — sans avoir rien
enregistré, et c'est tout l'enjeu : un appel qui échoue ne laisse jamais l'application liant une
énumération qu'il a nommée. Plus tard encore, ASP.NET Core a mis en cache le model binder construit
pour un type à la première utilisation : un paramètre déjà lié ne verrait donc pas un nouvel
enregistrement, même s'il était possible d'en faire un.
