#nullable enable
using PocketWilds.Simulation;
using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42: the postcard of an expedition (doc 01 Contenu, section 7.1): the team posed in the destination's postcard set,
    /// 512 × 768, rendered once at the welcome and stored as <c>Postcards/postcard_{id}.png</c> under the persistent data
    /// path. The expeditions screen asks for it and shows it; the refuge journal (T44) will read the stored files. Frozen
    /// for T42: the postcard renderer implements it, the screen only uses it.
    /// </summary>
    public interface IPostcardRenderer
    {
        /// <summary>
        /// Starts the postcard of a welcome: loads the destination's set the first time, poses the team, renders one frame,
        /// reads it back without stalling and writes the file off the main thread, then calls
        /// <see cref="IExpeditions.PostcardRendered"/>. One postcard at a time; a second call while busy is ignored.
        /// </summary>
        void Begin(in ExpeditionReturn welcome);

        /// <summary>Rendering or writing.</summary>
        bool Busy { get; }

        /// <summary>The picture of the last postcard begun, once rendered (null before); the same texture until <see cref="Release"/>.</summary>
        Texture2D? Picture { get; }

        /// <summary>Per frame from the screen's presenter while the screen is open (no allocation).</summary>
        void Tick();

        /// <summary>Lets go of the picture and the set (the screen closed).</summary>
        void Release();
    }

    /// <summary>
    /// T42: the refuge's side of the expeditions (implemented by the main session in RefugeController). The screen calls it;
    /// the refuge never shows an animal that is away (<see cref="IExpeditions.IsAway"/>).
    /// </summary>
    public interface IExpeditionWorld
    {
        /// <summary>The team left (the screen covers the world): their views go back to the pool, animals from the reserve take their places.</summary>
        void TeamLeft(uint[] team, int count);

        /// <summary>
        /// The team is welcomed (doc 05 sheet 23): with the world shown, they walk in from the edge of the view and hop
        /// happily on arrival, a small dust puff each (Reduce Motion: they fade in at their places). Returns the seconds until
        /// the last one arrived (1.2 at most); the postcard pivots in after that.
        /// </summary>
        float TeamBack(uint[] team, int count);
    }

    /// <summary>T42: where the postcards live and how the destination cards are named (shared by the renderer, the screen, the pipelines).</summary>
    public static class PostcardLayout
    {
        public const int Width = 512;
        public const int Height = 768;

        /// <summary>The folder under <c>Application.persistentDataPath</c>.</summary>
        public const string Folder = "Postcards";

        public static string FileName(int postcardId) => "postcard_" + postcardId + ".png";

        /// <summary>The destination card art made in the editor from the set without animals: EXP_{suffix of the runtime id}.</summary>
        public const string CardArtFolder = "Assets/_Game/Art/UI/Activities/Expeditions/Destinations";

        public static string CardArtName(string destinationKey)
        {
            int dot = destinationKey.LastIndexOf('.');
            return "EXP_" + (dot >= 0 ? destinationKey.Substring(dot + 1) : destinationKey);
        }

        /// <summary>The Addressables group of the screen and the postcard sets.</summary>
        public const string Group = "Activity_Expeditions";
    }
}
