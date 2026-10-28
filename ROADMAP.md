# Feuille de route

État d'avancement de Mots et suite prévue. À tenir à jour à chaque étape validée.

## Fait (v0.1.0)

- Styles de paragraphe Normal, Titre 1 et Ellipse, source unique pour l'éditeur et le `.docx`.
- Enregistrement et ouverture en `.docx` (marqueur de compatibilité Mots), import d'un manuscrit Word avec rejet explicite des styles inconnus.
- Plan des chapitres, zoom, recherche et remplacement, impression, liste des documents récents.
- Affichage des caractères non imprimables (bouton ¶).
- Typographie : saisie de l'espace insécable (`Ctrl+Maj+Espace`) et de l'espace insécable fine (`Ctrl+Alt+Maj+Espace`), espace insécable automatique après le tiret cadratin d'un dialogue (deux pas d'annulation distincts).
- Suite de tests NUnit, CI GitHub Actions, release automatique sur tag `vX.Y.Z`.

## En cours : typographie

Les étapes se font une par une, validées par l'utilisateur. Prochaines pistes, **non validées** (à confirmer avant de les entamer) :

- Espace insécable (fine) automatique avant `:`, `;`, `!`, `?` et `»`, et après `«`.
- Guillemets français `« »` à la place de `"`.
- Points de suspension `…` à la place de `...`.
- Apostrophe typographique `’` à la place de `'`.

## Limites connues

- Import Word : les tableaux sont aplatis en paragraphes ; images, notes et listes ne sont pas reprises.
- Le zoom n'est pas mémorisé d'une session à l'autre.
- La barre de défilement grossit avec le zoom.
- `Mots` n'ouvre que les documents qu'il a lui-même enregistrés ; tout autre `.docx` passe par **Importer**.
- Un test de clavier (`Enter_at_the_end_of_a_heading_starts_a_normal_paragraph`) peut échouer si Maj ou Ctrl est pressé pendant son exécution.
