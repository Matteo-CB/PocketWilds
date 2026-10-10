using UnityEngine;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T43: the garden set's anchors, filled by the set pipeline (agent GP) on <see cref="GardenLayout.SetPrefabName"/>:
    /// the five beds along their path, where a plant stands in each, the covers of the locked beds, the camera framing, the
    /// watering can's rest place and the basket the harvests fly into.
    /// </summary>
    public sealed class GardenSetView : MonoBehaviour
    {
        [Tooltip("The camera of the set (portrait framing of the whole row).")]
        public Camera setCamera;

        [Tooltip("The five beds, plot 0 nearest the camera (neighbours are consecutive).")]
        public Transform[] beds = new Transform[GardenLayout.PlotCount];

        [Tooltip("Where a plant stands in each bed (its root on the soil).")]
        public Transform[] plantAnchors = new Transform[GardenLayout.PlotCount];

        [Tooltip("A soft cover and sign on each bed while it is locked (null for the three owned beds).")]
        public GameObject[] lockedCovers = new GameObject[GardenLayout.PlotCount];

        [Tooltip("Each bed's touch area in world units (its top face, a little larger than the soil).")]
        public Vector2[] bedSizes = new Vector2[GardenLayout.PlotCount];

        [Tooltip("The watering can at rest, and the basket the harvests fly into.")]
        public Transform wateringCan;

        public Transform basket;

        [Tooltip("The set's own light and ambient (applied while the garden shows, the refuge's given back after).")]
        public Light sun;

        public Color ambientSky = new Color(0.78f, 0.86f, 0.95f);
        public Color ambientEquator = new Color(0.72f, 0.8f, 0.66f);
        public Color ambientGround = new Color(0.45f, 0.5f, 0.35f);
    }
}
