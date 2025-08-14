using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Misadventures.AI
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Collider))]
    public class EnemyAI : MonoBehaviour
    {
        // ------------ Inspector ----------------

        [Header("Group")]
        public EnemyGroup group;

        [Header("Perception")]
        public float viewAngle = 70f;           // half-angle in degrees
        public float viewDistance = 12f;        // max view range
        public float hearingRadius = 14f;       // sphere hearing radius (planar)
        public LayerMask visionObstacles;       // currently unused; kept for future occlusion tweaks

        [Header("Movement and Combat")]
        public float attackRange = 1.8f;        // how close we must be to strike
        public float patrolPointTolerance = 0.6f;
        public float patrolWait = 1.0f;
        public float huntSpeedMultiplier = 1.3f;

        [Header("Visuals")]
        public VisionCone visionCone;
        public Color selectedEmissionColor = Color.white;
        public float selectedEmissionStrength = 2.0f;

        [Header("Hearing Visual")]
        public Material hearingMaterial;
        public float hearingHeight = 0.05f;     // y-offset for the flat cylinder

        [Header("Attack")]
        public int attackDamage = 10;
        public float attackWindup = 0.35f;      // time standing still before applying damage
        public float attackRecovery = 0.25f;    // brief lockout after a swing
        public Material attackPulseMaterial;    // quick expanding ring to show attack area

        [Header("Hunting")]
        public float huntingTimeout = 6f;       // after this, go back to patrol if no visual

        // ------------ Runtime ----------------

        enum State { Patrol, Hunting, Attack }

        State state;
        NavMeshAgent agent;
        Renderer rend;
        MaterialPropertyBlock mpb;
        int emissionColorId;

        static EnemyAI active;                  // selection highlight

        PlayerProxy currentTarget;              // who we see (if any)
        Vector3 targetPos;                      // last known target pos or current goal
        float baseSpeed;
        float stateTimer;                       // reused for patrol wait
        float huntingTimer;                     // counts down while hunting

        // Attack loop
        bool attacking;                         // true during windup; false during recovery/idle
        float attackTimer;                      // counts windup and recovery

        // Hearing viz
        Transform hearingVis;

        // Prevent multiple triggers per pulse
        readonly HashSet<int> pulsesHandled = new HashSet<int>();

        // ------------ Unity ----------------

        void OnEnable()
        {
            NoiseSystem.OnNoiseEmitted += OnNoise;     // legacy one-shot
            NoiseSystem.OnNoisePulse += OnNoisePulse; // expanding wave overlap
        }

        void OnDisable()
        {
            NoiseSystem.OnNoiseEmitted -= OnNoise;
            NoiseSystem.OnNoisePulse -= OnNoisePulse;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            rend = GetComponentInChildren<Renderer>();
            baseSpeed = agent.speed;

            // Allow runtime dragging of enemies in your sandbox
            if (!TryGetComponent<RuntimeDraggable>(out _))
                gameObject.AddComponent<RuntimeDraggable>();

            if (visionCone == null)
                visionCone = GetComponentInChildren<VisionCone>();

            mpb = new MaterialPropertyBlock();
            emissionColorId = Shader.PropertyToID("_EmissionColor");

            // Start with a sane default; states will override per-need
            agent.stoppingDistance = 0f;
            agent.autoBraking = true; // keeps arrivals crisp; can tweak if needed
        }

        void Start()
        {
            if (group != null) group.Register(this);
            GoPatrol();                 // enter initial state
            ApplyVisionToCone();        // sync viz with values
            BuildHearingVisual();       // draw the hearing ring
        }

        void Update()
        {
            // Selection highlight
            SetSelected(this == active);

            // Tick current state
            switch (state)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Hunting: TickHunting(); break;
                case State.Attack: TickAttack(); break;
            }

            // Continuous perception sweep
            ScanForPlayers();

            // Keep hearing ring scaled/positioned
            UpdateHearingVisual();
        }

        void OnMouseDown()
        {
            if (active != null && active != this) active.SetSelected(false);
            active = this;
        }

        // ------------ Helpers ----------------

        float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void SetSelected(bool enabled)
        {
            if (rend == null) return;
            rend.GetPropertyBlock(mpb);

            // Write emission via property block to avoid material instancing overhead
            var c = enabled ? selectedEmissionColor * selectedEmissionStrength : Color.black;
            if (enabled) rend.material.EnableKeyword("_EMISSION");
            else rend.material.DisableKeyword("_EMISSION");

            mpb.SetColor(emissionColorId, c);
            rend.SetPropertyBlock(mpb);
        }

        public void ApplyVisionForExternalChange() { ApplyVisionToCone(); }

        void ApplyVisionToCone()
        {
            if (!visionCone) return;
            visionCone.angle = viewAngle;
            visionCone.distance = viewDistance;
        }

        // Consider arrived if within tolerance OR within stopping distance
        bool ArrivedAtPoint()
        {
            float arrive = Mathf.Max(patrolPointTolerance, agent.stoppingDistance + 0.05f);
            return !agent.pathPending && agent.remainingDistance <= arrive;
        }

        // ------------ States ----------------

        void GoPatrol()
        {
            state = State.Patrol;
            stateTimer = patrolWait;
            agent.speed = baseSpeed;
            agent.stoppingDistance = 0f;   // must reach waypoints fully
            agent.isStopped = false;
            SetNextPatrolPoint(false);
        }

        void TickPatrol()
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

        void GoHunting(Vector3 center)
        {
            state = State.Hunting;
            huntingTimer = huntingTimeout;
            agent.speed = baseSpeed * huntSpeedMultiplier;
            agent.stoppingDistance = 0f;   // we want to reach search points fully
            agent.isStopped = false;

            targetPos = center;
            agent.SetDestination(targetPos);
        }

        void TickHunting()
        {
            huntingTimer -= Time.deltaTime;
            if (huntingTimer <= 0f)
            {
                GoPatrol();
                return;
            }

            if (ArrivedAtPoint())
            {
                // Keep pacing around hunt center
                SetNextPatrolPoint(true);
            }
        }

        void GoAttack(Vector3 pos)
        {
            state = State.Attack;
            agent.speed = baseSpeed * huntSpeedMultiplier;

            // Stop near target so we can wind-up swings in place
            agent.stoppingDistance = Mathf.Max(0.01f, attackRange * 0.9f);
            agent.isStopped = false;

            targetPos = pos;
            agent.SetDestination(targetPos);

            // entering Attack does not immediately start a swing;
            // TickAttack handles chase->stop->windup->hit->recovery
            attacking = false;
            attackTimer = 0f;
        }

        void TickAttack()
        {
            float distToTarget = (currentTarget != null)
                ? PlanarDistance(transform.position, currentTarget.transform.position)
                : Mathf.Infinity;

            if (!attacking)
            {
                if (currentTarget != null && distToTarget <= attackRange)
                {
                    // In range: stop and start wind-up
                    agent.isStopped = true;
                    attacking = true;
                    attackTimer = attackWindup;

                    SpawnAttackPulse(); // quick visual of reach

                    // Face the target for readability
                    Vector3 look = currentTarget.transform.position - transform.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.001f)
                        transform.rotation = Quaternion.LookRotation(look);
                }
                else
                {
                    // Not yet in range: keep closing toward last known position
                    agent.isStopped = false;
                    agent.SetDestination(targetPos);
                }
            }
            else
            {
                // During wind-up we do not move
                agent.isStopped = true;
                attackTimer -= Time.deltaTime;

                if (attackTimer <= 0f)
                {
                    // Resolve the hit only if still in range
                    if (currentTarget != null && distToTarget <= attackRange)
                    {
                        var hp = currentTarget.GetComponent<PlayerHealth>();
                        if (hp != null) hp.TakeDamage(attackDamage);
                    }

                    // Start recovery; after this frame we allow movement again
                    attacking = false;
                    attackTimer = attackRecovery;
                    agent.isStopped = false;
                }
            }

            // Soft leash back to patrol if we stray too far from group
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

        // ------------ Patrol Utilities ----------------

        void SetNextPatrolPoint(bool nearCenter)
        {
            if (group == null) return;

            float r = nearCenter ? group.patrolRadius * 0.5f : group.patrolRadius;
            Vector2 v = Random.insideUnitCircle * r;
            Vector3 candidate = group.transform.position + new Vector3(v.x, 0f, v.y);

            // Snap to NavMesh
            if (NavMesh.SamplePosition(candidate, out var hit, r, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(group.transform.position);
        }

        // ------------ Perception ----------------

        void ScanForPlayers()
        {
            var players = FindObjectsByType<PlayerProxy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            PlayerProxy best = null;
            float bestDist = float.MaxValue;

            foreach (var p in players)
            {
                // Cheap early outs
                Vector3 to = p.transform.position - transform.position;
                float dist = to.magnitude;
                if (dist > viewDistance) continue;

                // FOV cone check
                float ang = Vector3.Angle(transform.forward, to);
                if (ang > viewAngle) continue;

                // LoS check; we allow any collider but want to know if the first hit is the player
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
                targetPos = best.transform.position;  // update last known
                currentTarget = best;

                // Move the group's hunt center only on visual contact
                if (group != null) group.SetHuntCenter(targetPos, group.huntDuration);

                // Enter Attack state once; TickAttack manages movement/strikes
                if (state != State.Attack) GoAttack(targetPos);
            }
            else
            {
                // Lost visual while attacking -> switch to hunting last known
                if (state == State.Attack) GoHunting(targetPos);
                currentTarget = null;
            }
        }

        // Called for discrete "sound events"
        void OnNoise(NoiseEvent e)
        {
            // Individual enemies react; group area does not move on sound alone
            // Use planar distance so height does not matter much for hearing
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = e.position; b.y = 0f;
            if (Vector3.Distance(a, b) <= hearingRadius)
            {
                GoHunting(e.position);
            }
        }

        // Called repeatedly by the expanding pulse so we can trigger when the wavefront
        // overlaps the hearing sphere (dist <= pulseRadius + hearingRadius)
        void OnNoisePulse(NoisePulse p)
        {
            if (pulsesHandled.Contains(p.pulseId)) return;

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = p.position; b.y = 0f;
            float dist = Vector3.Distance(a, b);

            if (dist <= p.currentRadius + hearingRadius)
            {
                pulsesHandled.Add(p.pulseId);
                GoHunting(p.position); // check the noise location
            }
        }

        // ------------ Hearing Visual ----------------

        void BuildHearingVisual()
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s.name = "Hearing_Vis";
            s.transform.SetParent(transform, worldPositionStays: false);
            s.transform.localRotation = Quaternion.identity;

            var col = s.GetComponent<Collider>();
            if (col) Destroy(col);                       // purely visual

            var mr = s.GetComponent<MeshRenderer>();
            if (mr && hearingMaterial != null) mr.sharedMaterial = hearingMaterial;

            s.layer = LayerMask.NameToLayer("Ignore Raycast");
            hearingVis = s.transform;

            UpdateHearingVisual();
        }

        void UpdateHearingVisual()
        {
            if (hearingVis == null) return;
            float diameter = Mathf.Max(0.1f, hearingRadius * 2f);
            hearingVis.position = transform.position + Vector3.up * hearingHeight;
            hearingVis.localScale = new Vector3(diameter, hearingHeight, diameter);
        }

        // ------------ Attack Visual ----------------

        void SpawnAttackPulse()
        {
            if (attackPulseMaterial == null) return;

            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "AttackPulse";
            cyl.transform.position = transform.position + Vector3.up * 0.05f;

            var col = cyl.GetComponent<Collider>();
            if (col) Destroy(col);

            var mr = cyl.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = attackPulseMaterial;

            cyl.layer = LayerMask.NameToLayer("Ignore Raycast");

            var pulse = cyl.AddComponent<AttackPulse>();
            pulse.maxRadius = attackRange;  // ring grows to attack reach
            pulse.lifetime = 0.25f;        // quick pop
            pulse.pulseMaterial = attackPulseMaterial;
        }

        // ------------ Scale Enemy ----------------

        public void ApplyEnemyScale(float factor)
        {
            // scale visuals
            transform.localScale *= factor;

            // scale perception/combat numbers
            viewDistance *= factor;
            hearingRadius *= factor;
            attackRange *= factor;

            // push to visuals immediately
            ApplyVisionToCone();
            UpdateHearingVisual();
        }

    }
}
