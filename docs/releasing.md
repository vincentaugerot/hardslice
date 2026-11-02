# Publier une préversion sur NuGet.org

Les trois paquets, `HardSlice.Analyzers`, `HardSlice.Tool` et `HardSlice.Templates`, sont publiés ensemble, à la version unique de `Directory.Build.props`, par le workflow [`.github/workflows/release.yml`](../.github/workflows/release.yml). Il se déclenche sur une étiquette `v<version>` et refuse toute étiquette qui ne nomme pas cette version.

## Ce que fait le workflow

| Job | Rôle |
|---|---|
| `build` | Vérifie que l'étiquette nomme la version, puis restaure, construit et teste la solution depuis l'étiquette, tests d'intégration compris. |
| `template` | Rejoue `scripts/test-template.sh` depuis l'étiquette : paquets locaux, solutions avec et sans Bookings, sondes de toutes les règles. |
| `pack` | Après `build` et `template` : packe les trois paquets, vérifie qu'il y en a exactement trois à la bonne version, rejoue sur eux le parcours d'un utilisateur (`scripts/verify-published.sh --source artifacts/packages`), puis les dépose comme artefact `packages`. |
| `publish` | Seul job qui détient un identifiant et modifie NuGet.org. Il tourne dans l'environnement protégé `nuget`, donc après l'approbation du mainteneur, échange le jeton OIDC du workflow contre une clé d'API NuGet.org valable une heure (publication de confiance), puis pousse `HardSlice.Analyzers`, `HardSlice.Tool` et enfin `HardSlice.Templates`, qui épingle les deux autres. |
| `verify` | Runner neuf, qui n'a jamais vu les paquets : attend que NuGet.org les serve (90 min au plus), puis `dotnet new install HardSlice.Templates@<version>`, génération avec et sans Bookings, `dotnet tool restore` et `dotnet hardslice check` dans chacune. Il vérifie que le template, l'analyseur et l'outil viennent de NuGet.org. Seuls `scripts/verify-published.sh` et `global.json` sont lus dans le dépôt. |

Une version poussée sur NuGet.org ne peut jamais être remplacée, seulement masquée (« unlist »). Tout ce qui peut échouer passe donc avant `publish`.

## Prérequis, une seule fois

1. **Dépôt public.** Les fiches NuGet.org renvoient au code, aux fiches de règles et à la documentation du dépôt `vincentaugerot/hardslice`.
2. **Compte NuGet.org** du mainteneur. Les identifiants `HardSlice.Analyzers`, `HardSlice.Templates` et `HardSlice.Tool` sont libres ; demander la réservation du préfixe `HardSlice.` (sans elle, `dotnet new` affiche `Reserved: ✘`).
3. **Publication de confiance** sur NuGet.org, menu du compte, « Trusted Publishing » : une politique pour le propriétaire `vincentaugerot`, le dépôt `hardslice`, le fichier de workflow `release.yml` et l'environnement `nuget`.
4. **Environnement GitHub `nuget`** (Settings, Environments) :
   - relecteur requis : le mainteneur, avec « Prevent self-review » désactivé, puisqu'il pose lui-même l'étiquette ;
   - branches et étiquettes de déploiement : étiquettes `v*` seulement ;
   - secret `NUGET_USER` : le nom du profil NuGet.org (pas l'adresse e-mail), lu par `NuGet/login`.

Aucune clé d'API n'est stockée. À défaut de publication de confiance, une clé limitée au push de `HardSlice.*` dans un secret de l'environnement `nuget` remplacerait l'étape `NuGet/login`.

## Publier

1. Vérifier que la CI est verte sur le commit à publier et que `Directory.Build.props` porte la version voulue.
2. Vérifier les paquets localement, sur le parcours d'un utilisateur :

   ```text
   for p in Analyzers Tool Templates; do dotnet pack src/HardSlice.$p/HardSlice.$p.csproj -c Release -o artifacts/packages; done
   scripts/verify-published.sh 0.2.0-preview.1 "$(mktemp -d)" --source artifacts/packages
   ```

3. Poser et pousser l'étiquette :

   ```text
   git tag -a v0.2.0-preview.1 -m "HardSlice 0.2.0-preview.1"
   git push origin v0.2.0-preview.1
   ```

4. Approuver le déploiement dans l'environnement `nuget` quand `build`, `template` et `pack` sont verts.
5. Attendre `verify` : il prouve qu'un utilisateur installe le template depuis NuGet.org et passe `dotnet hardslice check`.

## En cas d'échec

- **Avant `publish`** : rien n'est publié. Corriger sur `main`, supprimer l'étiquette (`git push origin :refs/tags/v<version>`, `git tag -d v<version>`) et la reposer sur le commit corrigé.
- **Pendant `publish`** : relancer le job. `--skip-duplicate` saute les versions déjà poussées et pousse les autres.
- **`verify` en attente trop longue** : NuGet.org valide et indexe chaque paquet, ce qui prend d'ordinaire quelques minutes et parfois davantage. Relancer le job seul ; le script reprend l'attente.
- **`verify` en échec après publication** : les paquets sont publiés et ne peuvent pas être remplacés. Les masquer sur NuGet.org, corriger, et publier la préversion suivante (`0.2.0-preview.2`) avec une nouvelle étiquette.

Pour contrôler une version déjà publiée depuis n'importe quelle machine : `scripts/verify-published.sh <version> "$(mktemp -d)"`.
