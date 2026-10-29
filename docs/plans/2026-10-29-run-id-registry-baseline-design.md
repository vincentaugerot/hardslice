# État de référence du compte de mesure et registre RUN_ID

Date : 2026-10-29
Statut : approuvé par le mainteneur ; mis à jour le 2026-10-29 après la décision D-18 option A (A-08)

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

La comparaison est **structurelle** : elle ignore les horodatages volatils des dossiers parents et vérifie les chemins autorisés, leur type, leur propriétaire et leur mode, l'absence de tout autre état HardSlice et le caractère vide des réservations. La répartition de cet invariant entre superviseur et runner, depuis D-18-A, est décrite dans la mise à jour ci-dessous.

Un échec avant réservation est un échec de lancement, pas une tentative. Après réservation, une tentative sans manifeste P0 reçoit un manifeste privé de récupération créé par le mainteneur avant son rangement. Une suppression ou modification d'une réservation historique est irréversible : la racine est retirée du service et quarantainée, puis une nouvelle racine exige une décision humaine et une nouvelle attestation OP-01.

## Portée de la correction

La citation historique de M-08-A1 reste inchangée. Ses conséquences, OP-01 et le protocole S1 préciseront l'interprétation opérationnelle ci-dessus. La localisation du registre, son format et P0 ne changent pas ; OP-01 reste la porte humaine qui vérifie l'état de référence avant Q5/Q6.

## Vérification

- aucune exigence opérationnelle de `HOME` vide ne subsiste pour S1, hors citation historique ;
- OP-01 distingue l'état de référence stable de la croissance append-only du registre ;
- `$HS_RUN` et tous les caches restent supprimés en fin de série ;
- les affirmations « registre préservé » et « jamais supprimé » restent vraies.

## Mise à jour du 2026-10-29 : D-18 option A (A-08)

L'invariant est conservé ; il se répartit désormais entre deux comptes :

- le **superviseur** (compte du mainteneur) possède `$HS_ART` et le registre `.run-ids`, sous un `HOME` en `0700` que le runner ne peut pas traverser. Avant une série, `$HS_ART` ne contient que `.run-ids/` aux réservations vides ; P0 le vérifie ;
- le **runner** (compte dédié de M-08-A1) a un `HOME` dans un état de référence propre **sans aucun état HardSlice** ; son staging est créé par S0-7 et détruit par la finalisation K ;
- l'append-only est garanti par les permissions face au code exécuté sous le runner, et reste une garantie procédurale, vérifiée par la fin de série OP-01, face à une erreur côté superviseur ;
- les trois classes d'échec P0, le manifeste de récupération et la quarantaine d'une racine compromise sont inchangés ; ils sont précisés dans le protocole S1 §6.5.1 et dans OP-01, points 16 et 17.
