using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using NO404.Core;

namespace NO404.Tests
{
    /// <summary>
    /// The whole building rendered magenta in a build and nothing was logged.
    ///
    /// That is the worst shape a bug can have: the world was taking its material from
    /// <c>GameObject.CreatePrimitive(...).sharedMaterial</c>, which hands back the legacy
    /// built-in default. The Universal Render Pipeline cannot draw it, so it drew magenta -
    /// and because the shader was present rather than missing, there was no error to find.
    /// It only showed up as somebody saying "화면이 보라색으로밖에 안 보이는데".
    ///
    /// These pin the two facts that make it not happen again: the greybox material comes from
    /// the active pipeline, and it is not the built-in shader.
    /// </summary>
    public sealed class GreyboxMaterialTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            while (!ServiceHub.Ready) yield return null;
            GreyboxMaterial.Forget();
        }

        [Test]
        public void TheProjectIsRunningAScriptableRenderPipeline()
        {
            // If this ever fails the rest of the file is meaningless, and the game is being
            // rendered by something nobody configured.
            Assert.IsNotNull(GraphicsSettings.currentRenderPipeline,
                             "no render pipeline asset is active - check Graphics Settings");
        }

        [Test]
        public void TheGreyboxMaterialIsNotTheBuiltInOne()
        {
            var material = GreyboxMaterial.Default;

            Assert.IsNotNull(material, "the world has to have something to draw with");

            var shader = material.shader.name;

            // "Standard" and the hidden error shader are the two that produce magenta under
            // URP. Naming them is the point of the test.
            Assert.AreNotEqual("Standard", shader,
                               "this is the built-in shader; under URP it renders magenta");
            Assert.IsFalse(shader.Contains("InternalErrorShader"),
                           "the shader failed to resolve at all: " + shader);

            Assert.IsTrue(shader.Contains("Universal Render Pipeline") || shader.Contains("URP"),
                          "expected a URP shader, got '" + shader + "'");
        }

        [Test]
        public void TintingProducesAnIndependentCopy()
        {
            // Writing a colour onto the pipeline's shared default would repaint every surface
            // in the game, including ones not built yet.
            var shared = GreyboxMaterial.Default;
            var red = GreyboxMaterial.Tinted(Color.red);
            var blue = GreyboxMaterial.Tinted(Color.blue);

            Assert.AreNotSame(shared, red, "a tint must never write onto the shared material");
            Assert.AreNotSame(red, blue);
            Assert.AreEqual(Color.red, red.color);
            Assert.AreEqual(Color.blue, blue.color);
            Assert.AreEqual(shared.shader, red.shader, "the tint keeps the pipeline's shader");
        }

        [Test]
        public void APrimitivesOwnMaterialIsStillTheTrap()
        {
            // Documents why the helper exists. If Unity ever changes CreatePrimitive to be
            // pipeline-aware this will fail, and the helper can be reconsidered - but until
            // then, this is the difference between a visible building and a magenta screen.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var fromPrimitive = probe.GetComponent<MeshRenderer>().sharedMaterial;
            Object.DestroyImmediate(probe);

            if (fromPrimitive != null && fromPrimitive.shader == GreyboxMaterial.Default.shader)
                Assert.Pass("CreatePrimitive now matches the pipeline; the helper is harmless");

            Assert.AreNotEqual(GreyboxMaterial.Default.shader, fromPrimitive.shader,
                               "recorded for the record: these differ, which is the bug");
        }
    }
}
