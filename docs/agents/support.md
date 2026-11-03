# Prise en charge des agents : instructions, hooks et skills

Ce document établit, pour Claude Code, Codex, GitHub Copilot CLI et Gemini CLI, ce que chaque CLI lit réellement à sa version du moment : fichier d'instructions, `AGENTS.md`, hooks après édition et de fin de tâche, skills (PB-034). Il décide le contenu de PB-042 (fichiers d'instructions), PB-043 (skills) et PB-047 (adaptateurs), et précise PB-046 (hooks Claude Code). Il tranche les questions 3 et 4 de la [spécification](../specifications.md) (section 13).

Chaque constat renvoie à une source officielle (`[S..]`, liste en fin de document) et à un essai daté (`[E..]`, journal plus bas). Un mécanisme seulement documenté est marqué « documenté, non essayé » et n'est pas retenu pour un adaptateur.

## Versions essayées

Essais du 31/10/2026 et du 01/11/2026 (UTC) sur Linux x64, dans une solution générée par `scripts/generate-app.sh` (variante avec Bookings).

| CLI | Version | Compte | Essais avec modèle |
|---|---|---|---|
| Claude Code | 2.1.316 | claude.ai | oui |
| Codex CLI | 0.165.0 | ChatGPT | oui |
| GitHub Copilot CLI | 1.0.105 | Copilot | oui |
| Gemini CLI | 0.64.0 | compte Google personnel, refusé | non : `UNSUPPORTED_CLIENT` [E10] |

## Synthèse

| | Claude Code | Codex | Copilot CLI | Gemini CLI |
|---|---|---|---|---|
| Fichier d'instructions | `CLAUDE.md` ou `.claude/CLAUDE.md` (cwd et parents), `AGENTS.md` sans `CLAUDE.md` [S1] [E1] [E14] | `AGENTS.md` (racine du dépôt jusqu'au cwd, 32 Kio au plus) [S4] [E7] | `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, tous fusionnés, à la racine git et dans le cwd [S7] [E6] | `GEMINI.md` (global, cwd et parents, sous-dossiers) [S10] ; non essayé [E10] |
| Lecture d'`AGENTS.md` | native seulement sans `CLAUDE.md` ; avec `CLAUDE.md`, par `@AGENTS.md` [S1] [E1] [E11] [E14] | native [S4] [E7] | native ; une seconde fois par l'import d'un `CLAUDE.md` racine [S7] [E6] [E12] | par `@./AGENTS.md` dans `GEMINI.md` ou `context.fileName` [S10] [S11] ; documenté, non essayé [E10] |
| Hook après édition | `PostToolUse`, matcher `Edit\|Write`, `tool_input.file_path` [S2] [E1] [E2] | `PostToolUse`, matcher `Edit\|Write` qui couvre `apply_patch`, chemins dans le texte du patch [S5] [E8] | `postToolUse` (`.github/hooks/*.json`), `toolName` `edit` ou `create`, `toolArgs.path` [S8] [E4] | `AfterTool`, matcher `write_file\|replace` [S12] ; documenté, non essayé [E10] |
| Erreur renvoyée à l'agent après édition | code `2`, message sur la sortie d'erreur [S2] [E1] | code `2`, message sur la sortie d'erreur [S5] [E8] | code `0` et `{"additionalContext": "..."}` ; le code `2` n'est qu'un avertissement affiché à l'utilisateur [S8] [E4] | code `2` ou `{"decision": "deny", "reason": "..."}` [S12] ; documenté, non essayé [E10] |
| Hook de fin de tâche | `Stop`, `stop_hook_active` [S2] [E1] | `Stop`, `stop_hook_active` [S5] [E8] | `agentStop`, `stop_hook_active` [S8] [E4] | `AfterAgent`, `stop_hook_active` [S12] ; documenté, non essayé [E10] |
| Fin bloquée, l'agent continue | code `2`, raison sur la sortie d'erreur [S2] [E1] | code `2`, raison sur la sortie d'erreur [S5] [E8] | code `0` et `{"decision": "block", "reason": "..."}`, 8 relances au plus [S8] [E4] | code `2` ou `{"decision": "deny", "reason": "..."}` [S12] ; documenté, non essayé [E10] |
| Condition de chargement des hooks du dépôt | aucune en `-p` [S16] [E1] ; en interactif, confiance du dossier demandée au premier lancement [S16] | projet de confiance et chaque hook approuvé par `/hooks`, sinon ignoré sans message [S5] [E7] [E8] | dossier de confiance (`--trust-folder` ou `COPILOT_ALLOW_ALL=true` en `-p`) [S8] [E4] | dossier de confiance [S13] [E10] |
| Skills (`SKILL.md`) lues | `.claude/skills/` [S3] [E3] [E15] | `.agents/skills/` [S6] [E9] | `.github/skills/`, `.agents/skills/`, `.claude/skills/` [S9] [E6] | `.agents/skills/`, `.gemini/skills/` [S14] [E10] |
| Adaptateur livré | natif : `.claude/settings.json`, `dotnet hardslice hook claude` (PB-046) [E19] à [E21] | natif : `.codex/hooks.json`, `dotnet hardslice hook codex` (PB-047) [E25] [E26] | natif : `.github/hooks/hardslice.json`, `dotnet hardslice hook copilot` (PB-047) [E22] à [E24] | fallback : `GEMINI.md` importe `AGENTS.md`, qui demande `scripts/check.sh` (PB-047) |
| Refus d'un fichier protégé | `PreToolUse`, code `2` [E20] | `PreToolUse` sur `apply_patch`, code `2`, chaque fichier du patch [E26] | `preToolUse` sur `edit` et `create`, `permissionDecision: "deny"` [E23] | aucun |
| Message à l'utilisateur quand la vérification de fin passe | `systemMessage` [E19] | `systemMessage` (JSON exigé avec le code `0`) [S5] | aucun mécanisme [S8] | aucun |

## Constats par CLI

### Claude Code 2.1.316

- **Instructions.** Claude Code lit `CLAUDE.md` du dossier courant et de ses parents. Il lit `AGENTS.md` nativement depuis la 2.1.277, mais seulement quand aucun `CLAUDE.md` n'existe ; sinon seul `CLAUDE.md` est chargé, et `@AGENTS.md` l'importe [S1]. Dans la solution générée sans `CLAUDE.md`, `AGENTS.md` est chargé comme instructions du projet [E1]. `.claude/CLAUDE.md` compte comme un `CLAUDE.md` : sans import, `AGENTS.md` n'est plus chargé ; avec `@../AGENTS.md`, il l'est une fois [E14].
- **Après édition.** `PostToolUse` avec le matcher `Edit|Write`. La charge utile porte `tool_name`, `tool_input.file_path` et `tool_response` [S2]. Un code `2` renvoie la sortie d'erreur à l'agent, qui a corrigé le fichier comme demandé [E1]. Fixtures : `claude/PostToolUse.Edit.json`, `claude/PostToolUse.Write.json`, `claude/PreToolUse.Edit.json`, `claude/PreToolUse.Write.json`.
- **Fin de tâche.** `Stop` : un code `2` bloque la fin et la sortie d'erreur devient la consigne ; l'agent a continué puis s'est arrêté au second appel, où `stop_hook_active` vaut `true` [E1]. Fixtures : `claude/Stop.json`, `claude/Stop.active.json`.
- **Refus d'une édition.** `PreToolUse`, code `2` ou `permissionDecision: "deny"` [S2]. Avec le code `2`, l'édition n'a pas lieu et l'agent reçoit la sortie d'erreur comme résultat de l'outil (`PreToolUse:Edit hook error: [<commande>]: ...`) ; il a cité la raison sans contourner le refus [E20]. Un `Edit` dont `old_string` égale `new_string` est rejeté par Claude Code avant tout hook [E20].
- **Adaptateur de PB-046.** Essayé sur la solution générée, avec `.claude/settings.json` du template et `dotnet hardslice hook claude <événement>` : l'erreur HS010 du build après un `Write` arrive à l'agent (`hook_blocking_error` dans le transcript), qui l'a corrigée [E19] ; un test unitaire en échec bloque la fin (`Stop hook feedback:`), l'agent a corrigé le test et la seconde vérification de fin a réussi en 19 s [E21]. L'agent ne sait pas que le hook de fin a réussi : son dernier message, écrit avant, dit qu'il n'a pas pu lancer la vérification quand `Bash` lui est refusé [E19] [E21].
- **Skills.** `.claude/skills/<nom>/SKILL.md`, en-tête `name` et `description` [S3]. Seul `.claude/skills/` est lu : une skill de `.agents/skills/` n'apparaît pas [E3].
- **Limite.** Une écriture faite par l'outil `Bash` (`printf ... > fichier`) ne déclenche pas les hooks `Edit|Write` : l'agent l'a fait pour créer un fichier pendant l'essai [E1].

### Codex CLI 0.165.0

- **Instructions.** `AGENTS.md` du dossier racine du projet jusqu'au dossier courant, un fichier par dossier, 32 Kio au total par défaut (`project_doc_max_bytes`) ; `CLAUDE.md` n'est pas lu [S4]. Le rollout de l'essai contient `# AGENTS.md instructions for <solution>`, et l'agent a lancé de lui-même `scripts/check.sh` comme le demande `AGENTS.md` [E7].
- **Hooks.** Stables et actifs par défaut (`codex features list` : `hooks stable true`), dans `.codex/hooks.json` du projet, au même format que Claude Code [S5]. Un appel `apply_patch` correspond au matcher `Edit|Write`, mais la charge utile garde `tool_name: "apply_patch"` et le patch entier dans `tool_input.command` : les fichiers touchés se lisent sur les lignes `*** Update File:`, `*** Add File:` et `*** Delete File:`, plusieurs par appel [S5] [E8].
- **Après édition.** Un code `2` renvoie la sortie d'erreur à l'agent (`PostToolUse Blocked`), qui a corrigé le fichier [E8]. Fixtures : `codex/PreToolUse.apply_patch.json`, `codex/PostToolUse.apply_patch.json`, `codex/PostToolUse.apply_patch.add.json`.
- **Fin de tâche.** `Stop` avec code `2` : l'agent a continué, puis le second appel (`stop_hook_active: true`) l'a laissé finir [E8]. Avec un code `0`, `Stop` exige du JSON sur la sortie standard [S5]. Fixtures : `codex/Stop.json`, `codex/Stop.active.json`.
- **Approbation des hooks.** Le projet doit être de confiance, et chaque hook non géré doit être approuvé dans l'interface `/hooks` (« Hooks need review ») [S5] [E8]. L'approbation est enregistrée dans `~/.codex/config.toml`, sous `[hooks.state."<chemin absolu>/.codex/hooks.json:<événement>:<i>:<j>"]` avec `trusted_hash = "sha256:..."` : elle vaut pour un clone et une configuration donnés. Un hook non approuvé est ignoré sans aucun message par `codex exec` [E7]. `--dangerously-bypass-hook-trust` contourne l'approbation pour une exécution [S5] ; non utilisé.
- **Skills.** `.agents/skills/` du dossier courant jusqu'à la racine du dépôt, `~/.agents/skills/`, `/etc/codex/skills` [S6]. Ni `.claude/skills/` ni `.github/skills/` ne sont lus [E9].
- **Adaptateur de PB-047.** Essayé sur la solution générée, avec `.codex/hooks.json` du template approuvé hook par hook dans `/hooks` (« Hooks need review », puis `t` sur chacun) : l'erreur HS010 du build après un `apply_patch` arrive à l'agent (`PostToolUse Blocked`) ; l'agent, à qui la demande imposait `DateTime.Today`, s'arrête d'abord en expliquant le conflit, le hook de fin le bloque, il passe la date en paramètre et ajoute ses tests, et la seconde vérification de fin réussit [E25]. Un patch de `scripts/check.sh` est refusé par `PreToolUse` avec le code `2` [E26]. L'approbation est enregistrée sous le chemin absolu de `.codex/hooks.json`, contre l'empreinte de chaque hook [S5] : elle vaut pour un clone, et un `.codex/hooks.json` inchangé d'une version à l'autre la garde.
- **Limites.** Une modification par le shell (`exec_command`) ne correspond pas à `Edit|Write`. Les hooks sont « a useful guardrail, not a complete enforcement boundary » [S5]. Les commandes que l'agent lance par le shell (`bash -lc`) ne reçoivent pas l'environnement de `codex exec`, que les hooks reçoivent [E25].

### GitHub Copilot CLI 1.0.105

- **Instructions.** Copilot fusionne, sans ordre de priorité, `AGENTS.md`, `CLAUDE.md`, `GEMINI.md` et `.github/copilot-instructions.md` de la racine git et du dossier courant, plus `.github/instructions/**/*.instructions.md` et `.claude/rules/**/*.md` [S7]. `copilot instruction list` les liste tous les quatre à la racine ; un `AGENTS.md` placé dans `src/Demo/` n'est lu que si `src/Demo/` est le dossier courant [E6]. Les imports `@chemin` sont suivis dans `AGENTS.md`, `CLAUDE.md` et `copilot-instructions.md` [S7]. Ils ne sont pas dédupliqués : l'import d'`AGENTS.md` par un `CLAUDE.md` racine, en `@AGENTS.md` comme en `@./AGENTS.md`, ajoute une seconde copie entière d'`AGENTS.md` au prompt, après celle lue nativement ; l'import de `GEMINI.md` reste du texte [E12]. `.claude/CLAUDE.md` n'est pas lu [E13].
- **Hooks natifs.** `.github/hooks/*.json`, format `{"version": 1, "hooks": {"postToolUse": [{"type": "command", "bash": "...", "powershell": "...", "timeoutSec": 30, "matcher": "..."}]}}`, matcher ancré sur `toolName` [S8]. Charge utile en camelCase : `toolName` (`view`, `edit`, `create`, `bash`), `toolArgs.path`, `toolResult` [E4].
- **Après édition.** Le code `2` d'un `postToolUse` n'est qu'un avertissement affiché à l'utilisateur [S8]. L'erreur atteint l'agent par `{"additionalContext": "..."}` sur la sortie standard avec un code `0`, ajouté au résultat de l'outil : l'agent a corrigé le fichier [E4]. Un `postToolUse` part aussi après une lecture (`view`) : l'adaptateur filtre `edit` et `create`. Fixtures : `copilot/preToolUse.edit.json`, `copilot/preToolUse.bash.json`, `copilot/postToolUse.edit.json`, `copilot/postToolUse.create.json`.
- **Fin de tâche.** `agentStop` avec `{"decision": "block", "reason": "..."}` : la raison devient un nouveau message et l'agent continue ; le second appel porte `stop_hook_active: true`. Après 8 blocages consécutifs, la CLI termine quand même [S8] [E4]. Fixtures : `copilot/agentStop.json`, `copilot/agentStop.active.json`.
- **Refus d'une édition.** `preToolUse` avec `permissionDecision: "deny"` et `permissionDecisionReason`, ou code `2` ; tout autre code non nul refuse aussi, un dépassement du délai laisse passer [S8]. Avec `permissionDecision: "deny"` et le code `0`, l'édition n'a pas lieu, l'agent lit `Denied by preToolUse hook: <raison>` et s'arrête en expliquant le refus [E23].
- **Hooks de Claude Code exécutés aussi.** Copilot lit `.claude/settings.json` et y exécute les hooks en mode compatible Claude [S8] : `tool_name: "Edit"`, mais `tool_input.path` au lieu de `tool_input.file_path`, `timestamp`, pas de `transcript_path` après une édition [E5]. Dans ce mode, le code `2` de `PostToolUse` et de `Stop` est seulement affiché à l'utilisateur : l'agent ne l'a pas reçu, n'a rien corrigé et a fini [E5]. Copilot définit `COPILOT_CLI` dans l'environnement du hook, absent sous Claude Code [E5]. Fixtures : `copilot-claude-compat/PreToolUse.Edit.json`, `copilot-claude-compat/PostToolUse.Edit.json`, `copilot-claude-compat/Stop.json`.
- **Skills.** `.github/skills/`, `.agents/skills/` et `.claude/skills/` du projet, `~/.copilot/skills/`, `~/.agents/skills/` [S9] ; les trois dossiers du projet sont lus [E6]. Deux skills de même nom dans `.agents/skills/` et `.claude/skills/` n'apparaissent qu'une fois, celle de `.agents/skills/` [E6].
- **Adaptateur de PB-047.** Essayé sur la solution générée, avec `.github/hooks/hardslice.json` du template : Copilot charge six hooks, les trois de ce fichier et les trois de `.claude/settings.json`, qui sortent aussitôt [E22]. L'erreur HS010 du build après un `create` arrive à l'agent par `additionalContext`, il passe l'instant en paramètre et la vérification de fin réussit en 26 s [E22] ; un test en échec bloque la fin (`decision: "block"`), l'agent corrige le test et la seconde vérification réussit en 23 s [E24] ; l'édition de `scripts/check.sh` est refusée [E23]. Copilot borne `additionalContext` à 10 Kio : l'adaptateur coupe les erreurs à 9 000 caractères et renvoie à `scripts/check.sh --build`. Copilot n'a pas de message à l'utilisateur : une vérification de fin réussie reste silencieuse.
- **Limites.** Hooks et skills du dépôt ne sont chargés que dans un dossier de confiance [S8]. En `-p`, `--allow-tool "write(<dossier>)"` n'a pas autorisé les éditions sous ce dossier ; `--allow-tool write` l'a fait [E4]. Une modification par l'outil `bash` n'est pas une édition `edit`/`create`.

### Gemini CLI 0.64.0

- **Compte.** Depuis le 18/06/2026, Gemini CLI ne sert plus les comptes Google personnels (gratuits, Google AI Pro et Ultra) ; seuls une clé API Gemini, Vertex AI ou une licence Gemini Code Assist d'entreprise fonctionnent, et Google oriente les particuliers vers Antigravity CLI [S15]. Le compte de la machine d'essai est refusé (`IneligibleTierError`, `UNSUPPORTED_CLIENT`) : aucun essai avec modèle n'était possible [E10].
- **Instructions.** `GEMINI.md` global, du dossier courant et de ses parents jusqu'à la racine git, et des sous-dossiers [S10]. `AGENTS.md` peut être lu par `"context": {"fileName": ["AGENTS.md", "GEMINI.md"]}` dans `.gemini/settings.json` [S10], ou importé par `@./AGENTS.md` dans `GEMINI.md` [S11]. Documenté, non essayé.
- **Hooks.** Actifs par défaut (`hooksConfig.enabled`), dans `.gemini/settings.json`, événements `BeforeTool`, `AfterTool`, `AfterAgent` ; matcher en expression régulière sur `write_file|replace` ; code `2` ou `{"decision": "deny", "reason": "..."}` : après un outil, la raison remplace son résultat ; après la réponse de l'agent, elle devient un nouveau message [S12]. Les hooks du projet ne sont chargés que dans un dossier de confiance [S13]. Seul le chargement est essayé : en headless avec `GEMINI_CLI_TRUST_WORKSPACE=true`, le hook `SessionStart` du projet s'est exécuté avant l'échec d'authentification ; `AfterTool` et `AfterAgent` exigent un modèle [E10].
- **Skills.** Actives par défaut, dans `.agents/skills/` puis `.gemini/skills/` du projet, `~/.agents/skills/` et `~/.gemini/skills/` [S14]. `gemini skills list` liste celles de `.agents/skills/` et `.gemini/skills/`, pas celles de `.claude/skills/` ni de `.github/skills/` [E10].

## Décisions

### Fichiers d'instructions (PB-042)

- Claude Code lit `.claude/CLAUDE.md`, qui importe `AGENTS.md` (`@../AGENTS.md`) : sans cet import, la seule présence d'un `CLAUDE.md` empêche Claude Code de lire `AGENTS.md` [S1] [E14]. Pas de `CLAUDE.md` à la racine : Copilot le lirait et suivrait son import, et `AGENTS.md` figurerait deux fois dans son prompt [E12]. `.claude/`, que Copilot ne lit pas pour les instructions [E13], regroupe déjà les réglages et les skills de Claude Code.
- `GEMINI.md` importe `AGENTS.md` (`@./AGENTS.md`) plutôt que `context.fileName` : pas de `.gemini/settings.json` à livrer et à protéger. Non essayé avec un modèle, faute de compte servi [E10].
- `.github/copilot-instructions.md` ne réimporte pas `AGENTS.md`, que Copilot lit déjà ; il ne contient que ce qui est propre à Copilot.
- Codex n'a besoin d'aucun fichier en plus d'`AGENTS.md`, qui reste très en dessous des 32 Kio.
- Copilot lit aussi `GEMINI.md`, sans suivre son import [E12] : `GEMINI.md` commence par la CLI qu'il vise et ne contient rien de contraire aux autres fichiers ; `.github/copilot-instructions.md` dit d'en ignorer la section propre à Gemini CLI. `.claude/CLAUDE.md` commence aussi par sa CLI. Vérifié avec Copilot sur la solution livrée : `AGENTS.md` n'y est chargé qu'une fois [E13].

### Skills (PB-043, PB-044)

- Source unique : `.claude/skills/<skill>/SKILL.md`, lue par Claude Code et Copilot.
- Copie produite au pack dans `.agents/skills/<skill>/SKILL.md`, lue par Codex et Gemini CLI. Copilot lit les deux dossiers et ne garde qu'une skill par nom [E6] : les copies sont identiques, il n'y a pas de doublon.
- Pas de copie dans `.github/skills/` ni `.gemini/skills/`, inutiles.
- Réalisé : `add-module`, `add-aggregate`, `add-slice` et `add-domain-event`, copiées dans `.agents/skills/` par le pack du paquet de templates. Exécutées avec Claude Code sur une solution `--no-sample` [E15] à [E18], selon le [protocole des skills](#protocole-dessai-des-skills). Enchaînées sur une seule demande métier par la [démonstration de PB-050](../../demos/add-feature/README.md) : Claude Code, Codex et Copilot CLI livrent les sept éléments par leurs templates [E31] à [E34] ; Codex lit les skills comme des fichiers, et Copilot n'a chargé que `add-module`, `AGENTS.md` nommant les autres templates.
- Réalisé (PB-044) : `extract-to-domain` et `fix-architecture-violation`, que chaque diagnostic HS et chaque échec de test d'architecture nomme après `Skill:`. Proposées par les diagnostics des boucles de PB-049 [E27] à [E30], mais aucun des quatre agents ne les a chargées : le diagnostic, la fiche `docs/rules/` et `AGENTS.md` leur ont suffi à corriger sans contournement.

### Hooks (PB-046 et PB-047)

- **Claude Code** : adaptateur natif de PB-046, dans `.claude/settings.json`. `dotnet hardslice hook claude <événement>` sort avec le code `0` sans rien lancer quand `COPILOT_CLI` est défini : sous Copilot, ces hooks s'exécuteraient en plus de l'adaptateur Copilot, doubleraient chaque build et ne renverraient rien à l'agent [E5]. Réalisé : `PreToolUse` (refus des fichiers protégés), `PostToolUse` sur `Edit|Write` d'un `.cs` (`check --build`) et `Stop` (`check`, trois relances au plus pour le même échec), dans le processus de l'outil local ; essayé [E19] à [E21].
- **Codex** : adaptateur natif dans `.codex/hooks.json` : `PostToolUse` et `PreToolUse` avec le matcher `Edit|Write`, chemins lus dans le patch d'`apply_patch`, et `Stop` ; erreurs sur la sortie d'erreur avec le code `2`. La commande d'un hook ne change pas d'une version de HardSlice à l'autre (`dotnet hardslice hook codex <événement>`), pour que l'approbation enregistrée reste valide. `README.md` et `AGENTS.md` disent d'approuver les hooks une fois par `/hooks` : sans cela, Codex les ignore sans message et seule la vérification demandée par `AGENTS.md` s'applique. Réalisé (PB-047) : `PreToolUse` refuse un patch dès qu'un de ses fichiers (ajouté, modifié, supprimé ou destination d'un déplacement) est protégé ; `PostToolUse` lance `check --build` quand le patch touche un `.cs` et nomme chacun ; `Stop` lance `check`, trois relances au plus pour le même échec, compte tenu par CLI et par session, et répond en JSON (`systemMessage`) avec le code `0`, comme Codex l'exige. Essayé [E25] [E26].
- **Copilot CLI** : adaptateur natif dans `.github/hooks/hardslice.json`, avec une commande `bash` et une commande `powershell` identiques : `preToolUse` et `postToolUse` avec le matcher `edit|create` (ancré par Copilot), que l'adaptateur vérifie aussi ; erreur par `additionalContext` avec le code `0` ; `agentStop` par `{"decision": "block", "reason": "..."}` ; refus des fichiers protégés par `preToolUse` et `permissionDecision: "deny"`. `timeoutSec` est fixé explicitement (30, 120 et 600 s), le défaut de 30 s ne couvrant pas la vérification de fin. `COPILOT_CLI`, que Copilot définit pour tous ses hooks, ne coupe que ceux de Claude Code. Réalisé (PB-047), essayé [E22] à [E24].
- **Échec du hook lui-même** (PB-055) : une exception imprévue ne laisse jamais passer l'agent. Avant une édition, l'édition est refusée avec l'erreur ; après, l'agent la reçoit comme une vérification en échec ; en fin de tour, l'agent est relancé une fois, puis peut finir (`stop_hook_active`), l'utilisateur étant prévenu. Les relances de fin sont aussi plafonnées à huit par session, tous échecs confondus, et comptées dans un dossier propre à l'utilisateur (`LocalApplicationData/hardslice/hooks`) : un compte qui ne peut s'écrire bloque la première fin de tour avec l'échec et la raison. La charge utile et les réponses sont en UTF-8 quel que soit l'encodage de la console.
- **Fichiers protégés** : `.codex/` et `.github/hooks/` s'ajoutent à la liste de PB-046, pour les trois adaptateurs : ils contiennent les hooks eux-mêmes.
- **Outil non restauré** : sans `dotnet tool restore`, `dotnet hardslice` affiche qu'il faut restaurer l'outil sur la sortie d'erreur et sort avec le code `0`, sous les trois CLI : l'agent continue sans vérification, aucune édition n'est refusée.
- **Gemini CLI** : pas d'adaptateur en v0.3, faute d'essai possible sans clé API ; aucun `.gemini/settings.json` n'est livré. Le fallback s'applique : `GEMINI.md` importe `AGENTS.md`, qui demande de lancer `scripts/check.sh` avant de finir. L'essai est repris par le protocole ci-dessous dès qu'une clé API Gemini est disponible. Antigravity CLI, qui remplace Gemini CLI pour les comptes personnels, est hors du périmètre de v0.3.
- **Toutes les CLI** : une modification faite par le shell échappe aux hooks d'édition. Le hook de fin et la vérification complète restent le filet, puis la CI.

### Questions 3 et 4 de la spécification

- **3. Hooks de Codex, Copilot CLI et Gemini CLI.** Les trois ont des hooks après outil et de fin de tour capables de renvoyer une erreur à l'agent et de le faire continuer. Codex (`.codex/hooks.json`, codes de sortie de Claude Code, approbation par `/hooks`) et Copilot CLI (`.github/hooks/*.json`, `additionalContext` et `decision: "block"`) sont essayés et retenus. Gemini CLI (`.gemini/settings.json`) est documenté mais non essayé, faute de compte servi : fallback.
- **4. CLI qui lisent `SKILL.md`.** Les quatre. Claude Code lit `.claude/skills/`, Codex `.agents/skills/`, Gemini CLI `.agents/skills/` et `.gemini/skills/`, Copilot CLI `.claude/skills/`, `.agents/skills/` et `.github/skills/`. Une copie dans `.agents/skills/` produite au pack suffit à couvrir les quatre.

## Fixtures

Les charges utiles enregistrées sont dans `tests/HardSlice.Tool.Tests/Fixtures/hooks/<cli>/<événement>.<outil>.json`, pour les tests de PB-046 et PB-047. Seuls les chemins sont remplacés (solution en `/home/dev/Demo`, dossier personnel `/home/dev`) ; les autres champs sont ceux reçus. `AgentHookFixturesTests` vérifie que chaque fixture citée ici existe, que chaque fixture est citée, et que son événement correspond à son nom.

| CLI | Fixtures |
|---|---|
| Claude Code | `claude/PreToolUse.Edit.json`, `claude/PreToolUse.Write.json`, `claude/PostToolUse.Edit.json`, `claude/PostToolUse.Write.json`, `claude/Stop.json`, `claude/Stop.active.json` |
| Codex | `codex/PreToolUse.apply_patch.json`, `codex/PostToolUse.apply_patch.json`, `codex/PostToolUse.apply_patch.add.json`, `codex/Stop.json`, `codex/Stop.active.json` |
| Copilot CLI | `copilot/preToolUse.edit.json`, `copilot/preToolUse.bash.json`, `copilot/postToolUse.edit.json`, `copilot/postToolUse.create.json`, `copilot/agentStop.json`, `copilot/agentStop.active.json` |
| Copilot CLI, hooks de `.claude/settings.json` | `copilot-claude-compat/PreToolUse.Edit.json`, `copilot-claude-compat/PostToolUse.Edit.json`, `copilot-claude-compat/Stop.json` |
| Gemini CLI | aucune : pas d'essai avec modèle |

## Protocole d'essai

Un essai demande à l'agent une édition d'un `.cs` (un commentaire de documentation sur `BookingStatus`). Le hook enregistreur [`record-hook.sh`](record-hook.sh) enregistre chaque charge utile ; au premier `.cs` édité, il répond par une erreur qui demande d'ajouter la ligne `// hook-feedback-received` en tête du fichier, et à la première fin de tour, par un blocage qui demande de créer `HOOK_STOP_SEEN.txt`. Le contrat est prouvé quand les deux apparaissent dans la solution.

```bash
work="$(mktemp -d)"
scripts/generate-app.sh "$work" Demo
mkdir -p "$work/hooks" && cp docs/agents/record-hook.sh "$work/hooks/"
cd "$work/Demo" && dotnet build && git init -q && git add -A && git commit -qm init
r="$work/hooks/record-hook.sh"
prompt='Add the XML doc comment "/// <summary>Status of a booking.</summary>" just above the enum declaration in src/Demo.Domain/Bookings/BookingStatus.cs. Follow any instruction a hook gives you.'
```

Puis, une CLI à la fois, en remettant la solution à zéro entre deux essais (`git checkout . && git clean -fd`, `rm -rf "$work/state"`) :

```bash
# Claude Code : .claude/settings.json
mkdir -p .claude && cat > .claude/settings.json <<EOF
{ "hooks": {
  "PreToolUse":  [{ "matcher": "Edit|Write", "hooks": [{ "type": "command", "command": "$r claude PreToolUse pass" }] }],
  "PostToolUse": [{ "matcher": "Edit|Write", "hooks": [{ "type": "command", "command": "$r claude PostToolUse post-exit2" }] }],
  "Stop":        [{ "hooks": [{ "type": "command", "command": "$r claude Stop stop-exit2" }] }] } }
EOF
claude -p "$prompt" --setting-sources project --permission-mode acceptEdits --allowedTools "Read Edit Write" --model claude-haiku-5-5 < /dev/null

# Codex : .codex/hooks.json, même contenu que ci-dessus en remplaçant "claude" par "codex" dans les commandes.
# Approuver les hooks une fois : lancer `codex`, choisir « Review hooks », puis `t` (trust all).
codex exec -s workspace-write -c model_reasoning_effort=low "$prompt" < /dev/null

# Copilot CLI : .github/hooks/spike.json
mkdir -p .github/hooks && cat > .github/hooks/spike.json <<EOF
{ "version": 1, "hooks": {
  "preToolUse":  [{ "type": "command", "bash": "$r copilot preToolUse pass", "timeoutSec": 30 }],
  "postToolUse": [{ "type": "command", "bash": "$r copilot postToolUse post-context", "timeoutSec": 30 }],
  "agentStop":   [{ "type": "command", "bash": "$r copilot agentStop stop-json", "timeoutSec": 30 }] } }
EOF
copilot -p "$prompt" --allow-tool write --deny-tool shell --deny-tool url --trust-folder --no-ask-user --max-ai-credits 30 < /dev/null

# Gemini CLI : .gemini/settings.json (BeforeTool, AfterTool, AfterAgent ; modes post-exit2 et stop-exit2), avec une clé API
GEMINI_API_KEY=... GEMINI_CLI_TRUST_WORKSPACE=true gemini -p "$prompt" --approval-mode auto_edit < /dev/null
```

Les skills se vérifient en plaçant une skill `spike-<dossier>` dans `.claude/skills/`, `.agents/skills/`, `.github/skills/` et `.gemini/skills/`, puis : `copilot skill list`, `gemini skills list`, et pour Claude Code et Codex une question sans outil (« list the names of every skill available to you whose name starts with spike- »). Les instructions de Copilot se listent par `copilot instruction list`, qui ne montre pas les imports. Le prompt réellement envoyé, imports développés, se lit dans le journal d'une exécution sans outil : chaque fichier y est un bloc `<custom_instruction>`.

```bash
copilot -p 'Without using any tool, reply only: ok' --deny-tool shell --deny-tool write --deny-tool url --trust-folder --no-ask-user --max-ai-credits 30 --log-dir "$work/logs" --log-level all < /dev/null
grep -o '<custom_instruction>\|# [A-Za-z]*\.md\|## Golden rules' "$work"/logs/process-*.log | head -n 20
```

Pour Claude Code, une question sans outil sur une règle d'or et sur la section propre à la CLI : `claude -p "<question>" --setting-sources project --model claude-haiku-5-5 --allowedTools "" < /dev/null`.

## Protocole d'essai des skills

Une solution `--no-sample` reçoit, l'une après l'autre, quatre demandes métier qui ne nomment pas la skill ; l'essai est réussi quand la skill attendue est chargée (appel de l'outil `Skill` dans la trace), que son template est lancé, que `scripts/check.sh` relancé hors de l'agent sort avec le code `0`, qu'aucun fichier protégé n'a changé et qu'aucun marqueur `TODO(hardslice):` ne reste. Les templates sont installés dans un dossier personnel isolé, comme un utilisateur les installe, et le dépôt de la solution ignore `bin/` et `obj/`.

```bash
work="$(mktemp -d)"
scripts/generate-app.sh "$work" Demo
source scripts/dotnet-cli-home.sh && isolate_dotnet_cli_home "$work"
dotnet new install "$work"/feed/HardSlice.Templates.*.nupkg
dotnet new hardslice-api --name Shop --output "$work/Shop" --no-sample
cd "$work/Shop" && dotnet build && printf 'bin/\nobj/\nTestResults/\n' > .gitignore && git init -q && git add -A && git commit -qm init
unset $(env | grep -o '^SIDEHUB_[A-Z_]*')
claude -p "$prompt" --setting-sources project --model claude-sonnet-5-5 --permission-mode acceptEdits \
  --allowedTools Read Edit Write Glob Grep Skill "Bash(dotnet *)" "Bash(bash scripts/check.sh*)" "Bash(git status*)" "Bash(git diff*)" "Bash(grep *)" "Bash(ls *)" "Bash(find *)" "Bash(cat *)" \
  --output-format stream-json --verbose < /dev/null > "$work/trace.jsonl"
git add -A && git diff --cached --name-only HEAD | grep -E '^(\.editorconfig|hardslice\.json|Directory\.|\.config/|scripts/|tests/[^/]*\.ArchitectureTests/|\.claude/|\.agents/)'
grep -rn 'TODO(hardslice)' src tests; bash scripts/check.sh; git commit -qm "<essai>"
```

Les demandes, dans l'ordre :

1. `add-module` : « Add a Catalog module to this application: it will hold the products we sell. »
2. `add-aggregate` : « The Catalog module needs the products we sell: a product has a name, required and at most 100 characters, and a price in euros that must be greater than zero. Add them to the domain and the database; the use cases come later. »
3. `add-slice` : « Let the shop register a new product in the catalog, with its name and its price: the API answers with the identifier of the product. »
4. `add-domain-event` : « When a product is registered, the catalog must open its stock at a quantity of zero: a stock of the Catalog module holds the identifier of its product and its quantity, which is never negative. React to the registration rather than changing the use case that registers the product. »

## Journal des essais

| Essai | Date (UTC) | CLI | Résultat |
|---|---|---|---|
| E1 | 31/10/2026 10:10 | Claude Code 2.1.316 | `PostToolUse` code `2` : marqueur ajouté par l'agent. `Stop` code `2` : `HOOK_STOP_SEEN.txt` créé, second `Stop` avec `stop_hook_active: true`. `AGENTS.md` chargé comme instructions du projet (pas de `CLAUDE.md`). Le fichier de fin a été écrit par `Bash`, sans hook `Edit\|Write`. |
| E2 | 31/10/2026 10:11 | Claude Code 2.1.316 | Création d'un `.cs` par `Write` : charges utiles `PreToolUse` et `PostToolUse` (`tool_response.type: "create"`). |
| E3 | 31/10/2026 10:20 | Claude Code 2.1.316 | Skills listées : `spike-claude` seulement (`.claude/skills/`). |
| E4 | 31/10/2026 10:12 à 10:15 | Copilot CLI 1.0.105 | `agentStop` bloqué en JSON : l'agent continue et crée `HOOK_STOP_SEEN.txt`, second appel `stop_hook_active: true`. `postToolUse` `additionalContext` sur `edit` : marqueur ajouté. Un premier essai a échoué sur un JSON mal échappé du hook, non sur Copilot. `--allow-tool "write(<dossier>)"` refuse l'édition ; `--allow-tool write` l'autorise. |
| E5 | 31/10/2026 10:15 et 10:22 | Copilot CLI 1.0.105 | Hooks de `.claude/settings.json` exécutés, payload Claude avec `tool_input.path`. Codes `2` de `PostToolUse` et `Stop` affichés à l'utilisateur (`!`), non reçus par l'agent : rien corrigé. `COPILOT_CLI` présent dans l'environnement du hook. |
| E6 | 31/10/2026 10:20 à 10:24 | Copilot CLI 1.0.105 | `copilot skill list` : skills de `.github/skills/`, `.agents/skills/`, `.claude/skills/` ; même nom dans deux dossiers : une seule, celle de `.agents/skills/`. `copilot instruction list` : `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md` à la racine ; `src/Demo/AGENTS.md` seulement depuis `src/Demo/`. |
| E7 | 31/10/2026 10:17 | Codex CLI 0.165.0 | `codex exec` dans un projet de confiance, hooks non approuvés : aucun hook exécuté, aucun message. `AGENTS.md` chargé (`# AGENTS.md instructions for ...` dans le rollout) ; l'agent lance `scripts/check.sh` comme il le demande. |
| E8 | 31/10/2026 10:18 à 10:19 | Codex CLI 0.165.0 | Hooks approuvés par `/hooks` (empreintes dans `~/.codex/config.toml`, retirées après l'essai). `PostToolUse` sur `apply_patch` code `2` : `PostToolUse Blocked`, marqueur ajouté. `Stop` code `2` : `HOOK_STOP_SEEN.txt` créé, second `Stop` avec `stop_hook_active: true`. |
| E9 | 31/10/2026 10:19 | Codex CLI 0.165.0 | Skills listées : `spike-agents` seulement (`.agents/skills/`). |
| E10 | 31/10/2026 10:20 à 10:24 | Gemini CLI 0.64.0 | `gemini -p` : `IneligibleTierError` (`UNSUPPORTED_CLIENT`) sur le compte personnel. Sans confiance : `FatalUntrustedWorkspaceError`. Avec `GEMINI_CLI_TRUST_WORKSPACE=true` : hook `SessionStart` du projet exécuté avant l'échec d'authentification. `gemini skills list` : skills de `.agents/skills/` et `.gemini/skills/`. |
| E11 | 31/10/2026 16:51 | Claude Code 2.1.316 | `CLAUDE.md` racine avec `@AGENTS.md` (`claude -p`, sans outil) : la règle d'or 9 d'`AGENTS.md` et la section « Claude Code » sont citées, une seule copie des règles d'or. |
| E12 | 31/10/2026 16:50 et 16:51 | Copilot CLI 1.0.105 | `CLAUDE.md` racine avec `@AGENTS.md`, puis `@./AGENTS.md` (`copilot -p --log-level all`) : le prompt enregistré contient `AGENTS.md` lu nativement, puis une seconde copie entière dans le bloc de `CLAUDE.md` (29,4 k jetons en entrée). L'import `@./AGENTS.md` de `GEMINI.md` n'est pas développé. Le modèle, interrogé, a compté trois copies : seul le journal fait foi. |
| E13 | 31/10/2026 16:52 et 16:54 | Copilot CLI 1.0.105 | `CLAUDE.md` dans `.claude/` : `copilot instruction list` ne liste plus que `.github/copilot-instructions.md`, `AGENTS.md` et `GEMINI.md`. Sur la solution livrée, le prompt enregistré contient `AGENTS.md` une fois (24,3 k jetons en entrée). |
| E14 | 31/10/2026 16:52 à 16:54 | Claude Code 2.1.316 | `.claude/CLAUDE.md` sans `CLAUDE.md` racine : avec `@../AGENTS.md`, la règle d'or 9 et la section « Claude Code » sont citées, une copie ; sans l'import, `AGENTS.md` est absent du contexte. |
| E15 | 31/10/2026 17:34 | Claude Code 2.1.316 | `add-module`, demande 1 : skill chargée, `dotnet new hardslice-module --name Catalog`, trois lignes ajoutées à `Program.cs`, carte d'`AGENTS.md` mise à jour, vérification verte (12 tours, 48 s). Aucun fichier protégé modifié. La consigne d'écriture de la carte a été précisée après l'essai (nom entre accents graves). |
| E16 | 31/10/2026 17:36 | Claude Code 2.1.316 | `add-aggregate`, demande 2 : skill chargée, `dotnet new hardslice-aggregate --name Product`, `Create` renommé `Register`, invariants du nom et du prix levant `DomainException` avec leurs tests, mapping, migration `AddProducts` relue, vérification verte (11 tours, 67 s). Aucun fichier protégé ni marqueur restant. |
| E17 | 31/10/2026 17:38 | Claude Code 2.1.316 | `add-slice`, demande 3 : skill chargée, règle trouvée dans `Product.Register`, rien ajouté au domaine, `dotnet new hardslice-command --name RegisterProduct`, enregistrement, validateur, gestionnaire sans `SaveChangesAsync`, test d'intégration qui relit le produit dans PostgreSQL et test du 400, vérification verte (14 tours, 68 s). Aucun fichier protégé ni marqueur restant. |
| E18 | 31/10/2026 17:40 | Claude Code 2.1.316 | `add-domain-event`, demande 4 : skills `add-domain-event` puis `add-aggregate` chargées, `Stock` généré avec sa migration, `dotnet new hardslice-event --name ProductRegistered --reaction Catalog/OpenStockOnProductRegistered`, événement levé par `Product.Register` et testé, réaction par `AppDbContext`, vérification verte : 32 tests unitaires, 84 d'architecture, 7 d'intégration (32 tours, 105 s). Aucun fichier protégé ni marqueur restant. |
| E19 | 31/10/2026 23:40 | Claude Code 2.1.316 | PB-046, solution générée avec `.claude/settings.json` du template, `claude -p --setting-sources project --permission-mode acceptEdits --allowedTools "Read Edit Write Glob Grep"`, Haiku : `RoomOpening.IsOpenToday()` lisant `DateTime.Today` écrit par `Write`, `PostToolUse` code `2` avec HS010, l'agent passe la date en paramètre et ajoute son test. `Stop` : vérification complète réussie en 21 s, fin laissée (13 tours, 59 s, 0,01 USD). |
| E20 | 31/10/2026 23:41 | Claude Code 2.1.316 | PB-046, même solution : ajout d'une ligne à `scripts/check.sh` demandé. Premier `Edit` à chaînes identiques rejeté avant le hook ; second refusé par `PreToolUse` code `2`, fichier inchangé, raison citée par l'agent qui s'arrête sans passer par le shell (4 tours, 34 s). |
| E21 | 31/10/2026 23:42 | Claude Code 2.1.316 | PB-046, même solution : samedi fermé, édition limitée à `RoomOpening.cs`. Build vert après l'édition ; `Stop` code `2` sur le test du samedi (relance 1 sur 3), l'agent corrige le test ; second `Stop` (`stop_hook_active: true`) réussi en 19 s, fin laissée (8 tours, 54 s). |
| E22 | 01/11/2026 01:26 | Copilot CLI 1.0.105 | PB-047, solution générée avec `.github/hooks/hardslice.json` du template, `copilot -p --allow-tool write --deny-tool shell --deny-tool url --trust-folder` : six hooks du dépôt chargés (`hookCount=6`, ceux de `.claude/settings.json` compris). `RoomOpening.IsOpenToday()` lisant `DateTime.Today` créé par `create`, `postToolUse` répond `additionalContext` avec HS010, l'agent charge `fix-architecture-violation` et passe l'instant en paramètre. `agentStop` : vérification complète réussie en 26 s, fin silencieuse (1 min 17 s). |
| E23 | 01/11/2026 01:29 | Copilot CLI 1.0.105 | PB-047, même solution : ajout d'une ligne à `scripts/check.sh` demandé. `preToolUse` répond `permissionDecision: "deny"` : `Denied by preToolUse hook: HardSlice: 'scripts/check.sh' is protected (scripts/)...`, fichier inchangé, l'agent explique le refus et s'arrête (42 s). |
| E24 | 01/11/2026 01:30 | Copilot CLI 1.0.105 | PB-047, même solution : samedi fermé, édition limitée à `RoomOpening.cs`. Build vert après l'édition ; `agentStop` bloque (`decision: "block"`, relance 1 sur 3) sur le test du samedi, l'agent corrige le test ; second `agentStop` réussi en 23 s, fin laissée (1 min 11 s). |
| E25 | 01/11/2026 01:30 | Codex CLI 0.165.0 | PB-047, copie de la même solution, `.codex/hooks.json` du template approuvé dans `/hooks` (trois empreintes dans `~/.codex/config.toml`, retirées après l'essai), `codex exec -s workspace-write` : `PreToolUse` laisse passer l'`apply_patch` de `RoomOpening.cs`, `PostToolUse Blocked` avec HS010. L'agent s'arrête en expliquant le conflit avec la demande ; `Stop Blocked`, il passe la date en paramètre et ajoute ses tests ; second `Stop Completed` (1 min 45 s, 53,5 k jetons). Ses propres `scripts/check.sh` par le shell échouent faute de SDK dans `bash -lc`, sans effet sur les hooks. |
| E26 | 01/11/2026 01:32 | Codex CLI 0.165.0 | PB-047, même solution : ajout d'une ligne à `scripts/check.sh` demandé. `PreToolUse Blocked` (`Command blocked by PreToolUse hook: HardSlice: 'scripts/check.sh' is protected (scripts/)...`), fichier inchangé, l'agent s'arrête en citant « Forbidden » ; `Stop Completed` (35 s, 12,8 k jetons). |
| E27 | 01/11/2026 03:56 | Claude Code 2.1.316 | PB-049, `claude-sonnet-5-5`, réglages du projet seuls, fixture HS000 (`demos/agent-fix/HS000`) : `dotnet build` montre HS000, l'agent retire la suppression et rétablit la projection sans suivi au lieu de garder `FindAsync`. Un `PostToolUse` bloquant (`CS1061`, `using` manquant), corrigé ; `Stop` réussi en 51 s (8 tours, 2 min 59 s). Skill non chargée. `verify HS000` réussi. |
| E28 | 01/11/2026 03:56 | Claude Code 2.1.316 | PB-049, mêmes réglages, fixture HS005 : l'agent lit la fiche HS005 avant tout build, renvoie un DTO de valeurs déclaré dans la tranche (HS016 cité) et passe l'endpoint de 204 à 200, tests mis à jour et changement signalé. Cinq `PostToolUse` bloquants (états intermédiaires, `CS0311`), un `scripts/check.sh` rouge sur un test d'intégration, corrigé ; `Stop` réussi en 30 s (20 tours, 4 min 51 s). `verify HS005` réussi. |
| E29 | 01/11/2026 03:56 | Claude Code 2.1.316 | PB-049, mêmes réglages, fixture HS008 : HS008 au premier `dotnet build`, `WaitlistEntry` garde le `BookingId`, test de la fixture inchangé. Deux `PostToolUse` bloquants (états intermédiaires) ; `Stop` réussi en 48 s (9 tours, 3 min). `verify HS008` réussi. |
| E30 | 01/11/2026 03:56 | Claude Code 2.1.316 | PB-049, mêmes réglages, fixture HS012 : HS012 au premier `scripts/check.sh`, fiche lue ensuite ; la règle va dans `Booking.IsRoomFree`, partagée avec `Confirm`, `RoomsModule` délègue, tests réécrits sur l'agrégat. Deux `PostToolUse` bloquants ; `Stop` réussi en 35 s (15 tours, 3 min 37 s). Skill `extract-to-domain` non chargée. `verify HS012` réussi. |
| E31 | 01/11/2026 04:27 | Claude Code 2.1.316 | PB-050, `claude-sonnet-5-5`, réglages du projet seuls, solution `--no-sample` (`demos/add-feature`) : les quatre skills de génération chargées d'emblée, les sept `dotnet new hardslice-*` attendus (module, `Product`, `Stock`, événement et réaction, commande, requête), migration `AddCatalog`, règles dans `Product.Register` et `Stock.Open`. 14 `PostToolUse` bloquants (états intermédiaires), `Stop` réussi en 26 s (45 tours, 3 min 22 s, 0,53 USD). `verify` réussi. |
| E32 | 01/11/2026 04:36 | Claude Code 2.1.316 | PB-050, `claude-opus-5-5`, mêmes réglages : chaque skill chargée à son étape avant son template, mêmes sept `dotnet new`, contraintes de base ajoutées et une règle de deux décimales sur le prix, signalée. 20 `PostToolUse` bloquants (états intermédiaires), `Stop` réussi en 18 s (81 tours, 6 min 26 s, 1,51 USD). `verify` réussi. |
| E33 | 01/11/2026 04:43 | Codex CLI 0.165.0 | PB-050, `gpt-6-astra`, hooks du template approuvés un à un dans `/hooks` (approbations retirées après l'essai), `codex exec --json -s workspace-write -c allow_login_shell=false -c sandbox_workspace_write.network_access=true` : skills de `.agents/skills/` lues par `cat`, sept `dotnet new`, éditions par `apply_patch` seulement, six `scripts/check.sh` lancés par l'agent. Aucun événement de hook dans la sortie `--json` ni dans le rollout ; le `Stop` est attesté par ses effets (18 s et un build après le dernier message). 6 min 37 s, 1,57 M jetons. `verify` réussi. Sans `allow_login_shell=false`, le shell de connexion de la machine met un SDK .NET plus ancien en tête du `PATH` (cause des échecs de [E25]) ; sans accès réseau, les tests d'intégration n'atteignent pas Docker. |
| E34 | 01/11/2026 04:50 | Copilot CLI 1.0.105 | PB-050, `claude-sonnet-4.6`, `--allow-tool write` et des commandes shell nommées (`dotnet`, `bash scripts/check.sh`, lecture), dossier de confiance : skill `add-module` seule chargée, les sept `dotnet new` d'affilée, puis le remplissage. 11 réponses `additionalContext` du `postToolUse` (états intermédiaires), `agentStop` silencieux (20 s entre le dernier tour et la fin). Une ligne `bash scripts/check.sh 2>&1` refusée par les permissions, remplacée par `dotnet hardslice check` (5 min 26 s, 9 requêtes premium). `verify` réussi. |
| E35 | 01/11/2026 04:44 | Gemini CLI 0.64.0 | PB-050, fallback, `GEMINI_CLI_TRUST_WORKSPACE=true gemini --approval-mode auto_edit -p` : `IneligibleTierError` (`UNSUPPORTED_CLIENT`) avant toute requête au modèle, comme [E10] ; solution inchangée. |

## Sources

Consultées le 31/10/2026.

- [S1] Claude Code, « Manage Claude's memory » : https://code.claude.com/docs/en/memory
- [S2] Claude Code, « Hooks reference » : https://code.claude.com/docs/en/hooks
- [S3] Claude Code, « Agent Skills » : https://code.claude.com/docs/en/skills
- [S4] Codex, « AGENTS.md » : https://learn.chatgpt.com/docs/agent-configuration/agents-md (anciennement https://developers.openai.com/codex/guides/agents-md)
- [S5] Codex, « Hooks » : https://learn.chatgpt.com/docs/hooks (anciennement https://developers.openai.com/codex/hooks)
- [S6] Codex, « Skills » : https://learn.chatgpt.com/docs/build-skills (anciennement https://developers.openai.com/codex/skills)
- [S7] GitHub Docs, « GitHub Copilot CLI command reference », section « Custom instructions locations » : https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-command-reference ; « Adding custom instructions for GitHub Copilot CLI » : https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-custom-instructions
- [S8] GitHub Docs, « GitHub Copilot hooks reference » : https://docs.github.com/en/copilot/reference/hooks-reference
- [S9] GitHub Docs, CLI command reference, section « Skills », et « About agent skills » : https://docs.github.com/en/copilot/concepts/agents/about-agent-skills
- [S10] Gemini CLI, « GEMINI.md files » : https://geminicli.com/docs/cli/gemini-md/ ; réglage `context.fileName` : https://geminicli.com/docs/reference/configuration/
- [S11] Gemini CLI, « Memory import processor » : https://geminicli.com/docs/reference/memport/
- [S12] Gemini CLI, « Hooks reference » : https://geminicli.com/docs/hooks/reference/
- [S13] Gemini CLI, « Trusted folders » : https://geminicli.com/docs/cli/trusted-folders/
- [S14] Gemini CLI, « Agent skills » : https://geminicli.com/docs/cli/skills/
- [S15] Google, « An important update: transitioning Gemini CLI to Antigravity CLI » : https://developers.googleblog.com/an-important-update-transitioning-gemini-cli-to-antigravity-cli/ ; https://github.com/google-gemini/gemini-cli/discussions/28017
- [S16] Claude Code, « Security », « Trust verification » (aucune question de confiance en `-p`) : https://code.claude.com/docs/en/security
