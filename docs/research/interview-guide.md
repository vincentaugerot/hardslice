# Guide d'entretien et règle de codage (UXR-02)

Statut : **DRAFT**, préparé par l'agent le 28/10/2026, corrigé le 28/10/2026 à la demande du mainteneur (A-01, A-02, A-03, A-05, A-06), non validé (D-UXR-02 `OPEN` dans [phase-0-decisions.md](../execution/phase-0-decisions.md)). La règle de codage ne vaut que si elle est validée et datée **avant le premier entretien**. Notes internes en français ; script et questions destinés au participant en français et en anglais (D-11), même contenu dans les deux langues.
Sources : [plan.md](../plan.md) §3 ; [persona.md](persona.md) ; backlog UXR-02, R-05 ; D-13.

**Porte avant tout premier contact** (recrutement, design partner, gardien) :
- D-UXR-01 validée (fait le 28/10/2026, version de `40a2763`) et son amendement D-UXR-01-A1 revalidé (`OPEN` à ce jour) ;
- D-UXR-02 **doit être validée** par le mainteneur (script corrigé, D-13) ; elle est `OPEN` à ce jour ;
- moyen de demande de suppression renseigné à la place du placeholder du §2 ;
- stockage vérifié et attesté (OP-UXR-01 : hors synchronisation iCloud, Dropbox et OneDrive ; permissions `0700` sur le dossier, `0600` sur les fichiers ; sauvegardes conformes à D-13).

**Porte avant tout entretien** :
- porte « premier contact » ouverte ;
- participant qualifié selon les critères C1 à C7 de [persona.md](persona.md) §2, relances conditionnelles comprises ; une réponse indéterminée ne qualifie pas ;
- preuve de consentement préparée (fichier hors dépôt, séparé de la table identité ↔ pseudonyme).

**Stockage (D-13)** : notes brutes, enregistrements, preuve de consentement, table identité ↔ pseudonyme et tracker dans `~/Documents/hardslice-research/`, hors git, hors synchronisation cloud, sur le disque chiffré par FileVault. La preuve de consentement et la table identité ↔ pseudonyme sont deux fichiers séparés. Le dépôt ne reçoit que la ligne anonymisée du §10, après la revue de divulgation du §9 si elle doit être publiée.

**Traitement des données brutes** :
- **Jamais transmis à un agent ni à un service cloud** : notes brutes, enregistrements, preuves de consentement, table identité ↔ pseudonyme, tracker, noms, adresses, entreprises ou tout autre identifiant. Cela vaut pour un agent de code (Claude, Codex ou autre), un assistant de rédaction, un service de transcription, de stockage ou de partage. Seules la ligne anonymisée du §10 et des synthèses anonymisées, après la revue du §9, peuvent être partagées avec un agent.
- **Désactivés dans l'outil de visioconférence** : enregistrement dans le cloud, transcription automatique, résumé ou assistant IA. Ils ne sont pas proposés au participant, puisque D-13 interdit tout stockage dans le cloud. Un enregistrement éventuel est fait localement sur le poste, avec le consentement du participant ; une transcription éventuelle est faite localement et hors ligne.

**Preuve de consentement : conservation et suppression** :
- La preuve de consentement est conservée tant qu'il existe une donnée du participant (notes, enregistrement, table identité ↔ pseudonyme). Elle est supprimée en même temps que la dernière de ces données, donc au plus tard à la fin de la recherche de la phase 4 ou 12 mois après l'entretien. Les sauvegardes suivent les mêmes échéances.
- Si le participant demande la suppression, toutes ses données, sa preuve de consentement et sa ligne dans la table identité ↔ pseudonyme sont supprimées, ainsi que sa ligne anonymisée du §10 si elle n'a pas encore été publiée. Ne subsiste, hors dépôt, qu'un **compteur agrégé** du nombre de demandes de suppression exécutées, sans pseudonyme, sans date et sans identifiant corrélable. Ce compteur est supprimé à la fin de la recherche de la phase 4. Une ligne publiée a été minimisée et revue selon le §9, mais un risque résiduel de réidentification par recoupement subsiste. Sur demande, elle est retirée des supports sous le contrôle du mainteneur à leur prochaine révision ; les copies tierces déjà diffusées peuvent rester hors de son contrôle, ce qui est expliqué au participant.
- Sans consentement à l'entretien, aucune preuve n'est conservée ; ses données de contact suivent la règle des prospects non interviewés de D-13 (suppression à la clôture du recrutement, ou au plus tard 90 jours après le dernier contact).

## 1. Principes

- 45 minutes. Questions ouvertes sur le travail actuel **avant** toute mention du produit.
- **Avant la section 6, aucune question ne mentionne ensemble les agents (ou l'IA) et l'architecture.** Les sections 3 et 4 parlent de structure du code sans parler d'outils ; la section 5 parle d'outils sans parler de structure.
- L'intervieweur ne reformule pas une réponse en y ajoutant le lien agent → architecture.
- Aucune proposition de design partner pendant l'entretien (R-05) ; une éventuelle sollicitation a lieu après, séparément (UXR-07).

## 2. Ouverture et consentement (0–5 min)

Script FR :

> Merci de prendre ce temps. Je fais une recherche sur la façon dont les équipes .NET font évoluer et maintiennent leurs API. Ce n'est pas une présentation commerciale ; je cherche à comprendre votre façon de travailler, il n'y a pas de bonne réponse.
>
> L'entretien dure 45 minutes. Mes notes sont conservées sur mon poste, sur un disque chiffré, sans synchronisation dans le cloud, et ne sont partagées avec personne.
>
> Je peux enregistrer l'entretien pour faciliter mes notes, mais c'est **facultatif** : si vous préférez, nous continuons sans enregistrement, et cela ne change rien à l'entretien. Si vous acceptez, l'enregistrement est supprimé dès que mes notes sont vérifiées, et au plus tard 30 jours après aujourd'hui.
>
> Mes notes et le lien entre votre nom et le pseudonyme qui vous désigne dans mon analyse sont supprimés à la fin de cette recherche, et au plus tard 12 mois après aujourd'hui. Les sauvegardes suivent les mêmes délais.
>
> Ce qui pourra être publié est minimisé et anonymisé : une synthèse, et éventuellement de **courtes citations anonymisées**, sans nom ni entreprise. Je relis chaque élément pour réduire le risque de vous identifier, mais une réidentification par recoupement ne peut pas être exclue complètement. Sur demande, je retire ces éléments des supports que je contrôle à leur prochaine révision ; des copies déjà diffusées par des tiers peuvent rester hors de mon contrôle.
>
> Vous pouvez refuser une question, arrêter à tout moment, ou me demander plus tard de supprimer vos données en écrivant à [moyen de contact pour les demandes de suppression, à fournir par le mainteneur].
>
> Êtes-vous d'accord pour participer ? Et acceptez-vous l'enregistrement, ou préférez-vous que nous continuions sans ?
>
> Enfin, acceptez-vous que de courtes citations anonymisées de cet entretien soient publiées ? Vous pouvez refuser : cela ne change rien à l'entretien, et aucune citation de vous ne sera alors publiée.

Script EN :

> Thank you for your time. I'm researching how .NET teams evolve and maintain their APIs. This isn't a sales pitch; I want to understand how you work, and there are no right answers.
>
> The interview takes 45 minutes. My notes are kept on my computer, on an encrypted disk, with no cloud sync, and are not shared with anyone.
>
> I can record the interview to help with my notes, but it is **optional**: if you prefer, we continue without a recording, and nothing else changes. If you agree, the recording is deleted as soon as my notes are checked, and no later than 30 days from today.
>
> My notes, and the link between your name and the pseudonym used in my analysis, are deleted at the end of this research, and no later than 12 months from today. Backups follow the same deadlines.
>
> What may be published is minimized and anonymized: a summary, and possibly **short anonymized quotes**, with no name or company. I review every item to reduce the risk of identifying you, but re-identification through cross-referencing cannot be ruled out completely. On request, I remove these items from materials I control at their next revision; copies already shared by third parties may remain outside my control.
>
> You can skip any question, stop at any time, or later ask me to delete your data by writing to [contact for deletion requests, to be provided by the maintainer].
>
> Do you agree to take part? And do you agree to the recording, or would you rather we continue without it?
>
> Finally, do you agree that short anonymized quotes from this interview may be published? You can say no: it changes nothing about the interview, and no quote from you will then be published.

Consigner, dans la preuve de consentement hors dépôt : consentement à l'entretien (oui/non), à l'enregistrement (oui/non), à la publication de courtes citations anonymisées (oui/non), date. Sans consentement à l'entretien, arrêter. Un refus de l'enregistrement ne met pas fin à l'entretien : il se poursuit avec des notes manuscrites ou tapées. Un refus des citations n'empêche pas l'entretien ; aucune citation de ce participant n'est alors publiée.

## 3. Contexte (5–12 min)

| # | FR | EN |
|---|---|---|
| 1 | Parlez-moi de votre équipe et du service sur lequel vous travaillez : ce qu'il fait, depuis quand il existe, combien de personnes y touchent. | Tell me about your team and the service you work on: what it does, how long it has existed, how many people work on it. |
| 2 | Comment le code est-il organisé aujourd'hui ? Comment l'expliqueriez-vous à quelqu'un qui arrive dans l'équipe ? | How is the code organized today? How would you explain it to someone joining the team? |
| 3 | Qui décide de cette organisation ? Est-elle écrite quelque part ? | Who decides on this organization? Is it written down anywhere? |

**Relances neutres de vérification** (C1 et C4, à poser si les réponses 1 à 3 ne suffisent pas à confirmer la présélection) :

| Critère | FR | EN |
|---|---|---|
| C1 | Quel est votre rôle quand il faut décider de l'organisation du code ? Que se passe-t-il quand l'équipe n'est pas d'accord ? | What is your role when the code organization has to be decided? What happens when the team disagrees? |
| C1 | Racontez-moi la dernière convention de code que vous avez introduite ou fait changer. | Tell me about the last code convention you introduced or got changed. |
| C4 | Si je cherchais tout le code d'une fonctionnalité précise, par exemple [une fonctionnalité citée par le participant], où irais-je regarder ? | If I were looking for all the code of one specific feature, for example [a feature the participant mentioned], where would I look? |
| C4 | Quand vous ajoutez une nouvelle fonctionnalité, combien de dossiers ou de projets touchez-vous en général ? | When you add a new feature, how many folders or projects do you usually touch? |

Ces relances ne mentionnent ni les agents ni l'IA. Si la vérification contredit la présélection (rôle sans décision sur le code pour C1, organisation purement par couches techniques pour C4), l'entretien est mené jusqu'au bout. Le participant est alors noté « non qualifié à la vérification » dans la ligne du §10 et **exclu des décomptes ≤ 1/3 et ≥ 3/5**.

## 4. Évolution de la structure du code (12–25 min)

| # | FR | EN |
|---|---|---|
| 4 | Racontez-moi la dernière fois qu'une pull request vous a posé un problème de structure ou d'organisation du code, pas un bug. Que s'est-il passé ? | Tell me about the last time a pull request caused a problem with code structure or organization, not a bug. What happened? |
| 5 | Comment vérifiez-vous aujourd'hui qu'un changement respecte l'organisation prévue ? (Relances neutres : revue, tests, outils, conventions écrites.) | How do you check today that a change follows the intended organization? (Neutral prompts: review, tests, tools, written conventions.) |
| 6 | Quand ce n'est pas respecté, qu'est-ce que ça vous coûte, et quand vous en apercevez-vous ? | When it isn't followed, what does it cost you, and when do you notice? |
| 7 | Avez-vous déjà essayé d'automatiser ce contrôle ? Qu'est-ce qui a marché ou pas ? | Have you ever tried to automate this check? What worked or didn't? |
| 8 | Si vous pouviez changer une seule chose dans la manière dont l'équipe garde le code organisé, ce serait quoi ? | If you could change one thing about how the team keeps the code organized, what would it be? |

## 5. Outils et façon de travailler (25–35 min)

| # | FR | EN |
|---|---|---|
| 9 | Avec quels outils l'équipe écrit-elle et modifie-t-elle du code aujourd'hui ? Qu'est-ce qui a changé ces douze derniers mois ? | Which tools does the team use today to write and change code? What has changed over the last twelve months? |
| 10 | Dans votre travail quotidien, qu'est-ce qui fonctionne bien avec ces outils, et qu'est-ce qui fonctionne moins bien ? | In your daily work, what works well with these tools, and what works less well? |
| 11 | Comment ces outils sont-ils encadrés dans l'équipe (règles, consignes, revue) ? | How are these tools governed in the team (rules, guidelines, review)? |

**Repère de codage** : noter l'horodatage de début de la section 6. Rien de ce qui est dit à partir de ce moment ne peut être codé positif.

## 6. Concept (35–42 min)

Présentation courte, lue telle quelle :

> FR : Une idée sur laquelle je travaille : des règles d'architecture vérifiées à la compilation, avec des messages d'erreur qui disent précisément comment corriger, pour que n'importe qui, humain ou agent de code, puisse se corriger seul. Sur un dépôt existant, les violations actuelles seraient gelées, et seules les nouvelles bloqueraient la CI.
>
> EN: An idea I'm working on: architecture rules checked at build time, with error messages that say exactly how to fix the problem, so that anyone, human or coding agent, can fix it on their own. On an existing repository, current violations would be frozen, and only new ones would block CI.

| # | FR | EN |
|---|---|---|
| 12 | Qu'est-ce que cela évoque pour vous ? Dans quelle situation récente cela aurait-il changé quelque chose, ou rien ? | What does this bring to mind? In which recent situation would it have changed something, or nothing? |
| 13 | Qu'est-ce qui vous empêcherait de l'essayer sur votre dépôt ? Combien de temps seriez-vous prêt à consacrer à une installation d'essai ? | What would stop you from trying it on your repository? How much time would you spend on a trial installation? |
| 14 | Qu'est-ce qui vous ferait le retirer après l'avoir installé ? | What would make you remove it after installing it? |

## 7. Clôture (42–45 min)

| # | FR | EN |
|---|---|---|
| 15 | Y a-t-il quelque chose que je n'ai pas demandé et qui compte pour vous sur ce sujet ? | Is there anything I haven't asked that matters to you on this topic? |
| 16 | Connaissez-vous une ou deux personnes dans une situation proche à qui je pourrais parler ? (Pas de coordonnées dans les notes du dépôt.) | Do you know one or two people in a similar situation I could talk to? (No contact details in repository notes.) |

Remercier ; rappeler le droit de suppression et le moyen de le demander.

## 8. Règle de codage du signal

**Ce qui est codé** : « le participant cite spontanément une dérive d'architecture due à un agent » (§3 du plan).

**Dérive d'architecture** : code qui s'écarte de l'organisation voulue par l'équipe : dépendance interdite entre modules, slices, fonctionnalités ou couches ; logique placée au mauvais endroit (par exemple accès à la base depuis un endpoint, règle métier dans un handler au lieu du domaine) ; règle métier dupliquée entre fonctionnalités ; contournement d'une convention de structure ; dossier partagé qui devient fourre-tout. **N'en sont pas** : bugs, style ou formatage seuls, nommage seul, qualité ou absence des tests, sécurité, performance.

**Agent** (aligné sur le critère C5 de [persona.md](persona.md)) : outil d'IA qui, à partir d'une demande, produit ou modifie du code de façon autonome en plusieurs étapes, souvent sur plusieurs fichiers : agent en ligne de commande, mode agent d'un IDE, PR générée par un agent. **N'est pas un agent** : la complétion IA en ligne, c'est-à-dire des suggestions de lignes ou de blocs que le développeur accepte une à une.

| Code | Condition |
|---|---|
| **Positif** | Avant le repère de la section 6, le participant décrit une dérive d'architecture **concrète** (un cas vécu ou une situation récurrente précise) et l'**attribue au moins en partie** à un agent au sens ci-dessus, sans que l'intervieweur ait associé les deux |
| **Négatif** | Aucune mention de ce type avant le repère |
| **Ambigu** | Avant le repère : dérive d'architecture mentionnée sans attribution claire à un agent ; ou attribuée à « l'IA » sans qu'on puisse savoir s'il s'agit d'un agent ou d'une simple complétion ; ou attribuée à la seule complétion IA ; ou problème attribué à un agent mais qui n'est pas une dérive d'architecture au sens ci-dessus ; ou mention seulement hypothétique (« ça pourrait arriver ») ; ou lien suggéré par une relance de l'intervieweur |

**Tranche de l'ambigu** : un code ambigu ne compte **jamais comme positif** ; il compte comme négatif dans les décomptes ≤ 1/3 et ≥ 3/5. Le mainteneur code chaque entretien à partir de ses notes brutes, avec la citation ou l'horodatage justificatif conservés hors dépôt. En cas de doute sur la frontière positif/ambigu, l'entretien est codé ambigu. La règle n'est jamais modifiée après le premier entretien ; une modification ouvrirait une nouvelle série.

## 9. Revue de divulgation avant publication

Avant toute publication (synthèse, article, rapport, ligne du §10 rendue publique), le mainteneur relit chaque élément issu des entretiens, **même si le participant a accepté les citations** :
- **dates** : réduites au mois, au trimestre ou supprimées si la date exacte permet de relier un entretien à une personne ou à un événement ;
- **citations distinctives** (tournure, anecdote, chiffre, nom d'outil interne, taille ou secteur d'équipe qui permettraient de reconnaître la personne ou son entreprise) : supprimées ou paraphrasées ;
- **combinaisons** : aucune combinaison de rôle, taille d'équipe, technologie et date qui isole un participant ;
- **minimisation** : tout champ non indispensable est supprimé ou agrégé si sa combinaison avec les autres peut isoler un participant ;
- la revue est datée et consignée hors dépôt ; un élément retenu sans modification est noté comme tel.

## 10. Ligne anonymisée versée au dépôt (UXR-04)

| Code | Période (mois ou trimestre ; jamais la date exacte, conservée uniquement dans les dossiers privés hors dépôt) | Durée | Correspondance persona (C1–C7, vérification C1/C4 en entretien) | Consentement entretien / enregistrement / citations | Signal (positif / négatif / ambigu) | Section où le signal est apparu | Référence des notes hors dépôt (non nominative) |
|---|---|---|---|---|---|---|---|

Aucune ligne n'est remplie avant un entretien réel attesté par le mainteneur.
