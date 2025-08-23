using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual-only expanding ring used to indicate an enemy attack.
/// Scales from near-zero to <see cref="maxRadius"/> over <see cref="lifetime"/> and then self-destructs.
/// No physics or gameplay impact.
/// </summary>
public class AttackPulse : MonoBehaviour
{
    public float maxRadius = 2f;          // Maximum radius reached at the end of the pulse (world units)
    public float lifetime = 0.25f;        // Duration of the pulse from spawn to fade-out
    public Material pulseMaterial;        // Material used for the ring; instanced at runtime to avoid shared edits

    private float age;                    // Elapsed time since spawn
    private MeshRenderer mr;              // Cached renderer for color updates
    private Color baseColor;              // Starting color (alpha drives fade-out)

    private void Start()
    {
        // Cache renderer and create a unique material instance
        mr = GetComponent<MeshRenderer>();
        mr.material = new Material(pulseMaterial);
        baseColor = mr.material.color;

        // Remove any collider to ensure visual-only behavior
        var col = GetComponent<Collider>();
        if (col) Destroy(col);

        // Ignore raycasts and selection; render as a thin initial ring
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        transform.localScale = Vector3.one * 0.01f;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime); // Normalized progress [0..1]

        // Scale outward from zero to the target diameter (radius * 2)
        float diameter = Mathf.Lerp(0f, maxRadius * 2f, t);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, diameter);

        // Fade alpha to zero over the pulse lifetime
        var c = baseColor;
        c.a = Mathf.Lerp(baseColor.a, 0f, t);
        mr.material.color = c;

        // Destroy once the pulse has completed
        if (age >= lifetime) Destroy(gameObject);
    }
}
