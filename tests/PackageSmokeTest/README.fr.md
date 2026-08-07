# Test de fumée du paquet

🌍 **Langues :**  
🇬🇧 [English](./README.md) | 🇫🇷 Français (ce fichier)

Tout ce qui vit sous `tests/` atteint la bibliothèque par une `ProjectReference`. Cela prouve que le
code fonctionne. Cela ne prouve pas que le *paquet* fonctionne, et ce sont deux affirmations
différentes : une `ProjectReference` n'exerce jamais la référence de framework, l'emplacement des
analyseurs dans le `.nupkg`, la disposition de `lib/`, les assets MSBuild, ni la capacité de quelqu'un
ayant ses propres réglages de projet à compiler face à tout cela.

Ce répertoire part de `dotnet pack` et va jusqu'à une réponse HTTP :

```
source → pack → restauration NuGet → compilation du consommateur → analyseur du consommateur → Kestrel → requête
```

Lancez-le avec `tests/PackageSmokeTest/run.sh`. Il est câblé dans les deux workflows.

## Pourquoi ce sont des applications et non des tests

`AddEnumMemberNameBinding()` sans options — l'appel que le README met en avant — scanne
`Assembly.GetEntryAssembly()`. Sous un host de test, c'est `testhost.dll` : aucun test xUnit de ce
dépôt ne peut donc atteindre ce chemin de code, et les suites existantes passent toutes des types
explicites à la place. Seul un vrai processus entré par son propre `Main` l'exerce, et c'est pourquoi
`Consumer` est une application.

## Ce qui se trouve ici

| | |
|---|---|
| `Consumer/` | Une transcription du README : enregistrement sans options, une énumération contractuelle, une énumération sans contrat, et le compagnon OpenAPI. Référencé par paquet, jamais par projet. |
| `InvalidContract/` | **Censé ne pas compiler.** Un contrat partiel, pour qu'`EMN0003` doive venir de l'analyseur contenu dans le `.nupkg`. |
| `Directory.Build.props` | Vide, et porteur — il empêche les fixtures d'hériter des réglages de build du dépôt, afin qu'elles ressemblent au projet d'un inconnu. |
| `NuGet.config` | `<clear />` plus le feed local de l'exécution, pour que rien d'autre ne puisse servir le paquet testé. |
| `.work/` | Généré : le feed, un répertoire de paquets NuGet isolé, et les journaux. Supprimé au début de chaque exécution. |

## Les trois choses qui rendraient ceci mensonger

Un test de fumée qui cesse discrètement de tester les bits courants est pire que pas de test : la
fraîcheur repose donc sur trois garanties indépendantes. `.work/` est supprimé à chaque exécution ;
`NUGET_PACKAGES` pointe à l'intérieur, si bien que le cache global de la machine ne peut pas servir
une extraction périmée ; et le paquet est packé en `0.0.0-smoke`, une version jamais publiée et qui ne
peut donc pas venir de nuget.org. L'exécution vérifie ensuite que la version a bien été résolue depuis
le feed local, au lieu de le supposer.

L'assertion sur `EMN0003` est délibérément positive — le build doit échouer, *et* l'échec doit nommer
cette règle. « Aucun diagnostic n'est apparu » est aussi ce qu'on obtient quand l'analyseur n'a jamais
été chargé : une vérification écrite ainsi passerait au vert précisément au moment où le packaging
casserait.
