# MAE4D Navis Tools
## Phase 7.4C-A Topology Foundation Test Harness

This project is a dependency-free console test harness for the
host-independent Phase 7.4C-A topology foundation.

It tests:

- ordered topology alignment;
- exact-name normalisation;
- substitution / rename handling;
- insertion and deletion handling;
- repeated-name topology;
- topology weighting;
- confidence classification;
- candidate separation;
- serialisation contracts;
- foundation configuration guards.

The harness deliberately does not load Autodesk Navisworks.

---

## Folder location

Place this project beside the existing NavisLegacyPlugin project:

    <solution root>\
        NavisLegacyPlugin.slnx
        NavisLegacyPlugin\
        NavisLegacyPlugin.Topology.Tests\
            NavisLegacyPlugin.Topology.Tests.csproj
            Program.cs
            run-tests.cmd
            README.md

The test project links directly to the live production source files:

    NavisLegacyPlugin\Models\Topology\TopologyModels.cs

    NavisLegacyPlugin\Services\Topology\
        TopologyAlignmentSettings.cs
        OrderedTopologyAligner.cs
        TopologyConfidenceClassifier.cs

This means the harness compiles and tests the actual working-tree
Phase 7.4C-A implementation rather than copied test versions.

---

## Optional solution registration

Add the following project beneath the Solution element in
NavisLegacyPlugin.slnx:

    <Project
        Path="NavisLegacyPlugin.Topology.Tests/NavisLegacyPlugin.Topology.Tests.csproj" />

For example:

    <Solution>
      <Project
        Path="NavisLegacyPlugin/NavisLegacyPlugin.csproj"
        Id="d43cb005-1306-44fb-90a8-de73641caa48" />

      <Project
        Path="NavisLegacyPlugin.Topology.Tests/NavisLegacyPlugin.Topology.Tests.csproj" />
    </Solution>

Registering the test project in the solution is optional.

The run-tests.cmd script can build and run the project directly.

---

## Run all tests

Open a Visual Studio Developer Command Prompt at the repository root.

Either run:

    NavisLegacyPlugin.Topology.Tests\run-tests.cmd

or change into the test directory:

    cd NavisLegacyPlugin.Topology.Tests
    run-tests.cmd

A successful run ends with:

    Result: N passed, 0 failed.
    PHASE 7.4C-A FOUNDATION TESTS PASSED.

The process returns exit code 0 when all selected tests pass.

Any test failure returns exit code 1.

---

## Run one test group

Alignment only:

    run-tests.cmd --filter Alignment

Confidence only:

    run-tests.cmd --filter Confidence

Contract only:

    run-tests.cmd --filter Contract

Foundation only:

    run-tests.cmd --filter Foundation

---

## Run an individual test

Examples:

    run-tests.cmd --filter Alignment.OwnerRename

    run-tests.cmd --filter Confidence.GoodAt80

    run-tests.cmd --filter Contract.DataContractRoundTrip

    run-tests.cmd --filter Foundation.LockedDefaults

The filter is case-insensitive and matches any part of the complete
test name.

---

# Test coverage

## Alignment

The harness verifies:

1. Identical topology paths score 100.
2. Name comparison ignores leading/trailing whitespace.
3. Name comparison is case-insensitive.
4. A renamed owner is retained as a substitution.
5. A TARGET-only hierarchy level appears as an insertion.
6. A SOURCE-only hierarchy level appears as a deletion.
7. Repeated node names preserve ordered occurrence mapping.
8. Anchor and owner evidence carry the strongest weights.
9. Empty topology paths return zero evidence.
10. Gap operations use -1 for the missing path index.

Example rename case:

    SOURCE
    Anchor
    Carrots
    D
    E

    TARGET
    Anchor
    Bananas
    D
    E

Carrots -> Bananas must reduce confidence but must not destroy the
remaining ordered topology evidence.

---

## Confidence

The harness verifies the Phase 7.4C starting policy:

### Good

    Best score >= 80
    AND
    separation >= 15 points

Good is write-eligible at the topology layer.

Example:

    Best = 80
    Next = 65
    Separation = 15
    Result = Good

### Vague

    Best score = 50 to 79
    AND
    separation >= 15 points

Vague is not write-eligible.

### Ambiguous

Where multiple viable candidates exist and their separation is less
than 15 points.

Example:

    Best = 95
    Next = 85
    Separation = 10
    Result = Ambiguous

This confirms that strong absolute confidence does not override a
credible competing candidate.

### Unresolved

Used when:

- no valid topology candidate exists;
- the anchor is not definitive;
- or best confidence is below 50.

---

## Contract

The harness verifies:

- TopologyResolution separation never becomes negative.
- Input collections are copied into read-only result collections.
- TopologyResolution survives DataContract JSON serialisation.
- Anchor evidence survives serialisation.
- SOURCE RID-owner evidence survives serialisation.
- SOURCE path evidence survives serialisation.
- TARGET candidate evidence survives serialisation.
- Alignment evidence survives serialisation.
- RID payload survives serialisation.
- Serialized topology data contains no ModelItem type.
- Serialized topology data contains no Autodesk.Navisworks type.
- Null string facts are normalised consistently.
- Alignment gaps use explicit -1 indexes.

---

## Foundation

The harness locks the initial Phase 7.4C-A settings:

    GoodThreshold = 80
    VagueThreshold = 50
    MinimumSeparation = 15

    ExactScore = 100
    SubstitutionScore = 35
    InsertionScore = 20
    DeletionScore = 20

    AnchorWeight = 40
    OwnerWeight = 32
    MinimumContextWeight = 8
    ContextWeightReduction = 6

The harness also verifies:

- invalid Good thresholds are rejected;
- invalid Vague thresholds are rejected;
- Vague cannot exceed Good;
- invalid separation thresholds are rejected;
- zero/negative critical weights are rejected;
- null aligner settings are rejected;
- null classifier settings are rejected;
- the required resolution enum states remain present;
- the required alignment disposition states remain present;
- the required anchor states remain present.

---

# Important scope boundary

This harness tests the host-independent Phase 7.4C-A foundation only.

It intentionally does NOT test:

- Navisworks ModelItem hierarchy extraction;
- live definitive-anchor discovery;
- IfcGuid + elementId matching;
- external SOURCE Automation;
- TARGET population membership;
- multiple-anchor group reconciliation;
- RID-owner conflict detection;
- overwrite policy;
- final pre-write revalidation;
- ComPropertyWriteService;
- RID mutation;
- Data Transfer reporting.

Those behaviours require integration/host-runtime tests in the later
Phase 7.4C implementation drops.

A Good topology classification in this harness therefore means only:

    topology.WriteEligible == true

It does NOT mean:

    RID was written

Final mutation remains subject to the later TARGET-boundary,
RID-conflict, overwrite and revalidation safety gates.

---

# Acceptance

Phase 7.4C-A foundation testing passes when:

1. The test project builds under C# 7.3 / .NET Framework 4.8.
2. Every Alignment test passes.
3. Every Confidence test passes.
4. Every Contract test passes.
5. Every Foundation test passes.
6. The process exits with code 0.
7. No Autodesk Navisworks runtime is required to execute the harness.