# Messages de démarchage (brouillons non envoyés)

Statut : **DRAFT**, préparé par l'agent le 28/10/2026, corrigé le 28/10/2026 (A-01, A-02). **Aucun de ces messages n'a été envoyé.** Les envois sont des actions du mainteneur, après les préalables de [outreach-log.md](outreach-log.md), et chaque envoi est attesté dans ce journal.

Les crochets `[...]` sont à remplir par le mainteneur au moment de l'envoi, hors dépôt.

## 1. Recrutement de participants (UXR-03)

Contraintes : ne mentionner ni les agents de code ni les dérives d'architecture (R-05) ; ne pas présenter de produit ; renvoyer vers les questions de présélection de [persona.md](persona.md) §2.

### FR

> Bonjour [prénom],
>
> Je mène une petite recherche sur la façon dont les équipes .NET font évoluer et maintiennent leurs API ASP.NET Core au fil du temps. Je cherche à parler 45 minutes, en visio, avec des leads techniques ou des développeurs seniors qui fixent les conventions de code dans une équipe de 3 à 15 développeurs.
>
> Ce n'est pas une démarche commerciale : je cherche à comprendre votre façon de travailler. Les notes sont anonymisées et rien d'identifiable n'est publié.
>
> Si cela vous intéresse, pourriez-vous répondre à sept questions rapides ([lien ou questions en réponse]) ? Je vous proposerai ensuite un créneau.
>
> Merci, [signature]

### EN

> Hi [first name],
>
> I'm running a small research study on how .NET teams evolve and maintain their ASP.NET Core APIs over time. I'd like a 45-minute video call with tech leads or senior developers who set code conventions in a team of 3 to 15 developers.
>
> This isn't a sales pitch: I want to understand how you work. Notes are anonymized and nothing identifiable is published.
>
> If you're interested, could you answer seven quick questions ([link or questions inline])? I'll then suggest a time slot.
>
> Thanks, [signature]

## 2. Design partner (UXR-07)

Contraintes : jamais envoyé à un participant avant la fin de son entretien ; décrire l'engagement du §3 du plan sans promettre de résultat.

### Description de l'engagement

- Un dépôt réel de l'équipe, organisé par fonctionnalités, sur lequel HardSlice serait installé en phase 4 (pas avant plusieurs mois).
- Une heure toutes les deux semaines avec le mainteneur pendant la période d'essai.
- Accord pour mesurer, sur ce dépôt : les faux positifs, l'évolution de la baseline (churn) et le maintien de l'outil après un mois.
- Liberté totale de retirer l'outil ; un retrait motivé est un résultat utile.
- Aucune donnée du dépôt publiée sans accord écrit ; les mesures publiées sont anonymisées.

### FR

> Bonjour [prénom],
>
> Je développe HardSlice, un outil open source expérimental pour .NET : des règles d'architecture vérifiées à la compilation, avec des messages qui disent comment corriger, et un gel des violations existantes pour ne bloquer que les nouvelles sur un dépôt existant.
>
> Je cherche une ou deux équipes « design partner » pour l'essayer plus tard sur un vrai dépôt. Concrètement : [description de l'engagement ci-dessus, résumée]. Rien n'est à installer aujourd'hui ; je cherche à savoir si le principe vous intéresse.
>
> Seriez-vous ouvert à un échange de 20 minutes pour en parler ?
>
> [signature]

### EN

> Hi [first name],
>
> I'm building HardSlice, an experimental open-source tool for .NET: architecture rules checked at build time, with messages that say how to fix the violation, and a frozen baseline so that only new violations block on an existing repository.
>
> I'm looking for one or two "design partner" teams to try it later on a real repository. Concretely: [engagement summary]. Nothing to install today; I'd like to know whether the idea is of interest.
>
> Would you be open to a 20-minute call about it?
>
> [signature]

## 3. Gardien indépendant du holdout (UXR-08)

Contraintes : ce message nomme les agents et les violations d'architecture ; il n'est donc jamais envoyé à une personne susceptible de devenir participante avant la fin de son entretien. Aucun envoi avant la validation de la fiche de rôle et D-13.

### Fiche de rôle

**Mission** (plan §9.2) : en phase 1B, écrire à partir de deux énoncés de tâches figés une suite de tests **fonctionnels** et **structurels** (réflexion ou inspection d'assemblies, sans code HardSlice) ; la sceller et transmettre son empreinte SHA-256, publiée dans le pré-enregistrement ; la conserver ; ne révéler ni les tests ni les résultats détaillés intermédiaires avant la fin de tous les runs mesurés ; exécuter ou fournir la suite à la fin.

**Indépendance** : aucun lien hiérarchique ni commercial avec le mainteneur ; aucun accès partagé au dépôt de la suite ; pas de participation au développement de HardSlice.

**Compétences** : C# et .NET, tests xUnit ou équivalent, notions de réflexion .NET.

**Charge estimée** : quelques jours de travail en phase 1B, puis une exécution en fin de phase 3. Calendrier indicatif : plusieurs mois après le premier contact.

**Ce que le gardien ne fait pas** : il ne relit pas le code de HardSlice et ne juge pas le produit.

**Contrepartie** : [à décider par le mainteneur ; aucune dépense n'est engagée par ce brouillon].

### FR

> Bonjour [prénom],
>
> Je prépare une petite expérience publique et pré-enregistrée pour mesurer si des messages d'erreur de build bien rédigés aident des agents de code à corriger seuls des violations d'architecture en .NET. Pour qu'elle soit crédible, la suite de tests qui juge les résultats doit être écrite et conservée par une personne indépendante. Je n'en verrai ni les tests ni les résultats détaillés intermédiaires avant la fin de tous les runs.
>
> Le rôle est décrit ici : [fiche de rôle]. Charge estimée : quelques jours dans plusieurs mois. Seriez-vous intéressé, ou connaîtriez-vous quelqu'un qui le serait ?
>
> [signature]

### EN

> Hi [first name],
>
> I'm preparing a small public, pre-registered experiment to measure whether well-written build error messages help coding agents fix architecture violations on their own in .NET. To keep it credible, the test suite that judges the results must be written and held by an independent person. I won't see the tests or any detailed intermediate results until all runs are done.
>
> The role is described here: [role sheet]. Estimated effort: a few days, several months from now. Would you be interested, or do you know someone who might be?
>
> [signature]
