using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Visual-only pulse for enemy attacks. Expands to maxRadius over lifetime, then self-destroys.
public class AttackPulse : MonoBehaviour
{
    public float maxRadius = 2f;
    public float lifetime = 0.25f;     // quick flash
    public Material pulseMaterial;

    float age;
    MeshRenderer mr;
    Color baseColor;

    void Start()
    {
        mr = GetComponent<MeshRenderer>();
        mr.material = new Material(pulseMaterial);
        baseColor = mr.material.color;

        var col = GetComponent<Collider>();
        if (col) Destroy(col);

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        transform.localScale = Vector3.one * 0.01f;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);

        float diameter = Mathf.Lerp(0f, maxRadius * 2f, t);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, diameter);

        var c = baseColor;
        c.a = Mathf.Lerp(baseColor.a, 0f, t);
        mr.material.color = c;

        if (age >= lifetime) Destroy(gameObject);
    }
}

