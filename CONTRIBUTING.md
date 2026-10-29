# Contribuer à Mots

Merci de vous intéresser à Mots ! Ce document résume comment proposer une contribution.

## Avant de commencer

Pour une nouvelle fonctionnalité ou un changement important, ouvrez d'abord une issue ou une [discussion](https://github.com/ymauray/mots/discussions) pour en parler avec le mainteneur. Le principe du projet est que **tout est piloté par les styles** : une fonctionnalité qui ajoute de la mise en forme manuelle (police, taille, alignement, listes) ira à l'encontre de ce principe.

## Environnement de développement

- Windows 10 ou 11 (l'application est une application WPF).
- [SDK .NET 10](https://dotnet.microsoft.com/download).

```sh
dotnet build Mots.slnx
dotnet test Mots.Tests
dotnet run --project Mots.App -c Release   # en Debug, la frappe est nettement plus lente : juger la fluidité en Release
```

Les tests créent de vraies fenêtres WPF (hors écran) et simulent des touches : évitez d'appuyer sur Maj ou Ctrl pendant leur exécution.

## Proposer une modification

1. Créez une branche dédiée depuis `main` (ex. `fix/...`, `feature/...`).
2. Faites des commits atomiques, avec des messages clairs en français.
3. Vérifiez que les tests passent en local, et ajoutez-en pour tout comportement nouveau ou corrigé.
4. Ouvrez une pull request vers `main` en remplissant le modèle fourni. Les PR sont mergées en squash.

## Conventions

- Le code et ses commentaires sont en français pour tout ce qui touche à l'interface et au domaine ; les identifiants sont en anglais.
- Nettoyez le code mort : pas de code commenté ni de fonctionnalités à moitié retirées.
- Les fins de ligne sont normalisées par `.gitattributes` (LF dans le dépôt, CRLF dans le répertoire de travail).
- Ne commitez jamais le dossier `Référence/` (documents de travail, ignoré par `.gitignore`).
