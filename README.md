# Mots

[![CI](https://github.com/ymauray/mots/actions/workflows/ci.yml/badge.svg)](https://github.com/ymauray/mots/actions/workflows/ci.yml)
[![Licence : MIT](https://img.shields.io/github/license/ymauray/mots)](LICENSE)
[![repocheck](https://img.shields.io/badge/repocheck%201.3.0-100%2F100-brightgreen)](https://github.com/ymauray/repocheck)

Mots est un petit traitement de textes pour Windows, pensé pour écrire des romans. Tout y est piloté par les **styles** : pas de police, de taille, d'alignement ni de liste à régler à la main, seulement le style du paragraphe. Les documents sont de vrais fichiers Word (`.docx`).

Site : <https://ymauray.github.io/mots/>

## Fonctionnalités

- Trois styles de paragraphe : **Normal**, **Titre 1** (un chapitre par page) et **Ellipse** (séparateur de scène).
- Plan des chapitres à gauche, pour naviguer d'un clic, ou avec `Ctrl+PageSuivante` / `Ctrl+PagePrécédente`.
- Fluide sur un manuscrit entier : l'éditeur n'affiche que le chapitre du curseur et ses voisins, tout le reste (enregistrement, recherche, comptage, impression) porte sur le manuscrit complet.
- Import d'un manuscrit Word existant (les styles inconnus de Mots sont signalés au lieu d'être perdus en silence).
- Typographie française : espace insécable (`Ctrl+Maj+Espace`), espace insécable fine (`Ctrl+Alt+Maj+Espace`), espace insécable automatique après le tiret cadratin d'un dialogue.
- Affichage des caractères non imprimables (`Ctrl+Maj+8`).
- Recherche et remplacement, zoom, impression, liste des documents récents.

## Prérequis

- Windows 10 ou 11, 64 bits.
- Pour compiler : le [SDK .NET 10](https://dotnet.microsoft.com/download).

## Installation

Téléchargez `Mots.exe` depuis la page des [releases](https://github.com/ymauray/mots/releases). Le programme est autonome : il n'y a rien d'autre à installer.

Pour le compiler vous-même :

```sh
dotnet publish Mots.slnx -c Release
```

L'exécutable se trouve dans `Mots.App/bin/Release/net10.0-windows/win-x64/publish/Mots.exe`.

## Utilisation

Lancez `Mots.exe`, puis choisissez le style du paragraphe dans la barre d'outils (ou avec `Ctrl+0` pour Normal, `Ctrl+1` pour Titre 1). Enregistrez avec `Ctrl+S` : le fichier produit est un `.docx` que Word ouvre normalement. Mots n'a pas de correcteur orthographique intégré (il ralentissait la frappe) : utilisez Antidote ou l'outil de votre choix. Mots n'ouvre que les documents qu'il a lui-même enregistrés ; pour reprendre un document Word existant, utilisez **Importer** (`Ctrl+Maj+O`).

## Développement

```sh
dotnet build Mots.slnx
dotnet test Mots.Tests
dotnet run --project Mots.App -c Release   # en Debug, la frappe est nettement plus lente
```

Voir [CONTRIBUTING.md](CONTRIBUTING.md) pour les conventions du projet.

## Intégration continue et releases

Tout est assuré par les workflows GitHub Actions du dépôt, sans outil externe :

- [`ci.yml`](.github/workflows/ci.yml) compile la solution et exécute les tests sur un runner Windows, à chaque push et pull request vers `main`.
- [`release.yml`](.github/workflows/release.yml) se déclenche sur un tag `vX.Y.Z` : il publie `Mots.exe` (autonome, win-x64) et le joint à une release GitHub.
- [Dependabot](.github/dependabot.yml) propose chaque semaine les mises à jour des paquets NuGet et des actions.

## Licence

[MIT](LICENSE)
