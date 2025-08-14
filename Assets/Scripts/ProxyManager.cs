using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProxyManager : MonoBehaviour
{
    public PlayerProxy playerProxyPrefab;
    public Material alertPulseMaterial;
    public KeyCode spawnKey = KeyCode.P;

    [Header("Grounding")]
    public LayerMask groundLayer;          // set this to your "Ground" layer in the Inspector
    public float defaultHeight = 1.0f;

    [Header("Spawn Spacing")]
    public float clearRadius = 0.6f;       // how far to keep new proxies from others
    public int clearTries = 10;            // attempts to nudge to a clear nearby spot

    int spawnedCount;

    void Update()
    {
        if (Input.GetKeyDown(spawnKey))
        {
            if (playerProxyPrefab == null)
            {
                Debug.LogError("ProxyManager: PlayerProxy prefab not assigned.");
                return;
            }
            if (alertPulseMaterial == null)
            {
                Debug.LogWarning("ProxyManager: AlertPulse material not assigned (visual may be missing).");
            }
            SpawnNearCamera();
        }
    }

    void SpawnNearCamera()
    {
        var cam = Camera.main ?? FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogError("ProxyManager: No camera found to place new proxy near.");
            return;
        }

        // Aim at what the camera sees
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 spawnPos;

        // Raycast only against the ground layer (prevents hitting previous proxies)
        if (Physics.Raycast(ray, out var hit, 1000f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            spawnPos = hit.point;
        }
        else
        {
            // Fallback: a few meters forward
            spawnPos = cam.transform.position + cam.transform.forward * 5f;
            spawnPos.y = defaultHeight;
        }

        // Instantiate first, then snap/clear so we can ignore self cleanly
        var proxy = Instantiate(playerProxyPrefab, spawnPos, Quaternion.identity);
        proxy.name = $"PlayerProxy_{++spawnedCount}";

        if (!proxy.TryGetComponent<RuntimeDraggable>(out _))
            proxy.gameObject.AddComponent<RuntimeDraggable>();

        proxy.alertPulseMaterial = alertPulseMaterial;

        var rb = proxy.GetComponent<Rigidbody>() ?? proxy.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Snap to ground ignoring this new proxy and other proxies
        var grounded = SnapToGroundWithOffset(proxy.gameObject, proxy.transform.position);

        // If that grounded point is already occupied, nudge to a nearby clear spot
        grounded = FindClearSpot(grounded, clearRadius, clearTries);

        proxy.transform.position = grounded;

        Debug.Log($"ProxyManager: Spawned {proxy.name} at {proxy.transform.position}");
    }

    // Choose the first ground hit that is NOT our own collider and NOT another PlayerProxy
    Vector3 SnapToGroundWithOffset(GameObject go, Vector3 approxPos, float rayUp = 5f, float rayDown = 100f)
    {
        float lift = 0.5f;
        if (go.TryGetComponent<Collider>(out var col)) lift = col.bounds.extents.y;

        Physics.SyncTransforms();

        Vector3 start = approxPos + Vector3.up * rayUp;
        float maxDist = rayUp + rayDown;

        // Only cast against ground (prevents stacking on proxies/enemies)
        var hits = Physics.RaycastAll(start, Vector3.down, maxDist, groundLayer, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            // ignore our own hierarchy just in case
            if (h.collider && h.collider.transform.IsChildOf(go.transform)) continue;

            // also ignore any collider that belongs to a PlayerProxy (belt and suspenders)
            if (h.collider && h.collider.GetComponentInParent<PlayerProxy>() != null) continue;

            return h.point + Vector3.up * lift;
        }

        // If we somehow didn’t find ground, just lift a bit
        return approxPos + Vector3.up * lift;
    }

    // Try a few radial offsets to find a spot not overlapping other players
    Vector3 FindClearSpot(Vector3 center, float radius, int tries)
    {
        // Ignore triggers; only consider actual colliders
        var overlaps = Physics.OverlapSphere(center, radius, ~LayerMask.GetMask("Ignore Raycast"), QueryTriggerInteraction.Ignore);
        foreach (var o in overlaps)
        {
            if (o.GetComponentInParent<PlayerProxy>() != null)
            {
                // Center is occupied: try to nudge around in a small spiral
                for (int i = 0; i < tries; i++)
                {
                    float angle = (360f / Mathf.Max(1, tries)) * i;
                    float dist = radius * 1.25f; // slight extra to ensure separation
                    Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * dist;

                    Vector3 candidate = center + offset;

                    // Check again at candidate
                    var hits = Physics.OverlapSphere(candidate, radius, ~LayerMask.GetMask("Ignore Raycast"), QueryTriggerInteraction.Ignore);
                    bool blocked = false;
                    foreach (var h in hits)
                    {
                        if (h.GetComponentInParent<PlayerProxy>() != null) { blocked = true; break; }
                    }
                    if (!blocked)
                    {
                        // Snap this candidate to ground too, then return
                        return SnapToGroundWithOffset(gameObject, candidate);
                    }
                }
                // If all tries failed, break; we will return the original center
                break;
            }
        }
        return center;
    }
}