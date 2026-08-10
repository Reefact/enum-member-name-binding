# Changelog

🌍 **Langues :**  
🇬🇧 [English](../CHANGELOG.md) | 🇫🇷 Français (ce fichier)

Tous les changements notables de ce projet sont documentés ici.
Le format suit [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
et le projet respecte le [versionnage sémantique](https://semver.org/spec/v2.0.0.html).

La version du paquet est indépendante de la version de .NET qu'il cible.

## [Non publié]

La surface publique est celle que portera la 1.0.0 : elle a été relue symbole par symbole et
figée dans une baseline versionnée, cette pré-version n'est donc pas un brouillon d'API. Ce
qu'elle met à l'épreuve, c'est la publication elle-même — le trusted publishing, l'attestation de
provenance, et l'allure réelle des paquets une fois que nuget.org les a reçus et resignés. Un
numéro de version ne se reprend pas : le premier à faire le voyage doit être un numéro qu'on peut
brûler sans rien perdre.

### Ajouté

- `AddEnumMemberNameBinding()` sur `IMvcBuilder` : les valeurs de route, les chaînes de requête, les
  champs de formulaire et les en-têtes acceptent les noms de membres d'énumération déclarés avec
  `[JsonStringEnumMemberName]`.
- La liaison via un `TypeConverter` piloté par l'attribut natif. ASP.NET Core résout les
  binders de types simples via `TypeDescriptor` : aucun model binder n'est donc remplacé, et les
  énumérations nullables, les en-têtes et les champs de formulaire sont couverts par construction. Le
  convertisseur est un détail d'implémentation : `AddEnumMemberNameBinding()` est la seule entrée
  supportée.
- Une base de référence versionnée de l'API publique des deux paquets, pour qu'un changement de la
  surface publiée soit une différence relue et non un effet de bord. La surface a été lue symbole par
  symbole avant cette publication et volontairement réduite à ce dont un consommateur a besoin :
  19 entrées dans le paquet principal, 2 dans le compagnon.
- Validation au démarrage de chaque contrat enregistré, levant `EnumContractException` pour les noms
  publics en double, les noms entourés d'espaces et les virgules dans le nom d'un membre `[Flags]`.
- L'enregistrement est tout ou rien, sur les deux chemins — une liste explicite et le scan
  d'assembly. Chaque contrat est résolu et validé avant l'installation du premier convertisseur, car
  `TypeDescriptor` modifie un état global au processus qu'on ne peut pas défaire : une liste nommant
  un contrat correct et un contrat malformé installerait sinon le correct puis lèverait, laissant le
  processus dans un état que personne n'a demandé, derrière une exception qui se lit comme si rien
  ne s'était produit. Le refus nomme `options`, le paramètre que l'appelant a réellement écrit, et
  non une variable locale dans laquelle l'implémentation dépaquette la liste.
- Des gardes d'arguments sur chaque frontière publique et interne : un `null` que la signature
  interdit lève une `ArgumentNullException` nommant le paramètre, plutôt qu'une
  `NullReferenceException` venue de plus loin. Une annotation nullable n'engage que les appelants qui
  l'ont acceptée — ni celui qui compile avec le nullable désactivé, ni une valeur arrivant par
  réflexion, par injection de dépendances ou par un désérialiseur. `TryParse` est celui dont la
  réponse change, et pas seulement le message : `null` atteignait `AsSpan()`, qui produit une étendue
  vide, si bien qu'une signature rompue était rapportée exactement comme la chaîne vide — une valeur
  qu'un appelant peut légitimement envoyer.
- Prise en charge de `[Flags]` : listes séparées par des virgules, à l'identique de
  `System.Text.Json`.
- Une suite de tests de parité qui utilise `JsonSerializer` lui-même comme oracle — pour chaque entrée
  candidate, le résultat HTTP doit être égal au résultat obtenu par le corps.
- `EMN0006`, qui signale un nom public qu'au moins un canal ne peut pas transporter. L'ensemble
  interdit a été établi en envoyant chaque caractère sur les cinq canaux face à un serveur en
  fonctionnement, pas lu dans une spécification : une barre oblique est refusée dans un segment de
  route, et un saut de ligne ou un caractère hors ASCII imprimable est refusé dans un en-tête. `?`,
  `#`, `&`, `=`, `+`, `%`, l'espace, la tabulation, la barre oblique inverse et le guillemet voyagent
  tous intacts. Un avertissement plutôt qu'une erreur, car savoir si cela mord dépend des canaux
  depuis lesquels une API lie réellement — le message nomme le caractère et le canal qui le refuse. La
  mesure elle-même est épinglée par des tests.
- Des analyseurs Roslyn, livrés dans le paquet sous `analyzers/dotnet/cs`, pour qu'une erreur de
  contrat soit une erreur de compilation plutôt qu'une exception au démarrage : `EMN0001` nom public en
  double, `EMN0002` nom public inutilisable, `EMN0003` contrat incomplet, `EMN0004` virgule dans un nom
  `[Flags]`, `EMN0005` un nom public masquant le nom C# d'un autre membre — ce qui laisse ce membre
  répondre à toutes les casses de son nom sauf la sienne. Ces cinq-là sont des erreurs ; `EMN0006`
  ci-dessus est le seul avertissement, car une limite de portabilité dépend des canaux depuis lesquels
  une API lie réellement, là où les cinq autres signalent une ambiguïté fausse sur tous les canaux.
  Une énumération qui ne déclare aucun contrat n'est jamais analysée.
- Des vérifications de CI qui font échouer le build si le paquet produit ne déclare pas sa référence
  de framework `Microsoft.AspNetCore.App`, ou ne livre pas les analyseurs.
- Un test de fumée du paquet, exécuté sur les deux SDK en CI puis à nouveau comme dernière barrière
  avant publication. Il packe dans un feed local, compile une application qui consomme le résultat par
  `PackageReference`, et la pilote en HTTP — couvrant ce qu'une `ProjectReference` saute entièrement :
  la référence de framework, la place des analyseurs dans le paquet, les assets MSBuild, et la
  capacité d'un projet ayant ses propres réglages à compiler face à tout cela. C'est aussi la seule
  chose qui exerce l'appel mis en avant par le README, puisqu'`AddEnumMemberNameBinding()` sans
  options scanne l'assembly d'entrée, qui sous un host de test est le host de test. Une seconde
  fixture est censée *ne pas* compiler, pour qu'`EMN0003` doive venir de l'analyseur contenu dans le
  `.nupkg` ; l'assertion est positive, car « aucun diagnostic n'est apparu » est aussi ce à quoi
  ressemble un analyseur absent.
- `AspNetCore.EnumMemberNameBinding.OpenApi`, un paquet compagnon dont le transformateur de schéma
  fait décrire au document généré ce que le serveur accepte réellement : un type `string` explicite,
  les noms publics déclarés, et — pour les énumérations `[Flags]`, qu'ASP.NET Core documente sans
  aucune valeur — une expression régulière couvrant les combinaisons séparées par des virgules. Ses
  tests vérifient la cohérence document/exécution en rejouant chaque valeur annoncée face au serveur
  en fonctionnement.
- Le compagnon relève le plancher de `Microsoft.OpenApi` à 2.11.0. `Microsoft.AspNetCore.OpenApi`
  10.0.x résout 2.0.0, qui porte l'avis de sécurité GHSA-v5pm-xwqc-g5wc.
- Une icône sur les deux paquets, pour qu'ils soient identifiables sur nuget.org au lieu d'apparaître
  derrière le placeholder par défaut. Le test de fumée vérifie les deux moitiés sur chaque paquet —
  que le `.nuspec` déclare une icône, et que le fichier qu'il nomme s'y trouve réellement — car garder
  l'inclusion sans la propriété produit un paquet parfaitement valide que nuget.org affiche gris.

- `EnumMemberNames.GetPublicName(Enum)`, pour la génération de liens. ASP.NET Core formate les valeurs
  de route sans consulter `TypeDescriptor` : un lien construit à partir d'une valeur d'énumération
  porte donc le nom C#, et le binder le refuse. Cet écart ne peut pas être comblé depuis un
  `TypeConverter` ; il est documenté, et voici le contournement.

### Modifié

- Une énumération partiellement annotée est désormais **refusée par défaut**, à la compilation par
  `EMN0003` et au démarrage par `EnumContractException`. Un membre sans `[JsonStringEnumMemberName]`
  répond à son nom C#, ce qui place un identifiant interne dans le contrat public — l'opposé de
  l'objectif. `EnumMemberNameBindingOptions.AllowPartialContracts` permet d'y revenir pour les
  énumérations qui ne vous appartiennent pas, et restaure un comportement identique à celui de
  `System.Text.Json`.

### Corrigé

- **`EMN0005` manquait l'essentiel de la forme qu'elle existe pour attraper.** L'analyseur comparait
  un nom public déclaré au nom C# d'un autre membre de façon ordinale, alors que l'exécution recherche
  ces noms sans tenir compte de la casse — si bien que `[JsonStringEnumMemberName("blue")]` à côté d'un
  membre `Blue` passait inaperçu. Les deux comparent désormais sans tenir compte de la casse.
- **La vérification de masquage n'existait pas du tout à l'exécution**, alors que la documentation
  affirmait que chaque règle d'analyseur était aussi appliquée au démarrage. `EnumContract` la refuse
  désormais, y compris lorsque `AllowPartialContracts` est activé : la collision est une ambiguïté, pas
  un choix de politique.
- **La gestion des espaces divergeait de `System.Text.Json` de trois façons.** Une valeur était
  comparée sans être détourée, si bien que `" available "` et `" read "` étaient refusés là où le corps
  de la requête les accepte ; et une virgule finale dans une liste `[Flags]` était refusée là où le
  corps en tolère une. Le comportement a été caractérisé face à `JsonSerializer` puis reproduit, et
  toute la matrice figure désormais dans la suite de parité.
- **Le motif `[Flags]` du document OpenAPI excluait des formes que le binder accepte** — les espaces
  en tête et en fin, et la virgule finale. Le document annonçait un contrat plus strict que celui que
  le serveur honorait.
- **Les help links des analyseurs pointaient vers des pages inexistantes**, si bien que le lien
  de l'IDE menait à un 404. Chaque règle a désormais une page sous `docs/rules`, et un test échoue si
  une règle et sa page divergent.
- **L'écriture d'une combinaison `[Flags]` divergeait de `System.Text.Json`.** La décomposition suivait
  l'ordre de déclaration, alors que le sérialiseur trie les membres topologiquement pour qu'une
  combinaison couvrant plusieurs bits l'emporte sur ses constituants. `7` s'écrivait
  `read, write, delete` là où le sérialiseur écrit `read_write, delete`, et une énumération de flags
  sur `sbyte` ordonnait ses membres différemment encore. Deux formes indépendantes ont suffi à écarter
  les deux règles de départage évidentes : les combinaisons sont donc désormais confiées au
  sérialiseur lui-même plutôt qu'imitées — la parité par construction. Un membre déclaré est toujours
  servi depuis le cache, seules les combinaisons en paient le prix.
- **Le motif OpenAPI `[Flags]` utilisait des échappements qu'ECMA-262 rejette.** `Regex.Escape`
  échappe les espaces et `#`, produisant `\ ` et `\#` ; ni l'un ni l'autre n'est un échappement
  d'identité valide dans le dialecte avec lequel un `pattern` de JSON Schema est lu, si bien qu'un
  consommateur strict rejetterait le motif entier. Seuls les caractères de syntaxe sont désormais
  échappés, et un test rejette tout autre échappement.
- **Aucun point d'entrée ne portait sa contrainte de trimming ou de Native AOT.** Les analyseurs de
  trim et d'AOT sont désormais activés sur les deux paquets, ce qui a fait remonter neuf diagnostics
  que rien ne signalait auparavant. Chaque point d'entrée est annoté : un consommateur qui compile pour
  l'un ou l'autre obtient donc un avertissement exact au lieu d'un échec silencieux à l'exécution. Les
  deux implémentations d'interface qui ne peuvent pas porter les attributs les suppriment
  explicitement, à côté d'un constructeur qui, lui, les porte.
  Les deux contraintes sont appliquées **séparément**, car ce ne sont pas les mêmes : lire les
  métadonnées d'une énumération exige de la réflexion mais ne génère aucun code. `GetPublicNames`,
  `IsFlagsContract`, l'enregistrement MVC et tout le paquet OpenAPI portent donc
  `[RequiresUnreferencedCode]` seul ; `GetPublicName`, le chemin de formatage `[Flags]` et la
  construction des convertisseurs JSON génériques portent les deux. Un consommateur n'est averti de la
  génération de code que sur un chemin qui en génère réellement.
- **Le README montrait le motif `[Flags]` précédent**, celui d'avant l'autorisation des espaces
  autour et de la virgule finale. Corrigé, et un test compare désormais le motif documenté à celui que
  le transformateur émet.
- **Enregistrer deux fois la même énumération empilait un nouveau fournisseur `TypeDescriptor` à
  chaque fois.** Un type n'est désormais enregistré qu'une fois par processus, tandis que la
  validation s'exécute toujours à chaque appel, de sorte qu'un second enregistrement avec des options
  plus strictes échoue encore. Couvert par des tests qui hébergent plusieurs applications côte à côte.
- **L'installation en un seul paquet du compagnon OpenAPI, telle que documentée, ne compilait pas.**
  `Microsoft.AspNetCore.OpenApi` active l'espace de noms d'intercepteurs dans lequel écrit son
  générateur de commentaires XML, et il le fait via des assets MSBuild `build`, que NuGet ne propage
  pas en transitif. Un consommateur qui prenait le compagnon et rien d'autre — exactement ce
  qu'indique `docs/openapi.fr.md` — héritait donc du générateur sans la propriété qui rend sa sortie
  légale, et son build échouait sur CS9137 dans du code généré qu'il n'avait jamais écrit. Référencer
  `Microsoft.AspNetCore.OpenApi` en direct corrigeait aussi le problème, et la plupart des
  consommateurs l'auront déjà fait, ce qui explique que rien dans ce dépôt ne l'ait remarqué. Le
  compagnon livre désormais cette propriété lui-même, depuis un `.targets` et non un `.props` : NuGet
  importe le premier sous le corps du projet consommateur et le second au-dessus, si bien qu'un
  consommateur qui affecte `InterceptorsNamespaces` au lieu d'y concaténer écraserait silencieusement
  un `.props` et retrouverait CS9137. Microsoft le livre depuis un `.targets` pour cette raison, et
  son paquet survit à cette affectation là où une version `.props` du nôtre a été mesurée comme n'y
  survivant pas. Elle active le seul espace de noms que Microsoft active, et rien de plus. Trouvé par
  le test de fumée du paquet dès sa première exécution, et le fixture consommateur fait désormais
  cette affectation lui-même pour que la distinction ne puisse plus se perdre.

- `Microsoft.AspNetCore.OpenApi` et la sérialisation des Minimal APIs lisent `Http.Json.JsonOptions`,
  tandis que MVC lit `Mvc.JsonOptions`. Seules ces dernières étaient configurées, si bien que chaque
  énumération sous contrat était décrite comme un entier dans le document généré. Les deux sont
  désormais configurées, toujours avec un convertisseur par type sous contrat.

### Documentation

- Le README a été scindé. Il avait atteint une longueur que personne ne lit avant d'adopter un
  paquet : la page d'accueil porte désormais le problème, l'installation, un exemple, le tableau des
  canaux, les garanties et les deux limitations à connaître avant d'adopter — le reste a été déplacé,
  sans coupe, vers `docs/contract-rules.en.md`, `docs/analyzers.en.md`, `docs/openapi.en.md` et
  `docs/limitations.en.md`. Le README est aussi la page NuGet du paquet, où un lien relatif est mort :
  il pointe donc vers GitHub en absolu. Un test échoue sur un lien relatif, et sur tout lien — dans
  n'importe quelle page — visant un fichier ou un titre inexistant.
- La documentation est désormais bilingue, suivant la convention utilisée dans les projets Reefact :
  chaque page existe en `Xxx.en.md` et `Xxx.fr.md` sous `docs`, et s'ouvre sur un lien vers sa
  contrepartie. Le README garde son nom et sa place, puisque NuGet le rend ; sa version française est
  `docs/README.fr.md`, et le changelog suit la même règle. Des tests échouent sur une page qui
  n'existe que dans une langue, sur une page qui n'offre pas l'autre, et sur une traduction dont la
  structure ne correspond plus à celle de l'original — les mots sont traduits, les sections, les
  puces, les lignes de tableau et les extraits ne sont ni retirés ni ajoutés. C'est ce dernier point
  qui attrape une entrée ajoutée à un seul des deux changelogs. Les help links des analyseurs
  pointent vers les pages de règles anglaises, qui font foi.

### Limitations connues

- **Les Minimal APIs ne sont pas couvertes.** La liaison de leurs paramètres n'utilise ni les model
  binders de MVC ni `TypeDescriptor` ; elle exige un `static TryParse`/`BindAsync` sur le type lié, ce
  qu'on ne peut pas ajouter à une `enum`. C'est une contrainte de la plateforme, pas un manque
  d'implémentation.
- **Une valeur vide sur un paramètre d'énumération nullable lie `null`** au lieu d'être rejetée, là où
  `System.Text.Json` rejette `""`. ASP.NET Core la résout avant que le moindre `TypeConverter` ne soit
  consulté. Un test épingle le comportement.
- **Incompatible avec le trimming et Native AOT.** `TypeDescriptor` et le scan d'assembly reposent sur
  la réflexion. Le point d'entrée public est annoté en conséquence, plutôt que de supprimer
  silencieusement les avertissements.
- L'enregistrement doit avoir lieu au démarrage : ASP.NET Core met en cache le model binder construit
  pour un type à la première utilisation.
