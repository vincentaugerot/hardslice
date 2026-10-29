# PB-014 review fixes

The user requested the four corrections reproduced in the PB-014 review. Keep the existing architecture rules and diagnostics, and make the missing dependencies and accessibility cases visible to them.

- Restore the domain assembly-reference check alongside its detailed type diagnostics. This catches assembly attributes even when their dependency is inherited from MSBuild configuration.
- Keep ArchUnitNET for the dependency graph and supplement it with reflection over exception-handling clauses. Include constructors and compiler-generated nested types for async methods, iterators and lambdas. Normalize the additional targets without loading every dependency assembly into ArchUnitNET.
- Track membership in Features separately from the module name, so Shared cannot depend on types directly in the Features namespace either.
- Check nested accessibility through every declaring type, including protected and protected internal types that another assembly can reach through inheritance. Internal, private and private protected containers remain inaccessible.

Replacing the graph library or writing a complete IL reader would expand the scope unnecessarily. Loading every referenced assembly into the graph would increase cost without recovering omitted catch edges. The supplemental reflection scan reads metadata only and never invokes application code.

Add focused regression tests for catch extraction and nested accessibility, plus generated-template probes for all four findings. The probes must fail the Bash and PowerShell checks with the expected HardSlice diagnostic and an existing rule document. Run the repository suite and the full template verification, including its clean application and integration tests. Preserve the user's existing PB-014 changes and perform no SideHub actions.
