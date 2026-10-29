# Feuille de route

État d'avancement de Mots et suite prévue. À tenir à jour à chaque étape validée.

## Fait (v0.1.0)

- Styles de paragraphe Normal, Titre 1 et Ellipse, source unique pour l'éditeur et le `.docx`.
- Enregistrement et ouverture en `.docx` (marqueur de compatibilité Mots), import d'un manuscrit Word avec rejet explicite des styles inconnus.
- Plan des chapitres, zoom, recherche et remplacement, impression, liste des documents récents.
- Affichage des caractères non imprimables (bouton ¶).
- Typographie : saisie de l'espace insécable (`Ctrl+Maj+Espace`) et de l'espace insécable fine (`Ctrl+Alt+Maj+Espace`), espace insécable automatique après le tiret cadratin d'un dialogue (deux pas d'annulation distincts).
- Suite de tests NUnit, CI GitHub Actions, release automatique sur tag `vX.Y.Z`.

## Fait depuis la v0.1.0 : performance de la saisie

WPF met en page tout le document à chaque frappe : sur un manuscrit entier (3 800 paragraphes), la saisie était « molle » même en Release (vérifié sur `Référence/Anya (Mots).docx`). Tester en Release (`dotnet run --project Mots.App -c Release`) : en Debug, tout est bien plus lent.

- **Affichage par chapitres** (validé) : l'éditeur ne montre que le chapitre du curseur, le précédent et le suivant ; les autres attendent à l'écart. Un chapitre est un « Titre 1 » et son texte ; le texte avant le premier titre forme un chapitre à part. La fenêtre se décale quand le curseur change de chapitre, sans que le texte saute à l'écran (un chapitre n'est retiré en haut qu'une fois sorti de la vue). `Ctrl+PageSuivante` / `Ctrl+PagePrécédente` changent de chapitre, comme le plan. L'enregistrement, la recherche, le remplacement, le comptage et l'impression portent sur tout le manuscrit.
- Le compteur de mots est recalculé 250 ms après la dernière frappe.
- Le correcteur orthographique de WPF est retiré (il coûtait environ 120 ms par frappe) : Antidote le remplace.

## En cours : typographie

Les étapes se font une par une, validées par l'utilisateur. Prochaines pistes, **non validées** (à confirmer avant de les entamer) :

- Espace insécable (fine) automatique avant `:`, `;`, `!`, `?` et `»`, et après `«`.
- Guillemets français `« »` à la place de `"`.
- Points de suspension `…` à la place de `...`.
- Apostrophe typographique `’` à la place de `'`.

## Limites connues

- L'annulation (`Ctrl+Z`) est perdue quand on ouvre un chapitre par le plan, `Ctrl+PageSuivante`/`Ctrl+PagePrécédente` ou la recherche.
- Le texte avant le premier titre n'apparaît pas dans le plan ; on l'atteint avec `Ctrl+PagePrécédente`.
- La sélection ne peut pas dépasser les chapitres affichés.
- Le défilement à la molette ne décale pas la fenêtre de chapitres (seul le curseur le fait) : on ne lit pas plus loin que le chapitre voisin sans y placer le curseur. Piste : faire suivre la fenêtre au défilement.
- Import Word : les tableaux sont aplatis en paragraphes ; images, notes et listes ne sont pas reprises.
- Le zoom n'est pas mémorisé d'une session à l'autre.
- La barre de défilement grossit avec le zoom.
- `Mots` n'ouvre que les documents qu'il a lui-même enregistrés ; tout autre `.docx` passe par **Importer**.
- Un test de clavier (`Enter_at_the_end_of_a_heading_starts_a_normal_paragraph`) peut échouer si Maj ou Ctrl est pressé pendant son exécution.
