FRESH MAE4D PHASE 7.4C TOPOLOGY MSTEST HARNESS

Purpose
- Replaces the broken test harness.
- Avoids ProjectReference completely, so it does not trigger "Unable to find project information" for NavisLegacyPlugin.csproj.
- Compiles the four host-independent production topology files directly as linked source.
- Contains 45 MSTest tests.

Install
1. Delete the old NavisLegacyPlugin.Topology.Tests folder.
2. Extract this folder beside the production NavisLegacyPlugin folder:
   NavisLegacyPlugin\NavisLegacyPlugin
   NavisLegacyPlugin\NavisLegacyPlugin.Topology.Tests
3. Open NavisLegacyPlugin.Topology.Tests.slnx directly.
4. Restore packages, build, then run Test Explorer.

Command line
  dotnet restore .\NavisLegacyPlugin.Topology.Tests.slnx
  dotnet test .\NavisLegacyPlugin.Topology.Tests.slnx --no-restore

Important
The relative linked-source paths assume the production project folder is exactly ..\NavisLegacyPlugin.
No Autodesk/Navisworks assemblies are required by this harness.
Target: .NET Framework 4.8, x64, C# 7.3, MSTest.
