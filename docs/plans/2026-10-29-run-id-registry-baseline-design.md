# État de référence du compte de mesure et registre RUN_ID

Date : 2026-10-29
Statut : approuvé par le mainteneur

## Problème

Le protocole exige à la fois un `HOME` dédié vide, un `$HS_ART` préexistant sous ce `HOME`, la conservation permanente de `$HS_ART/.run-ids/` et une remise à zéro du `HOME` après chaque série. Ces exigences ne peuvent pas être satisfaites simultanément.

## Invariant retenu

Le compte de mesure revient à un **état de référence propre**, et non à un `HOME` littéralement vide :

- l'état de référence est inventorié après la création de `$HS_ART/.run-ids/` ;
- il autorise seulement les éléments indispensables créés par macOS pour ce compte et l'arborescence `$HS_ART/.run-ids/` ;
- aucun secret, donnée personnelle, cache ou autre état HardSlice ne persiste ;
- avant une série, `$HS_ART` ne contient que `.run-ids/` et ses réservations historiques ;
- après une série, `$HS_RUN` est rangé puis supprimé et les caches sont supprimés ;
- la seule différence admise avec l'inventaire initial est l'ajout append-only des réservations `.run-ids/<RUN_ID>/` créées depuis cet inventaire ; aucune réservation existante n'est supprimée ou modifiée.

## Portée de la correction

La citation historique de M-08-A1 reste inchangée. Ses conséquences, OP-01 et le protocole S1 préciseront l'interprétation opérationnelle ci-dessus. La localisation du registre, son format et P0 ne changent pas.

## Vérification

- aucune exigence opérationnelle de `HOME` vide ne subsiste pour S1, hors citation historique ;
- OP-01 distingue l'état de référence stable de la croissance append-only du registre ;
- `$HS_RUN` et tous les caches restent supprimés en fin de série ;
- les affirmations « registre préservé » et « jamais supprimé » restent vraies.
