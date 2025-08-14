using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Misadventures.AI
{
    [RequireComponent(typeof(Collider))]
    public class EnemyGroup : MonoBehaviour
    {
        [Header("Patrol Area")]
        public float patrolRadius = 10f;
        public Color highlightColor = new Color(1f, 1f, 1f, 0.5f);

        [Header("Home and Spawn")]
        public Transform homeAnchor;           // where the group returns
        public Transform spawnPoint;           // where enemies spawn
        public GameObject enemyPrefab;

        [Header("Hunt")]
        public float huntDuration = 7f;        // time to keep area at last seen
        public float returnDelay = 0.1f;       // tiny delay before teleport home

        [Header("Selection and Scaling")]
        public static EnemyGroup Active;
        public KeyCode growKey = KeyCode.Equals;   // +
        public KeyCode shrinkKey = KeyCode.Minus;  // -
        public float radiusStep = 1f;

        private readonly List<EnemyAI> members = new List<EnemyAI>();
        private Vector3 originalHome;
        private float huntTimer;
        private bool atLastSeen;
        private Renderer areaRenderer;
        private Color areaBaseColor;

        void Awake()
        {
            if (!TryGetComponent<RuntimeDraggable>(out _))
                gameObject.AddComponent<RuntimeDraggable>();

            if (!TryGetComponent<Collider>(out var col))
                col = gameObject.AddComponent<SphereCollider>();

            var sc = col as SphereCollider;
            if (sc != null)
            {
                sc.radius = 1f;
                sc.isTrigger = true;
            }
        }

        void Start()
        {
            ResetHome();

            var gv = GetComponent<GroupVisuals>();
            if (gv != null) areaRenderer = gv.AreaRenderer;
            if (areaRenderer != null) areaBaseColor = areaRenderer.sharedMaterial.color;
        }

        public void ResetHome()
        {
            originalHome = (homeAnchor != null) ? homeAnchor.position : transform.position;
            transform.position = originalHome;
            huntTimer = 0f;
            atLastSeen = false;
        }

        void Update()
        {
            if (Active == this)
            {
                if (Input.GetKeyDown(growKey)) patrolRadius += radiusStep;
                if (Input.GetKeyDown(shrinkKey)) patrolRadius = Mathf.Max(1f, patrolRadius - radiusStep);
                SetHighlight(true);
            }
            else
            {
                SetHighlight(false);
            }

            if (atLastSeen)
            {
                huntTimer -= Time.deltaTime;
                if (huntTimer <= 0f)
                {
                    if (returnDelay > 0f) Invoke(nameof(TeleportHome), returnDelay);
                    else TeleportHome();
                    atLastSeen = false;
                }
            }
        }

        void TeleportHome()
        {
            transform.position = (homeAnchor != null) ? homeAnchor.position : originalHome;
        }

        void SetHighlight(bool on)
        {
            if (areaRenderer == null) return;

            // work on an instance so we do not affect other groups
            var mat = areaRenderer.material;

            // start from the original color (keep alpha)
            Color baseC = areaBaseColor;

            if (on)
            {
                // slightly brighter and a hint of emission
                Color lit = Color.Lerp(baseC, Color.white, 0.25f);   // 25% toward white
                mat.color = lit;

                // gentle emission (retain original hue)
                Color emissive = lit * 0.35f;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissive);
            }
            else
            {
                mat.color = baseC;
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
        }


        void OnMouseDown()
        {
            Active = this;
        }

        public void Register(EnemyAI ai)
        {
            if (!members.Contains(ai)) members.Add(ai);
            ai.group = this;
        }

        public void Unregister(EnemyAI ai)
        {
            members.Remove(ai);
        }

        public void SpawnEnemy()
        {
            if (enemyPrefab == null || spawnPoint == null)
            {
                Debug.LogError("EnemyGroup: Assign enemyPrefab and spawnPoint.");
                return;
            }

            // NOTE: no parent so group movement does not drag enemies
            var obj = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
            var ai = obj.GetComponent<EnemyAI>() ?? obj.AddComponent<EnemyAI>();
            Register(ai);
        }

        // Called when an enemy has VISUAL on a player
        public void SetHuntCenter(Vector3 worldPos, float duration)
        {
            transform.position = worldPos; // snap area to last seen
            huntTimer = duration;
            atLastSeen = true;
        }
    }
}
