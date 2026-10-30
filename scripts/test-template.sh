#!/usr/bin/env bash
# Packs HardSlice, installs the hardslice-api template, generates a solution in a temporary directory,
# verifies it with scripts/check.sh and check.ps1, integration tests included, checks that it holds every v0.1 deliverable,
# runs the "Run the API" path of README.md on it (PostgreSQL container, migrations, API, a booking created then read), checks its migrations with the pinned dotnet-ef,
# checks that commands go through its own mediator and pipeline and that queries read without tracking, proves both scripts name the missing container runtime,
# then proves a seeded violation of HS001, HS012, HS013, HS014, HS016, HS017, HS018, HS901 and HS902 fails both, naming the rule and a rule doc that exists,
# and that a missing or invalid hardslice.json fails them explicitly, with HS900 for its contracts.
# Finally generates the template with --no-sample, proves the result holds no trace of the Bookings sample, passes both scripts and the README path,
# and that seeded violations of HS001, HS013, HS014, HS016, HS017, HS018, HS901 and HS902 still fail them: without the sample, the rules keep their targets.
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
    export DOTNET_CLI_HOME="$work/dotnet-cli"
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

echo "==> Checking the generated migrations with the pinned dotnet-ef tool"
(
  cd "$app"
  # The restored tool lives under $work: keep its resolver cache there too, so
  # deleting this run cannot leave stale tool paths in the user's shared cache.
  export DOTNET_CLI_HOME="$work/dotnet-cli"
  dotnet tool restore
  dotnet ef migrations has-pending-model-changes --project "src/$app_name" --no-build
)

echo "==> Checking that $app_name holds every v0.1 deliverable of the specification"
for deliverable in \
  AGENTS.md .editorconfig hardslice.json scripts/check.sh scripts/check.ps1 docs/rules/HS001.md docs/rules/HS012.md \
  docs/rules/HS013.md docs/rules/HS014.md docs/rules/HS016.md docs/rules/HS017.md docs/rules/HS018.md \
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

echo "==> Checking that HS016 fails both check scripts on a public handler"
cat > "$app_src/Features/Bookings/CancelBooking/Hs016VisibilityProbe.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.CancelBooking;

public sealed record Hs016ProbeQuery : IQuery<bool>;

public sealed class Hs016ProbeHandler : IRequestHandler<Hs016ProbeQuery, bool>
{
    public Task<bool> Handle(Hs016ProbeQuery query, CancellationToken cancellationToken) => Task.FromResult(true);
}
EOF
expect_failure "HS016: handler 'Hs016ProbeHandler'" "docs/rules/HS016.md" "a public handler" check.sh check.ps1
rm "$app_src/Features/Bookings/CancelBooking/Hs016VisibilityProbe.cs"

echo "==> Checking protected handlers exposed through public containers"
cat > "$app_src/Features/Bookings/CancelBooking/Hs016NestedProbe.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.CancelBooking;

public class Hs016ExtensionPoint
{
    protected sealed class Hs016ProtectedHandler : IRequestHandler<CancelBookingCommand, bool>
    {
        public Task<bool> Handle(CancelBookingCommand command, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
EOF
expect_failure "HS016: handler 'Hs016ProtectedHandler'" "docs/rules/HS016.md" "a protected handler reachable from another assembly" check.sh check.ps1
rm "$app_src/Features/Bookings/CancelBooking/Hs016NestedProbe.cs"

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
cat > "$app_src/Features/Bookings/CancelBooking/Hs013Probe.cs" <<EOF
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.CancelBooking;

public sealed record Hs013ProbeCommand(Guid BookingId, string Reason) : ICommand<bool>;
EOF
expect_failure "HS013: command 'Hs013ProbeCommand' in slice 'Bookings/CancelBooking' has parameters (BookingId, Reason) but no validator." "docs/rules/HS013.md" "a command with parameters but no validator" check.sh check.ps1
rm "$app_src/Features/Bookings/CancelBooking/Hs013Probe.cs"

# A specialized or constrained validator must not cover every construction of an open command.
echo "==> Checking that HS013 preserves generic arguments and validator constraints"
cat > "$app_src/Features/Bookings/CancelBooking/Hs013GenericProbe.cs" <<EOF
using FluentValidation;
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.CancelBooking;

public sealed record Hs013GenericCommand<T>(T Value) : ICommand<bool>;

internal sealed class Hs013IntValidator : AbstractValidator<Hs013GenericCommand<int>>
{
    public Hs013IntValidator() => RuleFor(command => command.Value).GreaterThan(0);
}
EOF
expect_failure "HS013: command 'Hs013GenericCommand'" "docs/rules/HS013.md" "a generic command with only an int validator" check.sh check.ps1
cat > "$app_src/Features/Bookings/CancelBooking/Hs013GenericProbe.cs" <<EOF
using FluentValidation;
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.CancelBooking;

public sealed record Hs013GenericCommand<T>(T Value) : ICommand<bool>;

internal sealed class Hs013StructValidator<T> : AbstractValidator<Hs013GenericCommand<T>> where T : struct
{
    public Hs013StructValidator() => RuleFor(command => command.Value).NotEmpty();
}
EOF
expect_failure "HS013: command 'Hs013GenericCommand'" "docs/rules/HS013.md" "a generic command with only a struct-constrained validator" check.sh check.ps1
rm "$app_src/Features/Bookings/CancelBooking/Hs013GenericProbe.cs"

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
cat > "$app_src/Features/Bookings/CancelBooking/Hs013GenericProbe.cs" <<EOF
using FluentValidation;
using $app_name.Shared.Messaging;

namespace $app_name.Features.Bookings.CancelBooking;

public sealed record Hs013GenericCommand<T>(T Value) : ICommand<bool>;

internal sealed class Hs013GenericValidator<T> : AbstractValidator<Hs013GenericCommand<T>>
{
    public Hs013GenericValidator() => RuleFor(command => command.Value).NotEmpty();
}
EOF
expect_architecture_tests_pass "an unconstrained generic command with an unconstrained validator"
rm "$app_src/Features/Bookings/CancelBooking/Hs013GenericProbe.cs"

echo "==> Checking that HS018 fails both check scripts on a slice without tests"
mkdir -p "$app_src/Features/Bookings/ExportBookings"
cat > "$app_src/Features/Bookings/ExportBookings/ExportBookings.cs" <<EOF
namespace $app_name.Features.Bookings.ExportBookings;

internal static class ExportBookingsFormat
{
    internal const string ContentType = "text/csv";
}
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

echo "==> Checking that HS902 fails both check scripts on a domain type mapped without contract"
probe_unclassified_entity Bookings

echo "==> Checking that HS901 fails both check scripts on a domain type that is both an entity and an event"
probe_ambiguous_type Bookings

echo "==> Checking that a missing or invalid hardslice.json fails the check scripts explicitly"
mv "$app/hardslice.json" "$work/hardslice.json.backup"
expect_failure "HS900: hardslice.json was not found in '[^']*' or above it." "docs/rules/HS900.md" "a missing hardslice.json" check.sh check.ps1
sed 's/ICommand`1, /ICommandX`1, /' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "HS900: contract 'contracts.command' of hardslice.json, '$app_name.Shared.Messaging.ICommandX.1', is not declared by assembly '$app_name'." "docs/rules/HS900.md" "a renamed command contract" check.sh check.ps1
# No analyzer rule needs the handler contract yet: the architecture tests still reject its malformed value.
sed 's/IRequestHandler`2, /IRequestHandler`2 /' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "hardslice.json: 'contracts.requestHandler' is" "docs/rules/HS900.md" "a malformed handler contract" check.ps1
# The analyzers read the contracts only: the architecture tests reject the style.
sed 's/"style": "slices"/"style": "layers"/' "$work/hardslice.json.backup" > "$app/hardslice.json"
expect_failure "hardslice.json: 'style' must be" "docs/rules/HS017.md" "an invalid hardslice.json" check.ps1
mv "$work/hardslice.json.backup" "$app/hardslice.json"

echo "==> $app_name OK: it passes both check scripts and the README path, holds the v0.1 deliverables, sends its commands through its own mediator, and the scripts fail without a container runtime, on HS001, HS012, HS013, HS014, HS016, HS017, HS018, HS901 and HS902 with their rule docs, and on a missing or invalid hardslice.json and its contracts (HS900)"

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
  export DOTNET_CLI_HOME="$work/dotnet-cli"
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
namespace $app_name.Features.Orders.ExportOrders;

internal static class ExportOrdersFormat
{
    internal const string ContentType = "text/csv";
}
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
