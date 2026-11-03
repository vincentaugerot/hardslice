; Shipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

; Release headers name a System.Version, without prerelease label: the rules of every preview of a version go under its header.
; 0.3.0: first shipped in 0.3.0-preview.1.
## Release 0.3.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
HS000 | HardSlice.Architecture | Error | SuppressionJustificationAnalyzer
HS003 | HardSlice.Architecture | Error | EndpointDatabaseAccessAnalyzer
HS004 | HardSlice.Architecture | Error | SliceStructureAnalyzer
HS005 | HardSlice.Architecture | Error | CommandResponseAnalyzer
HS006 | HardSlice.Architecture | Error | QueryHandlerTrackingAnalyzer
HS007 | HardSlice.Architecture | Error | EntityStateAnalyzer
HS008 | HardSlice.Architecture | Error | AggregateReferenceAnalyzer
HS009 | HardSlice.Architecture | Error | DomainEventRecordAnalyzer
HS010 | HardSlice.Architecture | Error | SystemClockAnalyzer
HS011 | HardSlice.Architecture | Error | AsyncUsageAnalyzer
HS015 | HardSlice.Architecture | Error | SliceEndpointAnalyzer
HS016 | HardSlice.Architecture | Error | SliceReferenceAnalyzer
HS019 | HardSlice.Naming | Disabled | DomainEventNameAnalyzer
HS900 | HardSlice.Configuration | Error | ClassificationAnalyzer
HS901 | HardSlice.Configuration | Error | ClassificationAnalyzer
HS902 | HardSlice.Configuration | Error | ClassificationAnalyzer
