using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlertPulse : MonoBehaviour
{
    public float maxRadius = 10f;
    public float lifetime = 2f;
    public Material pulseMaterial;

    float age;
    MeshRenderer mr;
    Color baseColor;
    int pulseId;
    static int nextId = 1;

    void Start()
    {
        mr = GetComponent<MeshRenderer>();
        mr.material = new Material(pulseMaterial);
        baseColor = mr.material.color;

        var col = GetComponent<Collider>();
        if (col) Destroy(col);

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        transform.localScale = Vector3.one * 0.01f;

        pulseId = nextId++; // unique id for this pulse
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);

        float currentRadius = Mathf.Lerp(0f, maxRadius, t);
        float diameter = Mathf.Max(0.01f, currentRadius * 2f);
        transform.localScale = Vector3.one * diameter;

        var c = baseColor;
        c.a = Mathf.Lerp(baseColor.a, 0f, t);
        mr.material.color = c;

        // MUST be present: broadcast the expanding wavefront
        NoiseSystem.EmitPulse(new NoisePulse(pulseId, transform.position, currentRadius, 0));

        if (age >= lifetime) Destroy(gameObject);
    }

}
