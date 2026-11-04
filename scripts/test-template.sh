#!/usr/bin/env bash
# Packs the three HardSlice packages at the version of Directory.Build.props, installs the hardslice-api template, generates a solution in a temporary directory,
# verifies it with scripts/check.sh and check.ps1, integration tests included, checks that it holds every v0.1 deliverable and the instruction file of each agent and the skills they read,
# runs the "Run the API" path of docs/getting-started.md on it (PostgreSQL container, migrations, API, a booking created then read), checks its migrations with the pinned dotnet-ef,
# checks that commands go through its own mediator and pipeline, two concurrent creations under one unique key answered with a conflict, and that queries read without tracking, proves both scripts name the missing container runtime,
# then proves a seeded violation of HS000 (test projects included), HS001, HS003, HS004 (a reaction slice included), HS005, HS006, HS007, HS008, HS009, HS010 (a delay without TimeProvider included), HS011, HS012, HS013, HS014, HS015, HS016, HS017, HS018, HS901 and HS902 fails both, naming the rule and a rule doc that exists, that the HS019 suggestion leaves both passing and reaches the compiler log,
# that a missing or invalid hardslice.json, or one of a later version, fails them explicitly, with HS900 for its contracts and its version, that the solution compiles with the All analysis mode,
# that an unknown HardSliceAnalysisMode fails them with the list of modes, and that dotnet hardslice, which the scripts launch,
# is pinned with the analyzers to that version, both restored from the local feed only, refuses another one before the build and explains every rule with the sheet of the template.
# In both variants, dotnet user-secrets set passes without init on a UserSecretsId of its own, and the integration tests read no user
# secret and start a hosted service registered in Program.cs only in the test that keeps it.
# It adds CsvHelper to Directory.Packages.props, which the application owns, and a unit test that advances a FakeTimeProvider, and the check passes;
# without the import of the protected Directory.HardSlice.props both scripts fail before the build, and a reference removed in Directory.Packages.props
# or another Directory.Packages.props in a project folder fails them after the build, naming the project that no longer receives HardSlice.Analyzers.
# It generates a second module Rooms with dotnet new hardslice-module through each of its refusals, routes and printed lines, registers it in Program.cs,
# checks that the check passes on it and that HS012 fails both scripts when it reaches into a slice of Bookings.
# It generates command slices with dotnet new hardslice-command through each of its refusals, registers them with the lines it prints, checks that the test of the host names a forgotten registration,
# that only the TODO(hardslice): test fails, that the none and DTO responses build without HardSlice diagnostic, and that a reference completion passes the check.
# Its system commands, generated with --endpoint none, have no endpoint, fail HS004 while no type of their module sends them, and pass once a hosted service does.
# Its recurring commands, generated with --every through each of its refusals, are sent by AddRecurringCommand, and their tests run three cycles on a FakeTimeProvider.
# It generates query slices with dotnet new hardslice-query the same way, the three results, single, many and filtered (with the validator of
# its filter, through each refusal of --parameter), named without their verb, without HardSlice diagnostic, HS006 included, with the 400 of a
# filtered query without its filter, and proves that an unknown aggregate fails the build with a compiler error that names it.
# It generates slices with the route and the method of --route and --method, a PUT and a DELETE in a module created with its own
# --route and a query on a route of its own, through the refusals of a route or a method they cannot map, and their tests pass on these routes.
# It generates a command, a command without parameters and a query protected by --require, on a key scheme in Shared/Http, through the
# refusals of --require: their tests of the 401 pass, and those that send the client of the policy fail with the message of AppFactory
# until it holds that client, then pass after their reference completion.
# It generates an aggregate with dotnet new hardslice-aggregate through each of its refusals, proves it builds without HardSlice diagnostic,
# that the check fails on its TODO(hardslice): test, then on its missing migration, and passes once the printed migration is added.
# It generates two child entities of an aggregate with dotnet new hardslice-entity through each of its refusals, one held in a
# collection of the root, which creates the converter of the identifier of the root, and one that holds that identifier, which
# reuses it, proves they build without HardSlice diagnostic, that the check fails until the root holds the printed collection,
# then on their TODO(hardslice): tests, and passes once the printed migration creates both tables.
# It generates domain events with dotnet new hardslice-event through each of its refusals and its HS019 warning, with a reaction in
# the module of the event and one in a second module, registers them with the lines it prints, checks that the test of the host
# names a forgotten registration, that only the TODO(hardslice): tests fail without HardSlice diagnostic, and that a reference completion passes the check.
# With the sample, it runs the hooks of Claude Code with their payloads: a protected file refused, the errors of the build after the edit of a .cs file,
# the end of a turn blocked by a failing check and let through by a passing one, nothing under Copilot CLI; then the same with
# the hooks of Codex, which read the files of a patch, and of Copilot CLI, which answer in JSON.
# It proves that check --build, and the hook after an edit that runs it, restore once after an edit of a project that changes no package, then no more,
# restore a package added and build with it, and say that a project without obj/ has never been restored.
# Then, in a temporary git repository of the solution, the pre-commit hook of .githooks/, turned on as AGENTS.md says, refuses a commit
# that breaks HS010 and accepts a compliant one, and the job protected-files of .github/workflows/hardslice.yml fails on a change of
# scripts/check.sh until the label hardslice-approved is set. In both variants, actionlint validates that workflow.
# In both variants, it checks the modes of both scripts: any argument but --build exits with code 2, --build prints the errors of dotnet build alone
# and runs no test, and both modes fail on TODO(hardslice): markers left in the code and in a test, naming each file and line.
# Finally generates the template with --no-sample, proves the result holds no trace of the Bookings sample, passes both scripts and the getting started path,
# and that seeded violations of HS000, HS001, HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS013, HS014, HS015, HS016, HS017, HS018, HS901 and HS902 still fail them, and HS019 still reaches the compiler log: without the sample, the rules keep their targets.
# There, a first module generated by dotnet new hardslice-module the same way passes the check, then receives the same command slices,
# its first aggregate generated by dotnet new hardslice-aggregate and migrated, the query slices that read it, and its events with their reactions.
# Last, it generates Acme.Rooms with the sample and my-app without, proves that each takes one safe form of its name everywhere (my_app),
# passes the check, receives a module and a command slice from the item templates, and fails the check and the architecture test on HS016.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d "${TMPDIR:-/tmp}/hardslice-template.XXXXXX")"
postgres_container=""
api_pid=""

cleanup() {
  if [[ -n "$api_pid" ]]; then
    kill -TERM -- "-$api_pid" 2> /dev/null || true
    wait "$api_pid" 2> /dev/null || true
  fi
  if [[ -n "$postgres_container" ]]; then
    docker rm --force "$postgres_container" > /dev/null 2>&1 || true
  fi
  rm -rf "$work"
}
trap cleanup EXIT

app_name="Demo"
app="$work/$app_name"

# The check scripts restore dotnet hardslice and dotnet-ef from $work/nuget-packages: their resolver cache lives in $work too.
source "$repo_root/scripts/dotnet-cli-home.sh"
isolate_dotnet_cli_home "$work"
# register_module, register_slice and complete_command, shared with scripts/verify-published.sh.
source "$repo_root/scripts/item-completion.sh"

"$repo_root/scripts/generate-app.sh" "$work"
hardslice_version="$(cd "$repo_root" && dotnet msbuild src/HardSlice.Templates/HardSlice.Templates.csproj -getProperty:Version)"

if ! command -v pwsh > /dev/null; then
  echo "error: pwsh is required to verify scripts/check.ps1." >&2
  exit 1
fi

# The instruction file of each agent (docs/agents/support.md): .claude/CLAUDE.md and GEMINI.md import AGENTS.md by the mechanism of their CLI,
# .github/copilot-instructions.md does not, since Copilot reads AGENTS.md itself, and each names its CLI before anything else.
# No CLAUDE.md at the root: Copilot would expand its import and load AGENTS.md twice.
check_agent_instructions() {
  local root="$1" name="$2" file first import
  while IFS='|' read -r file first import; do
    if [[ ! -f "$root/$file" ]]; then
      echo "error: $name has no $file." >&2
      exit 1
    fi
    if [[ "$(grep -v -e '^#' -e '^$' "$root/$file" | head -n 1)" != "$first"* ]]; then
      echo "error: the $file of $name does not start with \"$first\"." >&2
      exit 1
    fi
    if [[ "$(grep '^@' "$root/$file" || true)" != "$import" ]]; then
      echo "error: the $file of $name does not import AGENTS.md as ${import:-nothing}, the mechanism of its CLI." >&2
      exit 1
    fi
  done <<'FILES'
.claude/CLAUDE.md|For Claude Code only.|@../AGENTS.md
GEMINI.md|For Gemini CLI only.|@./AGENTS.md
.github/copilot-instructions.md|For GitHub Copilot only.|
FILES
  if [[ -e "$root/CLAUDE.md" ]]; then
    echo "error: $name has a CLAUDE.md at its root, whose import Copilot expands: AGENTS.md would be loaded twice." >&2
    exit 1
  fi
  if ! grep -Fxq "## Agent kit" "$root/AGENTS.md"; then
    echo "error: the AGENTS.md of $name lists no agent kit." >&2
    exit 1
  fi
  # The skills (PB-043, PB-044): .claude/skills/ for Claude Code and Copilot, the copy that the pack writes in .agents/skills/ for Codex and
  # Gemini CLI, identical so that Copilot, which reads both, keeps one per name. Each names its folder and cites only paths of $name.
  local skill cited_path
  for skill in add-module add-aggregate add-slice add-domain-event extract-to-domain fix-architecture-violation; do
    if [[ ! -f "$root/.claude/skills/$skill/SKILL.md" || ! -f "$root/.agents/skills/$skill/SKILL.md" ]]; then
      echo "error: $name lacks the skill $skill in .claude/skills/ or .agents/skills/." >&2
      exit 1
    fi
    if ! cmp -s "$root/.claude/skills/$skill/SKILL.md" "$root/.agents/skills/$skill/SKILL.md"; then
      echo "error: the skill $skill of $name differs between .claude/skills/ and .agents/skills/." >&2
      exit 1
    fi
    if [[ "$(sed -n 2p "$root/.claude/skills/$skill/SKILL.md")" != "name: $skill" ]]; then
      echo "error: the skill $skill of $name is not named after its folder." >&2
      exit 1
    fi
    while read -r cited_path; do
      if [[ ! -e "$root/$cited_path" ]]; then
        echo "error: the skill $skill of $name cites $cited_path, which $name does not contain." >&2
        exit 1
      fi
    done < <(grep -o '`\(src\|tests\|docs\|scripts\)/[^`<>[:space:]]*`' "$root/.claude/skills/$skill/SKILL.md" | tr -d '`' | sort -u)
  done
  if [[ "$(find "$root/.claude/skills" "$root/.agents/skills" -type f | wc -l)" -ne 12 ]]; then
    echo "error: $name holds other files than the six skills in .claude/skills/ and .agents/skills/." >&2
    exit 1
  fi
}

# PB-056: the package keeps no executable bit, so the template makes the pre-commit hook and the check script executable itself.
check_executable_scripts() {
  local root="$1" name="$2" script
  for script in .githooks/pre-commit scripts/check.sh; do
    if [[ ! -x "$root/$script" ]]; then
      echo "error: dotnet new did not make $script of $name executable." >&2
      exit 1
    fi
  done
}

# One container for both variants, started as docs/getting-started.md documents it, except for its name and a free host port instead of 5432.
# Each variant migrates the database named by its own connection string.
start_documented_postgres() {
  [[ -z "$postgres_container" ]] || return 0
  echo "==> Starting PostgreSQL as docs/getting-started.md documents it, on a free port"
  postgres_container="$(docker run --detach --env POSTGRES_PASSWORD=postgres --publish 127.0.0.1::5432 postgres:18-alpine)"
  postgres_port="$(docker port "$postgres_container" 5432/tcp | head -n 1 | sed 's/.*://')"
  # The wait of docs/getting-started.md: TCP only, as the image's initialization server listens on its Unix socket, then restarts.
  if ! docker exec "$postgres_container" sh -c 'for i in $(seq 60); do pg_isready --quiet --host 127.0.0.1 --username postgres && exit 0; sleep 1; done; echo "PostgreSQL not ready after 60 s" >&2; exit 1'; then
    echo "error: the PostgreSQL container did not accept connections within 60 seconds." >&2
    exit 1
  fi
}

stop_api() {
  kill -TERM -- "-$api_pid" 2> /dev/null || true
  wait "$api_pid" 2> /dev/null || true
  api_pid=""
}

# Runs "Run the API" of docs/getting-started.md on a solution its check scripts have built: tool restore, migrations, dotnet run in Development,
# then, with the sample, the booking of docs/getting-started.md created, read at its Location, and refused when posted again.
# The connection string of the generated settings is kept, with the container's port; the API listens on a free port.
run_documented_path() {
  local app="$1"
  local app_name="$2"
  local with_sample="$3"
  local settings="$app/src/$app_name/appsettings.Development.json"
  local generated_connection_string
  local connection_string
  local api_log="$work/$app_name-api.log"
  local api_url=""
  local booking='{"roomId":"3f2b8c1e-0d4a-4b6e-9a7f-1c2d3e4f5a6b","start":"2026-12-01T09:00:00Z","end":"2026-12-01T10:00:00Z"}'
  local headers="$work/$app_name-booking.headers"
  local status
  local location
  local body

  start_documented_postgres

  generated_connection_string="$(sed -n 's/.*"Database": "\([^"]*\)".*/\1/p' "$settings")"
  if [[ "$generated_connection_string" != "Host=localhost;Port=5432;Database="*";Username=postgres;Password=postgres" ]]; then
    echo "error: $settings no longer matches the PostgreSQL container of docs/getting-started.md: $generated_connection_string" >&2
    exit 1
  fi
  connection_string="${generated_connection_string/Port=5432;/Port=$postgres_port;}"

  echo "==> Applying the migrations of $app_name to PostgreSQL as docs/getting-started.md documents it"
  (
    cd "$app"
    export ConnectionStrings__Database="$connection_string"
    dotnet tool restore
    dotnet ef database update --project "src/$app_name"
  )

  echo "==> Starting the API of $app_name as docs/getting-started.md documents it"
  # Job control gives the API its own process group: stopping it also stops the application dotnet run started.
  set -m
  (cd "$app" && ConnectionStrings__Database="$connection_string" exec dotnet run --project "src/$app_name" -- --environment Development --urls http://127.0.0.1:0) < /dev/null > "$api_log" 2>&1 &
  api_pid=$!
  set +m
  for _ in $(seq 180); do
    api_url="$(sed -n 's/.*Now listening on: \(http:[^[:space:]]*\).*/\1/p' "$api_log" | head -n 1)"
    [[ -z "$api_url" ]] || break
    if ! kill -0 "$api_pid" 2> /dev/null; then
      cat "$api_log" >&2
      echo "error: the API of $app_name stopped before listening." >&2
      exit 1
    fi
    sleep 1
  done
  if [[ -z "$api_url" ]]; then
    cat "$api_log" >&2
    echo "error: the API of $app_name did not listen within 180 seconds." >&2
    exit 1
  fi

  if [[ "$with_sample" == false ]]; then
    status="$(curl --silent --output /dev/null --write-out '%{http_code}' "$api_url/")"
    if [[ "$status" != 404 ]]; then
      echo "error: the API of $app_name, which has no endpoint, answered $status instead of 404." >&2
      exit 1
    fi
    stop_api
    echo "    $app_name started on its migrated database at $api_url"
    return 0
  fi

  echo "==> Confirming then reading the booking of docs/getting-started.md with $app_name"
  status="$(curl --silent --show-error --output /dev/null --dump-header "$headers" --write-out '%{http_code}' \
    -X POST "$api_url/bookings" -H "Content-Type: application/json" -d "$booking")"
  location="$(sed -n 's/^[Ll]ocation: *\([^[:space:]]*\).*/\1/p' "$headers" | tr -d '\r')"
  if [[ "$status" != 201 || ! "$location" =~ ^/bookings/[0-9a-f-]{36}$ ]]; then
    cat "$api_log" >&2
    echo "error: confirming the booking of docs/getting-started.md answered $status with Location '$location' instead of 201 and /bookings/<id>." >&2
    exit 1
  fi
  body="$(curl --silent --show-error --fail "$api_url$location")"
  if [[ "$body" != *'"id":"'"${location#/bookings/}"'"'* || "$body" != *'"roomId":"3f2b8c1e-0d4a-4b6e-9a7f-1c2d3e4f5a6b"'* || "$body" != *'"status":"Confirmed"'* ]]; then
    echo "error: reading $location did not return the confirmed booking: $body" >&2
    exit 1
  fi
  status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
    -X POST "$api_url/bookings" -H "Content-Type: application/json" -d "$booking")"
  if [[ "$status" != 409 ]]; then
    echo "error: posting the booking of docs/getting-started.md again answered $status instead of 409." >&2
    exit 1
  fi
  stop_api
  echo "    $body"
}

# The check scripts are run from another directory: they must locate the solution themselves.
# The documented 'bash scripts/check.sh' form works without the executable bit, which a clone may lose.
echo "==> Verifying the generated solution with scripts/check.sh"
bash "$app/scripts/check.sh"

echo "==> Verifying the generated solution with scripts/check.ps1"
pwsh -NoProfile -File "$app/scripts/check.ps1"

# The check scripts have restored the tool: it must be the version of the analyzers, and print the sheets of the template as it ships them.
# Both have the version of Directory.Build.props, which the pack wrote in the template, and come from the local feed, never from nuget.org:
# the packages restored into the private package folder are the very files generate-app.sh packed.
echo "==> Checking that dotnet hardslice is pinned to the version of the analyzers and explains every rule"
(
  cd "$app"
  analyzer_version="$(sed -n 's/.*<GlobalPackageReference Include="HardSlice.Analyzers" Version="\([^"]*\)".*/\1/p' Directory.HardSlice.props)"
  tool_version="$(dotnet hardslice --version)"
  if [[ -z "$analyzer_version" || "$tool_version" != "$analyzer_version" ]]; then
    echo "error: dotnet hardslice is version '$tool_version' but $app_name references HardSlice.Analyzers '$analyzer_version'." >&2
    exit 1
  fi
  if [[ "$analyzer_version" != "$hardslice_version" ]]; then
    echo "error: $app_name pins the HardSlice packages at '$analyzer_version' instead of $hardslice_version, the version of Directory.Build.props." >&2
    exit 1
  fi
  for package in HardSlice.Analyzers HardSlice.Tool; do
    lower="$(printf '%s' "$package" | tr '[:upper:]' '[:lower:]')"
    restored="$work/nuget-packages/$lower/$hardslice_version/$lower.$hardslice_version.nupkg"
    if ! cmp --silent "$work/feed/$package.$hardslice_version.nupkg" "$restored"; then
      echo "error: $app_name did not restore $package $hardslice_version from the local feed: $restored is not the package generate-app.sh packed." >&2
      exit 1
    fi
  done
  for sheet in "$repo_root"/src/HardSlice.Templates/content/hardslice-api/docs/rules/HS*.md; do
    rule="$(basename "$sheet" .md)"
    if ! dotnet hardslice explain "$rule" | cmp --silent - "$sheet"; then
      echo "error: dotnet hardslice explain $rule does not print the sheet of the template." >&2
      exit 1
    fi
  done
  explain_status=0
  explain_output="$(dotnet hardslice explain HS999 2>&1)" || explain_status=$?
  if [[ "$explain_status" != 2 || "$explain_output" != *"Rules with a sheet: HS000, HS001, "* ]]; then
    printf '%s\n' "$explain_output" >&2
    echo "error: dotnet hardslice explain HS999 exited $explain_status instead of 2 with the list of rules." >&2
    exit 1
  fi
  echo "    dotnet hardslice $tool_version, restored from the local feed with HardSlice.Analyzers $analyzer_version, explains $(ls docs/rules | wc -l) rules"
)

echo "==> Checking the generated migrations with the pinned dotnet-ef tool"
(
  cd "$app"
  dotnet tool restore
  dotnet ef migrations has-pending-model-changes --project "src/$app_name" --no-build
)

echo "==> Checking that $app_name holds every v0.1 deliverable of the specification"
for deliverable in \
  AGENTS.md .editorconfig hardslice.json scripts/check.sh scripts/check.ps1 docs/rules/HS000.md docs/rules/HS001.md docs/rules/HS003.md docs/rules/HS004.md docs/rules/HS005.md docs/rules/HS006.md docs/rules/HS007.md docs/rules/HS008.md docs/rules/HS009.md docs/rules/HS010.md docs/rules/HS011.md docs/rules/HS012.md \
  docs/rules/HS013.md docs/rules/HS014.md docs/rules/HS015.md docs/rules/HS016.md docs/rules/HS017.md docs/rules/HS018.md docs/rules/HS019.md \
  docs/rules/HS900.md docs/rules/HS901.md docs/rules/HS902.md "src/$app_name.Domain/IEntity.cs" "src/$app_name.Domain/IAggregateRoot.cs" \
  "src/$app_name.Domain/Bookings/Booking.cs" "src/$app_name.Domain/Bookings/BookingId.cs" \
  "src/$app_name.Domain/Bookings/BookingPeriod.cs" "src/$app_name.Domain/Bookings/Events/BookingConfirmed.cs" \
  "src/$app_name/Features/Bookings/ConfirmBooking/ConfirmBooking.cs" "src/$app_name/Features/Bookings/CancelBooking/CancelBooking.cs" \
  "src/$app_name/Features/Bookings/GetBooking/GetBooking.cs" "src/$app_name/Features/Bookings/BookingConfiguration.cs" \
  "src/$app_name/Features/Bookings/BookingsModule.cs" "src/$app_name/Shared/Messaging/IMediator.cs" \
  "src/$app_name/Shared/Persistence/AppDbContext.cs" "src/$app_name/Shared/Behaviors/TransactionBehavior.cs" \
  "tests/$app_name.Tests/Domain/Bookings/BookingTests.cs" "tests/$app_name.IntegrationTests/Features/Bookings/ConfirmBooking/ConfirmBookingTests.cs" \
  "tests/$app_name.ArchitectureTests/SliceTests.cs"; do
  if [[ ! -f "$app/$deliverable" ]]; then
    echo "error: $app_name has no $deliverable, a v0.1 deliverable of the specification." >&2
    exit 1
  fi
done
# The overlap invariant holds under concurrency only through the exclusion constraint of PostgreSQL.
if ! grep -rqF 'EXCLUDE USING gist ("RoomId" WITH =' "$app/src/$app_name/Shared/Persistence/Migrations"; then
  echo "error: the migrations of $app_name no longer create the constraint that keeps bookings of a room from overlapping." >&2
  exit 1
fi

echo "==> Checking the instruction file of each agent and the skills in $app_name"
check_agent_instructions "$app" "$app_name"
check_executable_scripts "$app" "$app_name"

echo "==> Validating the CI workflow of $app_name with actionlint"
"$repo_root/scripts/generated-workflow.sh" lint "$app"

run_documented_path "$app" "$app_name" true

# The check scripts above ran these tests: make sure the generated solution still contains them, and no mediation library.
echo "==> Checking that commands go through the generated solution's own mediator, validation and transaction, and queries read without tracking"
(
  cd "$app"
  if dotnet list "$app_name.slnx" package --include-transitive | grep -Eq '^[[:space:]]*>[[:space:]]*(MediatR|Mediator[.])'; then
    echo "error: the generated solution depends on a mediation library; its mediator is Shared/Messaging." >&2
    exit 1
  fi

  listed_tests="$(
    dotnet test --project "tests/$app_name.Tests/$app_name.Tests.csproj" --no-build --list-tests
    dotnet test --project "tests/$app_name.IntegrationTests/$app_name.IntegrationTests.csproj" --no-build --list-tests
  )"
  for test in \
    "Shared.Messaging.MediatorTests.Runs_the_behaviors_in_registration_order_around_the_handler" \
    "Shared.Messaging.MediatorTests.A_request_cannot_be_widened_to_an_unregistered_response_contract" \
    "Shared.Messaging.MediatorTests.Dispatches_each_declared_response_contract_in_either_order_across_scopes" \
    "Shared.Behaviors.BehaviorsServiceCollectionExtensionsTests.Commands_go_through_logging_then_validation_then_the_transaction" \
    "Shared.Behaviors.TransactionBehaviorTests.Rolls_back_everything_a_command_saved_when_it_throws" \
    "Shared.Behaviors.TransactionBehaviorTests.A_later_command_never_commits_a_failed_commands_additions" \
    "Shared.Behaviors.TransactionBehaviorTests.A_later_command_reloads_the_state_before_a_failed_commands_changes" \
    "Shared.Behaviors.TransactionBehaviorTests.Answers_a_conflict_to_the_second_of_two_concurrent_creations_under_the_same_key" \
    "Shared.Behaviors.TransactionBehaviorTests.Keeps_the_exception_of_a_handler_that_translates_a_unique_violation_itself" \
    "Shared.Behaviors.TransactionBehaviorTests.Answers_a_conflict_to_a_creation_that_EF_Core_saves_under_an_existing_key" \
    "Shared.Behaviors.DomainEventPublisherTests.Publishes_the_event_once_inside_the_transaction_before_anything_is_committed" \
    "Shared.Behaviors.DomainEventPublisherTests.A_failing_handler_saves_nothing_drops_the_pending_events_and_answers_with_its_exception" \
    "Shared.Behaviors.DomainEventPublisherTests.Calls_every_handler_of_the_event_in_registration_order" \
    "Shared.Behaviors.DomainEventPublisherTests.Fails_with_the_name_of_the_event_when_handlers_raise_events_beyond_the_maximum_depth" \
    "Shared.Behaviors.DomainEventPublisherTests.Fails_with_the_name_of_the_handler_when_it_sends_a_request_through_the_mediator" \
    "Shared.Messaging.MediatorTests.Refuses_a_request_sent_while_a_domain_event_handler_runs_and_names_the_handler" \
    "Features.Bookings.ConfirmBooking.ConfirmBookingTests.Rejects_a_command_without_room_with_a_validation_problem_before_its_handler" \
    "Features.Bookings.ConfirmBooking.ConfirmBookingTests.Stores_offset_dates_in_UTC_and_rejects_the_equivalent_UTC_booking" \
    "Features.Bookings.ConfirmBooking.ConfirmBookingTests.Rejects_sub_microsecond_periods_without_storing_or_blocking_the_room" \
    "Features.Bookings.ConfirmBooking.ConfirmBookingTests.Reads_and_cancels_a_period_at_its_persisted_precision" \
    "Features.Bookings.BookingConfigurationTests.Database_rejects_an_empty_period_even_when_bypassing_the_domain" \
    "Features.Bookings.BookingConfigurationTests.Upgrade_preserves_bookings_and_requires_explicit_repair_of_legacy_empty_periods" \
    "Features.Bookings.CancelBooking.CancelBookingTests.Rejects_an_empty_booking_id_with_a_validation_problem" \
    "Features.Bookings.GetBooking.GetBookingTests.Reads_a_confirmed_booking_at_the_location_returned_by_its_creation" \
    "Features.Bookings.GetBooking.GetBookingTests.Reads_a_booking_without_tracking_any_entity"; do
    if ! printf '%s\n' "$listed_tests" | grep -Fq ".$test"; then
      echo "error: the generated solution has no test $test." >&2
      exit 1
    fi
  done
)

# Isolated probe: DOCKER_HOST points to a socket that does not exist, only for these two runs.
echo "==> Checking that both check scripts fail and name the prerequisite without a container runtime"
container_prerequisite="integration tests need a Docker-compatible container runtime"
unreachable_runtime="unix://$work/no-container-runtime.sock"

if check_output="$(DOCKER_HOST="$unreachable_runtime" bash "$app/scripts/check.sh" 2>&1)"; then
  echo "error: scripts/check.sh passed without a reachable container runtime." >&2
  exit 1
fi
if ! printf '%s\n' "$check_output" | grep -Fq "$container_prerequisite"; then
  printf '%s\n' "$check_output" >&2
  echo "error: scripts/check.sh failed without naming the container runtime prerequisite." >&2
  exit 1
fi

if check_output="$(DOCKER_HOST="$unreachable_runtime" pwsh -NoProfile -File "$app/scripts/check.ps1" 2>&1)"; then
  echo "error: scripts/check.ps1 passed without a reachable container runtime." >&2
  exit 1
fi
if ! printf '%s\n' "$check_output" | grep -Fq "$container_prerequisite"; then
  printf '%s\n' "$check_output" >&2
  echo "error: scripts/check.ps1 failed without naming the container runtime prerequisite." >&2
  exit 1
fi


# dotnet test colors its report on a CI runner, even with its output captured: "failed <test>" there reads ESC[31mfailed ESC[m <test>.
# The probes that read the names of the tests in its report remove the escape sequences first.
strip_ansi() {
  sed $'s/\e\\[[0-9;]*[A-Za-z]//g'
}

run_check() {
  case "$1" in
    check.sh) bash "$app/scripts/check.sh" ;;
    check.ps1) pwsh -NoProfile -File "$app/scripts/check.ps1" ;;
  esac
}

# Runs the given check scripts on a seeded violation: each must fail with a diagnostic that starts with <marker>
# and announces <doc>, which must exist from the generated solution root.
expect_failure() {
  local marker="$1"
  local doc="$2"
  local description="$3"
  shift 3
  local script
  local check_output
  local diagnostic
  local skill

  if [[ ! -f "$app/$doc" ]]; then
    echo "error: $doc does not exist from the generated solution root." >&2
    exit 1
  fi
  for script in "$@"; do
    if check_output="$(run_check "$script" 2>&1)"; then
      echo "error: scripts/$script passed despite $description." >&2
      exit 1
    fi
    diagnostic="$(printf '%s\n' "$check_output" | grep -o "$marker.* Skill: [a-z-]*\. Doc: '[^']*'" | head -n 1 || true)"
    if [[ -z "$diagnostic" ]]; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script failed on $description without a \"$marker\" diagnostic announcing a skill and a Doc path." >&2
      exit 1
    fi
    if [[ "$diagnostic" != *" Doc: '$doc'" ]]; then
      echo "error: scripts/$script reports \"$diagnostic\", which does not announce $doc." >&2
      exit 1
    fi
    # PB-044: the skill that the message names ships with the generated solution.
    skill="$(printf '%s\n' "$diagnostic" | sed -n "s/.* Skill: \([a-z-]*\)\. Doc: .*/\1/p")"
    if [[ ! -f "$app/.claude/skills/$skill/SKILL.md" || ! -f "$app/.agents/skills/$skill/SKILL.md" ]]; then
      echo "error: scripts/$script reports \"$diagnostic\", whose skill $skill the generated solution does not contain." >&2
      exit 1
    fi
  done
  echo "    $diagnostic"
}

app_src="$app/src/$app_name"
domain_project="$app/src/$app_name.Domain/$app_name.Domain.csproj"

echo "==> Checking inherited domain dependencies used only by an assembly attribute"
cp "$app/Directory.Build.targets" "$app/Directory.Build.original.targets"
cat > "$app/Directory.Build.targets" <<EOF
<Project>
  <Import Project="Directory.Build.original.targets" />
  <ItemGroup Condition="'\$(MSBuildProjectName)' == '$app_name.Domain'">
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
EOF
cat > "$app/src/$app_name.Domain/Hs001AssemblyProbe.cs" <<EOF
[assembly: Microsoft.Extensions.Configuration.UserSecrets.UserSecretsId("hardslice-probe")]
EOF
expect_failure "HS001: assembly '$app_name.Domain' references 'Microsoft.Extensions.Configuration.UserSecrets'" "docs/rules/HS001.md" "an inherited dependency used by a domain assembly attribute" check.sh check.ps1
mv "$app/Directory.Build.original.targets" "$app/Directory.Build.targets"
rm "$app/src/$app_name.Domain/Hs001AssemblyProbe.cs"

echo "==> Checking that the HS016 analyzer fails both check scripts on a cross-slice reference"
cat > "$app_src/Features/Bookings/CancelBooking/Hs016Probe.cs" <<EOF
namespace $app_name.Features.Bookings.CancelBooking;

internal static class Hs016Probe
{
    internal static readonly Type Target = typeof($app_name.Features.Bookings.ConfirmBooking.ConfirmBookingHandler);
}
EOF
cat > "$app_src/Features/Bookings/ConfirmBooking/Hs016ProbeException.cs" <<EOF
namespace $app_name.Features.Bookings.ConfirmBooking;

internal sealed class Hs016ProbeException : Exception;
EOF
cat > "$app_src/Features/Bookings/CancelBooking/Hs016CatchProbe.cs" <<EOF
namespace $app_name.Features.Bookings.CancelBooking;

internal static class Hs016CatchProbe
{
    internal static void Run(Action action)
    {
        try { action(); }
        catch ($app_name.Features.Bookings.ConfirmBooking.Hs016ProbeException) { }
    }
}
EOF
expect_failure "error HS016: " "docs/rules/HS016.md" "a cross-slice reference" check.sh check.ps1

# The analyzer stops both check scripts at build time: only this probe builds without it, to reach the architecture test.
echo "==> Checking that the HS016 architecture test reports the same reference in the compiled assembly"
if check_output="$(
  cd "$app"
  dotnet build "$app_name.slnx" -p:RunAnalyzers=false 2>&1 &&
    dotnet test --project "tests/$app_name.ArchitectureTests/$app_name.ArchitectureTests.csproj" --no-build 2>&1
)"; then
  echo "error: the architecture tests passed despite a cross-slice reference in the compiled assembly." >&2
  exit 1
fi
if ! printf '%s\n' "$check_output" | grep -Fq "HS016: 'Hs016Probe' in slice 'Bookings/CancelBooking' uses 'ConfirmBookingHandler' from slice 'Bookings/ConfirmBooking'."; then
  printf '%s\n' "$check_output" >&2
  echo "error: the architecture tests failed without reporting the cross-slice reference as HS016." >&2
  exit 1
fi
if ! printf '%s\n' "$check_output" | grep -Fq "HS016: 'Hs016CatchProbe' in slice 'Bookings/CancelBooking' uses 'Hs016ProbeException'"; then
  printf '%s\n' "$check_output" >&2
  echo "error: the architecture tests missed the catch-only cross-slice reference." >&2
  exit 1
fi
rm "$app_src/Features/Bookings/CancelBooking/Hs016Probe.cs" \
  "$app_src/Features/Bookings/CancelBooking/Hs016CatchProbe.cs" \
  "$app_src/Features/Bookings/ConfirmBooking/Hs016ProbeException.cs"

# The probes of the architecture tests seed complete slices, so that HS004 lets the build reach the tests. An endpoint class
# that maps nothing completes a slice without a module to call it (HS015).
echo "==> Checking that HS016 fails both check scripts on a public handler"
mkdir -p "$app_src/Features/Bookings/Hs016Probe"
cat > "$app_src/Features/Bookings/Hs016Probe/Hs016Probe.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.Hs016Probe;

public sealed record Hs016ProbeQuery : IQuery<bool>;

public sealed class Hs016ProbeHandler : IRequestHandler<Hs016ProbeQuery, bool>
{
    public Task<bool> Handle(Hs016ProbeQuery query, CancellationToken cancellationToken) => Task.FromResult(true);
}

internal static class Hs016ProbeEndpoint;
EOF
expect_failure "HS016: handler 'Hs016ProbeHandler'" "docs/rules/HS016.md" "a public handler" check.sh check.ps1
rm -r "$app_src/Features/Bookings/Hs016Probe"

echo "==> Checking protected handlers exposed through public containers"
mkdir -p "$app_src/Features/Bookings/Hs016Protected"
cat > "$app_src/Features/Bookings/Hs016Protected/Hs016Protected.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.Hs016Protected;

public sealed record Hs016ProtectedQuery : IQuery<bool>;

public class Hs016ExtensionPoint
{
    protected sealed class Hs016ProtectedHandler : IRequestHandler<Hs016ProtectedQuery, bool>
    {
        public Task<bool> Handle(Hs016ProtectedQuery query, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}

internal static class Hs016ProtectedEndpoint;
EOF
expect_failure "HS016: handler 'Hs016ProtectedHandler'" "docs/rules/HS016.md" "a protected handler reachable from another assembly" check.sh check.ps1
rm -r "$app_src/Features/Bookings/Hs016Protected"

echo "==> Checking that HS001 fails both check scripts when the domain depends on dependency injection"
cp "$domain_project" "$work/domain-project.backup"
cat > "$domain_project" <<EOF
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

</Project>
EOF
cat > "$app/src/$app_name.Domain/Hs001Probe.cs" <<EOF
using Microsoft.Extensions.DependencyInjection;

namespace $app_name.Domain;

public static class Hs001Probe
{
    public static bool IsEmpty(IServiceCollection services) => services.Count == 0;
}
EOF
expect_failure "HS001: 'Hs001Probe' in the domain uses 'IServiceCollection'" "docs/rules/HS001.md" "a domain type using dependency injection" check.sh check.ps1
mv "$work/domain-project.backup" "$domain_project"
rm "$app/src/$app_name.Domain/Hs001Probe.cs"

echo "==> Checking catch-only dependencies from another module and Shared"
cat > "$app_src/Features/Bookings/GetBooking/HsCatchProbeException.cs" <<EOF
namespace $app_name.Features.Bookings.GetBooking;

internal sealed class HsCatchProbeException : Exception;
EOF
mkdir -p "$app_src/Features/Rooms"
cat > "$app_src/Features/Rooms/Hs012CatchProbe.cs" <<EOF
namespace $app_name.Features.Rooms;

internal static class Hs012CatchProbe
{
    internal static void Run(Action action)
    {
        try { action(); }
        catch ($app_name.Features.Bookings.GetBooking.HsCatchProbeException) { }
    }
}
EOF
expect_failure "HS012: 'Hs012CatchProbe' in module 'Rooms' uses 'HsCatchProbeException'" "docs/rules/HS012.md" "a module catching another module's slice exception" check.sh check.ps1
rm -r "$app_src/Features/Rooms"
cat > "$app_src/Shared/Behaviors/Hs017CatchProbe.cs" <<EOF
namespace $app_name.Shared.Behaviors;

internal static class Hs017CatchProbe
{
    internal static async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch ($app_name.Features.Bookings.GetBooking.HsCatchProbeException) { }
    }
}
EOF
expect_failure "HS017: 'Hs017CatchProbe' in 'Shared/Behaviors' uses 'HsCatchProbeException'" "docs/rules/HS017.md" "Shared catching a Features exception inside an async state machine" check.sh check.ps1
rm "$app_src/Shared/Behaviors/Hs017CatchProbe.cs" "$app_src/Features/Bookings/GetBooking/HsCatchProbeException.cs"

echo "==> Checking that HS017 fails both check scripts when Shared depends on Features"
cat > "$app_src/Shared/Behaviors/Hs017Probe.cs" <<EOF
using $app_name.Features.Bookings;

namespace $app_name.Shared.Behaviors;

internal static class Hs017Probe
{
    internal static IServiceCollection AddEverything(IServiceCollection services) => services.AddBookingsModule();
}
EOF
expect_failure "HS017: 'Hs017Probe' in 'Shared/Behaviors' uses 'BookingsModule'" "docs/rules/HS017.md" "Shared depending on Features" check.sh check.ps1
rm "$app_src/Shared/Behaviors/Hs017Probe.cs"

echo "==> Checking Shared dependencies on the Features root namespace"
cat > "$app_src/Features/Hs017RootFeature.cs" <<EOF
namespace $app_name.Features;

internal static class Hs017RootFeature
{
    internal static int Count => 1;
}
EOF
cat > "$app_src/Shared/Behaviors/Hs017RootProbe.cs" <<EOF
namespace $app_name.Shared.Behaviors;

internal static class Hs017RootProbe
{
    internal static int Count => $app_name.Features.Hs017RootFeature.Count;
}
EOF
expect_failure "HS017: 'Hs017RootProbe' in 'Shared/Behaviors' uses 'Hs017RootFeature' in 'Features'" "docs/rules/HS017.md" "Shared depending on the Features root" check.sh check.ps1
rm "$app_src/Features/Hs017RootFeature.cs" "$app_src/Shared/Behaviors/Hs017RootProbe.cs"

echo "==> Checking that HS013 fails both check scripts on a command with parameters but no validator"
mkdir -p "$app_src/Features/Bookings/Hs013Probe"
cat > "$app_src/Features/Bookings/Hs013Probe/Hs013Probe.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.Hs013Probe;

public sealed record Hs013ProbeCommand(Guid BookingId, string Reason) : ICommand<bool>;

internal sealed class Hs013ProbeHandler : IRequestHandler<Hs013ProbeCommand, bool>
{
    public Task<bool> Handle(Hs013ProbeCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
}

internal static class Hs013ProbeEndpoint;
EOF
expect_failure "HS013: command 'Hs013ProbeCommand' in slice 'Bookings/Hs013Probe' has parameters (BookingId, Reason) but no validator." "docs/rules/HS013.md" "a command with parameters but no validator" check.sh check.ps1
rm -r "$app_src/Features/Bookings/Hs013Probe"

# A specialized or constrained validator must not cover every construction of an open command.
# The slice and its handler have their tests: only HS013 sees the validator.
echo "==> Checking that HS013 preserves generic arguments and validator constraints"
# Writes the slice and its test: validator declaration, constructor body, optional command/handler constraint.
write_hs013_generic_slice() {
  mkdir -p "$app_src/Features/Bookings/Hs013Generic" "$app/tests/$app_name.Tests/Features/Bookings/Hs013Generic"
  cat > "$app/tests/$app_name.Tests/Features/Bookings/Hs013Generic/Hs013GenericHandlerTests.cs" <<EOF
namespace $app_name.Tests.Features.Bookings.Hs013Generic;

public sealed class Hs013GenericHandlerTests
{
    [Fact]
    public void Probe() => Assert.True(true);
}
EOF
  cat > "$app_src/Features/Bookings/Hs013Generic/Hs013Generic.cs" <<EOF
using FluentValidation;
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.Hs013Generic;

public sealed record Hs013GenericCommand<T>(T Value) : ICommand<bool> ${3:-};

internal sealed class Hs013GenericHandler<T> : IRequestHandler<Hs013GenericCommand<T>, bool> ${3:-}
{
    public Task<bool> Handle(Hs013GenericCommand<T> command, CancellationToken cancellationToken) => Task.FromResult(true);
}

$1
{
    public Hs013GenericValidator() => $2;
}

internal static class Hs013GenericEndpoint;
EOF
}
write_hs013_generic_slice "internal sealed class Hs013GenericValidator : AbstractValidator<Hs013GenericCommand<int>>" "RuleFor(command => command.Value).GreaterThan(0)"
expect_failure "HS013: command 'Hs013GenericCommand'" "docs/rules/HS013.md" "a generic command with only an int validator" check.sh check.ps1
write_hs013_generic_slice "internal sealed class Hs013GenericValidator<T> : AbstractValidator<Hs013GenericCommand<T>> where T : struct" "RuleFor(command => command.Value).NotEmpty()"
expect_failure "HS013: command 'Hs013GenericCommand'" "docs/rules/HS013.md" "a generic command with only a struct-constrained validator" check.sh check.ps1
write_hs013_generic_slice "internal sealed class Hs013GenericValidator<T> : AbstractValidator<Hs013GenericCommand<T>> where T : unmanaged" "RuleFor(command => command.Value).NotEmpty()" "where T : struct"
expect_failure "HS013: command 'Hs013GenericCommand'" "docs/rules/HS013.md" "a struct-constrained command with only an unmanaged validator" check.sh check.ps1
rm -r "$app_src/Features/Bookings/Hs013Generic" "$app/tests/$app_name.Tests/Features/Bookings/Hs013Generic"

# The slice has a test folder, so HS018 passes: only HS014 sees that it holds no test.
echo "==> Checking that HS014 fails both check scripts on a command handler without tests at the path of its slice"
mkdir -p "$app_src/Features/Bookings/ArchiveBooking" "$app/tests/$app_name.Tests/Features/Bookings/ArchiveBooking"
cat > "$app_src/Features/Bookings/ArchiveBooking/ArchiveBooking.cs" <<EOF
using FluentValidation;
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.ArchiveBooking;

public sealed record ArchiveBookingCommand(Guid BookingId) : ICommand<bool>;

internal sealed class ArchiveBookingValidator : AbstractValidator<ArchiveBookingCommand>
{
    public ArchiveBookingValidator() =>
        RuleFor(command => command.BookingId).NotEmpty();
}

internal sealed class ArchiveBookingHandler : IRequestHandler<ArchiveBookingCommand, bool>
{
    public Task<bool> Handle(ArchiveBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(false);
}

internal static class ArchiveBookingEndpoint;
EOF
cat > "$app/tests/$app_name.Tests/Features/Bookings/ArchiveBooking/ArchiveBookingFixture.cs" <<EOF
namespace $app_name.Tests.Features.Bookings.ArchiveBooking;

internal static class ArchiveBookingFixture
{
    internal static readonly Guid BookingId = Guid.NewGuid();
}
EOF
expect_failure "HS014: command handler 'ArchiveBookingHandler' in slice 'Bookings/ArchiveBooking' has no test .* Fix: add an integration test that sends 'ArchiveBookingCommand', an invalid one included, in '[^']*', and the unit tests of its validator in" "docs/rules/HS014.md" "a command handler without tests" check.sh check.ps1

# PB-063: a command without parameters has no validator (HS013) nor invalid request, and the fix of HS014 asks for neither.
echo "==> Checking that HS014 asks a command without parameters for no validator nor invalid request"
mkdir -p "$app_src/Features/Bookings/PurgeArchivedBookings" "$app/tests/$app_name.Tests/Features/Bookings/PurgeArchivedBookings"
cat > "$app_src/Features/Bookings/PurgeArchivedBookings/PurgeArchivedBookings.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.PurgeArchivedBookings;

public sealed record PurgeArchivedBookingsCommand : ICommand<int>;

internal sealed class PurgeArchivedBookingsHandler : IRequestHandler<PurgeArchivedBookingsCommand, int>
{
    public Task<int> Handle(PurgeArchivedBookingsCommand command, CancellationToken cancellationToken) => Task.FromResult(0);
}

internal static class PurgeArchivedBookingsEndpoint;
EOF
cat > "$app/tests/$app_name.Tests/Features/Bookings/PurgeArchivedBookings/PurgeArchivedBookingsFixture.cs" <<EOF
namespace $app_name.Tests.Features.Bookings.PurgeArchivedBookings;

internal static class PurgeArchivedBookingsFixture;
EOF
expect_failure "HS014: command handler 'PurgeArchivedBookingsHandler' in slice 'Bookings/PurgeArchivedBookings' has no test .* Fix: add an integration test that sends 'PurgeArchivedBookingsCommand' in '[^']*' and asserts its success, and a failure of the domain when one can happen. A command without parameters has no validator nor invalid request." "docs/rules/HS014.md" "a command handler without parameters nor tests" check.sh
rm -r "$app_src/Features/Bookings/PurgeArchivedBookings" "$app/tests/$app_name.Tests/Features/Bookings/PurgeArchivedBookings"

# Runs the architecture tests alone: the probe slice is registered nowhere, so its tests only need to exist.
expect_architecture_tests_pass() {
  local description="$1"
  local configuration="${2:-Debug}"
  local check_output

  if ! check_output="$(
    cd "$app"
    dotnet build "$app_name.slnx" --configuration "$configuration" 2>&1 &&
      dotnet test --project "tests/$app_name.ArchitectureTests/$app_name.ArchitectureTests.csproj" --configuration "$configuration" --no-build 2>&1
  )"; then
    printf '%s\n' "$check_output" >&2
    echo "error: the architecture tests failed with $description." >&2
    exit 1
  fi
}

echo "==> Checking that HS014 and HS018 accept the tests of a slice in either test project"
mkdir -p "$app/tests/$app_name.IntegrationTests/Features/Bookings/ArchiveBooking"
# Only this test project defines the extra symbol. Neither the unit nor architecture project does.
cat > "$app/tests/$app_name.IntegrationTests/Directory.Build.props" <<'EOF'
<Project>
  <Import Project="../../Directory.Build.props" />
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);HS014_PROJECT_PROBE</DefineConstants>
  </PropertyGroup>
</Project>
EOF
cat > "$app/tests/$app_name.IntegrationTests/Features/Bookings/ArchiveBooking/ArchiveBookingTests.cs" <<EOF
namespace $app_name.IntegrationTests.Features.Bookings.ArchiveBooking;

#if !NET10_0 || !HS014_PROJECT_PROBE
public sealed class ArchiveBookingTests
{
    [Fact]
    public void Probe() => Assert.True(true);
}
#endif
EOF
expect_failure "HS014: command handler 'ArchiveBookingHandler' in slice 'Bookings/ArchiveBooking' has no test" "docs/rules/HS014.md" "tests excluded by framework and project symbols" check.sh check.ps1
cat > "$app/tests/$app_name.IntegrationTests/Features/Bookings/ArchiveBooking/ArchiveBookingTests.cs" <<EOF
namespace $app_name.IntegrationTests.Features.Bookings.ArchiveBooking;

#if NET10_0 && HS014_PROJECT_PROBE && DEBUG
public sealed class ArchiveBookingTests
{
    [Fact]
    public void Probe() => Assert.True(true);
}
#endif
EOF
expect_architecture_tests_pass "a command handler tested under framework, project and Debug symbols in $app_name.IntegrationTests"
# The same source is inactive in Release: architecture tests must use the current configuration.
if check_output="$(
  cd "$app"
  dotnet build "$app_name.slnx" --configuration Release 2>&1 &&
    dotnet test --project "tests/$app_name.ArchitectureTests/$app_name.ArchitectureTests.csproj" --configuration Release --no-build 2>&1
)"; then
  echo "error: HS014 accepted Debug-only tests in Release." >&2
  exit 1
fi
if ! printf '%s\n' "$check_output" | grep -Fq "HS014: command handler 'ArchiveBookingHandler'"; then
  printf '%s\n' "$check_output" >&2
  echo "error: the Release probe failed without the expected HS014 diagnostic." >&2
  exit 1
fi
cat > "$app/tests/$app_name.IntegrationTests/Features/Bookings/ArchiveBooking/ArchiveBookingTests.cs" <<EOF
namespace $app_name.IntegrationTests.Features.Bookings.ArchiveBooking;

#if NET10_0 && HS014_PROJECT_PROBE && !DEBUG
public sealed class ArchiveBookingTests
{
    [Fact]
    public void Probe() => Assert.True(true);
}
#endif
EOF
expect_architecture_tests_pass "a command handler with Release-only tests in $app_name.IntegrationTests" Release
rm "$app/tests/$app_name.IntegrationTests/Directory.Build.props"

echo "==> Checking that HS014 accepts attribute aliases and rejects integration tests excluded by Compile Remove"
cat > "$app/tests/$app_name.IntegrationTests/Features/Bookings/ArchiveBooking/ArchiveBookingTests.cs" <<EOF
using Test = global::Xunit.FactAttribute;

namespace $app_name.IntegrationTests.Features.Bookings.ArchiveBooking;

public sealed class ArchiveBookingTests
{
    [Test]
    public void Probe() => Assert.True(true);
}
EOF
expect_architecture_tests_pass "a command handler tested with an attribute alias in $app_name.IntegrationTests"
cat > "$app/tests/$app_name.IntegrationTests/Directory.Build.targets" <<'EOF'
<Project>
  <Import Project="../../Directory.Build.targets" />
  <ItemGroup>
    <Compile Remove="Features/Bookings/ArchiveBooking/**/*.cs" />
  </ItemGroup>
</Project>
EOF
expect_failure "HS014: command handler 'ArchiveBookingHandler' in slice 'Bookings/ArchiveBooking' has no test" "docs/rules/HS014.md" "integration tests excluded by Compile Remove" check.sh check.ps1
rm "$app/tests/$app_name.IntegrationTests/Directory.Build.targets"
rm -r "$app/tests/$app_name.IntegrationTests/Features/Bookings/ArchiveBooking" "$app/tests/$app_name.Tests/Features/Bookings/ArchiveBooking"
mkdir -p "$app/tests/$app_name.Tests/Features/Bookings/ArchiveBooking"
cat > "$app/tests/$app_name.Tests/ProbeAliases.cs" <<'EOF'
global using SliceTestAttribute = Xunit.FactAttribute;
EOF
cat > "$app/tests/$app_name.Tests/Features/Bookings/ArchiveBooking/ArchiveBookingValidatorTests.cs" <<EOF
namespace $app_name.Tests.Features.Bookings.ArchiveBooking;

public sealed class ArchiveBookingValidatorTests
{
    [SliceTest]
    public void Probe() => Assert.True(true);
}
EOF
expect_architecture_tests_pass "a command handler tested with a global attribute alias in $app_name.Tests only"
echo "==> Checking that HS014 rejects unit tests excluded by Compile Remove"
cat > "$app/tests/$app_name.Tests/Directory.Build.targets" <<'EOF'
<Project>
  <Import Project="../../Directory.Build.targets" />
  <ItemGroup>
    <Compile Remove="Features/Bookings/ArchiveBooking/**/*.cs" />
  </ItemGroup>
</Project>
EOF
expect_failure "HS014: command handler 'ArchiveBookingHandler' in slice 'Bookings/ArchiveBooking' has no test" "docs/rules/HS014.md" "unit tests excluded by Compile Remove" check.sh check.ps1
rm "$app/tests/$app_name.Tests/Directory.Build.targets" "$app/tests/$app_name.Tests/ProbeAliases.cs"
cat > "$app/tests/$app_name.Tests/Directory.Build.props" <<'EOF'
<Project>
  <Import Project="../../Directory.Build.props" />
  <ItemGroup>
    <Using Include="Xunit.FactAttribute" Alias="SliceTest" />
  </ItemGroup>
</Project>
EOF
expect_architecture_tests_pass "a command handler tested with an MSBuild-generated global alias"
rm "$app/tests/$app_name.Tests/Directory.Build.props"
rm -r "$app_src/Features/Bookings/ArchiveBooking" "$app/tests/$app_name.Tests/Features/Bookings/ArchiveBooking"

echo "==> Checking that HS013 accepts a generic validator covering all command arguments"
write_hs013_generic_slice "internal sealed class Hs013GenericValidator<T> : AbstractValidator<Hs013GenericCommand<T>>" "RuleFor(command => command.Value).NotEmpty()"
expect_architecture_tests_pass "an unconstrained generic command with an unconstrained validator"
write_hs013_generic_slice "internal sealed class Hs013GenericValidator<T> : AbstractValidator<Hs013GenericCommand<T>> where T : unmanaged" "RuleFor(command => command.Value).NotEmpty()" "where T : unmanaged"
expect_architecture_tests_pass "an unmanaged command with an unmanaged validator"
rm -r "$app_src/Features/Bookings/Hs013Generic" "$app/tests/$app_name.Tests/Features/Bookings/Hs013Generic"

echo "==> Checking that HS018 fails both check scripts on a slice without tests"
mkdir -p "$app_src/Features/Bookings/ExportBookings"
cat > "$app_src/Features/Bookings/ExportBookings/ExportBookings.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.ExportBookings;

public sealed record ExportBookingsQuery : IQuery<string>;

internal sealed class ExportBookingsHandler : IRequestHandler<ExportBookingsQuery, string>
{
    public Task<string> Handle(ExportBookingsQuery query, CancellationToken cancellationToken) => Task.FromResult("text/csv");
}

internal static class ExportBookingsEndpoint;
EOF
expect_failure "HS018: slice 'Bookings/ExportBookings' has no tests" "docs/rules/HS018.md" "a slice without tests" check.sh check.ps1
rm -r "$app_src/Features/Bookings/ExportBookings"

echo "==> Checking that HS018 fails both check scripts on a test folder without its slice"
mkdir -p "$app/tests/$app_name.Tests/Features/Bookings/ConfirmBookng"
cat > "$app/tests/$app_name.Tests/Features/Bookings/ConfirmBookng/ConfirmBookngTests.cs" <<EOF
namespace $app_name.Tests.Features.Bookings.ConfirmBookng;

public sealed class ConfirmBookngTests
{
    [Fact]
    public void Probe() => Assert.True(true);
}
EOF
expect_failure "HS018: test folder 'tests/$app_name.Tests/Features/Bookings/ConfirmBookng/' has no slice" "docs/rules/HS018.md" "a test folder without its slice" check.sh check.ps1
rm -r "$app/tests/$app_name.Tests/Features/Bookings/ConfirmBookng"

# Seeds a domain type of module <module> without contract, mapped by its module: the analyzer of the web project reports HS902.
probe_unclassified_entity() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module" "$app_src/Features/$module"
  cat > "$app/src/$app_name.Domain/$module/Room.cs" <<EOF
namespace $app_name.Domain.$module;

public sealed class Room
{
    public Guid Id { get; private set; }
}
EOF
  cat > "$app_src/Features/$module/RoomConfiguration.cs" <<EOF
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using $app_name.Domain.$module;

namespace $app_name.Features.$module;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder) => builder.HasKey(room => room.Id);
}
EOF
  # PB-075: the fix also offers to remove the mapping of a type that became a value object.
  expect_failure "HS902: 'Room' is mapped as an EF Core entity by 'RoomConfiguration' but implements neither 'IEntity' nor 'IAggregateRoot'\. .* or, if 'Room' is a value object, remove 'RoomConfiguration' or map it with ComplexProperty in its entity\." "docs/rules/HS902.md" "a domain type of $app_name mapped without contract" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/Room.cs" "$app_src/Features/$module/RoomConfiguration.cs"
}

# Seeds a domain type of module <module> that is both an entity and a domain event: the analyzer of the domain reports HS901.
probe_ambiguous_type() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomSnapshot.cs" <<EOF
namespace $app_name.Domain.$module;

public sealed class RoomSnapshot : IAggregateRoot, IDomainEvent
{
    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [];
}
EOF
  expect_failure "HS901: 'RoomSnapshot' is both an entity and a domain event: it implements 'IAggregateRoot' and 'IDomainEvent'." "docs/rules/HS901.md" "a domain type of $app_name that is both an entity and an event" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/RoomSnapshot.cs"
}

# Seeds a slice of module <module> whose endpoint receives AppDbContext: the analyzer of the web project reports HS003.
probe_endpoint_database_access() {
  local module="$1"
  mkdir -p "$app_src/Features/$module/ListProbes"
  cat > "$app_src/Features/$module/ListProbes/ListProbes.cs" <<EOF
using $app_name.Shared.Persistence;

namespace $app_name.Features.$module.ListProbes;

internal static class ListProbesEndpoint
{
    public static void Map(RouteGroupBuilder group) => group.MapGet("/", (AppDbContext db) => db.Database.ProviderName);
}
EOF
  expect_failure "HS003: endpoint 'GET /' in slice '$module/ListProbes' uses 'AppDbContext' (parameter 'db')." "docs/rules/HS003.md" "an endpoint of $app_name that receives AppDbContext" check.sh check.ps1
  rm -r "$app_src/Features/$module/ListProbes"
}

# Seeds a slice of module <module> whose endpoint, on a group and with TypedResults, is mapped by a method that the module
# never calls: the analyzer of the web project reports HS015 at the end of its compilation.
probe_unmapped_endpoint() {
  local module="$1"
  mkdir -p "$app_src/Features/$module/ArchiveProbes"
  cat > "$app_src/Features/$module/ArchiveProbes/ArchiveProbes.cs" <<EOF
namespace $app_name.Features.$module.ArchiveProbes;

internal static class ArchiveProbesEndpoint
{
    public static void Map(RouteGroupBuilder group) => group.MapPost("/archive", () => TypedResults.NoContent());
}
EOF
  expect_failure "HS015: endpoint 'POST /archive' in slice '$module/ArchiveProbes' is mapped by 'ArchiveProbesEndpoint.Map', which '${module}Module' never calls." "docs/rules/HS015.md" "an endpoint of $app_name that ${module}Module never maps" check.sh check.ps1
  rm -r "$app_src/Features/$module/ArchiveProbes"
}

# Seeds a slice of module <module> whose command is not named after it: the analyzer of the web project reports HS004.
probe_slice_structure() {
  local module="$1"
  mkdir -p "$app_src/Features/$module/PurgeProbes"
  cat > "$app_src/Features/$module/PurgeProbes/PurgeProbes.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.$module.PurgeProbes;

public sealed record PurgeCommand : ICommand<int>;

internal sealed class PurgeProbesHandler : IRequestHandler<PurgeCommand, int>
{
    public Task<int> Handle(PurgeCommand command, CancellationToken cancellationToken) => Task.FromResult(0);
}

internal static class PurgeProbesEndpoint;
EOF
  expect_failure "HS004: slice '$module/PurgeProbes' declares 'PurgeCommand'." "docs/rules/HS004.md" "a slice of $app_name whose command is not named after it" check.sh check.ps1
  rm -r "$app_src/Features/$module/PurgeProbes"
}

# Seeds a domain event of module <module> and a reaction slice that handles it, with its test: HS004, HS014 and HS018 accept it.
# Then a misnamed handler fails HS004, a public one HS016, a test folder without test HS014 and no test folder HS018.
write_reaction_handler() {
  local module="$1"
  local visibility="$2"
  local name="$3"
  cat > "$app_src/Features/$module/NotifyOnReactionProbed/NotifyOnReactionProbed.cs" <<EOF
using $app_name.Domain.$module.Events;
using $app_name.Shared.Messaging;

namespace $app_name.Features.$module.NotifyOnReactionProbed;

$visibility sealed class $name : IDomainEventHandler<ReactionProbed>
{
    public Task Handle(ReactionProbed domainEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
EOF
}

probe_reaction_slice() {
  local module="$1"
  local events="$app/src/$app_name.Domain/$module/Events"
  local tests="$app/tests/$app_name.IntegrationTests/Features/$module/NotifyOnReactionProbed"
  local events_existed=false
  [[ -d "$events" ]] && events_existed=true
  mkdir -p "$events" "$app_src/Features/$module/NotifyOnReactionProbed" "$tests"
  cat > "$events/ReactionProbed.cs" <<EOF
namespace $app_name.Domain.$module.Events;

public sealed record ReactionProbed(Guid Id) : IDomainEvent;
EOF
  write_reaction_handler "$module" internal NotifyOnReactionProbedHandler
  cat > "$tests/NotifyOnReactionProbedTests.cs" <<EOF
namespace $app_name.IntegrationTests.Features.$module.NotifyOnReactionProbed;

public sealed class NotifyOnReactionProbedTests
{
    [Fact]
    public void Probe() => Assert.True(true);
}
EOF
  expect_architecture_tests_pass "a reaction slice of $module with its handler and its test"

  write_reaction_handler "$module" internal NotifyHandler
  expect_failure "HS004: slice '$module/NotifyOnReactionProbed' declares 'NotifyHandler'." "docs/rules/HS004.md" "a domain event handler of $app_name not named after its reaction slice" check.sh check.ps1
  write_reaction_handler "$module" public NotifyOnReactionProbedHandler
  expect_failure "HS016: domain event handler 'NotifyOnReactionProbedHandler'" "docs/rules/HS016.md" "a public domain event handler of $app_name" check.sh check.ps1
  write_reaction_handler "$module" internal NotifyOnReactionProbedHandler

  # The folder keeps a file, so HS018 passes: only HS014 sees that it holds no test.
  cat > "$tests/NotifyOnReactionProbedTests.cs" <<EOF
namespace $app_name.IntegrationTests.Features.$module.NotifyOnReactionProbed;

internal static class NotifyOnReactionProbedFixture
{
    internal static readonly Guid Id = Guid.NewGuid();
}
EOF
  expect_failure "HS014: domain event handler 'NotifyOnReactionProbedHandler' in slice '$module/NotifyOnReactionProbed' has no test" "docs/rules/HS014.md" "a domain event handler of $app_name without tests" check.sh check.ps1
  rm -r "$tests"
  expect_failure "HS018: slice '$module/NotifyOnReactionProbed' has no tests" "docs/rules/HS018.md" "a reaction slice of $app_name without tests" check.sh check.ps1

  rm -r "$app_src/Features/$module/NotifyOnReactionProbed"
  rm "$events/ReactionProbed.cs"
  if [[ "$events_existed" == false ]]; then
    rmdir "$events"
  fi
}

# Seeds an aggregate of module <module> and a complete slice whose command handler returns a DTO holding a list of it: the
# analyzer of the web project reports HS005, reading the aggregate from the domain it references.
probe_command_response() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module" "$app_src/Features/$module/HoldProbes"
  cat > "$app/src/$app_name.Domain/$module/RoomHold.cs" <<EOF
namespace $app_name.Domain.$module;

public sealed class RoomHold : IAggregateRoot
{
    public Guid Id { get; } = Guid.NewGuid();

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [];
}
EOF
  cat > "$app_src/Features/$module/HoldProbes/HoldProbes.cs" <<EOF
using $app_name.Domain.$module;
using $app_name.Shared.Messaging;

namespace $app_name.Features.$module.HoldProbes;

public sealed record HoldProbesCommand : ICommand<HoldProbesDto>;

public sealed record HoldProbesDto(IReadOnlyList<RoomHold> Holds);

internal sealed class HoldProbesHandler : IRequestHandler<HoldProbesCommand, HoldProbesDto>
{
    public Task<HoldProbesDto> Handle(HoldProbesCommand command, CancellationToken cancellationToken) => Task.FromResult(new HoldProbesDto([]));
}

internal static class HoldProbesEndpoint;
EOF
  expect_failure "HS005: handler 'HoldProbesHandler' of command 'HoldProbesCommand' returns 'HoldProbesDto', whose property 'Holds' holds the entity 'RoomHold'." "docs/rules/HS005.md" "a command handler of $app_name that returns its aggregate in a DTO" check.sh check.ps1
  rm -r "$app_src/Features/$module/HoldProbes"
  rm "$app/src/$app_name.Domain/$module/RoomHold.cs"
}

# Seeds an aggregate of module <module> and a complete slice whose query handler loads it without AsNoTracking: the
# analyzer of the web project reports HS006, reading the aggregate from the domain it references and the context of Shared.
probe_query_tracking() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module" "$app_src/Features/$module/VisitProbes"
  cat > "$app/src/$app_name.Domain/$module/RoomVisit.cs" <<EOF
namespace $app_name.Domain.$module;

public sealed class RoomVisit : IAggregateRoot
{
    public Guid Id { get; } = Guid.NewGuid();

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [];
}
EOF
  cat > "$app_src/Features/$module/VisitProbes/VisitProbes.cs" <<EOF
using Microsoft.EntityFrameworkCore;
using $app_name.Domain.$module;
using $app_name.Shared.Messaging;
using $app_name.Shared.Persistence;

namespace $app_name.Features.$module.VisitProbes;

public sealed record VisitProbesQuery : IQuery<IReadOnlyList<Guid>>;

internal sealed class VisitProbesHandler(AppDbContext db) : IRequestHandler<VisitProbesQuery, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> Handle(VisitProbesQuery query, CancellationToken cancellationToken) =>
        (await db.Set<RoomVisit>().ToListAsync(cancellationToken)).Select(visit => visit.Id).ToList();
}

internal static class VisitProbesEndpoint;
EOF
  expect_failure "HS006: query handler 'VisitProbesHandler' reads 'RoomVisit' from 'Set<RoomVisit>()' without a local guarantee that it is not tracked." "docs/rules/HS006.md" "a query handler of $app_name that loads its aggregate without AsNoTracking" check.sh check.ps1
  rm -r "$app_src/Features/$module/VisitProbes"
  rm "$app/src/$app_name.Domain/$module/RoomVisit.cs"
}

# Seeds an aggregate of module <module> whose status any code can set: the analyzer of the domain reports HS007 in its own compilation.
probe_entity_state() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomInspection.cs" <<EOF
namespace $app_name.Domain.$module;

public sealed class RoomInspection : IAggregateRoot
{
    public Guid Id { get; } = Guid.NewGuid();

    public bool Passed { get; set; }

    public void Pass() => Passed = true;

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [];
}
EOF
  expect_failure "HS007: entity 'RoomInspection' exposes 'Passed' with a public setter." "docs/rules/HS007.md" "an aggregate of $app_name whose state has a public setter" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/RoomInspection.cs"
}

# Seeds an aggregate of module <module> whose child entity holds another aggregate instead of its identifier: the analyzer
# of the domain reports HS008 in its own compilation, the child belonging to the root that reaches it.
probe_aggregate_reference() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomTour.cs" <<EOF
namespace $app_name.Domain.$module;

public sealed class RoomKey : IAggregateRoot
{
    public Guid Id { get; } = Guid.NewGuid();

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [];
}

public sealed class RoomTour : IAggregateRoot
{
    private readonly List<TourStop> _stops = [];

    public Guid Id { get; } = Guid.NewGuid();

    public IReadOnlyList<TourStop> Stops => _stops;

    public IReadOnlyList<IDomainEvent> DequeueDomainEvents() => [];
}

public sealed class TourStop : IEntity
{
    public RoomKey Key { get; private set; } = new();
}
EOF
  expect_failure "HS008: entity 'TourStop' of aggregate 'RoomTour' references aggregate 'RoomKey' through property 'Key'." "docs/rules/HS008.md" "a child entity of $app_name that holds another aggregate" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/RoomTour.cs"
}

# Seeds a domain event of module <module> declared as a class: the analyzer of the domain reports HS009 in its own compilation.
probe_domain_event_record() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module/Events"
  cat > "$app/src/$app_name.Domain/$module/Events/RoomInspected.cs" <<EOF
namespace $app_name.Domain.$module.Events;

public sealed class RoomInspected : IDomainEvent
{
    public Guid RoomId { get; } = Guid.NewGuid();
}
EOF
  expect_failure "HS009: domain event 'RoomInspected' is a class. Domain events are records." "docs/rules/HS009.md" "a domain event of $app_name declared as a class" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/Events/RoomInspected.cs"
}

# Seeds a domain event of module <module> named as a command. HS019 is a suggestion of the All mode: with the shipped
# configuration, both check scripts still pass, and the suggestion is in the SARIF log of the compiler of the domain.
probe_domain_event_name() {
  local module="$1"
  local script
  local check_output
  local build_output
  local sarif="$work/hs019.sarif"
  mkdir -p "$app/src/$app_name.Domain/$module/Events"
  cat > "$app/src/$app_name.Domain/$module/Events/InspectRoom.cs" <<EOF
namespace $app_name.Domain.$module.Events;

public sealed record InspectRoom(Guid RoomId) : IDomainEvent;
EOF
  if [[ ! -f "$app/docs/rules/HS019.md" ]]; then
    echo "error: docs/rules/HS019.md does not exist from the generated solution root." >&2
    exit 1
  fi
  for script in check.sh check.ps1; do
    if ! check_output="$(run_check "$script" 2>&1)"; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script failed on a domain event of $app_name not named as a past fact: HS019 never blocks." >&2
      exit 1
    fi
  done
  rm -f "$sarif"
  if ! build_output="$(dotnet build "$app/src/$app_name.Domain/$app_name.Domain.csproj" --no-restore --no-incremental "-p:ErrorLog=$sarif" 2>&1)"; then
    printf '%s\n' "$build_output" >&2
    echo "error: the domain of $app_name does not build with a domain event not named as a past fact." >&2
    exit 1
  fi
  if ! grep -Fq '"ruleId": "HS019"' "$sarif" ||
    ! grep -Fq "domain event 'InspectRoom' is not named as a past fact. Domain events are named in the past tense." "$sarif" ||
    ! grep -Fq "Skill: add-domain-event. Doc: 'docs/rules/HS019.md'." "$sarif"; then
    cat "$sarif" >&2 || true
    echo "error: the compiler log of $app_name.Domain does not hold the HS019 suggestion on InspectRoom." >&2
    exit 1
  fi
  echo "    HS019: domain event 'InspectRoom' is not named as a past fact, a suggestion of the compiler log; both scripts pass"
  rm "$app/src/$app_name.Domain/$module/Events/InspectRoom.cs" "$sarif"
}

# Seeds suppressions of HardSlice rules without justification in a slice of module <module>, in its domain and in a unit test
# class: HS000 fails both check scripts on each, test projects included. A justified suppression builds without HardSlice
# diagnostic and still turns its rule off; a suppression of HS000 itself fails even justified.
probe_suppression_justification() {
  local module="$1"
  local build_output
  mkdir -p "$app_src/Features/$module/ExpireProbes"
  cat > "$app_src/Features/$module/ExpireProbes/ExpireProbes.cs" <<EOF
#pragma warning disable HS010
namespace $app_name.Features.$module.ExpireProbes;

internal sealed class ExpireProbesHandler
{
    public bool Handle(DateTimeOffset end) => end < DateTimeOffset.UtcNow;
}
EOF
  expect_failure "HS000: '#pragma warning disable HS010' in 'ExpireProbes.cs' has no justification." "docs/rules/HS000.md" "a slice of $app_name that suppresses HS010 without justification" check.sh check.ps1
  rm -r "$app_src/Features/$module/ExpireProbes"

  mkdir -p "$app/tests/$app_name.Tests/Probes"
  cat > "$app/tests/$app_name.Tests/Probes/SuppressionProbeTests.cs" <<EOF
namespace $app_name.Tests.Probes;

[System.Diagnostics.CodeAnalysis.SuppressMessage("HardSlice.Architecture", "HS016", Justification = "A probe.")]
public sealed class SuppressionProbeTests;
EOF
  expect_failure "HS000: '\\[SuppressMessage(\"HardSlice.Architecture\", \"HS016\")]' in 'SuppressionProbeTests.cs' has no justification." "docs/rules/HS000.md" "a test class of $app_name that suppresses HS016 without justification" check.sh check.ps1
  rm -r "$app/tests/$app_name.Tests/Probes"

  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomOpening.cs" <<EOF
// hardslice-justification: probe of scripts/test-template.sh, approved by the maintainer
#pragma warning disable HS010
namespace $app_name.Domain.$module;

public static class RoomOpening
{
    public static bool IsOpenToday() => DateTime.Today.DayOfWeek != DayOfWeek.Sunday;
}
EOF
  if ! build_output="$(dotnet build "$app/src/$app_name.Domain/$app_name.Domain.csproj" --no-restore 2>&1)" || grep -Eq '(error|warning) HS[0-9]{3}' <<< "$build_output"; then
    printf '%s\n' "$build_output" >&2
    echo "error: the domain of $app_name does not build without HardSlice diagnostic with a justified suppression of HS010." >&2
    exit 1
  fi
  echo "    a justified '#pragma warning disable HS010' in the domain builds without HardSlice diagnostic"

  sed -i.bak 's|^#pragma warning disable HS010$|#pragma warning disable HS000, HS010|' "$app/src/$app_name.Domain/$module/RoomOpening.cs"
  rm "$app/src/$app_name.Domain/$module/RoomOpening.cs.bak"
  expect_failure "HS000: '#pragma warning disable HS000' in 'RoomOpening.cs' suppresses HS000, which no justification allows." "docs/rules/HS000.md" "a domain type of $app_name that suppresses HS000" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/RoomOpening.cs"
}

# Seeds a slice handler of module <module>, then a domain type of it, that read the system clock: the analyzer of the web project
# reports HS010 in the slice, the analyzer of the domain in its own compilation, which it recognizes by the entity contract.
probe_system_clock() {
  local module="$1"
  mkdir -p "$app_src/Features/$module/ExpireProbes"
  cat > "$app_src/Features/$module/ExpireProbes/ExpireProbes.cs" <<EOF
namespace $app_name.Features.$module.ExpireProbes;

internal sealed class ExpireProbesHandler
{
    public bool Handle(DateTimeOffset end) => end < DateTimeOffset.UtcNow;
}
EOF
  expect_failure "HS010: 'ExpireProbesHandler.Handle' in slice '$module/ExpireProbes' reads 'DateTimeOffset.UtcNow'." "docs/rules/HS010.md" "a slice handler of $app_name that reads the system clock" check.sh check.ps1
  # PB-068: a delay on the system clock, which no test can advance. The one on the injected TimeProvider passes.
  cat > "$app_src/Features/$module/ExpireProbes/ExpireProbes.cs" <<EOF
namespace $app_name.Features.$module.ExpireProbes;

internal sealed class ExpireProbesHandler(TimeProvider timeProvider)
{
    public async Task Handle(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), timeProvider, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}
EOF
  expect_failure "HS010: 'ExpireProbesHandler.Handle' in slice '$module/ExpireProbes' waits on the system clock with 'Task.Delay(TimeSpan)'." "docs/rules/HS010.md" "a slice handler of $app_name that waits on the system clock" check.sh check.ps1
  rm -r "$app_src/Features/$module/ExpireProbes"

  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomOpening.cs" <<EOF
namespace $app_name.Domain.$module;

public static class RoomOpening
{
    public static bool IsOpenToday() => DateTime.Today.DayOfWeek != DayOfWeek.Sunday;
}
EOF
  expect_failure "HS010: 'RoomOpening.IsOpenToday' in domain project '$app_name.Domain' reads 'DateTime.Today'." "docs/rules/HS010.md" "a domain type of $app_name that reads the system clock" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/RoomOpening.cs"
}

# Runs one check script with the given arguments.
run_check_mode() {
  local script="$1"
  shift
  case "$script" in
    check.sh) bash "$app/scripts/check.sh" "$@" ;;
    check.ps1) pwsh -NoProfile -File "$app/scripts/check.ps1" "$@" ;;
  esac
}

# The modes of the check scripts (PB-045), with a domain type of module <module> for HS010 and the TODO(hardslice): markers seeded
# at the end of <code-file> and <test-file>, relative to the root of the solution and restored at the end:
# - any argument but --build exits with code 2 before building;
# - --build runs the build alone and fails with its errors alone, each line as dotnet build prints it but for the project
#   MSBuild appends, and says that only the full check decides;
# - both modes fail while a marker is left, after their other steps, and name each file and line.
probe_check_modes() {
  local module="$1"
  local code_file="$2"
  local test_file="$3"
  local script
  local check_output
  local status
  local arguments

  for script in check.sh check.ps1; do
    for arguments in "--affected" "--build --build" "build"; do
      status=0
      # shellcheck disable=SC2086 # Each case is split into its arguments.
      check_output="$(run_check_mode "$script" $arguments 2>&1)" || status=$?
      if [[ "$status" != 2 || "$check_output" != "error: scripts/$script takes no argument, or --build for the incremental build alone. Unknown arguments: '$arguments'." ]]; then
        printf '%s\n' "$check_output" >&2
        echo "error: scripts/$script $arguments exited with code $status instead of refusing the arguments with code 2." >&2
        exit 1
      fi
    done
  done
  echo "    unknown arguments: code 2, nothing built"

  for script in check.sh check.ps1; do
    if ! check_output="$(run_check_mode "$script" --build 2>&1)"; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script --build failed on $app_name." >&2
      exit 1
    fi
    if [[ "$check_output" != *"Only the full check, 'dotnet hardslice check' or 'scripts/check.sh' without argument, decides that the work is done."* ||
      "$check_output" != *"==> Build passed. Tests did not run: only the full check decides that the work is done."* ]] ||
      grep -Eq 'Running (unit|architecture|integration) tests|Build succeeded|Restore was successful|Time Elapsed' <<< "$check_output"; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script --build on $app_name ran more than the build, or did not say that only the full check decides." >&2
      exit 1
    fi
  done
  echo "    --build passes: the build alone, without tests, banner nor summary"

  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomOpening.cs" <<CODE
namespace $app_name.Domain.$module;

public static class RoomOpening
{
    public static bool IsOpenToday() => DateTime.Today.DayOfWeek != DayOfWeek.Sunday;
}
CODE
  local build_errors
  build_errors="$( (cd "$app" && dotnet build "$app_name.slnx" --no-restore -nologo -tl:off 2>&1) | grep -E ': error [A-Z]+[0-9]+: ' | sed -E 's/ \[[^]]*\.csproj\]$//' | LC_ALL=C sort -u || true)"
  if ! grep -Fq "error HS010: 'RoomOpening.IsOpenToday' in domain project '$app_name.Domain' reads 'DateTime.Today'." <<< "$build_errors"; then
    printf '%s\n' "$build_errors" >&2
    echo "error: dotnet build of $app_name does not report HS010 on RoomOpening." >&2
    exit 1
  fi
  for script in check.sh check.ps1; do
    if check_output="$(run_check_mode "$script" --build 2>&1)"; then
      echo "error: scripts/$script --build passed despite HS010 in the domain of $app_name." >&2
      exit 1
    fi
    if [[ "$(grep -E ': error [A-Z]+[0-9]+: ' <<< "$check_output" | LC_ALL=C sort -u)" != "$build_errors" ]] ||
      grep -Eq 'Running (unit|architecture|integration) tests|Build FAILED|Time Elapsed|TODO\(hardslice\): markers' <<< "$check_output"; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script --build did not print the errors of dotnet build alone, as the compiler wrote them." >&2
      exit 1
    fi
  done
  rm "$app/src/$app_name.Domain/$module/RoomOpening.cs"
  rmdir "$app/src/$app_name.Domain/$module" 2> /dev/null || true
  echo "    --build fails with the errors of dotnet build alone: ${build_errors##*: error }"

  cp "$app/$code_file" "$work/marker-code.backup"
  cp "$app/$test_file" "$work/marker-test.backup"
  printf '%s\n' "// TODO(hardslice): a marker left in the code." >> "$app/$code_file"
  printf '%s\n' "// TODO(hardslice): a marker left in a test." >> "$app/$test_file"
  local expected_markers
  expected_markers="$code_file:$(grep -c '' "$app/$code_file"): // TODO(hardslice): a marker left in the code.
$test_file:$(grep -c '' "$app/$test_file"): // TODO(hardslice): a marker left in a test."
  for arguments in "" "--build"; do
    for script in check.sh check.ps1; do
      # The full check reaches the markers once every other step passed: they are its only failure, with the code 1 (PB-055).
      status=0
      # shellcheck disable=SC2086 # No argument for the full check.
      check_output="$(run_check_mode "$script" $arguments 2>&1)" || status=$?
      check_output="$(strip_ansi <<< "$check_output")"
      if [[ "$status" != 1 ]]; then
        printf '%s\n' "$check_output" >&2
        echo "error: scripts/$script $arguments exited with code $status instead of 1 despite TODO(hardslice): markers in $code_file and $test_file." >&2
        exit 1
      fi
      if [[ "$check_output" != *"error: 2 TODO(hardslice): markers are left in src/ and tests/: "* ||
        "$(grep -E '^(src|tests)/[^:]+:[0-9]+: ' <<< "$check_output")" != "$expected_markers" ]] ||
        { [[ -z "$arguments" && "$check_output" != *"==> Running integration tests"* ]]; } ||
        grep -Eq '^failed |(error|warning) HS[0-9]{3}' <<< "$check_output"; then
        printf '%s\n' "$check_output" >&2
        echo "error: scripts/$script $arguments did not fail on the TODO(hardslice): markers alone, naming $code_file and $test_file with their lines." >&2
        exit 1
      fi
    done
  done
  mv "$work/marker-code.backup" "$app/$code_file"
  mv "$work/marker-test.backup" "$app/$test_file"
  echo "    TODO(hardslice): markers fail both modes with code 1 after their other steps: ${expected_markers//$'\n'/, }"
}

# Runs one hook of an agent in the solution with the payload given on its standard input, as the configuration of the CLI declares it.
# Keeps its standard error in hook_error, its standard output in $work/hook-output, and sets hook_status; extra arguments are environment assignments.
run_hook() {
  local cli="$1" event="$2" payload="$3"
  shift 3
  hook_status=0
  hook_error="$(cd "$app" && printf '%s' "$payload" | env "$@" dotnet hardslice hook "$cli" "$event" 2>&1 > "$work/hook-output")" || hook_status=$?
}

run_claude_hook() {
  local event="$1" payload="$2"
  shift 2
  run_hook claude "$event" "$payload" "$@"
}

claude_payload() {
  local event="$1" file="$2" active="${3:-false}"
  printf '{"session_id":"test-template-%s","cwd":"%s","hook_event_name":"%s","tool_name":"Edit","tool_input":{"file_path":"%s"},"stop_hook_active":%s}' \
    "$$" "$app" "$event" "$file" "$active"
}

# The hooks of Claude Code (PB-046), with a domain type of module <module> for HS010: .claude/settings.json runs
# dotnet hardslice hook claude <event> for each event; the edit of a protected file is refused with code 2, the edit of a .cs file
# that breaks the build hands its errors to the agent with code 2, any other file builds nothing, the end of a turn passes on a
# passing check and is blocked on a failing one, and every hook exits 0 at once under Copilot CLI.
probe_claude_hooks() {
  local module="$1" event
  for event in PreToolUse PostToolUse Stop; do
    if ! grep -Fq "\"command\": \"dotnet hardslice hook claude $event\"" "$app/.claude/settings.json"; then
      echo "error: .claude/settings.json of $app_name does not run dotnet hardslice hook claude $event." >&2
      exit 1
    fi
  done

  run_claude_hook PreToolUse "$(claude_payload PreToolUse "$app/scripts/check.sh")"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: 'scripts/check.sh' is protected (scripts/)"*"\"Forbidden\" of AGENTS.md"* ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PreToolUse hook did not refuse the edit of scripts/check.sh with code 2 (code $hook_status)." >&2
    exit 1
  fi
  run_claude_hook PreToolUse "$(claude_payload PreToolUse "$app/src/$app_name/Program.cs")"
  if [[ "$hook_status" != 0 || -n "$hook_error" ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PreToolUse hook refused the edit of src/$app_name/Program.cs (code $hook_status)." >&2
    exit 1
  fi
  echo "    PreToolUse: scripts/check.sh refused with code 2, src/$app_name/Program.cs allowed"

  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomOpening.cs" <<CODE
namespace $app_name.Domain.$module;

public static class RoomOpening
{
    public static bool IsOpenToday() => DateTime.Today.DayOfWeek != DayOfWeek.Sunday;
}
CODE
  run_claude_hook PostToolUse "$(claude_payload PostToolUse "$app/src/$app_name.Domain/$module/RoomOpening.cs")"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: the build fails after your edit of 'src/$app_name.Domain/$module/RoomOpening.cs'."* ||
    "$hook_error" != *"error HS010: 'RoomOpening.IsOpenToday' in domain project '$app_name.Domain' reads 'DateTime.Today'."* ]] ||
    grep -Eq 'Running (unit|architecture|integration) tests' <<< "$hook_error"; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PostToolUse hook did not hand the HS010 error of the build to the agent with code 2 (code $hook_status)." >&2
    exit 1
  fi
  run_claude_hook PostToolUse "$(claude_payload PostToolUse "$app/README.md")"
  if [[ "$hook_status" != 0 || -n "$hook_error" ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PostToolUse hook failed after the edit of a file other than .cs (code $hook_status)." >&2
    exit 1
  fi
  echo "    PostToolUse: HS010 handed to the agent with code 2, nothing built for a file other than .cs"

  # PB-077: after an edit, the errors of the edited file are shown and those of the other files only counted, while the Stop hook lists them all.
  cat > "$app/src/$app_name.Domain/$module/RoomClosing.cs" <<CODE
namespace $app_name.Domain.$module;

public static class RoomClosing
{
    public static bool IsClosedToday() => DateTime.Today.DayOfWeek == DayOfWeek.Sunday;
}
CODE
  run_claude_hook PostToolUse "$(claude_payload PostToolUse "$app/src/$app_name.Domain/$module/RoomOpening.cs")"
  if [[ "$hook_status" != 2 || "$hook_error" != *"error HS010: 'RoomOpening.IsOpenToday'"* || "$hook_error" == *"RoomClosing.cs("* ]] ||
    ! grep -Eq '^HardSlice: [0-9]+ more errors? in 1 other file, not shown here since the next edits of your change may fix them\.' <<< "$hook_error"; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PostToolUse hook did not show the errors of the edited file and only count those of src/$app_name.Domain/$module/RoomClosing.cs (code $hook_status)." >&2
    exit 1
  fi
  echo "    PostToolUse: the HS010 of the edited file shown, the one of another file only counted"

  run_claude_hook Stop "$(claude_payload Stop "" false)"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: the check fails, so the work is not done."*"(relaunch 1 of 3 for this failure)"* ||
    "$hook_error" != *"error HS010: 'RoomOpening.IsOpenToday'"* || "$hook_error" != *"error HS010: 'RoomClosing.IsClosedToday'"* ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the Stop hook did not block the end of the turn on HS010 with code 2, with the errors of every file (code $hook_status)." >&2
    exit 1
  fi
  rm "$app/src/$app_name.Domain/$module/RoomClosing.cs"
  for event in PreToolUse PostToolUse Stop; do
    run_claude_hook "$event" "$(claude_payload "$event" "$app/src/$app_name.Domain/$module/RoomOpening.cs")" COPILOT_CLI=1
    if [[ "$hook_status" != 0 || -n "$hook_error" || -s "$work/hook-output" ]]; then
      printf '%s\n' "$hook_error" >&2
      echo "error: the $event hook did something under Copilot CLI (code $hook_status)." >&2
      exit 1
    fi
  done
  # PB-055: once the build passes, the markers left are work to finish, not errors of the edit.
  cat > "$app/src/$app_name.Domain/$module/RoomOpening.cs" <<CODE
namespace $app_name.Domain.$module;

// TODO(hardslice): give the opening days of a room.
public static class RoomOpening;
CODE
  run_claude_hook PostToolUse "$(claude_payload PostToolUse "$app/src/$app_name.Domain/$module/RoomOpening.cs")"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: the build passes after your edit of 'src/$app_name.Domain/$module/RoomOpening.cs'. 1 TODO(hardslice): marker is left: "* ||
    "$hook_error" != *"src/$app_name.Domain/$module/RoomOpening.cs:3: // TODO(hardslice): give the opening days of a room."* || "$hook_error" == *"the build fails"* ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PostToolUse hook did not hand the marker left after a passing build to the agent as work to finish (code $hook_status)." >&2
    exit 1
  fi
  rm "$app/src/$app_name.Domain/$module/RoomOpening.cs"
  rmdir "$app/src/$app_name.Domain/$module" 2> /dev/null || true
  echo "    PostToolUse: a marker left after a passing build reaches the agent as work to finish"

  run_claude_hook Stop "$(claude_payload Stop "" true)"
  if [[ "$hook_status" != 0 || -n "$hook_error" || "$(cat "$work/hook-output")" != '{"systemMessage":"HardSlice: the check passed."}' ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the Stop hook blocked the end of the turn on a passing check, or did not tell the user that it passed (code $hook_status)." >&2
    exit 1
  fi
  echo "    Stop: HS010 blocks the end of the turn with code 2, a passing check lets the agent finish; under Copilot CLI, every hook exits 0"
}

# Runs check --build of both scripts in turn: each must pass, and restore first only for the first <restores> of them, with
# <reason> in its title. Prints the output of a failing one.
expect_build_restores() {
  local restores="$1" reason="$2" description="$3" script check_output count=0
  for script in check.sh check.ps1; do
    if ! check_output="$(run_check_mode "$script" --build 2>&1)"; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script --build failed on $app_name after $description." >&2
      exit 1
    fi
    count=$((count + 1))
    if (( count <= restores )); then
      if [[ "$check_output" != *"==> Restoring $app_name.slnx: $reason"* ]]; then
        printf '%s\n' "$check_output" >&2
        echo "error: scripts/$script --build did not restore $app_name after $description, saying '$reason'." >&2
        exit 1
      fi
    elif [[ "$check_output" == *"==> Restoring"* ]]; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script --build restored $app_name again after $description, although no file read by the restore changed since the last one." >&2
      exit 1
    fi
  done
}

# PB-061: a restore that changes no package does not rewrite project.assets.json. check --build records what its restore read,
# so a project file edited without changing a package, as dotnet user-secrets init does, is restored once, by the hook after an
# edit or by the first check --build, then no more. A package added is restored and seen by the build. Without obj/, the project
# has never been restored.
probe_restore_once() {
  local project="src/$app_name/$app_name.csproj" csv_file="tests/$app_name.Tests/CsvProbe.cs"
  cp -p "$app/$project" "$work/restore-project.backup"
  cp -p "$app/Directory.Packages.props" "$work/restore-packages.backup"

  printf '%s\n' "<!-- An edit that changes no package. -->" >> "$app/$project"
  expect_build_restores 1 "$project changed since the last restore of $project" "an edit of $project that changes no package"
  printf '%s\n' "<!-- Another edit that changes no package. -->" >> "$app/$project"
  run_claude_hook PostToolUse "$(claude_payload PostToolUse "$app/src/$app_name/Program.cs")"
  if [[ "$hook_status" != 0 || -n "$hook_error" ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PostToolUse hook failed after an edit of $project that changes no package (code $hook_status)." >&2
    exit 1
  fi
  expect_build_restores 0 "" "the restore of the PostToolUse hook"
  echo "    $project edited without a package change: restored once, by check --build or the hook after an edit, then no more"

  sed -i.bak 's|<ItemGroup Label="Validation">|<ItemGroup Label="Validation"><PackageVersion Include="CsvHelper" Version="33.1.0" />|' "$app/Directory.Packages.props"
  sed -i.bak 's|<PackageReference Include="FluentValidation" />|<PackageReference Include="FluentValidation" /><PackageReference Include="CsvHelper" />|' "$app/$project"
  rm "$app/Directory.Packages.props.bak" "$app/$project.bak"
  cat > "$app/$csv_file" <<CODE
namespace $app_name.Tests;

internal static class CsvProbe
{
    internal static readonly Type Reader = typeof(CsvHelper.CsvReader);
}
CODE
  expect_build_restores 1 "Directory.Packages.props changed since the last restore of" "CsvHelper added to Directory.Packages.props and $project"
  echo "    CsvHelper added: restored, then built by a test that uses it, then no more restore"
  rm "$app/$csv_file"

  mv "$work/restore-project.backup" "$app/$project"
  mv "$work/restore-packages.backup" "$app/Directory.Packages.props"
  touch "$app/$project" "$app/Directory.Packages.props"
  expect_build_restores 1 "Directory.Packages.props changed since the last restore of" "CsvHelper removed from Directory.Packages.props and $project"
  rm -rf "${app:?}/src/$app_name/obj"
  expect_build_restores 1 "$project has never been restored" "the deletion of src/$app_name/obj"
  echo "    src/$app_name/obj deleted: has never been restored"
}

codex_payload() {
  local event="$1" patch="$2" active="${3:-false}"
  printf '{"session_id":"test-template-codex-%s","cwd":"%s","hook_event_name":"%s","tool_name":"apply_patch","tool_input":{"command":"%s"},"stop_hook_active":%s}' \
    "$$" "$app" "$event" "$patch" "$active"
}

copilot_payload() {
  local tool="$1" file="$2" active="${3:-false}"
  printf '{"sessionId":"test-template-copilot-%s","cwd":"%s","toolName":"%s","toolArgs":{"path":"%s"},"stopReason":"end_turn","stop_hook_active":%s}' \
    "$$" "$app" "$tool" "$file" "$active"
}

# PB-053: the files that would turn the rules off without touching a protected file of v0.2, a .globalconfig in a project folder,
# which overrides HardSlice.All.globalconfig, a Directory.Build.rsp, whose properties are global, and a skill of .agents/, which
# Codex, Gemini CLI and Copilot CLI read: the hook before an edit of Claude Code, Codex and Copilot CLI refuses to create each one.
probe_analyzer_settings_hooks() {
  local file entry
  if [[ ! -f "$app/.agents/skills/add-slice/SKILL.md" ]]; then
    echo "error: $app_name has no .agents/skills/add-slice/SKILL.md, the skill that Codex and Gemini CLI read." >&2
    exit 1
  fi
  for file in "src/$app_name/.globalconfig|.globalconfig" "Directory.Build.rsp|Directory.Build.rsp" ".agents/skills/add-slice/SKILL.md|.agents/" \
    "Directory.HardSlice.props|Directory.HardSlice.props"; do
    entry="${file#*|}"
    file="${file%%|*}"
    run_claude_hook PreToolUse "$(claude_payload PreToolUse "$app/$file")"
    if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: '$file' is protected ($entry)"*"\"Forbidden\" of AGENTS.md"* ]]; then
      printf '%s\n' "$hook_error" >&2
      echo "error: the PreToolUse hook of Claude Code did not refuse to write $file with code 2 (code $hook_status)." >&2
      exit 1
    fi
    run_hook codex PreToolUse "$(codex_payload PreToolUse "*** Begin Patch\\n*** Add File: $app/$file\\n+x\\n*** End Patch")"
    if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: '$file' is protected ($entry)"*"\"Forbidden\" of AGENTS.md"* ]]; then
      printf '%s\n' "$hook_error" >&2
      echo "error: the PreToolUse hook of Codex did not refuse a patch that adds $file with code 2 (code $hook_status)." >&2
      exit 1
    fi
    run_hook copilot preToolUse "$(copilot_payload create "$app/$file")"
    if [[ "$hook_status" != 0 || -n "$hook_error" ]] ||
      ! grep -Fq "\"permissionDecision\":\"deny\",\"permissionDecisionReason\":\"HardSlice: \\u0027$file\\u0027 is protected ($entry)" "$work/hook-output"; then
      printf '%s\n' "$hook_error" >&2
      cat "$work/hook-output" >&2
      echo "error: the preToolUse hook of Copilot CLI did not deny the creation of $file in JSON with code 0 (code $hook_status)." >&2
      exit 1
    fi
  done
  echo "    before an edit: Claude Code, Codex and Copilot CLI refuse src/$app_name/.globalconfig, Directory.Build.rsp, .agents/skills/add-slice/SKILL.md and Directory.HardSlice.props"

  # PB-060: the application owns Directory.Packages.props and its project files, where it adds its own packages.
  for file in Directory.Packages.props "src/$app_name/$app_name.csproj"; do
    run_claude_hook PreToolUse "$(claude_payload PreToolUse "$app/$file")"
    if [[ "$hook_status" != 0 || -n "$hook_error" ]]; then
      printf '%s\n' "$hook_error" >&2
      echo "error: the PreToolUse hook of Claude Code refused the edit of $file (code $hook_status)." >&2
      exit 1
    fi
    run_hook codex PreToolUse "$(codex_payload PreToolUse "*** Begin Patch\\n*** Update File: $app/$file\\n@@\\n*** End Patch")"
    if [[ "$hook_status" != 0 || -n "$hook_error" ]]; then
      printf '%s\n' "$hook_error" >&2
      echo "error: the PreToolUse hook of Codex refused a patch of $file (code $hook_status)." >&2
      exit 1
    fi
    run_hook copilot preToolUse "$(copilot_payload edit "$app/$file")"
    if [[ "$hook_status" != 0 || -n "$hook_error" || -s "$work/hook-output" ]]; then
      printf '%s\n' "$hook_error" >&2
      cat "$work/hook-output" >&2
      echo "error: the preToolUse hook of Copilot CLI answered on the edit of $file (code $hook_status)." >&2
      exit 1
    fi
  done
  echo "    before an edit: Claude Code, Codex and Copilot CLI let the agent edit Directory.Packages.props and src/$app_name/$app_name.csproj"
}

# Fails unless <file> of the solution runs dotnet hardslice hook <cli> <event> for each event.
expect_hook_commands() {
  local file="$1" field="$2" cli="$3" event
  shift 3
  for event in "$@"; do
    if ! grep -Fq "\"$field\": \"dotnet hardslice hook $cli $event\"" "$app/$file"; then
      echo "error: $file of $app_name does not run dotnet hardslice hook $cli $event." >&2
      exit 1
    fi
  done
}

# The hooks of Codex and Copilot CLI (PB-047), with a domain type of module <module> for HS010. Codex reads the files of an
# apply_patch in its patch and answers with code 2, as Claude Code; Copilot CLI reads JSON on the standard output with code 0:
# a denial before an edit, additionalContext after it, a block decision at the end of the turn, and nothing on a passing check.
probe_codex_and_copilot_hooks() {
  local module="$1" domain_file
  expect_hook_commands .codex/hooks.json command codex PreToolUse PostToolUse Stop
  expect_hook_commands .github/hooks/hardslice.json bash copilot preToolUse postToolUse agentStop
  expect_hook_commands .github/hooks/hardslice.json powershell copilot preToolUse postToolUse agentStop

  run_hook codex PreToolUse "$(codex_payload PreToolUse "*** Begin Patch\\n*** Update File: $app/src/$app_name/Program.cs\\n@@\\n*** Update File: $app/scripts/check.sh\\n@@\\n*** End Patch")"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: 'scripts/check.sh' is protected (scripts/)"*"\"Forbidden\" of AGENTS.md"* ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PreToolUse hook of Codex did not refuse a patch of scripts/check.sh with code 2 (code $hook_status)." >&2
    exit 1
  fi
  run_hook copilot preToolUse "$(copilot_payload edit "$app/.github/hooks/hardslice.json")"
  if [[ "$hook_status" != 0 || -n "$hook_error" ]] ||
    ! grep -Fq '"permissionDecision":"deny","permissionDecisionReason":"HardSlice: \u0027.github/hooks/hardslice.json\u0027 is protected (.github/hooks/)' "$work/hook-output"; then
    printf '%s\n' "$hook_error" >&2
    cat "$work/hook-output" >&2
    echo "error: the preToolUse hook of Copilot CLI did not deny the edit of .github/hooks/hardslice.json in JSON with code 0 (code $hook_status)." >&2
    exit 1
  fi
  run_hook copilot preToolUse "$(copilot_payload edit "$app/src/$app_name/Program.cs")"
  if [[ "$hook_status" != 0 || -n "$hook_error" || -s "$work/hook-output" ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the preToolUse hook of Copilot CLI answered on the edit of src/$app_name/Program.cs (code $hook_status)." >&2
    exit 1
  fi
  echo "    before an edit: Codex refuses a patch of scripts/check.sh with code 2, Copilot CLI denies .github/hooks/hardslice.json in JSON"

  domain_file="src/$app_name.Domain/$module/RoomOpening.cs"
  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/$domain_file" <<CODE
namespace $app_name.Domain.$module;

public static class RoomOpening
{
    public static bool IsOpenToday() => DateTime.Today.DayOfWeek != DayOfWeek.Sunday;
}
CODE
  run_hook codex PostToolUse "$(codex_payload PostToolUse "*** Begin Patch\\n*** Add File: $app/$domain_file\\n+code\\n*** End Patch")"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: the build fails after your edit of '$domain_file'."* ||
    "$hook_error" != *"error HS010: 'RoomOpening.IsOpenToday'"* ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the PostToolUse hook of Codex did not hand the HS010 error of the build to the agent with code 2 (code $hook_status)." >&2
    exit 1
  fi
  run_hook copilot postToolUse "$(copilot_payload create "$app/$domain_file")" COPILOT_CLI=1
  if [[ "$hook_status" != 0 || -n "$hook_error" ]] || ! grep -Fq '"additionalContext":"HardSlice: the build fails after your edit of' "$work/hook-output" ||
    ! grep -Fq 'error HS010: \u0027RoomOpening.IsOpenToday\u0027' "$work/hook-output"; then
    printf '%s\n' "$hook_error" >&2
    cat "$work/hook-output" >&2
    echo "error: the postToolUse hook of Copilot CLI did not hand the HS010 error of the build to the agent as additionalContext (code $hook_status)." >&2
    exit 1
  fi
  run_hook copilot postToolUse "$(copilot_payload view "$app/$domain_file")"
  if [[ "$hook_status" != 0 || -n "$hook_error" || -s "$work/hook-output" ]]; then
    echo "error: the postToolUse hook of Copilot CLI answered after a read (code $hook_status)." >&2
    exit 1
  fi
  echo "    after an edit: HS010 handed to Codex with code 2 and to Copilot CLI as additionalContext, nothing after a read"

  run_hook codex Stop "$(codex_payload Stop "" false)"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: the check fails, so the work is not done."*"(relaunch 1 of 3 for this failure)"* ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the Stop hook of Codex did not block the end of the turn on HS010 with code 2 (code $hook_status)." >&2
    exit 1
  fi
  run_hook copilot agentStop "$(copilot_payload "" "" false)" COPILOT_CLI=1
  if [[ "$hook_status" != 0 || -n "$hook_error" ]] ||
    ! grep -Fq '{"decision":"block","reason":"HardSlice: the check fails, so the work is not done.' "$work/hook-output"; then
    printf '%s\n' "$hook_error" >&2
    cat "$work/hook-output" >&2
    echo "error: the agentStop hook of Copilot CLI did not block the end of the turn on HS010 in JSON with code 0 (code $hook_status)." >&2
    exit 1
  fi
  rm "$app/$domain_file"
  rmdir "$app/src/$app_name.Domain/$module" 2> /dev/null || true

  run_hook codex Stop "$(codex_payload Stop "" true)"
  if [[ "$hook_status" != 0 || -n "$hook_error" || "$(cat "$work/hook-output")" != '{"systemMessage":"HardSlice: the check passed."}' ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the Stop hook of Codex blocked the end of the turn on a passing check, or did not answer in JSON (code $hook_status)." >&2
    exit 1
  fi
  run_hook copilot agentStop "$(copilot_payload "" "" true)" COPILOT_CLI=1
  if [[ "$hook_status" != 0 || -n "$hook_error" || -s "$work/hook-output" ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the agentStop hook of Copilot CLI blocked the end of the turn on a passing check (code $hook_status)." >&2
    exit 1
  fi
  echo "    end of a turn: HS010 blocks Codex with code 2 and Copilot CLI with a block decision; a passing check lets both finish"
}

# git in the solution, as a user with an identity, without signature.
app_git() {
  git -C "$app" -c user.name=HardSlice -c user.email=hardslice@example.invalid -c commit.gpgsign=false "$@"
}

# Runs the job protected-files of .github/workflows/hardslice.yml on the last commit of the repository of the solution, which stands
# for the merge commit of a pull request, with or without the label hardslice-approved. Keeps its output in workflow_output.
run_protected_files_job() {
  local approved="$1"
  workflow_status=0
  workflow_output="$(APPROVED="$approved" "$repo_root/scripts/generated-workflow.sh" run "$app" protected-files 2>&1)" || workflow_status=$?
}

# PB-048: the pre-commit hook of .githooks/ and the CI of .github/workflows/hardslice.yml, which both run scripts/check.sh without argument.
# In a temporary git repository of the solution, the hook turned on with the commands of AGENTS.md refuses a commit that breaks HS010
# and accepts a compliant one; the job protected-files of the workflow fails on a change of scripts/check.sh until a maintainer
# sets the label, and passes a change of documentation. The repository is then removed, the solution left as it was.
probe_pre_commit_and_ci() {
  local module="$1" activation baseline commit_status commit_output domain_file
  for activation in 'chmod +x .githooks/pre-commit' 'git config core.hooksPath .githooks'; do
    if ! grep -Fq "\`$activation\`" "$app/AGENTS.md"; then
      echo "error: the AGENTS.md of $app_name does not give the command '$activation' that turns the pre-commit hook on." >&2
      exit 1
    fi
  done

  # PB-055: the .gitignore of the solution keeps bin/, obj/ and TestResults/, written by the checks before, out of the repository.
  app_git init --quiet --initial-branch main
  app_git add --all
  if app_git ls-files | grep -Eq '(^|/)(bin|obj|TestResults)/'; then
    app_git ls-files | grep -E '(^|/)(bin|obj|TestResults)/' | head -5 >&2
    echo "error: git add --all tracks the build output or the test results of $app_name: its .gitignore does not keep them out." >&2
    exit 1
  fi
  echo "    git: add --all tracks neither bin/, obj/ nor TestResults/"
  app_git commit --quiet --no-verify --message "Generated by hardslice-api"
  baseline="$(app_git rev-parse HEAD)"
  if [[ -n "$(app_git config --get core.hooksPath || true)" ]]; then
    echo "error: the repository of $app_name runs the pre-commit hook before a human turns it on." >&2
    exit 1
  fi
  # PB-053: the merge commit of a pull request always has a first parent. Without one, git diff fails: the job must fail with it,
  # as shell: bash makes it with pipefail, instead of passing on the empty list that protected would read.
  run_protected_files_job false
  if [[ "$workflow_status" == 0 ]] || ! grep -Fq "HEAD^1" <<< "$workflow_output"; then
    printf '%s\n' "$workflow_output" >&2
    echo "error: the job protected-files of the workflow passed while git diff failed (code $workflow_status)." >&2
    exit 1
  fi
  (cd "$app" && chmod +x .githooks/pre-commit && git config core.hooksPath .githooks)

  domain_file="src/$app_name.Domain/$module/RoomOpening.cs"
  cat > "$app/$domain_file" <<CODE
namespace $app_name.Domain.$module;

public static class RoomOpening
{
    public static bool IsOpenToday() => DateTime.Today.DayOfWeek != DayOfWeek.Sunday;
}
CODE
  app_git add "$domain_file"
  commit_status=0
  commit_output="$(app_git commit --message "Read the clock in the domain" 2>&1)" || commit_status=$?
  if [[ "$commit_status" == 0 || "$(app_git rev-parse HEAD)" != "$baseline" ]] ||
    ! grep -Fq "error HS010: 'RoomOpening.IsOpenToday'" <<< "$commit_output" ||
    ! grep -Fq "HardSlice: the check fails, so the commit is refused." <<< "$commit_output"; then
    printf '%s\n' "$commit_output" >&2
    echo "error: the pre-commit hook did not refuse a commit that breaks HS010 (code $commit_status)." >&2
    exit 1
  fi
  app_git rm --quiet --cached "$domain_file"
  rm "${app:?}/${domain_file:?}"
  echo "    pre-commit: a commit that breaks HS010 is refused"

  printf '# Decisions\n\nThe rooms open every day.\n' > "$app/docs/decisions.md"
  app_git add docs/decisions.md
  commit_status=0
  commit_output="$(app_git commit --message "Record a decision" 2>&1)" || commit_status=$?
  if [[ "$commit_status" != 0 || "$(app_git rev-parse HEAD^)" != "$baseline" ]] || ! grep -Fq "==> Check passed" <<< "$commit_output"; then
    printf '%s\n' "$commit_output" >&2
    echo "error: the pre-commit hook did not run the check on a compliant commit, or refused it (code $commit_status)." >&2
    exit 1
  fi
  echo "    pre-commit: a compliant commit passes the check and is accepted"

  printf '# A change of the check\n' >> "$app/scripts/check.sh"
  app_git commit --quiet --no-verify --all --message "Change the check"
  run_protected_files_job false
  if [[ "$workflow_status" == 0 ]] || ! grep -Fq "HardSlice: 'scripts/check.sh' is protected (scripts/)." <<< "$workflow_output" ||
    ! grep -Fq "::error::This pull request changes protected files" <<< "$workflow_output"; then
    printf '%s\n' "$workflow_output" >&2
    echo "error: the job protected-files of the workflow did not fail on a change of scripts/check.sh without the label (code $workflow_status)." >&2
    exit 1
  fi
  run_protected_files_job true
  if [[ "$workflow_status" != 0 ]] || ! grep -Fq "::notice::This pull request changes protected files, approved by a maintainer" <<< "$workflow_output"; then
    printf '%s\n' "$workflow_output" >&2
    echo "error: the job protected-files of the workflow failed on a change of scripts/check.sh with the label hardslice-approved (code $workflow_status)." >&2
    exit 1
  fi
  # PB-053: a .globalconfig of a project folder takes precedence over HardSlice.All.globalconfig and turns HS016 off in silence.
  printf 'is_global = true\ndotnet_diagnostic.HS016.severity = none\n' > "$app/src/$app_name/.globalconfig"
  app_git add "src/$app_name/.globalconfig"
  app_git commit --quiet --no-verify --message "Turn HS016 off"
  run_protected_files_job false
  if [[ "$workflow_status" == 0 ]] || ! grep -Fq "HardSlice: 'src/$app_name/.globalconfig' is protected (.globalconfig)." <<< "$workflow_output" ||
    ! grep -Fq "::error::This pull request changes protected files" <<< "$workflow_output"; then
    printf '%s\n' "$workflow_output" >&2
    echo "error: the job protected-files of the workflow did not fail on a .globalconfig that turns HS016 off (code $workflow_status)." >&2
    exit 1
  fi
  # PB-060: the reference to HardSlice.Analyzers is protected, the packages of the application are not.
  sed -i.bak 's|Version="[^"]*"|Version="0.0.1"|' "$app/Directory.HardSlice.props"
  rm "$app/Directory.HardSlice.props.bak"
  app_git commit --quiet --no-verify --all --message "Downgrade the analyzers"
  run_protected_files_job false
  if [[ "$workflow_status" == 0 ]] || ! grep -Fq "HardSlice: 'Directory.HardSlice.props' is protected (Directory.HardSlice.props)." <<< "$workflow_output" ||
    ! grep -Fq "::error::This pull request changes protected files" <<< "$workflow_output"; then
    printf '%s\n' "$workflow_output" >&2
    echo "error: the job protected-files of the workflow did not fail on a change of Directory.HardSlice.props (code $workflow_status)." >&2
    exit 1
  fi
  printf 'The rooms close on Sundays.\n' >> "$app/docs/decisions.md"
  sed -i.bak 's|<ItemGroup Label="Validation">|<ItemGroup Label="Validation"><PackageVersion Include="CsvHelper" Version="33.1.0" />|' "$app/Directory.Packages.props"
  rm "$app/Directory.Packages.props.bak"
  app_git commit --quiet --no-verify --all --message "Change a decision and add a package"
  run_protected_files_job false
  if [[ "$workflow_status" != 0 ]] || ! grep -Fq "HardSlice: none of the 2 paths read is protected." <<< "$workflow_output"; then
    printf '%s\n' "$workflow_output" >&2
    echo "error: the job protected-files of the workflow failed on a change of documentation and of Directory.Packages.props alone (code $workflow_status)." >&2
    exit 1
  fi
  echo "    CI: protected-files fails without a first parent, on a change of scripts/check.sh, on a .globalconfig that turns HS016 off and on a change of Directory.HardSlice.props, passes scripts/check.sh with the label hardslice-approved, and passes a change of documentation and of Directory.Packages.props"

  app_git reset --quiet --hard "$baseline"
  rm -rf "${app:?}/.git"
}

# Restores a root file of the solution from its backup, newer than the assets files: the next check --build restores again.
restore_root_file() {
  mv "$work/$1.backup" "$app/$1"
  touch "$app/$1"
}

# Runs both check scripts, with the given argument if any: each must fail with <expected> in its output and <absent> nowhere.
expect_check_message() {
  local expected="$1" absent="$2" description="$3" script check_output
  shift 3
  for script in check.sh check.ps1; do
    if check_output="$(run_check_mode "$script" "$@" 2>&1)"; then
      echo "error: scripts/$script $* passed with $description." >&2
      exit 1
    fi
    if ! grep -Fq -- "$expected" <<< "$check_output" || { [[ -n "$absent" ]] && grep -Fq -- "$absent" <<< "$check_output"; }; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script $* failed with $description without '$expected'." >&2
      exit 1
    fi
  done
  echo "    $description: $expected"
}

# PB-060: the application owns Directory.Packages.props, which imports the reference to HardSlice.Analyzers from the protected
# Directory.HardSlice.props. A package of the application, CsvHelper, and a unit test that advances a FakeTimeProvider pass the check.
# Without the import, both scripts fail before the build, naming the file. A reference removed in Directory.Packages.props, or another
# Directory.Packages.props in a project folder, fails them after the build, naming each project without the analyzers.
probe_application_packages() {
  local packages_project="src/$app_name/$app_name.csproj" clock_test="tests/$app_name.Tests/FakeClockTests.cs" test_output project
  echo "==> Checking that the application adds its own packages and that its tests advance a FakeTimeProvider"
  cp -p "$app/Directory.Packages.props" "$work/Directory.Packages.props.backup"
  cp -p "$app/$packages_project" "$work/$app_name.csproj.backup"
  sed -i.bak 's|<ItemGroup Label="Validation">|<ItemGroup Label="Validation"><PackageVersion Include="CsvHelper" Version="33.1.0" />|' "$app/Directory.Packages.props"
  sed -i.bak 's|<PackageReference Include="FluentValidation" />|<PackageReference Include="FluentValidation" /><PackageReference Include="CsvHelper" />|' "$app/$packages_project"
  rm "$app/Directory.Packages.props.bak" "$app/$packages_project.bak"
  if ! grep -Fq '<PackageReference Include="CsvHelper" />' "$app/$packages_project" || ! grep -Fq '<PackageVersion Include="CsvHelper"' "$app/Directory.Packages.props"; then
    echo "error: the probe could not add CsvHelper to Directory.Packages.props and $packages_project of $app_name." >&2
    exit 1
  fi
  cat > "$app/$clock_test" <<CODE
using Microsoft.Extensions.Time.Testing;

namespace $app_name.Tests;

public sealed class FakeClockTests
{
    [Fact]
    public async Task A_delay_of_an_hour_ends_when_the_test_advances_the_clock_by_an_hour()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 4, 4, 30, 0, TimeSpan.Zero));
        var delay = Task.Delay(TimeSpan.FromHours(1), clock, TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(59));
        delay.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMinutes(1));
        await delay;

        clock.GetUtcNow().ShouldBe(new DateTimeOffset(2026, 11, 4, 5, 30, 0, TimeSpan.Zero));
    }
}
CODE
  run_check check.sh
  test_output="$(cd "$app" && dotnet test --project "tests/$app_name.Tests/$app_name.Tests.csproj" --no-build --filter-class "$app_name.Tests.FakeClockTests" 2>&1 | strip_ansi)" || {
    printf '%s\n' "$test_output" >&2
    echo "error: the unit test that advances a FakeTimeProvider failed in $app_name.Tests." >&2
    exit 1
  }
  if ! grep -Eq 'succeeded: 1$' <<< "$test_output"; then
    printf '%s\n' "$test_output" >&2
    echo "error: the check of $app_name did not run the unit test that advances a FakeTimeProvider." >&2
    exit 1
  fi
  rm "$app/$clock_test"
  mv "$work/$app_name.csproj.backup" "$app/$packages_project"
  touch "$app/$packages_project"
  echo "    CsvHelper added to Directory.Packages.props and $packages_project, a FakeTimeProvider advanced in $app_name.Tests: the check passes"

  sed -i.bak '/Directory.HardSlice.props" \/>/d' "$app/Directory.Packages.props"
  rm "$app/Directory.Packages.props.bak"
  expect_check_message "error: Directory.Packages.props does not import Directory.HardSlice.props, which references HardSlice.Analyzers" "==> Building" \
    "Directory.Packages.props without its import" --build
  expect_check_message "error: Directory.Packages.props does not import Directory.HardSlice.props, which references HardSlice.Analyzers" "==> Building" \
    "Directory.Packages.props without its import"
  cp "$work/Directory.Packages.props.backup" "$app/Directory.Packages.props"

  sed -i.bak 's|<ItemGroup Label="Validation">|<ItemGroup Label="Validation"><GlobalPackageReference Remove="HardSlice.Analyzers" />|' "$app/Directory.Packages.props"
  rm "$app/Directory.Packages.props.bak"
  expect_check_message "error: $packages_project does not receive HardSlice.Analyzers $hardslice_version: it does not reference it for net10.0." "Build passed" \
    "a GlobalPackageReference Remove of HardSlice.Analyzers in Directory.Packages.props" --build
  cp "$work/Directory.Packages.props.backup" "$app/Directory.Packages.props"
  touch "$app/Directory.Packages.props"

  # The nearest Directory.Packages.props wins: one in the folder of the domain hides the root one, and its import, from that project.
  printf '<Project>\n  <PropertyGroup>\n    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>\n  </PropertyGroup>\n</Project>\n' \
    > "$app/src/$app_name.Domain/Directory.Packages.props"
  project="src/$app_name.Domain/$app_name.Domain.csproj"
  expect_check_message "error: $project does not receive HardSlice.Analyzers $hardslice_version: it does not reference it for net10.0." "Running unit tests" \
    "a Directory.Packages.props in the folder of the domain"
  rm "$app/src/$app_name.Domain/Directory.Packages.props"
  restore_root_file Directory.Packages.props
}

# PB-062: each generated solution has its own UserSecretsId, so dotnet user-secrets set needs no init, and its integration tests
# never read a user secret nor start a hosted service of the application. A BackgroundService registered in Program.cs writes a line
# each time it starts: after the whole integration test project, only the seeded test that keeps it with KeepHostedService started it,
# while a user secret set for the application stays out of the configuration of AppFactory, whose test database stays in place.
user_secrets_ids=()
# PB-079: writes in Shared/<folder> the authentication scheme of an API key, its policy and the extension that registers them, as an
# application whose endpoints of several modules share it writes them, and registers it in Program.cs.
write_probe_key_scheme() {
  local folder="$1"
  mkdir -p "$app_src/Shared/$folder"
  cat > "$app_src/Shared/$folder/ProbeKeyAuthentication.cs" <<EOF
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace $app_name.Shared.$folder;

internal sealed class ProbeKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ProbeKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(Request.Headers["X-Probe-Key"] == "probe"
            ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "probe")], SchemeName)), SchemeName))
            : AuthenticateResult.NoResult());
}

public static class ProbeKeyAuthentication
{
    public const string Policy = "ProbeKey";

    public static IServiceCollection AddProbeKeyAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(ProbeKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ProbeKeyAuthenticationHandler>(ProbeKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy.RequireAuthenticatedUser());
        return services;
    }
}
EOF
  # Program.cs already imports Shared.Http: only another folder needs its using.
  {
    grep -Fxq "using $app_name.Shared.$folder;" "$work/program.backup" || printf 'using %s.Shared.%s;\n' "$app_name" "$folder"
    perl -pe 's/^(builder\.Services\.AddProblemDetails\(\);)$/$1\nbuilder.Services.AddProbeKeyAuthentication();/' "$work/program.backup"
  } > "$app_src/Program.cs"
  grep -Fxq "builder.Services.AddProbeKeyAuthentication();" "$app_src/Program.cs" || {
    echo "error: the Program.cs of $app_name no longer calls builder.Services.AddProblemDetails() on a line of its own." >&2
    exit 1
  }
}

# PB-079: the authentication scheme of the application is cross-cutting: in Shared/Http, the check passes. The same scheme in a
# folder that hardslice.json does not allow fails HS017 on the given check scripts, and its message lists the four default folders.
probe_shared_folders() {
  local check_output

  cp "$app_src/Program.cs" "$work/program.backup"
  echo "==> Checking that the check script of $app_name passes with an authentication scheme in Shared/Http"
  write_probe_key_scheme Http
  if ! check_output="$(run_check check.sh 2>&1)"; then
    printf '%s\n' "$check_output" >&2
    echo "error: scripts/check.sh failed with an authentication scheme in Shared/Http, which hardslice.json allows." >&2
    exit 1
  fi
  rm -r "$app_src/Shared/Http/ProbeKeyAuthentication.cs"

  echo "==> Checking that HS017 fails the check scripts of $app_name on the same scheme in Shared/Security, and lists the allowed folders"
  write_probe_key_scheme Security
  expect_failure "HS017: '[A-Za-z]*' is in 'Shared/Security', a folder hardslice.json does not allow\. Shared holds only cross-cutting code, in the folders allowed by hardslice.json: Messaging, Persistence, Behaviors, Http\." \
    "docs/rules/HS017.md" "an authentication scheme in Shared/Security, a folder of $app_name that hardslice.json does not allow" "$@"
  rm -r "$app_src/Shared/Security"
  mv "$work/program.backup" "$app_src/Program.cs"
}

probe_integration_isolation() {
  local project="src/$app_name/$app_name.csproj" service_file="$app_src/Shared/Behaviors/ProbeHostedService.cs"
  local test_file="$app/tests/$app_name.IntegrationTests/ProbeIsolationTests.cs" starts_file="$app/tests/$app_name.IntegrationTests/bin/Debug/net10.0/probe-hosted-service.log"
  local user_secrets_id known test_output
  echo "==> Checking that $app_name has its own UserSecretsId and that its integration tests read no user secret and start no hosted service of the application"
  user_secrets_id="$(sed -n 's|.*<UserSecretsId>\(.*\)</UserSecretsId>.*|\1|p' "$app/$project")"
  if [[ ! "$user_secrets_id" =~ ^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$ ]] ||
    grep -rIlF -- "$user_secrets_id" "$app" --exclude-dir=bin --exclude-dir=obj | grep -vFx "$app/$project"; then
    echo "error: $project has no UserSecretsId of its own: '$user_secrets_id', or it appears in another file than $project." >&2
    exit 1
  fi
  for known in "${user_secrets_ids[@]}" 5d0a9c3e-7b41-4f6a-9e28-c1b4a7f3d206; do
    if [[ "$user_secrets_id" == "$known" ]]; then
      echo "error: $project has the UserSecretsId $user_secrets_id of the template or of another generated solution." >&2
      exit 1
    fi
  done
  user_secrets_ids+=("$user_secrets_id")
  (cd "$app" && dotnet user-secrets set "Probe:Secret" "from-the-user-secrets" --project "src/$app_name")
  if ! (cd "$app" && dotnet user-secrets list --project "src/$app_name") | grep -Fxq "Probe:Secret = from-the-user-secrets"; then
    echo "error: dotnet user-secrets did not keep Probe:Secret for $app_name." >&2
    exit 1
  fi
  echo "    UserSecretsId $user_secrets_id: dotnet user-secrets set passes without init"

  cp "$app_src/Program.cs" "$work/Program-isolation.backup"
  awk '/^var app = builder.Build\(\);$/ { print "builder.Services.AddHostedService<ProbeHostedService>();"; print "" } { print }' \
    "$work/Program-isolation.backup" > "$app_src/Program.cs"
  if ! grep -Fxq 'builder.Services.AddHostedService<ProbeHostedService>();' "$app_src/Program.cs"; then
    echo "error: the probe could not register a hosted service in the Program.cs of $app_name." >&2
    exit 1
  fi
  cat > "$service_file" <<CODE
namespace $app_name.Shared.Behaviors;

// Writes a line next to the assembly each time it starts.
internal sealed class ProbeHostedService : BackgroundService
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await File.AppendAllTextAsync(Path.Combine(AppContext.BaseDirectory, "probe-hosted-service.log"), "started\n", stoppingToken);
        Started.TrySetResult();
    }
}
CODE
  cat > "$test_file" <<CODE
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using $app_name.Shared.Behaviors;

namespace $app_name.IntegrationTests;

public sealed class ProbeIsolationTests(PostgresDatabase database)
{
    [Fact]
    public async Task Reads_no_user_secret_and_starts_no_hosted_service_of_the_application()
    {
        await using var app = new AppFactory(database);

        var response = await app.CreateClient().GetAsync(\$"/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        configuration["Probe:Secret"].ShouldBeNull();
        configuration.GetConnectionString("Database").ShouldBe(database.ConnectionString);
        app.Services.GetServices<IHostedService>().OfType<ProbeHostedService>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Starts_the_hosted_service_that_the_test_keeps()
    {
        await using var app = new AppFactory(database).KeepHostedService<ProbeHostedService>();

        var service = app.Services.GetServices<IHostedService>().OfType<ProbeHostedService>().ShouldHaveSingleItem();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        app.Services.GetRequiredService<IConfiguration>()["Probe:Secret"].ShouldBeNull();
    }
}
CODE
  rm -f "$starts_file"
  test_output="$(
    cd "$app"
    dotnet build "$app_name.slnx" 2>&1 &&
      dotnet test --project "tests/$app_name.IntegrationTests/$app_name.IntegrationTests.csproj" --no-build 2>&1
  )" || {
    printf '%s\n' "$test_output" >&2
    echo "error: the integration tests of $app_name failed with a hosted service registered and a user secret set." >&2
    exit 1
  }
  if [[ "$(grep -c '^started$' "$starts_file" 2>/dev/null)" != 1 ]]; then
    printf '%s\n' "$test_output" >&2
    echo "error: the hosted service of $app_name started $(grep -c '^started$' "$starts_file" 2>/dev/null || echo 0) times in its integration tests, instead of once, in the test that keeps it." >&2
    exit 1
  fi
  echo "    ProbeHostedService started once, in the test that keeps it, and no test read Probe:Secret"
  rm "$service_file" "$test_file" "$starts_file"
  mv "$work/Program-isolation.backup" "$app_src/Program.cs"
  (cd "$app" && dotnet user-secrets clear --project "src/$app_name")
  rm -r "${HOME:?}/.microsoft/usersecrets/${user_secrets_id:?}"
}

# Seeds a domain type of module <module> that blocks on a task: the analyzer of the domain reports HS011 in its own compilation.
probe_blocking_call() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomAvailability.cs" <<EOF
namespace $app_name.Domain.$module;

public static class RoomAvailability
{
    public static bool IsFree(Task<bool> lookup) => lookup.Result;
}
EOF
  expect_failure "HS011: 'RoomAvailability.IsFree' blocks on 'Task<TResult>.Result'." "docs/rules/HS011.md" "a domain type of $app_name that blocks on a task" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/RoomAvailability.cs"
}

# Runs the item template <template> with dotnet new from the root of the solution under test, from the hive where generate-app.sh
# installed the templates of the package. Prints its output, standard error included.
new_item() {
  local template="$1"
  shift
  (cd "$app" && dotnet new "$template" --debug:custom-hive "$work/template-hive" "$@" 2>&1)
}

# The sources and tests of the solution under test with their content: a refused generation leaves them unchanged.
solution_snapshot() {
  (cd "$app" && find src tests -type f -not -path '*/bin/*' -not -path '*/obj/*' -print0 | LC_ALL=C sort -z | xargs -0 cksum)
}

# A refused generation of the item template <template> exits with a non-zero code, left in $item_refused_status, prints <message>
# and creates or changes nothing.
expect_item_refused() {
  local template="$1"
  local description="$2"
  local message="$3"
  shift 3
  local before
  local output
  local status=0

  before="$(solution_snapshot)"
  output="$(new_item "$template" "$@")" || status=$?
  if [[ "$status" == 0 ]]; then
    printf '%s\n' "$output" >&2
    echo "error: dotnet new $template accepted $description in $app_name." >&2
    exit 1
  fi
  if [[ "$output" != *"$message"* ]]; then
    printf '%s\n' "$output" >&2
    echo "error: dotnet new $template refused $description in $app_name without saying: $message" >&2
    exit 1
  fi
  if [[ "$(solution_snapshot)" != "$before" ]]; then
    echo "error: dotnet new $template refused $description in $app_name but changed its files." >&2
    exit 1
  fi
  item_refused_status="$status"
  echo "    $description: exit $status, $message"
}

# Generates the command slice <module>/<name> with the remaining options, checks that its three files exist, or two without
# validator tests for --parameters none or --every, and leaves the printed instructions in $command_output.
generate_command() {
  local module="$1"
  local name="$2"
  shift 2
  local generated
  local validator_tests="tests/$app_name.Tests/Features/$module/$name/${name}ValidatorTests.cs"

  if ! command_output="$(new_item hardslice-command --project "src/$app_name/$app_name.csproj" --module "$module" --name "$name" "$@")"; then
    printf '%s\n' "$command_output" >&2
    echo "error: dotnet new hardslice-command failed on the slice $module/$name of $app_name." >&2
    exit 1
  fi
  if [[ " $* " == *" --parameters none "* || " $* " == *" --every "* ]]; then
    if [[ -e "$app/$validator_tests" || -e "$app/$(dirname "$validator_tests")" ]]; then
      echo "error: dotnet new hardslice-command $* created $(dirname "$validator_tests") in $app_name." >&2
      exit 1
    fi
    validator_tests=""
  fi
  for generated in "src/$app_name/Features/$module/$name/$name.cs" $validator_tests \
    "tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"; do
    if [[ ! -f "$app/$generated" ]]; then
      printf '%s\n' "$command_output" >&2
      echo "error: dotnet new hardslice-command did not create $generated in $app_name." >&2
      exit 1
    fi
  done
}

# The folders of the module <name>: its slices and its contract, its domain, then its test folders. dotnet new hardslice-module
# creates the first one alone, with the contract, and the templates of its aggregates, slices and events create the others.
module_folders() {
  local name="$1"
  printf '%s\n' "src/$app_name/Features/$name" \
    "src/$app_name.Domain/$name" \
    "tests/$app_name.Tests/Domain/$name" \
    "tests/$app_name.Tests/Features/$name" \
    "tests/$app_name.IntegrationTests/Features/$name"
}

# PB-073: dotnet new never deletes a file, so a placeholder of an empty folder would stay next to the files of the templates that
# fill it. Fails when the solution holds a .gitkeep after <step>.
expect_no_placeholder() {
  local step="$1"
  local placeholders

  placeholders="$(cd "$app" && find . -name .gitkeep -not -path './.git/*')"
  if [[ -n "$placeholders" ]]; then
    printf '%s\n' "$placeholders" >&2
    echo "error: $app_name holds the .gitkeep files above after $step." >&2
    exit 1
  fi
}

# Generates the module <name> with the remaining options, checks that it creates its contract and no other file, and leaves the
# printed instructions in $module_output.
generate_module() {
  local name="$1"
  shift
  local contract="src/$app_name/Features/$name/${name}Module.cs"
  local before
  local created

  before="$(cd "$app" && find src tests -type f -not -path '*/bin/*' -not -path '*/obj/*' | LC_ALL=C sort)"
  if ! module_output="$(new_item hardslice-module --project "src/$app_name/$app_name.csproj" --name "$name" "$@")"; then
    printf '%s\n' "$module_output" >&2
    echo "error: dotnet new hardslice-module failed on the module $name of $app_name." >&2
    exit 1
  fi
  created="$(LC_ALL=C comm -13 <(printf '%s\n' "$before") <(cd "$app" && find src tests -type f -not -path '*/bin/*' -not -path '*/obj/*' | LC_ALL=C sort))"
  if [[ "$created" != "$contract" ]]; then
    printf '%s\n' "$module_output" "created:" "$created" >&2
    echo "error: dotnet new hardslice-module did not create $contract alone in $app_name: the templates that fill the other folders of the module create them." >&2
    exit 1
  fi
  expect_no_placeholder "dotnet new hardslice-module --name $name"
}

# Removes the folders of the module <name>, with what the templates added to them, then the parent folders they leave empty.
remove_module() {
  local name="$1"
  local generated
  local folder

  while read -r generated; do
    [[ ! -e "$app/$generated" ]] || rm -r "${app:?}/$generated"
  done < <(module_folders "$name")
  for folder in "src/$app_name/Features" "tests/$app_name.Tests/Domain" "tests/$app_name.Tests/Features" "tests/$app_name.IntegrationTests/Features"; do
    [[ ! -d "$app/$folder" ]] || rmdir --ignore-fail-on-non-empty "$app/$folder"
  done
}

# Checks that the group of the module <name> maps its endpoints under <route>, in its contract and in the printed instructions.
expect_group_route() {
  local name="$1"
  local route="$2"

  if ! grep -Fxq "    public const string Route = \"$route\";" "$app_src/Features/$name/${name}Module.cs" ||
    ! grep -Fxq "        var group = endpoints.MapGroup(Route);" "$app_src/Features/$name/${name}Module.cs" ||
    [[ "$module_output" != *"Its endpoints answer under $route."* ]]; then
    printf '%s\n' "$module_output" >&2
    echo "error: dotnet new hardslice-module did not map the module $name of $app_name under $route." >&2
    exit 1
  fi
  echo "    $name: $route"
}

# Generates the module <name> through every path of dotnet new hardslice-module: refusals, the route of its group, the kebab-case
# of <routed-name> by default, <default-route>, or the one of --route, the lines it prints for Program.cs, then a check that passes
# once they are added. Program.cs is saved in $work/Program.cs.backup and the module stays registered: the caller removes it.
probe_module_template() {
  local name="$1"
  local routed_name="$2"
  local default_route="$3"
  local project="src/$app_name/$app_name.csproj"
  local module_file="$app_src/Features/$name/${name}Module.cs"
  local kebab_name
  local expected

  echo "==> Checking that dotnet new hardslice-module refuses an unreadable application, an invalid name or route in $app_name"
  expect_item_refused hardslice-module "a run without the web project" "with --project src/<App>/<App>.csproj" --name "$name"
  expect_item_refused hardslice-module "a run without name" "--name is required" --project "$project"
  expect_item_refused hardslice-module "a name that is not PascalCase" "--name 'meeting-rooms' is not PascalCase" --project "$project" --name meeting-rooms
  expect_item_refused hardslice-module "a name with the Module suffix" "--name '${name}Module' ends with Module" --project "$project" --name "${name}Module"
  expect_item_refused hardslice-module "a route that is not made of lowercase words" "--route '/Meeting Rooms' is not a route of lowercase words separated by hyphens or slashes" --project "$project" --name "$name" --route "Meeting Rooms"
  expect_item_refused hardslice-module "an empty route" "--route '/' is not a route of lowercase words" --project "$project" --name "$name" --route /

  echo "==> Checking that dotnet new hardslice-module maps a module of $app_name under its name in kebab-case, or the route of --route"
  generate_module "$routed_name"
  expect_group_route "$routed_name" "/$default_route"
  remove_module "$routed_name"
  generate_module "$routed_name" --route /spaces/meeting
  expect_group_route "$routed_name" "/spaces/meeting"
  remove_module "$routed_name"
  generate_module "$routed_name" --route spaces
  expect_group_route "$routed_name" "/spaces"
  remove_module "$routed_name"

  echo "==> Generating the module $name of $app_name with dotnet new hardslice-module, registered in Program.cs as it prints"
  generate_module "$name"
  kebab_name="$(sed 's/\([a-z0-9]\)\([A-Z]\)/\1-\2/g' <<< "$name" | tr '[:upper:]' '[:lower:]')"
  for expected in "namespace $app_name.Features.$name;" \
    "public static class ${name}Module" \
    "    public static IServiceCollection Add${name}Module(this IServiceCollection services) =>" \
    "    public static IEndpointRouteBuilder Map${name}Module(this IEndpointRouteBuilder endpoints)" \
    "    public const string Route = \"/$kebab_name\";" \
    "        var group = endpoints.MapGroup(Route);"; do
    if ! grep -Fxq "$expected" "$module_file"; then
      cat "$module_file" >&2
      echo "error: dotnet new hardslice-module did not write the line '$expected' in the contract of module $name." >&2
      exit 1
    fi
  done
  expect_item_refused hardslice-module "a module that exists" "--force" --project "$project" --name "$name"

  cp "$app_src/Program.cs" "$work/Program.cs.backup"
  register_module "$module_output"
  # An empty module registered and mapped: HS012, HS015, HS017 and HS018 have nothing to report, the host starts.
  bash "$app/scripts/check.sh"
}

# Runs scripts/check.sh, which must fail on the tests marked TODO(hardslice): only, named by <expected-failures>, without any
# HardSlice diagnostic.
expect_check_fails_on_todo_tests_only() {
  local description="$1"
  local expected_failures="$2"
  local check_output
  local failures

  if check_output="$(run_check check.sh 2>&1)"; then
    echo "error: scripts/check.sh passed on $description, whose tests are marked TODO(hardslice):." >&2
    exit 1
  fi
  check_output="$(strip_ansi <<< "$check_output")"
  failures="$(sed -n 's/^failed \([^ ]*\) (.*/\1/p' <<< "$check_output" | LC_ALL=C sort -u)"
  if [[ "$failures" != "$expected_failures" || "$check_output" != *"TODO(hardslice): "* ]] ||
    grep -Eq '(error|warning) HS[0-9]{3}' <<< "$check_output"; then
    printf '%s\n' "$check_output" >&2
    echo "error: scripts/check.sh did not fail on $description with the TODO(hardslice): tests alone (${expected_failures//$'\n'/, }), without HardSlice diagnostic." >&2
    exit 1
  fi
  echo "    ${failures//$'\n'/, }: TODO(hardslice):"
}

# Generates a command slice of module <module>, which must exist, through every path of dotnet new hardslice-command: refusals,
# instructions, a forgotten registration, the TODO(hardslice): tests, the other responses, then the reference completion.
# Then the commands without parameters <parameterless-name> and <parameterless-dto-name>, through their TODO(hardslice): tests
# and their reference completion, and the system commands <system-name> and <system-parameterless-name>, through HS004 and
# their reference completion. The module file is restored and the slices removed at the end.
probe_command_template() {
  local module="$1"
  local name="$2"
  local none_name="$3"
  local dto_name="$4"
  local parameterless_name="$5"
  local parameterless_dto_name="$6"
  local system_name="$7"
  local system_parameterless_name="$8"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local slice="$app_src/Features/$module/$name/$name.cs"
  local integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"
  local unit_test="$app/tests/$app_name.Tests/Features/$module/$name/${name}ValidatorTests.cs"
  local project="src/$app_name/$app_name.csproj"
  local generated_slice
  local test_output
  local build_output

  cp "$module_file" "$work/module.backup"

  echo "==> Checking that dotnet new hardslice-command refuses an unreadable application, an invalid name or response, and a missing module in $app_name"
  expect_item_refused hardslice-command "a run without the web project" "with --project src/<App>/<App>.csproj" --module "$module" --name "$name"
  expect_item_refused hardslice-command "a run without name" "--name is required" --project "$project" --module "$module"
  expect_item_refused hardslice-command "a name that is not PascalCase" "--name 'extend-slot' is not PascalCase" --project "$project" --module "$module" --name extend-slot
  expect_item_refused hardslice-command "a name with the Command suffix" "--name '${name}Command' ends with Command" --project "$project" --module "$module" --name "${name}Command"
  expect_item_refused hardslice-command "a module that is not PascalCase" "--module 'payments' is not PascalCase" --project "$project" --module payments --name "$name"
  expect_item_refused hardslice-command "a module that does not exist" "module 'Payments' does not exist: src/$app_name/Features/Payments/PaymentsModule.cs was not found. Create the module first with dotnet new hardslice-module --project src/$app_name/$app_name.csproj --name Payments" --project "$project" --module Payments --name "$name"
  expect_item_refused hardslice-command "an unknown response" "--response 'dto' is neither id, none nor the PascalCase name of a DTO" --project "$project" --module "$module" --name "$name" --response dto
  # PB-056: a DTO named as a type that the slice already uses would not compile (Id, the command) or would hide another (Guid, None).
  local hidden_response
  for hidden_response in Guid "${name}Command" "$name"; do
    expect_item_refused hardslice-command "the response $hidden_response" "--response '$hidden_response' would name the DTO as a type that the slice already uses" --project "$project" --module "$module" --name "$name" --response "$hidden_response"
  done
  # PB-064: --response none answers 404 to an aggregate that a command without parameters cannot name.
  expect_item_refused hardslice-command "a command without parameters that answers 404" "--response none answers 404 to an unknown aggregate, which a command with --parameters none has no identifier to look for" --project "$project" --module "$module" --name "$parameterless_name" --parameters none --response none
  if [[ "$item_refused_status" != 105 ]]; then
    echo "error: dotnet new hardslice-command refused --parameters none --response none with exit $item_refused_status instead of 105." >&2
    exit 1
  fi
  expect_item_refused hardslice-command "unknown parameters" "'many' is not a valid value for --parameters" --project "$project" --module "$module" --name "$parameterless_name" --parameters many

  echo "==> Generating the command slice $module/$name of $app_name with dotnet new hardslice-command"
  generate_command "$module" "$name"
  for expected in "Register the slice in src/$app_name/Features/$module/${module}Module.cs:" \
    "using $app_name.Features.$module.$name;" \
    "            .AddScoped<IRequestHandler<${name}Command, Guid>, ${name}Handler>()" \
    "            .AddScoped<IValidator<${name}Command>, ${name}Validator>()" \
    "        ${name}Endpoint.Map(group);"; do
    if ! grep -Fxq -- "$expected" <<< "${command_output//Manual instructions: /}"; then
      printf '%s\n' "$command_output" >&2
      echo "error: dotnet new hardslice-command did not print the line '$expected'." >&2
      exit 1
    fi
  done
  if grep -q 'MyApp\|ModuleName\|CommandName\|ResponseTypeName\|command-route\|module-route' "$slice" "$unit_test" "$integration_test"; then
    echo "error: the slice $module/$name generated in $app_name keeps a placeholder of the template." >&2
    exit 1
  fi
  generated_slice="$(cat "$slice")"
  expect_item_refused hardslice-command "a slice that exists" "--force" --project "$project" --module "$module" --name "$name"
  expect_explicit_parameters_id "$module" "$name"

  # The endpoint is mapped, so the build passes: only the test of the host and the generated tests see the forgotten registrations.
  echo "==> Checking that the test of the host names the handler and the validator of $module/$name that ${module}Module does not register"
  register_slice hardslice-command "$command_output" without-registrations
  if test_output="$(
    cd "$app"
    dotnet build "$app_name.slnx" 2>&1 &&
      dotnet test --project "tests/$app_name.IntegrationTests/$app_name.IntegrationTests.csproj" --no-build 2>&1
  )"; then
    echo "error: the integration tests of $app_name passed with a slice its module does not register." >&2
    exit 1
  fi
  test_output="$(strip_ansi <<< "$test_output")"
  for expected in "failed $app_name.IntegrationTests.ProgramTests.Resolves_every_handler_and_validator_of_the_application_by_its_contract" \
    "'${name}Handler' as 'IRequestHandler<${name}Command, Guid>'" \
    "'${name}Validator' as 'IValidator<${name}Command>'" \
    "failed $app_name.IntegrationTests.Features.$module.$name.${name}Tests.Handles_a_valid_command"; do
    if [[ "$test_output" != *"$expected"* ]]; then
      printf '%s\n' "$test_output" >&2
      echo "error: the integration tests of $app_name did not report '$expected' for the unregistered slice $module/$name." >&2
      exit 1
    fi
  done
  echo "    ProgramTests names '${name}Handler' and '${name}Validator'"
  cp "$work/module.backup" "$module_file"

  echo "==> Checking that $app_name builds the registered slice $module/$name without HardSlice diagnostic and fails the check on its TODO(hardslice): test only"
  register_slice hardslice-command "$command_output"
  expect_check_fails_on_todo_tests_only "the generated slice $module/$name" \
    "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Handles_a_valid_command"
  cp "$module_file" "$work/module-registered.backup"

  echo "==> Checking that the none and DTO responses of dotnet new hardslice-command build without HardSlice diagnostic, HS005 included, in $app_name"
  generate_command "$module" "$none_name" --response none
  register_slice hardslice-command "$command_output"
  if ! grep -Fxq "            .AddScoped<IRequestHandler<${none_name}Command, bool>, ${none_name}Handler>()" "$module_file"; then
    echo "error: dotnet new hardslice-command --response none did not register a handler that returns bool." >&2
    exit 1
  fi
  generate_command "$module" "$dto_name" --response "${dto_name}Result"
  register_slice hardslice-command "$command_output"
  if ! grep -Fxq "public sealed record ${dto_name}Result(Guid Id);" "$app_src/Features/$module/$dto_name/$dto_name.cs"; then
    echo "error: dotnet new hardslice-command --response ${dto_name}Result did not declare the DTO in the slice." >&2
    exit 1
  fi
  if ! build_output="$(cd "$app" && dotnet build "$app_name.slnx" 2>&1)" || grep -Eq '(error|warning) HS[0-9]{3}' <<< "$build_output"; then
    printf '%s\n' "$build_output" >&2
    echo "error: $app_name does not build the slices generated with --response none and --response ${dto_name}Result without HardSlice diagnostic." >&2
    exit 1
  fi
  echo "    $none_name (bool, 204 or 404) and $dto_name (${dto_name}Result) build"
  rm -r "$app_src/Features/$module/$none_name" "$app_src/Features/$module/$dto_name" \
    "$app/tests/$app_name.Tests/Features/$module/$none_name" "$app/tests/$app_name.Tests/Features/$module/$dto_name" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$none_name" "$app/tests/$app_name.IntegrationTests/Features/$module/$dto_name"
  cp "$work/module-registered.backup" "$module_file"

  # The reference completion: the handler returns the identifier of the command, the test asserts it. No marker remains.
  echo "==> Checking that the reference completion of $module/$name passes the check of $app_name"
  if [[ "$(cat "$slice")" != "$generated_slice" ]]; then
    echo "error: the slice $module/$name changed before its completion." >&2
    exit 1
  fi
  complete_command "$module" "$name"
  bash "$app/scripts/check.sh"

  probe_parameterless_commands "$module" "$parameterless_name" "$parameterless_dto_name"
  probe_system_commands "$module" "$system_name" "$system_parameterless_name"

  rm -r "$app_src/Features/$module/$name" "$app/tests/$app_name.Tests/Features/$module/$name" "$app/tests/$app_name.IntegrationTests/Features/$module/$name"
  mv "$work/module.backup" "$module_file"
  rm "$work/module-registered.backup"
}

# The slice <module>/<name> generated with --parameters id holds the files and prints the lines of the slice generated without
# the option, which already exists: it is generated as <name>Explicit, compared once renamed back, then removed.
expect_explicit_parameters_id() {
  local module="$1"
  local name="$2"
  local default_output="$command_output"
  local kebab_name
  local renaming
  local folder
  local generated

  kebab_name="$(sed 's/\([a-z0-9]\)\([A-Z]\)/\1-\2/g' <<< "$name" | tr '[:upper:]' '[:lower:]')"
  renaming="s/${name}Explicit/$name/g; s/$kebab_name-explicit/$kebab_name/g"
  generate_command "$module" "${name}Explicit" --parameters id
  for folder in "src/$app_name/Features/$module" "tests/$app_name.Tests/Features/$module" "tests/$app_name.IntegrationTests/Features/$module"; do
    for generated in "$app/$folder/${name}Explicit"/*; do
      if ! sed "$renaming" "$generated" | diff - "$app/$folder/$name/$(basename "${generated//${name}Explicit/$name}")"; then
        echo "error: dotnet new hardslice-command --parameters id did not generate $generated as the default does." >&2
        exit 1
      fi
    done
    rm -r "${app:?}/$folder/${name:?}Explicit"
  done
  if [[ "$(sed "$renaming" <<< "$command_output")" != "$default_output" ]]; then
    diff <(sed "$renaming" <<< "$command_output") <(printf '%s\n' "$default_output") >&2 || true
    echo "error: dotnet new hardslice-command --parameters id did not print the lines of the default." >&2
    exit 1
  fi
  command_output="$default_output"
  echo "    --parameters id: the files and the lines of the default"
}

# The command slices <module>/<name> and <module>/<dto-name> generated with --parameters none, responding with their identifier
# and with <dto-name>Result: empty records without validator nor validator tests, endpoints without body, the handler alone
# registered, integration tests that post without body and check no 400. The check fails on their TODO(hardslice): tests only,
# then passes after their reference completion. The module is left as it was and the slices are removed.
probe_parameterless_commands() {
  local module="$1"
  local name="$2"
  local dto_name="$3"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local slice
  local integration_test
  local expected
  local generated

  echo "==> Generating the commands without parameters $module/$name and $module/$dto_name of $app_name with dotnet new hardslice-command --parameters none"
  cp "$module_file" "$work/module-parameterless.backup"
  generate_command "$module" "$name" --parameters none
  if [[ "$command_output" == *"IValidator"* || "$command_output" == *"using FluentValidation;"* ]] ||
    ! grep -Fxq "            .AddScoped<IRequestHandler<${name}Command, Guid>, ${name}Handler>()" <<< "$command_output"; then
    printf '%s\n' "$command_output" >&2
    echo "error: dotnet new hardslice-command --parameters none did not print the registration of the handler alone." >&2
    exit 1
  fi
  register_slice hardslice-command "$command_output"
  generate_command "$module" "$dto_name" --parameters none --response "${dto_name}Result"
  register_slice hardslice-command "$command_output"
  for generated in "$name|public sealed record ${name}Command : ICommand<Guid>;" \
    "$dto_name|public sealed record ${dto_name}Command : ICommand<${dto_name}Result>;" \
    "$dto_name|public sealed record ${dto_name}Result(Guid Id);"; do
    slice="$app_src/Features/$module/${generated%%|*}/${generated%%|*}.cs"
    integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/${generated%%|*}/${generated%%|*}Tests.cs"
    expected="${generated#*|}"
    if ! grep -Fxq "$expected" "$slice" || grep -q 'Validator\|FluentValidation\|command =>' "$slice" ||
      ! grep -Fq "async (IMediator mediator, CancellationToken cancellationToken) =>" "$slice" ||
      ! grep -Fq "mediator.Send(new ${generated%%|*}Command(), cancellationToken)" "$slice"; then
      cat "$slice" >&2
      echo "error: dotnet new hardslice-command --parameters none did not generate an empty command, without validator, sent by an endpoint without body: $expected" >&2
      exit 1
    fi
    if ! grep -Fq "PostAsync(Route, content: null, " "$integration_test" || grep -q 'BadRequest\|Rejects_\|PostAsJsonAsync' "$integration_test"; then
      cat "$integration_test" >&2
      echo "error: the integration test of $module/${generated%%|*} generated with --parameters none does not post without body, or checks a 400." >&2
      exit 1
    fi
  done
  if grep -q 'MyApp\|ModuleName\|CommandName\|ResponseTypeName\|command-route\|module-route' \
    "$app_src/Features/$module/$name/$name.cs" "$app_src/Features/$module/$dto_name/$dto_name.cs" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs" "$app/tests/$app_name.IntegrationTests/Features/$module/$dto_name/${dto_name}Tests.cs"; then
    echo "error: a command without parameters generated in $app_name keeps a placeholder of the template." >&2
    exit 1
  fi

  # HS013 asks no validator of them, HS014 sees their integration tests: only the markers fail.
  echo "==> Checking that $app_name builds the commands without parameters without HardSlice diagnostic and fails the check on their TODO(hardslice): tests only"
  expect_check_fails_on_todo_tests_only "the commands without parameters $module/$name and $module/$dto_name" \
    "$(printf '%s\n' "$app_name.IntegrationTests.Features.$module.$dto_name.${dto_name}Tests.Handles_the_command" \
      "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Handles_the_command" | LC_ALL=C sort)"

  echo "==> Checking that the reference completion of the commands without parameters $module/$name and $module/$dto_name passes the check of $app_name"
  complete_parameterless_command "$module" "$name" id
  complete_parameterless_command "$module" "$dto_name" "${dto_name}Result"
  bash "$app/scripts/check.sh"

  rm -r "$app_src/Features/$module/$name" "$app_src/Features/$module/$dto_name" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$name" "$app/tests/$app_name.IntegrationTests/Features/$module/$dto_name"
  mv "$work/module-parameterless.backup" "$module_file"
}

# The system commands <module>/<name> and <module>/<parameterless-name> generated with --endpoint none, the first with its
# identifier and its validator, the second with --parameters none and the DTO <parameterless-name>Result: commands that implement
# ISystemCommand<TResponse>, without endpoint, registered without the call of Map, with integration tests that send them through
# IMediator. The check fails with HS004 while no type of the module sends them, then passes after their reference completion,
# whose hosted service sends each one. --route and --method are refused with them. The module is left as it was and the slices removed.
probe_system_commands() {
  local module="$1"
  local name="$2"
  local parameterless_name="$3"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local project="src/$app_name/$app_name.csproj"
  local refusal
  local generated
  local slice
  local integration_test
  local check_output

  echo "==> Checking that dotnet new hardslice-command --endpoint none refuses the options of an endpoint in $app_name"
  for refusal in "a system command with a route|--route|versions" "a system command with a method|--method|PUT"; do
    expect_item_refused hardslice-command "${refusal%%|*}" "--endpoint none generates a system command, which only the application sends and which has no endpoint, while --route and --method choose its endpoint" \
      --project "$project" --module "$module" --name "$name" --endpoint none "$(cut -d'|' -f2 <<< "$refusal")" "$(cut -d'|' -f3 <<< "$refusal")"
    if [[ "$item_refused_status" != 105 ]]; then
      echo "error: dotnet new hardslice-command refused ${refusal%%|*} with exit $item_refused_status instead of 105." >&2
      exit 1
    fi
  done
  expect_item_refused hardslice-command "an unknown endpoint" "'grpc' is not a valid value for --endpoint" --project "$project" --module "$module" --name "$name" --endpoint grpc

  echo "==> Generating the system commands $module/$name and $module/$parameterless_name of $app_name with dotnet new hardslice-command --endpoint none"
  cp "$module_file" "$work/module-system.backup"
  generate_command "$module" "$name" --endpoint none
  if [[ "$command_output" == *"Endpoint.Map(group);"* || "$command_output" != *"HS004 fails the build until a type of the module sends it."* ]] ||
    ! grep -Fxq "            .AddScoped<IValidator<${name}Command>, ${name}Validator>()" <<< "$command_output"; then
    printf '%s\n' "$command_output" >&2
    echo "error: dotnet new hardslice-command --endpoint none did not print the registrations without the call of Map, and how HS004 expects the command to be sent." >&2
    exit 1
  fi
  register_slice hardslice-command "$command_output"
  generate_command "$module" "$parameterless_name" --endpoint none --parameters none --response "${parameterless_name}Result"
  register_slice hardslice-command "$command_output"
  for generated in "$name|public sealed record ${name}Command(Guid Id) : ISystemCommand<Guid>;" \
    "$parameterless_name|public sealed record ${parameterless_name}Command : ISystemCommand<${parameterless_name}Result>;"; do
    slice="$app_src/Features/$module/${generated%%|*}/${generated%%|*}.cs"
    integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/${generated%%|*}/${generated%%|*}Tests.cs"
    if ! grep -Fxq "${generated#*|}" "$slice" || grep -q 'Endpoint\|Map[A-Z][a-z]*(\|HttpResults' "$slice"; then
      cat "$slice" >&2
      echo "error: dotnet new hardslice-command --endpoint none did not generate a system command without endpoint: ${generated#*|}" >&2
      exit 1
    fi
    if ! grep -Fq "await scope.ServiceProvider.GetRequiredService<IMediator>().Send(" "$integration_test" || grep -q 'CreateClient\|Route\|HttpStatusCode' "$integration_test"; then
      cat "$integration_test" >&2
      echo "error: the integration test of $module/${generated%%|*} generated with --endpoint none does not send the command through IMediator, or sends an HTTP request." >&2
      exit 1
    fi
  done
  if ! grep -Fq "await Should.ThrowAsync<ValidationException>(" "$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"; then
    echo "error: the integration test of the system command $module/$name does not check that an invalid one throws ValidationException." >&2
    exit 1
  fi
  if grep -q 'MyApp\|ModuleName\|CommandName\|ResponseTypeName\|command-route\|ISystemCommandX' \
    "$app_src/Features/$module/$name/$name.cs" "$app_src/Features/$module/$parameterless_name/$parameterless_name.cs" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs" "$app/tests/$app_name.IntegrationTests/Features/$module/$parameterless_name/${parameterless_name}Tests.cs"; then
    echo "error: a system command generated in $app_name keeps a placeholder of the template." >&2
    exit 1
  fi

  # PB-067: nothing sends them yet, so HS004 fails the build on each, at the end of the compilation.
  echo "==> Checking that HS004 fails the check of $app_name while no type of module $module sends the system commands"
  if check_output="$(run_check check.sh 2>&1)"; then
    echo "error: scripts/check.sh passed with system commands that no type of module $module sends." >&2
    exit 1
  fi
  check_output="$(strip_ansi <<< "$check_output")"
  for generated in "$name" "$parameterless_name"; do
    if [[ "$check_output" != *"HS004: system command '${generated}Command' of slice '$module/$generated' is sent by no type of module '$module'."* ]]; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/check.sh did not report HS004 on the system command $module/$generated that nothing sends." >&2
      exit 1
    fi
  done
  echo "    HS004: ${name}Command and ${parameterless_name}Command are sent by no type of module $module"

  echo "==> Checking that the reference completion of the system commands $module/$name and $module/$parameterless_name, sent by hosted services of their slices, passes the check of $app_name"
  complete_system_command "$module" "$name" id id
  complete_system_command "$module" "$parameterless_name" none "${parameterless_name}Result"
  bash "$app/scripts/check.sh"

  rm -r "$app_src/Features/$module/$name" "$app_src/Features/$module/$parameterless_name" \
    "$app/tests/$app_name.Tests/Features/$module/$name" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$name" "$app/tests/$app_name.IntegrationTests/Features/$module/$parameterless_name"
  mv "$work/module-system.backup" "$module_file"
}

# Seeds a system command of module <module> that nothing sends, then one with an endpoint sent by a type of its slice: the analyzer
# of the web project reports HS004 on each.
probe_system_command_rule() {
  local module="$1"
  local slice="$app_src/Features/$module/RefreshProbes/RefreshProbes.cs"
  mkdir -p "$app_src/Features/$module/RefreshProbes"
  cat > "$slice" <<PROBE
using $app_name.Shared.Messaging;

namespace $app_name.Features.$module.RefreshProbes;

public sealed record RefreshProbesCommand : ISystemCommand<int>;

internal sealed class RefreshProbesHandler : IRequestHandler<RefreshProbesCommand, int>
{
    public Task<int> Handle(RefreshProbesCommand command, CancellationToken cancellationToken) => Task.FromResult(0);
}
PROBE
  expect_failure "HS004: system command 'RefreshProbesCommand' of slice '$module/RefreshProbes' is sent by no type of module '$module'." "docs/rules/HS004.md" "a system command of $app_name that nothing sends" check.sh check.ps1
  cat >> "$slice" <<PROBE

internal sealed class RefreshProbesRunner(IMediator mediator)
{
    public Task<int> RunAsync(CancellationToken cancellationToken) => mediator.Send(new RefreshProbesCommand(), cancellationToken);
}

internal static class RefreshProbesEndpoint
{
    public static void Map(RouteGroupBuilder group) => group.MapPost("/refresh", () => TypedResults.NoContent());
}
PROBE
  expect_failure "HS004: slice '$module/RefreshProbes' of system command 'RefreshProbesCommand' declares endpoint 'RefreshProbesEndpoint'." "docs/rules/HS004.md" "a system command of $app_name with an endpoint" check.sh check.ps1
  rm -r "$app_src/Features/$module/RefreshProbes"
}

# The recurring commands <module>/<name> and <module>/<dto-name> generated with --every, the second with --parameters none
# --endpoint none written out and the DTO <dto-name>Result: system commands without parameter, without validator nor endpoint,
# whose printed registrations hold AddRecurringCommand, with an integration test that runs three cycles on a FakeTimeProvider.
# The check fails on their TODO(hardslice): tests only, then passes after their reference completion, whose second test checks
# that a test that does not keep them never starts them. An invalid interval, --parameters id, --endpoint http and --response none
# are refused with exit 105. The module is left as it was and the slices are removed.
probe_recurring_commands() {
  local module="$1"
  local name="$2"
  local dto_name="$3"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local project="src/$app_name/$app_name.csproj"
  local refusal
  local generated
  local slice
  local integration_test
  local -a parts

  echo "==> Checking that dotnet new hardslice-command --every refuses an invalid interval and the options it contradicts in $app_name"
  for refusal in \
    "an interval of zero|is not an interval of the form [d.]hh:mm:ss|--every|00:00:00" \
    "an interval in seconds|is not an interval of the form [d.]hh:mm:ss|--every|30s" \
    "an interval of 49 days|is not an interval of the form [d.]hh:mm:ss|--every|49.00:00:00" \
    "a recurring command with parameters|--every sends a new command at regular intervals, built without parameter|--every|00:00:30|--parameters|id" \
    "a recurring command with an endpoint|--every sends a new command at regular intervals, built without parameter|--every|00:00:30|--endpoint|http" \
    "a recurring command that answers 404|--response none answers 404 to an unknown aggregate|--every|00:00:30|--response|none"; do
    IFS='|' read -r -a parts <<< "$refusal"
    expect_item_refused hardslice-command "${parts[0]}" "${parts[1]}" --project "$project" --module "$module" --name "$name" "${parts[@]:2}"
    if [[ "$item_refused_status" != 105 ]]; then
      echo "error: dotnet new hardslice-command refused ${parts[0]} with exit $item_refused_status instead of 105." >&2
      exit 1
    fi
  done

  echo "==> Generating the recurring commands $module/$name and $module/$dto_name of $app_name with dotnet new hardslice-command --every"
  cp "$module_file" "$work/module-recurring.backup"
  generate_command "$module" "$name" --every 00:00:30
  if [[ "$command_output" == *"Endpoint.Map(group);"* || "$command_output" == *"IValidator"* ]] ||
    ! grep -Fxq "            .AddScoped<IRequestHandler<${name}Command, Guid>, ${name}Handler>()" <<< "$command_output" ||
    ! grep -Fxq "            .AddRecurringCommand<${name}Command, Guid>(\"$module:$name\", TimeSpan.Parse(\"00:00:30\", CultureInfo.InvariantCulture))" <<< "$command_output"; then
    printf '%s\n' "$command_output" >&2
    echo "error: dotnet new hardslice-command --every did not print the registration of the handler and of AddRecurringCommand, without validator nor call of Map." >&2
    exit 1
  fi
  register_slice hardslice-command "$command_output"
  generate_command "$module" "$dto_name" --every 1.00:00:00 --parameters none --endpoint none --response "${dto_name}Result"
  register_slice hardslice-command "$command_output"
  for generated in "$name|public sealed record ${name}Command : ISystemCommand<Guid>;|RecurringCommandCycles<${name}Command, Guid>" \
    "$dto_name|public sealed record ${dto_name}Command : ISystemCommand<${dto_name}Result>;|RecurringCommandCycles<${dto_name}Command, ${dto_name}Result>"; do
    IFS='|' read -r -a parts <<< "$generated"
    slice="$app_src/Features/$module/${parts[0]}/${parts[0]}.cs"
    integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/${parts[0]}/${parts[0]}Tests.cs"
    if ! grep -Fxq "${parts[1]}" "$slice" || grep -q 'Endpoint\|Map[A-Z][a-z]*(\|Validator' "$slice"; then
      cat "$slice" >&2
      echo "error: dotnet new hardslice-command --every did not generate a system command without parameter, validator nor endpoint: ${parts[1]}" >&2
      exit 1
    fi
    if ! grep -Fq "var cycles = new ${parts[2]}(app);" "$integration_test" || ! grep -Fq "for (var cycle = 1; cycle <= 3; cycle++)" "$integration_test" ||
      grep -q 'CreateClient\|GetRequiredService<IMediator>' "$integration_test"; then
      cat "$integration_test" >&2
      echo "error: the integration test of $module/${parts[0]} generated with --every does not run three cycles with RecurringCommandCycles." >&2
      exit 1
    fi
  done
  if grep -q 'MyApp\|ModuleName\|CommandName\|ResponseTypeName\|RecurringInterval' \
    "$app_src/Features/$module/$name/$name.cs" "$app_src/Features/$module/$dto_name/$dto_name.cs" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs" "$app/tests/$app_name.IntegrationTests/Features/$module/$dto_name/${dto_name}Tests.cs"; then
    echo "error: a recurring command generated in $app_name keeps a placeholder of the template." >&2
    exit 1
  fi

  # AddRecurringCommand sends them, so HS004 is satisfied: only the markers fail, the first one through the NotImplementedException
  # of the handler, thrown by the first cycle.
  echo "==> Checking that $app_name builds the recurring commands without HardSlice diagnostic and fails the check on their TODO(hardslice): tests only"
  expect_check_fails_on_todo_tests_only "the recurring commands $module/$name and $module/$dto_name" \
    "$(printf '%s\n' "$app_name.IntegrationTests.Features.$module.$dto_name.${dto_name}Tests.Runs_a_cycle_at_each_interval" \
      "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Runs_a_cycle_at_each_interval" | LC_ALL=C sort)"

  echo "==> Checking that the reference completion of the recurring commands $module/$name and $module/$dto_name passes the check of $app_name"
  complete_recurring_command "$module" "$name" id
  complete_recurring_command "$module" "$dto_name" "${dto_name}Result"
  # The second one waits a day between its cycles: its test moves the clock by three days, never the real one.
  sed 's/TestInstants\.Start\.AddSeconds(90)/TestInstants.Start.AddDays(3)/' \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$dto_name/${dto_name}Tests.cs" > "$work/test.cs"
  mv "$work/test.cs" "$app/tests/$app_name.IntegrationTests/Features/$module/$dto_name/${dto_name}Tests.cs"
  bash "$app/scripts/check.sh"

  rm -r "$app_src/Features/$module/$name" "$app_src/Features/$module/$dto_name" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$name" "$app/tests/$app_name.IntegrationTests/Features/$module/$dto_name"
  mv "$work/module-recurring.backup" "$module_file"
}

# The DTO that dotnet new hardslice-query names after the slice <name>: the name without its first word, the verb of the use case,
# followed by Dto, such as BookingSummaryDto for GetBookingSummary. A name of one word, or whose first word is a single capital,
# is kept whole.
query_dto() {
  printf '%sDto\n' "$(sed -E 's/^[A-Z][a-z0-9]+([A-Z])/\1/' <<< "$1")"
}

# Generates the query slice <module>/<name> with the remaining options, checks that its two files exist, and the unit tests of
# its validator with --result filtered only, and leaves the printed instructions in $query_output.
generate_query() {
  local module="$1"
  local name="$2"
  shift 2
  local generated
  local unit_test="tests/$app_name.Tests/Features/$module/$name/${name}ValidatorTests.cs"

  if ! query_output="$(new_item hardslice-query --project "src/$app_name/$app_name.csproj" --module "$module" --name "$name" "$@")"; then
    printf '%s\n' "$query_output" >&2
    echo "error: dotnet new hardslice-query failed on the slice $module/$name of $app_name." >&2
    exit 1
  fi
  for generated in "src/$app_name/Features/$module/$name/$name.cs" \
    "tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"; do
    if [[ ! -f "$app/$generated" ]]; then
      printf '%s\n' "$query_output" >&2
      echo "error: dotnet new hardslice-query did not create $generated in $app_name." >&2
      exit 1
    fi
  done
  if [[ " $* " == *" --result filtered "* ]]; then
    if [[ ! -f "$app/$unit_test" ]]; then
      echo "error: dotnet new hardslice-query --result filtered did not create $unit_test in $app_name." >&2
      exit 1
    fi
  elif [[ -e "$app/tests/$app_name.Tests/Features/$module/$name" ]]; then
    echo "error: dotnet new hardslice-query created unit tests for the slice $module/$name of $app_name, which has no validator." >&2
    exit 1
  fi
}

# The reference completion of the integration tests <test-file>... of query slices generated by dotnet new hardslice-query: each
# stores an aggregate of type <aggregate> created by <create> and asserts the identifier it reads.
complete_query_tests() {
  local aggregate="$1"
  local create="$2"
  shift 2
  local test_file

  for test_file in "$@"; do
    CREATE="$create" AGGREGATE="$aggregate" awk '
      /^        Assert\.Fail\("TODO\(hardslice\): / { next }
      /^        throw new NotImplementedException\($/ { skipping = 1; next }
      skipping {
        if ($0 ~ /and return its identifier\."\);$/) {
          print "        var stored = " ENVIRON["CREATE"] ";"
          print "        db.Set<" ENVIRON["AGGREGATE"] ">().Add(stored);"
          print "        await db.SaveChangesAsync(TestContext.Current.CancellationToken);"
          print "        return stored.Id.Value;"
          skipping = 0
        }
        next
      }
      { print }' "$test_file" > "$work/test.cs"
    mv "$work/test.cs" "$test_file"
  done
}

# Generates a query slice of module <module>, which must exist, reading <aggregate>, an aggregate of the domain of the module
# whose identifier is an <aggregate>Id, through every path of dotnet new hardslice-query: refusals, instructions, a forgotten
# registration, the TODO(hardslice): tests of the three results (<name> single, <many-name> many, <filtered-name> filtered by a
# date, whose request without date is answered 400), an unknown aggregate, then the reference completion, in which <create> is
# the expression that creates an aggregate to store. The module file is restored and the slices removed at the end.
probe_query_template() {
  local module="$1"
  local name="$2"
  local many_name="$3"
  local filtered_name="$4"
  local aggregate="$5"
  local create="$6"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local project="src/$app_name/$app_name.csproj"
  local slice_name
  local test_output
  local build_output
  local test_file
  local refusal
  local -a fields
  local dto="$(query_dto "$name")"
  local many_dto="$(query_dto "$many_name")"
  local filtered_dto="$(query_dto "$filtered_name")"
  local filtered_slice="$app_src/Features/$module/$filtered_name/$filtered_name.cs"

  cp "$module_file" "$work/module.backup"

  echo "==> Checking that dotnet new hardslice-query refuses an unreadable application, an invalid name, module or aggregate, and a missing module or aggregate in $app_name"
  expect_item_refused hardslice-query "a run without the web project" "with --project src/<App>/<App>.csproj" --module "$module" --name "$name" --aggregate "$aggregate"
  expect_item_refused hardslice-query "a run without name" "--name is required" --project "$project" --module "$module" --aggregate "$aggregate"
  expect_item_refused hardslice-query "a name that is not PascalCase" "--name 'list-slots' is not PascalCase" --project "$project" --module "$module" --name list-slots --aggregate "$aggregate"
  expect_item_refused hardslice-query "a name with the Query suffix" "--name '${name}Query' ends with Query" --project "$project" --module "$module" --name "${name}Query" --aggregate "$aggregate"
  expect_item_refused hardslice-query "a module that is not PascalCase" "--module 'payments' is not PascalCase" --project "$project" --module payments --name "$name" --aggregate "$aggregate"
  expect_item_refused hardslice-query "a module that does not exist" "module 'Payments' does not exist: src/$app_name/Features/Payments/PaymentsModule.cs was not found. Create the module first with dotnet new hardslice-module --project src/$app_name/$app_name.csproj --name Payments" --project "$project" --module Payments --name "$name" --aggregate "$aggregate"
  expect_item_refused hardslice-query "a run without aggregate" "Missing mandatory option(s) for the template 'HardSlice query slice': '--aggregate'." --project "$project" --module "$module" --name "$name"
  expect_item_refused hardslice-query "an aggregate that is not PascalCase" "--aggregate 'room-slot' is not PascalCase" --project "$project" --module "$module" --name "$name" --aggregate room-slot
  # 'list' is not a value: dotnet new would read '--result list' as its list command.
  expect_item_refused hardslice-query "an unknown result" "'list' is not a valid value for --result" --project "$project" --module "$module" --name "$name" --aggregate "$aggregate" --result=list

  echo "==> Checking that dotnet new hardslice-query refuses a filtered result without its filter, a filter without filtered result, and a filter it cannot generate in $app_name"
  for refusal in "a filtered result without --parameter|--result filtered needs --parameter <Name>:<Type>|--result|filtered" \
    "a filter of a single result|--parameter 'Date:DateOnly' is the filter of --result filtered|--parameter|Date:DateOnly" \
    "a filter of a list of every aggregate|--parameter 'Date:DateOnly' is the filter of --result filtered|--result|many|--parameter|Date:DateOnly" \
    "a filter without type|--parameter 'Date' is not <Name>:<Type>|--result|filtered|--parameter|Date" \
    "a filter whose name is not PascalCase|--parameter 'date:DateOnly' is not <Name>:<Type>|--result|filtered|--parameter|date:DateOnly" \
    "a filter of a type it does not generate|--parameter 'Date:DateTime' has the type 'DateTime', which is not one of string, bool, int, long, decimal, double, Guid, DateOnly, TimeOnly and DateTimeOffset|--result|filtered|--parameter|Date:DateTime"; do
    IFS='|' read -r -a fields <<< "$refusal"
    expect_item_refused hardslice-query "${fields[0]}" "${fields[1]}" --project "$project" --module "$module" --name "$filtered_name" --aggregate "$aggregate" "${fields[@]:2}"
    if [[ "$item_refused_status" != 105 ]]; then
      echo "error: dotnet new hardslice-query refused ${fields[0]} with exit $item_refused_status instead of 105." >&2
      exit 1
    fi
  done

  echo "==> Generating the query slice $module/$name of $app_name with dotnet new hardslice-query"
  generate_query "$module" "$name" --aggregate "$aggregate"
  for expected in "Register the slice in src/$app_name/Features/$module/${module}Module.cs:" \
    "using $app_name.Features.$module.$name;" \
    "            .AddScoped<IRequestHandler<${name}Query, ${dto}?>, ${name}Handler>()" \
    "        ${name}Endpoint.Map(group);"; do
    if ! grep -Fxq -- "$expected" <<< "${query_output//Manual instructions: /}"; then
      printf '%s\n' "$query_output" >&2
      echo "error: dotnet new hardslice-query did not print the line '$expected'." >&2
      exit 1
    fi
  done
  if ! grep -Fxq "public sealed record $dto(Guid Id);" "$app_src/Features/$module/$name/$name.cs"; then
    echo "error: dotnet new hardslice-query did not name the DTO of $module/$name $dto, the name of the slice without its verb." >&2
    exit 1
  fi
  if grep -q 'MyApp\|ModuleName\|QueryName\|AggregateName\|ResultDtoName\|query-route\|module-route' \
    "$app_src/Features/$module/$name/$name.cs" "$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"; then
    echo "error: the slice $module/$name generated in $app_name keeps a placeholder of the template." >&2
    exit 1
  fi
  if ! grep -Fq "db.Set<$aggregate>()" "$app_src/Features/$module/$name/$name.cs" ||
    ! grep -Fxq "            .AsNoTracking()" "$app_src/Features/$module/$name/$name.cs"; then
    echo "error: the handler of $module/$name does not read $aggregate with AsNoTracking()." >&2
    exit 1
  fi
  expect_item_refused hardslice-query "a slice that exists" "--force" --project "$project" --module "$module" --name "$name" --aggregate "$aggregate"

  # The endpoint is mapped, so the build passes: only the test of the host and the generated tests see the forgotten registration.
  echo "==> Checking that the test of the host names the handler of $module/$name that ${module}Module does not register"
  register_slice hardslice-query "$query_output" without-registrations
  if test_output="$(
    cd "$app"
    dotnet build "$app_name.slnx" 2>&1 &&
      dotnet test --project "tests/$app_name.IntegrationTests/$app_name.IntegrationTests.csproj" --no-build 2>&1
  )"; then
    echo "error: the integration tests of $app_name passed with a query slice its module does not register." >&2
    exit 1
  fi
  test_output="$(strip_ansi <<< "$test_output")"
  for expected in "failed $app_name.IntegrationTests.ProgramTests.Resolves_every_handler_and_validator_of_the_application_by_its_contract" \
    "'${name}Handler' as 'IRequestHandler<${name}Query, ${dto}>'" \
    "failed $app_name.IntegrationTests.Features.$module.$name.${name}Tests.Returns_404_for_an_unknown_aggregate"; do
    if [[ "$test_output" != *"$expected"* ]]; then
      printf '%s\n' "$test_output" >&2
      echo "error: the integration tests of $app_name did not report '$expected' for the unregistered slice $module/$name." >&2
      exit 1
    fi
  done
  echo "    ProgramTests names '${name}Handler'"
  cp "$work/module.backup" "$module_file"

  # The three results at once: each fails the check on its TODO(hardslice): test only, the 404 of the single result passes, and
  # so do the validator tests of the filtered result and its 400 on a request without date.
  echo "==> Checking that $app_name builds the registered query slices $module/$name, $module/$many_name (--result many) and $module/$filtered_name (--result filtered --parameter Date:DateOnly) without HardSlice diagnostic, HS006 included, and fails the check on their TODO(hardslice): tests only"
  register_slice hardslice-query "$query_output"
  generate_query "$module" "$many_name" --aggregate "$aggregate" --result many
  if ! grep -Fxq "            .AddScoped<IRequestHandler<${many_name}Query, IReadOnlyList<${many_dto}>>, ${many_name}Handler>()" <<< "${query_output//Manual instructions: /}"; then
    printf '%s\n' "$query_output" >&2
    echo "error: dotnet new hardslice-query --result many did not register a handler that returns a list." >&2
    exit 1
  fi
  register_slice hardslice-query "$query_output"
  generate_query "$module" "$filtered_name" --aggregate "$aggregate" --result filtered --parameter Date:DateOnly
  for expected in "using FluentValidation;" \
    "            .AddScoped<IRequestHandler<${filtered_name}Query, IReadOnlyList<${filtered_dto}>>, ${filtered_name}Handler>()" \
    "            .AddScoped<IValidator<${filtered_name}Query>, ${filtered_name}Validator>()"; do
    if ! grep -Fxq -- "$expected" <<< "${query_output//Manual instructions: /}"; then
      printf '%s\n' "$query_output" >&2
      echo "error: dotnet new hardslice-query --result filtered did not print the line '$expected'." >&2
      exit 1
    fi
  done
  for expected in "public sealed record ${filtered_name}Query(DateOnly? Date) : IQuery<IReadOnlyList<${filtered_dto}>>;" \
    "public sealed record ${filtered_dto}(Guid Id);" \
    "        RuleFor(query => query.Date).NotNull();" \
    "        group.MapGet(\"/$(sed 's/\([a-z0-9]\)\([A-Z]\)/\1-\2/g' <<< "$filtered_name" | tr '[:upper:]' '[:lower:]')\", async ([AsParameters] ${filtered_name}Query query, IMediator mediator, CancellationToken cancellationToken) =>"; do
    if ! grep -Fxq -- "$expected" "$filtered_slice"; then
      cat "$filtered_slice" >&2
      echo "error: the slice $module/$filtered_name does not hold the line: $expected" >&2
      exit 1
    fi
  done
  if ! grep -Fxq "public sealed record $many_dto(Guid Id);" "$app_src/Features/$module/$many_name/$many_name.cs" ||
    ! grep -Fq '?date=2026-11-04"' "$app/tests/$app_name.IntegrationTests/Features/$module/$filtered_name/${filtered_name}Tests.cs" ||
    ! grep -Fxq '        problem.Errors.Keys.ShouldBe(["date"]);' "$app/tests/$app_name.IntegrationTests/Features/$module/$filtered_name/${filtered_name}Tests.cs"; then
    echo "error: dotnet new hardslice-query did not name the DTO of $module/$many_name $many_dto, or the tests of $module/$filtered_name do not read ?date=." >&2
    exit 1
  fi
  register_slice hardslice-query "$query_output"
  expect_check_fails_on_todo_tests_only "the generated query slices $module/$name, $module/$many_name and $module/$filtered_name" \
    "$(printf '%s\n' "$app_name.IntegrationTests.Features.$module.$many_name.${many_name}Tests.Lists_a_stored_aggregate_as_its_dto" \
      "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Reads_a_stored_aggregate_as_its_dto" \
      "$app_name.IntegrationTests.Features.$module.$filtered_name.${filtered_name}Tests.Lists_the_stored_aggregates_that_its_filter_selects" | LC_ALL=C sort)"
  cp "$module_file" "$work/module-registered.backup"

  # A registered slice, so that HS015 has nothing to report: the compiler alone names the aggregate it does not find.
  echo "==> Checking that an unknown aggregate fails the build of $app_name with a compiler error that names it, without HardSlice diagnostic"
  for result in single many; do
    slice_name="Read${result^}Unknown${aggregate}"
    generate_query "$module" "$slice_name" --aggregate "Unknown$aggregate" --result "$result"
    register_slice hardslice-query "$query_output"
  done
  if build_output="$(cd "$app" && dotnet build "$app_name.slnx" 2>&1)"; then
    echo "error: $app_name builds query slices that read the unknown aggregate Unknown$aggregate." >&2
    exit 1
  fi
  if ! grep -Eq "Features/$module/ReadSingleUnknown$aggregate/ReadSingleUnknown$aggregate\.cs\([0-9]+,[0-9]+\): error CS0246: The type or namespace name 'Unknown$aggregate' could not be found" <<< "$build_output" ||
    ! grep -Eq "Features/$module/ReadManyUnknown$aggregate/ReadManyUnknown$aggregate\.cs\([0-9]+,[0-9]+\): error CS0246: The type or namespace name 'Unknown$aggregate' could not be found" <<< "$build_output" ||
    grep -Eq '(error|warning) HS[0-9]{3}' <<< "$build_output"; then
    printf '%s\n' "$build_output" >&2
    echo "error: the build of $app_name did not name the unknown aggregate Unknown$aggregate with CS0246 alone." >&2
    exit 1
  fi
  echo "    error CS0246: The type or namespace name 'Unknown$aggregate' could not be found"
  for result in Single Many; do
    rm -r "$app_src/Features/$module/Read${result}Unknown$aggregate" "$app/tests/$app_name.IntegrationTests/Features/$module/Read${result}Unknown$aggregate"
  done
  cp "$work/module-registered.backup" "$module_file"

  # The reference completion: each test stores an aggregate created by <create> and asserts the identifier it reads, and the
  # filtered handler keeps every aggregate, the stored one included. No marker remains.
  echo "==> Checking that the reference completion of $module/$name, $module/$many_name and $module/$filtered_name passes the check of $app_name"
  complete_query_tests "$aggregate" "$create" "$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$many_name/${many_name}Tests.cs" \
    "$app/tests/$app_name.IntegrationTests/Features/$module/$filtered_name/${filtered_name}Tests.cs"
  sed '/^            \/\/ TODO(hardslice): keep the /d' "$filtered_slice" > "$work/slice.cs"
  mv "$work/slice.cs" "$filtered_slice"
  if grep -rl 'TODO(hardslice):' "$app/src" "$app/tests" --include='*.cs'; then
    echo "error: the reference completion of $module/$name, $module/$many_name and $module/$filtered_name leaves TODO(hardslice): markers." >&2
    exit 1
  fi
  bash "$app/scripts/check.sh"

  for slice_name in "$name" "$many_name" "$filtered_name"; do
    rm -r "$app_src/Features/$module/$slice_name" "$app/tests/$app_name.IntegrationTests/Features/$module/$slice_name"
  done
  rm -r "$app/tests/$app_name.Tests/Features/$module/${filtered_name:?}"
  mv "$work/module.backup" "$module_file"
  rm "$work/module-registered.backup"
}

# PB-065: the slices choose their route under the group of their module and their method. Generates the module <module> with
# --route <group-route>, registered in Program.cs, with the command <name> (--route versions --method PUT) and the command
# <delete-name> (--method DELETE, which reads its Id from its route), and in module <query-module>, the query <query-name> of
# <aggregate> (--route trips). Before, each route or method that a slice could not map is refused with exit 105. The check fails
# on their TODO(hardslice): tests only, the 400 of the PUT and of the DELETE and the 404 of the query passing on their routes, then
# passes after their reference completion, in which <create> creates the aggregate to store. Everything is removed at the end.
probe_slice_routes() {
  local module="$1"
  local group_route="$2"
  local name="$3"
  local delete_name="$4"
  local query_module="$5"
  local query_name="$6"
  local aggregate="$7"
  local create="$8"
  local project="src/$app_name/$app_name.csproj"
  local query_module_file="$app_src/Features/$query_module/${query_module}Module.cs"
  local delete_route
  local -a fields
  local refusal
  local generated
  local expected

  cp "$app_src/Program.cs" "$work/Program-routes.cs.backup"
  cp "$query_module_file" "$work/query-module-routes.backup"
  delete_route="$(sed 's/\([a-z0-9]\)\([A-Z]\)/\1-\2/g' <<< "$delete_name" | tr '[:upper:]' '[:lower:]')"

  echo "==> Generating the module $module of $app_name under /$group_route with dotnet new hardslice-module --route"
  generate_module "$module" --route "$group_route"
  expect_group_route "$module" "/$group_route"
  register_module "$module_output"

  echo "==> Checking that dotnet new hardslice-command and hardslice-query refuse a route or a method that their slice cannot map in $app_name"
  for refusal in "hardslice-command|a route that is not made of words and route parameters|--route '/Schedule Versions' is not a route of lowercase words separated by hyphens, and route parameters between braces|--route|Schedule Versions" \
    "hardslice-command|a route that names the id that a DELETE appends|--route '/{id}/history' holds a parameter id, which --method DELETE appends to it as /{id:guid}|--route|{id}/history|--method|DELETE" \
    "hardslice-command|the method of a query|--method GET only reads, while a command changes state|--method|get" \
    "hardslice-command|an unknown method|--method 'FETCH' is not POST, PUT, PATCH or DELETE|--method|FETCH" \
    "hardslice-query|a route that ends with a slash|--route '/trips/' is not a route of lowercase words separated by hyphens, and route parameters between braces|--route|trips/|--aggregate|$aggregate" \
    "hardslice-query|a route that names the id that a single result appends|--route '/{id:guid}' holds a parameter id, which --result single appends to it as /{id:guid}|--route|{id:guid}|--aggregate|$aggregate" \
    "hardslice-query|the method of a command|--method 'POST' is not GET: a query only reads and answers GET|--method|post|--aggregate|$aggregate"; do
    IFS='|' read -r -a fields <<< "$refusal"
    if [[ "${fields[0]}" == hardslice-command ]]; then
      expect_item_refused "${fields[0]}" "${fields[1]}" "${fields[2]}" --project "$project" --module "$module" --name "$name" "${fields[@]:3}"
    else
      expect_item_refused "${fields[0]}" "${fields[1]}" "${fields[2]}" --project "$project" --module "$query_module" --name "$query_name" "${fields[@]:3}"
    fi
    if [[ "$item_refused_status" != 105 ]]; then
      echo "error: dotnet new ${fields[0]} refused ${fields[1]} with exit $item_refused_status instead of 105." >&2
      exit 1
    fi
  done

  echo "==> Generating $module/$name (--route versions --method PUT), $module/$delete_name (--method DELETE) and $query_module/$query_name (--route trips) in $app_name"
  generate_command "$module" "$name" --route versions --method PUT
  register_slice hardslice-command "$command_output"
  generate_command "$module" "$delete_name" --method delete
  register_slice hardslice-command "$command_output"
  generate_query "$query_module" "$query_name" --aggregate "$aggregate" --route trips
  register_slice hardslice-query "$query_output"
  for generated in "src/$app_name/Features/$module/$name/$name.cs|        group.MapPut(\"/versions\", async (${name}Command command, IMediator mediator, CancellationToken cancellationToken) =>" \
    "tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs|    private const string Route = ${module}Module.Route + \"/versions\";" \
    "tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs|        var response = await app.CreateClient().PutAsJsonAsync(Route, command, TestContext.Current.CancellationToken);" \
    "src/$app_name/Features/$module/$delete_name/$delete_name.cs|        group.MapDelete(\"/$delete_route/{id:guid}\", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>" \
    "src/$app_name/Features/$module/$delete_name/$delete_name.cs|            TypedResults.Ok(await mediator.Send(new ${delete_name}Command(id), cancellationToken)));" \
    "tests/$app_name.IntegrationTests/Features/$module/$delete_name/${delete_name}Tests.cs|    private const string Route = ${module}Module.Route + \"/$delete_route\";" \
    "tests/$app_name.IntegrationTests/Features/$module/$delete_name/${delete_name}Tests.cs|        var response = await app.CreateClient().DeleteAsync(\$\"{Route}/{command.Id}\", TestContext.Current.CancellationToken);" \
    "tests/$app_name.IntegrationTests/Features/$module/$delete_name/${delete_name}Tests.cs|        var response = await app.CreateClient().DeleteAsync(\$\"{Route}/{Guid.Empty}\", TestContext.Current.CancellationToken);" \
    "src/$app_name/Features/$query_module/$query_name/$query_name.cs|        group.MapGet(\"/trips/{id:guid}\", async Task<Results<Ok<$(query_dto "$query_name")>, NotFound>> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>" \
    "tests/$app_name.IntegrationTests/Features/$query_module/$query_name/${query_name}Tests.cs|    private const string Route = ${query_module}Module.Route + \"/trips\";"; do
    expected="${generated#*|}"
    if ! grep -Fxq -- "$expected" "$app/${generated%%|*}"; then
      cat "$app/${generated%%|*}" >&2
      echo "error: ${generated%%|*} does not hold the line, for its route or its method: $expected" >&2
      exit 1
    fi
  done
  echo "    PUT /$group_route/versions, DELETE /$group_route/$delete_route/{id:guid}, GET trips/{id:guid} of $query_module"

  # The tests of the 400 of the PUT and of the DELETE, and of the 404 of the query, reach their endpoints on the chosen routes.
  echo "==> Checking that $app_name builds the slices on their routes without HardSlice diagnostic and fails the check on their TODO(hardslice): tests only"
  expect_check_fails_on_todo_tests_only "the slices $module/$name, $module/$delete_name and $query_module/$query_name" \
    "$(printf '%s\n' "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Handles_a_valid_command" \
      "$app_name.IntegrationTests.Features.$module.$delete_name.${delete_name}Tests.Handles_a_valid_command" \
      "$app_name.IntegrationTests.Features.$query_module.$query_name.${query_name}Tests.Reads_a_stored_aggregate_as_its_dto" | LC_ALL=C sort)"

  echo "==> Checking that the reference completion of the slices on their routes passes the check of $app_name"
  complete_command "$module" "$name"
  complete_command "$module" "$delete_name"
  complete_query_tests "$aggregate" "$create" "$app/tests/$app_name.IntegrationTests/Features/$query_module/$query_name/${query_name}Tests.cs"
  if grep -rl 'TODO(hardslice):' "$app/src" "$app/tests" --include='*.cs'; then
    echo "error: the reference completion of the slices on their routes leaves TODO(hardslice): markers." >&2
    exit 1
  fi
  bash "$app/scripts/check.sh"

  rm -r "$app_src/Features/$query_module/$query_name" "$app/tests/$app_name.IntegrationTests/Features/$query_module/$query_name"
  remove_module "$module"
  mv "$work/query-module-routes.backup" "$query_module_file"
  mv "$work/Program-routes.cs.backup" "$app_src/Program.cs"
}

# PB-081: --require <policy> protects the endpoint of a slice. In module <module>, the command <name>, the command without parameters
# <parameterless-name> and the query <query-name> of <aggregate> are generated with --require ProbeKey, the policy of the key scheme
# that write_probe_key_scheme writes in Shared/Http: each endpoint chains .RequireAuthorization("ProbeKey"), each test sends the
# client of app.CreateAuthorizedClient("ProbeKey") but the test of the 401, which sends none. Before, --require is refused
# with exit 105 with --endpoint none, --every and a name that the code could not hold. While AppFactory has no client for the policy,
# the tests that send it fail with the message that says where to add it, and the tests of the 401 pass. Once the client is in
# AppFactory, the check fails on the TODO(hardslice): tests only, then passes after the reference completion, in which <create>
# creates the aggregate to store, with a test that sends the client from an instance derived with WithWebHostBuilder. Program.cs, AppFactory and the module are restored and everything else removed at the end.
probe_protected_endpoints() {
  local module="$1"
  local name="$2"
  local parameterless_name="$3"
  local query_name="$4"
  local aggregate="$5"
  local create="$6"
  local project="src/$app_name/$app_name.csproj"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local app_factory="$app/tests/$app_name.IntegrationTests/AppFactory.cs"
  local tests="$app/tests/$app_name.IntegrationTests/Features/$module"
  local missing_client="AppFactory has no client for the authorization policy 'ProbeKey'. Add one to AuthorizedClients, in AppFactory.cs of the integration tests"
  local refusal
  local slice
  local check_output
  local failures
  local expected_failures
  local -a fields

  cp "$app_src/Program.cs" "$work/program.backup"
  cp "$module_file" "$work/module-protected.backup"
  cp "$app_factory" "$work/app-factory.backup"

  echo "==> Checking that dotnet new hardslice-command and hardslice-query refuse --require without an endpoint to protect, or with a name the code could not hold, in $app_name"
  for refusal in "hardslice-command|a policy for a system command|--require protects the endpoint of the slice with an authorization policy, while --endpoint none and --every generate a system command|--require|ProbeKey|--endpoint|none" \
    "hardslice-command|a policy for a recurring command|--require protects the endpoint of the slice with an authorization policy, while --endpoint none and --every generate a system command|--require|ProbeKey|--every|00:00:30" \
    "hardslice-command|a policy name with a space|--require 'probe key' is not the name of an authorization policy that the template can write|--require|probe key" \
    "hardslice-query|a policy name with a quote|--require 'probe\"key' is not the name of an authorization policy that the template can write|--require|probe\"key|--aggregate|$aggregate"; do
    IFS='|' read -r -a fields <<< "$refusal"
    if [[ "${fields[0]}" == hardslice-command ]]; then
      expect_item_refused "${fields[0]}" "${fields[1]}" "${fields[2]}" --project "$project" --module "$module" --name "$name" "${fields[@]:3}"
    else
      expect_item_refused "${fields[0]}" "${fields[1]}" "${fields[2]}" --project "$project" --module "$module" --name "$query_name" "${fields[@]:3}"
    fi
    if [[ "$item_refused_status" != 105 ]]; then
      echo "error: dotnet new ${fields[0]} refused ${fields[1]} with exit $item_refused_status instead of 105." >&2
      exit 1
    fi
  done

  echo "==> Generating $module/$name, $module/$parameterless_name (--parameters none) and $module/$query_name with --require ProbeKey in $app_name, whose key scheme is in Shared/Http"
  write_probe_key_scheme Http
  generate_command "$module" "$name" --route protections --require ProbeKey
  register_slice hardslice-command "$command_output"
  generate_command "$module" "$parameterless_name" --route protected-imports --parameters none --require ProbeKey
  register_slice hardslice-command "$command_output"
  generate_query "$module" "$query_name" --aggregate "$aggregate" --route protected-readings --require ProbeKey
  register_slice hardslice-query "$query_output"
  for slice in "$name|$command_output" "$parameterless_name|$command_output" "$query_name|$query_output"; do
    if [[ "${slice#*|}" != *"The endpoint requires the authorization policy ProbeKey, which the application registers in src/$app_name/Shared/Http/ with its authentication scheme (docs/rules/HS015.md). Its integration tests send their requests through app.CreateAuthorizedClient(\"ProbeKey\"), which fails until AuthorizedClients, in tests/$app_name.IntegrationTests/AppFactory.cs, holds the client of that policy"* ]]; then
      printf '%s\n' "${slice#*|}" >&2
      echo "error: dotnet new did not say what the policy ProbeKey of $module/${slice%%|*} asks of AppFactory." >&2
      exit 1
    fi
    slice="${slice%%|*}"
    if ! grep -Fxq '            .RequireAuthorization("ProbeKey");' "$app_src/Features/$module/$slice/$slice.cs"; then
      cat "$app_src/Features/$module/$slice/$slice.cs" >&2
      echo "error: the endpoint of $module/$slice generated with --require ProbeKey does not chain .RequireAuthorization(\"ProbeKey\")." >&2
      exit 1
    fi
    if ! grep -Fq 'app.CreateAuthorizedClient("ProbeKey").' "$tests/$slice/${slice}Tests.cs" || grep -Fq 'app.CreateClient().' "$tests/$slice/${slice}Tests.cs" ||
      ! grep -Fxq '    public async Task Rejects_a_request_without_authentication_before_its_handler()' "$tests/$slice/${slice}Tests.cs" ||
      ! grep -Fxq '        var anonymous = app.CreateClient();' "$tests/$slice/${slice}Tests.cs"; then
      cat "$tests/$slice/${slice}Tests.cs" >&2
      echo "error: the tests of $module/$slice generated with --require ProbeKey do not send the client of the policy, or have no test of the 401 without it." >&2
      exit 1
    fi
  done
  echo "    .RequireAuthorization(\"ProbeKey\"), CreateAuthorizedClient(\"ProbeKey\") and the test of the 401"

  # AppFactory as generated: no client for ProbeKey. The tests that send it fail with its message, the tests of the 401 pass.
  echo "==> Checking that the tests that send the client of ProbeKey fail with the message of AppFactory while it has none, and that the tests of the 401 pass"
  if check_output="$(run_check check.sh 2>&1)"; then
    echo "error: scripts/check.sh passed with protected slices whose policy has no client in AppFactory." >&2
    exit 1
  fi
  check_output="$(strip_ansi <<< "$check_output")"
  failures="$(sed -n 's/^failed \([^ ]*\) (.*/\1/p' <<< "$check_output" | LC_ALL=C sort -u)"
  expected_failures="$(printf '%s\n' "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Handles_a_valid_command" \
    "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Rejects_an_invalid_command_with_a_validation_problem_before_its_handler" \
    "$app_name.IntegrationTests.Features.$module.$parameterless_name.${parameterless_name}Tests.Handles_the_command" \
    "$app_name.IntegrationTests.Features.$module.$query_name.${query_name}Tests.Reads_a_stored_aggregate_as_its_dto" \
    "$app_name.IntegrationTests.Features.$module.$query_name.${query_name}Tests.Returns_404_for_an_unknown_aggregate" | LC_ALL=C sort)"
  if [[ "$failures" != "$expected_failures" || "$check_output" != *"$missing_client"* ]] || grep -Eq '(error|warning) HS[0-9]{3}' <<< "$check_output"; then
    printf '%s\n' "$check_output" >&2
    echo "error: scripts/check.sh did not fail on the tests that send the client of ProbeKey alone (${expected_failures//$'\n'/, }), with: $missing_client" >&2
    exit 1
  fi
  echo "    ${failures//$'\n'/, }: $missing_client"

  # The client of the policy, added to AppFactory as the application does: the header that the key scheme reads.
  echo "==> Checking that, with the client of ProbeKey in AppFactory, $app_name fails the check on the TODO(hardslice): tests of the protected slices only"
  if ! { printf 'using %s.Shared.Http;\n' "$app_name"; awk '
      /^    internal static readonly Dictionary<string, Action<HttpClient>> AuthorizedClients = / { declared = 1 }
      { print }
      declared && /^    \{$/ && !added { print "        [ProbeKeyAuthentication.Policy] = client => client.DefaultRequestHeaders.Add(\"X-Probe-Key\", \"probe\"),"; added = 1 }
      END { if (!added) exit 1 }' "$work/app-factory.backup"; } > "$app_factory"; then
    echo "error: the AppFactory of $app_name has no AuthorizedClients to hold the client of ProbeKey." >&2
    exit 1
  fi
  expect_check_fails_on_todo_tests_only "the protected slices $module/$name, $module/$parameterless_name and $module/$query_name" \
    "$(printf '%s\n' "$app_name.IntegrationTests.Features.$module.$name.${name}Tests.Handles_a_valid_command" \
      "$app_name.IntegrationTests.Features.$module.$parameterless_name.${parameterless_name}Tests.Handles_the_command" \
      "$app_name.IntegrationTests.Features.$module.$query_name.${query_name}Tests.Reads_a_stored_aggregate_as_its_dto" | LC_ALL=C sort)"

  echo "==> Checking that the reference completion of the protected slices passes the check of $app_name"
  complete_command "$module" "$name"
  complete_parameterless_command "$module" "$parameterless_name" id
  complete_query_tests "$aggregate" "$create" "$tests/$query_name/${query_name}Tests.cs"
  # The client of the policy also serves an instance derived with WithWebHostBuilder, as tests that replace a service build them.
  if ! awk '
      { lines[NR] = $0 }
      END {
        for (i = 1; i < NR; i++) print lines[i]
        print ""
        print "    [Fact]"
        print "    public async Task Handles_the_command_on_an_instance_derived_with_WithWebHostBuilder()"
        print "    {"
        print "        await using var app = new AppFactory(database);"
        print "        await using var derived = app.WithWebHostBuilder(_ => { });"
        print ""
        print "        var response = await derived.CreateAuthorizedClient(\"ProbeKey\").PostAsync(Route, content: null, TestContext.Current.CancellationToken);"
        print ""
        print "        response.StatusCode.ShouldBe(HttpStatusCode.OK);"
        print "    }"
        print lines[NR]
        if (lines[NR] != "}") exit 1
      }' "$tests/$parameterless_name/${parameterless_name}Tests.cs" > "$work/test.cs"; then
    echo "error: the integration test of $module/$parameterless_name does not end with the closing brace of its class." >&2
    exit 1
  fi
  mv "$work/test.cs" "$tests/$parameterless_name/${parameterless_name}Tests.cs"
  if grep -rl 'TODO(hardslice):' "$app/src" "$app/tests" --include='*.cs'; then
    echo "error: the reference completion of the protected slices leaves TODO(hardslice): markers." >&2
    exit 1
  fi
  bash "$app/scripts/check.sh"

  for slice in "$name" "$parameterless_name" "$query_name"; do
    rm -r "$app_src/Features/$module/${slice:?}" "$tests/${slice:?}"
  done
  rm -r "$app/tests/$app_name.Tests/Features/$module/${name:?}" "$app_src/Shared/Http/ProbeKeyAuthentication.cs"
  mv "$work/module-protected.backup" "$module_file"
  mv "$work/app-factory.backup" "$app_factory"
  mv "$work/program.backup" "$app_src/Program.cs"
}

# The files that dotnet new hardslice-aggregate creates for the aggregate <name> of module <module>: its root, its identifier,
# its mapping in the module and its unit tests.
aggregate_files() {
  local module="$1"
  local name="$2"
  printf '%s\n' "src/$app_name.Domain/$module/$name.cs" \
    "src/$app_name.Domain/$module/${name}Id.cs" \
    "src/$app_name/Features/$module/${name}Configuration.cs" \
    "tests/$app_name.Tests/Domain/$module/${name}Tests.cs"
}

# Generates the aggregate <module>/<name> with the remaining options, checks that its files exist, and leaves the printed
# instructions in $aggregate_output.
generate_aggregate() {
  local module="$1"
  local name="$2"
  shift 2
  local generated

  if ! aggregate_output="$(new_item hardslice-aggregate --project "src/$app_name/$app_name.csproj" --module "$module" --name "$name" "$@")"; then
    printf '%s\n' "$aggregate_output" >&2
    echo "error: dotnet new hardslice-aggregate failed on the aggregate $module/$name of $app_name." >&2
    exit 1
  fi
  while read -r generated; do
    if [[ ! -f "$app/$generated" ]]; then
      printf '%s\n' "$aggregate_output" >&2
      echo "error: dotnet new hardslice-aggregate did not create $generated in $app_name." >&2
      exit 1
    fi
  done < <(aggregate_files "$module" "$name")
}

# Generates the aggregate <name> of module <module>, which must exist, mapped to the table <table>, through every path of
# dotnet new hardslice-aggregate: refusals, the printed migration, a build without HardSlice diagnostic, the TODO(hardslice):
# test, then the missing migration, which the check detects, and the reference completion. The remaining options are passed to
# the generation. With <keep> 'keep', the aggregate and its migration stay for the query slices; with 'remove', both are removed.
probe_aggregate_template() {
  local module="$1"
  local name="$2"
  local table="$3"
  local keep="$4"
  shift 4
  local project="src/$app_name/$app_name.csproj"
  local migrations="$app_src/Shared/Persistence/Migrations"
  local unit_test="$app/tests/$app_name.Tests/Domain/$module/${name}Tests.cs"
  local migration
  local build_output
  local check_output
  local failures
  local generated

  echo "==> Checking that dotnet new hardslice-aggregate refuses an unreadable application, an invalid name, module or table, and a missing module in $app_name"
  expect_item_refused hardslice-aggregate "a run without the web project" "with --project src/<App>/<App>.csproj" --module "$module" --name "$name"
  expect_item_refused hardslice-aggregate "a run without name" "--name is required" --project "$project" --module "$module"
  expect_item_refused hardslice-aggregate "a name that is not PascalCase" "--name 'room-slot' is not PascalCase" --project "$project" --module "$module" --name room-slot
  expect_item_refused hardslice-aggregate "a run without module" "Missing mandatory option(s) for the template 'HardSlice aggregate': '--module'." --project "$project" --name "$name"
  expect_item_refused hardslice-aggregate "a module that is not PascalCase" "--module 'payments' is not PascalCase" --project "$project" --module payments --name "$name"
  expect_item_refused hardslice-aggregate "a module that does not exist" "module 'Payments' does not exist: src/$app_name/Features/Payments/PaymentsModule.cs was not found. Create the module first with dotnet new hardslice-module --project src/$app_name/$app_name.csproj --name Payments, then add the aggregate" --project "$project" --module Payments --name "$name"
  expect_item_refused hardslice-aggregate "a table that is not an identifier" "--table 'room-slots' is not a table name of letters, digits and underscores" --project "$project" --module "$module" --name "$name" --table room-slots
  # PB-056: names that the generated mapping and tests could not use, CS0118 for a namespace and CS0104 for an imported type.
  expect_item_refused hardslice-aggregate "an aggregate named as its module" "--name '$module' is already the name of a module or of a namespace of the application" --project "$project" --module "$module" --name "$module"
  expect_item_refused hardslice-aggregate "an aggregate named as a namespace of the application" "--name 'Domain' is already the name of a module or of a namespace of the application" --project "$project" --module "$module" --name Domain
  expect_item_refused hardslice-aggregate "an aggregate named as a type of the implicit usings" "--name 'Task' is already the name of a type that the files of the application or of its tests import" --project "$project" --module "$module" --name Task

  [[ ! -d "$migrations" ]] || cp -r "$migrations" "$work/migrations.backup"

  echo "==> Generating the aggregate $module/$name of $app_name with dotnet new hardslice-aggregate"
  generate_aggregate "$module" "$name" "$@"
  migration="dotnet ef migrations add Add$table --project src/$app_name --output-dir Shared/Persistence/Migrations"
  for expected in "Complete the aggregate $name of module $module, then add its migration:" \
    "dotnet tool restore" \
    "$migration"; do
    if ! grep -Fxq -- "$expected" <<< "${aggregate_output//Manual instructions: /}"; then
      printf '%s\n' "$aggregate_output" >&2
      echo "error: dotnet new hardslice-aggregate did not print the line '$expected'." >&2
      exit 1
    fi
  done
  while read -r generated; do
    if grep -q 'MyApp\|ModuleName\|AggregateName\|AggregateTable' "$app/$generated"; then
      echo "error: $generated, generated in $app_name, keeps a placeholder of the template." >&2
      exit 1
    fi
    # PB-056: the template cites no type of the Bookings sample, which --no-sample leaves out.
    if [[ "$module$name" != *Booking* ]] && grep -qi 'booking' "$app/$generated"; then
      echo "error: $generated, generated in $app_name, mentions the Bookings sample." >&2
      exit 1
    fi
  done < <(aggregate_files "$module" "$name")
  for expected in "public sealed class $name : IAggregateRoot" \
    "    private $name()" \
    "    public static $name Create() => new(${name}Id.New());" \
    "    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()"; do
    if ! grep -Fxq -- "$expected" "$app/src/$app_name.Domain/$module/$name.cs"; then
      cat "$app/src/$app_name.Domain/$module/$name.cs" >&2
      echo "error: dotnet new hardslice-aggregate did not write the line '$expected' in the aggregate $name." >&2
      exit 1
    fi
  done
  if ! grep -Fxq "public readonly record struct ${name}Id(Guid Value)" "$app/src/$app_name.Domain/$module/${name}Id.cs" ||
    ! grep -Fxq "internal sealed class ${name}Configuration : IEntityTypeConfiguration<$name>" "$app_src/Features/$module/${name}Configuration.cs" ||
    ! grep -Fxq "        builder.ToTable(\"$table\");" "$app_src/Features/$module/${name}Configuration.cs"; then
    echo "error: dotnet new hardslice-aggregate did not declare ${name}Id as a record struct, or did not map $name to the table $table in an internal configuration." >&2
    exit 1
  fi
  expect_item_refused hardslice-aggregate "an aggregate that exists" "--force" --project "$project" --module "$module" --name "$name"

  # The analyzers of the domain and of the web project see the aggregate and its mapping: HS007, HS008, HS901 and HS902 stay silent.
  echo "==> Checking that $app_name builds the aggregate $module/$name without HardSlice diagnostic, HS007, HS008, HS901 and HS902 included"
  if ! build_output="$(cd "$app" && dotnet build "$app_name.slnx" 2>&1)" || grep -Eq '(error|warning) HS[0-9]{3}' <<< "$build_output"; then
    printf '%s\n' "$build_output" >&2
    echo "error: $app_name does not build the aggregate $module/$name without HardSlice diagnostic." >&2
    exit 1
  fi
  echo "    $name, ${name}Id and ${name}Configuration build"

  # The check runs the unit tests before the integration tests: the TODO(hardslice): test fails first, the missing migration then.
  echo "==> Checking that the check of $app_name fails on the TODO(hardslice): test of $module/$name only"
  expect_check_fails_on_todo_tests_only "the generated aggregate $module/$name" \
    "$app_name.Tests.Domain.$module.${name}Tests.Create_gives_the_new_${name}_its_state"

  # The reference completion of the test: the state of the generated aggregate is its identifier, already asserted.
  sed '/^        Assert\.Fail("TODO(hardslice): /,/Should\.Throw<DomainException>\.");$/d' "$unit_test" > "$work/test.cs"
  mv "$work/test.cs" "$unit_test"
  if grep -rl 'TODO(hardslice):' "$app/src" "$app/tests" --include='*.cs'; then
    echo "error: the reference completion of $module/$name leaves TODO(hardslice): markers." >&2
    exit 1
  fi

  # MigrationsTests always fails. Once the solution has a migration, EF Core also refuses to migrate the database of the others.
  echo "==> Checking that the check of $app_name detects the missing migration of $module/$name"
  if check_output="$(run_check check.sh 2>&1)"; then
    echo "error: scripts/check.sh passed on the aggregate $module/$name without its migration." >&2
    exit 1
  fi
  check_output="$(strip_ansi <<< "$check_output")"
  failures="$(sed -n 's/^failed \([^ ]*\) (.*/\1/p' <<< "$check_output" | LC_ALL=C sort -u)"
  if [[ "$failures" != *"$app_name.IntegrationTests.Shared.Persistence.MigrationsTests.Model_has_no_changes_missing_from_the_migrations"* ]] ||
    grep -v "^$app_name\.IntegrationTests\." <<< "$failures" ||
    [[ "$check_output" != *"has pending changes. Add a new migration before updating the database."* &&
      "$check_output" != *"Add a migration: dotnet ef migrations add <Name> --project src/$app_name --output-dir Shared/Persistence/Migrations"* ]] ||
    grep -Eq '(error|warning) HS[0-9]{3}' <<< "$check_output"; then
    printf '%s\n' "$check_output" >&2
    echo "error: scripts/check.sh did not fail on the integration tests alone, MigrationsTests included, for the missing migration of $module/$name." >&2
    exit 1
  fi
  echo "    MigrationsTests: the model has changes missing from the migrations"

  echo "==> Checking that $app_name passes the check once the migration of $module/$name is added as printed"
  (
    cd "$app"
    dotnet tool restore > /dev/null
    $migration
  )
  bash "$app/scripts/check.sh"

  if [[ "$keep" == remove ]]; then
    while read -r generated; do
      rm "${app:?}/${generated:?}"
    done < <(aggregate_files "$module" "$name")
    rm -r "${migrations:?}"
    [[ ! -d "$work/migrations.backup" ]] || mv "$work/migrations.backup" "$migrations"
  else
    rm -rf "${work:?}/migrations.backup"
  fi
}

# The files that dotnet new hardslice-entity creates for the child entity <name> of module <module>, besides the converter of
# the identifier of its root, which only the first child of a root creates.
entity_files() {
  local module="$1"
  local name="$2"
  printf '%s\n' "src/$app_name.Domain/$module/$name.cs" \
    "src/$app_name.Domain/$module/${name}Id.cs" \
    "src/$app_name/Features/$module/${name}Configuration.cs" \
    "tests/$app_name.Tests/Domain/$module/${name}Tests.cs"
}

# Generates the child entity <name> of the aggregate <module>/<aggregate> with the remaining options, checks that its files
# exist, and leaves the printed instructions in $entity_output.
generate_entity() {
  local module="$1"
  local aggregate="$2"
  local name="$3"
  shift 3
  local generated

  if ! entity_output="$(new_item hardslice-entity --project "src/$app_name/$app_name.csproj" --module "$module" --aggregate "$aggregate" --name "$name" "$@")"; then
    printf '%s\n' "$entity_output" >&2
    echo "error: dotnet new hardslice-entity failed on the child entity $name of $module/$aggregate in $app_name." >&2
    exit 1
  fi
  while read -r generated; do
    if [[ ! -f "$app/$generated" ]]; then
      printf '%s\n' "$entity_output" >&2
      echo "error: dotnet new hardslice-entity did not create $generated in $app_name." >&2
      exit 1
    fi
  done < <(entity_files "$module" "$name")
}

# Generates two child entities of the aggregate <aggregate> of module <module>, mapped to the table <aggregate>s and without a
# converter of its identifier yet, through every path of dotnet new hardslice-entity: its refusals, <held> held in a collection
# of the root, which creates the converter of the identifier of the root, then <with-id>, which holds that identifier
# (--link id), mapped to the table <table>, and reuses the converter. Both build without HardSlice diagnostic, HS008 included,
# fail the check on their TODO(hardslice): tests only, then pass it once the root holds the collection its instructions print,
# the markers are replaced and the printed migration is added. The children, the converter and the migration are removed and
# the root restored at the end.
probe_entity_template() {
  local module="$1"
  local aggregate="$2"
  local held="$3"
  local with_id="$4"
  local table="$5"
  local project="src/$app_name/$app_name.csproj"
  local migrations="$app_src/Shared/Persistence/Migrations"
  local root="$app/src/$app_name.Domain/$module/$aggregate.cs"
  local converter="$app_src/Features/$module/${aggregate}IdConverter.cs"
  local collection="${held}s"
  local field="_${collection,}"
  local held_lines
  local held_output
  local migration
  local migration_file
  local build_output
  local refusal
  local fields
  local arguments
  local option
  local converter_sum
  local check_output
  local generated
  local expected

  if [[ -e "$converter" ]]; then
    echo "error: $module/$aggregate of $app_name already has its converter ${aggregate}IdConverter: the probe of hardslice-entity needs a root without it." >&2
    exit 1
  fi

  echo "==> Checking that dotnet new hardslice-entity refuses an unreadable application, an invalid or missing name, module or aggregate, an invalid table and a name the module cannot take in $app_name"
  expect_item_refused hardslice-entity "a run without the web project" "with --project src/<App>/<App>.csproj" --module "$module" --aggregate "$aggregate" --name "$held"
  expect_item_refused hardslice-entity "a run without module" "Missing mandatory option(s) for the template 'HardSlice child entity': '--module'." --project "$project" --aggregate "$aggregate" --name "$held"
  expect_item_refused hardslice-entity "a run without aggregate" "Missing mandatory option(s) for the template 'HardSlice child entity': '--aggregate'." --project "$project" --module "$module" --name "$held"
  expect_item_refused hardslice-entity "an unknown link" "'parent' is not a valid value for --link" --project "$project" --module "$module" --aggregate "$aggregate" --name "$held" --link parent
  for refusal in "a run without name|--name is required" \
    "a name that is not PascalCase|--name 'room-seat' is not PascalCase|--name|room-seat" \
    "a module that is not PascalCase|--module 'payments' is not PascalCase|--module|payments" \
    "a module that does not exist|module 'Payments' does not exist: src/$app_name/Features/Payments/PaymentsModule.cs was not found|--module|Payments" \
    "an aggregate that is not PascalCase|--aggregate 'invoice' is not PascalCase|--aggregate|invoice" \
    "an aggregate that the module does not map|aggregate 'Invoice' does not exist in module '$module': its mapping src/$app_name/Features/$module/InvoiceConfiguration.cs was not found. Create the aggregate first with dotnet new hardslice-aggregate --project src/$app_name/$app_name.csproj --module $module --name Invoice|--aggregate|Invoice" \
    "a table that is not an identifier|--table 'room-seats' is not a table name of letters, digits and underscores|--table|room-seats" \
    "a child entity named as its module|--name '$module' is already the name of a module or of a namespace of the application|--name|$module" \
    "a child entity named as a namespace of the application|--name 'Domain' is already the name of a module or of a namespace of the application|--name|Domain" \
    "a child entity named as a type of the implicit usings|--name 'Task' is already the name of a type that the files of the application or of its tests import|--name|Task" \
    "a child entity named as its aggregate|--name '$aggregate' is already mapped in module '$module': src/$app_name/Features/$module/${aggregate}Configuration.cs exists|--name|$aggregate"; do
    IFS='|' read -r -a fields <<< "$refusal"
    # The valid value of each option that the refusal does not give, and none for --name in the run without name.
    arguments=(--project "$project")
    for option in --module --aggregate --name; do
      [[ " ${fields[*]:2} " == *" $option "* || ( "$option" == --name && "${fields[0]}" == "a run without name" ) ]] && continue
      case "$option" in
        --module) arguments+=(--module "$module") ;;
        --aggregate) arguments+=(--aggregate "$aggregate") ;;
        --name) arguments+=(--name "$held") ;;
      esac
    done
    expect_item_refused hardslice-entity "${fields[0]}" "${fields[1]}" "${arguments[@]}" "${fields[@]:2}"
    if [[ "$item_refused_status" != 105 ]]; then
      echo "error: dotnet new hardslice-entity refused ${fields[0]} with exit $item_refused_status instead of 105." >&2
      exit 1
    fi
  done

  cp "$root" "$work/entity-root.backup"
  [[ ! -d "$migrations" ]] || cp -r "$migrations" "$work/entity-migrations.backup"

  echo "==> Generating the child entity $held of $module/$aggregate in $app_name, held in a collection of its root, with dotnet new hardslice-entity"
  generate_entity "$module" "$aggregate" "$held"
  held_output="$entity_output"
  if [[ ! -f "$converter" ]]; then
    echo "error: dotnet new hardslice-entity did not create the converter ${aggregate}IdConverter with the first child of $aggregate." >&2
    exit 1
  fi
  held_lines="$(printf '%s\n' "private readonly List<$held> $field = [];" "public IReadOnlyList<$held> $collection => $field.AsReadOnly();")"
  for expected in "Complete the child entity $held of aggregate $aggregate, then add its migration:" \
    "private readonly List<$held> $field = [];" \
    "public IReadOnlyList<$held> $collection => $field.AsReadOnly();" \
    "dotnet ef migrations add Add$collection --project src/$app_name --output-dir Shared/Persistence/Migrations"; do
    if ! grep -Fxq -- "$expected" <<< "${entity_output//Manual instructions: /}"; then
      printf '%s\n' "$entity_output" >&2
      echo "error: dotnet new hardslice-entity did not print the line '$expected'." >&2
      exit 1
    fi
  done
  for expected in "src/$app_name.Domain/$module/$held.cs|public sealed class $held : IEntity" \
    "src/$app_name.Domain/$module/$held.cs|    public static $held Create() => new(${held}Id.New());" \
    "src/$app_name.Domain/$module/${held}Id.cs|public readonly record struct ${held}Id(Guid Value)" \
    "src/$app_name/Features/$module/${held}Configuration.cs|internal sealed class ${held}Configuration : IEntityTypeConfiguration<$held>" \
    "src/$app_name/Features/$module/${held}Configuration.cs|        builder.ToTable(\"$collection\");" \
    "src/$app_name/Features/$module/${held}Configuration.cs|        builder.Property<${aggregate}Id>(\"${aggregate}Id\").HasConversion<${aggregate}IdConverter>();" \
    "src/$app_name/Features/$module/${held}Configuration.cs|            .WithMany(\"$collection\")" \
    "src/$app_name/Features/$module/${aggregate}IdConverter.cs|internal sealed class ${aggregate}IdConverter() : ValueConverter<${aggregate}Id, Guid>(id => id.Value, value => new ${aggregate}Id(value));"; do
    if ! grep -Fxq -- "${expected#*|}" "$app/${expected%%|*}"; then
      cat "$app/${expected%%|*}" >&2
      echo "error: dotnet new hardslice-entity did not write the line '${expected#*|}' in ${expected%%|*}." >&2
      exit 1
    fi
  done
  expect_item_refused hardslice-entity "a child entity that exists" "--name '$held' is already mapped in module '$module'" --project "$project" --module "$module" --aggregate "$aggregate" --name "$held"
  if [[ "$item_refused_status" != 105 ]]; then
    echo "error: dotnet new hardslice-entity refused a child entity that exists with exit $item_refused_status instead of 105." >&2
    exit 1
  fi

  echo "==> Generating the child entity $with_id of $module/$aggregate in $app_name, which holds the identifier of its root, with dotnet new hardslice-entity --link id --table $table"
  converter_sum="$(cksum < "$converter")"
  generate_entity "$module" "$aggregate" "$with_id" --link id --table "$table"
  if [[ "$(cksum < "$converter")" != "$converter_sum" ]] ||
    [[ "$(find "$app_src/Features/$module" -name '*IdConverter.cs' | wc -l)" != 1 ]]; then
    echo "error: dotnet new hardslice-entity did not reuse the converter ${aggregate}IdConverter for the second child of $aggregate." >&2
    exit 1
  fi
  for expected in "Complete the child entity $with_id of aggregate $aggregate, then add its migration:" \
    "dotnet ef migrations add Add$table --project src/$app_name --output-dir Shared/Persistence/Migrations"; do
    if ! grep -Fxq -- "$expected" <<< "${entity_output//Manual instructions: /}"; then
      printf '%s\n' "$entity_output" >&2
      echo "error: dotnet new hardslice-entity --link id did not print the line '$expected'." >&2
      exit 1
    fi
  done
  if [[ "$entity_output" == *"private readonly List<"* ]]; then
    printf '%s\n' "$entity_output" >&2
    echo "error: dotnet new hardslice-entity --link id asks for a collection of the root." >&2
    exit 1
  fi
  for expected in "src/$app_name.Domain/$module/$with_id.cs|    public ${aggregate}Id ${aggregate}Id { get; }" \
    "src/$app_name.Domain/$module/$with_id.cs|    public static $with_id Create(${aggregate}Id ${aggregate,}Id) => new(${with_id}Id.New(), ${aggregate,}Id);" \
    "src/$app_name/Features/$module/${with_id}Configuration.cs|        builder.ToTable(\"$table\");" \
    "src/$app_name/Features/$module/${with_id}Configuration.cs|        builder.Property(entity => entity.${aggregate}Id).HasConversion<${aggregate}IdConverter>();" \
    "src/$app_name/Features/$module/${with_id}Configuration.cs|            .WithMany()" \
    "src/$app_name/Features/$module/${with_id}Configuration.cs|            .HasForeignKey(entity => entity.${aggregate}Id)"; do
    if ! grep -Fxq -- "${expected#*|}" "$app/${expected%%|*}"; then
      cat "$app/${expected%%|*}" >&2
      echo "error: dotnet new hardslice-entity --link id did not write the line '${expected#*|}' in ${expected%%|*}." >&2
      exit 1
    fi
  done
  while read -r generated; do
    if grep -q 'MyApp\|ModuleName\|AggregateName\|aggregateName\|EntityName\|EntityTable\|EntityCollection\|entityCollection\|#if\|#else\|#endif' "$app/$generated"; then
      echo "error: $generated, generated in $app_name, keeps a placeholder or a condition of the template." >&2
      exit 1
    fi
    # PB-056: the template cites no type of the Bookings sample, which --no-sample leaves out.
    if [[ "$module$aggregate" != *Booking* ]] && grep -qi 'booking' "$app/$generated"; then
      echo "error: $generated, generated in $app_name, mentions the Bookings sample." >&2
      exit 1
    fi
  done < <(entity_files "$module" "$held"; entity_files "$module" "$with_id"; echo "src/$app_name/Features/$module/${aggregate}IdConverter.cs")

  # HS008 knows no owner for either child yet, then knows the root as the owner of the held one: neither reports.
  echo "==> Checking that $app_name builds both children of $module/$aggregate without HardSlice diagnostic, HS007, HS008 and HS902 included"
  if ! build_output="$(cd "$app" && dotnet build "$app_name.slnx" 2>&1)" || grep -Eq '(error|warning) HS[0-9]{3}' <<< "$build_output"; then
    printf '%s\n' "$build_output" >&2
    echo "error: $app_name does not build the children of $module/$aggregate without HardSlice diagnostic." >&2
    exit 1
  fi
  echo "    $held, $with_id and ${aggregate}IdConverter build"

  # Until the root holds the collection that the mapping of the held child names, EF Core builds no model: the tests that build
  # it fail with the TODO(hardslice): tests, on the message that the instructions announce.
  echo "==> Checking that the check of $app_name fails on the collection of $held missing from $aggregate, as the template says"
  if check_output="$(run_check check.sh 2>&1)"; then
    echo "error: scripts/check.sh passed on the generated children of $module/$aggregate." >&2
    exit 1
  fi
  check_output="$(strip_ansi <<< "$check_output")"
  if [[ "$check_output" != *"The navigation '$collection' cannot be added to the entity type '$aggregate' because there is no corresponding CLR property"* ]] ||
    [[ "$held_output" != *"saying that the navigation $collection has no corresponding CLR property"* ]] ||
    grep -Eq '(error|warning) HS[0-9]{3}' <<< "$check_output"; then
    printf '%s\n' "$check_output" >&2
    echo "error: scripts/check.sh did not fail on the collection $collection missing from $aggregate, without HardSlice diagnostic." >&2
    exit 1
  fi
  echo "    EF Core: the navigation '$collection' has no corresponding CLR property on '$aggregate'"

  # The reference completion: the root holds the collection as the instructions print it, the state of each child is its identifiers.
  echo "==> Completing $module/$aggregate with the collection of $held that the template printed"
  if ! HELD_LINES="$held_lines" awk '
      BEGIN { split(ENVIRON["HELD_LINES"], held, "\n") }
      { print }
      /^    private readonly List<IDomainEvent> _domainEvents = \[\];$/ { print "    " held[1]; added++ }
      /^    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents\.AsReadOnly\(\);$/ { print ""; print "    " held[2]; added++ }
      END { if (added != 2) exit 1 }' "$root" > "$work/root.cs"; then
    echo "error: $root has no list of events or no DomainEvents to add the collection of $held next to." >&2
    exit 1
  fi
  mv "$work/root.cs" "$root"

  echo "==> Checking that the check of $app_name then fails on the TODO(hardslice): tests of the children of $module/$aggregate only"
  expect_check_fails_on_todo_tests_only "the generated children of $module/$aggregate" \
    "$(printf '%s\n' "$app_name.Tests.Domain.$module.${held}Tests.Create_gives_the_new_${held}_its_state" \
      "$app_name.Tests.Domain.$module.${with_id}Tests.Create_gives_the_new_${with_id}_its_state" | LC_ALL=C sort)"
  for generated in "$held" "$with_id"; do
    sed '/^        Assert\.Fail("TODO(hardslice): /,/");$/d' "$app/tests/$app_name.Tests/Domain/$module/${generated}Tests.cs" > "$work/test.cs"
    mv "$work/test.cs" "$app/tests/$app_name.Tests/Domain/$module/${generated}Tests.cs"
  done
  if grep -rl 'TODO(hardslice):' "$app/src" "$app/tests" --include='*.cs'; then
    echo "error: the reference completion of the children of $module/$aggregate leaves TODO(hardslice): markers." >&2
    exit 1
  fi

  echo "==> Checking that $app_name passes the check once the migration printed for $held is added, with the tables of both children"
  migration="dotnet ef migrations add Add$collection --project src/$app_name --output-dir Shared/Persistence/Migrations"
  (
    cd "$app"
    dotnet tool restore > /dev/null
    $migration
  )
  migration_file="$(find "$migrations" -name "*_Add$collection.cs" -not -name '*.Designer.cs')"
  for expected in "name: \"$collection\"," "name: \"$table\"," "principalTable: \"${aggregate}s\","; do
    if ! grep -Fq -- "$expected" "$migration_file"; then
      cat "$migration_file" >&2
      echo "error: the migration of the children of $module/$aggregate does not hold '$expected'." >&2
      exit 1
    fi
  done
  if [[ "$(grep -Fc 'onDelete: ReferentialAction.Cascade' "$migration_file")" != 2 ]]; then
    cat "$migration_file" >&2
    echo "error: the migration of the children of $module/$aggregate does not delete both children with their root." >&2
    exit 1
  fi
  bash "$app/scripts/check.sh"

  while read -r generated; do
    rm "${app:?}/${generated:?}"
  done < <(entity_files "$module" "$held"; entity_files "$module" "$with_id")
  rm "${converter:?}"
  mv "$work/entity-root.backup" "$root"
  rm -r "${migrations:?}"
  [[ ! -d "$work/entity-migrations.backup" ]] || mv "$work/entity-migrations.backup" "$migrations"
}

# PB-085: the example of "Mapping private fields" in the add-aggregate skill of the generated solution, applied as it is written
# to an aggregate Trip of module <module> generated by dotnet new hardslice-aggregate: its value object, the members of the
# aggregate and the lines of its mapping. Its migration stores each private field in its own column, the check passes, and an
# integration test reads the stops of a saved trip back through the fields. Everything is removed afterwards.
probe_private_fields() {
  local module="$1"
  local skill="$app/.claude/skills/add-aggregate/SKILL.md"
  local migrations="$app_src/Shared/Persistence/Migrations"
  local aggregate="$app/src/$app_name.Domain/$module/Trip.cs"
  local configuration="$app_src/Features/$module/TripConfiguration.cs"
  local value_object="$app/src/$app_name.Domain/$module/TripStop.cs"
  local integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/TripConfigurationTests.cs"
  local unit_test="$app/tests/$app_name.Tests/Domain/$module/TripTests.cs"
  local blocks
  local migration_file
  local expected
  local generated

  echo "==> Applying the example of \"Mapping private fields\" of add-aggregate to the aggregate $module/Trip of $app_name"
  rm -f "$work"/private-fields-*.cs
  awk -v out="$work/private-fields-" '
    /^## / { section = ($0 == "## Mapping private fields") }
    section && /^```csharp$/ { block++; inside = 1; next }
    inside && /^```$/ { inside = 0; next }
    inside { print > (out block ".cs") }
  ' "$skill"
  blocks="$(find "$work" -maxdepth 1 -name 'private-fields-*.cs' | wc -l)"
  if [[ "$blocks" != 3 ]] ||
    [[ "$(head -n 1 "$work/private-fields-1.cs")" != "// src/$app_name.Domain/<Module>/TripStop.cs" ]] ||
    [[ "$(head -n 1 "$work/private-fields-2.cs")" != "// src/$app_name.Domain/<Module>/Trip.cs, members added to the generated aggregate" ]] ||
    [[ "$(head -n 1 "$work/private-fields-3.cs")" != "// src/$app_name/Features/<Module>/TripConfiguration.cs, lines added to Configure" ]]; then
    sed -n '/^## Mapping private fields$/,/^## Never$/p' "$skill" >&2
    echo "error: \"Mapping private fields\" of add-aggregate in $app_name does not hold the value object TripStop, the members of Trip and the lines of TripConfiguration, in that order ($blocks C# blocks)." >&2
    exit 1
  fi

  [[ ! -d "$migrations" ]] || cp -r "$migrations" "$work/private-fields-migrations.backup"
  generate_aggregate "$module" Trip
  sed "s/<Module>/$module/g" "$work/private-fields-1.cs" > "$value_object"
  {
    sed '$d' "$aggregate"
    echo
    tail -n +2 "$work/private-fields-2.cs" | sed 's/^\(.\)/    \1/'
    echo '}'
  } > "$work/Trip.cs"
  mv "$work/Trip.cs" "$aggregate"
  if ! grep -Fxq '        builder.Ignore(aggregate => aggregate.DomainEvents);' "$configuration"; then
    echo "error: the mapping of $module/Trip generated in $app_name does not ignore the domain events of the aggregate." >&2
    exit 1
  fi
  tail -n +2 "$work/private-fields-3.cs" | sed 's/^\(.\)/        \1/' > "$work/private-fields-mapping.cs"
  awk -v mapping="$work/private-fields-mapping.cs" '
    $0 == "        builder.Ignore(aggregate => aggregate.DomainEvents);" { while ((getline line < mapping) > 0) print line; print "" }
    { print }
  ' "$configuration" > "$work/TripConfiguration.cs"
  mv "$work/TripConfiguration.cs" "$configuration"
  sed '/^        Assert\.Fail("TODO(hardslice): /,/Should\.Throw<DomainException>\.");$/d' "$unit_test" > "$work/test.cs"
  mv "$work/test.cs" "$unit_test"
  cat > "$integration_test" <<EOF
using Microsoft.EntityFrameworkCore;
using $app_name.Domain.$module;

namespace $app_name.IntegrationTests.Features.$module;

public sealed class TripConfigurationTests(PostgresDatabase database)
{
    [Fact]
    public async Task Reads_back_the_stops_that_the_private_fields_of_a_trip_store()
    {
        var trip = Trip.Create();
        trip.Plan([new TripStop("A", 0), new TripStop("B", 12)], new DateTimeOffset(2099, 11, 2, 9, 0, 0, TimeSpan.Zero));
        await using (var db = database.CreateDbContext())
        {
            db.Set<Trip>().Add(trip);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = database.CreateDbContext();
        var stored = await reader.Set<Trip>().AsNoTracking().SingleAsync(candidate => candidate.Id == trip.Id, TestContext.Current.CancellationToken);

        stored.Stops.ShouldBe(new[] { new TripStop("A", 0), new TripStop("B", 12) });
        stored.IsPlanned.ShouldBeTrue();
    }
}
EOF

  echo "==> Checking that the migration of $module/Trip stores each private field in its own column, and that $app_name passes the check"
  (
    cd "$app"
    dotnet tool restore > /dev/null
    dotnet ef migrations add AddTrips --project "src/$app_name" --output-dir Shared/Persistence/Migrations
  )
  migration_file="$(find "$migrations" -name '*_AddTrips.cs' -not -name '*.Designer.cs')"
  for expected in 'StopIds = table.Column<string[]>(type: "text[]", nullable: false)' \
    'Minutes = table.Column<int[]>(type: "integer[]", nullable: false)' \
    'PlannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)'; do
    if ! grep -Fq -- "$expected" "$migration_file"; then
      cat "$migration_file" >&2
      echo "error: the migration of $module/Trip does not hold '$expected'." >&2
      exit 1
    fi
  done
  if grep -Eq '"(Stops|IsPlanned|_stopIds|_minutes|_plannedAt)"' "$migration_file"; then
    cat "$migration_file" >&2
    echo "error: the migration of $module/Trip maps a public view of the aggregate, or names a column after a private field." >&2
    exit 1
  fi
  bash "$app/scripts/check.sh"
  echo "    Trip: StopIds text[], Minutes integer[] and PlannedAt from private fields, read back through Stops"

  while read -r generated; do
    rm "${app:?}/${generated:?}"
  done < <(aggregate_files "$module" Trip)
  rm "$value_object" "$integration_test"
  rm -r "${migrations:?}"
  [[ ! -d "$work/private-fields-migrations.backup" ]] || mv "$work/private-fields-migrations.backup" "$migrations"
}

# The files that dotnet new hardslice-event creates for the event <name> of module <module>, then, with <reaction>
# '<module>/<slice>', for its reaction slice: the handler and its integration tests.
event_files() {
  local module="$1"
  local name="$2"
  local reaction="${3:-}"
  printf '%s\n' "src/$app_name.Domain/$module/Events/$name.cs"
  [[ -z "$reaction" ]] || printf '%s\n' "src/$app_name/Features/$reaction/${reaction#*/}.cs" \
    "tests/$app_name.IntegrationTests/Features/$reaction/${reaction#*/}Tests.cs"
}

# Generates the event <module>/<name> with the remaining options, checks that its files exist, those of its reaction when
# --reaction is given and none otherwise, and leaves the printed instructions in $event_output.
generate_event() {
  local module="$1"
  local name="$2"
  shift 2
  local reaction=""
  local option
  local generated

  for option in "$@"; do
    [[ "$reaction" != next ]] || reaction="$option"
    [[ "$option" != --reaction ]] || reaction=next
  done
  if ! event_output="$(new_item hardslice-event --project "src/$app_name/$app_name.csproj" --module "$module" --name "$name" "$@")"; then
    printf '%s\n' "$event_output" >&2
    echo "error: dotnet new hardslice-event failed on the event $module/$name of $app_name." >&2
    exit 1
  fi
  while read -r generated; do
    if [[ ! -f "$app/$generated" ]]; then
      printf '%s\n' "$event_output" >&2
      echo "error: dotnet new hardslice-event did not create $generated in $app_name." >&2
      exit 1
    fi
    if grep -q 'MyApp\|ModuleName\|EventName\|ReactionModule\|ReactionSlice\|ReactionArgument' "$app/$generated"; then
      echo "error: $generated, generated in $app_name, keeps a placeholder of the template." >&2
      exit 1
    fi
  done < <(event_files "$module" "$name" "$reaction")
  if [[ -z "$reaction" ]] && grep -rl "IDomainEventHandler<$name>" "$app/src" "$app/tests" --include='*.cs'; then
    echo "error: dotnet new hardslice-event created a reaction to $name in $app_name without --reaction." >&2
    exit 1
  fi
}

# Completes the reaction <module>/<slice> as a reference: its handler adds through AppDbContext the aggregate <aggregate> of module
# <aggregate-module> that <create> creates, and its test reads it back from PostgreSQL. No marker remains in them.
complete_reaction() {
  local reaction="$1"
  local aggregate_module="$2"
  local aggregate="$3"
  local create="$4"
  local slice="$app_src/Features/$reaction/${reaction#*/}.cs"
  local test_file="$app/tests/$app_name.IntegrationTests/Features/$reaction/${reaction#*/}Tests.cs"

  {
    printf 'using %s.Domain.%s;\nusing %s.Shared.Persistence;\n' "$app_name" "$aggregate_module" "$app_name"
    CREATE="$create" AGGREGATE="$aggregate" awk '
      /^internal sealed class [A-Za-z0-9]+Handler : IDomainEventHandler</ { sub(/Handler : /, "Handler(AppDbContext db) : ") }
      /^    public Task Handle\(.*\) =>$/ {
        sub(/ =>$/, "")
        print
        print "    {"
        print "        db.Set<" ENVIRON["AGGREGATE"] ">().Add(" ENVIRON["CREATE"] ");"
        print "        return Task.CompletedTask;"
        print "    }"
        skipping = 1
        next
      }
      skipping { if ($0 ~ /HTTP call\."\);$/) skipping = 0; next }
      { print }' "$slice"
  } > "$work/slice.cs"
  mv "$work/slice.cs" "$slice"
  # A reaction generated with --reaction-only builds its event in a helper: the reference completion builds it with a new Guid.
  {
    printf 'using Microsoft.EntityFrameworkCore;\nusing %s.Domain.%s;\n' "$app_name" "$aggregate_module"
    AGGREGATE="$aggregate" awk '
      /^    private static [A-Za-z0-9]+ Build[A-Za-z0-9]+\(\) =>$/ { event = $3; print; building = 1; next }
      building { if ($0 ~ /"\);$/) { print "        new " event "(Guid.NewGuid());"; building = 0 } next }
      /^        Assert\.Fail\("TODO\(hardslice\): / {
        print "        var added = db.ChangeTracker.Entries<" ENVIRON["AGGREGATE"] ">().ShouldHaveSingleItem().Entity;"
        print "        await using var stored = database.CreateDbContext();"
        print "        (await stored.Set<" ENVIRON["AGGREGATE"] ">().AnyAsync(aggregate => aggregate.Id == added.Id, TestContext.Current.CancellationToken)).ShouldBeTrue();"
        next
      }
      { print }' "$test_file"
  } > "$work/test.cs"
  mv "$work/test.cs" "$test_file"
}

# Generates domain events of module <module> through every path of dotnet new hardslice-event: refusals, the event <event> alone,
# a name that HS019 does not read as a past fact, then the reaction of <event> in <module>, added with --reaction-only to the
# event once changed by hand, and the event <other-event> with its reaction in the module <other-module>, which it generates and registers in
# Program.cs. Then a forgotten registration, the TODO(hardslice): tests, and the reference completion, in which each reaction
# adds an aggregate <aggregate> of <module> created by <create>. The module file and Program.cs are restored, the events, the
# reactions and <other-module> removed at the end.
probe_event_template() {
  local module="$1"
  local event="$2"
  local other_event="$3"
  local other_module="$4"
  local aggregate="$5"
  local create="$6"
  local project="src/$app_name/$app_name.csproj"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local events="$app/src/$app_name.Domain/$module/Events"
  local events_existed=false
  local reaction="$module/RecordOn$event"
  local other_reaction="$other_module/PrepareOn$other_event"
  local command_name="Rename$aggregate"
  local reaction_output
  local test_output
  local expected
  local generated

  [[ ! -d "$events" ]] || events_existed=true
  cp "$module_file" "$work/module.backup"
  cp "$app_src/Program.cs" "$work/Program-event.backup"

  echo "==> Checking that dotnet new hardslice-event refuses an unreadable application, an invalid name, module or reaction, and a missing module in $app_name"
  expect_item_refused hardslice-event "a run without the web project" "with --project src/<App>/<App>.csproj" --module "$module" --name "$event"
  expect_item_refused hardslice-event "a run without name" "--name is required" --project "$project" --module "$module"
  expect_item_refused hardslice-event "a name that is not PascalCase" "--name 'room-renamed' is not PascalCase" --project "$project" --module "$module" --name room-renamed
  expect_item_refused hardslice-event "a run without module" "Missing mandatory option(s) for the template 'HardSlice domain event': '--module'." --project "$project" --name "$event"
  expect_item_refused hardslice-event "a module that is not PascalCase" "--module 'payments' is not PascalCase" --project "$project" --module payments --name "$event"
  expect_item_refused hardslice-event "a module that does not exist" "module 'Payments' does not exist: src/$app_name/Features/Payments/PaymentsModule.cs was not found. Create the module first with dotnet new hardslice-module --project src/$app_name/$app_name.csproj --name Payments, then add the event" --project "$project" --module Payments --name "$event"
  expect_item_refused hardslice-event "a reaction that is not <Module>/<Slice>" "--reaction 'RecordOn$event' is not <Module>/<Slice> in PascalCase" --project "$project" --module "$module" --name "$event" --reaction "RecordOn$event"
  expect_item_refused hardslice-event "a reaction with the Handler suffix" "--reaction '${reaction}Handler' ends with Handler" --project "$project" --module "$module" --name "$event" --reaction "${reaction}Handler"
  expect_item_refused hardslice-event "a reaction named as its event" "--reaction '$module/$event' is named as the event: its namespace would hide the event $event" --project "$project" --module "$module" --name "$event" --reaction "$module/$event"
  expect_item_refused hardslice-event "--reaction-only without reaction" "--reaction-only creates the reaction slice alone: pass it with --reaction <Module>/<Slice>" --project "$project" --module "$module" --name "$event" --reaction-only
  expect_item_refused hardslice-event "a reaction in a module that does not exist" "module 'Payments' of --reaction does not exist: src/$app_name/Features/Payments/PaymentsModule.cs was not found. Create the module first with dotnet new hardslice-module --project src/$app_name/$app_name.csproj --name Payments, then add the reaction" --project "$project" --module "$module" --name "$event" --reaction "Payments/RecordOn$event"

  echo "==> Generating the event $module/$event of $app_name alone with dotnet new hardslice-event"
  generate_event "$module" "$event"
  for expected in "Raise $event from the aggregate of module $module that records the fact:" \
    "_domainEvents.Add(new $event(...));"; do
    if ! grep -Fxq -- "$expected" <<< "${event_output//Manual instructions: /}"; then
      printf '%s\n' "$event_output" >&2
      echo "error: dotnet new hardslice-event did not print the line '$expected'." >&2
      exit 1
    fi
  done
  if [[ "$event_output" == *"warning: "* ]] ||
    ! grep -Fxq "namespace $app_name.Domain.$module.Events;" "$events/$event.cs" ||
    ! grep -Fxq "public sealed record $event(Guid Id) : IDomainEvent;" "$events/$event.cs"; then
    printf '%s\n' "$event_output" >&2
    echo "error: dotnet new hardslice-event did not declare $event as a sealed record of $app_name.Domain.$module.Events, or warned about its name." >&2
    exit 1
  fi
  expect_item_refused hardslice-event "an event that exists" "--force" --project "$project" --module "$module" --name "$event"

  # HS019 is a suggestion: the template warns and creates the event all the same.
  echo "==> Checking that dotnet new hardslice-event warns about an event of $app_name not named as a past fact, and creates it"
  generate_event "$module" "$command_name"
  if [[ "$event_output" != *"warning: $command_name is not named as a past fact: HS019 suggests the past tense"* ]]; then
    printf '%s\n' "$event_output" >&2
    echo "error: dotnet new hardslice-event did not warn that $command_name is not named as a past fact." >&2
    exit 1
  fi
  echo "    warning: $command_name is not named as a past fact"
  rm "${events:?}/${command_name:?}.cs"

  # The event, changed by hand as when it is completed, receives its reaction with --reaction-only, which leaves it as it is and
  # generates a test that builds it in a helper; the other reaction is in another module.
  echo "==> Generating the reaction $reaction to the existing $event with --reaction-only, and $other_reaction to $other_event in another module of $app_name"
  printf '// Completed by hand.\n' >> "$events/$event.cs"
  cp "$events/$event.cs" "$work/event.backup"
  generate_event "$module" "$event" --reaction "$reaction" --reaction-only
  if ! cmp -s "$events/$event.cs" "$work/event.backup"; then
    echo "error: dotnet new hardslice-event --reaction-only changed the existing event $event." >&2
    exit 1
  fi
  rm "$work/event.backup"
  if [[ "$event_output" != *"The event src/$app_name.Domain/$module/Events/$event.cs was left as it is"* ]] ||
    ! grep -Fq "TODO(hardslice): build the $event that the aggregate raises" "$app/tests/$app_name.IntegrationTests/Features/$reaction/RecordOn${event}Tests.cs"; then
    printf '%s\n' "$event_output" >&2
    echo "error: dotnet new hardslice-event --reaction-only did not say that it left $event as it is, or its test does not leave the building of $event to complete." >&2
    exit 1
  fi
  reaction_output="$event_output"
  generate_module "$other_module"
  register_module "$module_output"
  cp "$app_src/Features/$other_module/${other_module}Module.cs" "$work/other-module.backup"
  generate_event "$module" "$other_event" --reaction "$other_reaction"
  for expected in "Register the reaction in src/$app_name/Features/$other_module/${other_module}Module.cs:" \
    "using $app_name.Domain.$module.Events;" \
    "using $app_name.Features.$other_module.PrepareOn$other_event;" \
    "using $app_name.Shared.Messaging;" \
    "            .AddScoped<IDomainEventHandler<$other_event>, PrepareOn${other_event}Handler>()"; do
    if ! grep -Fxq -- "$expected" <<< "${event_output//Manual instructions: /}"; then
      printf '%s\n' "$event_output" >&2
      echo "error: dotnet new hardslice-event did not print the line '$expected'." >&2
      exit 1
    fi
  done
  if ! grep -Fxq "internal sealed class PrepareOn${other_event}Handler : IDomainEventHandler<$other_event>" "$app_src/Features/$other_reaction/PrepareOn$other_event.cs" ||
    grep -Eq 'IMediator\.|SaveChanges\(|Endpoint' "$app_src/Features/$other_reaction/PrepareOn$other_event.cs"; then
    echo "error: dotnet new hardslice-event did not generate PrepareOn${other_event}Handler alone in its reaction slice, without IMediator nor SaveChanges." >&2
    exit 1
  fi

  # Nothing calls the handlers, so the build passes: only the test of the host and the generated tests see the forgotten registrations.
  echo "==> Checking that the test of the host names the handlers of $reaction and $other_reaction that their modules do not register"
  register_slice hardslice-event "$reaction_output" without-registrations
  register_slice hardslice-event "$event_output" without-registrations
  if test_output="$(
    cd "$app"
    dotnet build "$app_name.slnx" 2>&1 &&
      dotnet test --project "tests/$app_name.IntegrationTests/$app_name.IntegrationTests.csproj" --no-build 2>&1
  )"; then
    echo "error: the integration tests of $app_name passed with reactions their modules do not register." >&2
    exit 1
  fi
  test_output="$(strip_ansi <<< "$test_output")"
  for expected in "failed $app_name.IntegrationTests.ProgramTests.Resolves_every_handler_and_validator_of_the_application_by_its_contract" \
    "'RecordOn${event}Handler' as 'IDomainEventHandler<$event>'" \
    "'PrepareOn${other_event}Handler' as 'IDomainEventHandler<$other_event>'" \
    "${module}Module does not register RecordOn${event}Handler as IDomainEventHandler<$event>." \
    "${other_module}Module does not register PrepareOn${other_event}Handler as IDomainEventHandler<$other_event>."; do
    if [[ "$test_output" != *"$expected"* ]]; then
      printf '%s\n' "$test_output" >&2
      echo "error: the integration tests of $app_name did not report '$expected' for the unregistered reactions." >&2
      exit 1
    fi
  done
  echo "    ProgramTests names 'RecordOn${event}Handler' and 'PrepareOn${other_event}Handler'"
  cp "$work/module.backup" "$module_file"
  mv "$work/other-module.backup" "$app_src/Features/$other_module/${other_module}Module.cs"

  # HS004, HS009, HS012, HS014, HS016 and HS018 stay silent, in the module of the event and in the other one.
  echo "==> Checking that $app_name builds the registered reactions without HardSlice diagnostic and fails the check on their TODO(hardslice): tests only"
  register_slice hardslice-event "$reaction_output"
  register_slice hardslice-event "$event_output"
  expect_check_fails_on_todo_tests_only "the generated reactions $reaction and $other_reaction" \
    "$(printf '%s\n' "$app_name.IntegrationTests.Features.$other_module.PrepareOn$other_event.PrepareOn${other_event}Tests.Reacts_to_$other_event" \
      "$app_name.IntegrationTests.Features.$module.RecordOn$event.RecordOn${event}Tests.Reacts_to_$event" | LC_ALL=C sort)"

  echo "==> Checking that the reference completion of $reaction and $other_reaction passes the check of $app_name"
  complete_reaction "$reaction" "$module" "$aggregate" "$create"
  complete_reaction "$other_reaction" "$module" "$aggregate" "$create"
  if grep -rl 'TODO(hardslice):' "$app/src" "$app/tests" --include='*.cs'; then
    echo "error: the reference completion of $reaction and $other_reaction leaves TODO(hardslice): markers." >&2
    exit 1
  fi
  bash "$app/scripts/check.sh"

  while read -r generated; do
    rm "${app:?}/${generated:?}"
  done < <(event_files "$module" "$event"; event_files "$module" "$other_event")
  rm -r "${app_src:?}/Features/${reaction:?}" "${app:?}/tests/$app_name.IntegrationTests/Features/${reaction:?}"
  [[ "$events_existed" == true ]] || rmdir "$events"
  remove_module "$other_module"
  mv "$work/module.backup" "$module_file"
  mv "$work/Program-event.backup" "$app_src/Program.cs"
}

echo "==> Checking that HS003 fails both check scripts on an endpoint that receives AppDbContext"
probe_endpoint_database_access Bookings

echo "==> Checking that HS015 fails both check scripts on an endpoint that BookingsModule never maps"
probe_unmapped_endpoint Bookings

echo "==> Checking that HS004 fails both check scripts on a slice whose command is not named after it"
probe_slice_structure Bookings
probe_system_command_rule Bookings

echo "==> Checking that HS004, HS014, HS016 and HS018 accept a reaction slice and fail both check scripts on a faulty one"
probe_reaction_slice Bookings

echo "==> Checking that HS005 fails both check scripts on a command handler that returns its aggregate in a DTO"
probe_command_response Bookings

echo "==> Checking that HS006 fails both check scripts on a query handler that loads its aggregate without AsNoTracking"
probe_query_tracking Bookings

echo "==> Checking that HS007 fails both check scripts on an aggregate whose state has a public setter"
probe_entity_state Bookings

echo "==> Checking that HS008 fails both check scripts on a child entity that holds another aggregate"
probe_aggregate_reference Bookings

echo "==> Checking that HS009 fails both check scripts on a domain event declared as a class"
probe_domain_event_record Bookings

echo "==> Checking that HS019 suggests a past fact for a domain event named as a command, without failing the check scripts"
probe_domain_event_name Bookings

echo "==> Checking that HS010 fails both check scripts on a slice handler and a domain type that read the system clock"
probe_system_clock Bookings

echo "==> Checking that HS011 fails both check scripts on a domain type that blocks on a task"
probe_blocking_call Bookings

echo "==> Checking the modes of both check scripts: arguments, --build and the TODO(hardslice): markers"
probe_check_modes Bookings "src/$app_name/Features/Bookings/ConfirmBooking/ConfirmBooking.cs" "tests/$app_name.IntegrationTests/Features/Bookings/ConfirmBooking/ConfirmBookingTests.cs"

echo "==> Checking the hooks of Claude Code: protected files, the build after an edit and the check before handing back"
probe_claude_hooks Bookings

echo "==> Checking that check --build restores once after an edit of a project that changes no package, then no more"
probe_restore_once

echo "==> Checking the hooks of Codex and Copilot CLI: protected files, the build after an edit and the check before handing back"
probe_codex_and_copilot_hooks Bookings

echo "==> Checking that the hooks of Claude Code, Codex and Copilot CLI refuse the files that would turn the analyzers off"
probe_analyzer_settings_hooks

echo "==> Checking the pre-commit hook and the CI of $app_name in a git repository: a commit that breaks a rule refused, a compliant one accepted, protected files"
probe_pre_commit_and_ci Bookings

echo "==> Checking that HS000 fails both check scripts on suppressions without justification, in a slice, the domain and a test project"
probe_suppression_justification Bookings

echo "==> Checking that HS902 fails both check scripts on a domain type mapped without contract"
probe_unclassified_entity Bookings

echo "==> Checking that HS901 fails both check scripts on a domain type that is both an entity and an event"
probe_ambiguous_type Bookings

echo "==> Checking that a missing or invalid hardslice.json fails the check scripts explicitly"
mv "$app/hardslice.json" "$work/hardslice.json.backup"
expect_failure "HS900: hardslice.json was not found in '[^']*' or above it." "docs/rules/HS900.md" "a missing hardslice.json" check.sh check.ps1
sed 's/ICommand`1, /ICommandX`1, /' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "HS900: contract 'contracts.command' of hardslice.json, '$app_name.Shared.Messaging.ICommandX.1', is not declared by assembly '$app_name'." "docs/rules/HS900.md" "a renamed command contract" check.sh check.ps1
# HS004 to HS006 need the handler contract: the build rejects its malformed value before the architecture tests run.
sed 's/IRequestHandler`2, /IRequestHandler`2 /' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "HS900: 'contracts.requestHandler' in hardslice.json is not '<metadata name>, <assembly>'." "docs/rules/HS900.md" "a malformed handler contract" check.sh check.ps1
# PB-067: the system command contract derives from the command contract, and a file may leave it out.
sed 's/ISystemCommand`1, /IRequest`1, /' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "HS900: contract 'contracts.systemCommand', 'IRequest', does not derive from contract 'contracts.command', 'ICommand'." "docs/rules/HS900.md" "a system command contract that is no command" check.sh
grep -v '"systemCommand": ' "$work/hardslice.json.backup" > "$app/hardslice.json"
if ! check_output="$(run_check check.sh 2>&1)"; then
  printf '%s\n' "$check_output" >&2
  echo "error: scripts/check.sh failed on a hardslice.json without the optional systemCommand contract." >&2
  exit 1
fi
echo "    a hardslice.json without systemCommand passes the check"
# The analyzers read the contracts only: the architecture tests reject the style.
# PB-055: a later format of hardslice.json is reported with the update to make, before its keys are read.
sed 's/"version": 1,/"version": 2, "baseline": {},/' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "HS900: hardslice.json declares version 2, which this version of HardSlice.Analyzers does not read: it reads version 1." "docs/rules/HS900.md" "a hardslice.json of a later version" check.sh
sed 's/"style": "slices"/"style": "layers"/' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "hardslice.json: 'style' must be" "docs/rules/HS017.md" "an invalid hardslice.json" check.ps1
mv "$work/hardslice.json.backup" "$app/hardslice.json"

# The package targets give the compiler the global config of the mode that Directory.Build.props sets.
echo "==> Checking that $app_name compiles with the global config of the All analysis mode"
evaluation="$(cd "$app" && dotnet msbuild "src/$app_name/$app_name.csproj" -getProperty:HardSliceAnalysisMode -getItem:EditorConfigFiles)"
if ! printf '%s\n' "$evaluation" | grep -Fq '"HardSliceAnalysisMode": "All"' ||
  ! printf '%s\n' "$evaluation" | grep -Fq 'HardSlice.All.globalconfig' ||
  printf '%s\n' "$evaluation" | grep -Eq 'HardSlice[.](Minimum|Recommended)[.]globalconfig'; then
  printf '%s\n' "$evaluation" >&2
  echo "error: $app_name does not compile with the global config of the All analysis mode alone." >&2
  exit 1
fi

echo "==> Checking that an unknown analysis mode fails both check scripts with the list of modes"
cp "$app/Directory.Build.props" "$work/Directory.Build.props.backup"
sed 's|<HardSliceAnalysisMode>All</HardSliceAnalysisMode>|<HardSliceAnalysisMode>Strict</HardSliceAnalysisMode>|' "$work/Directory.Build.props.backup" > "$app/Directory.Build.props"
if ! grep -Fq '<HardSliceAnalysisMode>Strict</HardSliceAnalysisMode>' "$app/Directory.Build.props"; then
  echo "error: the Directory.Build.props of $app_name does not set HardSliceAnalysisMode to All." >&2
  exit 1
fi
unknown_mode="HardSliceAnalysisMode is 'Strict', which is not a HardSlice analysis mode. The mode selects the active HardSlice rules: Minimum, Recommended or All."
for script in check.sh check.ps1; do
  if check_output="$(run_check "$script" 2>&1)"; then
    echo "error: scripts/$script passed with an unknown HardSliceAnalysisMode." >&2
    exit 1
  fi
  if ! printf '%s\n' "$check_output" | grep -Fq "$unknown_mode"; then
    printf '%s\n' "$check_output" >&2
    echo "error: scripts/$script failed on an unknown HardSliceAnalysisMode without listing the modes." >&2
    exit 1
  fi
done
echo "    $unknown_mode"
mv "$work/Directory.Build.props.backup" "$app/Directory.Build.props"

# The tool verifies the rules of the analyzers it ships with: another analyzer version stops both scripts before the build.
# PB-076: that error, and the one on a missing Directory.HardSlice.props, give the probable cause, a Fix: that regenerates the
# template under the name of the solution, and a Doc: to the section of getting-started.md that describes the HardSlice files.
probe_hardslice_files() {
  local doc="Doc: https://github.com/vincentaugerot/hardslice/blob/v$hardslice_version/docs/getting-started.md#when-check-stops-on-a-hardslice-file"
  local regenerate="'dotnet new hardslice-api -n $app_name --no-sample' with HardSlice.Templates $hardslice_version"
  local version_mismatch="error: Directory.HardSlice.props references HardSlice.Analyzers 0.0.1, but dotnet hardslice is version" script check_output expected
  echo "==> Checking that another HardSlice.Analyzers version fails both check scripts before the build, with a fix and its doc"
  cp -p "$app/Directory.HardSlice.props" "$work/Directory.HardSlice.props.backup"
  sed 's|<GlobalPackageReference Include="HardSlice.Analyzers" Version="[^"]*" />|<GlobalPackageReference Include="HardSlice.Analyzers" Version="0.0.1" />|' "$work/Directory.HardSlice.props.backup" > "$app/Directory.HardSlice.props"
  if ! grep -Fq '<GlobalPackageReference Include="HardSlice.Analyzers" Version="0.0.1" />' "$app/Directory.HardSlice.props"; then
    echo "error: the Directory.HardSlice.props of $app_name does not reference HardSlice.Analyzers." >&2
    exit 1
  fi
  for script in check.sh check.ps1; do
    if check_output="$(run_check "$script" 2>&1)"; then
      echo "error: scripts/$script passed with another HardSlice.Analyzers version." >&2
      exit 1
    fi
    if printf '%s\n' "$check_output" | grep -Fq "==> Building"; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script did not stop before the build on another HardSlice.Analyzers version." >&2
      exit 1
    fi
    for expected in "$version_mismatch" "Fix: a maintainer, since Directory.HardSlice.props and .config/dotnet-tools.json are protected, sets the 'hardslice.tool' entry of .config/dotnet-tools.json back to 0.0.1" \
      "$regenerate. $doc"; do
      if ! printf '%s\n' "$check_output" | grep -Fq -- "$expected"; then
        printf '%s\n' "$check_output" >&2
        echo "error: scripts/$script failed on another HardSlice.Analyzers version without '$expected'." >&2
        exit 1
      fi
    done
  done
  echo "    $version_mismatch ... Fix: ... $doc"

  rm "$app/Directory.HardSlice.props"
  expect_check_message "': it must reference HardSlice.Analyzers $hardslice_version, the version of dotnet hardslice. The solution was probably generated by another template or another version of dotnet hardslice" \
    "==> Building" "no Directory.HardSlice.props" --build
  expect_check_message "$regenerate, or sets the 'hardslice.tool' entry of .config/dotnet-tools.json back to the version that generated the solution, then runs 'dotnet tool restore'. $doc" \
    "==> Building" "no Directory.HardSlice.props"
  # PB-078: only a maintainer can fix it, so the Stop hook reports it once, then lets the agent hand back.
  run_claude_hook Stop "$(claude_payload Stop "" false)"
  if [[ "$hook_status" != 2 || "$hook_error" != "HardSlice: the check stops before the build on a protected HardSlice file of the solution"* ||
    "$hook_error" != *"error: Directory.HardSlice.props was not found in"* || "$hook_error" == *"relaunch 1 of 3"* ]]; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the Stop hook did not report the missing Directory.HardSlice.props once, as a failure for a maintainer, with code 2 (code $hook_status)." >&2
    exit 1
  fi
  run_claude_hook Stop "$(claude_payload Stop "" true)"
  if [[ "$hook_status" != 0 || -n "$hook_error" ]] || ! grep -Fq '"systemMessage":"HardSlice: the check stops on a protected HardSlice file of the solution' "$work/hook-output"; then
    printf '%s\n' "$hook_error" >&2
    echo "error: the Stop hook relaunched the agent a second time on the missing Directory.HardSlice.props (code $hook_status)." >&2
    exit 1
  fi
  echo "    Stop: the missing Directory.HardSlice.props reported once with code 2, then the agent hands back"
  restore_root_file Directory.HardSlice.props
}

probe_hardslice_files
probe_application_packages
probe_integration_isolation
probe_shared_folders check.sh

# A second module beside Bookings, generated by dotnet new hardslice-module and registered in Program.cs: the check passes.
probe_module_template Rooms MeetingRooms meeting-rooms

# Outside any slice, the contract of module Rooms is out of the analyzer's reach: only HS012 sees this reference.
echo "==> Checking that HS012 fails both check scripts when the generated module Rooms reaches into a slice of Bookings"
cp "$app_src/Features/Rooms/RoomsModule.cs" "$work/RoomsModule.cs.backup"
{
  printf 'using %s.Features.Bookings.GetBooking;\n\n' "$app_name"
  sed '$d' "$work/RoomsModule.cs.backup"
  cat <<EOF

    public static bool IsBooked(BookingDto? booking) => booking is { Status: "Confirmed" };
}
EOF
} > "$app_src/Features/Rooms/RoomsModule.cs"
expect_failure "HS012: 'RoomsModule' in module 'Rooms' uses 'BookingDto'" "docs/rules/HS012.md" "a module using a slice of another module" check.sh check.ps1
rm "$work/RoomsModule.cs.backup"
remove_module Rooms
mv "$work/Program.cs.backup" "$app_src/Program.cs"

probe_command_template Bookings ExtendBooking ArchiveBooking RescheduleBooking ImportBookings PurgeExpiredBookings RemindBookings CollectBookingFeeds
probe_recurring_commands Bookings CollectBookingStats PurgeBookingDrafts
probe_query_template Bookings GetBookingSummary ListBookingSummaries GetBookingsOnDate Booking \
  "Booking.Confirm(Guid.NewGuid(), new BookingPeriod(new DateTimeOffset(2099, 11, 2, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2099, 11, 2, 10, 0, 0, TimeSpan.Zero)), [])"
probe_slice_routes Timetable schedule ImportSchedule PurgeSchedule Bookings GetBookingTrip Booking \
  "Booking.Confirm(Guid.NewGuid(), new BookingPeriod(new DateTimeOffset(2099, 11, 2, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2099, 11, 2, 10, 0, 0, TimeSpan.Zero)), [])"
probe_protected_endpoints Bookings ProtectBooking ImportProtectedBookings GetProtectedBooking Booking \
  "Booking.Confirm(Guid.NewGuid(), new BookingPeriod(new DateTimeOffset(2099, 11, 2, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2099, 11, 2, 10, 0, 0, TimeSpan.Zero)), [])"
probe_aggregate_template Bookings BookingCategory BookingCategories remove --table BookingCategories
probe_entity_template Bookings Booking BookingNote BookingAuditEntry BookingAudit
probe_private_fields Bookings
probe_event_template Bookings BookingExtended BookingArchived Housekeeping Booking \
  "Booking.Confirm(Guid.NewGuid(), new BookingPeriod(new DateTimeOffset(2099, 11, 2, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2099, 11, 2, 10, 0, 0, TimeSpan.Zero)), [])"

echo "==> $app_name OK: it restores the HardSlice packages $hardslice_version from the local feed, passes both check scripts and the getting started path, holds the v0.1 deliverables, sends its commands through its own mediator, and the scripts fail without a container runtime, on HS000, HS001, HS003, HS004, HS005, HS006, HS007, HS008, HS009, HS010, HS011, HS012, HS013, HS014, HS015, HS016, HS017, HS018, HS901 and HS902 with their rule docs, on a missing or invalid hardslice.json and its contracts (HS900), on an unknown analysis mode, and on another analyzer version than the one of dotnet hardslice, which explains every rule, while they pass with the HS019 suggestion in the compiler log"

sample_name="$app_name"
blank_name="Blank"
blank="$work/$blank_name"

# Same package and hive as $app_name: generate-app.sh installed the template in $work/template-hive, next to the local nuget.config.
echo "==> Generating $blank_name with --no-sample"
dotnet new hardslice-api --name "$blank_name" --output "$blank" --no-sample --debug:custom-hive "$work/template-hive"

# Checked before any build: bin and obj would hold copies of the sources.
echo "==> Checking that $blank_name holds no trace of the Bookings sample"
# The rule sheets keep their Bookings examples: they document the rules, not the generated application.
if sample_files="$(grep -rIil --exclude-dir=rules booking "$blank")"; then
  printf '%s\n' "$sample_files" >&2
  echo "error: $blank_name mentions the Bookings sample outside docs/rules/." >&2
  exit 1
fi
# PB-056: their examples are illustrations; none sends the reader to a type or a file of the sample to follow or copy.
if sample_files="$(grep -rnE '[Ff]ollow `[^`]*Booking[^`]*`|as `[^`]*Booking[^`]*` does|`Booking(Configuration|sModule|ConfigurationTests)`|`(Confirm|Cancel|Get)Booking(Validator)?Tests`' "$blank/docs/rules")"; then
  printf '%s\n' "$sample_files" >&2
  echo "error: a rule sheet of $blank_name sends the reader to the Bookings sample, which --no-sample leaves out." >&2
  exit 1
fi
for sample_path in "src/$blank_name.Domain/Bookings" "src/$blank_name/Features" "src/$blank_name/Shared/Persistence/Migrations"; do
  if [[ -e "$blank/$sample_path" ]]; then
    echo "error: $blank_name contains $sample_path, which only the Bookings sample needs." >&2
    exit 1
  fi
done
if ! grep -Fq -- "- None yet: start with [Adding a first module](#adding-a-first-module)." "$blank/AGENTS.md" ||
  ! grep -Fxq "## Adding a first module" "$blank/AGENTS.md"; then
  echo "error: the AGENTS.md of $blank_name does not map an empty application with the steps of a first module." >&2
  exit 1
fi
check_agent_instructions "$blank" "$blank_name"
check_executable_scripts "$blank" "$blank_name"
"$repo_root/scripts/generated-workflow.sh" lint "$blank"
while read -r cited_path; do
  if [[ ! -e "$blank/$cited_path" ]]; then
    echo "error: the AGENTS.md of $blank_name cites $cited_path, which $blank_name does not contain." >&2
    exit 1
  fi
done < <(grep -o '`\(src\|tests\|docs\|scripts\)/[^`<>[:space:]]*`' "$blank/AGENTS.md" | tr -d '`' | sort -u)

echo "==> Verifying $blank_name with scripts/check.sh"
bash "$blank/scripts/check.sh"

echo "==> Verifying $blank_name with scripts/check.ps1"
pwsh -NoProfile -File "$blank/scripts/check.ps1"

echo "==> Checking that the model of $blank_name needs no migration, with the pinned dotnet-ef tool"
(
  cd "$blank"
  dotnet tool restore
  dotnet ef migrations has-pending-model-changes --project "src/$blank_name" --no-build
)

run_documented_path "$blank" "$blank_name" false

echo "==> Checking that each test project of $blank_name runs tests of the foundation"
(
  cd "$blank"
  for expected in \
    "$blank_name.Tests:Shared.Messaging.MediatorTests.Runs_the_behaviors_in_registration_order_around_the_handler" \
    "$blank_name.Tests:Shared.Behaviors.BehaviorsServiceCollectionExtensionsTests.Commands_go_through_logging_then_validation_then_the_transaction" \
    "$blank_name.Tests:Shared.Http.ExceptionHandlersTests.Answers_a_broken_rule_with_400_and_a_conflict_with_409" \
    "$blank_name.Tests:Shared.Http.ExplicitOffsetJsonConverterTests.Refuses_a_date_without_offset_or_that_is_no_ISO_8601_string" \
    "$blank_name.IntegrationTests:ProgramTests.Starts_and_answers_HTTP_requests_on_its_migrated_database" \
    "$blank_name.IntegrationTests:Shared.Behaviors.TransactionBehaviorTests.Answers_a_conflict_to_the_second_of_two_concurrent_creations_under_the_same_key" \
    "$blank_name.IntegrationTests:Shared.Behaviors.TransactionBehaviorTests.Keeps_the_exception_of_a_handler_that_translates_a_unique_violation_itself" \
    "$blank_name.IntegrationTests:Shared.Persistence.MigrationsTests.Model_has_no_changes_missing_from_the_migrations" \
    "$blank_name.ArchitectureTests:SliceTests.Slices_never_use_types_of_another_slice" \
    "$blank_name.ArchitectureTests:TestLayoutTests.Every_slice_has_tests_at_its_path"; do
    project="${expected%%:*}"
    test="${expected#*:}"
    listed_tests="$(dotnet test --project "tests/$project/$project.csproj" --no-build --list-tests)"
    if ! printf '%s\n' "$listed_tests" | grep -Fq ".$test"; then
      echo "error: $project has no test $test." >&2
      exit 1
    fi
  done
)

# The probes below run on the empty application: each rule must still find its target and fail both scripts.
app_name="$blank_name"
app="$blank"
app_src="$app/src/$app_name"
domain_project="$app/src/$app_name.Domain/$app_name.Domain.csproj"

probe_integration_isolation

echo "==> Checking that the HS016 analyzer fails both check scripts on a cross-slice reference in $app_name"
mkdir -p "$app_src/Features/Orders/PlaceOrder" "$app_src/Features/Orders/CancelOrder"
cat > "$app_src/Features/Orders/PlaceOrder/PlaceOrder.cs" <<EOF
namespace $app_name.Features.Orders.PlaceOrder;

internal static class PlaceOrderRules
{
    internal static int MaxLines => 20;
}
EOF
cat > "$app_src/Features/Orders/CancelOrder/CancelOrder.cs" <<EOF
namespace $app_name.Features.Orders.CancelOrder;

internal static class CancelOrderRules
{
    internal static int MaxLines => $app_name.Features.Orders.PlaceOrder.PlaceOrderRules.MaxLines;
}
EOF
expect_failure "error HS016: 'CancelOrderRules' in slice 'Orders/CancelOrder' uses 'PlaceOrderRules' from slice 'Orders/PlaceOrder'." "docs/rules/HS016.md" "a cross-slice reference in $app_name" check.sh check.ps1

echo "==> Checking that the HS016 architecture test reports the same reference in the compiled assembly of $app_name"
if check_output="$(
  cd "$app"
  dotnet build "$app_name.slnx" -p:RunAnalyzers=false 2>&1 &&
    dotnet test --project "tests/$app_name.ArchitectureTests/$app_name.ArchitectureTests.csproj" --no-build 2>&1
)"; then
  echo "error: the architecture tests of $app_name passed despite a cross-slice reference in the compiled assembly." >&2
  exit 1
fi
if ! printf '%s\n' "$check_output" | grep -Fq "HS016: 'CancelOrderRules' in slice 'Orders/CancelOrder' uses 'PlaceOrderRules' from slice 'Orders/PlaceOrder'."; then
  printf '%s\n' "$check_output" >&2
  echo "error: the architecture tests of $app_name failed without reporting the cross-slice reference as HS016." >&2
  exit 1
fi
rm -r "$app_src/Features/Orders"

echo "==> Checking that HS018 fails both check scripts on the first slice of $app_name without tests"
mkdir -p "$app_src/Features/Orders/ExportOrders"
cat > "$app_src/Features/Orders/ExportOrders/ExportOrders.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Orders.ExportOrders;

public sealed record ExportOrdersQuery : IQuery<string>;

internal sealed class ExportOrdersHandler : IRequestHandler<ExportOrdersQuery, string>
{
    public Task<string> Handle(ExportOrdersQuery query, CancellationToken cancellationToken) => Task.FromResult("text/csv");
}

internal static class ExportOrdersEndpoint;
EOF
expect_failure "HS018: slice 'Orders/ExportOrders' has no tests" "docs/rules/HS018.md" "the first slice of $app_name without tests" check.sh check.ps1
rm -r "$app_src/Features/Orders"

# The slice has a test folder, so HS018 passes: HS013, then HS014, see the command and its handler.
echo "==> Checking that HS013 and HS014 fail both check scripts on the first command of $app_name"
mkdir -p "$app_src/Features/Orders/PlaceOrder" "$app/tests/$app_name.Tests/Features/Orders/PlaceOrder"
cat > "$app_src/Features/Orders/PlaceOrder/PlaceOrder.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Orders.PlaceOrder;

public sealed record PlaceOrderCommand(Guid CustomerId) : ICommand<bool>;

internal sealed class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, bool>
{
    public Task<bool> Handle(PlaceOrderCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
}

internal static class PlaceOrderEndpoint;
EOF
cat > "$app/tests/$app_name.Tests/Features/Orders/PlaceOrder/PlaceOrderFixture.cs" <<EOF
namespace $app_name.Tests.Features.Orders.PlaceOrder;

internal static class PlaceOrderFixture
{
    internal static readonly Guid CustomerId = Guid.NewGuid();
}
EOF
expect_failure "HS013: command 'PlaceOrderCommand' in slice 'Orders/PlaceOrder' has parameters (CustomerId) but no validator." "docs/rules/HS013.md" "the first command of $app_name without a validator" check.sh check.ps1
cat > "$app_src/Features/Orders/PlaceOrder/PlaceOrderValidator.cs" <<EOF
using FluentValidation;

namespace $app_name.Features.Orders.PlaceOrder;

internal sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator() => RuleFor(command => command.CustomerId).NotEmpty();
}
EOF
expect_failure "HS014: command handler 'PlaceOrderHandler' in slice 'Orders/PlaceOrder' has no test" "docs/rules/HS014.md" "the first command handler of $app_name without tests" check.sh check.ps1
rm -r "$app_src/Features/Orders" "$app/tests/$app_name.Tests/Features/Orders"

probe_shared_folders check.sh check.ps1

echo "==> Checking that HS003 fails both check scripts on the first endpoint of $app_name that receives AppDbContext"
probe_endpoint_database_access Rooms

echo "==> Checking that HS015 fails both check scripts on the first endpoint of $app_name, which no RoomsModule maps"
probe_unmapped_endpoint Rooms

echo "==> Checking that HS004 fails both check scripts on the first slice of $app_name whose command is not named after it"
probe_slice_structure Rooms
probe_system_command_rule Rooms

echo "==> Checking that HS004, HS014, HS016 and HS018 accept the first reaction slice of $app_name and fail both check scripts on a faulty one"
probe_reaction_slice Rooms

echo "==> Checking that HS005 fails both check scripts on the first command handler of $app_name that returns its aggregate in a DTO"
probe_command_response Rooms

echo "==> Checking that HS006 fails both check scripts on the first query handler of $app_name that loads its aggregate without AsNoTracking"
probe_query_tracking Rooms

echo "==> Checking that HS007 fails both check scripts on the first aggregate of $app_name whose state has a public setter"
probe_entity_state Rooms

echo "==> Checking that HS008 fails both check scripts on a child entity of the first aggregate of $app_name that holds another aggregate"
probe_aggregate_reference Rooms

echo "==> Checking that HS009 fails both check scripts on the first domain event of $app_name declared as a class"
probe_domain_event_record Rooms

echo "==> Checking that HS019 suggests a past fact for the first domain event of $app_name named as a command, without failing the check scripts"
probe_domain_event_name Rooms

echo "==> Checking that HS010 fails both check scripts on the first slice handler and domain type of $app_name that read the system clock"
probe_system_clock Rooms

echo "==> Checking that HS011 fails both check scripts on the first domain type of $app_name that blocks on a task"
probe_blocking_call Rooms

echo "==> Checking the modes of both check scripts on $app_name: arguments, --build and the TODO(hardslice): markers"
probe_check_modes Rooms "src/$app_name/Shared/Behaviors/LoggingBehavior.cs" "tests/$app_name.Tests/Shared/Behaviors/LoggingBehaviorTests.cs"

echo "==> Checking that HS000 fails both check scripts on suppressions without justification in the first slice, domain type and test class of $app_name"
probe_suppression_justification Rooms

echo "==> Checking that HS902 fails both check scripts on the first domain type of $app_name mapped without contract"
probe_unclassified_entity Rooms
rm -r "$app/src/$app_name.Domain/Rooms" "$app_src/Features"

echo "==> Checking that HS901 fails both check scripts on the first domain type of $app_name that is both an entity and an event"
probe_ambiguous_type Rooms
rm -r "$app/src/$app_name.Domain/Rooms"

echo "==> Checking that HS001 fails both check scripts when the domain of $app_name depends on dependency injection"
cp "$domain_project" "$work/domain-project.backup"
cat > "$domain_project" <<EOF
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

</Project>
EOF
cat > "$app/src/$app_name.Domain/Hs001Probe.cs" <<EOF
using Microsoft.Extensions.DependencyInjection;

namespace $app_name.Domain;

public static class Hs001Probe
{
    public static bool IsEmpty(IServiceCollection services) => services.Count == 0;
}
EOF
expect_failure "HS001: 'Hs001Probe' in the domain uses 'IServiceCollection'" "docs/rules/HS001.md" "a domain type of $app_name using dependency injection" check.sh check.ps1
mv "$work/domain-project.backup" "$domain_project"
rm "$app/src/$app_name.Domain/Hs001Probe.cs"

# The first module of $app_name is generated by dotnet new hardslice-module, as "Adding a first module" of AGENTS.md says, and
# registered in Program.cs with the lines it prints: dotnet new hardslice-command then adds its first command slice, and
# hardslice-query its first query slices.
probe_module_template Rooms MeetingRooms meeting-rooms

probe_command_template Rooms RenameRoom CloseRoom ResizeRoom ImportRooms CountRooms RemindRooms CollectRoomFeeds
probe_recurring_commands Rooms CollectRoomStats PurgeRoomDrafts

# The aggregate that the query slices read is generated by dotnet new hardslice-aggregate in the domain of Rooms, mapped by the
# module and migrated with the command it prints, as "Adding a first module" of AGENTS.md says.
probe_aggregate_template Rooms Room Rooms keep
# Its child entities, generated by dotnet new hardslice-entity in both forms, then removed with their migration.
probe_entity_template Rooms Room RoomSeat RoomVisit RoomVisitLog
probe_query_template Rooms GetRoomSummary ListRoomSummaries GetRoomsOnDate Room 'Room.Create()'
probe_slice_routes Timetable schedule ImportSchedule PurgeSchedule Rooms GetRoomTrip Room 'Room.Create()'
probe_protected_endpoints Rooms ProtectRoom ImportProtectedRooms GetProtectedRoom Room 'Room.Create()'
# The events of Rooms are generated by dotnet new hardslice-event, with a reaction in Rooms and one in a second module, both
# completed with the aggregate Room.
probe_event_template Rooms RoomRenamed RoomClosed Housekeeping Room 'Room.Create()'
rm -r "$app_src/Shared/Persistence/Migrations"

# PB-073: the module Rooms, its aggregate, its child entities, its slices and its events leave no .gitkeep in its folders.
echo "==> Checking that the module Rooms of $app_name and the elements generated in it leave no .gitkeep"
if [[ ! -f "$app/src/$app_name.Domain/Rooms/Room.cs" ]]; then
  echo "error: the domain of the module Rooms of $app_name has no aggregate Room to check the folders of the module with." >&2
  exit 1
fi
expect_no_placeholder "the generation of the module Rooms, its aggregate, its entities, its slices and its events"
echo "    $(module_folders Rooms | while read -r folder; do [[ ! -d "$app/$folder" ]] || printf '%s ' "$folder"; done)hold their files alone"

remove_module Rooms
mv "$work/Program.cs.backup" "$app_src/Program.cs"
if [[ -e "$app_src/Features" ]]; then
  echo "error: removing the module Rooms of $app_name left src/$app_name/Features." >&2
  exit 1
fi

# PB-052: dotnet new turns the name of the application into one safe namespace, <form>, which names every folder, project,
# assembly and namespace; only the output folder keeps <name>. Generates <name> with the remaining options next to $sample_name,
# from the same hive, and checks that form, that the check passes, that a module and a command slice generated by the item
# templates land in its projects and pass the check once completed, and that a cross-slice reference fails the analyzer and the
# architecture test, whose namespace prefix comes from the name of the assembly.
probe_application_name() {
  local name="$1"
  local form="$2"
  local module="$3"
  local command="$4"
  shift 4
  local expected
  local check_output

  app="$work/$name"
  app_name="$form"
  app_src="$app/src/$form"

  echo "==> Generating $name $*, whose single form is $form"
  dotnet new hardslice-api --name "$name" --output "$app" "$@" --debug:custom-hive "$work/template-hive"
  for expected in "$form.slnx" "src/$form/$form.csproj" "src/$form.Domain/$form.Domain.csproj" "tests/$form.Tests/$form.Tests.csproj" \
    "tests/$form.IntegrationTests/$form.IntegrationTests.csproj" "tests/$form.IntegrationTests/AppFactory.cs" \
    "tests/$form.ArchitectureTests/$form.ArchitectureTests.csproj"; do
    if [[ ! -f "$app/$expected" ]]; then
      echo "error: $name has no $expected: its folders and projects do not take its single form $form." >&2
      exit 1
    fi
  done
  if [[ "$name" != "$form" ]] && { find "$app" -mindepth 1 -name "*$name*" | grep . || grep -rIlF -- "$name" "$app"; }; then
    echo "error: $name keeps its raw name in the paths or files above instead of $form." >&2
    exit 1
  fi
  if ! grep -Fq "\"command\": \"$form.Shared.Messaging.ICommand\`1, $form\"," "$app/hardslice.json"; then
    echo "error: the contracts of hardslice.json in $name do not name the namespaces and assemblies $form." >&2
    exit 1
  fi

  echo "==> Verifying $name with scripts/check.sh"
  bash "$app/scripts/check.sh"

  echo "==> Generating the module $module and the command slice $module/$command of $name with the item templates"
  generate_module "$module"
  register_module "$module_output"
  generate_command "$module" "$command"
  register_slice hardslice-command "$command_output"
  complete_command "$module" "$command"
  bash "$app/scripts/check.sh"

  echo "==> Checking that HS016 fails the check of $name and its architecture test on a cross-slice reference"
  mkdir -p "$app_src/Features/$module/Hs016Probe"
  cat > "$app_src/Features/$module/Hs016Probe/Hs016Probe.cs" <<EOF
namespace $form.Features.$module.Hs016Probe;

internal static class Hs016Probe
{
    internal static readonly Type Target = typeof($form.Features.$module.$command.${command}Handler);
}
EOF
  expect_failure "error HS016: " "docs/rules/HS016.md" "a cross-slice reference in $name" check.sh
  if check_output="$(
    cd "$app"
    dotnet build "$form.slnx" -p:RunAnalyzers=false 2>&1 &&
      dotnet test --project "tests/$form.ArchitectureTests/$form.ArchitectureTests.csproj" --no-build 2>&1
  )"; then
    echo "error: the architecture tests of $name passed despite a cross-slice reference in the compiled assembly." >&2
    exit 1
  fi
  if ! printf '%s\n' "$check_output" | grep -Fq "HS016: 'Hs016Probe' in slice '$module/Hs016Probe' uses '${command}Handler' from slice '$module/$command'."; then
    printf '%s\n' "$check_output" >&2
    echo "error: the architecture tests of $name failed without reporting the cross-slice reference as HS016." >&2
    exit 1
  fi
  echo "    architecture test: HS016 on '${command}Handler' in $form"
  rm -r "$app_src/Features/$module/Hs016Probe"
}

probe_application_name Acme.Rooms Acme.Rooms Rooms RenameRoom
probe_application_name my-app my_app Guests RegisterGuest --no-sample

echo "==> Template OK: $sample_name and $blank_name, generated with --no-sample, pass both check scripts and the getting started path, and seeded violations fail them in both; Acme.Rooms and my-app, generated as my_app, pass the check with a generated module and command slice, and fail it on HS016"
