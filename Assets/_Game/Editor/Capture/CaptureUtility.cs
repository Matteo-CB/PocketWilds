using System.IO;
using UnityEngine;

namespace PocketWilds.Editor.Capture
{
    /// <summary>
    /// ED-008 Capture Tool core: renders a camera offscreen through URP and writes a PNG.
    /// One temporary RenderTexture per capture, released immediately.
    /// </summary>
    public static class CaptureUtility
    {
        public const string DefaultFolder = "TestResults/Captures";

        public static string Render(Camera camera, int width, int height, string path, int msaa = 4, System.Action beforeRender = null)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, msaa);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                beforeRender?.Invoke();
                camera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return path;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Renders through a throwaway copy of <paramref name="template"/>. Instanced draws (scatter, life, weather) are
        /// bound to a camera and stay queued for it until the editor frame ends: a fresh camera per shot keeps every
        /// frame clean of the previous shot's draws. <paramref name="draw"/> issues the draws for that camera.
        /// </summary>
        public static string RenderWithFreshCamera(Camera template, int width, int height, string path, System.Action<Camera> draw, int msaa = 4)
        {
            var go = new GameObject("CaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.transform.SetPositionAndRotation(template.transform.position, template.transform.rotation);
                var camera = go.AddComponent<Camera>();
                camera.CopyFrom(template);
                camera.enabled = false;
                var source = template.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                var data = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (data == null)
                    data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (source != null)
                {
                    data.renderPostProcessing = source.renderPostProcessing;
                    data.antialiasing = source.antialiasing;
                    data.renderShadows = source.renderShadows;
                }
                // First render may compile shader variants: render twice, keep the second.
                Render(camera, width, height, path, msaa, () => draw?.Invoke(camera));
                return Render(camera, width, height, path, msaa, () => draw?.Invoke(camera));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Renders through a throwaway copy of <paramref name="template"/> into a linear float target, cleared to
        /// <paramref name="clear"/>, without post-processing, antialiasing or MSAA, and returns the pixels: debug values
        /// (overdraw counts), never a picture.
        /// </summary>
        public static Color[] RenderLinear(Camera template, int width, int height, Color clear, System.Action<Camera> draw)
        {
            var go = new GameObject("CaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1);
            var texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            var previousActive = RenderTexture.active;
            try
            {
                go.transform.SetPositionAndRotation(template.transform.position, template.transform.rotation);
                var camera = go.AddComponent<Camera>();
                camera.CopyFrom(template);
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = clear;
                camera.allowMSAA = false;
                camera.allowHDR = true;
                var data = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                           ?? go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                data.renderShadows = false;
                camera.targetTexture = rt;
                // First render may compile shader variants: render twice, keep the second.
                for (int pass = 0; pass < 2; pass++)
                {
                    draw?.Invoke(camera);
                    camera.Render();
                }
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                camera.targetTexture = null;
                return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Renders a camera found by name. Convenience for MCP and batch callers.</summary>
        public static string Render(string cameraName, int width, int height, string fileName)
        {
            var go = GameObject.Find(cameraName);
            if (go == null || !go.TryGetComponent<Camera>(out var camera))
                throw new System.ArgumentException("Camera not found: " + cameraName);
            return Render(camera, width, height, Path.Combine(DefaultFolder, fileName + ".png"));
        }
    }
}
