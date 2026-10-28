# AGENTS.md

Mots est un petit traitement de textes WPF (.NET 10, Windows) pour écrire des romans. Les documents sont de vrais `.docx`. Principe central : **tout est piloté par les styles** (Normal, Titre 1, Ellipse) ; il n'y a ni police, ni taille, ni alignement, ni liste à régler à la main.

## Commandes

```sh
dotnet build Mots.slnx
dotnet test Mots.Tests
dotnet publish Mots.slnx -c Release   # Mots.App/bin/Release/net10.0-windows/win-x64/publish/Mots.exe (autonome, fichier unique)
```

Pas de linter. Si `Mots.exe` tourne en Debug, il verrouille la compilation : lancer les tests avec `-c Release`.

## Architecture

- `Mots.App/` : l'application (assembly `Mots`).
  - `ParagraphStyles.cs` : source unique des styles, pour l'éditeur (WPF) comme pour le `.docx`.
  - `DocxConverter.cs` : `Save`, `Load` (marqueur Mots exigé) et `Import` (document Word quelconque ; les styles inconnus sont rejetés avec un message explicite).
  - `MotsMetadata.cs` : marqueur de compatibilité (partie Custom XML `urn:mots:metadata`, `formatVersion`).
  - `MainWindow*.cs` / `MainWindow.xaml` : fenêtre principale en classe partielle (plan, zoom, typographie, récents).
  - `FormattingMarksOverlay.cs` : dessin des caractères non imprimables ; `RecentFiles.cs` : documents récents.
- `Mots.Tests/` : tests NUnit, exécutés en STA (WPF). `WindowHarness` pilote la vraie fenêtre par réflexion.

## Conventions

- Interface, messages et commentaires en français ; identifiants en anglais. Commits en français.
- Pas de mise en forme manuelle : toute nouvelle mise en forme passe par un style de `ParagraphStyles`.
- Nettoyer le code mort ; commiter par petites étapes validées.
- `Référence/` (manuscrit de travail) est ignoré par git : ne jamais le lire pour autre chose qu'une analyse, ne jamais le modifier ni le commiter.
- Fins de ligne : LF dans le dépôt, CRLF dans le répertoire de travail (`.gitattributes`).

## Pièges

- Les objets WPF exigent un thread STA : les tests le déclarent au niveau de l'assembly.
- Les tests de clavier lisent l'état réel de Maj et Ctrl : ne pas les presser pendant l'exécution.
- Un projet qui référence `Mots.App` doit être autonome win-x64 (`SelfContained`, sinon erreur NETSDK1151).
