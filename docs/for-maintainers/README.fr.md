# Documentation mainteneur

🌍 **Langues :**  
🇫🇷 Français (ce fichier) | 🇬🇧 [English](README.md)

Écrite pour quiconque modifie ce dépôt, ce qui veut dire aujourd'hui les enregistrements de
décision. Un enregistrement dit ce qui a été décidé, contre quoi cela a été pesé, et ce qui le
renverserait — pour qu'un choix fait une fois n'ait pas à être réargumenté de mémoire chaque fois
qu'on le questionne.

Deux choses y sont délibérées. Ses pages citent du C# pour porter un argument plutôt que pour
enseigner : c'est donc le seul corpus que le contrat de compilation ne couvre pas, et la raison vit
dans `DocumentationCorpus`. Et les conventions qu'un contributeur suit au quotidien ne sont pas ici
mais dans [CONTRIBUTING](../../CONTRIBUTING.md), que GitHub rend depuis la racine du dépôt.

## Enregistrements de décision

| Enregistrement | Ce qu'il tranche |
|---|---|
| [ADR 0001 — NFluent pour les assertions de test](adr/0001-nfluent-for-test-assertions.fr.md) | pourquoi les assertions se lisent `Check.That(…)` alors que xUnit découvre et exécute toujours les tests |
