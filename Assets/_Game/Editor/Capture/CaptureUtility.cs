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
