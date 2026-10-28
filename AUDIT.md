# Audit repocheck

- **Date** : 2026-10-28
- **Référentiel** : 1.3.0
- **Score** : 100/100

| Criticité | Niveau | Poids dans le score |
|---|---|---|
| 🔴 | Haute | 4 |
| 🟠 | Moyenne | 2 |
| 🟡 | Faible | 1 |
| ⚪ | Optionnelle | 0,5 |

Le score est la somme des poids des pratiques `OK` divisée par celle des pratiques `OK` et `KO`, sur 100. Les `NA` sont exclus du calcul.

| Code | Description | Criticité | Résultat |
|---|---|---|---|
| META-01 | Description du dépôt renseignée | 🟡 | OK |
| META-02 | Topics renseignés | 🟡 | OK |
| META-03 | LICENSE présente | 🔴 | OK |
| META-04 | README complet (installation et usage) | 🟠 | OK |
| META-05 | Homepage URL renseignée | ⚪ | NA |
| META-06 | `.gitignore` adapté au langage | 🟡 | OK |
| META-07 | `.editorconfig` présent | 🟡 | OK |
| META-08 | Badges de statut dans le README (build, licence, version...) | ⚪ | OK |
| META-09 | Le README documente la chaîne CI/CD réelle, outils externes compris | 🟡 | OK |
| META-10 | `.gitattributes` qui normalise les fins de ligne | 🟡 | OK |
| GOV-01 | `CONTRIBUTING.md` | 🟡 | OK |
| GOV-02 | `CODE_OF_CONDUCT.md` | ⚪ | OK |
| GOV-03 | `SECURITY.md` | 🟠 | OK |
| GOV-04 | Template(s) d'issue | 🟡 | OK |
| GOV-05 | Template de pull request | 🟡 | OK |
| GOV-06 | `CODEOWNERS` | ⚪ | NA |
| GOV-07 | Discussions activées | ⚪ | OK |
| GOV-08 | `SUPPORT.md` | 🟡 | OK |
| CI-01 | CI configurée (build et tests sur push/PR) | 🔴 | OK |
| CI-02 | Required status checks sur la branche par défaut | 🔴 | OK |
| CI-03 | Bloc `permissions:` explicite et minimal dans chaque workflow | 🟠 | OK |
| CI-04 | Actions tierces épinglées à un SHA | 🔴 | OK |
| CI-05 | Release automatisée avec versioning sémantique | 🟡 | OK |
| CI-06 | Tags de version au format `vX.Y.Z` | 🟡 | NA |
| CI-07 | Permissions par défaut des workflows en lecture seule | 🟠 | OK |
| BR-01 | Branche par défaut protégée (force-push et suppression interdits) | 🔴 | OK |
| BR-02 | Suppression automatique des branches mergées | ⚪ | OK |
| BR-03 | Méthode de merge unique : squash | ⚪ | OK |
| BR-04 | Revues obligatoires avant merge | ⚪ | NA |
| BR-05 | Protection appliquée aux administrateurs (`enforce_admins`) | 🔴 | OK |
| SEC-01 | Dependabot alerts activées | 🔴 | OK |
| SEC-02 | Dependabot security updates activées | 🔴 | OK |
| SEC-03 | Dependabot version updates configuré (`.github/dependabot.yml`) | 🟠 | OK |
| SEC-04 | Secret scanning activé | 🔴 | OK |
| SEC-05 | Push protection du secret scanning activée | 🟠 | OK |
| TOOL-01 | `AGENTS.md` présent à la racine, et `CLAUDE.md` à la racine qui l'importe (`@AGENTS.md`) | 🟠 | OK |
