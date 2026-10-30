# PB-015 test discovery fixes

The user approved fixing both findings from the review of `6a55894`, with no Side Hub actions.

Extend the existing MSBuild collection to supply each test project's evaluated `Compile` files alongside its compilation symbols. HS014 reads those current source files, so `Compile Remove`, explicit includes and disabled default items affect which tests count. HS018 keeps its documented folder-presence contract. Generate SDK global usings when collecting the inputs so project-level attribute aliases are available too.

Resolve attribute aliases from the enclosing namespaces, the compilation unit and the project's active global usings before applying the existing Fact/Theory naming convention. Only compiled sources contribute global aliases, and a local alias takes precedence over an outer or global alias.

Reading test assemblies would reintroduce stale tests. Parsing project XML manually would miss imports, conditions and SDK defaults. MSBuild's evaluated inputs and the existing Roslyn syntax trees address both findings without adding a dependency.

Add regression tests for excluded and deleted sources, local and global aliases, namespace scope and shadowing. Extend the generated-template probes to reject excluded tests in both check scripts and accept aliased tests in either test project. Update the rule documentation, then run the repository tests and the complete generated-template checks with Docker.
