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
        public float patrolRadius = 10f;                     // Radius of the patrol zone
        public Color highlightColor = new Color(1f, 1f, 1f, 0.5f); // Highlight overlay color when selected

        [Header("Home and Spawn")]
        public Transform homeAnchor;     // Transform where the group resets and returns to
        public Transform spawnPoint;     // Transform used as the spawn location for enemies
        public GameObject enemyPrefab;   // Prefab template for spawning new enemies

        [Header("Hunt")]
        public float huntDuration = 7f;  // Duration to hold at the "last seen" position
        public float returnDelay = 0.1f; // Delay before teleporting back home after hunt ends

        [Header("Selection and Radius Scaling")]
        public static EnemyGroup Active;           // Reference to the currently selected/active group
        public KeyCode growKey = KeyCode.Equals;   // Hotkey to increase patrol radius
        public KeyCode shrinkKey = KeyCode.Minus;  // Hotkey to decrease patrol radius
        public float radiusStep = 1f;              // Increment/decrement step for patrol radius

        [Header("Group Enemy Scaling (only affects Active group)")]
        public KeyCode memberShrinkKey = KeyCode.LeftBracket;    // Hotkey to shrink group members
        public KeyCode memberGrowKey = KeyCode.RightBracket;     // Hotkey to grow group members
        public float memberScaleStep = 1.10f;    // Scale multiplier per keypress (10% change)
        public float memberMinScale = 0.25f;     // Minimum allowed member scale
        public float memberMaxScale = 3.0f;      // Maximum allowed member scale

        [Header("Visuals")]
        public bool randomizeColorOnStart = true; // Whether to randomize patrol area color on Start

        // Collection of registered EnemyAI agents belonging to this group
        private readonly List<EnemyAI> members = new List<EnemyAI>();
        public IReadOnlyList<EnemyAI> Members => members;

        // Internal state and visuals
        private Vector3 originalHome;       // Cached home position for resets
        private float huntTimer;            // Countdown timer for hunt state
        private bool atLastSeen;            // Whether the group is stationed at last seen position
        private Renderer areaRenderer;      // Renderer for patrol area visualization
        private Color areaBaseColor;        // Base color of patrol area material

        private void Awake()
        {
            // Ensure group can be dragged in editor/runtime
            if (!TryGetComponent<RuntimeDraggable>(out _))
                gameObject.AddComponent<RuntimeDraggable>();

            // Ensure collider exists; default to a trigger sphere
            if (!TryGetComponent<Collider>(out var col))
                col = gameObject.AddComponent<SphereCollider>();

            if (col is SphereCollider sc)
            {
                sc.radius = 1f;
                sc.isTrigger = true;
            }
        }

        private void Start()
        {
            ResetHome();

            // Cache group visuals and base material color
            var gv = GetComponent<GroupVisuals>();
            if (gv != null) areaRenderer = gv.AreaRenderer;
            if (areaRenderer != null) areaBaseColor = areaRenderer.sharedMaterial.color;

            // Optionally randomize patrol area color for group differentiation
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
            // Reset group position/state to home anchor or original placement
            originalHome = (homeAnchor != null) ? homeAnchor.position : transform.position;
            transform.position = originalHome;
            huntTimer = 0f;
            atLastSeen = false;
        }

        private void Update()
        {
            // Handle patrol radius scaling and highlighting when this group is active
            if (Active == this)
            {
                if (Input.GetKeyDown(growKey)) patrolRadius += radiusStep;
                if (Input.GetKeyDown(shrinkKey)) patrolRadius = Mathf.Max(1f, patrolRadius - radiusStep);

                if (Input.GetKeyDown(memberGrowKey)) ScaleMembers(memberScaleStep);
                if (Input.GetKeyDown(memberShrinkKey)) ScaleMembers(1f / memberScaleStep);

                SetHighlight(true);
            }
            else
            {
                SetHighlight(false);
            }

            // Handle hunt countdown and return to home after hunt duration ends
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

        private void TeleportHome()
        {
            // Instantly move patrol area back to home anchor or original location
            transform.position = (homeAnchor != null) ? homeAnchor.position : originalHome;
        }

        private void SetHighlight(bool on)
        {
            if (areaRenderer == null) return;

            var mat = areaRenderer.material;

            if (on)
            {
                // Lighten base color and apply emissive glow for selection
                Color lit = Color.Lerp(areaBaseColor, Color.white, 0.25f);
                mat.color = lit;
                Color emissive = lit * 0.35f;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissive);
            }
            else
            {
                // Restore base color and disable emission
                mat.color = areaBaseColor;
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
        }

        private void OnMouseDown()
        {
            // Select this group on mouse click
            Active = this;
        }

        public void Register(EnemyAI ai)
        {
            // Add new member to group registry and set its group reference
            if (!members.Contains(ai)) members.Add(ai);
            ai.group = this;
        }

        public void Unregister(EnemyAI ai)
        {
            // Remove enemy from registry
            members.Remove(ai);
        }

        public void SpawnEnemy()
        {
            // Spawns a new enemy instance under this group
            if (enemyPrefab == null || spawnPoint == null)
            {
                Debug.LogError("EnemyGroup: Assign enemyPrefab and spawnPoint.");
                return;
            }

            var obj = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
            var ai = obj.GetComponent<EnemyAI>() ?? obj.AddComponent<EnemyAI>();
            Register(ai);
        }

        public void SetHuntCenter(Vector3 worldPos, float duration)
        {
            // Move patrol area to last seen position and start hunt timer
            transform.position = worldPos;
            huntTimer = duration;
            atLastSeen = true;
        }

        // Scales all registered group members uniformly by a given factor, clamped by limits
        private void ScaleMembers(float factor)
        {
            for (int i = members.Count - 1; i >= 0; i--)
            {
                var ai = members[i];
                if (ai == null) { members.RemoveAt(i); continue; }

                float current = ai.transform.localScale.x;
                float clamped = Mathf.Clamp(current * factor, memberMinScale, memberMaxScale);
                float actual = clamped / current;

                // Apply new transform scale
                ai.transform.localScale = new Vector3(clamped, clamped, clamped);

                // Adjust gameplay-relevant ranges to scale with size
                ai.viewDistance *= actual;
                ai.hearingRadius *= actual;
                ai.attackRange *= actual;

                // Update navigation agent size to match new scale
                var agent = ai.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.radius *= actual;
                    agent.height *= actual;
                    if (ai.enabled && agent.enabled && agent.stoppingDistance > 0f)
                        agent.stoppingDistance = Mathf.Max(0.01f, ai.attackRange * 0.9f);
                }

                // Immediately refresh vision cone to reflect new range
                ai.ApplyVisionForExternalChange();
            }
        }
    }
}
