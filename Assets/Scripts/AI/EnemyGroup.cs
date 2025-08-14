using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

        [Header("Selection and Radius Scaling")]
        public static EnemyGroup Active;
        public KeyCode growKey = KeyCode.Equals;   // +  (patrol radius)
        public KeyCode shrinkKey = KeyCode.Minus;  // -  (patrol radius)
        public float radiusStep = 1f;

        [Header("Group Enemy Scaling (only affects Active group)")]
        public KeyCode memberShrinkKey = KeyCode.LeftBracket;   // [
        public KeyCode memberGrowKey = KeyCode.RightBracket;  // ]
        public float memberScaleStep = 1.10f;  // 10% per tap
        public float memberMinScale = 0.25f;
        public float memberMaxScale = 3.0f;

        [Header("Visuals")]
        public bool randomizeColorOnStart = true;

        // Registered enemies in this group
        private readonly List<EnemyAI> members = new List<EnemyAI>();

        public IReadOnlyList<EnemyAI> Members => members;


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

            if (col is SphereCollider sc)
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

            if (areaRenderer != null)
            {
                areaBaseColor = areaRenderer.material.color;

                if (randomizeColorOnStart)
                {
                    float h = Random.value;
                    float s = 0.6f;
                    float v = 0.5f;
                    Color rand = Color.HSVToRGB(h, s, v);
                    rand.a = areaBaseColor.a;
                    areaRenderer.material.color = rand;
                    areaBaseColor = rand;
                }
            }
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
            // Patrol radius hotkeys + selection highlight
            if (Active == this)
            {
                if (Input.GetKeyDown(growKey)) patrolRadius += radiusStep;
                if (Input.GetKeyDown(shrinkKey)) patrolRadius = Mathf.Max(1f, patrolRadius - radiusStep);

                // Group-only member scaling
                if (Input.GetKeyDown(memberGrowKey)) ScaleMembers(memberScaleStep);
                if (Input.GetKeyDown(memberShrinkKey)) ScaleMembers(1f / memberScaleStep);

                SetHighlight(true);
            }
            else
            {
                SetHighlight(false);
            }

            // Hunt countdown -> teleport home
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

            var mat = areaRenderer.material;   // instance

            Color baseC = areaBaseColor;
            if (on)
            {
                Color lit = Color.Lerp(baseC, Color.white, 0.25f);
                mat.color = lit;
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

            // No parent so group movement/teleport does not drag enemies
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

        // ---- Group member scaling ----
        void ScaleMembers(float factor)
        {
            for (int i = members.Count - 1; i >= 0; i--)
            {
                var ai = members[i];
                if (ai == null) { members.RemoveAt(i); continue; }

                // clamp per-enemy
                float current = ai.transform.localScale.x;
                float clamped = Mathf.Clamp(current * factor, memberMinScale, memberMaxScale);
                float actual = clamped / current;

                // visual scale
                ai.transform.localScale = new Vector3(clamped, clamped, clamped);

                // scale gameplay ranges
                ai.viewDistance *= actual;
                ai.hearingRadius *= actual;
                ai.attackRange *= actual;

                // keep agent hull in sync
                var agent = ai.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.radius *= actual;
                    agent.height *= actual;
                    // keep attack stopping distance near edge of new range
                    if (ai.enabled && agent.enabled && agent.stoppingDistance > 0f)
                        agent.stoppingDistance = Mathf.Max(0.01f, ai.attackRange * 0.9f);
                }

                // refresh cone distance immediately
                ai.ApplyVisionForExternalChange();
                // hearing visual auto-updates in EnemyAI.Update()
            }
        }
    }
}

