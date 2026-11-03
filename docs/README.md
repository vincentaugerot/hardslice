# HardSlice documentation

## Using HardSlice

| Page | Read it to |
|---|---|
| [Getting started](getting-started.md) | install the templates, generate a solution, verify it, run its API and hand it to an agent |
| [Item templates](item-templates.md) | generate a module, an aggregate, a command, a query or a domain event with `dotnet new` |
| [Agent loop](agent-loop.md) | know what each agent reads, what its hooks check, which files are protected and what the skills do |
| [Rules](rules.md) | understand how the rules are reported, choose an analysis mode, and know what each rule cannot see |

The table of the rules is in the [README](../README.md#rules), and every generated solution carries the sheet of each rule in `docs/rules/`.

## Evidence

| Page | Shows |
|---|---|
| [Demonstrations](../demos/README.md) | agents fixing each blocking rule, and agents adding a feature through the templates |
| [Agent support](agents/support.md) | what each CLI reads, at a dated version, and the trials behind each adapter |
| [Analyzer cost](performance/analyzers.md) | what the analyzers add to a real build, and what the hooks cost, against their budgets |

Both reports open with an English summary, but their detail is in French.

## Working on HardSlice

| Page | Read it to |
|---|---|
| [Contributing](../CONTRIBUTING.md) | build and test this repository, try a local build of the packages, follow its conventions |
| [Releasing](releasing.md) | publish a preview on NuGet.org |
| [Design](design/specifications.md) | read the specification, the [delivery plan](design/plan.md) and the [backlog](design/product-backlog.md) |

`design/` holds the working documents of the project, in French: the product specification, the delivery plan and the backlog of every ticket, with their dated decisions. They are public so that the method stays transparent. They are not user documentation.
