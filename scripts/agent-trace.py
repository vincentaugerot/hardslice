#!/usr/bin/env python3
"""Reads the JSONL trace of an agent run: Claude Code (claude -p --output-format stream-json --verbose), Codex
(codex exec --json) or GitHub Copilot CLI (copilot -p --output-format json). Used by demo-add-feature.sh (PB-050).

  agent-trace.py format <trace>                 prints claude, codex or copilot.
  agent-trace.py commands <trace>               prints each shell command the agent ran with success, one JSON string per line.
  agent-trace.py skills <trace>                 prints each skill the agent loaded through its skill tool, one per line.
A tool call that the CLI refused or that failed is left out, and so is a Codex command without an exit code of 0.
  agent-trace.py expect <trace> <expectation>...
      fails unless, for each expectation <template>:<option>=<value>, a shell command of the trace runs
      dotnet new <template> with <option> <value> (or <option>=<value>), and prints the matching commands.
  agent-trace.py filter <trace> [<work> <home>]
      prints the trace as demos/ keeps it (PB-057): without the streaming events of Copilot, the inventory of the CLI (tools,
      plugins, skills, local paths and account state of the initialization and usage events) and the encrypted reasoning
      blocks, and with <work> and <home> replaced by /home/dev/work and /home/dev. A line it does not change is kept as is,
      so filtering a filtered trace changes nothing.
"""
import json
import re
import shlex
import sys


def events(path):
    with open(path, encoding="utf-8") as trace:
        for number, line in enumerate(trace, 1):
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except json.JSONDecodeError as error:
                sys.exit(f"error: {path}:{number} is not JSON: {error}")
            if isinstance(event, dict):
                yield event


def trace_format(path):
    for event in events(path):
        kind = event.get("type", "")
        if kind in ("system", "assistant", "user", "result") and ("session_id" in event or "message" in event):
            return "claude"
        if kind in ("thread.started", "turn.started") or kind.startswith("item."):
            return "codex"
        if "." in kind and "data" in event:
            return "copilot"
    sys.exit(f"error: {path} is not a trace of Claude Code, Codex or GitHub Copilot CLI.")


def tool_calls(path):
    """Yields (tool, input) for each tool call of the trace that ran and succeeded, shell commands as ("shell", command).
    A call the CLI refused, or a command that failed, never counts: Claude Code's is_error result, Codex's status and exit
    code, Copilot's success flag."""
    fmt = trace_format(path)
    started = {}
    for event in events(path):
        kind = event.get("type")
        if fmt == "claude" and kind == "assistant":
            for block in event.get("message", {}).get("content", []) or []:
                if isinstance(block, dict) and block.get("type") == "tool_use":
                    name = block.get("name", "")
                    arguments = block.get("input", {}) or {}
                    if name == "Bash":
                        started[block.get("id")] = ("shell", arguments.get("command", ""))
                    elif name == "Skill":
                        started[block.get("id")] = ("skill", arguments.get("skill", ""))
        elif fmt == "claude" and kind == "user":
            content = event.get("message", {}).get("content", [])
            for block in content if isinstance(content, list) else []:
                if isinstance(block, dict) and block.get("type") == "tool_result":
                    call = started.pop(block.get("tool_use_id"), None)
                    if call and not block.get("is_error"):
                        yield call
        elif fmt == "codex" and kind == "item.completed":
            item = event.get("item", {}) or {}
            if item.get("type") == "command_execution" and item.get("status") == "completed" and item.get("exit_code") == 0:
                yield "shell", item.get("command", "")
        elif fmt == "copilot" and kind == "tool.execution_start":
            data = event.get("data", {}) or {}
            name = data.get("toolName", "")
            arguments = data.get("arguments", {}) or {}
            if name in ("bash", "powershell", "shell"):
                started[data.get("toolCallId")] = ("shell", arguments.get("command", ""))
            elif name == "skill":
                started[data.get("toolCallId")] = ("skill", arguments.get("skill", "") or arguments.get("name", ""))
        elif fmt == "copilot" and kind == "tool.execution_complete":
            data = event.get("data", {}) or {}
            call = started.pop(data.get("toolCallId"), None)
            if call and data.get("success") is True:
                yield call


def commands(path):
    return [value for tool, value in tool_calls(path) if tool == "shell"]


def skills(path):
    return [value for tool, value in tool_calls(path) if tool == "skill"]


def unwrap(command):
    """The script of a command wrapped by the CLI, such as Codex's /bin/bash -lc '<script>'."""
    match = re.match(r"^\S*(?:ba|z)?sh\s+-l?c\s+(.*)$", command, re.S)
    if match:
        try:
            parts = shlex.split(match.group(1))
            if parts:
                return parts[0]
        except ValueError:
            pass
    return command


# The operators of the shell that end a simple command, longest first, as shlex groups punctuation characters into one token.
OPERATORS = re.compile(r"&&|\|\||;;|\|&|&>>?|>>|>&|<&|<<<?|>\||[;&|()<>\n]")
CONTROL = {"&&", "||", ";", ";;", "&", "|", "|&", "\n", "(", ")"}
# Words that may come before the command name of a simple command.
PREFIXES = {"{", "!", "time", "command", "exec", "if", "then", "do"}


def shell_words(script):
    """The words and operators of a shell script, quotes kept together: an operator inside a quoted string never splits it."""
    lexer = shlex.shlex(script, posix=True, punctuation_chars=";&|()<>\n")
    lexer.whitespace = " \t\r"
    lexer.whitespace_split = True
    lexer.commenters = ""
    tokens = []
    for token in lexer:
        if token and all(character in ";&|()<>\n" for character in token):
            tokens.extend(OPERATORS.findall(token))
        else:
            tokens.append(token)
    return tokens


def without_heredocs(script):
    """The script without the bodies of its here-documents, which are text given to a command, never commands."""
    kept = []
    delimiters = []
    for line in script.split("\n"):
        if delimiters:
            if line.strip() == delimiters[0]:
                delimiters.pop(0)
            continue
        kept.append(line)
        delimiters.extend(match.group(2) for match in re.finditer(r"(?<!<)<<(?!<)-?\s*(['\"]?)([A-Za-z_][A-Za-z0-9_]*)\1", line))
    return "\n".join(kept)


def simple_commands(script):
    """Yields the words of each simple command of a shell script that ran whenever the script succeeded, without its
    redirections. A command after || runs only when the one before it failed, and so do the commands chained to it by &&
    or a pipe: they are left out, up to the next ;, & or line, and a subshell keeps the state of the place it opens at."""
    try:
        # A backslash at the end of a line continues the command on the next one.
        tokens = shell_words(without_heredocs(script).replace("\\\n", " "))
    except ValueError:
        return
    words = []
    uncertain = False
    subshells = []
    comment = False
    index = 0
    while index < len(tokens):
        token = tokens[index]
        index += 1
        if comment:
            comment = token != "\n"
            if comment:
                continue
        if token == "\n" and index > 1 and tokens[index - 2] in ("&&", "||", "|", "|&"):
            # A line that ends with && or || continues the list on the next one.
            continue
        if token in CONTROL:
            if words and not uncertain:
                yield words
            words = []
            if token == "||":
                uncertain = True
            elif token == "(":
                subshells.append(uncertain)
            elif token == ")":
                uncertain = subshells.pop() if subshells else False
            elif token in (";", ";;", "&", "\n"):
                uncertain = subshells[-1] if subshells else False
            continue
        if re.match(r"^(&>|[<>])", token):
            # A redirection, its target and the descriptor before it, as 2 in 2>&1, are not words of the command.
            if words and re.fullmatch(r"[0-9]+", words[-1]):
                words.pop()
            index += 1
            continue
        if token.startswith("#"):
            comment = True
            continue
        words.append(token)
    if words and not uncertain:
        yield words


def dotnet_new_calls(command):
    """The arguments of each dotnet new call of a shell command that ran, chained commands included."""
    for words in simple_commands(unwrap(command)):
        start = 0
        while start < len(words) and (words[start] in PREFIXES or re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", words[start])):
            start += 1
        if words[start:start + 2] == ["dotnet", "new"]:
            yield words[start + 2:]


# The fields of the initialization event of Claude Code that the verifier and the accounts of the runs need: the rest is the
# inventory of the CLI that ran (tools, commands, plugins, skills, agents, memory and socket paths).
CLAUDE_INIT_FIELDS = ("type", "subtype", "session_id", "model", "cwd", "claude_code_version", "permissionMode")
# The usage totals of Copilot: the rest of its checkpoint lists the tools of the CLI and the identifiers of each request.
COPILOT_USAGE_FIELDS = ("totalPremiumRequests", "totalNanoAiu")
# Encrypted reasoning, of Claude Code (thinking signatures), Copilot (opaque reasoning, signed blocks) or Codex.
ENCRYPTED_FIELDS = {"signature", "reasoningOpaque", "encrypted_content", "encryptedContent"}


def without_encrypted(value):
    if isinstance(value, dict):
        return {
            key: without_encrypted(item) for key, item in value.items()
            if key not in ENCRYPTED_FIELDS and not (isinstance(item, dict) and item.get("type") == "redacted_thinking")
        }
    if isinstance(value, list):
        return [
            without_encrypted(item) for item in value
            if not (isinstance(item, dict) and item.get("type") == "redacted_thinking")
        ]
    return value


def filtered_event(event):
    """The event as demos/ keeps it, or None to leave it out."""
    kind = event.get("type")
    if event.get("ephemeral") is True or kind == "rate_limit_event":
        return None
    if kind == "system" and event.get("subtype") == "init":
        return {key: event[key] for key in CLAUDE_INIT_FIELDS if key in event}
    if kind == "session.usage_checkpoint" and isinstance(event.get("data"), dict):
        return {**event, "data": {key: event["data"][key] for key in COPILOT_USAGE_FIELDS if key in event["data"]}}
    return without_encrypted(event)


def filter_trace(path, work=None, home=None):
    with open(path, encoding="utf-8") as trace:
        for number, line in enumerate(trace, 1):
            if not line.strip():
                continue
            if work:
                # Claude Code also names its project folder after the work directory, with - in place of /.
                line = line.replace(work, "/home/dev/work").replace(work.replace("/", "-"), "-home-dev-work")
            if home:
                line = line.replace(home, "/home/dev")
            try:
                event = json.loads(line)
            except json.JSONDecodeError as error:
                sys.exit(f"error: {path}:{number} is not JSON: {error}")
            kept = filtered_event(event) if isinstance(event, dict) else event
            if kept is None:
                continue
            if kept == event:
                sys.stdout.write(line if line.endswith("\n") else line + "\n")
            else:
                sys.stdout.write(json.dumps(kept, ensure_ascii=False, separators=(",", ":")) + "\n")


def option_value(arguments, option):
    for index, word in enumerate(arguments):
        if word == option and index + 1 < len(arguments):
            return arguments[index + 1]
        if word.startswith(option + "="):
            return word[len(option) + 1:]
    return None


def expect(path, expectations):
    all_commands = commands(path)
    missing = []
    for expectation in expectations:
        match = re.fullmatch(r"([a-z-]+):(--[a-z-]+)=(\S+)", expectation)
        if not match:
            sys.exit(f"error: expectation '{expectation}' is not <template>:<option>=<value>.")
        template, option, value = match.groups()
        found = [
            arguments for command in all_commands for arguments in dotnet_new_calls(command)
            if arguments and arguments[0] == template and option_value(arguments, option) == value
        ]
        if found:
            print(f"    dotnet new {shlex.join(found[0])}")
        else:
            missing.append(f"dotnet new {template} {option} {value}")
    if missing:
        sys.exit("error: the trace runs no " + ", no ".join(missing) + ".")


def main(argv):
    if len(argv) < 3 or argv[1] not in ("format", "commands", "skills", "expect", "filter"):
        sys.exit(__doc__)
    action, path = argv[1], argv[2]
    if action == "filter":
        if len(argv) not in (3, 5):
            sys.exit(__doc__)
        filter_trace(path, *argv[3:5])
    elif action == "format":
        print(trace_format(path))
    elif action == "commands":
        for command in commands(path):
            print(json.dumps(command))
    elif action == "skills":
        for skill in skills(path):
            print(skill)
    else:
        expect(path, argv[3:])


if __name__ == "__main__":
    main(sys.argv)
