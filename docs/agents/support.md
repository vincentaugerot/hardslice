# Prise en charge des agents : instructions, hooks et skills

Ce document établit, pour Claude Code, Codex, GitHub Copilot CLI et Gemini CLI, ce que chaque CLI lit réellement à sa version du moment : fichier d'instructions, `AGENTS.md`, hooks après édition et de fin de tâche, skills (PB-034). Il décide le contenu de PB-042 (fichiers d'instructions), PB-043 (skills) et PB-047 (adaptateurs), et précise PB-046 (hooks Claude Code). Il tranche les questions 3 et 4 de la [spécification](../specifications.md) (section 13).

Chaque constat renvoie à une source officielle (`[S..]`, liste en fin de document) et à un essai daté (`[E..]`, journal plus bas). Un mécanisme seulement documenté est marqué « documenté, non essayé » et n'est pas retenu pour un adaptateur.

## Versions essayées

Essais du 31/10/2026 (UTC) sur Linux x64, dans une solution générée par `scripts/generate-app.sh` (variante avec Bookings).

| CLI | Version | Compte | Essais avec modèle |
|---|---|---|---|
| Claude Code | 2.1.316 | claude.ai | oui |
| Codex CLI | 0.165.0 | ChatGPT | oui |
| GitHub Copilot CLI | 1.0.105 | Copilot | oui |
| Gemini CLI | 0.64.0 | compte Google personnel, refusé | non : `UNSUPPORTED_CLIENT` [E10] |

## Synthèse

| | Claude Code | Codex | Copilot CLI | Gemini CLI |
|---|---|---|---|---|
| Fichier d'instructions | `CLAUDE.md` (cwd et parents), `AGENTS.md` sans `CLAUDE.md` [S1] [E1] | `AGENTS.md` (racine du dépôt jusqu'au cwd, 32 Kio au plus) [S4] [E7] | `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `.github/copilot-instructions.md`, tous fusionnés, à la racine git et dans le cwd [S7] [E6] | `GEMINI.md` (global, cwd et parents, sous-dossiers) [S10] ; non essayé [E10] |
| Lecture d'`AGENTS.md` | native seulement sans `CLAUDE.md` ; avec `CLAUDE.md`, par `@AGENTS.md` [S1] [E1] | native [S4] [E7] | native [S7] [E6] | par `@./AGENTS.md` dans `GEMINI.md` ou `context.fileName` [S10] [S11] ; documenté, non essayé [E10] |
| Hook après édition | `PostToolUse`, matcher `Edit\|Write`, `tool_input.file_path` [S2] [E1] [E2] | `PostToolUse`, matcher `Edit\|Write` qui couvre `apply_patch`, chemins dans le texte du patch [S5] [E8] | `postToolUse` (`.github/hooks/*.json`), `toolName` `edit` ou `create`, `toolArgs.path` [S8] [E4] | `AfterTool`, matcher `write_file\|replace` [S12] ; documenté, non essayé [E10] |
| Erreur renvoyée à l'agent après édition | code `2`, message sur la sortie d'erreur [S2] [E1] | code `2`, message sur la sortie d'erreur [S5] [E8] | code `0` et `{"additionalContext": "..."}` ; le code `2` n'est qu'un avertissement affiché à l'utilisateur [S8] [E4] | code `2` ou `{"decision": "deny", "reason": "..."}` [S12] ; documenté, non essayé [E10] |
| Hook de fin de tâche | `Stop`, `stop_hook_active` [S2] [E1] | `Stop`, `stop_hook_active` [S5] [E8] | `agentStop`, `stop_hook_active` [S8] [E4] | `AfterAgent`, `stop_hook_active` [S12] ; documenté, non essayé [E10] |
| Fin bloquée, l'agent continue | code `2`, raison sur la sortie d'erreur [S2] [E1] | code `2`, raison sur la sortie d'erreur [S5] [E8] | code `0` et `{"decision": "block", "reason": "..."}`, 8 relances au plus [S8] [E4] | code `2` ou `{"decision": "deny", "reason": "..."}` [S12] ; documenté, non essayé [E10] |
| Condition de chargement des hooks du dépôt | aucune en `-p` [S16] [E1] ; en interactif, confiance du dossier demandée au premier lancement [S16] | projet de confiance et chaque hook approuvé par `/hooks`, sinon ignoré sans message [S5] [E7] [E8] | dossier de confiance (`--trust-folder` ou `COPILOT_ALLOW_ALL=true` en `-p`) [S8] [E4] | dossier de confiance [S13] [E10] |
| Skills (`SKILL.md`) lues | `.claude/skills/` [S3] [E3] | `.agents/skills/` [S6] [E9] | `.github/skills/`, `.agents/skills/`, `.claude/skills/` [S9] [E6] | `.agents/skills/`, `.gemini/skills/` [S14] [E10] |
| Adaptateur retenu | natif (PB-046) | natif (PB-047) | natif (PB-047) | fallback (PB-047) |

## Constats par CLI

### Claude Code 2.1.316

- **Instructions.** Claude Code lit `CLAUDE.md` du dossier courant et de ses parents. Il lit `AGENTS.md` nativement depuis la 2.1.277, mais seulement quand aucun `CLAUDE.md` n'existe ; sinon seul `CLAUDE.md` est chargé, et `@AGENTS.md` l'importe [S1]. Dans la solution générée sans `CLAUDE.md`, `AGENTS.md` est chargé comme instructions du projet [E1].
- **Après édition.** `PostToolUse` avec le matcher `Edit|Write`. La charge utile porte `tool_name`, `tool_input.file_path` et `tool_response` [S2]. Un code `2` renvoie la sortie d'erreur à l'agent, qui a corrigé le fichier comme demandé [E1]. Fixtures : `claude/PostToolUse.Edit.json`, `claude/PostToolUse.Write.json`, `claude/PreToolUse.Edit.json`, `claude/PreToolUse.Write.json`.
- **Fin de tâche.** `Stop` : un code `2` bloque la fin et la sortie d'erreur devient la consigne ; l'agent a continué puis s'est arrêté au second appel, où `stop_hook_active` vaut `true` [E1]. Fixtures : `claude/Stop.json`, `claude/Stop.active.json`.
- **Refus d'une édition.** `PreToolUse`, code `2` ou `permissionDecision: "deny"` [S2] ; documenté, non essayé (PB-046 l'essaie).
- **Skills.** `.claude/skills/<nom>/SKILL.md`, en-tête `name` et `description` [S3]. Seul `.claude/skills/` est lu : une skill de `.agents/skills/` n'apparaît pas [E3].
- **Limite.** Une écriture faite par l'outil `Bash` (`printf ... > fichier`) ne déclenche pas les hooks `Edit|Write` : l'agent l'a fait pour créer un fichier pendant l'essai [E1].

### Codex CLI 0.165.0

- **Instructions.** `AGENTS.md` du dossier racine du projet jusqu'au dossier courant, un fichier par dossier, 32 Kio au total par défaut (`project_doc_max_bytes`) ; `CLAUDE.md` n'est pas lu [S4]. Le rollout de l'essai contient `# AGENTS.md instructions for <solution>`, et l'agent a lancé de lui-même `scripts/check.sh` comme le demande `AGENTS.md` [E7].
- **Hooks.** Stables et actifs par défaut (`codex features list` : `hooks stable true`), dans `.codex/hooks.json` du projet, au même format que Claude Code [S5]. Un appel `apply_patch` correspond au matcher `Edit|Write`, mais la charge utile garde `tool_name: "apply_patch"` et le patch entier dans `tool_input.command` : les fichiers touchés se lisent sur les lignes `*** Update File:`, `*** Add File:` et `*** Delete File:`, plusieurs par appel [S5] [E8].
- **Après édition.** Un code `2` renvoie la sortie d'erreur à l'agent (`PostToolUse Blocked`), qui a corrigé le fichier [E8]. Fixtures : `codex/PreToolUse.apply_patch.json`, `codex/PostToolUse.apply_patch.json`, `codex/PostToolUse.apply_patch.add.json`.
- **Fin de tâche.** `Stop` avec code `2` : l'agent a continué, puis le second appel (`stop_hook_active: true`) l'a laissé finir [E8]. Avec un code `0`, `Stop` exige du JSON sur la sortie standard [S5]. Fixtures : `codex/Stop.json`, `codex/Stop.active.json`.
- **Approbation des hooks.** Le projet doit être de confiance, et chaque hook non géré doit être approuvé dans l'interface `/hooks` (« Hooks need review ») [S5] [E8]. L'approbation est enregistrée dans `~/.codex/config.toml`, sous `[hooks.state."<chemin absolu>/.codex/hooks.json:<événement>:<i>:<j>"]` avec `trusted_hash = "sha256:..."` : elle vaut pour un clone et une configuration donnés. Un hook non approuvé est ignoré sans aucun message par `codex exec` [E7]. `--dangerously-bypass-hook-trust` contourne l'approbation pour une exécution [S5] ; non utilisé.
- **Skills.** `.agents/skills/` du dossier courant jusqu'à la racine du dépôt, `~/.agents/skills/`, `/etc/codex/skills` [S6]. Ni `.claude/skills/` ni `.github/skills/` ne sont lus [E9].
- **Limites.** Une modification par le shell (`exec_command`) ne correspond pas à `Edit|Write`. Les hooks sont « a useful guardrail, not a complete enforcement boundary » [S5].

### GitHub Copilot CLI 1.0.105

- **Instructions.** Copilot fusionne, sans ordre de priorité, `AGENTS.md`, `CLAUDE.md`, `GEMINI.md` et `.github/copilot-instructions.md` de la racine git et du dossier courant, plus `.github/instructions/**/*.instructions.md` et `.claude/rules/**/*.md` [S7]. `copilot instruction list` les liste tous les quatre à la racine ; un `AGENTS.md` placé dans `src/Demo/` n'est lu que si `src/Demo/` est le dossier courant [E6]. Les imports `@chemin` sont suivis dans `AGENTS.md`, `CLAUDE.md` et `copilot-instructions.md` [S7].
- **Hooks natifs.** `.github/hooks/*.json`, format `{"version": 1, "hooks": {"postToolUse": [{"type": "command", "bash": "...", "powershell": "...", "timeoutSec": 30, "matcher": "..."}]}}`, matcher ancré sur `toolName` [S8]. Charge utile en camelCase : `toolName` (`view`, `edit`, `create`, `bash`), `toolArgs.path`, `toolResult` [E4].
- **Après édition.** Le code `2` d'un `postToolUse` n'est qu'un avertissement affiché à l'utilisateur [S8]. L'erreur atteint l'agent par `{"additionalContext": "..."}` sur la sortie standard avec un code `0`, ajouté au résultat de l'outil : l'agent a corrigé le fichier [E4]. Un `postToolUse` part aussi après une lecture (`view`) : l'adaptateur filtre `edit` et `create`. Fixtures : `copilot/preToolUse.edit.json`, `copilot/preToolUse.bash.json`, `copilot/postToolUse.edit.json`, `copilot/postToolUse.create.json`.
- **Fin de tâche.** `agentStop` avec `{"decision": "block", "reason": "..."}` : la raison devient un nouveau message et l'agent continue ; le second appel porte `stop_hook_active: true`. Après 8 blocages consécutifs, la CLI termine quand même [S8] [E4]. Fixtures : `copilot/agentStop.json`, `copilot/agentStop.active.json`.
- **Refus d'une édition.** `preToolUse` avec `permissionDecision: "deny"` et `permissionDecisionReason`, ou code `2` [S8] ; documenté, non essayé (PB-047 l'essaie).
- **Hooks de Claude Code exécutés aussi.** Copilot lit `.claude/settings.json` et y exécute les hooks en mode compatible Claude [S8] : `tool_name: "Edit"`, mais `tool_input.path` au lieu de `tool_input.file_path`, `timestamp`, pas de `transcript_path` après une édition [E5]. Dans ce mode, le code `2` de `PostToolUse` et de `Stop` est seulement affiché à l'utilisateur : l'agent ne l'a pas reçu, n'a rien corrigé et a fini [E5]. Copilot définit `COPILOT_CLI` dans l'environnement du hook, absent sous Claude Code [E5]. Fixtures : `copilot-claude-compat/PreToolUse.Edit.json`, `copilot-claude-compat/PostToolUse.Edit.json`, `copilot-claude-compat/Stop.json`.
- **Skills.** `.github/skills/`, `.agents/skills/` et `.claude/skills/` du projet, `~/.copilot/skills/`, `~/.agents/skills/` [S9] ; les trois dossiers du projet sont lus [E6]. Deux skills de même nom dans `.agents/skills/` et `.claude/skills/` n'apparaissent qu'une fois, celle de `.agents/skills/` [E6].
- **Limites.** Hooks et skills du dépôt ne sont chargés que dans un dossier de confiance [S8]. En `-p`, `--allow-tool "write(<dossier>)"` n'a pas autorisé les éditions sous ce dossier ; `--allow-tool write` l'a fait [E4]. Une modification par l'outil `bash` n'est pas une édition `edit`/`create`.

### Gemini CLI 0.64.0

- **Compte.** Depuis le 18/06/2026, Gemini CLI ne sert plus les comptes Google personnels (gratuits, Google AI Pro et Ultra) ; seuls une clé API Gemini, Vertex AI ou une licence Gemini Code Assist d'entreprise fonctionnent, et Google oriente les particuliers vers Antigravity CLI [S15]. Le compte de la machine d'essai est refusé (`IneligibleTierError`, `UNSUPPORTED_CLIENT`) : aucun essai avec modèle n'était possible [E10].
- **Instructions.** `GEMINI.md` global, du dossier courant et de ses parents jusqu'à la racine git, et des sous-dossiers [S10]. `AGENTS.md` peut être lu par `"context": {"fileName": ["AGENTS.md", "GEMINI.md"]}` dans `.gemini/settings.json` [S10], ou importé par `@./AGENTS.md` dans `GEMINI.md` [S11]. Documenté, non essayé.
- **Hooks.** Actifs par défaut (`hooksConfig.enabled`), dans `.gemini/settings.json`, événements `BeforeTool`, `AfterTool`, `AfterAgent` ; matcher en expression régulière sur `write_file|replace` ; code `2` ou `{"decision": "deny", "reason": "..."}` : après un outil, la raison remplace son résultat ; après la réponse de l'agent, elle devient un nouveau message [S12]. Les hooks du projet ne sont chargés que dans un dossier de confiance [S13]. Seul le chargement est essayé : en headless avec `GEMINI_CLI_TRUST_WORKSPACE=true`, le hook `SessionStart` du projet s'est exécuté avant l'échec d'authentification ; `AfterTool` et `AfterAgent` exigent un modèle [E10].
- **Skills.** Actives par défaut, dans `.agents/skills/` puis `.gemini/skills/` du projet, `~/.agents/skills/` et `~/.gemini/skills/` [S14]. `gemini skills list` liste celles de `.agents/skills/` et `.gemini/skills/`, pas celles de `.claude/skills/` ni de `.github/skills/` [E10].

## Décisions

### Fichiers d'instructions (PB-042)

- `CLAUDE.md` importe `AGENTS.md` (`@AGENTS.md`) : sans cet import, la seule présence de `CLAUDE.md` empêche Claude Code de lire `AGENTS.md` [S1].
- `GEMINI.md` importe `AGENTS.md` (`@./AGENTS.md`) plutôt que `context.fileName` : pas de `.gemini/settings.json` à livrer et à protéger. Non essayé avec un modèle : PB-042 le consigne comme tel.
- `.github/copilot-instructions.md` ne réimporte pas `AGENTS.md`, que Copilot lit déjà ; il ne contient que ce qui est propre à Copilot.
- Codex n'a besoin d'aucun fichier en plus d'`AGENTS.md`, qui reste très en dessous des 32 Kio.
- Copilot lit aussi `CLAUDE.md` et `GEMINI.md` : chacun commence par la CLI qu'il vise et ne contient rien de contraire aux autres. PB-042 vérifie avec Copilot que l'import `@AGENTS.md` de `CLAUDE.md` ne charge pas `AGENTS.md` deux fois ; la documentation annonce que les copies identiques sont dédupliquées [S7], ce qui n'est pas essayé.

### Skills (PB-043)

- Source unique : `.claude/skills/<skill>/SKILL.md`, lue par Claude Code et Copilot.
- Copie produite au pack dans `.agents/skills/<skill>/SKILL.md`, lue par Codex et Gemini CLI. Copilot lit les deux dossiers et ne garde qu'une skill par nom [E6] : les copies sont identiques, il n'y a pas de doublon.
- Pas de copie dans `.github/skills/` ni `.gemini/skills/`, inutiles.

### Hooks (PB-046 et PB-047)

- **Claude Code** : adaptateur natif de PB-046, dans `.claude/settings.json`. `dotnet hardslice hook claude <événement>` sort avec le code `0` sans rien lancer quand `COPILOT_CLI` est défini : sous Copilot, ces hooks s'exécuteraient en plus de l'adaptateur Copilot, doubleraient chaque build et ne renverraient rien à l'agent [E5].
- **Codex** : adaptateur natif dans `.codex/hooks.json` : `PostToolUse` et `PreToolUse` avec le matcher `Edit|Write`, chemins lus dans le patch d'`apply_patch`, et `Stop` ; erreurs sur la sortie d'erreur avec le code `2`. La commande d'un hook ne change pas d'une version de HardSlice à l'autre (`dotnet hardslice hook codex <événement>`), pour que l'approbation enregistrée reste valide. `README.md` et `AGENTS.md` disent d'approuver les hooks une fois par `/hooks` : sans cela, Codex les ignore sans message et seule la vérification demandée par `AGENTS.md` s'applique.
- **Copilot CLI** : adaptateur natif dans `.github/hooks/hardslice.json`, avec une commande `bash` et une commande `powershell` : `postToolUse` filtré sur `edit` et `create`, erreur par `additionalContext` avec le code `0` ; `agentStop` par `{"decision": "block", "reason": "..."}` ; refus des fichiers protégés par `preToolUse` si PB-047 l'essaie avec succès. `timeoutSec` est fixé explicitement, le défaut de 30 s ne couvrant pas la vérification de fin.
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

Les skills se vérifient en plaçant une skill `spike-<dossier>` dans `.claude/skills/`, `.agents/skills/`, `.github/skills/` et `.gemini/skills/`, puis : `copilot skill list`, `gemini skills list`, et pour Claude Code et Codex une question sans outil (« list the names of every skill available to you whose name starts with spike- »). Les instructions de Copilot se listent par `copilot instruction list`.

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
