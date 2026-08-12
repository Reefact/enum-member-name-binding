# Changelog

🌍 **Langues :**  
🇬🇧 [English](../../CHANGELOG.md) | 🇫🇷 Français (ce fichier)

Tous les changements notables de ce projet sont documentés ici.
Le format suit [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
et le projet respecte le [versionnage sémantique](https://semver.org/spec/v2.0.0.html).

La version du paquet est indépendante de la version de .NET qu'il cible.

## [Non publié]

## [1.0.0-beta.1] - 2026-08-10

La surface publique est celle que portera la 1.0.0 : elle a été relue symbole par symbole et
figée dans une baseline versionnée, cette pré-version n'est donc pas un brouillon d'API. Ce
qu'elle met à l'épreuve, c'est la publication elle-même — le trusted publishing, l'attestation de
provenance, et l'allure réelle des paquets une fois que nuget.org les a reçus et resignés. Un
numéro de version ne se reprend pas : le premier à faire le voyage doit être un numéro qu'on peut
brûler sans rien perdre.

Une bêta et non une release candidate, et la différence est une affirmation plutôt qu'une étape :
la surface publique est arrêtée, le comportement derrière elle n'est éprouvé nulle part ailleurs
qu'ici. Une candidate dirait le contraire.

### Ajouté

- `AddEnumMemberNameBinding()` sur `IMvcBuilder` : les valeurs de route, les chaînes de requête, les
  champs de formulaire et les en-têtes acceptent les noms de membres d'énumération déclarés avec
  `[JsonStringEnumMemberName]`.
- La liaison via un model binder enregistré sur les `MvcOptions` de l'application elle-même, inséré
  juste devant le fournisseur qu'ASP.NET Core utilise pour les énumérations — et non en tête, ce qui
  retirerait `[FromBody]` à `System.Text.Json`. Tout ce que l'enregistrement configure vit dans le
  conteneur de cette application : une seconde application hébergée dans le même processus reste
  intacte, qu'elle démarre avant ou après. Le binder est un détail d'implémentation :
  `AddEnumMemberNameBinding()` est la seule entrée supportée.
- Une base de référence versionnée de l'API publique des deux paquets, pour qu'un changement de la
  surface publiée soit une différence relue et non un effet de bord. La surface a été lue symbole par
  symbole avant cette publication et volontairement réduite à ce dont un consommateur a besoin :
  19 entrées dans le paquet principal, 2 dans le compagnon.
- L'enregistrement se configure par `EnumMemberNameBindingOptions` : `AddEnum<TEnum>()` nomme un
  contrat explicitement et `ScanAssemblyContaining<T>()` dirige le scan ailleurs que sur l'assembly
  d'entrée, avec `EnumTypes` et `Assemblies` en dessous comme échappatoires pour un appelant qui
  tient un `Type` ou un `Assembly` à l'exécution. `AllowPartialContracts` accepte une énumération
  annotée en partie, et `ConfigureJsonSerialization` refuse la moitié `System.Text.Json` de
  l'enregistrement pour une application qui configure ses convertisseurs elle-même. Nommer quoi que
  ce soit vaut « ne scanne rien d'autre » : l'assembly d'entrée est un défaut, pas un ajout.
- Validation au démarrage de chaque contrat enregistré, levant `EnumContractException` pour les noms
  publics en double, les noms entourés d'espaces et — sur une énumération `[Flags]` — les virgules
  dans le nom d'un membre.
- L'enregistrement est tout ou rien, sur les deux chemins — une liste explicite et le scan
  d'assembly. Chaque contrat est résolu et validé avant que quoi que ce soit ne soit configuré : une
  liste nommant un contrat correct et un contrat malformé laisserait sinon le correct branché,
  derrière une exception qui se lit comme si rien ne s'était produit. Le refus nomme `options`, le
  paramètre que l'appelant a réellement écrit, et non une variable locale dans laquelle
  l'implémentation dépaquette la liste.
- Des gardes d'arguments sur chaque frontière publique et interne : un `null` que la signature
  interdit lève une `ArgumentNullException` nommant le paramètre, plutôt qu'une
  `NullReferenceException` venue de plus loin. Une annotation nullable n'engage que les appelants qui
  l'ont acceptée — ni celui qui compile avec le nullable désactivé, ni une valeur arrivant par
  réflexion, par injection de dépendances ou par un désérialiseur. `TryParse` est celui dont la
  réponse change, et pas seulement le message : `null` atteignait `AsSpan()`, qui produit une étendue
  vide, si bien qu'une signature rompue était rapportée exactement comme la chaîne vide — une valeur
  qu'un appelant peut légitimement envoyer.
- Les listes séparées par des virgules, à l'identique de `System.Text.Json` — sur toutes les
  énumérations et pas seulement sur les `[Flags]`, car ni `Enum.Parse` ni `System.Text.Json` ne
  regardent l'attribut avant de découper. Les refuser aurait rendu une énumération enregistrée plus
  stricte que la même énumération laissée tranquille.
- Une suite de tests de parité qui utilise `JsonSerializer` lui-même comme oracle — pour chaque entrée
  candidate, le résultat HTTP doit être égal au résultat obtenu par le corps. Elle couvre tous les
  types sous-jacents qu'une `enum` peut avoir, et pas seulement `int` : l'analyse élargit chaque
  membre en `ulong`, les combine par OU puis rétrécit le résultat, et l'extension de signe comme le
  bit de poids fort sont exactement ce que cette arithmétique peut perdre — ce qu'une énumération
  `int` sans membre négatif n'éprouve ni l'un ni l'autre.
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
  double, `EMN0002` nom public inutilisable, `EMN0003` contrat incomplet, `EMN0004` virgule dans un
  nom public sur une énumération `[Flags]`, `EMN0005` un nom public masquant le nom C# d'un autre
  membre — ce qui laisse ce membre répondre à toutes les casses de son nom sauf l'orthographe
  déclarée. Ces cinq-là sont des erreurs ; `EMN0006` ci-dessus est le seul avertissement, car une
  limite de portabilité dépend des canaux depuis lesquels une API lie réellement, là où les cinq
  autres signalent une ambiguïté fausse sur tous les canaux. Une énumération qui ne déclare aucun
  contrat n'est jamais analysée.
- Une vérification du contenu des deux paquets publiés, exécutée par la CI puis à nouveau par la
  release depuis un unique script partagé. Elle fait échouer le build si le paquet principal ne
  déclare pas sa référence de framework `Microsoft.AspNetCore.App` ou ne livre pas les analyseurs,
  et si le compagnon ne dépend pas du paquet principal, ne livre pas le `build/*.targets` dont un
  consommateur ne peut pas se passer, ou déclare un `Microsoft.OpenApi` sous le plancher qui évite
  GHSA-v5pm-xwqc-g5wc. Le compagnon n'était jusque-là vérifié par rien.
- Un test de fumée du paquet, exécuté sur les deux SDK en CI puis à nouveau comme dernière barrière
  avant publication. Il packe dans un feed local, compile une application qui consomme le résultat par
  `PackageReference`, et la pilote en HTTP — couvrant ce qu'une `ProjectReference` saute entièrement :
  la référence de framework, la place des analyseurs dans le paquet, les assets MSBuild, et la
  capacité d'un projet ayant ses propres réglages à compiler face à tout cela. Une seconde fixture
  est censée *ne pas* compiler, pour qu'`EMN0003` doive venir de l'analyseur contenu dans le
  `.nupkg` ; l'assertion est positive, car « aucun diagnostic n'est apparu » est aussi ce à quoi
  ressemble un analyseur absent.
- Une suite de tests de documentation : chaque exemple C# sous `docs/` et dans le README est compilé
  contre les paquets publiés, puis les analyseurs sont passés sur le résultat. La documentation est la
  seule chose que rien n'exécute, donc une option renommée ou un exemple écrit de mémoire se lit
  parfaitement et reste faux jusqu'à ce qu'un nouveau venu le découvre. Un exemple qui montre
  volontairement une erreur déclare la règle qu'il illustre, et une autorisation qui ne se déclenche
  plus échoue aussi — une page qui dit « voici à quoi ressemble `EMN0001` », au-dessus d'un code qui
  ne le déclenche plus, a cessé d'être un exemple.
- `AspNetCore.EnumMemberNameBinding.OpenApi`, un paquet compagnon dont l'unique point d'entrée,
  `AddEnumMemberNames()` sur `OpenApiOptions`, installe un transformateur de schéma qui fait décrire
  au document généré ce que le serveur accepte réellement : un type `string` explicite,
  les noms publics déclarés, et — pour les énumérations `[Flags]`, qu'ASP.NET Core documente sans
  aucune valeur — une expression régulière couvrant les combinaisons séparées par des virgules. Il
  décrit les énumérations que l'application a enregistrées et aucune autre : porter l'attribut n'est
  pas la même chose qu'être couvert, et une énumération que personne n'a enregistrée se lie par ses
  noms C# et se sérialise en nombre, donc annoncer ses noms déclarés serait faux pour la query string
  et pour le corps à la fois. Utilisé sans le paquet principal, il n'a aucun enregistrement à
  consulter et décrit toutes les énumérations sous contrat, ce qui est la forme voulue par une
  application qui sérialise avec ses propres convertisseurs. Ses tests vérifient la cohérence
  document/exécution en rejouant chaque valeur annoncée face au serveur en fonctionnement.
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

- **Une énumération imbriquée dans un type générique empêchait l'application de démarrer.**
  `Assembly.GetTypes()` livre une telle énumération sous sa forme ouverte — ``Box`1+Colour`` — où
  `Type.IsEnum` est vrai et `ContainsGenericParameters` l'est aussi, et `FieldInfo.GetValue` sur
  n'importe lequel de ses membres lève `ArgumentException: Specified type is not supported` depuis
  `Enum.InternalBoxEnum`. Cela se produit dans le constructeur d'`EnumContract`, avant même que le
  contrat soit consulté : `public class Box<T> { public enum Colour { Red } }` n'importe où dans
  l'assembly scannée suffisait donc à faire tomber toute l'application — une énumération que
  personne n'a annotée, ni enregistrée, ni voulue — avec un message ne nommant ni le type ni ce
  paquet. Le scan la laisse désormais passer, comme toute énumération qu'il ne peut pas lire. Nommer
  une forme fermée qui déclare un contrat reste enregistrable en la nommant —
  `AddEnum<Crate<int>.State>()` — puisqu'elle ne porte aucun paramètre générique ; `Box<T>.Colour`
  ci-dessus n'en déclare aucun, la nommer est donc refusé pour cette raison-là, comme toujours.
- **Le motif `[Flags]` du document OpenAPI se trompait de classe insensible à la casse, dans les deux
  sens.** Un membre non annoté garde son nom C#, que le binder reconnaît avec `OrdinalIgnoreCase`, et
  le motif écrivait cela comme les deux formes de casse du caractère. Ce n'est pas le même ensemble.
  Trop large sur cinq code points : `ToLowerInvariant` envoie U+212A KELVIN SIGN sur `k`, si bien
  qu'un membre nommé avec lui annonçait un `k` ordinaire auquel le serveur répond 400 — de même pour
  U+03F4, U+1E9E, U+2126 et U+212B. Trop étroit sur soixante-dix-neuf autres, où deux caractères sont
  égaux sans que l'un soit la casse de l'autre, comme U+00B5 MICRO SIGN face à U+03BC GREEK SMALL MU.
  Les deux découlent d'une seule règle, mesurée sur chaque `char` plutôt que déduite : deux caractères
  sont égaux au sens d'`OrdinalIgnoreCase` exactement quand `ToUpperInvariant` les envoie au même
  endroit, la classe est donc ce groupe. Un nom en ASCII ne change pas : `Delete` reste
  `[Dd][Ee][Ll][Ee][Tt][Ee]`.
- **Un nom mal cassé se résolvait vers le mauvais membre sur une énumération `[Flags]`.** De deux
  membres non annotés ne différant que par la casse, celui vers lequel se rabat un jeton qui ne
  correspond exactement à aucune des deux orthographes dépend de l'ordre dans lequel le sérialiseur
  détient ses membres — et cet ordre n'est pas le même sur les deux sortes d'énumération. Une
  énumération ordinaire suit l'ordre d'`Enum.GetNames` ; une `[Flags]` place d'abord ceux qui posent
  le plus de bits, de sorte qu'un composite l'emporte sur un membre qu'il recouvre. Ce paquet
  appliquait la première règle aux deux : sur `{ Read = 1, read = 3 }`, le corps de la requête lisait
  donc `"READ"` comme 3 là où tous les autres canaux lisaient 1. Le nombre de bits se compte sur la
  valeur **étendue en signe**, ce qui a été mesuré et non supposé : `-128` sur une énumération
  `sbyte` pose un bit de l'octet et cinquante-sept de la valeur élargie, et le sérialiseur en compte
  cinquante-sept. Douze formes ont été mesurées pour établir la règle ; quatre sont désormais des
  fixtures du corpus de parité dérivé, lequel a aussi trouvé la divergence sur le chemin des listes —
  une virgule finale suffit à basculer un jeton là où l'orthographe exacte ne l'emporte plus.
- **`EMN0004` refusait un contrat que `System.Text.Json` accepte.** La règle signalait une virgule
  dans un nom déclaré sur toutes les énumérations, et le contrôle au démarrage la refusait, sur
  l'idée qu'une virgule sépare des valeurs partout, donc qu'un nom qui en porte une ne peut jamais
  être relu. La première moitié est vraie, la seconde n'en découle pas : le sérialiseur cherche la
  valeur détourée **comme un seul nom avant de découper quoi que ce soit**. Mesuré — sur une
  énumération déclarant `a`, `b` et `a,b`, il répond `"a,b"` par le membre de ce nom et `"a, b"` par
  `a | b` ; il ne refuse la forme que sur une `[Flags]`, ce que son propre message énonce :
  *« Flags enums must **additionally** not contain commas »*. Ce paquet était donc plus strict que
  l'énumération laissée tranquille, ce qu'`EnumContract` qualifie lui-même de seule chose qu'il
  promet de ne jamais faire. `EMN0004` et le contrôle au démarrage s'arrêtent désormais là où le
  sérialiseur s'arrête, et `TryParse` essaie la valeur entière comme nom avant de découper — un
  changement sans effet sur aucun contrat légal auparavant, puisqu'aucun ne pouvait porter de
  virgule. L'ancien ordre ne se contentait pas de refuser la forme : sur l'énumération ci-dessus, il
  lisait `"a,b"` comme `a | b`, un autre membre, silencieusement. Deux fixtures du corpus de parité
  dérivé le tiennent désormais face au sérialiseur.
- **Une énumération sous contrat nullable perdait le `null` de son schéma OpenAPI.** Un seul
  composant décrit le type partout où il apparaît, et un élément de collection nullable —
  `List<TEnum?>` — n'est pas encapsulé comme l'est une propriété nullable : ASP.NET Core l'exprime en
  plaçant un null JSON dans l'`enum` du composant lui-même. Remplacer cette liste par les noms
  déclarés supprimait ce null, et le type `string` apposé par-dessus l'interdisait purement et
  simplement — le document refusait donc une valeur que le serveur accepte et renvoie, mesuré sur un
  corps qui répond 200 à `["available",null,"sold"]`. Le transformateur lit désormais la nullabilité
  sur le schéma qu'il remplace et conserve les deux : `"type": ["null","string"]`, et le null à côté
  des noms. Un schéma qui n'admet aucun null ne gagne ni l'un ni l'autre.
- **Une liste séparée par des virgules résolvait un membre non annoté autrement que le corps de la
  requête.** `System.Text.Json` ne privilégie l'orthographe exacte d'un nom C# que si la valeur ne
  porte aucune virgule ; dans une liste, chaque partie passe par une unique recherche insensible à
  la casse — une simple virgule finale fait donc basculer d'une règle à l'autre : sur
  `{ Read = 2, read = 4 }`, le sérialiseur lit `"read"` comme 4 et `"read,"` comme 2. Ce paquet
  appliquait la règle de l'orthographe exacte aux deux chemins : `?value=read,one` liait donc 5 là
  où le corps liait 3. Le chemin liste s'aligne désormais. Les noms déclarés ne changent pas : ils
  sont ordinaux des deux côtés.
- **Un enregistrement en échec pouvait tout de même modifier une application en fonctionnement.** Les
  énumérations sous contrat étaient inscrites dans le registre que le model binder provider consulte à
  chaque requête *avant* que la collection de services ne soit configurée — si bien qu'un appel
  effectué après `Build()`, où cette collection est en lecture seule, levait
  `InvalidOperationException` en les ayant déjà inscrites. L'application liait alors ces énumérations
  par leurs noms déclarés et les sérialisait en nombres, aucun convertisseur ne les ayant accompagnées :
  exactement la divergence liaison/sérialisation que ce paquet existe pour supprimer, produite par un
  appel dont l'appelant s'était vu dire qu'il avait échoué. Le registre est désormais rempli en
  dernier, une fois qu'il ne reste plus rien qui puisse lever — ce que le code affirmait déjà :
  « l'enregistrement n'a pas eu lieu » doit être vrai, et non presque vrai.
- **Le motif `[Flags]` du document OpenAPI ne décrivait exactement ni l'une ni l'autre moitié du
  vocabulaire.** Un membre laissé sans annotation garde son nom C#, que le binder reconnaît sans tenir
  compte de la casse, alors que le motif le listait tel quel — `delete`, `DELETE` et `read, delete`
  étaient donc exclus du document là où le serveur liait les trois. Un nom non annoté s'écrit
  désormais `[Dd][Ee][Ll][Ee][Tt][Ee]` ; un nom déclaré est reconnu de façon ordinale et reste
  littéral, si bien qu'une mauvaise casse en est toujours exclue.
- **Ce même motif utilisait `\s`, qui n'est pas l'espace que le binder détoure.** Un `pattern` de JSON
  Schema est lu comme de l'ECMA-262, où `\s` prend U+FEFF et laisse U+0085, tandis que `String.Trim`
  — c'est-à-dire `char.IsWhiteSpace` — fait l'inverse sur les deux. Le document se trompait donc dans
  les deux sens à la fois : il annonçait une valeur commençant par U+FEFF à laquelle le serveur
  répond 400, et excluait une valeur commençant par U+0085 qu'il lie. Les vingt-cinq points de code
  sont désormais écrits en toutes lettres. Les tests du dépôt ne pouvaient pas le voir, car ils
  lisent le motif avec `System.Text.RegularExpressions`, dont le `\s` coïncide justement avec `Trim`
  sur ces deux-là.
- **De deux membres non annotés ne différant que par la casse, l'un était inatteignable.** Les noms
  C# tenaient dans un unique dictionnaire insensible à la casse : `Read` et `read` entraient donc en
  collision et le second était perdu — le jeton qui le nommait exactement se résolvait alors vers le
  premier. `System.Text.Json` fait correspondre l'orthographe exacte avant de se rabattre, si bien
  que la query string et le corps de la requête répondaient au même mot par deux valeurs
  différentes, sur une énumération enregistrée avec `AllowPartialContracts`. Le nom exact est
  désormais essayé d'abord, et seule une casse ne correspondant exactement à aucun se rabat — vers le
  membre que choisit le sérialiseur, c'est-à-dire le premier dans l'ordre de `Enum.GetNames`, qui
  n'est ni le premier déclaré ni celui de plus petite valeur. Les noms déclarés ne changent pas et
  restent sensibles à la casse.
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
- **Deux membres partageant une valeur numérique pouvaient être écrits sous deux noms différents dans
  une même application.** La table valeur vers nom était bâtie dans l'ordre de déclaration, là où
  `System.Text.Json` la bâtit dans l'ordre de `Enum.GetNames` — qui trie par la valeur binaire et,
  entre membres partageant la même, ne conserve pas l'ordre d'écriture. Un corps de réponse disait
  donc `shipped` pendant qu'un lien construit avec `EnumMemberNames.GetPublicName` pour la même
  valeur disait `in_transit`. La table est désormais lue sur `Enum.GetNames`, si bien que les deux
  s'accordent par construction ; sept formes ont été mesurées face à `JsonSerializer` et trois
  divergeaient sous l'ancienne règle. La lecture est inchangée — les deux noms se sont toujours
  relus vers la même valeur — de même que l'ordre de déclaration dont sont tirés le document OpenAPI
  et la phrase des valeurs autorisées.
- **Une énumération sous contrat `[Flags]` liait une combinaison qu'ASP.NET Core refuse**, la seule
  chose que ce paquet promet de ne jamais faire. Le binder prenait `[Flags]` pour une dispense du
  contrôle de valeur non déclarée et répondait oui sans demander, au motif qu'une valeur bâtie en
  combinant des membres déclarés se décompose en eux par construction. C'est faux : deux composites
  déclarés qui se recouvrent peuvent couvrir un bit qu'aucun membre ne fournit seul, si bien qu'une
  énumération déclarant `3` et `6` liait `read_write,write_delete` en `7` là où la même énumération
  laissée intacte répond 400. Il exécute désormais le test qu'exécute `EnumTypeModelBinder` — une
  valeur qui se décompose écrit les noms de ses membres, une qui ne se décompose pas renvoie son
  nombre — et la suite de parité l'épingle avec, pour témoin, une énumération intacte de même forme.
- **Le motif `[Flags]` du document OpenAPI excluait des formes que le binder accepte** — les espaces
  en tête et en fin, et la virgule finale. Le document annonçait un contrat plus strict que celui que
  le serveur honorait.
- **Les help links des analyseurs pointaient vers des pages inexistantes**, si bien que le lien
  de l'IDE menait à un 404. Chaque règle a désormais une page sous `docs/for-users/rules`, et un test échoue si
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
- **Enregistrer deux fois la même énumération empilait un second enregistrement à chaque fois.** Un
  seul fournisseur de binder est désormais installé par application, quel que soit le nombre d'appels,
  tandis que la validation s'exécute toujours à chaque appel, de sorte qu'un second enregistrement
  avec des options plus strictes échoue encore. Couvert par des tests qui hébergent plusieurs
  applications côte à côte, inscrites et non inscrites.
- **L'installation en un seul paquet du compagnon OpenAPI, telle que documentée, ne compilait pas.**
  `Microsoft.AspNetCore.OpenApi` active l'espace de noms d'intercepteurs dans lequel écrit son
  générateur de commentaires XML, et il le fait via des assets MSBuild `build`, que NuGet ne propage
  pas en transitif. Un consommateur qui prenait le compagnon et rien d'autre — exactement ce
  qu'indique `docs/for-users/openapi.fr.md` — héritait donc du générateur sans la propriété qui rend sa sortie
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
- Un `JsonStringEnumConverter` que l'application avait enregistré avant `AddEnumMemberNameBinding()`
  décidait de ce qu'une énumération sous contrat acceptait dans le corps de la requête.
  `System.Text.Json` retient le premier convertisseur de la liste dont `CanConvert` répond vrai, et ce
  paquet ajoutait le sien à la fin ; or le convertisseur standard a pour défaut
  `allowIntegerValues: true`, si bien qu'un corps `{"status": 1}` était accepté là où `?status=1`
  répondait 400 — exactement la divergence que ce paquet existe pour supprimer, réintroduite par une
  application qui demandait des énumérations en chaînes. Les convertisseurs sont désormais insérés en
  tête des deux objets d'options. Cela règle aussi la question de l'ordre : un convertisseur
  enregistré *après* cet appel est ajouté à la fin et se retrouve derrière eux de toute façon, si bien
  que le vocabulaire ne dépend plus de celui des deux appels qui a été écrit en premier. Une
  application qui tient à son propre convertisseur pour une énumération sous contrat décline toujours
  cette moitié avec `ConfigureJsonSerialization`, ce qui laisse la liaison en place.

### Documentation

- **`EMN0005` désignait le mauvais membre quand l'annotation vient en second.** Le message de
  l'analyseur comme celui d'`EnumContractException` disaient que le nom déclaré est cherché en
  premier, donc que la valeur résout vers le membre annoté. Mesuré dans les deux sens : sur
  `{ ["Blue"] Red = 0, Blue = 1 }` la valeur `Blue` lit `Red`, et sur `{ Blue = 0, ["Blue"] Red = 1 }`
  elle lit `Blue`. Ce qui décide est l'ordre d'`Enum.GetNames` — le premier membre rencontré s'empare
  de l'orthographe, que son nom soit le nom déclaré ou le sien — si bien que déplacer l'annotation
  inverse quel membre disparaît. Les messages disent désormais que les deux répondent à
  l'orthographe et que l'ordre d'`Enum.GetNames` tranche, sans désigner de gagnant.
- **La description d'`EMN0004` donnait un mécanisme que les tests du dépôt réfutent.** Elle disait
  « where the whole name is never looked up before the value is split » ; `TryParse` cherche d'abord
  la valeur détourée entière, sans exception pour `[Flags]`. La raison est celle que donne la page de
  la règle : le sérialiseur valide les noms déclarés à la construction du convertisseur et refuse
  purement et simplement une virgule sur une `[Flags]`, l'énumération ne pouvant alors pas être
  sérialisée du tout.
- **`EMN0001` se trompait sur les deux moitiés de sa propre justification.** Elle affirmait que
  `System.Text.Json` rejette un nom public dupliqué — il l'accepte, lit le nom comme le membre
  premier dans l'ordre d'`Enum.GetNames` et écrit les deux membres sous ce nom, en silence — et
  décrivait ce paquet comme retenant la première déclaration, là où le contrat est refusé purement et
  simplement à la compilation et au démarrage. Dans les deux langues.
- **`contract-rules` conservait une affirmation que sa page sœur avait déjà abandonnée.** Le 400 sur
  une combinaison non déclarée est le refus de ce paquet, qui reproduit celui d'ASP.NET Core, et
  c'est l'une des entrées que le corps accepte et qu'un autre canal refuse — pas « la seule ».
  `limitations` a été corrigée une note de version plus tôt ; les deux pages se contredisaient
  depuis.
- **Le message d'`EMN0005` était faux sur la moitié des formes qu'il signale.** La formulation de
  l'analyseur comme celle d'`EnumContractException` disaient le membre masqué « atteignable
  uniquement par une autre casse ». C'est vrai quand le nom déclaré s'écrit comme le nom C# qu'il
  masque, et faux sinon : sur `[JsonStringEnumMemberName("blue")]` à côté d'un membre `Blue`, `Blue`
  répond toujours à `Blue` et c'est `blue` qu'il perd. Le membre perd l'orthographe **déclarée** et
  conserve toutes les autres casses, ce qui est vrai dans les deux cas. Mesuré face à
  `System.Text.Json`, puisque ce paquet refuse les deux formes et n'a aucun contrat à interroger —
  ce qui explique aussi qu'un message décrivant une forme que rien ne peut construire soit resté
  invérifié. Les pages de règles, elles, étaient justes : elles l'énoncent de leur propre exemple,
  celui à casse identique, et décrivent correctement l'image miroir.
- **Le binder qualifiait le refus d'une valeur non déclarée de « seule entrée où un canal et le corps
  divergent ».** Le paragraphe juste en dessous le contredisait déjà, et la suite de tests aussi, à
  deux méthodes d'écart : la moitié `[Flags]` est une deuxième entrée, refusée par un autre test, et
  un nom déclaré portant un caractère qu'une route ou un en-tête ne sait pas transporter en est une
  troisième — ce qu'`EMN0006` existe pour signaler.
- **`EMN0001` affirmait encore que le premier alias déclaré est utilisé en écriture.** C'est le
  premier dans l'ordre d'`Enum.GetNames`, qui n'est ni l'ordre de déclaration ni l'ordre arithmétique
  — la correction apportée à la table valeur-vers-nom n'avait atteint cette page dans aucune des deux
  langues.
- **Le README disait qu'ASP.NET Core formate les valeurs de route *sans* le `ToString()` de la
  valeur**, puis expliquait que le lien porte de ce fait le nom C# — qui est précisément ce que
  `ToString()` renvoie. Il les formate *avec* : c'est toute la raison d'être du contournement.
- **`limitations` attribuait le refus à `EnumTypeModelBinder` et le disait « non atteignable
  d'ici ».** Ce paquet enregistre son binder devant le fournisseur qu'ASP.NET Core utilise pour les
  énumérations : `EnumTypeModelBinder` ne voit donc jamais la valeur, le contrôle est reproduit ici
  délibérément, et le refus qu'un appelant rencontre est celui de ce paquet.
- **`EnumContractException` se documentait comme « levée au démarrage, jamais sur une requête ».** Le
  compagnon OpenAPI résout un contrat au moment d'écrire le document, ce qui sous `MapOpenApi` est une
  requête — une application utilisant le compagnon seul, sans `AddEnumMemberNameBinding`, démarre donc
  normalement et répond 500 sur `/openapi/v1.json` pour une énumération malformée. Les analyseurs ne
  comblent pas cet écart non plus, NuGet ne propageant pas les assets d'analyseur de façon transitive.
  Le type dit désormais où il est levé, et `openapi.md` nomme la conséquence de la configuration
  autonome qu'il documentait déjà comme supportée. Le comportement ne change pas : échouer bruyamment
  sur un contrat malformé est l'intention.
- **La section trimming rangeait `AddEnumMemberNameBinding` dans le mauvais groupe.** Elle donnait
  l'enregistrement MVC comme portant `[RequiresUnreferencedCode]` seul, alors que le point d'entrée
  porte `[RequiresDynamicCode]` depuis son écriture — il le doit, puisqu'il atteint la construction
  des convertisseurs JSON génériques, que la même phrase désigne comme exigeant les deux. Corrigé
  dans les deux langues, qui portaient la même affirmation.
- Le README a été scindé. Il avait atteint une longueur que personne ne lit avant d'adopter un
  paquet : la page d'accueil porte désormais le problème, l'installation, un exemple, le tableau des
  canaux, les garanties et les deux limitations à connaître avant d'adopter — le reste a été déplacé,
  sans coupe, vers `docs/for-users/contract-rules.en.md`, `docs/for-users/analyzers.en.md`,
  `docs/for-users/openapi.en.md` et `docs/for-users/limitations.en.md`. Le README est aussi la page NuGet du paquet, où un lien relatif est mort :
  il pointe donc vers GitHub en absolu. Un test échoue sur un lien relatif, et sur tout lien — dans
  n'importe quelle page — visant un fichier ou un titre inexistant.
- La documentation est désormais bilingue, suivant la convention utilisée dans les projets Reefact :
  chaque page existe en `Xxx.en.md` et `Xxx.fr.md` sous `docs/for-users`, et s'ouvre sur un lien vers sa
  contrepartie. Le README garde son nom et sa place, puisque NuGet le rend ; sa version française est
  `README.fr.md`, et le changelog suit la même règle. Des tests échouent sur une page qui
  n'existe que dans une langue, sur une page qui n'offre pas l'autre, et sur une traduction dont la
  structure ne correspond plus à celle de l'original — les mots sont traduits, les sections, les
  puces, les lignes de tableau et les extraits ne sont ni retirés ni ajoutés. C'est ce dernier point
  qui attrape une entrée ajoutée à un seul des deux changelogs. Les help links des analyseurs
  pointent vers les pages de règles anglaises, qui font foi.

- Les pages sont rangées selon qui les lit : `docs/for-users` pour la documentation que lit un
  consommateur, `docs/for-maintainers` pour les enregistrements de décision. Cette séparation est
  aussi ce que lisent les suites — le contrat de compilation couvre `for-users` et rien d'autre, si
  bien qu'une page de mainteneur écrite demain en sort sans que personne ait à l'exclure. Les help
  links des analyseurs ont suivi les pages de règles qu'ils visent, vers `docs/for-users/rules/`.

- Chaque dossier de documentation porte un index — le `README.md` que GitHub affiche quand on
  l'ouvre — si bien que naviguer dans l'arborescence ne dépend jamais de deviner un nom de fichier.
  Un index est la seule page dont tout le travail est d'être complète, et la seule dont rien d'autre
  ne verrait qu'elle a décroché : un test tient donc chacun au dossier dont il parle, et une page
  ajoutée à côté d'un index qui ne la liste pas casse le build. Le jumeau français de la page
  d'accueil est remonté à la racine du dépôt, à côté de l'anglais, parce que `README.fr.md` sous
  `docs/for-users` occupait le nom dont l'index de ce dossier avait besoin.

### Limitations connues

- **Les Minimal APIs ne sont pas couvertes.** La liaison de leurs paramètres n'utilise ni les model
  binders de MVC ni `TypeDescriptor` ; elle exige un `static TryParse`/`BindAsync` sur le type lié, ce
  qu'on ne peut pas ajouter à une `enum`. C'est une contrainte de la plateforme, pas un manque
  d'implémentation.
- **Une valeur vide sur un paramètre d'énumération nullable lie `null`** au lieu d'être rejetée, là où
  `System.Text.Json` rejette `""`. ASP.NET Core tranche une valeur vide avant qu'aucune analyse ne soit
  atteinte. Un test épingle le comportement.
- **Une combinaison qui ne nomme aucun membre se lie dans le corps et nulle part ailleurs.**
  `"out_of_stock,discontinued"` vaut `(ProductStatus)3`, que `System.Text.Json` accepte et que le
  binder d'ASP.NET Core refuse sur une énumération sans `[Flags]` — y compris sur une énumération que
  ce paquet ne touche jamais, ce qui rend sa fermeture contraire au sens voulu. Caractérisée, témoin
  compris.
- **Un paramètre d'énumération sous contrat n'écrit aucun des enregistrements propres au binder.**
  Le `SimpleTypeModelBinder` d'ASP.NET Core reçoit un `ILoggerFactory` et journalise sa tentative et
  son résultat ; le binder installé ici ne prend aucun logger, si bien qu'un tel paramètre est muet en
  `Debug` là où tous les autres ne le sont pas. Seuls ces enregistrements manquent — le trace de
  `ParameterBinder` qui les entoure appartient à ASP.NET Core et reste intact, un journal montre donc
  toujours que le paramètre a été lié puis validé. Ils passent par `MvcCoreLoggerExtensions`, qui est
  `internal` : les reproduire reviendrait à un sosie sous la catégorie et les identifiants
  d'événement de ce paquet, une parité d'apparence et aucune en fait. Les deux moitiés sont mesurées.
- **Incompatible avec le trimming et Native AOT.** Résoudre un contrat et scanner un assembly
  reposent sur la réflexion. Le point d'entrée public est annoté en conséquence, plutôt que de
  supprimer silencieusement les avertissements.
- L'enregistrement doit avoir lieu au démarrage : ASP.NET Core met en cache le model binder construit
  pour un type à la première utilisation.
