using UnityEngine;
using UnityEngine.UI;
using ApexRush.Core;

namespace ApexRush.Menu
{
    /// <summary>
    /// Left/right car carousel. Shows a display model on a turntable and writes the
    /// chosen index to GameSettings.SelectedCarIndex — RaceManager's carPrefabs
    /// array in each track scene MUST use the same order as `entries` here.
    /// Display models are plain visual prefabs (no physics needed).
    /// </summary>
    public class CarSelectUI : MonoBehaviour
    {
        [System.Serializable]
        public class CarEntry
        {
            public string displayName = "Roadster";
            [TextArea] public string statsBlurb = "Top speed 220 km/h\nHandling ★★★☆";
            public GameObject displayModel;   // scene object under the turntable, one per car
        }

        [SerializeField] private CarEntry[] entries;
        [SerializeField] private Text nameText;
        [SerializeField] private Text statsText;
        [SerializeField] private Transform turntable;      // parent of all display models
        [SerializeField] private float turntableSpeed = 25f;

        private void OnEnable() => Refresh();

        private void Update()
        {
            if (turntable != null)
                turntable.Rotate(0f, turntableSpeed * Time.deltaTime, 0f);
        }

        public void NextCar() => Cycle(+1);
        public void PreviousCar() => Cycle(-1);

        private void Cycle(int dir)
        {
            int count = entries.Length;
            GameSettings.SelectedCarIndex =
                (GameSettings.SelectedCarIndex + dir + count) % count;
            Refresh();
        }

        private void Refresh()
        {
            int idx = Mathf.Clamp(GameSettings.SelectedCarIndex, 0, entries.Length - 1);
            nameText.text = entries[idx].displayName;
            statsText.text = entries[idx].statsBlurb;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].displayModel != null)
                    entries[i].displayModel.SetActive(i == idx);
        }
    }
}
