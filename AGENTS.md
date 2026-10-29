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
  - `MainWindow*.cs` / `MainWindow.xaml` : fenêtre principale en classe partielle (chapitres, plan, zoom, typographie, récents).
  - `MainWindow.Chapters.cs` : le manuscrit est découpé en chapitres (un « Titre 1 » et son texte). `Editor.Document` n'est qu'une **fenêtre** sur le chapitre du curseur et ses voisins ; les autres chapitres sont à l'écart (`_before`, `_after`). Pour tout le manuscrit, utiliser `AllDocuments` / `AllParagraphs`, jamais `Editor.Document`.
  - `FormattingMarksOverlay.cs` : dessin des caractères non imprimables ; `RecentFiles.cs` : documents récents.
- `Mots.Tests/` : tests NUnit, exécutés en STA (WPF). `WindowHarness` pilote la vraie fenêtre par réflexion.

## Conventions

- Interface, messages et commentaires en français ; identifiants en anglais.
- Pas de mise en forme manuelle : toute nouvelle mise en forme passe par un style de `ParagraphStyles`.
- Nettoyer le code mort ; avancer par petites étapes, validées une à une par l'utilisateur.
- **Commits : uniquement sur demande de l'utilisateur.** Ne pas demander s'il faut commiter après chaque étape ; attendre qu'il le dise. Messages en français.
- `main` est protégée (check `build` requis, pas de push direct, merge en squash) : tout passe par une pull request.
- L'état d'avancement et la suite prévue sont dans [`ROADMAP.md`](ROADMAP.md) : le lire en début de session, le tenir à jour.
- `Référence/` (manuscrit de travail) est ignoré par git : ne jamais le lire pour autre chose qu'une analyse, ne jamais le modifier ni le commiter.
- Fins de ligne : LF dans le dépôt, CRLF dans le répertoire de travail (`.gitattributes`).

## Pièges

- WPF met en page tout le document de l'éditeur à chaque frappe : ne jamais y remettre le manuscrit entier, ni ajouter de travail proportionnel à sa taille dans un gestionnaire de frappe (le recomptage et le plan sont différés de 250 ms). Juger la fluidité en Release (`dotnet run -c Release`), pas en Debug.
- Déplacer des blocs dans la fenêtre de l'éditeur déclenche `TextChanged` : passer par `WhileSwitching` pour ne pas marquer le document modifié. Le correcteur orthographique de WPF est volontairement désactivé (coût à la frappe).
- Les objets WPF exigent un thread STA : les tests le déclarent au niveau de l'assembly.
- Les tests de clavier lisent l'état réel de Maj et Ctrl : ne pas les presser pendant l'exécution.
- Un projet qui référence `Mots.App` doit être autonome win-x64 (`SelfContained`, sinon erreur NETSDK1151).
