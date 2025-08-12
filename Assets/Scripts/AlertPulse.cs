using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlertPulse : MonoBehaviour
{
    public float maxRadius = 10f;     // final visual radius of the pulse
    public float lifetime = 2f;       // how long the pulse should live
    public Material pulseMaterial;    // transparent material to fade out

    float age;                        // seconds since spawn
    MeshRenderer mr;                  // renderer we tint/fade
    Color baseColor;                  // starting color (with alpha)

    void Start()
    {
        mr = GetComponent<MeshRenderer>();                 // sphere has one by default
        mr.material = new Material(pulseMaterial);         // instance so we can change alpha per pulse
        baseColor = mr.material.color;                     // remember initial color

        var col = GetComponent<Collider>();                // remove collider so it never blocks clicks
        if (col) Destroy(col);

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast"); // keep drag raycasts clean
        transform.localScale = Vector3.one * 0.01f;        // start tiny and expand
    }

    void Update()
    {
        age += Time.deltaTime;                              // advance timer
        var t = Mathf.Clamp01(age / lifetime);              // normalized 0..1 time

        // Expand from 0 to diameter of maxRadius over lifetime
        float diameter = maxRadius * 2f;
        transform.localScale = Vector3.one * Mathf.Lerp(0.01f, diameter, t);

        // Fade alpha to 0 over time (keeps color hue but makes it transparent)
        var c = baseColor;
        c.a = Mathf.Lerp(baseColor.a, 0f, t);
        mr.material.color = c;

        if (age >= lifetime) Destroy(gameObject);           // auto-cleanup
    }
}
