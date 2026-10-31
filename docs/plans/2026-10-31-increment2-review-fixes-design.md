# Corrections de la revue de l’incrément 2

Les trois corrections ont été demandées après la revue de PB-009 à PB-017. Elles s’appliquent à `main`, qui contient déjà les analyseurs v0.2.

- `BookingPeriod` représente des instants UTC à la microseconde. Son constructeur tronque les fractions de microseconde avant de vérifier que la fin dépasse le début. Le domaine, la recherche de chevauchement, les événements et PostgreSQL utilisent ainsi les mêmes bornes. Un créneau qui devient vide reçoit le 400 métier habituel ; les fuseaux non UTC sont acceptés sans changer les instants à la précision retenue.
- Une nouvelle migration ajoute `CK_Bookings_Period_Positive` (`End > Start`), également déclaré dans le mapping EF Core. Les migrations déjà livrées restent intactes. Une base contenant des périodes invalides doit être réparée explicitement avant la migration : aucune réservation n’est supprimée ou réinterprétée automatiquement.
- HS013 conserve la vérification des contraintes CLR et ajoute celle de `unmanaged` : attribut du paramètre générique ouvert, ou inspection récursive des champs d’instance d’un type valeur fermé. Les références, les structures contenant une référence et les paramètres seulement `struct` ne satisfont pas cette contrainte.

Normaliser dans le seul handler laisserait les autres créations du domaine et les événements diverger ; convertir dans le mapping serait trop tard pour vérifier la durée et le chevauchement. La normalisation dans l’objet valeur donne un contrat unique, sans dépendance du domaine à EF Core. Pour HS013, compléter la réflexion évite de reconstruire une compilation Roslyn de l’application.

Validation : tests de régression d’abord observés en échec, puis tests de domaine, HTTP/PostgreSQL et contraintes génériques ; nouvelle migration et modèle synchronisés ; paquet généré avec les deux variantes et les deux scripts ; sonde HS013 `struct`/`unmanaged` ; démonstration HS016.
