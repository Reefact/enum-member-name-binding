# ADR 0001 — NFluent pour les assertions de test

🌍 **Langues :**  
🇬🇧 [English](./0001-nfluent-for-test-assertions.en.md) | 🇫🇷 Français (ce fichier)

**Statut :** accepté — 2026-08-10  
**Portée :** les trois projets de test. La bibliothèque elle-même ne prend aucune dépendance de test.

## Contexte

Les assertions étaient écrites avec l'`Assert` de xUnit, 280 appels répartis sur 24 fichiers. Rien
ne clochait ; ce qui leur manque, c'est un ordre de lecture. `Assert.Equal(attendu, obtenu)` place
l'attente d'abord et le sujet ensuite, soit l'inverse de la phrase qu'on prononce et l'inverse de
tous les autres appels du test. `Assert.Contains` échange ses deux arguments selon que le sujet est
une chaîne ou une collection, et les deux surcharges compilent avec les arguments inversés dès que
les types coïncident — un test qui passe alors pour la mauvaise raison.

Ce dépôt traite déjà une ligne qui n'est pas une pensée comme un défaut qui mérite un vérificateur.
Une assertion qui se lit à l'envers, c'est le même reproche un cran plus haut.

## Décision

Les assertions de test s'écrivent avec [NFluent](https://github.com/tpierrain/NFluent) 3.1.0.

xUnit reste : il découvre, exécute et rapporte les tests, et `[Fact]`, `[Theory]` et les fixtures ne
changent pas. Seule l'assertion change.

```csharp
Check.That(bound.Value).IsEqualTo("out_of_stock");
Check.That(exception.Problems).HasOneElementOnly();
Check.ThatCode(() => EnumContract.For(typeof(DuplicateNames))).Throws<EnumContractException>();
```

Le sujet vient d'abord, puis ce qu'on en affirme.

## Solutions écartées

| Option | Pourquoi non |
| --- | --- |
| Garder `Assert` | Aucune dépendance, et l'ordre des arguments reste inversé et inconstant. |
| FluentAssertions | La version 8 est passée à une licence payante pour l'usage commercial. Un changement de licence dans une dépendance de test est un mauvais héritage. |
| AwesomeAssertions | Le fork Apache-2.0 de FluentAssertions 7, et une réponse plausible ; il est jeune, et sa maintenance à long terme reste la question ouverte. |
| Shouldly | Comparable et bien portant. NFluent a la préférence du mainteneur ; rien ici n'écarte Shouldly. |

## Conséquences

Quatre d'entre elles sont mesurées sur ce dépôt plutôt que lues dans une documentation.

**NFluent 3.1.0 ne reconnaît pas xUnit v3.** Il lève `NFluent.Kernel.FluentCheckException` au lieu
de `Xunit.Sdk.XunitException`. Le test échoue quand même, et l'exécuteur le rapporte bien comme un
échec avec le message NFluent complet — mais le rapport porte le nom du type d'exception là où il se
lisait auparavant comme un échec d'assertion natif. Vérifié : forcer le chargement préalable de
l'assembly d'assertions de xUnit n'y change rien, c'est donc NFluent qui cherche l'assembly de xUnit
v2 et non un accident d'ordre de chargement.

**Un check sans assertion passe en silence.** `Check.That(valeur);` compile, s'exécute, n'affirme
rien et rapporte au vert — là où `Assert.Equal` amputé d'un argument ne compile pas. C'est la seule
façon dont cette migration pouvait affaiblir la suite invisiblement, alors un test y veille :
`AssertionStyleTests` lit les sources de test et échoue sur une instruction `Check.That` sans rien
de chaîné.

**Capturer une exception, c'est `.Value`.** `Assert.Throws<T>(...)` rendait l'exception ;
l'équivalent NFluent est `Check.ThatCode(...).Throws<T>().Value`, et c'est ce qu'utilisent les sites
qui affirment quelque chose sur `exception.Problems`.

**Un test de levée asynchrone devient synchrone.** `Check.ThatAsyncCode` est marqué obsolète au
profit de `ThatCode`, qui accepte un `Func<Task>` et n'a pas besoin d'être attendu. Un test dont le
seul `await` était `Assert.ThrowsAsync` perd donc son `async`, sinon le compilateur signale CS1998 —
ce qui est une erreur ici, puisque les avertissements sont des erreurs.

**`CA1861` se déclenche sur un tableau en ligne** passé à un check à paramètres variables : une
valeur attendue de type collection est donc d'abord liée à une variable locale.

## La traduction retenue

Appliquée uniformément, pour qu'un lecteur qui croise l'une d'elles sache ce qu'elle était.

| Avant | Après |
| --- | --- |
| `Assert.Equal(e, a)` | `Check.That(a).IsEqualTo(e)` |
| `Assert.True(c)` / `Assert.False(c)` | `Check.That(c).IsTrue()` / `.IsFalse()` |
| `Assert.Null(x)` / `Assert.NotNull(x)` | `Check.That(x).IsNull()` / `.IsNotNull()` |
| `Assert.Contains(part, texte)` | `Check.That(texte).Contains(part)` |
| `Assert.DoesNotContain(part, texte)` | `Check.That(texte).Not.Contains(part)` |
| `Assert.Matches(p, s)` / `Assert.DoesNotMatch(p, s)` | `Check.That(s).Matches(p)` / `.Not.Matches(p)` |
| `Assert.Empty(xs)` / `Assert.NotEmpty(xs)` | `Check.That(xs).IsEmpty()` / `.Not.IsEmpty()` |
| `Assert.Single(xs)` | `Check.That(xs).HasOneElementOnly()` |
| `Assert.All(xs, x => …)` | `Check.That(xs).ContainsOnlyElementsThatMatch(x => …)` |
| `Assert.IsType<T>(x)` / `Assert.IsNotType<T>(x)` | `Check.That(x).IsInstanceOf<T>()` / `.IsNotInstanceOf<T>()` |
| `Assert.Same(a, b)` | `Check.That(b).IsSameReferenceAs(a)` |
| `Assert.Throws<T>(() => …)` | `Check.ThatCode(() => …).Throws<T>()` |
| `T e = Assert.Throws<T>(() => …)` | `T e = Check.ThatCode(() => …).Throws<T>().Value` |
| `Assert.ThrowsAny<Exception>(() => …)` | `Check.ThatCode(() => …).ThrowsAny()` |
| `await Assert.ThrowsAsync<T>(() => …)` | `Check.ThatCode(() => …).Throws<T>()` |

`Assert.Fail` n'a pas d'équivalent NFluent et reste. Ce n'est pas une affirmation sur une valeur,
c'est une branche où l'on n'aurait pas dû passer.

## Comment cela a été vérifié

Pas par une suite au vert : une migration qui transforme des assertions en non-opérations est verte
elle aussi. Cinq mutations ont été introduites dans la bibliothèque — un nom formaté passé en
majuscules, une sévérité d'analyseur abaissée, un schéma OpenAPI typé entier, un séparateur changé
dans `AllowedValues`, et l'analyse des combinaisons `[Flags]` mise en échec — et l'ensemble des cas
de test qui remarquaient chacune a été relevé avant la migration puis comparé à celui d'après. Les
cinq ensembles sont identiques.

L'instrument a dû être réparé deux fois, et chacun des deux défauts se lisait comme un résultat
plutôt que comme un défaut. Son extraction ne reconnaissait que les noms de test faits de caractères
de mot, si bien que chaque cas de `[Theory]` était invisible et que la première mutation semblait
attrapée par 7 tests là où elle l'est par 27. Puis il lançait les trois projets de test à la fois,
dont la sortie partage un seul flux, et une lecture a perdu une ligne en silence — ce qui ne se
distingue pas d'une assertion qui aurait cessé de remarquer, et a coûté un détour pour le savoir. Un
projet à la fois, et trois lectures consécutives concordent.
