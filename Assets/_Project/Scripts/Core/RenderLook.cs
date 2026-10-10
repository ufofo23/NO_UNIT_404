using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NO404.Core
{
    /// <summary>
    /// The camera look: what turns lit boxes into something closer to a photograph of a
    /// building at night. Filmic tonemapping, bloom on the tubes and the city, a slightly
    /// desaturated cool grade, a vignette and fine grain - the things a real lens and sensor
    /// add that the greybox never had.
    ///
    /// Built in code like everything else, as one global volume, so no profile asset has to
    /// be kept in step with it. Only the player's camera post-processes; the CCTV lenses keep
    /// their own flat feed look.
    /// </summary>
    public static class RenderLook
    {
        static Volume _volume;

        public static void Install(Camera camera)
        {
            if (camera == null) return;

            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            if (_volume != null) return;

            // Every room renders its own reflection once (WorldBuilder.ReflectionProbes).
            QualitySettings.realtimeReflectionProbes = true;

            var go = new GameObject("[NO404 Look]");
            Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "NO404_Look";

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);

            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.75f);
            colour.contrast.Override(14f);
            colour.saturation.Override(-14f);
            colour.colorFilter.Override(new Color(0.96f, 0.99f, 1f));

            var balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(-6f);
            balance.tint.Override(2f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.8f);

            var fringe = profile.Add<ChromaticAberration>(true);
            fringe.intensity.Override(0.05f);

            _volume.sharedProfile = profile;
        }

        /// <summary>
        /// Graded rather than flat ambient: a little cooler from above, darker from below,
        /// so a room's floor and ceiling stop reading as the same shade of grey.
        /// </summary>
        public static void ApplyAmbient(float lift)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.13f, 0.16f) * lift;
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.10f, 0.105f) * lift;
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.048f, 0.045f) * lift;
        }
    }
}
