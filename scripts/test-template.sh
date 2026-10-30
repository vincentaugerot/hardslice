#!/usr/bin/env bash
# Packs HardSlice, installs the hardslice-api template, generates a solution in a temporary directory,
# verifies it with scripts/check.sh and check.ps1, integration tests included, checks its migrations with the pinned dotnet-ef,
# checks that commands go through its own mediator and pipeline and that queries read without tracking, proves both scripts name the missing container runtime,
# then proves a seeded violation of HS001, HS012, HS013, HS014, HS016, HS017 and HS018 fails both, naming the rule and a rule doc that exists,
# and that a missing or invalid hardslice.json fails them explicitly.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d "${TMPDIR:-/tmp}/hardslice-template.XXXXXX")"
trap 'rm -rf "$work"' EXIT

app_name="Demo"
app="$work/$app_name"

"$repo_root/scripts/generate-app.sh" "$work"

if ! command -v pwsh > /dev/null; then
  echo "error: pwsh is required to verify scripts/check.ps1." >&2
  exit 1
fi

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

echo "==> Checking that a missing or invalid hardslice.json fails the check scripts explicitly"
mv "$app/hardslice.json" "$work/hardslice.json.backup"
expect_failure "hardslice.json: no file at" "docs/rules/HS017.md" "a missing hardslice.json" check.sh
printf '%s\n' '{ "style": "layers", "shared": { "allowedFolders": ["Messaging", "Persistence", "Behaviors"] } }' > "$app/hardslice.json"
expect_failure "hardslice.json: 'style' must be" "docs/rules/HS017.md" "an invalid hardslice.json" check.ps1
mv "$work/hardslice.json.backup" "$app/hardslice.json"

echo "==> Template OK: $app_name passes both check scripts, sends its commands through its own mediator, and the scripts fail without a container runtime, on HS001, HS012, HS013, HS014, HS016, HS017 and HS018 with their rule docs, and on a missing or invalid hardslice.json"
