# Politique de sécurité

🌍 **Langues :**  
🇬🇧 [English](../SECURITY.md) | 🇫🇷 Français (ce fichier)

## Versions prises en charge

Les correctifs de sécurité sont produits sur la dernière version stable. Mettez-vous à jour avant de
signaler, afin qu'un rapport décrive quelque chose de toujours présent.

| Version | Prise en charge |
| --- | --- |
| Dernière version stable | Oui |
| Versions antérieures | Non |
| Préversions | Au mieux |

## Signaler une vulnérabilité

Merci de ne pas ouvrir d'issue, de discussion ni de pull request publique pour une vulnérabilité
suspectée. Utilisez plutôt le signalement privé de GitHub :

[Ouvrir un avis de sécurité privé](https://github.com/Reefact/enum-member-name-binding/security/advisories/new)

Incluez autant des éléments suivants que vous avez :

- le paquet et la version concernés ;
- l'environnement dans lequel vous l'avez observée ;
- en quoi consiste la vulnérabilité, et ce qu'elle permet de faire ;
- les étapes pour la reproduire ;
- une preuve de concept minimale, si en écrire une est raisonnable ;
- toute atténuation ou tout contournement que vous connaissez déjà ;
- si elle a été divulguée publiquement quelque part.

N'incluez ni secrets, ni données personnelles, ni jetons d'accès, ni rien appartenant à un tiers.

## À quoi vous attendre

- un accusé de réception sous 3 jours ouvrés ;
- une première évaluation sous 7 jours ouvrés ;
- un point d'avancement au moins tous les 14 jours tant que le sujet reste ouvert ;
- un correctif et une divulgation coordonnée sous 90 jours lorsque c'est raisonnablement possible.

La gravité, la complexité et la disponibilité d'un correctif sûr peuvent déplacer ces échéances.
Tout changement significatif sera discuté avec vous plutôt que décidé en silence. Merci de garder le
rapport confidentiel jusqu'à ce qu'un correctif ou une atténuation soit disponible.

## Périmètre

Cette bibliothèque se tient sur le chemin des requêtes : elle transforme du texte arrivant par une
route, une chaîne de requête, un champ de formulaire ou un en-tête en valeur d'énumération. Les
rapports portant sur cette frontière sont ceux qui comptent le plus ici. Exemples de ce qui
qualifie :

- une entrée qui se lie à une valeur que le contrat déclaré n'autorise pas ;
- un moyen de contourner la validation d'un contrat au démarrage ;
- l'exposition d'informations qu'un appelant ne devrait pas voir ;
- l'exécution de code arbitraire ou non voulue ;
- une vulnérabilité dans la façon dont le paquet est construit, signé ou publié ;
- une faiblesse de chaîne d'approvisionnement introduite par ce projet.

Généralement pas des vulnérabilités de sécurité :

- les bogues ordinaires sans impact de sécurité ;
- les demandes de fonctionnalité et les erreurs de documentation ;
- les problèmes reproductibles uniquement sur une version non prise en charge ;
- les vulnérabilités de dépendances que ce paquet n'expose pas réellement.

Tout ce qui relève de cette seconde liste est bienvenu en issue publique.

## Divulgation

Une fois un rapport confirmé, un avis privé peut être ouvert pour coordonner le correctif. Une fois
un correctif ou une atténuation disponible, un avis peut être publié contenant :

- ce qu'était la vulnérabilité et ce qu'elle permettait ;
- les versions affectées et corrigées ;
- les atténuations ou contournements disponibles ;
- les instructions de mise à jour ;
- un identifiant CVE lorsque c'est pertinent ;
- le crédit au rapporteur, sauf s'il préfère rester anonyme.

La divulgation publique devrait normalement suivre, et non précéder, une version qui corrige le
problème. Il n'y a pas de programme de prime ; les chercheurs signalant de bonne foi sont crédités
dans l'avis.
