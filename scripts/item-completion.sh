# Sourced: the steps that follow an item template, as the skills of a generated solution take them: the lines that dotnet new
# hardslice-module and hardslice-command print, added to Program.cs and to the module, then the reference completion of a command
# slice. Used by scripts/test-template.sh, and by scripts/verify-published.sh on the packages of a release.
# They act on the solution <app>, named <app_name>, whose web project is <app_src>, and write their temporary files in <work>.

# Adds to the module the lines that dotnet new <template> printed in <output>, as the skills add-slice and add-domain-event do:
# the usings the file does not have yet, the registrations after 'services' (unless <registrations> is 'without-registrations'),
# and the call of Map before 'return endpoints;'. Fails if the instructions do not name the module file or one of these lines:
# two registrations for hardslice-command, the handler and the validator, or the handler alone of a command without parameters,
# one for hardslice-query, the handler, or two with --result filtered or summary, the handler and the validator of its filters, and one for
# hardslice-event, the handler of its reaction, which has no endpoint to map, as a system command of hardslice-command
# --endpoint none has none. A recurring command of hardslice-command --every has two, its handler and its AddRecurringCommand,
# and no endpoint.
register_slice() {
  local template="$1"
  local output="$2"
  local registrations="${3:-with-registrations}"
  local expected_registrations=2
  local module_file
  local using
  local usings=""
  local lines
  local mapping
  local without_endpoint=false

  if [[ "$template" == hardslice-event || "$output" == *"is a system command, which has no endpoint:"* ||
    "$output" == *$'\n            .AddRecurringCommand<'* ]]; then
    without_endpoint=true
  fi
  if [[ "$template" != hardslice-command || "$output" == *"without validator since the command has no parameter:"* ]]; then
    expected_registrations=1
  fi
  if [[ "$template" == hardslice-query && "$output" == *"register the handler and the validator by their contracts:"* ]]; then
    expected_registrations=2
  fi
  module_file="$(sed -n 's/^Manual instructions: Register the \(slice\|reaction\) in \(src\/.*Module\.cs\):$/\2/p' <<< "$output")"
  lines="$(grep -E '^            \.(AddScoped<(IRequestHandler|IValidator|IDomainEventHandler)|AddRecurringCommand)<' <<< "$output" || true)"
  mapping="$(grep -E '^        [A-Za-z0-9]+Endpoint\.Map\(group\);$' <<< "$output" || true)"
  if [[ -z "$module_file" || ! -f "$app/$module_file" || "$(grep -c . <<< "$lines")" != "$expected_registrations" ||
    ( "$without_endpoint" == true && -n "$mapping" ) || ( "$without_endpoint" == false && -z "$mapping" ) ]]; then
    printf '%s\n' "$output" >&2
    echo "error: dotnet new $template did not print the module file, the $expected_registrations registrations and, for a slice with an endpoint, the call of Map of its slice." >&2
    exit 1
  fi
  [[ "$registrations" == with-registrations ]] || lines=""
  while read -r using; do
    grep -Fxq "$using" "$app/$module_file" || usings+="$using"$'\n'
  done < <(grep -E '^using [A-Za-z0-9_.]+;$' <<< "$output")
  if ! { printf '%s' "$usings"; REGISTRATIONS="$lines" MAPPING="$mapping" awk '
      /^        services;?$/ && !registered {
        count = split(ENVIRON["REGISTRATIONS"], added, "\n")
        closed = $0 ~ /;$/
        chain = (closed && count == 0) ? $0 : "        services"
        print chain
        for (i = 1; i <= count; i++) print added[i] ((closed && i == count) ? ";" : "")
        registered = 1
        next
      }
      /^        return endpoints;$/ && !mapped && ENVIRON["MAPPING"] != "" { print ENVIRON["MAPPING"]; mapped = 1 }
      { print }
      END { if (!registered || (!mapped && ENVIRON["MAPPING"] != "")) exit 1 }' "$app/$module_file"; } > "$work/module.cs"; then
    echo "error: $module_file has no 'services' chain or 'return endpoints;' line to register the slice in." >&2
    exit 1
  fi
  mv "$work/module.cs" "$app/$module_file"
}


# Adds to Program.cs the lines that dotnet new hardslice-module printed in <output>, as the skill add-module does: the using at
# the top, the registration before 'var app = builder.Build();' and the mapping before 'app.Run();'. Fails if the instructions
# do not name Program.cs or one of these three lines.
register_module() {
  local output="$1"
  local program
  local using
  local registration
  local mapping

  program="$(sed -n 's/^Manual instructions: Register the module in \(src\/.*Program\.cs\):$/\1/p' <<< "$output")"
  using="$(grep -E '^using [A-Za-z0-9_.]+;$' <<< "$output" || true)"
  registration="$(grep -E '^builder\.Services\.Add[A-Za-z0-9]+Module\(\);$' <<< "$output" || true)"
  mapping="$(grep -E '^app\.Map[A-Za-z0-9]+Module\(\);$' <<< "$output" || true)"
  if [[ -z "$program" || ! -f "$app/$program" || "$(grep -c . <<< "$using")" != 1 || "$(grep -c . <<< "$registration")" != 1 ||
    "$(grep -c . <<< "$mapping")" != 1 ]]; then
    printf '%s\n' "$output" >&2
    echo "error: dotnet new hardslice-module did not print Program.cs, the using, the registration and the mapping of its module." >&2
    exit 1
  fi
  if ! USING="$using" REGISTRATION="$registration" MAPPING="$mapping" awk '
      NR == 1 { print ENVIRON["USING"] }
      /^var app = builder\.Build\(\);$/ && !registered { print ENVIRON["REGISTRATION"]; print ""; registered = 1 }
      /^app\.Run\(\);$/ && !mapped { print ENVIRON["MAPPING"]; print ""; mapped = 1 }
      { print }
      END { if (!registered || !mapped) exit 1 }' "$app/$program" > "$work/Program.cs"; then
    echo "error: $program has no 'var app = builder.Build();' or 'app.Run();' line to register the module in." >&2
    exit 1
  fi
  mv "$work/Program.cs" "$app/$program"
}


# The reference completion of the command slice <module>/<name> generated by dotnet new hardslice-command with its default
# response: the handler returns the identifier of the command, the test asserts it. No marker remains in the slice and its test.
complete_command() {
  local module="$1"
  local name="$2"
  local slice="$app_src/Features/$module/$name/$name.cs"
  local integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"

  sed '/throw new NotImplementedException(/,/Never call SaveChangesAsync\.");/c\
        Task.FromResult(command.Id);' "$slice" > "$work/slice.cs"
  mv "$work/slice.cs" "$slice"
  sed 's/^        Assert\.Fail("TODO(hardslice): .*/        (await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken)).ShouldBe(command.Id);/' \
    "$integration_test" > "$work/test.cs"
  mv "$work/test.cs" "$integration_test"
  if grep -l 'TODO(hardslice):' "$slice" "$integration_test"; then
    echo "error: the reference completion of $module/$name leaves TODO(hardslice): markers." >&2
    exit 1
  fi
}


# The reference completion of the command slice <module>/<name> generated by dotnet new hardslice-command --parameters none,
# or with the list of its fields, with the response <response>: id, or the name of the DTO that the slice declares. The handler returns a new identifier, alone
# or in the DTO, and the test asserts it. No marker remains in the slice and its test.
complete_parameterless_command() {
  local module="$1"
  local name="$2"
  local response="$3"
  local slice="$app_src/Features/$module/$name/$name.cs"
  local integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"
  local returned="Guid.NewGuid()"
  local asserted="(await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken)).ShouldNotBe(Guid.Empty);"

  if [[ "$response" != id ]]; then
    returned="new $response(Guid.NewGuid())"
    asserted="(await response.Content.ReadFromJsonAsync<$response>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Id.ShouldNotBe(Guid.Empty);"
  fi
  sed '/throw new NotImplementedException(/,/Never call SaveChangesAsync\.");/c\
        Task.FromResult('"$returned"');' "$slice" > "$work/slice.cs"
  mv "$work/slice.cs" "$slice"
  sed 's/^        Assert\.Fail("TODO(hardslice): .*/        '"$asserted"'/' "$integration_test" > "$work/test.cs"
  mv "$work/test.cs" "$integration_test"
  if grep -l 'TODO(hardslice):' "$slice" "$integration_test"; then
    echo "error: the reference completion of $module/$name leaves TODO(hardslice): markers." >&2
    exit 1
  fi
}


# The reference completion of the system command <module>/<name> generated by dotnet new hardslice-command --endpoint none, with
# the parameters <parameters> (id or none) and the response <response> (id, or the name of the DTO that the slice declares): the
# handler returns the identifier of the command, or a new one, alone or in the DTO, and the test asserts it. Its sender is a hosted
# service of the slice, <name>Runner, which sends it once in a scope of its own and logs a failure, registered by the module after
# 'services'. No marker remains in the slice and its test.
complete_system_command() {
  local module="$1"
  local name="$2"
  local parameters="$3"
  local response="$4"
  local slice="$app_src/Features/$module/$name/$name.cs"
  local module_file="$app_src/Features/$module/${module}Module.cs"
  local integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"
  local created="new ${name}Command()"
  local returned="Guid.NewGuid()"
  local asserted="response.ShouldNotBe(Guid.Empty);"

  if [[ "$parameters" == id ]]; then
    created="new ${name}Command(Guid.NewGuid())"
    returned="command.Id"
    asserted="response.ShouldBe(command.Id);"
  fi
  if [[ "$response" != id ]]; then
    returned="new $response($returned)"
    asserted="response.Id.ShouldNotBe(Guid.Empty);"
  fi
  sed '/throw new NotImplementedException(/,/Never call SaveChangesAsync\.");/c\
        Task.FromResult('"$returned"');' "$slice" > "$work/slice.cs"
  mv "$work/slice.cs" "$slice"
  sed 's/^        Assert\.Fail("TODO(hardslice): .*/        '"$asserted"'/' "$integration_test" > "$work/test.cs"
  mv "$work/test.cs" "$integration_test"
  cat > "$app_src/Features/$module/$name/${name}Runner.cs" <<RUNNER
using $app_name.Shared.Messaging;

namespace $app_name.Features.$module.$name;

// Sends ${name}Command once the application starts, in a scope of its own: a failure is logged and never stops the application.
internal sealed class ${name}Runner(IServiceScopeFactory scopes, ILogger<${name}Runner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send($created, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "${name}Command failed");
        }
    }
}
RUNNER
  if ! RUNNER="            .AddHostedService<${name}Runner>()" awk '
      /^        services$/ && !registered { print; print ENVIRON["RUNNER"]; registered = 1; next }
      { print }
      END { if (!registered) exit 1 }' "$module_file" > "$work/module.cs"; then
    echo "error: $module_file has no 'services' chain to register ${name}Runner in." >&2
    exit 1
  fi
  mv "$work/module.cs" "$module_file"
  if grep -l 'TODO(hardslice):' "$slice" "$integration_test"; then
    echo "error: the reference completion of $module/$name leaves TODO(hardslice): markers." >&2
    exit 1
  fi
}


# The reference completion of the recurring command <module>/<name> generated by dotnet new hardslice-command --every 00:00:30,
# with the response <response>: id, or the name of the DTO that the slice declares. The handler returns a new identifier, alone or
# in the DTO, each of the three cycles of the test asserts it, and the test asserts that the clock moved by three intervals only:
# no real wait. A second test checks that a test that does not keep the recurring command never starts it. No marker remains.
complete_recurring_command() {
  local module="$1"
  local name="$2"
  local response="$3"
  local slice="$app_src/Features/$module/$name/$name.cs"
  local integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"
  local response_type="Guid"
  local returned="Guid.NewGuid()"
  local asserted="(await cycles.RunNextAsync()).ShouldNotBe(Guid.Empty);"

  if [[ "$response" != id ]]; then
    response_type="$response"
    returned="new $response(Guid.NewGuid())"
    asserted="(await cycles.RunNextAsync()).Id.ShouldNotBe(Guid.Empty);"
  fi
  sed '/throw new NotImplementedException(/,/Never call SaveChangesAsync\.");/c\
        Task.FromResult('"$returned"');' "$slice" > "$work/slice.cs"
  mv "$work/slice.cs" "$slice"
  sed -e '/^            \/\/ '"$name"'Handler throws its NotImplementedException/d' \
    -e 's/^            var response = await cycles\.RunNextAsync();$/            '"$asserted"'/' \
    -e 's/^        Assert\.Fail("TODO(hardslice): .*/        cycles.Clock.GetUtcNow().ShouldBe(TestInstants.Start.AddSeconds(90));/' \
    "$integration_test" > "$work/test.cs"
  if ! RESPONSE="$response_type" NAME="$name" awk '
      { lines[NR] = $0 }
      END {
        for (i = 1; i < NR; i++) print lines[i]
        print ""
        print "    [Fact]"
        print "    public async Task Is_not_started_by_a_test_that_does_not_keep_it()"
        print "    {"
        print "        await using var app = new AppFactory(database);"
        print ""
        print "        app.Services.GetServices<IHostedService>().OfType<RecurringCommandService<" ENVIRON["NAME"] "Command, " ENVIRON["RESPONSE"] ">>().ShouldBeEmpty();"
        print "    }"
        print lines[NR]
        if (lines[NR] != "}") exit 1
      }' "$work/test.cs" > "$integration_test"; then
    echo "error: the integration test of $module/$name does not end with the closing brace of its class." >&2
    exit 1
  fi
  { printf 'using Microsoft.Extensions.DependencyInjection;\nusing Microsoft.Extensions.Hosting;\nusing %s.Shared.Messaging;\n' "$app_name"
    cat "$integration_test"; } > "$work/test.cs"
  mv "$work/test.cs" "$integration_test"
  if grep -l 'TODO(hardslice):' "$slice" "$integration_test" || grep -q 'var response = ' "$integration_test"; then
    echo "error: the reference completion of $module/$name leaves TODO(hardslice): markers, or a cycle it does not assert." >&2
    exit 1
  fi
}

# The reference completion of the scheduled command <module>/<name> generated by dotnet new hardslice-command with --daily-at or
# --every, with or without --retry, and the response <response>: id, or the name of the DTO that the slice declares. The handler
# returns a new identifier, alone or in the DTO, each cycle of the generated test asserts it, and the test asserts that the clock
# stands where <clock-assertion>, a Shouldly assertion on cycles.Clock.GetUtcNow(), says: no real wait. The test of the retries,
# when there is one, is left as generated: it passes without the use case. No marker remains.
complete_scheduled_command() {
  local module="$1"
  local name="$2"
  local response="$3"
  local clock_assertion="$4"
  local slice="$app_src/Features/$module/$name/$name.cs"
  local integration_test="$app/tests/$app_name.IntegrationTests/Features/$module/$name/${name}Tests.cs"
  local returned="Guid.NewGuid()"
  local asserted="(await cycles.RunNextAsync()).ShouldNotBe(Guid.Empty);"

  if [[ "$response" != id ]]; then
    returned="new $response(Guid.NewGuid())"
    asserted="(await cycles.RunNextAsync()).Id.ShouldNotBe(Guid.Empty);"
  fi
  sed '/throw new NotImplementedException(/,/Never call SaveChangesAsync\.");/c\
        Task.FromResult('"$returned"');' "$slice" > "$work/slice.cs"
  mv "$work/slice.cs" "$slice"
  sed -e '/^            \/\/ '"$name"'Handler throws its NotImplementedException/d' \
    -e 's/^            var response = await cycles\.RunNextAsync();$/            '"$asserted"'/' \
    -e 's/^        Assert\.Fail("TODO(hardslice): .*/        cycles.Clock.GetUtcNow().'"$clock_assertion"';/' \
    "$integration_test" > "$work/test.cs"
  mv "$work/test.cs" "$integration_test"
  if grep -l 'TODO(hardslice):' "$slice" "$integration_test" || grep -q 'var response = ' "$integration_test"; then
    echo "error: the reference completion of $module/$name leaves TODO(hardslice): markers, or a cycle it does not assert." >&2
    exit 1
  fi
}
