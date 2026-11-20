using CloseTheDeal.MatchMode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Records
{
    /// <summary>
    /// Puts the match records, profile, secretary, first-run hints, death report and names over
    /// heads into every scene that holds a match, from one prefab, unless the scene already has
    /// them. So no scene has to be edited, and a scene rebuilt by a setup tool still gets them.
    /// The prefab is named by Resources/Gaps/RecordsInstaller.asset; delete that asset to turn
    /// this off. Design: docs/systems/RECORDS.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Records/Installer", fileName = "RecordsInstaller")]
    public sealed class RecordsInstaller : ScriptableObject
    {
        public const string ResourcePath = "Gaps/RecordsInstaller";

        [Tooltip("What is added to a scene with a match in it: the Records prefab built by Seal the Deal > Lanes > Gaps.")]
        [SerializeField] GameObject _prefab;

        public GameObject Prefab => _prefab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

        /// <summary>Adds the prefab if this scene has a match and no records yet. Returns the instance, or null.</summary>
        public static GameObject Install()
        {
            if (Object.FindAnyObjectByType<MatchDirector>(FindObjectsInactive.Include) == null
                || Object.FindAnyObjectByType<MatchRecords>(FindObjectsInactive.Include) != null)
                return null;

            var installer = Resources.Load<RecordsInstaller>(ResourcePath);
            if (installer == null || installer._prefab == null)
            {
                Debug.LogWarning("[Records] No installer prefab (Resources/" + ResourcePath + "); match records, profile and names over heads are off in this scene. Run Seal the Deal > Lanes > Gaps > Build Records Prefab.");
                return null;
            }

            GameObject instance = Instantiate(installer._prefab);
            instance.name = installer._prefab.name;
            return instance;
        }

#if UNITY_EDITOR
        /// <summary>Editor tools only: fills a freshly created asset.</summary>
        public void EditorInit(GameObject prefab) => _prefab = prefab;
#endif
    }
}
