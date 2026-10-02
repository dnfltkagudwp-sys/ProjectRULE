using UnityEngine;

namespace RuleGhost.Anomalies
{
    // Every texture/material the anomalies and death stings swap in at runtime, referenced
    // directly from the Lobby scene -- the same pattern as SoundBank. These used to be loaded by
    // path through UnityEditor.AssetDatabase, which only exists in the Editor: in a build every
    // lookup returned null, so the open-eyed portrait, the person in the landscape, the hygrometer's
    // routine/anomaly readouts and both painting death swaps silently never showed. A scene
    // reference is what makes the build include them at all.
    //
    // Arrays are indexed by painting index - 1 (North = portraits, West = landscapes). Filled by
    // RuleGhost/Anomalies/Wire Anomaly Visual Assets; one instance in the Lobby scene.
    public class AnomalyVisualAssets : MonoBehaviour
    {
        public static AnomalyVisualAssets Instance { get; private set; }

        [Header("이상현상 그림 (1~3번)")]
        [SerializeField] private Texture2D[] portraitEyesOpen = new Texture2D[3];
        [SerializeField] private Texture2D[] landscapePerson = new Texture2D[3];

        [Header("사망 연출 그림 (1~3번)")]
        [SerializeField] private Texture2D[] portraitDeath = new Texture2D[3];
        [SerializeField] private Texture2D[] landscapeDeath = new Texture2D[3];

        [Header("습도계 화면")]
        [SerializeField] private Material hygrometerNormal;
        [SerializeField] private Material hygrometerRoutine;
        [SerializeField] private Material hygrometerAnomaly;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public Texture2D PortraitEyesOpen(int index) => At(portraitEyesOpen, index);
        public Texture2D LandscapePerson(int index) => At(landscapePerson, index);
        public Texture2D PortraitDeath(int index) => At(portraitDeath, index);
        public Texture2D LandscapeDeath(int index) => At(landscapeDeath, index);

        public Material Hygrometer(HygrometerDisplay.State state) => state switch
        {
            HygrometerDisplay.State.Routine => hygrometerRoutine,
            HygrometerDisplay.State.Anomaly => hygrometerAnomaly,
            _ => hygrometerNormal
        };

        private static Texture2D At(Texture2D[] textures, int index)
        {
            return textures != null && index >= 1 && index <= textures.Length ? textures[index - 1] : null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Texture2D[] eyesOpen, Texture2D[] person, Texture2D[] portraitDeathTextures,
            Texture2D[] landscapeDeathTextures, Material normal, Material routine, Material anomaly)
        {
            portraitEyesOpen = eyesOpen;
            landscapePerson = person;
            portraitDeath = portraitDeathTextures;
            landscapeDeath = landscapeDeathTextures;
            hygrometerNormal = normal;
            hygrometerRoutine = routine;
            hygrometerAnomaly = anomaly;
        }
#endif
    }
}
