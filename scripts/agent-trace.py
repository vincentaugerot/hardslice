#!/usr/bin/env python3
"""Reads the JSONL trace of an agent run: Claude Code (claude -p --output-format stream-json --verbose), Codex
(codex exec --json) or GitHub Copilot CLI (copilot -p --output-format json). Used by demo-add-feature.sh (PB-050).

  agent-trace.py format <trace>                 prints claude, codex or copilot.
  agent-trace.py commands <trace>               prints each shell command the agent ran with success, one JSON string per line.
  agent-trace.py skills <trace>                 prints each skill the agent loaded through its skill tool, one per line.
A tool call that the CLI refused or that failed is left out.
  agent-trace.py expect <trace> <expectation>...
      fails unless, for each expectation <template>:<option>=<value>, a shell command of the trace runs
      dotnet new <template> with <option> <value> (or <option>=<value>), and prints the matching commands.
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
            if item.get("type") == "command_execution" and item.get("status") == "completed" and item.get("exit_code") in (0, None):
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


def dotnet_new_calls(command):
    """The arguments of each dotnet new call of a shell command, chained commands included."""
    for segment in re.split(r"&&|\|\||[;|\n]", unwrap(command)):
        try:
            words = shlex.split(segment)
        except ValueError:
            words = segment.split()
        for index in range(len(words) - 1):
            if words[index] == "dotnet" and words[index + 1] == "new":
                arguments = words[index + 2:]
                # A redirection or a pipe of the shell ends the arguments of dotnet new.
                end = next((i for i, word in enumerate(arguments) if re.match(r"^[0-9]*[<>]", word)), len(arguments))
                yield arguments[:end]
                break


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
    if len(argv) < 3 or argv[1] not in ("format", "commands", "skills", "expect"):
        sys.exit(__doc__)
    action, path = argv[1], argv[2]
    if action == "format":
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
