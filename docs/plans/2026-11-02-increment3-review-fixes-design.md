# Corrections de la revue de l’incrément 3

Les trois corrections ont été demandées après la revue de PB-018 à PB-033. Elles préservent le périmètre des règles et leurs sévérités.

- Le parcours partagé par HS005 et HS006 devient itératif. Chaque type construit garde ses arguments analysés ; les membres d’une même définition de DTO ne sont développés qu’une fois. Les substitutions ne peuvent donc plus engendrer une suite infinie comme `Node<int>`, `Node<List<int>>`, etc. Le contrat examine déjà tous les arguments génériques, même lorsqu’aucun membre ne les expose : cette séparation conserve la détection des entités. Chaque étape prend en compte l’annulation du compilateur. Une limite arbitraire de profondeur perdrait des DTO valides ; mémoriser seulement les types construits ne suffit pas à terminer.
- HS008 examine toute la hiérarchie avec les substitutions de l’entité analysée. Un diagnostic hérité n’est omis que si un contexte de base de la compilation détecte déjà le même membre et la même racine. Une base enfant sans propriétaire, ou une base générique dont le paramètre devient une racine, ne dispense plus le type dérivé de contrôle. Supprimer toute déduplication créerait des erreurs identiques sur chaque sous-type.
- HS006 retrouve le sélecteur de `Select` et de `SelectMany` par le paramètre associé à l’argument Roslyn, indépendamment de l’ordre des arguments nommés. Les conversions de lambdas et les cas conformes `AsNoTracking` restent pris en charge.

Validation : régressions observées avant correction ; DTO génériques récursifs testés dans un sous-processus avec délai maximal, pour isoler un éventuel arrêt fatal ; cas positifs, conformes, hérités et génériques ; suite des analyseurs, vérification du template packagé et mesure du coût des analyseurs.

Résultats du 2 novembre 2026 :

- Avant correction, dix des onze nouveaux cas échouent, dont les deux parcours récursifs dans leurs processus isolés. Le témoin de déduplication HS008 passe déjà.
- Après correction, les 53 cas ciblés passent, puis les 895 tests de la solution, sans échec ni test ignoré, intégration PostgreSQL comprise.
- `scripts/test-template.sh` passe intégralement : Demo et Blank (`--no-sample`), scripts Bash et PowerShell, parcours README, violations injectées et générateurs de modules, slices, agrégats et événements.
- `scripts/measure-analyzers.sh` conclut au respect des seuils. Surcoût médian : 0,39 s après modification d’une slice, 0,39 s après modification d’une API d’agrégat et 14,8 % sur le build complet. Les hooks prennent respectivement 4,19 s et 22,79 s. La mesure et sa dispersion sont conservées dans `docs/performance/analyzers.md`.
