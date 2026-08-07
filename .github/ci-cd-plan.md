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
`RootPages`. C'est un coût réel, à budgéter à l'étape 3.

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
| Require status checks to pass | ✅ → **`CI`** uniquement | le job agrégé du §2.1 |
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

Repris de la référence, **fortement raccourci** : ce dépôt n'a ni ADR, ni trains de release.
Sections retenues : *Résumé*, *Type de changement*, *Changements*, *Tests effectués*
(`dotnet build`, `dotnet test`, `tests/PackageSmokeTest/run.sh`), *Documentation*
(avec la case « traduction FR à jour », que la garde structurelle rend non négociable),
*Issues liées*.

### 4.2 `CODEOWNERS`

Une ligne, `* @Reefact`. Sans approbation requise, l'effet est la demande de revue
automatique — utile surtout le jour où un contributeur externe ouvre une PR.

### 4.3 `CONTRIBUTING.md` + `SECURITY.md`

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

1. **Rien ne garantit que le commit tagué a vu la CI.** Une fois `main` protégée, tout commit
   de `main` est vert — mais un tag peut être posé n'importe où. Ajouter un premier pas qui
   refuse de publier si `$GITHUB_SHA` n'est pas contenu dans `origin/main` (`git merge-base
   --is-ancestor`). Quelques lignes, et cela ferme le seul chemin restant vers une
   publication non revue.
2. **Attestation de provenance.** `actions/attest-build-provenance` avec
   `attestations: write`, pour que `gh attestation verify` réponde sur les `.nupkg` publiés.
   La référence le fait ; c'est aussi un point Scorecard.
3. **Approbation manuelle avant publication.** L'environnement `nuget` existe déjà dans le
   workflow mais n'a probablement aucune règle de protection. Y ajouter un *required
   reviewer* (vous) fait de la publication un geste délibéré, tag poussé ou non. Gratuit sur
   un dépôt public.
4. **`concurrency: cancel-in-progress: false`**, pour ne jamais interrompre une publication.

## 7. Écart de couverture repéré au passage

Hors périmètre strict CI/CD, mais à traiter avant la v1 puisque c'est la CI qui devrait le
voir : la vérification du contenu de paquet, dans `ci.yml` comme dans `release.yml`, ne porte
que sur `AspNetCore.EnumMemberNameBinding.nupkg`. Le second paquet publié,
`AspNetCore.EnumMemberNameBinding.OpenApi`, n'est vérifié par rien — ni ses dépendances, ni
son contenu — alors qu'il part sur nuget.org dans le même `dotnet nuget push artifacts/*.nupkg`.
Une poignée de lignes dans le même style que l'existant.

## 8. Volontairement écarté de la référence

| Ce que fait `first-class-errors` | Pourquoi pas ici |
|---|---|
| SonarQube Cloud (3 workflows + profil + gate) | Service tiers, configuration lourde ; les analyseurs Roslyn + `TreatWarningsAsErrors` couvrent déjà l'essentiel sur une base de code de cette taille |
| Tests de mutation (Stryker) | Coûteux en temps de runner, pertinent sur une base bien plus large |
| `adr-check`, `gendoc-docs`, `canary`, `justdummies-*` | Répondent à des besoins qui n'existent pas ici (base d'ADR, générateur de doc, trains multiples) |
| Release multi-trains (`lib-v*`, `cli-v*`, `dum-v*`) | Un seul train ici ; `v*` suffit |
| `dependabot-autofix` (triage par LLM) | À reconsidérer une fois Dependabot en place et le volume de PR connu |
| `dependabot-automerge` | Utile en solo, mais **strictement** après le ruleset : sans checks requis, l'auto-merge fusionne immédiatement. À faire en phase 2, et en version simplifiée (patch/minor seulement) |
| Matrice Windows | Aucun code sensible à la plateforme dans cette bibliothèque ; la matrice actuelle porte sur les SDK, ce qui est le bon axe (deux régressions passées venaient d'un écart d'analyseur entre SDK) |

## 9. Séquencement proposé

Chaque étape est une PR ; à partir de la 2, elles passent toutes par le circuit protégé.

| # | PR | Contenu | Effort |
|---|---|---|---|
| 1 | `ci: add an aggregate gate and harden the workflows` | job `CI`, concurrency, timeouts, permissions par job, épinglage SHA + montée de version des actions | ~1 h |
| 2 | *(pas de PR)* | Rulesets `main` et `v*`, réglages Actions du dépôt | ~20 min |
| 3 | `ci: guard the supply chain` | dependabot, dependency-review, codeql, lint (shellcheck + actionlint) | ~1 h |
| 4 | `docs: state how this repository is contributed to` | template de PR, CODEOWNERS, CONTRIBUTING + SECURITY **et leurs traductions**, entrées `RootPages` | ~2 h |
| 5 | `ci: lint the commit messages` | script partagé, hook `commit-msg`, workflow ; puis ajout aux checks requis | ~1 h |
| 6 | `ci: make a release provable and deliberate` | garde tag-sur-`main`, attestation, reviewer sur l'environnement `nuget`, concurrency ; + vérification du paquet OpenApi (§7) | ~1 h |
| 7 | *après la v1* | Scorecard + badge, dependabot-automerge | — |

**Coupe minimale pour débloquer la v1** : étapes 1, 2 et 6. C'est ce qui répond exactement à
la demande — PR obligatoire, `main` protégée, publication sûre — en une demi-journée. Les
étapes 3 à 5 sont du confort et de la posture, et peuvent suivre la v1 sans rien bloquer.
