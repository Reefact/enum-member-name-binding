# Diagnostics

🌍 **Langues :**  
🇫🇷 Français (ce fichier) | 🇬🇧 [English](README.md)

Une page par règle, et c'est là que pointe le lien d'aide du diagnostic — un lecteur y arrive donc
généralement depuis une erreur de build plutôt que depuis cet index. Chaque page dit ce que la règle
attrape, pourquoi c'est important, comment corriger, et si la supprimer est parfois justifié.

Une énumération qui ne déclare aucun `[JsonStringEnumMemberName]` n'est jamais analysée : ajouter ce
paquet à une solution existante n'allume donc rien qui ne le concerne pas. La version narrative du
tableau ci-dessous est dans [Analyseurs](../analyzers.fr.md).

| Règle | Sévérité | Ce qu'elle attrape |
|---|---|---|
| [EMN0001](EMN0001.fr.md) | Erreur | Deux membres d'énumération déclarent le même nom public |
| [EMN0002](EMN0002.fr.md) | Erreur | Le nom public est vide ou entouré d'espaces |
| [EMN0003](EMN0003.fr.md) | Erreur | Le contrat de l'énumération est incomplet |
| [EMN0004](EMN0004.fr.md) | Erreur | Un nom public `[Flags]` contient une virgule |
| [EMN0005](EMN0005.fr.md) | Erreur | Un nom public masque le nom C# d'un autre membre |
| [EMN0006](EMN0006.fr.md) | Avertissement | Le nom public ne peut pas voyager sur tous les canaux d'entrée |
