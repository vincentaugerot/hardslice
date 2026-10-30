# PB-015 review fixes

The user authorized correcting the two reproduced review findings. Preserve the existing PB-015 work and perform no Side Hub actions.

- HS013 keeps the validator types and matches each `IValidator<T>` contract against the command. Bind generic parameters without discarding closed arguments, then construct the validator with those bindings: the runtime checks that its constraints also accept the command's arguments. An open command is covered only when its own type parameters satisfy the validator's constraints. Do not instantiate validators or execute application code.
- HS014 continues reading current test sources, but parses them with each test project's `DefineConstants`. A small shared MSBuild target returns the symbols after the SDK adds framework symbols. The architecture project collects them for its current configuration into assembly metadata, without building or referencing the test assemblies. This preserves source freshness and handles project-specific constants, Debug/Release and framework conditions.

Comparing only generic definitions loses specializations and constraints; rejecting all generics would reject valid validators. Reading test assemblies would reintroduce stale tests, while hardcoding `NET10_0` or using the architecture project's constants would miss differences between test projects. The selected approach uses reflection and the existing SDK, with no new dependency.

Add regression tests for closed and constrained generic validators, including valid open matches, and both active and inactive preprocessor branches. Extend the generated-template probes to prove HS013 and HS014 fail both check scripts for the reviewed defects and accept valid generic validators and project-specific test symbols. Update the rule documentation, run the repository suite and the complete generated-template checks with Docker.
