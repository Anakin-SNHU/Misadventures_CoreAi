using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Misadventures.AI
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Collider))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("Group")]
        public EnemyGroup group;

        [Header("Perception")]
        public float viewAngle = 70f;
        public float viewDistance = 12f;
        public float hearingRadius = 14f;
        public LayerMask visionObstacles;

        [Header("Movement and Combat")]
        public float attackRange = 1.8f;
        public float patrolPointTolerance = 0.6f;
        public float patrolWait = 1.0f;
        public float huntSpeedMultiplier = 1.3f;

        [Header("Visuals")]
        public VisionCone visionCone;
        public Color selectedEmissionColor = Color.white;
        public float selectedEmissionStrength = 2.0f;

        [Header("Hearing Visual")]
        public Material hearingMaterial;
        public float hearingHeight = 0.05f;

        private enum State { Patrol, Hunting, Attack }

        private State state;
        private NavMeshAgent agent;
        private Renderer rend;
        private MaterialPropertyBlock mpb;
        private int emissionColorId;
        private static EnemyAI active;

        private Vector3 targetPos;
        private float stateTimer;
        private float baseSpeed;
        private Transform hearingVis;

        private HashSet<int> pulsesHandled = new HashSet<int>();

        void OnEnable()
        {
            NoiseSystem.OnNoiseEmitted += OnNoise;   // legacy one-shot (kept; optional)
            NoiseSystem.OnNoisePulse += OnNoisePulse;
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

            if (!TryGetComponent<RuntimeDraggable>(out _))
                gameObject.AddComponent<RuntimeDraggable>();

            if (visionCone == null) visionCone = GetComponentInChildren<VisionCone>();

            mpb = new MaterialPropertyBlock();
            emissionColorId = Shader.PropertyToID("_EmissionColor");
        }

        void Start()
        {
            if (group != null) group.Register(this);
            GoPatrol();
            ApplyVisionToCone();
            BuildHearingVisual();
        }

        void Update()
        {
            if (this == active) SetSelected(true); else SetSelected(false);

            switch (state)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Hunting: TickHunting(); break;
                case State.Attack: TickAttack(); break;
            }

            ScanForPlayers();
            UpdateHearingVisual();
        }

        void OnMouseDown()
        {
            if (active != null && active != this) active.SetSelected(false);
            active = this;
        }

        void SetSelected(bool enabled)
        {
            if (rend == null) return;
            rend.GetPropertyBlock(mpb);
            Color c = enabled ? selectedEmissionColor * selectedEmissionStrength : Color.black;
            if (enabled) rend.material.EnableKeyword("_EMISSION");
            else rend.material.DisableKeyword("_EMISSION");
            mpb.SetColor(emissionColorId, c);
            rend.SetPropertyBlock(mpb);
        }

        void ApplyVisionToCone()
        {
            if (visionCone == null) return;
            visionCone.angle = viewAngle;
            visionCone.distance = viewDistance;
        }

        // STATES -------------------------------------------------

        void GoPatrol()
        {
            state = State.Patrol;
            stateTimer = patrolWait;
            agent.speed = baseSpeed;
            SetNextPatrolPoint(false);
        }

        void TickPatrol()
        {
            if (!agent.pathPending && agent.remainingDistance <= patrolPointTolerance)
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
            agent.speed = baseSpeed * huntSpeedMultiplier;
            targetPos = center;
            agent.SetDestination(targetPos);
        }

        void TickHunting()
        {
            if (!agent.pathPending && agent.remainingDistance <= patrolPointTolerance)
            {
                SetNextPatrolPoint(true);
            }
        }

        void GoAttack(Vector3 pos)
        {
            state = State.Attack;
            targetPos = pos;
            agent.speed = baseSpeed * huntSpeedMultiplier;
            agent.SetDestination(targetPos);
        }

        void TickAttack()
        {
            agent.SetDestination(targetPos);

            if (group != null)
            {
                float distCenter = Vector3.Distance(transform.position, group.transform.position);
                if (distCenter > group.patrolRadius * 1.2f)
                {
                    GoPatrol();
                    return;
                }
            }
        }

        // PATROL UTILS -------------------------------------------

        void SetNextPatrolPoint(bool nearCenter)
        {
            if (group == null) return;

            float r = nearCenter ? group.patrolRadius * 0.5f : group.patrolRadius;
            Vector2 v = Random.insideUnitCircle * r;
            Vector3 candidate = group.transform.position + new Vector3(v.x, 0f, v.y);

            if (NavMesh.SamplePosition(candidate, out var hit, r, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(group.transform.position);
        }

        // SIGHT / HEARING ----------------------------------------

        void ScanForPlayers()
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

                if (Physics.Raycast(transform.position + Vector3.up * 0.5f, to.normalized, out var hit, viewDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<PlayerProxy>() == p)
                    {
                        if (dist < bestDist) { best = p; bestDist = dist; }
                    }
                }
            }

            if (best != null)
            {
                targetPos = best.transform.position;

                // Only move group on visual contact
                if (group != null) group.SetHuntCenter(targetPos, group.huntDuration);

                if (bestDist <= attackRange) GoAttack(targetPos);
                else if (state != State.Attack) GoAttack(targetPos);
            }
            else
            {
                if (state == State.Attack) GoHunting(targetPos);
            }
        }

        void OnNoise(NoiseEvent e)
        {
            // Per-enemy check; do not move group on noise
            if (Vector3.Distance(e.position, transform.position) <= hearingRadius)
            {
                GoHunting(e.position);
            }
        }

        void OnNoisePulse(NoisePulse p)
        {
            // prevent multiple triggers from the same pulse
            if (pulsesHandled.Contains(p.pulseId)) return;

            // Use planar distance (XZ) so height does not matter for hearing
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = p.position; b.y = 0f;
            float dist = Vector3.Distance(a, b);

            // Trigger when the expanding pulse overlaps this enemy's hearing sphere
            if (dist <= p.currentRadius + hearingRadius)
            {
                pulsesHandled.Add(p.pulseId);
                GoHunting(p.position); // go check the noise location
                                       // Intentionally do not move the group on noise.
            }
        }



        // HEARING VIS --------------------------------------------

        void BuildHearingVisual()
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            s.name = "Hearing_Vis";
            s.transform.SetParent(transform);
            s.transform.localRotation = Quaternion.identity;

            var col = s.GetComponent<Collider>();
            if (col) Destroy(col);

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
    }
}
