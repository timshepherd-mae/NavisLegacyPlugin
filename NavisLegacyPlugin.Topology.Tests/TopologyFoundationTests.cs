using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NavisLegacyPlugin.Models.Topology;
using NavisLegacyPlugin.Services.Topology;

namespace NavisLegacyPlugin.Topology.Tests
{
    [TestClass]
    public sealed class TopologyFoundationTests
    {
        // ============================================================
        // ALIGNMENT
        // ============================================================

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_ExactPathScores100()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("E"),
                    Path("A", "B", "C", "D", "E"),
                    Path("A", "B", "C", "D", "E"));

            Assert.AreEqual(
                100,
                result.Score);

            Assert.IsTrue(
                result.Alignment.All(
                    step =>
                        step.Disposition ==
                        TopologyAlignmentDisposition.Exact));
        }

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_ComparisonIsTrimmedAndCaseInsensitive()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("C"),
                    Path(" A ", "b", "C"),
                    Path("a", " B ", "c"));

            Assert.AreEqual(
                100,
                result.Score);

            Assert.IsTrue(
                result.Alignment.All(
                    step =>
                        step.Disposition ==
                        TopologyAlignmentDisposition.Exact));
        }

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_OwnerRenameIsSubstitution()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("Bananas"),
                    Path(
                        "Anchor",
                        "Carrots",
                        "D",
                        "E"),
                    Path(
                        "Anchor",
                        "Bananas",
                        "D",
                        "E"));

            Assert.IsTrue(
                result.Alignment.Any(
                    step =>
                        step.SourceName == "Carrots" &&
                        step.TargetName == "Bananas" &&
                        step.Disposition ==
                        TopologyAlignmentDisposition.Substituted));

            Assert.IsTrue(
                result.Score > 0);

            Assert.IsTrue(
                result.Score < 100);
        }

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_TargetInsertionIsRetained()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("Owner"),
                    Path(
                        "Anchor",
                        "Owner",
                        "Parent"),
                    Path(
                        "Anchor",
                        "Container",
                        "Owner",
                        "Parent"));

            Assert.IsTrue(
                result.Alignment.Any(
                    step =>
                        step.Disposition ==
                            TopologyAlignmentDisposition.Inserted &&
                        step.TargetName == "Container" &&
                        step.SourceIndex == -1));
        }

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_SourceDeletionIsRetained()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("Owner"),
                    Path(
                        "Anchor",
                        "Container",
                        "Owner"),
                    Path(
                        "Anchor",
                        "Owner"));

            Assert.IsTrue(
                result.Alignment.Any(
                    step =>
                        step.Disposition ==
                            TopologyAlignmentDisposition.Deleted &&
                        step.SourceName == "Container" &&
                        step.TargetIndex == -1));
        }

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_RepeatedNamesPreserveOrder()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("E"),
                    Path(
                        "A",
                        "B",
                        "C",
                        "C",
                        "D",
                        "E"),
                    Path(
                        "A",
                        "B",
                        "F",
                        "C",
                        "G",
                        "C",
                        "D",
                        "E"));

            List<TopologyAlignmentStep> cMatches =
                result.Alignment
                    .Where(
                        step =>
                            step.Disposition ==
                                TopologyAlignmentDisposition.Exact &&
                            step.SourceName == "C" &&
                            step.TargetName == "C")
                    .ToList();

            Assert.AreEqual(
                2,
                cMatches.Count);

            Assert.IsTrue(
                cMatches[0].SourceIndex <
                cMatches[1].SourceIndex);

            Assert.IsTrue(
                cMatches[0].TargetIndex <
                cMatches[1].TargetIndex);
        }

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_AnchorAndOwnerCarryHighestWeights()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("Owner"),
                    Path(
                        "Anchor",
                        "Owner",
                        "Parent1",
                        "Parent2"),
                    Path(
                        "Anchor",
                        "Owner",
                        "Parent1",
                        "Parent2"));

            TopologyAlignmentStep anchor =
                result.Alignment.Single(
                    step =>
                        step.SourceIndex == 0);

            TopologyAlignmentStep owner =
                result.Alignment.Single(
                    step =>
                        step.SourceIndex == 1);

            TopologyAlignmentStep parent =
                result.Alignment.Single(
                    step =>
                        step.SourceIndex == 2);

            Assert.AreEqual(
                40,
                anchor.Weight);

            Assert.AreEqual(
                32,
                owner.Weight);

            Assert.IsTrue(
                parent.Weight < owner.Weight);
        }

        [TestMethod]
        [TestCategory("Alignment")]
        public void Alignment_EmptyPathsProduceZeroScore()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("None"),
                    null,
                    null);

            Assert.AreEqual(
                0,
                result.Score);

            Assert.AreEqual(
                0,
                result.Alignment.Count);
        }

        // ============================================================
        // CONFIDENCE
        // ============================================================

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_GoodAt80With15PointSeparation()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Best", 80),
                    Candidate("Next", 65));

            Assert.AreEqual(
                TopologyResolutionStatus.Good,
                result.Status);

            Assert.IsTrue(
                result.WriteEligible);

            Assert.AreEqual(
                15,
                result.Separation);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_VagueAt79WithClearSeparation()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Best", 79),
                    Candidate("Next", 60));

            Assert.AreEqual(
                TopologyResolutionStatus.Vague,
                result.Status);

            Assert.IsFalse(
                result.WriteEligible);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_VagueAt50WithClearSeparation()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Best", 50),
                    Candidate("Next", 20));

            Assert.AreEqual(
                TopologyResolutionStatus.Vague,
                result.Status);

            Assert.IsFalse(
                result.WriteEligible);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_HighScoreNearTieIsAmbiguous()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Best", 95),
                    Candidate("Next", 85));

            Assert.AreEqual(
                TopologyResolutionStatus.Ambiguous,
                result.Status);

            Assert.IsFalse(
                result.WriteEligible);

            Assert.AreEqual(
                10,
                result.Separation);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_FourteenPointSeparationIsAmbiguous()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Best", 95),
                    Candidate("Next", 81));

            Assert.AreEqual(
                TopologyResolutionStatus.Ambiguous,
                result.Status);

            Assert.AreEqual(
                14,
                result.Separation);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_FifteenPointSeparationPassesGate()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Best", 90),
                    Candidate("Next", 75));

            Assert.AreEqual(
                TopologyResolutionStatus.Good,
                result.Status);

            Assert.AreEqual(
                15,
                result.Separation);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_UnresolvedBelow50()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Best", 49));

            Assert.AreEqual(
                TopologyResolutionStatus.Unresolved,
                result.Status);

            Assert.IsFalse(
                result.WriteEligible);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_UnresolvedWithoutCandidate()
        {
            TopologyResolution result =
                Classify();

            Assert.AreEqual(
                TopologyResolutionStatus.Unresolved,
                result.Status);

            Assert.AreEqual(
                0,
                result.Candidates.Count);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_MissingKeyAnchorIsUnresolved()
        {
            AssertNonMatchedAnchorIsUnresolved(
                TopologyAnchorStatus.MissingKey);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_UnmatchedAnchorIsUnresolved()
        {
            AssertNonMatchedAnchorIsUnresolved(
                TopologyAnchorStatus.Unmatched);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_AmbiguousAnchorIsUnresolved()
        {
            AssertNonMatchedAnchorIsUnresolved(
                TopologyAnchorStatus.Ambiguous);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_OnlyGoodIsWriteEligible()
        {
            Assert.IsTrue(
                Classify(
                    Candidate(
                        "Good",
                        100))
                .WriteEligible);

            Assert.IsFalse(
                Classify(
                    Candidate(
                        "Vague",
                        70))
                .WriteEligible);

            Assert.IsFalse(
                Classify(
                    Candidate(
                        "Unresolved",
                        40))
                .WriteEligible);

            Assert.IsFalse(
                Classify(
                    Candidate("CandidateA", 90),
                    Candidate("CandidateB", 80))
                .WriteEligible);
        }

        [TestMethod]
        [TestCategory("Confidence")]
        public void Confidence_CandidatesRankDescending()
        {
            TopologyResolution result =
                Classify(
                    Candidate("Low", 51),
                    Candidate("High", 91),
                    Candidate("Mid", 70));

            Assert.AreEqual(
                91,
                result.Candidates[0].Score);

            Assert.AreEqual(
                70,
                result.Candidates[1].Score);

            Assert.AreEqual(
                51,
                result.Candidates[2].Score);
        }

        // ============================================================
        // CONTRACT
        // ============================================================

        [TestMethod]
        [TestCategory("Contract")]
        public void Contract_SeparationNeverNegative()
        {
            var reversed =
                new TopologyResolution(
                    TopologyResolutionStatus.Ambiguous,
                    "Test",
                    false,
                    50,
                    70,
                    null,
                    null,
                    null,
                    null,
                    null);

            Assert.AreEqual(
                0,
                reversed.Separation);

            var normal =
                new TopologyResolution(
                    TopologyResolutionStatus.Good,
                    "Test",
                    true,
                    85,
                    70,
                    null,
                    null,
                    null,
                    null,
                    null);

            Assert.AreEqual(
                15,
                normal.Separation);
        }

        [TestMethod]
        [TestCategory("Contract")]
        public void Contract_CollectionsAreReadOnlySnapshots()
        {
            var input =
                new List<TopologyAnchorEvidence>
                {
                    MatchedAnchor()
                };

            var resolution =
                new TopologyResolution(
                    TopologyResolutionStatus.Good,
                    "Test",
                    true,
                    100,
                    0,
                    input,
                    Fingerprint("Owner"),
                    "RID-1",
                    Path(
                        "Anchor",
                        "Owner"),
                    null);

            input.Clear();

            Assert.AreEqual(
                1,
                resolution.Anchors.Count);

            Assert.ThrowsException<NotSupportedException>(
                () =>
                    ((IList<TopologyAnchorEvidence>)
                        resolution.Anchors)
                    .Add(
                        MatchedAnchor()));
        }

        [TestMethod]
        [TestCategory("Contract")]
        public void Contract_DataContractRoundTripPreservesEvidence()
        {
            TopologyResolution original =
                CreatePopulatedResolution();

            var serializer =
                new DataContractJsonSerializer(
                    typeof(TopologyResolution));

            TopologyResolution copy;

            using (var stream =
                new MemoryStream())
            {
                serializer.WriteObject(
                    stream,
                    original);

                stream.Position = 0;

                copy =
                    (TopologyResolution)
                        serializer.ReadObject(
                            stream);
            }

            Assert.IsNotNull(copy);

            Assert.AreEqual(
                original.Status,
                copy.Status);

            Assert.AreEqual(
                original.RidValue,
                copy.RidValue);

            Assert.AreEqual(
                original.BestScore,
                copy.BestScore);

            Assert.AreEqual(
                original.NextBestScore,
                copy.NextBestScore);

            Assert.AreEqual(
                original.Separation,
                copy.Separation);

            Assert.AreEqual(
                original.Anchors.Count,
                copy.Anchors.Count);

            Assert.AreEqual(
                original.SourcePath.Count,
                copy.SourcePath.Count);

            Assert.AreEqual(
                original.Candidates.Count,
                copy.Candidates.Count);

            Assert.AreEqual(
                original.Candidates[0]
                    .Alignment
                    .Count,
                copy.Candidates[0]
                    .Alignment
                    .Count);

            Assert.AreEqual(
                original.SourceRidOwner.DisplayName,
                copy.SourceRidOwner.DisplayName);
        }

        [TestMethod]
        [TestCategory("Contract")]
        public void Contract_SerializedEvidenceContainsNoNavisworksTypes()
        {
            TopologyResolution resolution =
                CreatePopulatedResolution();

            var serializer =
                new DataContractJsonSerializer(
                    typeof(TopologyResolution));

            string json;

            using (var stream =
                new MemoryStream())
            {
                serializer.WriteObject(
                    stream,
                    resolution);

                json =
                    Encoding.UTF8.GetString(
                        stream.ToArray());
            }

            Assert.IsFalse(
                json.IndexOf(
                    "Autodesk.Navisworks",
                    StringComparison.OrdinalIgnoreCase) >= 0);

            Assert.IsFalse(
                json.IndexOf(
                    "ModelItem",
                    StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [TestMethod]
        [TestCategory("Contract")]
        public void Contract_NullStringsNormalizeToEmpty()
        {
            var fingerprint =
                new TopologyItemFingerprint(
                    null,
                    null,
                    null,
                    null);

            Assert.AreEqual(
                string.Empty,
                fingerprint.InstanceGuid);

            Assert.AreEqual(
                string.Empty,
                fingerprint.ItemGuid);

            Assert.AreEqual(
                string.Empty,
                fingerprint.SourceFile);

            Assert.AreEqual(
                string.Empty,
                fingerprint.DisplayName);

            var anchor =
                new TopologyAnchorEvidence(
                    null,
                    null,
                    TopologyAnchorStatus.Unmatched,
                    null,
                    null,
                    null);

            Assert.AreEqual(
                string.Empty,
                anchor.StrategyName);

            Assert.AreEqual(
                string.Empty,
                anchor.NormalizedKey);

            Assert.AreEqual(
                string.Empty,
                anchor.Reason);
        }

        [TestMethod]
        [TestCategory("Contract")]
        public void Contract_InsertionUsesMinusOneSourceIndex()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("X"),
                    Path("A"),
                    Path(
                        "A",
                        "X"));

            Assert.IsTrue(
                result.Alignment.Any(
                    step =>
                        step.Disposition ==
                            TopologyAlignmentDisposition.Inserted &&
                        step.SourceIndex == -1));
        }

        [TestMethod]
        [TestCategory("Contract")]
        public void Contract_DeletionUsesMinusOneTargetIndex()
        {
            TopologyCandidateEvidence result =
                CreateAligner().Align(
                    Fingerprint("X"),
                    Path(
                        "A",
                        "X"),
                    Path("A"));

            Assert.IsTrue(
                result.Alignment.Any(
                    step =>
                        step.Disposition ==
                            TopologyAlignmentDisposition.Deleted &&
                        step.TargetIndex == -1));
        }

        // ============================================================
        // FOUNDATION
        // ============================================================

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_DefaultSettingsMatchLockedPolicy()
        {
            var settings =
                new TopologyAlignmentSettings();

            Assert.AreEqual(
                80,
                settings.GoodThreshold);

            Assert.AreEqual(
                50,
                settings.VagueThreshold);

            Assert.AreEqual(
                15,
                settings.MinimumSeparation);

            Assert.AreEqual(
                100,
                settings.ExactScore);

            Assert.AreEqual(
                35,
                settings.SubstitutionScore);

            Assert.AreEqual(
                20,
                settings.InsertionScore);

            Assert.AreEqual(
                20,
                settings.DeletionScore);

            Assert.AreEqual(
                40,
                settings.AnchorWeight);

            Assert.AreEqual(
                32,
                settings.OwnerWeight);

            Assert.AreEqual(
                8,
                settings.MinimumContextWeight);

            Assert.AreEqual(
                6,
                settings.ContextWeightReduction);
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_GoodThresholdAbove100IsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    GoodThreshold = 101
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_NegativeGoodThresholdIsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    GoodThreshold = -1
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_NegativeVagueThresholdIsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    VagueThreshold = -1
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_VagueCannotExceedGood()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    GoodThreshold = 60,
                    VagueThreshold = 61
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_SeparationAbove100IsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    MinimumSeparation = 101
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_NegativeSeparationIsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    MinimumSeparation = -1
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_ZeroAnchorWeightIsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    AnchorWeight = 0
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_ZeroOwnerWeightIsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    OwnerWeight = 0
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_ZeroMinimumContextWeightIsRejected()
        {
            var settings =
                new TopologyAlignmentSettings
                {
                    MinimumContextWeight = 0
                };

            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    settings.Validate());
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_AlignerRejectsNullSettings()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () =>
                    new OrderedTopologyAligner(
                        null));
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_ClassifierRejectsNullSettings()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () =>
                    new TopologyConfidenceClassifier(
                        null));
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_AlignerRejectsInvalidSettingsImmediately()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    new OrderedTopologyAligner(
                        new TopologyAlignmentSettings
                        {
                            GoodThreshold = -1
                        }));
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_ClassifierRejectsInvalidSettingsImmediately()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () =>
                    new TopologyConfidenceClassifier(
                        new TopologyAlignmentSettings
                        {
                            MinimumSeparation = -1
                        }));
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_ResolutionEnumContainsLockedStates()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Good",
                    "Vague",
                    "Ambiguous",
                    "Unresolved"
                },
                Enum.GetNames(
                    typeof(
                        TopologyResolutionStatus)));
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_AlignmentEnumContainsLockedStates()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Exact",
                    "Substituted",
                    "Inserted",
                    "Deleted"
                },
                Enum.GetNames(
                    typeof(
                        TopologyAlignmentDisposition)));
        }

        [TestMethod]
        [TestCategory("Foundation")]
        public void Foundation_AnchorEnumContainsRequiredStates()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Matched",
                    "MissingKey",
                    "Unmatched",
                    "Ambiguous"
                },
                Enum.GetNames(
                    typeof(
                        TopologyAnchorStatus)));
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static OrderedTopologyAligner
            CreateAligner()
        {
            return new OrderedTopologyAligner(
                new TopologyAlignmentSettings());
        }

        private static TopologyConfidenceClassifier
            CreateClassifier()
        {
            return new TopologyConfidenceClassifier(
                new TopologyAlignmentSettings());
        }

        private static TopologyResolution Classify(
            params TopologyCandidateEvidence[] candidates)
        {
            return CreateClassifier().Classify(
                new[]
                {
                    MatchedAnchor()
                },
                Fingerprint("Owner"),
                "RID-1",
                Path(
                    "Anchor",
                    "Owner"),
                candidates);
        }

        private static void AssertNonMatchedAnchorIsUnresolved(
            TopologyAnchorStatus status)
        {
            var anchor =
                new TopologyAnchorEvidence(
                    "Test.Anchor",
                    "KEY",
                    status,
                    "Anchor did not resolve definitively.",
                    null,
                    null);

            TopologyResolution result =
                CreateClassifier().Classify(
                    new[]
                    {
                        anchor
                    },
                    Fingerprint("Owner"),
                    "RID-1",
                    Path(
                        "Anchor",
                        "Owner"),
                    new[]
                    {
                        Candidate(
                            "Candidate",
                            100)
                    });

            Assert.AreEqual(
                TopologyResolutionStatus.Unresolved,
                result.Status);

            Assert.IsFalse(
                result.WriteEligible);
        }

        private static TopologyItemFingerprint Fingerprint(
            string name)
        {
            string safeName =
                name ?? "NULL";

            return new TopologyItemFingerprint(
                "instance-" + safeName,
                "item-" + safeName,
                "model.nwd",
                name);
        }

        private static List<TopologyPathNode> Path(
            params string[] names)
        {
            return names
                .Select(
                    (name, index) =>
                        new TopologyPathNode(
                            index,
                            name,
                            Fingerprint(name)))
                .ToList();
        }

        private static TopologyAnchorEvidence
            MatchedAnchor()
        {
            return new TopologyAnchorEvidence(
                "Test.Anchor",
                "ANCHOR-1",
                TopologyAnchorStatus.Matched,
                "Matched for topology test.",
                Fingerprint(
                    "SourceAnchor"),
                Fingerprint(
                    "TargetAnchor"));
        }

        private static TopologyCandidateEvidence Candidate(
            string name,
            int score)
        {
            return new TopologyCandidateEvidence(
                Fingerprint(name),
                score,
                Path(
                    "Anchor",
                    name),
                new[]
                {
                    new TopologyAlignmentStep(
                        0,
                        0,
                        "Anchor",
                        "Anchor",
                        TopologyAlignmentDisposition.Exact,
                        40,
                        40)
                });
        }

        private static TopologyResolution
            CreatePopulatedResolution()
        {
            TopologyCandidateEvidence candidate =
                CreateAligner().Align(
                    Fingerprint(
                        "RenamedOwner"),
                    Path(
                        "Anchor",
                        "Owner",
                        "Parent"),
                    Path(
                        "Anchor",
                        "RenamedOwner",
                        "Parent"));

            return new TopologyResolution(
                TopologyResolutionStatus.Vague,
                "Contract round-trip test.",
                false,
                candidate.Score,
                10,
                new[]
                {
                    MatchedAnchor()
                },
                Fingerprint(
                    "Owner"),
                "RID-42",
                Path(
                    "Anchor",
                    "Owner",
                    "Parent"),
                new[]
                {
                    candidate
                });
        }
    }
}