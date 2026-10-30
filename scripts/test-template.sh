#!/usr/bin/env bash
# Packs HardSlice, installs the hardslice-api template, generates a solution in a temporary directory,
# verifies it with scripts/check.sh and check.ps1, integration tests included, checks that it holds every v0.1 deliverable,
# runs the "Run the API" path of README.md on it (PostgreSQL container, migrations, API, a booking created then read), checks its migrations with the pinned dotnet-ef,
# checks that commands go through its own mediator and pipeline and that queries read without tracking, proves both scripts name the missing container runtime,
# then proves a seeded violation of HS001, HS003, HS004, HS005, HS010, HS011, HS012, HS013, HS014, HS015, HS016, HS017, HS018, HS901 and HS902 fails both, naming the rule and a rule doc that exists,
# that a missing or invalid hardslice.json fails them explicitly, with HS900 for its contracts, that the solution compiles with the All analysis mode,
# that an unknown HardSliceAnalysisMode fails them with the list of modes, and that dotnet hardslice, which the scripts launch,
# is pinned to the version of the analyzers, refuses another one before the build and explains every rule with the sheet of the template.
# Finally generates the template with --no-sample, proves the result holds no trace of the Bookings sample, passes both scripts and the README path,
# and that seeded violations of HS001, HS003, HS004, HS005, HS010, HS011, HS013, HS014, HS015, HS016, HS017, HS018, HS901 and HS902 still fail them: without the sample, the rules keep their targets.
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

"$repo_root/scripts/generate-app.sh" "$work"

if ! command -v pwsh > /dev/null; then
  echo "error: pwsh is required to verify scripts/check.ps1." >&2
  exit 1
fi

# One container for both variants, started as README.md documents it, except for its name and a free host port instead of 5432.
# Each variant migrates the database named by its own connection string.
start_documented_postgres() {
  [[ -z "$postgres_container" ]] || return 0
  echo "==> Starting PostgreSQL as README.md documents it, on a free port"
  postgres_container="$(docker run --detach --env POSTGRES_PASSWORD=postgres --publish 127.0.0.1::5432 postgres:18-alpine)"
  postgres_port="$(docker port "$postgres_container" 5432/tcp | head -n 1 | sed 's/.*://')"
  # The wait of README.md: TCP only, as the image's initialization server listens on its Unix socket, then restarts.
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

# Runs "Run the API" of README.md on a solution its check scripts have built: tool restore, migrations, dotnet run in Development,
# then, with the sample, the booking of the README created, read at its Location, and refused when posted again.
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
    echo "error: $settings no longer matches the PostgreSQL container of README.md: $generated_connection_string" >&2
    exit 1
  fi
  connection_string="${generated_connection_string/Port=5432;/Port=$postgres_port;}"

  echo "==> Applying the migrations of $app_name to PostgreSQL as README.md documents it"
  (
    cd "$app"
    export ConnectionStrings__Database="$connection_string"
    dotnet tool restore
    dotnet ef database update --project "src/$app_name"
  )

  echo "==> Starting the API of $app_name as README.md documents it"
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

  echo "==> Confirming then reading the booking of README.md with $app_name"
  status="$(curl --silent --show-error --output /dev/null --dump-header "$headers" --write-out '%{http_code}' \
    -X POST "$api_url/bookings" -H "Content-Type: application/json" -d "$booking")"
  location="$(sed -n 's/^[Ll]ocation: *\([^[:space:]]*\).*/\1/p' "$headers" | tr -d '\r')"
  if [[ "$status" != 201 || ! "$location" =~ ^/bookings/[0-9a-f-]{36}$ ]]; then
    cat "$api_log" >&2
    echo "error: confirming the booking of README.md answered $status with Location '$location' instead of 201 and /bookings/<id>." >&2
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
    echo "error: posting the booking of README.md again answered $status instead of 409." >&2
    exit 1
  fi
  stop_api
  echo "    $body"
}

# The check scripts are run from another directory: they must locate the solution themselves.
# dotnet new does not preserve the executable bit, hence the documented 'bash scripts/check.sh' form.
echo "==> Verifying the generated solution with scripts/check.sh"
bash "$app/scripts/check.sh"

echo "==> Verifying the generated solution with scripts/check.ps1"
pwsh -NoProfile -File "$app/scripts/check.ps1"

# The check scripts have restored the tool: it must be the version of the analyzers, and print the sheets of the template as it ships them.
echo "==> Checking that dotnet hardslice is pinned to the version of the analyzers and explains every rule"
(
  cd "$app"
  analyzer_version="$(sed -n 's/.*<GlobalPackageReference Include="HardSlice.Analyzers" Version="\([^"]*\)".*/\1/p' Directory.Packages.props)"
  tool_version="$(dotnet hardslice --version)"
  if [[ -z "$analyzer_version" || "$tool_version" != "$analyzer_version" ]]; then
    echo "error: dotnet hardslice is version '$tool_version' but $app_name references HardSlice.Analyzers '$analyzer_version'." >&2
    exit 1
  fi
  for sheet in "$repo_root"/src/HardSlice.Templates/content/hardslice-api/docs/rules/HS*.md; do
    rule="$(basename "$sheet" .md)"
    if ! dotnet hardslice explain "$rule" | cmp --silent - "$sheet"; then
      echo "error: dotnet hardslice explain $rule does not print the sheet of the template." >&2
      exit 1
    fi
  done
  explain_status=0
  explain_output="$(dotnet hardslice explain HS999 2>&1)" || explain_status=$?
  if [[ "$explain_status" != 2 || "$explain_output" != *"Rules with a sheet: HS001, "* ]]; then
    printf '%s\n' "$explain_output" >&2
    echo "error: dotnet hardslice explain HS999 exited $explain_status instead of 2 with the list of rules." >&2
    exit 1
  fi
  echo "    dotnet hardslice $tool_version explains $(ls docs/rules | wc -l) rules"
)

echo "==> Checking the generated migrations with the pinned dotnet-ef tool"
(
  cd "$app"
  dotnet tool restore
  dotnet ef migrations has-pending-model-changes --project "src/$app_name" --no-build
)

echo "==> Checking that $app_name holds every v0.1 deliverable of the specification"
for deliverable in \
  AGENTS.md .editorconfig hardslice.json scripts/check.sh scripts/check.ps1 docs/rules/HS001.md docs/rules/HS003.md docs/rules/HS004.md docs/rules/HS005.md docs/rules/HS010.md docs/rules/HS011.md docs/rules/HS012.md \
  docs/rules/HS013.md docs/rules/HS014.md docs/rules/HS015.md docs/rules/HS016.md docs/rules/HS017.md docs/rules/HS018.md \
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
    "Features.Bookings.ConfirmBooking.ConfirmBookingTests.Rejects_a_command_without_room_with_a_validation_problem_before_its_handler" \
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

  if [[ ! -f "$app/$doc" ]]; then
    echo "error: $doc does not exist from the generated solution root." >&2
    exit 1
  fi
  for script in "$@"; do
    if check_output="$(run_check "$script" 2>&1)"; then
      echo "error: scripts/$script passed despite $description." >&2
      exit 1
    fi
    diagnostic="$(printf '%s\n' "$check_output" | grep -o "$marker.* Doc: '[^']*'" | head -n 1 || true)"
    if [[ -z "$diagnostic" ]]; then
      printf '%s\n' "$check_output" >&2
      echo "error: scripts/$script failed on $description without a \"$marker\" diagnostic announcing a Doc path." >&2
      exit 1
    fi
    if [[ "$diagnostic" != *" Doc: '$doc'" ]]; then
      echo "error: scripts/$script reports \"$diagnostic\", which does not announce $doc." >&2
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

# Outside any slice, the contract of module Rooms is out of the analyzer's reach: only HS012 sees this reference.
echo "==> Checking that HS012 fails both check scripts when a module reaches into a slice of another module"
mkdir -p "$app_src/Features/Rooms"
cat > "$app_src/Features/Rooms/RoomsModule.cs" <<EOF
using $app_name.Features.Bookings.GetBooking;

namespace $app_name.Features.Rooms;

public static class RoomsModule
{
    public static bool IsBooked(BookingDto? booking) => booking is { Status: "Confirmed" };
}
EOF
expect_failure "HS012: 'RoomsModule' in module 'Rooms' uses 'BookingDto'" "docs/rules/HS012.md" "a module using a slice of another module" check.sh check.ps1
rm -r "$app_src/Features/Rooms"

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

echo "==> Checking that HS017 fails the check script on a folder of Shared that hardslice.json does not allow"
mkdir -p "$app_src/Shared/Helpers"
cat > "$app_src/Shared/Helpers/TextHelpers.cs" <<EOF
namespace $app_name.Shared.Helpers;

internal static class TextHelpers
{
    internal static string Normalize(string value) => value.Trim();
}
EOF
expect_failure "HS017: 'TextHelpers' is in 'Shared/Helpers'" "docs/rules/HS017.md" "a Shared folder that hardslice.json does not allow" check.sh
rm -r "$app_src/Shared/Helpers"

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
# Writes the Hs013Generic slice and its test, with the declaration of its validator and the body of the validator's constructor.
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

public sealed record Hs013GenericCommand<T>(T Value) : ICommand<bool>;

internal sealed class Hs013GenericHandler<T> : IRequestHandler<Hs013GenericCommand<T>, bool>
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
expect_failure "HS014: command handler 'ArchiveBookingHandler' in slice 'Bookings/ArchiveBooking' has no test" "docs/rules/HS014.md" "a command handler without tests" check.sh check.ps1

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
  expect_failure "HS902: 'Room' is mapped as an EF Core entity by 'RoomConfiguration' but implements neither 'IEntity' nor 'IAggregateRoot'." "docs/rules/HS902.md" "a domain type of $app_name mapped without contract" check.sh check.ps1
  rm "$app/src/$app_name.Domain/$module/Room.cs" "$app_src/Features/$module/RoomConfiguration.cs"
}

# Seeds a domain type of module <module> that is both an entity and a domain event: the analyzer of the domain reports HS901.
probe_ambiguous_type() {
  local module="$1"
  mkdir -p "$app/src/$app_name.Domain/$module"
  cat > "$app/src/$app_name.Domain/$module/RoomSnapshot.cs" <<EOF
namespace $app_name.Domain.$module;

public sealed class RoomSnapshot : IAggregateRoot, IDomainEvent;
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

echo "==> Checking that HS003 fails both check scripts on an endpoint that receives AppDbContext"
probe_endpoint_database_access Bookings

echo "==> Checking that HS015 fails both check scripts on an endpoint that BookingsModule never maps"
probe_unmapped_endpoint Bookings

echo "==> Checking that HS004 fails both check scripts on a slice whose command is not named after it"
probe_slice_structure Bookings

echo "==> Checking that HS005 fails both check scripts on a command handler that returns its aggregate in a DTO"
probe_command_response Bookings

echo "==> Checking that HS010 fails both check scripts on a slice handler and a domain type that read the system clock"
probe_system_clock Bookings

echo "==> Checking that HS011 fails both check scripts on a domain type that blocks on a task"
probe_blocking_call Bookings

echo "==> Checking that HS902 fails both check scripts on a domain type mapped without contract"
probe_unclassified_entity Bookings

echo "==> Checking that HS901 fails both check scripts on a domain type that is both an entity and an event"
probe_ambiguous_type Bookings

echo "==> Checking that a missing or invalid hardslice.json fails the check scripts explicitly"
mv "$app/hardslice.json" "$work/hardslice.json.backup"
expect_failure "HS900: hardslice.json was not found in '[^']*' or above it." "docs/rules/HS900.md" "a missing hardslice.json" check.sh check.ps1
sed 's/ICommand`1, /ICommandX`1, /' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "HS900: contract 'contracts.command' of hardslice.json, '$app_name.Shared.Messaging.ICommandX.1', is not declared by assembly '$app_name'." "docs/rules/HS900.md" "a renamed command contract" check.sh check.ps1
# HS004 and HS005 need the handler contract: the build rejects its malformed value before the architecture tests run.
sed 's/IRequestHandler`2, /IRequestHandler`2 /' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "HS900: 'contracts.requestHandler' in hardslice.json is not '<metadata name>, <assembly>'." "docs/rules/HS900.md" "a malformed handler contract" check.sh check.ps1
# The analyzers read the contracts only: the architecture tests reject the style.
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
echo "==> Checking that another HardSlice.Analyzers version fails both check scripts before the build"
cp "$app/Directory.Packages.props" "$work/Directory.Packages.props.backup"
sed 's|<GlobalPackageReference Include="HardSlice.Analyzers" Version="[^"]*" />|<GlobalPackageReference Include="HardSlice.Analyzers" Version="0.0.1" />|' "$work/Directory.Packages.props.backup" > "$app/Directory.Packages.props"
if ! grep -Fq '<GlobalPackageReference Include="HardSlice.Analyzers" Version="0.0.1" />' "$app/Directory.Packages.props"; then
  echo "error: the Directory.Packages.props of $app_name does not reference HardSlice.Analyzers." >&2
  exit 1
fi
version_mismatch="error: Directory.Packages.props references HardSlice.Analyzers 0.0.1, but dotnet hardslice is version"
for script in check.sh check.ps1; do
  if check_output="$(run_check "$script" 2>&1)"; then
    echo "error: scripts/$script passed with another HardSlice.Analyzers version." >&2
    exit 1
  fi
  if ! printf '%s\n' "$check_output" | grep -Fq "$version_mismatch" || printf '%s\n' "$check_output" | grep -Fq "==> Building"; then
    printf '%s\n' "$check_output" >&2
    echo "error: scripts/$script did not stop before the build on another HardSlice.Analyzers version." >&2
    exit 1
  fi
done
echo "    $version_mismatch ..."
mv "$work/Directory.Packages.props.backup" "$app/Directory.Packages.props"

echo "==> $app_name OK: it passes both check scripts and the README path, holds the v0.1 deliverables, sends its commands through its own mediator, and the scripts fail without a container runtime, on HS001, HS003, HS004, HS005, HS010, HS011, HS012, HS013, HS014, HS015, HS016, HS017, HS018, HS901 and HS902 with their rule docs, on a missing or invalid hardslice.json and its contracts (HS900), on an unknown analysis mode, and on another analyzer version than the one of dotnet hardslice, which explains every rule"

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
    "$blank_name.IntegrationTests:ProgramTests.Starts_and_answers_HTTP_requests_on_its_migrated_database" \
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

echo "==> Checking that HS017 fails both check scripts on a folder of Shared in $app_name that hardslice.json does not allow"
mkdir -p "$app_src/Shared/Helpers"
cat > "$app_src/Shared/Helpers/TextHelpers.cs" <<EOF
namespace $app_name.Shared.Helpers;

internal static class TextHelpers
{
    internal static string Normalize(string value) => value.Trim();
}
EOF
expect_failure "HS017: 'TextHelpers' is in 'Shared/Helpers'" "docs/rules/HS017.md" "a Shared folder of $app_name that hardslice.json does not allow" check.sh check.ps1
rm -r "$app_src/Shared/Helpers"

echo "==> Checking that HS003 fails both check scripts on the first endpoint of $app_name that receives AppDbContext"
probe_endpoint_database_access Rooms

echo "==> Checking that HS015 fails both check scripts on the first endpoint of $app_name, which no RoomsModule maps"
probe_unmapped_endpoint Rooms

echo "==> Checking that HS004 fails both check scripts on the first slice of $app_name whose command is not named after it"
probe_slice_structure Rooms

echo "==> Checking that HS005 fails both check scripts on the first command handler of $app_name that returns its aggregate in a DTO"
probe_command_response Rooms

echo "==> Checking that HS010 fails both check scripts on the first slice handler and domain type of $app_name that read the system clock"
probe_system_clock Rooms

echo "==> Checking that HS011 fails both check scripts on the first domain type of $app_name that blocks on a task"
probe_blocking_call Rooms

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

echo "==> Template OK: $sample_name and $blank_name, generated with --no-sample, pass both check scripts and the README path, and seeded violations fail them in both"
