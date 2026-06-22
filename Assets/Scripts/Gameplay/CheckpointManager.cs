using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AdequateEnough
{
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        [SerializeField] private string mainMenuScene = "MainMenu";

        private Vector2 respawnPosition;
        private readonly HashSet<Checkpoint> visited = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Start()
        {
            // Default respawn to the player's starting position if no checkpoint has been activated yet.
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                respawnPosition = player.transform.position;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.Equals(mainMenuScene, System.StringComparison.OrdinalIgnoreCase))
                Reset();
        }

        public void SetCheckpoint(Checkpoint checkpoint, Vector2 playerPosition)
        {
            // Once a checkpoint is visited it's locked in — re-entering it won't clobber a later one.
            if (!visited.Add(checkpoint)) return;
            respawnPosition = playerPosition;
        }

        public Vector2 GetRespawnPosition() => respawnPosition;

        private void Reset()
        {
            visited.Clear();
            respawnPosition = default;
        }
    }
}
