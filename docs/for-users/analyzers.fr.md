# Analyseurs

🌍 **Langues :**  
🇬🇧 [English](./analyzers.en.md) | 🇫🇷 Français (ce fichier)

Le paquet livre des analyseurs Roslyn — aucune installation supplémentaire. Une erreur de contrat est
une erreur de compilation dans votre éditeur, pas une exception découverte au démarrage de
l'application.

| ID | Sévérité | Signalé quand |
|---|---|---|
| [`EMN0001`](rules/EMN0001.fr.md) | Erreur | Deux membres déclarent le même nom public |
| [`EMN0002`](rules/EMN0002.fr.md) | Erreur | Un nom public est vide, ou entouré d'espaces |
| [`EMN0003`](rules/EMN0003.fr.md) | Erreur | Une énumération sous contrat laisse des membres sans annotation |
| [`EMN0004`](rules/EMN0004.fr.md) | Erreur | Un nom public sur une énumération `[Flags]` contient une virgule |
| [`EMN0005`](rules/EMN0005.fr.md) | Erreur | Un nom public est aussi le nom C# d'un autre membre |
| [`EMN0006`](rules/EMN0006.fr.md) | Avertissement | Un nom public ne peut pas voyager sur tous les canaux d'entrée |

**Une énumération ne portant aucun `[JsonStringEnumMemberName]` n'est jamais analysée.** Les règles ne
s'appliquent qu'une fois un contrat déclaré : ajouter ce paquet à une solution existante n'allume donc
pas les énumérations avec lesquelles il n'a rien à voir.

## EMN0005, celle qui mérite deux lectures

<!-- emn:allow=EMN0003,EMN0005 -->
```csharp
public enum Colour
{
    [JsonStringEnumMemberName("Blue")] Red,   // EMN0005
    Blue
}
```

Un nom public déclaré est comparé en premier et en respectant la casse, tandis que le nom C# d'un
membre non annoté est comparé sans tenir compte de la casse. Ainsi `?colour=Blue` lie **`Red`**, alors
que `?colour=blue` et `?colour=BLUE` lient `Blue`. Le membre répond à toutes les casses de son nom
sauf la sienne — ce qu'aucun lecteur de cette énumération ne devinerait.

La casse du nom déclaré n'y change rien, donc la règle ignore la casse :
`[JsonStringEnumMemberName("blue")]` à côté d'un membre `Blue` est l'image miroir du même piège et est
signalé aussi.

Elle ne se déclenche qu'aux côtés d'`EMN0003`, puisqu'elle a besoin d'un membre non annoté. C'est
précisément pourquoi elle est une règle à part : désactivez `EMN0003` pour autoriser les contrats
partiels et `EMN0005` devient la seule protection restante — d'où son statut d'erreur plutôt que
d'avertissement. Et contrairement à `EMN0003`, elle reste appliquée au démarrage même lorsque
`AllowPartialContracts` est activé.

## Pourquoi EMN0006 n'est qu'un avertissement

Contrairement aux autres, il ne s'agit pas d'une ambiguïté : le contrat est parfaitement défini, il ne
peut simplement pas traverser un canal particulier. Que cela compte ou non dépend des canaux depuis
lesquels votre API lie. Un nom public français est un choix parfaitement valable pour une API qui ne
lit jamais d'en-tête. Le message nomme le caractère et le canal qui le refuse, afin que la décision
puisse se prendre nom par nom.

## Configurer les sévérités

Les analyseurs ne voient pas votre configuration d'exécution : si vous utilisez délibérément
`AllowPartialContracts`, désactivez `EMN0003` — et gardez `EMN0005` :

```ini
[*.cs]
dotnet_diagnostic.EMN0003.severity = none
```

## Au démarrage aussi

Toutes les règles sauf `EMN0006` sont également appliquées au démarrage de l'application, pour les
énumérations qui atteignent l'exécution depuis un assembly compilé sans les analyseurs.
`EnumContractException` nomme alors le type, chaque problème et la correction attendue.

Deux membres peuvent partager la même valeur numérique tant que leurs noms publics diffèrent.
Réécrire cette valeur — dans un corps de réponse, ou via `EnumMemberNames.GetPublicName` — produit le
nom que `System.Text.Json` écrit pour elle, qui n'est pas nécessairement le premier déclaré.
