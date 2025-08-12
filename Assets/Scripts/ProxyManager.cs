using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProxyManager : MonoBehaviour
{
    public PlayerProxy playerProxyPrefab;
    public Material alertPulseMaterial;
    public KeyCode spawnKey = KeyCode.P;
    public float defaultHeight = 1.0f;

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
                Debug.LogWarning("ProxyManager: AlertPulse material not assigned (alerts will still fire, but visual may be missing).");
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

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 spawnPos;

        // Needs a collider (your Ground plane has one). If it doesn't hit, we fall back.
        if (Physics.Raycast(ray, out var hit, 1000f))
        {
            spawnPos = hit.point;
        }
        else
        {
            spawnPos = cam.transform.position + cam.transform.forward * 5f;
            spawnPos.y = defaultHeight;
        }

        var proxy = Instantiate(playerProxyPrefab, spawnPos, Quaternion.identity);
        proxy.name = $"PlayerProxy_{++spawnedCount}";

        if (!proxy.TryGetComponent<RuntimeDraggable>(out _))
            proxy.gameObject.AddComponent<RuntimeDraggable>();

        proxy.alertPulseMaterial = alertPulseMaterial;

        var rb = proxy.GetComponent<Rigidbody>() ?? proxy.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Debug.Log($"ProxyManager: Spawned {proxy.name} at {spawnPos}");
    }
}


