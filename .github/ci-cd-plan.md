# Plan CI/CD avant la v1

Document de travail destiné au mainteneur. Il n'est pas publié : il vit sous `.github/`,
que les gardes de documentation ignorent (tout répertoire commençant par un point est exclu
de `DocumentationLinksTests`), donc il ne coûte ni traduction ni lien entrant. À supprimer —
ou à replier dans `CONTRIBUTING.md` — une fois exécuté.

Référence : [`Reefact/first-class-errors`](https://github.com/Reefact/first-class-errors).
Ce plan en reprend la posture, pas le volume : ce dépôt-ci est une bibliothèque à deux
paquets et six projets, pas une plateforme à trois trains de release.

## 1. Constat

| Sujet | État aujourd'hui |
|---|---|
| Protection de `main` | **aucune** — l'API renvoie `"protected": false` |
| Pull request | **non requise** — les 24 commits de l'historique sont allés droit sur `main` |
| Checks requis | aucun : la CI peut être rouge, rien n'empêche de pousser |
| Force-push / suppression de `main` | autorisés |
| Protection des tags `v*` | aucune — or le tag *est* la source de vérité de la version publiée |
| `concurrency` | absente des deux workflows : deux pushes rapprochés font tourner deux runs complets |
| `timeout-minutes` | absent : un job bloqué tient un runner jusqu'au défaut de 6 h |
| Épinglage des actions | par tag mobile (`@v4`), pas par SHA |
| `permissions` | déclarées au niveau workflow, pas au niveau job |
| Dependabot | absent |
| CodeQL / dependency-review / Scorecard | absents |
| Lint shell + workflows | absent, alors que la CI embarque du bash et `tests/PackageSmokeTest/run.sh` |
| `CONTRIBUTING.md`, `SECURITY.md`, `CODEOWNERS`, template de PR | absents |
| Commit-lint | absent, alors que l'historique suit déjà Conventional Commits de fait |

Deux contraintes propres à ce dépôt encadrent tout ce qui suit.

**Mainteneur solo.** GitHub interdit d'approuver sa propre pull request. Exiger ne serait-ce
qu'une approbation bloquerait toutes les PR, sauf à les contourner systématiquement par le
bypass admin — ce qui vide la règle de son sens. Le plan met donc **zéro approbation requise**
et fait porter la garde par les *checks*, pas par la revue.

**La documentation est sous garde structurelle.** `DocumentationLinksTests` lit tous les `.md`
du dépôt : chaque lien doit résoudre, et chaque page appariée doit avoir la même structure
(titres, puces, lignes de tableau) dans les deux langues. Un `CONTRIBUTING.md` à la racine
implique donc un `docs/CONTRIBUTING.fr.md` de structure identique, plus une entrée dans
`RootPages`. C'est un coût réel, à budgéter au §4.3.

## 2. Étape 1 — rendre la protection possible

Ordre imposé : on ne peut pas exiger un check qui n'existe pas encore. Cette étape passe
avant l'étape 2, et par une PR — la dernière qui aura le droit d'être fusionnée sans filet.

### 2.1 Un check agrégé, et un seul

Le job actuel s'appelle `build (SDK ${{ matrix.sdk }})`, ce qui produit deux checks nommés
`build (SDK 10.0.100)` et `build (SDK 10.0.x)`. Les exiger nommément est un piège : le jour où
la matrice change (SDK 11, ou un troisième axe), le nom exigé disparaît et **toutes les PR
restent bloquées en attente d'un check qui ne viendra jamais**.

On ajoute donc un job terminal, seul check requis, qui lit le résultat de la matrice :

```yaml
  ci:
    name: CI
    needs: [build]
    if: always()          # sinon un job annulé ferait « skipped », que GitHub compte comme réussi
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      contents: read
    steps:
      - name: Verify the matrix succeeded
        run: |
          echo "build: ${{ needs.build.result }}"
          [ "${{ needs.build.result }}" = "success" ] || exit 1
```

La matrice peut alors évoluer librement ; le contrat de branche, lui, ne bouge plus.

### 2.2 Durcissement de `ci.yml` et `release.yml`

- **`concurrency`** par workflow et par ref, `cancel-in-progress: true` sur la CI (une PR
  poussée trois fois d'affilée ne doit pas mobiliser six runners) et **`false` sur la
  release** — on n'annule pas une publication en vol.
- **`timeout-minutes`** sur chaque job. La CI tourne en quelques minutes ; 20 suffisent, et
  bornent un runner bloqué au lieu des 6 h par défaut.
- **`permissions` au niveau job** plutôt qu'au niveau workflow, pour qu'un job ajouté plus
  tard n'hérite de rien qu'il n'ait demandé.
- **Épinglage par SHA** de toutes les actions, avec le tag en commentaire. C'est ce que
  vérifie le check *Pinned-Dependencies* de Scorecard, et ce qui empêche qu'un tag `v4`
  redéplacé change le code exécuté. Les SHA que `first-class-errors` épingle aujourd'hui
  (à revérifier au moment de l'implémentation) :

  | Action | SHA | Tag |
  |---|---|---|
  | `actions/checkout` | `3d3c42e5aac5ba805825da76410c181273ba90b1` | v7 |
  | `actions/setup-dotnet` | `a98b56852c35b8e3190ac28c8c2271da59106c68` | v6.0.0 |
  | `actions/upload-artifact` | `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` | v7 |

  Ce dépôt est encore en `@v4` sur les trois : la montée de version se fait dans la même PR.
  Dependabot (étape 4) prend ensuite le relais des bumps.

### 2.3 Réglages du dépôt

Dans *Settings → Actions → General* :

- **Workflow permissions** → *Read repository contents and packages permissions*. Le
  `GITHUB_TOKEN` par défaut redevient lecture seule ; chaque job élargit ce dont il a besoin.
- **Require approval for all external contributors** sur les runs de fork.

## 3. Étape 2 — protéger `main` et les tags

Via des **rulesets** (*Settings → Rules → Rulesets*) et non la protection de branche
classique : ils s'exportent en JSON, se versionnent, et gèrent proprement les listes de
bypass. Les rulesets sont disponibles gratuitement sur un dépôt public.

### 3.1 Ruleset `main`

| Règle | Valeur | Pourquoi |
|---|---|---|
| Target | `main` (branche par défaut) | |
| Restrict deletions | ✅ | `main` ne se supprime pas |
| Block force pushes | ✅ | l'historique de `main` est acquis |
| Require linear history | ✅ | cohérent avec un historique déjà linéaire et lisible |
| Require a pull request before merging | ✅ | **la demande centrale** |
| — Required approvals | **0** | mainteneur solo : voir §1. À passer à 1 dès qu'un second contributeur arrive |
| — Dismiss stale approvals on push | ✅ | sans coût aujourd'hui, correct demain |
| — Require conversation resolution | ✅ | ce qui remplace l'approbation absente : un commentaire non résolu bloque |
| Require status checks to pass | ✅ → **`CI`** uniquement | le job agrégé du §2.1. **Fait** : une PR obligatoire sans check requis n'est qu'une demi-règle — elle arrête le push direct et laisse toujours merger une branche rouge. Le job agrégé a donc été avancé de l'étape 1 à ici |
| — Require branches to be up to date | ✅ | interdit le merge d'une branche verte contre un `main` qui a bougé |
| Require signed commits | ⚠️ optionnel | fort, mais impose la signature à *tous* les auteurs, agents compris. À décider séparément |
| Bypass list | *Repository admin* | filet en cas d'incident ; à n'utiliser qu'en connaissance de cause |

Effet immédiat : le développement — le vôtre comme celui d'un agent — passe désormais par
`branche → PR → CI verte → merge`.

### 3.2 Ruleset `v*` (tags)

Souvent oublié, et pourtant : ici le tag Git **est** la version publiée (`Directory.Build.props`
le dit explicitement, `release.yml` en dérive `-p:Version=`). Un tag supprimé puis recréé sur
un autre commit republierait un numéro de version déjà consommé.

| Règle | Valeur |
|---|---|
| Target | `refs/tags/v*` |
| Restrict updates | ✅ |
| Restrict deletions | ✅ |

## 4. Étape 3 — cadre de contribution

### 4.1 Template de pull request (`.github/pull_request_template.md`)

**Fait**, et livré avec les rulesets plutôt qu'ici : le regroupement d'origine était mauvais.
`CONTRIBUTING.md` et `SECURITY.md` coûtent chacun une traduction française de structure
identique ; le template, lui, vit sous `.github/`, que les gardes de documentation ignorent, et
ne coûte donc rien. Rien ne justifiait de le faire attendre — d'autant que c'est la ruleset
`main` qui rend les pull requests obligatoires, et que le template est ce qu'elles remplissent.

Repris de la référence, **fortement raccourci** : ce dépôt n'a ni ADR, ni trains de release.
Sections retenues : *Résumé*, *Type de changement*, *Changements*, *Tests effectués*
(`dotnet build`, `dotnet test`, `tests/PackageSmokeTest/run.sh`), *API publique* (la baseline
du §7.1 a rendu cette section possible), *Documentation* (avec les cases « traduction FR à
jour » et « les deux CHANGELOG », que la garde structurelle rend non négociables), *Issues
liées*.

### 4.2 `CODEOWNERS`

**Fait**, dans `.github/CODEOWNERS`. Une ligne, `* @Reefact`. Sans approbation requise
(`required_approving_review_count` vaut 0), l'effet est la demande de revue automatique — utile
surtout le jour où un contributeur externe ouvre une PR. Le fichier note pourquoi
`require_code_owner_review` reste à `false` : avec un seul propriétaire, l'activer enfermerait le
mainteneur dehors, GitHub interdisant d'approuver sa propre pull request.

### 4.3 `CONTRIBUTING.md` + `SECURITY.md`

**Fait**, les deux, avec leurs traductions.

`SECURITY.md` active le bouton *Report a vulnerability* et fait gagner un point Scorecard ;
celui de la référence est réutilisable presque tel quel, en remplaçant le nom du projet et
l'URL de l'advisory.

`CONTRIBUTING.md` : version resserrée de la référence — comment builder et tester, la
convention de branche `<auteur>/<description>`, la convention de commit, la règle
« `main` ne s'écrit que par merge ».

**Coût à ne pas sous-estimer** : les deux pages ajoutent `docs/CONTRIBUTING.fr.md` et
`docs/SECURITY.fr.md`, de structure strictement identique (même nombre de titres, de puces et
de lignes de tableau, mêmes tags de blocs de code), plus deux entrées dans `RootPages` de
`DocumentationLinksTests`. Sans ça, la suite passe au rouge. C'est le poste le plus coûteux
du plan, et le seul qui touche du code de test.

Coût confirmé, et payé : la paire `CONTRIBUTING` s'est alignée sur 10 titres, 0 puce, 8 lignes de
tableau et quatre blocs `sh` dans le même ordre. Ce que le plan n'avait pas anticipé, c'est que
`CONTRIBUTING.md` documente surtout des règles que le dépôt applique déjà tout seul — la ruleset,
le commit-lint, la baseline d'API, les gardes de documentation. La page tient donc de l'inventaire
de ce qui est déjà vrai plutôt que de la promesse, ce qui la rend vérifiable : chaque affirmation
y renvoie au fichier qui la fait respecter.

### 4.4 Commit-lint

L'historique suit déjà Conventional Commits (`fix:`, `feat(analyzers):`, `ci:`, `docs:`,
`test:`) sans que rien ne l'impose. Trois pièces, reprises de la référence :

- `tools/commit-lint/lint-commit-message.sh` — le check, partagé ;
- `.githooks/commit-msg` — retour immédiat en local, activé par
  `git config core.hooksPath .githooks` ;
- `.github/workflows/commit-lint.yml` — rattrape un `--no-verify`, et se branche sur les
  commits de la PR (`base.sha..head.sha`, `--no-merges`).

À ajouter aux checks requis du ruleset une fois vert. Payant surtout au moment où la
génération du CHANGELOG s'appuiera dessus.

## 5. Étape 4 — chaîne d'approvisionnement

Quatre ajouts peu coûteux, tous en `permissions: contents: read` sauf mention.

- **`.github/dependabot.yml`** — écosystèmes `nuget` et `github-actions`, hebdomadaire,
  préfixes de commit `build` et `ci` pour rester compatible avec le commit-lint. C'est
  l'écosystème `github-actions` qui rend l'épinglage par SHA du §2.2 tenable dans la durée.
  À noter : `Microsoft.CodeAnalysis.CSharp` est ici volontairement à `4.11.0` (plancher Roslyn
  de l'analyseur) — comme dans la référence, on l'exclut des mises à jour, sinon Dependabot
  reproposera indéfiniment un bump qui casserait le chargement de l'analyseur sur les SDK
  plus anciens.
- **`dependency-review.yml`** — sur `pull_request`, échoue si la PR *introduit* une
  dépendance vulnérable (`fail-on-severity: moderate`). Complément amont de Dependabot, qui
  lui ne réagit qu'une fois la dépendance déjà sur `main`.
- **`codeql.yml`** — C#, `build-mode: none` (pas de SDK à installer, pas de tracing du
  compilateur à faire fonctionner sur un SDK très récent), sur push/PR + hebdomadaire.
  `permissions: security-events: write`.
- **`lint.yml`** — `shellcheck` (préinstallé sur l'image Ubuntu, donc zéro dépendance
  ajoutée) sur `tests/PackageSmokeTest/run.sh`, et `actionlint` (tarball épinglé + vérifié
  par SHA-256) sur les workflows. Directement pertinent ici : la vérification du nuspec dans
  `ci.yml` est du bash inline, et le smoke test est un script de 200 lignes que rien ne relit
  aujourd'hui.

**`scorecard.yml`** — à garder pour la fin, voire après la v1. Il note la posture du dépôt et
alimente un badge README, mais il ne devient flatteur qu'une fois les étapes 1 à 4 faites :
le lancer avant, c'est produire un mauvais score et un badge à cacher.

## 6. Étape 5 — durcir la release

`release.yml` est déjà en bon état (OIDC / trusted publishing, dry run, vérification du
nuspec, smoke test avant toute étape irréversible). Quatre manques :

1. **Rien ne garantit que le commit tagué a vu la CI.** ~~Une fois `main` protégée, tout commit
   de `main` est vert — mais un tag peut être posé n'importe où.~~ **Fait** : un premier pas
   refuse de publier si `$GITHUB_SHA` n'est pas contenu dans `origin/main` (`git merge-base
   --is-ancestor`), avant que quoi que ce soit ne soit construit. La ruleset de branche gouverne
   ce qui atterrit sur `main`, pas où pointe une ref — c'était donc le dernier chemin vers une
   publication non revue. Sauté sur un dry run, puisque répéter une release depuis une branche
   est précisément son objet.

   **Fait aussi, et lié** : la validation SemVer stricte du numéro de version. Le déclencheur
   est `v*`, qui attrape `vnext`, `v1`, `v1.0.0.0` — et, Git autorisant le caractère dans un nom
   de ref, `v1.0.0;whoami`. La valeur était ensuite interpolée par `${{ }}` **dans le corps du
   `run:`** de l'étape de pack, ce qui en faisait du script et non de la donnée : le fichier
   énonçait la règle pour l'input de dispatch et l'enfreignait quatre lignes plus bas. La version
   est désormais validée contre une liste blanche SemVer 2.0.0 avant tout build, transmise par
   l'environnement, et la clé API OIDC de même. Le métadonnée de build (`1.0.0+abc`) est refusée :
   nuget.org la retire de la version publiée, si bien qu'un tag qui en porte annoncerait une
   chose et publierait l'autre.
2. **Attestation de provenance.** `actions/attest-build-provenance` avec
   `attestations: write`, pour que `gh attestation verify` réponde sur les `.nupkg` publiés.
   La référence le fait ; c'est aussi un point Scorecard.
3. ~~**Approbation manuelle avant publication.** L'environnement `nuget` existe déjà dans le
   workflow mais n'a probablement aucune règle de protection. Y ajouter un *required
   reviewer* (vous) fait de la publication un geste délibéré, tag poussé ou non.~~
   **Écarté**, au profit de la cohérence : aucun autre dépôt de l'organisation n'utilise
   d'environnement de déploiement, `first-class-errors` compris. L'environnement `nuget` est
   donc retiré plutôt que durci. Ce qu'on perd est réel — l'approbation faisait du clic
   *Approve*, et non du tag, le geste irréversible. Ce qui reste pour compenser : la garde
   d'ascendance (§6.1), la ruleset de tags, et le fait que le tag ne peut être posé que par
   quelqu'un qui a déjà accès en écriture.
4. **`concurrency: cancel-in-progress: false`**, pour ne jamais interrompre une publication.

## 7. Ce qui passe avant le tag v1

Décision prise : la v1 part **avant** le gros du plan. Le critère n'est pas « est-ce que ça
touche aux fonctionnalités » mais la réversibilité. Un workflow, une ruleset, un template se
corrigent n'importe quand. Un numéro de version sur nuget.org, jamais : `1.0.0` peut être
délisté, il ne pourra plus jamais être republié. Seuls les items qui protègent **l'artefact**,
et non le processus, passent donc devant — dans cet ordre.

### 7.1 Baseline d'API publique (`PublicApiAnalyzers`)

`PublicApiContractTests` teste des **comportements** (immuabilité de la liste rendue, ce que
l'API refuse), pas la **surface**. Rien ne signalerait aujourd'hui qu'une surcharge disparaît
ou qu'un type de retour se rétrécit sous un numéro de version qui promet la compatibilité.

Mais l'argument décisif n'est pas le cliquet : générer `PublicAPI.Shipped.txt` produit
**l'inventaire ligne à ligne de la surface publique**, au moment précis où elle cesse d'être
révocable. C'est la relecture de v1, pas seulement la garde des versions suivantes — et
`Shipped` veut dire *shipped* : remplir le fichier à la v1 fait coïncider la sémantique de
l'outil avec la réalité, au lieu de rétro-documenter une promesse déjà faite.

D'où sa place en tête : c'est **le seul des trois items qui peut changer ce qui est publié**.
Si la relecture conclut qu'un type doit devenir `internal`, c'est un changement de code, et il
doit atterrir avant le tag.

Portée : les deux projets `IsPackable` — `Reefact.AspNetCore.EnumMemberNameBinding` et
`Reefact.AspNetCore.EnumMemberNameBinding.OpenApi`. Pas le projet d'analyseurs, dont la surface
publique est un jeu de diagnostics, pas une API.

La surface est petite — de l'ordre de 25 entrées au total :

```
EnumMemberNames.GetPublicNames / GetPublicName / IsFlagsContract
EnumMemberNameBindingOptions.ScanAssemblyContaining<T> / AddEnum<TEnum>
EnumMemberNameBindingMvcBuilderExtensions.AddEnumMemberNameBinding
EnumContractException, EnumMemberNameConverter (+ ctor, 2 overrides)
EnumMemberNameOpenApiOptionsExtensions.AddEnumMemberNames
EnumMemberNameSchemaTransformer (+ ctor public, TransformAsync)
```

**Fait.** La relecture a eu lieu, sous cinq lentilles indépendantes, chaque constat vérifié de
façon adversariale contre le code. Treize constats vérifiés se sont réduits à sept décisions,
dont trois changeaient ce qui est publié — toutes appliquées :

| # | Décision | Effet |
|---|---|---|
| D1 | `EnumMemberNameConverter` → `internal sealed`, constructeur public conservé | −4 entrées |
| D2 | `EnumMemberNameSchemaTransformer` → `internal sealed` | −3 entrées |
| D3 | `AddEnumMemberNames` → namespace `Microsoft.Extensions.DependencyInjection` | `using` supprimé chez le consommateur |

Les deux justifications supposées ci-dessus étaient **fausses**, et c'est la relecture qui l'a
établi : `TypeConverterAttribute` exige un *constructeur* public et non un *type* public, et
`AddEnumMemberNames` enregistre le transformer par la surcharge d'instance d'`AddSchemaTransformer`,
indifférente à l'accessibilité. Les deux types étaient publics par héritage, pas par décision —
ce que le baseline devait précisément révéler.

Quatre autres questions ont été posées et répondues « on ne change rien » : le nom
`AddEnumMemberNames`, `IReadOnlyList<string>?` sur `GetPublicNames`, les collections `IList<T>` des
options, et `Problems` en prose. Chacune porte désormais son raisonnement dans son propre
commentaire de doc.

Surface finale : **19 entrées** dans le paquet principal, **2** dans le compagnon.

Pièges d'installation :

- `TreatWarningsAsErrors` est **déjà actif** dans `Directory.Build.props`, donc RS0016 est une
  erreur dès que l'analyseur entre : le build casse tant que les baselines sont vides. Elles
  doivent être remplies **dans le même commit**. Chemin fiable : ajouter le `PackageReference`
  (`PrivateAssets="all"`), builder, récolter les messages RS0016 — ils donnent la signature
  exacte à écrire.
- Un seul TFM (`net10.0`) : pas de sous-dossiers par framework, contrairement à
  `first-class-errors` qui multi-cible.
- RS0026/RS0027 ne devraient pas se déclencher ici (aucune méthode à paramètre optionnel n'a
  de surcharge), donc pas besoin de les désactiver comme le fait la référence. À confirmer au
  build.
- Au moment de la release : promouvoir les entrées de `PublicAPI.Unshipped.txt` vers
  `PublicAPI.Shipped.txt`.

### 7.2 Ruleset `v*`

Voir §3.2. Cinq minutes, aucune dépendance, aucun code. C'est le moment exact où elle compte :
le tag qu'on s'apprête à pousser crée une correspondance tag ↔ version publiée qui devient
permanente.

### 7.3 Dry run

`workflow_dispatch` sur `release.yml`, avec le vrai numéro de version. Il prouve l'OIDC et les
vérifications de paquet avant que le tag existe — ce pour quoi il a été écrit.

**En dernier**, une fois §7.1 fusionné : le dry run doit exercer le commit **final**. Lancé
avant, il prouve un commit qui ne sera pas celui publié.

La ruleset `main` (§3.1) peut se poser dès maintenant en parallèle, mais en version réduite —
*require a pull request* et blocage du force-push, **sans check requis**, puisque le job
agrégé `CI` n'existe pas encore. Exiger les deux noms de matrice actuels rejouerait exactement
le piège du §2.1. Le check requis arrive avec l'étape 1.

## 8. Écart de couverture repéré au passage

La vérification du contenu de paquet, dans `ci.yml` comme dans `release.yml`, ne porte que sur
`Reefact.AspNetCore.EnumMemberNameBinding.nupkg`. Le second paquet publié,
`Reefact.AspNetCore.EnumMemberNameBinding.OpenApi`, n'est vérifié par rien — ni ses dépendances, ni son
contenu — alors qu'il part sur nuget.org dans le même `dotnet nuget push artifacts/*.nupkg`.

**Vérifié à la main avant la v1** (`dotnet pack -p:Version=1.0.0-check`, inspection des deux
`.nupkg`) : les deux paquets sont sains. Le principal déclare son `frameworkReference`
Microsoft.AspNetCore.App et embarque `analyzers/dotnet/cs/` ; celui d'OpenApi porte ses trois
dépendances, dont le plancher `Microsoft.OpenApi` 2.11.0 qui évite l'advisory, et livre bien
son `build/*.targets`. Le trou est donc réel dans la CI mais ne cache aucun défaut, ce qui est
la raison pour laquelle il ne bloque pas la v1 — il suit à l'étape 6.

## 9. Volontairement écarté de la référence

| Ce que fait `first-class-errors` | Pourquoi pas ici |
|---|---|
| ~~SonarQube Cloud~~ | **Repris**, dans une version réduite : un seul workflow, ni profil de qualité ni gate personnalisés. L'argument d'origine — les analyseurs Roslyn couvrent l'essentiel — reste vrai pour la *correction*, mais rate ce que Sonar apporte réellement ici : la mesure de couverture, que rien d'autre ne produit, et les deux badges *Quality* de la page d'accueil |
| Tests de mutation (Stryker) | Coûteux en temps de runner, pertinent sur une base bien plus large |
| `adr-check`, `gendoc-docs`, `canary`, `justdummies-*` | Répondent à des besoins qui n'existent pas ici (base d'ADR, générateur de doc, trains multiples) |
| Release multi-trains (`lib-v*`, `cli-v*`, `dum-v*`) | Un seul train ici ; `v*` suffit |
| `dependabot-autofix` (triage par LLM) | À reconsidérer une fois Dependabot en place et le volume de PR connu |
| `dependabot-automerge` | Utile en solo, mais **strictement** après le ruleset : sans checks requis, l'auto-merge fusionne immédiatement. À faire en phase 2, et en version simplifiée (patch/minor seulement) |
| Matrice Windows | Aucun code sensible à la plateforme dans cette bibliothèque ; la matrice actuelle porte sur les SDK, ce qui est le bon axe (deux régressions passées venaient d'un écart d'analyseur entre SDK) |

## 10. Séquencement

### Avant le tag v1

| # | Quoi | Effort |
|---|---|---|
| A | `build: baseline the public API before v1 promises it` — PublicApiAnalyzers sur les deux projets packables, baselines remplies, surface relue (§7.1) | ~45 min |
| B | Ruleset `v*` ; et, en option, ruleset `main` en version réduite — PR requise, sans check (§7.2) | ~15 min |
| C | Dry run de `release.yml` sur le commit final, puis tag `v1.0.0-beta.1` (§7.3) | ~15 min |

Sur le C : la pré-version est revenue au menu après coup, et pour une raison qui n'est pas la
prudence. Le chemin de publication n'a jamais tourné en vrai — le dry run saute justement les deux
étapes irréversibles. Et la ruleset `v*` interdit `deletion` comme `update`, donc un tag qui
échouerait à publier ne se reprend pas : il faudrait passer à la version suivante. Brûler
`beta.1` ne coûte rien, brûler `1.0.0` coûte le numéro qu'on ne peut avoir qu'une fois.

L'ordre A → B → C est contraint : A peut changer ce qui est publié, C doit exercer ce qui l'est.

### Après la v1

Chaque étape est une PR, toutes par le circuit protégé.

| # | PR | Contenu | Détail | Effort |
|---|---|---|---|---|
| 1 | `ci: add an aggregate gate and harden the workflows` | job `CI`, concurrency, timeouts, permissions par job, épinglage SHA + montée de version des actions ; puis ajout de `CI` aux checks requis | §2 | ~1 h |
| 2 | *(pas de PR)* | Ruleset `main` complète, réglages Actions du dépôt | §3.1, §2.3 | ~15 min |
| 3 | `ci: guard the supply chain` | dependabot, dependency-review, codeql, lint (shellcheck + actionlint) | §5 | ~1 h |
| 4 | `docs: state how this repository is contributed to` | template de PR, CODEOWNERS, CONTRIBUTING + SECURITY **et leurs traductions**, entrées `RootPages` | §4.1 à §4.3 | ~2 h |
| 5 | `ci: lint the commit messages` | script partagé, hook `commit-msg`, workflow ; puis ajout aux checks requis | §4.4 | ~1 h |
| 6 | `ci: make a release provable and deliberate` | garde tag-sur-`main`, attestation, reviewer sur l'environnement `nuget`, concurrency ; + vérification du paquet OpenApi | §6, §8 | ~1 h |
| 7 | *plus tard* | Scorecard + badge, dependabot-automerge | §5, §9 | — |

L'étape 6 gagne en valeur une fois la v1 publiée, pas l'inverse : les releases suivantes sont
des correctifs sur un paquet vivant, où l'approbation manuelle et l'attestation comptent plus
que sur une première publication délibérée.
