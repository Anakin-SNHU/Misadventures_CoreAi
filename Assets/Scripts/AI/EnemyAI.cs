using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Misadventures.AI
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Collider))]
    public class EnemyAI : MonoBehaviour
    {
        // ---------------- Inspector: configuration ----------------

        [Header("Group")]
        public EnemyGroup group;                       // Owning group; used for patrol center and hunt coordination

        [Header("Perception")]
        public float viewAngle = 70f;                  // Vision half-angle in degrees (full FOV = viewAngle * 2)
        public float viewDistance = 12f;               // Maximum straight-line vision distance
        public float hearingRadius = 14f;              // Planar hearing radius (Y ignored for checks/visual)
        public LayerMask visionObstacles;              // Reserved for occlusion rules (not currently used)

        [Header("Movement and Combat")]
        public float attackRange = 1.8f;               // Required distance to resolve a strike
        public float patrolPointTolerance = 0.6f;      // Distance considered “arrived” at a patrol waypoint
        public float patrolWait = 1.0f;                // Pause duration at each patrol waypoint
        public float huntSpeedMultiplier = 1.3f;       // Speed scale while in Hunting/Attack states

        [Header("Visuals")]
        public VisionCone visionCone;                  // Vision cone mesh to visualize FOV and range
        public Color selectedEmissionColor = Color.white; // Emission color when selected
        public float selectedEmissionStrength = 2.0f;  // Emission intensity multiplier when selected

        [Header("Hearing Visual")]
        public Material hearingMaterial;               // Material for the hearing radius cylinder
        public float hearingHeight = 0.05f;            // Y thickness/offset for the hearing cylinder

        [Header("Attack")]
        public int attackDamage = 10;                  // Damage applied on a successful strike
        public float attackWindup = 0.35f;             // Time spent winding up before applying damage
        public float attackRecovery = 0.25f;           // Post-attack recovery during which a new swing cannot start
        public Material attackPulseMaterial;           // Material for brief expanding ring showing attack reach

        [Header("Hunting")]
        public float huntingTimeout = 6f;              // Time allowed to search after losing visual before returning to patrol

        // ---------------- Runtime: state & caches ----------------

        private enum State { Patrol, Hunting, Attack } // High-level behavior state machine

        private State state;                            // Current AI state
        private NavMeshAgent agent;                     // Navigation agent controlling movement
        private Renderer rend;                          // Visual renderer used for selection emission
        private MaterialPropertyBlock mpb;              // Property block to write per-instance material properties
        private int emissionColorId;                    // Shader property ID for emission color

        private static EnemyAI active;                  // Currently selected EnemyAI for editor/runtime highlighting

        private PlayerProxy currentTarget;              // Current visible player (if any)
        private Vector3 targetPos;                      // Last known target position or current movement goal
        private float baseSpeed;                        // Cached agent.speed baseline
        private float stateTimer;                       // Generic timer (e.g., patrol waypoint wait)
        private float huntingTimer;                     // Countdown while in Hunting

        // Attack control
        private bool attacking;                         // True during windup; false during move/recovery
        private float attackTimer;                      // Tracks windup and recovery durations

        // Hearing visualization
        private Transform hearingVis;                   // Cylinder transform used to display hearing radius

        // Noise pulse deduplication
        private readonly HashSet<int> pulsesHandled = new HashSet<int>(); // Pulse IDs already reacted to

        // ---------------- Unity lifecycle ----------------

        private void OnEnable()
        {
            // Subscribe to instantaneous noises and expanding noise pulses
            NoiseSystem.OnNoiseEmitted += OnNoise;
            NoiseSystem.OnNoisePulse += OnNoisePulse;
        }

        private void OnDisable()
        {
            NoiseSystem.OnNoiseEmitted -= OnNoise;
            NoiseSystem.OnNoisePulse -= OnNoisePulse;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rend = GetComponentInChildren<Renderer>();
            baseSpeed = agent.speed;

            // Optional runtime drag helper to reposition agents during testing
            if (!TryGetComponent<RuntimeDraggable>(out _))
                gameObject.AddComponent<RuntimeDraggable>();

            if (visionCone == null)
                visionCone = GetComponentInChildren<VisionCone>();

            mpb = new MaterialPropertyBlock();
            emissionColorId = Shader.PropertyToID("_EmissionColor");

            // Conservative defaults; specific states set stricter values as needed
            agent.stoppingDistance = 0f;
            agent.autoBraking = true;
        }

        private void Start()
        {
            if (group != null) group.Register(this);
            GoPatrol();              // Enter initial state
            ApplyVisionToCone();     // Sync FOV/range visual
            BuildHearingVisual();    // Create hearing radius visual
        }

        private void Update()
        {
            // Selection highlight update
            SetSelected(this == active);

            // State tick
            switch (state)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Hunting: TickHunting(); break;
                case State.Attack: TickAttack(); break;
            }

            // Perception sweep (visual detection)
            ScanForPlayers();

            // Keep hearing visualization in sync
            UpdateHearingVisual();
        }

        private void OnMouseDown()
        {
            // Assign this instance as the active/selected agent
            if (active != null && active != this) active.SetSelected(false);
            active = this;
        }

        // ---------------- Utility ----------------

        private float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void SetSelected(bool enabled)
        {
            if (rend == null) return;

            // Write emission via property block to avoid instancing the shared material
            rend.GetPropertyBlock(mpb);
            var c = enabled ? selectedEmissionColor * selectedEmissionStrength : Color.black;

            if (enabled) rend.material.EnableKeyword("_EMISSION");
            else rend.material.DisableKeyword("_EMISSION");

            mpb.SetColor(emissionColorId, c);
            rend.SetPropertyBlock(mpb);
        }

        public void ApplyVisionForExternalChange() { ApplyVisionToCone(); }

        private void ApplyVisionToCone()
        {
            if (!visionCone) return;
            visionCone.angle = viewAngle;
            visionCone.distance = viewDistance;
        }

        // Considered arrived if within tolerance or the agent’s stopping distance
        private bool ArrivedAtPoint()
        {
            float arrive = Mathf.Max(patrolPointTolerance, agent.stoppingDistance + 0.05f);
            return !agent.pathPending && agent.remainingDistance <= arrive;
        }

        // ---------------- States ----------------

        private void GoPatrol()
        {
            state = State.Patrol;
            stateTimer = patrolWait;
            agent.speed = baseSpeed;
            agent.stoppingDistance = 0f; // Fully reach patrol waypoints
            agent.isStopped = false;
            SetNextPatrolPoint(false);
        }

        private void TickPatrol()
        {
            if (ArrivedAtPoint())
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    SetNextPatrolPoint(false);
                    stateTimer = patrolWait;
                }
            }
        }

        private void GoHunting(Vector3 center)
        {
            state = State.Hunting;
            huntingTimer = huntingTimeout;
            agent.speed = baseSpeed * huntSpeedMultiplier;
            agent.stoppingDistance = 0f; // Fully traverse search points
            agent.isStopped = false;

            targetPos = center;
            agent.SetDestination(targetPos);
        }

        private void TickHunting()
        {
            huntingTimer -= Time.deltaTime;
            if (huntingTimer <= 0f)
            {
                GoPatrol();
                return;
            }

            if (ArrivedAtPoint())
            {
                // Continue pacing around the hunt center
                SetNextPatrolPoint(true);
            }
        }

        private void GoAttack(Vector3 pos)
        {
            state = State.Attack;
            agent.speed = baseSpeed * huntSpeedMultiplier;

            // Stop close to target to allow in-place windup
            agent.stoppingDistance = Mathf.Max(0.01f, attackRange * 0.9f);
            agent.isStopped = false;

            targetPos = pos;
            agent.SetDestination(targetPos);

            // Attack loop begins in TickAttack; no immediate strike on entry
            attacking = false;
            attackTimer = 0f;
        }

        private void TickAttack()
        {
            float distToTarget = (currentTarget != null)
                ? PlanarDistance(transform.position, currentTarget.transform.position)
                : Mathf.Infinity;

            if (!attacking)
            {
                if (currentTarget != null && distToTarget <= attackRange)
                {
                    // In range: wind up, pause movement, and preview reach
                    agent.isStopped = true;
                    attacking = true;
                    attackTimer = attackWindup;

                    SpawnAttackPulse();

                    // Face current target for clarity
                    Vector3 look = currentTarget.transform.position - transform.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(look);
                }
                else
                {
                    // Not in range: continue to chase last known position
                    agent.isStopped = false;
                    agent.SetDestination(targetPos);
                }
            }
            else
            {
                // Windup: remain stationary until timer elapses
                agent.isStopped = true;
                attackTimer -= Time.deltaTime;

                if (attackTimer <= 0f)
                {
                    // Resolve damage only if still in range
                    if (currentTarget != null && distToTarget <= attackRange)
                    {
                        var hp = currentTarget.GetComponent<PlayerHealth>();
                        if (hp != null) hp.TakeDamage(attackDamage);
                    }

                    // Begin recovery; movement resumes after this frame
                    attacking = false;
                    attackTimer = attackRecovery;
                    agent.isStopped = false;
                }
            }

            // Soft leash back to patrol if straying beyond group radius
            if (group != null)
            {
                float distCenter = Vector3.Distance(transform.position, group.transform.position);
                if (distCenter > group.patrolRadius * 1.2f)
                {
                    agent.isStopped = false;
                    GoPatrol();
                }
            }
        }

        // ---------------- Patrol utilities ----------------

        private void SetNextPatrolPoint(bool nearCenter)
        {
            if (group == null) return;

            float r = nearCenter ? group.patrolRadius * 0.5f : group.patrolRadius;
            Vector2 v = Random.insideUnitCircle * r;
            Vector3 candidate = group.transform.position + new Vector3(v.x, 0f, v.y);

            // Snap to NavMesh or fall back to group center
            if (NavMesh.SamplePosition(candidate, out var hit, r, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(group.transform.position);
        }

        // ---------------- Perception ----------------

        private void ScanForPlayers()
        {
            var players = FindObjectsByType<PlayerProxy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            PlayerProxy best = null;
            float bestDist = float.MaxValue;

            foreach (var p in players)
            {
                Vector3 to = p.transform.position - transform.position;
                float dist = to.magnitude;
                if (dist > viewDistance) continue;

                float ang = Vector3.Angle(transform.forward, to);
                if (ang > viewAngle) continue;

                // Line-of-sight check: first hit must be the target’s proxy
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                                    to.normalized,
                                    out var hit,
                                    viewDistance,
                                    ~0,
                                    QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<PlayerProxy>() == p)
                    {
                        if (dist < bestDist) { best = p; bestDist = dist; }
                    }
                }
            }

            if (best != null)
            {
                targetPos = best.transform.position;  // Update last known
                currentTarget = best;

                // Move group hunt center only when there is visual confirmation
                if (group != null) group.SetHuntCenter(targetPos, group.huntDuration);

                if (state != State.Attack) GoAttack(targetPos);
            }
            else
            {
                // Lost visual while attacking: transition to Hunting around last known
                if (state == State.Attack) GoHunting(targetPos);
                currentTarget = null;
            }
        }

        // ---------------- Noise reactions ----------------

        private void OnNoise(NoiseEvent e)
        {
            // React to discrete sound if within planar hearing range; group center is not moved by sound alone
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = e.position; b.y = 0f;

            if (Vector3.Distance(a, b) <= hearingRadius)
                GoHunting(e.position);
        }

        private void OnNoisePulse(NoisePulse p)
        {
            // Trigger once per pulse ID when wavefront overlaps this hearing radius
            if (pulsesHandled.Contains(p.pulseId)) return;

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = p.position; b.y = 0f;
            float dist = Vector3.Distance(a, b);

            if (dist <= p.currentRadius + hearingRadius)
            {
                pulsesHandled.Add(p.pulseId);
                GoHunting(p.position);
            }
        }

        // ---------------- Hearing visual ----------------

        private void BuildHearingVisual()
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s.name = "Hearing_Vis";
            s.transform.SetParent(transform, worldPositionStays: false);
            s.transform.localRotation = Quaternion.identity;

            var col = s.GetComponent<Collider>();
            if (col) Destroy(col); // Visual-only

            var mr = s.GetComponent<MeshRenderer>();
            if (mr && hearingMaterial != null) mr.sharedMaterial = hearingMaterial;

            s.layer = LayerMask.NameToLayer("Ignore Raycast");
            hearingVis = s.transform;

            UpdateHearingVisual();
        }

        private void UpdateHearingVisual()
        {
            if (hearingVis == null) return;

            float diameter = Mathf.Max(0.1f, hearingRadius * 2f);
            hearingVis.position = transform.position + Vector3.up * hearingHeight;
            hearingVis.localScale = new Vector3(diameter, hearingHeight, diameter);
        }

        // ---------------- Attack visual ----------------

        private void SpawnAttackPulse()
        {
            if (attackPulseMaterial == null) return;

            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "AttackPulse";
            cyl.transform.position = transform.position + Vector3.up * 0.05f;

            var col = cyl.GetComponent<Collider>();
            if (col) Destroy(col); // Visual-only

            var mr = cyl.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = attackPulseMaterial;

            cyl.layer = LayerMask.NameToLayer("Ignore Raycast");

            var pulse = cyl.AddComponent<AttackPulse>();
            pulse.maxRadius = attackRange;   // Ring expands to attack reach
            pulse.lifetime = 0.25f;          // Quick indicator
            pulse.pulseMaterial = attackPulseMaterial;
        }

        // ---------------- External scale hook ----------------

        public void ApplyEnemyScale(float factor)
        {
            // Visual scale
            transform.localScale *= factor;

            // Perception/combat scaling
            viewDistance *= factor;
            hearingRadius *= factor;
            attackRange *= factor;

            // Sync associated visuals
            ApplyVisionToCone();
            UpdateHearingVisual();
        }
    }
}
