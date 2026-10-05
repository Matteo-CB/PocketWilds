using System.IO;
using TMPro;
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

        /// <summary>
        /// Review captures only (T31 font review): every TextMeshPro text gets this font right before the render, and
        /// texts of at least <see cref="TitleMinSize"/> get <see cref="TitleFontOverride"/> when set. Captured scenes are
        /// reopened from disk afterwards, so nothing is saved. Null in normal captures.
        /// </summary>
        public static TMP_FontAsset FontOverride;
        public static TMP_FontAsset TitleFontOverride;
        public static float TitleMinSize = 44f;

        /// <summary>Size factors that give each candidate the same average text width as the font it replaces (fair comparison).</summary>
        public static float FontScale = 1f;
        public static float TitleScale = 1f;

        private static void ApplyFontOverride()
        {
            if (FontOverride == null)
                return;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Converted once: later renders of the same scene must not convert it again.
                bool converted = text.font == FontOverride || (TitleFontOverride != null && text.font == TitleFontOverride);
                bool title = TitleFontOverride != null && (text.fontSize >= TitleMinSize || text.name == "Title");
                var font = title ? TitleFontOverride : FontOverride;
                if (!converted)
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                    text.fontSize *= title ? TitleScale : FontScale;
                    // A title font is already heavy: no faux bold on top of it.
                    if (title)
                        text.fontStyle &= ~FontStyles.Bold;
                }
                text.ForceMeshUpdate(true, true);
            }
            Canvas.ForceUpdateCanvases();
        }

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
                ApplyFontOverride();
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
