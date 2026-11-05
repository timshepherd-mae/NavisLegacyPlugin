MAE4D Phase 7.4C MSTest DLL project

Purpose
- Builds a net48 x64 MSTest test assembly named NavisLegacyPlugin.Topology.Tests.dll.
- References the production NavisLegacyPlugin project rather than duplicating production code.
- Covers the host-independent topology foundation currently present in Phase7.4C-A: settings validation, ordered alignment, confidence classification and DataContract JSON round-trip.

Install
1. Copy NavisLegacyPlugin.Topology.Tests beside the production NavisLegacyPlugin project folder.
2. Ensure the ProjectReference path ..\..\NavisLegacyPlugin\NavisLegacyPlugin.csproj matches your checkout. If you place this folder directly beside NavisLegacyPlugin rather than under a tests folder, change it to ..\NavisLegacyPlugin\NavisLegacyPlugin.csproj.
3. Restore NuGet packages and build x64.

Run
- Visual Studio Test Explorer, or vstest.console.exe against bin\Debug\net48\NavisLegacyPlugin.Topology.Tests.dll.

Scope
This is the unit-test DLL project for the pure Phase 7.4C topology calculation objects. Navisworks-host integration, ModelItem tree construction, Automation worker transport, conflict reconciliation and branch mutation require later production seams / host integration and are not simulated here.
