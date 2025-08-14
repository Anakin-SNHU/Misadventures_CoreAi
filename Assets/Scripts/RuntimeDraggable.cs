using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Drag at runtime while respecting either NavMesh or Physics obstacles.
/// - Set `mode = NavMesh` for capsules that should move like agents (PlayerProxy).
/// - Set `mode = PhysicsCapsule` if you don't have a NavMesh and want collision-safe sliding.
/// - Assign `dragMask` to the layers you consider "solid ground/geometry".
[DisallowMultipleComponent]
public class RuntimeDraggable : MonoBehaviour
{
    public enum ConstraintMode { None, NavMesh, PhysicsCapsule }

    [Header("General")]
    public ConstraintMode mode = ConstraintMode.NavMesh;
    public LayerMask dragMask = ~0;          // everything except IgnoreRaycast by default
    public float maxRayDistance = 200f;

    [Header("NavMesh")]
    public float navSampleMaxDistance = 1.5f;    // how far we allow off-mesh snap
    public int navAreaMask = NavMesh.AllAreas;

    [Header("Physics Capsule")]
    public float capsuleSkin = 0.02f;            // small separation from walls
    public float maxSlidePerFrame = 5f;          // clamp teleports per frame

    [Header("Smoothing")]
    public float planarSmoothTime = 0.08f;  // XZ smoothing (seconds)
    public float ySmoothTime = 0.05f;  // Y smoothing (seconds)
    public float maxSmoothSpeed = 50f;    // clamp SmoothDamp speed

    Vector3 planarVel;   // XZ velocity cache for SmoothDamp
    float yVel;        // Y velocity cache for SmoothDamp


    Camera cam;
    bool dragging;
    Vector3 grabOffsetWorld;     // offset from hit point to object origin (world)
    float yLift;                 // half height to rest on surfaces (from collider bounds)
    Rigidbody rb;

    void Awake()
    {
        cam = Camera.main ?? FindAnyObjectByType<Camera>();
        rb  = GetComponent<Rigidbody>();

        // compute lift from any collider so the object rests on surfaces
        if (TryGetComponent<Collider>(out var col))
            yLift = col.bounds.extents.y;
        else
            yLift = 0.5f;

        // kinematic rigidbody is fine; we manually prevent penetration
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnMouseDown()
    {
        if (cam == null) return;

        // Ray to find initial grab point on geometry
        if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, maxRayDistance, dragMask, QueryTriggerInteraction.Ignore))
        {
            dragging = true;

            // store world offset so the object doesn't "snap" mid-drag
            grabOffsetWorld = transform.position - hit.point;
        }
        else
        {
            // fall back: no valid hit; still allow dragging but without offset
            dragging = true;
            grabOffsetWorld = Vector3.zero;
        }
    }

    void OnMouseUp()
    {
        dragging = false;

        // Final settle on ground/navmesh so it never sits sunken
        var p = (rb != null && rb.isKinematic) ? rb.position : transform.position;
        p = SnapToSurface(p);
        if (rb != null && rb.isKinematic) rb.MovePosition(p);
        else transform.position = p;
    }


    void Update()
    {
        if (!dragging || cam == null) return;

        // 1) Get desired world point from cursor ray against dragMask surfaces
        if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, maxRayDistance, dragMask, QueryTriggerInteraction.Ignore))
            return;

        Vector3 desired = hit.point + grabOffsetWorld;

        // Keep bottom resting on the hit surface (planar objects)
        desired.y = hit.point.y + yLift;

        // 2) Constrain by mode
        switch (mode)
        {
            case ConstraintMode.NavMesh:
                desired = ConstrainByNavMesh(transform.position, desired);
                break;

            case ConstraintMode.PhysicsCapsule:
                desired = ConstrainByCapsule(transform.position, desired);
                break;

            case ConstraintMode.None:
            default:
                // no constraints; still snap to surface
                break;
        }

        // 3) Apply move (kinematic move or transform set)
        if (rb != null && rb.isKinematic)
        {
            // MovePosition for smoother interpolation; still kinematic so no forces
            desired = SnapToSurface(desired);
            rb.MovePosition(desired);
        }
        else
        {
            transform.position = desired;
        }
    }

    // ---------- NavMesh constraint ----------

    Vector3 ConstrainByNavMesh(Vector3 from, Vector3 to)
    {
        // Snap the target to the nearest NavMesh within a small distance
        if (NavMesh.SamplePosition(to, out var hit, navSampleMaxDistance, navAreaMask))
        {
            Vector3 onMeshTarget = hit.position;

            // If a wall is between from -> target, NavMesh.Raycast returns true and reports hit
            if (NavMesh.Raycast(from, onMeshTarget, out var hitInfo, navAreaMask))
            {
                // Stop just before the wall
                return hitInfo.position;
            }
            return onMeshTarget;
        }

        // If no mesh nearby, try to stay where we are by projecting from current position
        if (NavMesh.SamplePosition(from, out var stayHit, navSampleMaxDistance, navAreaMask))
            return stayHit.position;

        // As a last resort, clamp the step distance to avoid big teleports
        return Vector3.MoveTowards(from, to, maxSlidePerFrame * Time.deltaTime);
    }

    // ---------- Physics capsule constraint ----------

    Vector3 ConstrainByCapsule(Vector3 from, Vector3 to)
    {
        // Capsule dimensions from a CapsuleCollider if present; else approximate from bounds
        float radius;
        Vector3 top, bottom;
        GetCapsule(out top, out bottom, out radius);

        // Direction and distance we want to move
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.0001f) return from;

        dir /= dist;

        // Cast capsule along the path; if hit, stop just before obstacle
        if (Physics.CapsuleCast(top, bottom, radius, dir, out var hit, dist + capsuleSkin, dragMask, QueryTriggerInteraction.Ignore))
        {
            float travel = Mathf.Max(0f, hit.distance - capsuleSkin);
            travel = Mathf.Min(travel, maxSlidePerFrame * Time.deltaTime);
            return from + dir * travel;
        }

        // No hit: clamp how far we move this frame to keep it stable
        float step = Mathf.Min(dist, maxSlidePerFrame * Time.deltaTime);
        return from + dir * step;
    }

    void GetCapsule(out Vector3 top, out Vector3 bottom, out float radius)
    {
        Vector3 c = transform.position;
        if (TryGetComponent<CapsuleCollider>(out var cap))
        {
            float scale = transform.lossyScale.y;
            float height = Mathf.Max(cap.height * scale, cap.radius * 2f * scale);
            radius = cap.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            float half = height * 0.5f - radius;
            if (half < 0f) half = 0f;

            top    = c + Vector3.up * (half + radius);
            bottom = c - Vector3.up * (half - 0f); // bottom sphere center sits at radius above ground
        }
        else if (TryGetComponent<Collider>(out var any))
        {
            var b = any.bounds;
            radius = Mathf.Min(b.extents.x, b.extents.z);
            top    = new Vector3(c.x, b.max.y - radius, c.z);
            bottom = new Vector3(c.x, b.min.y + radius, c.z);
        }
        else
        {
            // fallback
            radius = 0.5f;
            top    = c + Vector3.up * 1.0f;
            bottom = c;
        }
    }

    float CurrentLift()
    {
        // Re-read bounds so scaling updates the lift automatically
        if (TryGetComponent<Collider>(out var c)) return c.bounds.extents.y;
        return 0.5f;
    }

    Vector3 SnapToSurface(Vector3 pos)
    {
        // Prefer NavMesh height if we’re using NavMesh (more reliable on slopes)
        if (mode == ConstraintMode.NavMesh)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var nHit, 2f, navAreaMask))
            {
                pos.y = nHit.position.y + CurrentLift();
                return pos;
            }
        }

        // Fallback: physics ray straight down to ground
        var start = pos + Vector3.up * 2f;
        if (Physics.Raycast(start, Vector3.down, out var hit, 10f, dragMask, QueryTriggerInteraction.Ignore))
        {
            pos.y = hit.point.y + CurrentLift();
        }
        return pos;
    }

}
