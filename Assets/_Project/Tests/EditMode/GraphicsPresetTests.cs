using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// GDD 16.16 lists a quality preset row in the options. The project shipped two engine
    /// levels named after platforms - "Mobile", which excluded Standalone, and "PC" - so a
    /// Windows build offered one usable preset under two English labels, and the saved default
    /// of 2 clamped onto whichever one survived. These tests hold the three-level row.
    /// </summary>
    public sealed class GraphicsPresetTests
    {
        const int ExpectedLevels = 3;

        [SetUp]
        public void SetUp()
        {
            TestServices.Ensure();
        }

        [Test]
        public void ThereAreExactlyThreePresets()
        {
            Assert.AreEqual(ExpectedLevels, QualitySettings.names.Length,
                "GDD 16.16 asks for three presets; found: " + string.Join(", ", QualitySettings.names));
        }

        [Test]
        public void PresetsRunLowToHigh()
        {
            // The options screen labels them by index (ui.quality.low/medium/high), so the
            // engine order is part of the contract rather than cosmetic.
            CollectionAssert.AreEqual(new[] { "Low", "Medium", "High" }, QualitySettings.names);
        }

        [Test]
        public void EveryPresetHasItsOwnRenderPipelineAsset()
        {
            var seen = new System.Collections.Generic.List<RenderPipelineAsset>();
            int restore = QualitySettings.GetQualityLevel();

            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    var asset = QualitySettings.renderPipeline;

                    Assert.IsNotNull(asset, QualitySettings.names[i] + " has no render pipeline asset");
                    CollectionAssert.DoesNotContain(seen, asset,
                        QualitySettings.names[i] + " shares its pipeline asset with another preset");
                    seen.Add(asset);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(restore, false);
            }
        }

        [Test]
        public void ShadowDistanceIncreasesWithThePreset()
        {
            int restore = QualitySettings.GetQualityLevel();
            try
            {
                float previous = -1f;
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    float distance = QualitySettings.shadowDistance;

                    Assert.Greater(distance, previous,
                        QualitySettings.names[i] + " does not cost more than the preset below it");
                    previous = distance;
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(restore, false);
            }
        }

        [Test]
        public void TheDefaultSettingLandsOnARealPreset()
        {
            // GameSettings ships qualityLevel = 2. With two levels that silently clamped to
            // the top one; the default has to name a preset that exists.
            var defaults = new GameSettings();
            Assert.Less(defaults.qualityLevel, QualitySettings.names.Length,
                "the default quality level is outside the preset row");
            Assert.GreaterOrEqual(defaults.qualityLevel, 0);
        }

        [Test]
        public void EveryPresetHasBothLocalisedLabels()
        {
            foreach (var key in new[] { "ui.quality.low", "ui.quality.medium", "ui.quality.high" })
            {
                Assert.AreNotEqual(key, Loc.T(key), key + " is missing from strings.csv");
                Assert.IsNotEmpty(Loc.T(key));
            }
        }
    }
}
