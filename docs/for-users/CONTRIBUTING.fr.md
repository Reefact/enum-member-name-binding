# Contribuer

🌍 **Langues :**  
🇬🇧 [English](../../CONTRIBUTING.md) | 🇫🇷 Français (ce fichier)

Merci d'envisager une contribution. Ce qui suit décrit surtout ce que le build impose déjà, afin
que rien ici ne soit une surprise au moment de la revue : presque chaque règle ci-dessous est
vérifiée par un test, un workflow ou une ruleset du dépôt plutôt que par la bonne volonté du
lecteur.

## Installation

Le plancher SDK est déclaré dans `global.json` ; toute version plus récente est acceptée.

```sh
git clone https://github.com/Reefact/enum-member-name-binding.git
cd enum-member-name-binding
git config core.hooksPath .githooks
dotnet build -c Release
```

La ligne `core.hooksPath` active deux hooks : `commit-msg`, qui vérifie un message avant qu'il ne
soit enregistré, et `pre-commit`, qui vérifie le C# indexé au regard de la règle de style
ci-dessous. Un hook ne peut pas s'installer lui-même, d'où cette étape plutôt que de la magie. S'en
passer ne coûte rien au moment du commit, et coûte une réécriture d'historique plus tard, quand la
CI passe les mêmes contrôles.

## Compiler et tester

```sh
dotnet build -c Release          # les avertissements sont des erreurs : un warning casse le build
dotnet test -c Release
tests/PackageSmokeTest/run.sh    # empaquette, publie, exécute et appelle un vrai point de terminaison HTTP
```

Le test de fumée est plus lent que les autres, et vaut l'attente dès que l'empaquetage, les
analyseurs ou un point d'entrée public ont changé. Il part de `dotnet pack` et finit sur une
réponse HTTP : c'est le seul contrôle qui remarquerait un paquet qui compile et ne fonctionne pas.

La CI compile et teste sur deux SDK — le plancher de `global.json` et le dernier 10.0.x — parce
qu'un désaccord entre versions d'analyseurs a sa place ici plutôt que dans le build d'un
consommateur.

## Un changement arrive avec ses tests

Une fonctionnalité majeure arrive avec ses tests dans la suite automatisée, dans la même pull
request. Cette part-là est une politique et non un contrôle, parce que rien ne sait distinguer une
fonctionnalité d'un remaniement — mais ce qui l'entoure, lui, est imposé : un changement de la
surface publique casse le build tant que la baseline committée ne l'enregistre pas, et un exemple de
la documentation qui cesse de compiler échoue comme n'importe quel autre test.

Quand l'ordre peut être choisi, écrivez le test d'abord. Pas comme méthode de conception, et pas du
TDD — l'affirmation est plus étroite que l'un comme l'autre. Un test de régression écrit après le
correctif n'a jamais été vu échouer : rien n'établit donc qu'il couvre le bogue plutôt qu'il ne passe
pour une raison sans rapport. Le voir rougir, puis voir le correctif le faire verdir, est la seule
chose qui l'établisse — c'est l'argument même du job de style, qui lance le test du vérificateur
avant le vérificateur. Un correctif porte donc le test qui l'aurait attrapé, écrit avant le correctif
partout où c'est possible.

Partout où ça ne l'est pas — un test qui ne peut pas exister avant la fonctionnalité — dites-le dans
la pull request plutôt que d'escamoter l'étape.

## Style de code

L'essentiel tient dans `.editorconfig`, que votre éditeur lit déjà. Trois règles qu'il ne sait pas
exprimer, et c'est le même reproche : une ligne qui n'est pas une pensée, que le lecteur doit
reconstituer.

Un `if` dont tout le corps est une seule sortie — `return`, `throw`, `continue`, `break` — s'écrit
sur une ligne, et une suite de gardes forme un bloc, sans ligne vide entre elles :

```csharp
if (string.IsNullOrEmpty(name)) { return Problem.EmptyName(memberName); }
if (isFlags && name.Contains(',')) { return Problem.CommaInFlagsName(memberName, name); }
```

Une déclaration ne coupe pas après le `=` quand sa valeur tient à côté du nom :

```csharp
internal const string Reflection = "Enum member name binding reads enum metadata reflectively…";
```

Et un attribut de suppression s'écrit sur une ligne, quelle que soit sa largeur :

```csharp
[UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = SuppressionJustification.IL2026.RequirementCarriedByConstructor)]
```

La garde et ce qu'elle fait sont une seule pensée ; un nom et sa valeur en sont une autre. Tout ce
qui est moins trivial qu'une sortie nue garde la forme multiligne, et de même une valeur qui a
réellement besoin de plusieurs lignes — une concaténation, un initialiseur long. La largeur est un
plafond pour ces deux règles, 140 caractères pour une garde et 160 pour une valeur, et un plafond
plutôt qu'une dispense : ce qui est trop large est en général ce qui réclame un nom.

Une suppression, elle, n'a pas de plafond, et l'écart est voulu. Un membre en porte souvent
plusieurs, et repliées à la virgule elles s'entrelacent en un paragraphe où deux d'entre elles ne
diffèrent que par un mot quelque part au milieu — les distinguer devient un diff fait à l'œil, c'est
ainsi qu'un doublon survit à ce qu'on le regarde. Les deux autres règles s'arrêtent là où la ligne
unique cesse d'être la plus lisible, parce qu'elles portent de la logique ; une suppression se
parcourt pour savoir quelle règle et se survole pour savoir pourquoi, jamais pour sa logique — la
replier ne fait rien gagner et coûte la comparaison.

`tools/style/lint-layout.sh` signale ce qui n'y répond pas, `--fix` réécrit ce qu'il peut et nomme ce
qu'il ne peut pas — en sortant non nul pour cela, afin qu'une exécution ayant laissé une violation
derrière elle ne se lise pas comme un arbre propre — et la CI l'exécute sans l'option, en lançant
d'abord le test du vérificateur, pour que « rien à signaler » veuille dire quelque chose. C'est donc
vérifié plutôt que retenu. Il reste muet sur deux formes qu'il ne sait pas lire : une suppression qui
partage ses crochets avec un autre attribut, comme dans `[Fact, SuppressMessage(...)]`, et une
suppression repliée dont le `)]` de fermeture est suivi d'un membre sur la même ligne. Dans les deux
cas, savoir quoi joindre suppose d'analyser le C#. Un commentaire `//` en fin de ligne n'est ni l'un
ni l'autre : il court jusqu'au bout de la ligne, rien ne peut s'y cacher derrière et la jonction est
donc sûre.

## Branches

Partez de `main`, avec un nom de la forme `<auteur>/<description>` :

```sh
git switch -c jane/flags-enum-pattern main
```

`main` n'accepte ni push direct, ni push forcé, ni commit de merge. Elle n'avance que par pull
request, en squash ou en rebase, pour que son historique reste linéaire.

Le commit de merge est la moitié sur laquelle un contributeur peut trébucher : la CI le refuse donc,
plutôt que de le laisser découvrir au bouton de merge — où GitHub le signale comme un rebase
impossible, après la relecture plutôt qu'avant. Récupérez `main` en rebasant dessus :

```sh
git fetch origin main && git rebase origin/main
```

## Messages de commit

[Conventional Commits](https://www.conventionalcommits.org/fr/v1.0.0/), imposé par
`tools/commit-lint/lint-commit-message.sh` — l'unique script que lancent le hook et la CI, si bien
que les deux ne peuvent pas diverger :

| Règle | Forme |
| --- | --- |
| En-tête | `<type>[(portée)][!] : <description>` |
| Type | `feat`, `fix`, `build`, `chore`, `ci`, `docs`, `perf`, `refactor`, `revert`, `style`, `test` |
| Portée | facultative ; en kebab-case minuscule, nommant un domaine plutôt qu'un fichier |
| Description | à l'impératif et en minuscules, sans point final |
| Longueur | 72 caractères, là où GitHub tronque sa liste de commits |
| Rupture | `!` dans l'en-tête **et** un pied `BREAKING CHANGE:` portant la migration |

Seul l'en-tête est validé. Les corps sont de la prose et sont laissés tranquilles — écrivez-les
pour le lecteur qui demandera pourquoi, dans six mois, plutôt que quoi.

```sh
git log -1 --format=%B | tools/commit-lint/lint-commit-message.sh -
```

## Pull requests

Remplissez le gabarit. Sa liste de contrôle est vérifiée par des tests plutôt que crue sur parole,
et les deux sections ci-dessous sont celles qui piègent. Les fils de revue doivent être résolus
avant le merge, et `CI` est le check requis : un seul job, qui échoue si n'importe quelle branche
de la matrice de build a échoué.

## L'API publique

Les deux projets empaquetables portent une baseline versionnée. Modifier la surface publique casse
le build tant que le même changement n'est pas écrit dans le `PublicAPI.Unshipped.txt` voisin du
projet. C'est le but plutôt qu'une corvée : la surface bouge dans un diff que quelqu'un a relu,
jamais comme effet de bord d'une modification faite pour une autre raison.

## La documentation est bilingue

Chaque page existe en anglais et en français, et la paire est comparée structurellement : même
nombre de titres, de puces et de lignes de tableau, et même séquence de langages de blocs de code.
Ne mettre à jour qu'un côté fait échouer la suite, délibérément — une traduction qui décroche en
silence est pire qu'une traduction absente, parce qu'on y croit encore.

Le README anglais est aussi la page du paquet NuGet, où un lien relatif est mort : ses liens vers
ce dépôt sont donc absolus. Les pages sous `docs/` ne sont lues que sur GitHub et pointent en
relatif.

## Les exemples de la documentation sont compilés

Chaque bloc C# sous `docs/for-users` et dans les deux pages d'accueil est compilé contre les paquets
publiés, puis les analyseurs livrés par cette bibliothèque sont passés sur le résultat. Les pages de
mainteneur en sont exclues, et comme un corps entier plutôt qu'une par une : un enregistrement de
décision cite du C# pour porter un argument, pas pour être collé. Un exemple qui ne compile
plus, ou qui enseigne du code qu'`EMN0005` refuserait, casse le build comme n'importe quel autre
test — la documentation est la seule chose que rien n'exécute, donc rien d'autre ne le verrait.

Les exemples sont des fragments à dessein : une action sans son contrôleur, trois instructions sans
le `Main` autour. L'enrobage dont un fragment a besoin est déduit en l'analysant, il n'y a donc rien
à déclarer. Deux choses le sont, chacune sur la ligne au-dessus du bloc et dans les deux langues :

- `<!-- emn:allow=EMN0003 -->` — l'exemple montre l'erreur volontairement, et la règle doit se
  déclencher. Une autorisation qui ne se déclenche plus échoue aussi, parce que l'exemple a cessé
  d'en être un.
- `<!-- emn:skip -->` — l'exemple n'est pas du code que quiconque pourrait compiler, par exemple un
  fragment de chaîne d'appels montré pour pointer une seule ligne. Il doit vraiment ne pas compiler :
  une exemption devenue inutile échoue également.

## Remontées des analyseurs

Une remontée de Roslyn ou de SonarQube est une affirmation sur le code, et [CLAUDE.md](../../CLAUDE.md)
expose les six façons d'y répondre — de la correction au fait de la laisser visible comme dette
connue. Deux méritent d'être connues avant qu'on y recoure : une suppression porte toujours une
`Justification` disant pourquoi la prémisse de la règle ne tient pas à cet endroit, et une
exclusion dans `.editorconfig` est une décision à soumettre d'abord, car elle fait aussi taire la
règle partout où lui obéir aurait été juste.

## Les décisions qui méritent une trace

Un choix qui survit à la pull request qui l'a pris — une bibliothèque, une convention, un compromis
assumé — s'écrit sous [`docs/for-maintainers/adr`](../for-maintainers/adr), un fichier par décision, dans les deux langues comme
toute autre page. Ce qu'on y consigne, c'est le raisonnement : les solutions qui étaient réellement
en lice à ce moment-là, et ce que la décision coûte, pour qu'un lecteur en désaccord voie ce qu'il
aurait à renverser. La première est [NFluent pour les assertions de test](../for-maintainers/adr/0001-nfluent-for-test-assertions.fr.md).

## Signaler une vulnérabilité

Pas ici. Une vulnérabilité présumée passe par un avis de sécurité privé plutôt que par une issue,
une discussion ou une pull request publique — voir [SECURITY.fr.md](SECURITY.fr.md).
