using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using NO404.Core;
using NO404.Gameplay;
using NO404.Visitors;
using NO404.CCTV;

namespace NO404.Tests
{
    public sealed class FirstGuestCharacterTests
    {
        [UnityTest]
        public IEnumerator FirstScheduledCallerHasArtAndKeepsItAfterAdmission()
        {
            float timeout = Time.realtimeSinceStartup + 45f;
            while (!ServiceHub.Ready && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(ServiceHub.Ready);
            GameLoop.Instance.NewGame(false);
            while (!GameLoop.Instance.WorldBuilt && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            // Finish the opening radio, then advance to the authored first-main beat.
            // The caller must still come from the real schedule, never a test-only enqueue.
            ServiceHub.Dialogue.End();
            ServiceHub.Clock.SetGameSecond(GameClock.ShiftStartSecond + 7 * 60 + 1);
            while ((!GameLoop.Instance.WorldBuilt || ServiceHub.Interphone.Active == null) &&
                   Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsNotNull(ServiceHub.Interphone.Active, "The new game never scheduled its first visitor.");
            Assert.AreEqual(VisitorAppearance.FirstGuestId, ServiceHub.Interphone.Active.visitorId);
            yield return null;
            var caller = GameObject.Find("CALLER_" + VisitorAppearance.FirstGuestId);
            Assert.IsNotNull(caller);
            Assert.IsNotNull(caller.GetComponent<FirstGuestMotion>(), "Caller fell back to the capsule.");
            Assert.AreEqual(0, caller.GetComponentsInChildren<Collider>().Length);
            var skin = caller.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skin, "The reference character must use a deforming skin, not rigid primitive parts.");
            Assert.GreaterOrEqual(skin.bones.Length, 16);
            Assert.Greater(skin.sharedMesh.vertexCount, 10000);
            Assert.IsNotNull(skin.sharedMaterial.GetTexture("_BaseMap"));
            Assert.GreaterOrEqual(skin.sharedMaterial.GetTexture("_BaseMap").width, 2048);

            var rig = Object.FindFirstObjectByType<CctvRig>();
            var camera = rig.CameraFor("CAM-01");
            Assert.IsNotNull(camera);
            var face = camera.WorldToViewportPoint(caller.transform.position + Vector3.up * 1.65f);
            Assert.That(face.x, Is.InRange(.1f, .9f));
            Assert.That(face.y, Is.InRange(.1f, .95f));
            Assert.Greater(face.z, camera.nearClipPlane);
            // Check actual face-facing geometry, not just the transform at the feet.
            Assert.Greater(Vector3.Dot(caller.transform.forward,
                (camera.transform.position - caller.transform.position).normalized), 0f);
            rig.SetVisible(new[] { "CAM-01" }, "CAM-01", true);
            for (int i = 0; i < 8; i++) yield return null;
            Capture(rig.TextureFor("CAM-01"), "FirstGuest_Interphone.png");

            var shot = new GameObject("FirstGuestValidationCamera");
            var photo = shot.AddComponent<Camera>();
            photo.transform.position = caller.transform.position + new Vector3(.10f, 1.50f, -1.85f);
            photo.transform.LookAt(caller.transform.position + Vector3.up * .95f);
            photo.fieldOfView = 54;
            photo.nearClipPlane = .05f;
            var rt = new RenderTexture(1000, 1000, 24);
            photo.targetTexture = rt;
            for (int i = 0; i < 4; i++) yield return null;
            Capture(rt, "FirstGuest_InGame.png");
            photo.targetTexture = null;
            Object.Destroy(shot); rt.Release(); Object.Destroy(rt);

            ServiceHub.Interphone.Grant(VisitorAccessLevel.FloorPass);
            yield return null; yield return null;
            Assert.IsNull(GameObject.Find("CALLER_" + VisitorAppearance.FirstGuestId));
            var admitted = GameObject.Find("VISITOR_" + VisitorAppearance.FirstGuestId);
            Assert.IsNotNull(admitted);
            Assert.IsNotNull(admitted.GetComponent<FirstGuestMotion>());
            Assert.IsNotNull(ServiceHub.ActiveVisitors.Find(VisitorAppearance.FirstGuestId));

            // Pause must freeze the rig, and resuming with a walk must move the joints.
            var motion = admitted.GetComponent<FirstGuestMotion>();
            var joint = System.Array.Find(admitted.GetComponentsInChildren<Transform>(), t => t.name == "LeftLegPivot");
            Assert.IsNotNull(joint);
            Quaternion before = joint.localRotation;
            var admittedSkin = admitted.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            admittedSkin.BakeMesh(baked);
            var beforeVertices = baked.vertices;
            motion.Tick(0f, VisitorBodies.WalkSpeed);
            Assert.Less(Quaternion.Angle(before, joint.localRotation), .001f);
            motion.Tick(.15f, VisitorBodies.WalkSpeed);
            Assert.Greater(Quaternion.Angle(before, joint.localRotation), 1f);
            admittedSkin.BakeMesh(baked);
            var afterVertices = baked.vertices;
            float deformation = 0f;
            for (int i = 0; i < beforeVertices.Length; i++)
                deformation = Mathf.Max(deformation, Vector3.Distance(beforeVertices[i], afterVertices[i]));
            Object.Destroy(baked);
            Assert.Greater(deformation, .01f, "Moving the joints must deform the mesh vertices.");
            AssertRigidShoes();

            ServiceHub.ActiveVisitors.EndNight();
            yield return null; yield return null;
            Assert.IsNull(GameObject.Find("VISITOR_" + VisitorAppearance.FirstGuestId));
        }

        static void AssertRigidShoes()
        {
            var root = Object.Instantiate(Resources.Load<GameObject>(VisitorAppearance.FirstGuestResource));
            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
            var motion = root.GetComponent<FirstGuestMotion>();
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh);
                var rest = mesh.vertices;
                var shoes = new System.Collections.Generic.List<int>[]
                {
                    new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>()
                };
                // Select the entire shoe, including its collar, in the neutral pose.
                for (int i = 0; i < rest.Length; i++)
                {
                    var point = root.transform.InverseTransformPoint(skin.transform.TransformPoint(rest[i]));
                    if (point.y < .16f)
                        shoes[point.x >= 0 ? 0 : 1].Add(i);
                }
                foreach (var shoe in shoes) Assert.Greater(shoe.Count, 100);
                // Cover several complete strides; every shoe vertex keeps its distances
                // to two anchors, even while the leg, ankle and character are rotated.
                for (int frame = 0; frame < 72; frame++)
                {
                    motion.Tick(1f / 30f, VisitorBodies.WalkSpeed);
                    skin.BakeMesh(mesh);
                    var posed = mesh.vertices;
                    foreach (var shoe in shoes)
                        foreach (int anchor in new[] { shoe[0], shoe[shoe.Count / 2] })
                            foreach (int index in shoe)
                                Assert.That(Vector3.Distance(posed[index], posed[anchor]),
                                    Is.EqualTo(Vector3.Distance(rest[index], rest[anchor])).Within(.0002f),
                                    "The shoe stretched during a stride.");
                }
            }
            finally { Object.Destroy(mesh); Object.Destroy(root); }
        }

        static void Capture(RenderTexture source, string name)
        {
            Assert.IsNotNull(source);
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                image.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Art/FirstGuest"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }
    }
}
