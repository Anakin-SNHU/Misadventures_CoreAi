using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerProxy : MonoBehaviour
{
    public KeyCode alertKey = KeyCode.F;
    public float alertRadius = 12f;
    public float alertLifetime = 2f;
    public Material alertPulseMaterial;
    public Material outlineMaterial;     // NEW — assigned in inspector

    public static PlayerProxy Active;
    public int id;

    static int nextId = 1;

    Renderer rend;                        // cache the renderer
    Material originalMaterial;            // store the starting mat

    void Awake()
    {
        if (id == 0) id = nextId++;
        rend = GetComponentInChildren<Renderer>(); // works if mesh is child of capsule
        if (rend != null)
            originalMaterial = rend.material;
    }

    void OnMouseDown()
    {
        // Remove outline from previous active
        if (Active != null && Active != this)
            Active.SetOutline(false);

        Active = this;
        SetOutline(true);
    }

    void Update()
    {
        if (this == Active && Input.GetKeyDown(alertKey))
        {
            EmitAlert();
        }
    }

    void EmitAlert()
    {
        NoiseSystem.Emit(new NoiseEvent(transform.position, alertRadius, id));

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"AlertPulse_P{id}";
        sphere.transform.position = transform.position;

        var pulse = sphere.AddComponent<AlertPulse>();
        pulse.maxRadius = alertRadius;
        pulse.lifetime = alertLifetime;
        pulse.pulseMaterial = alertPulseMaterial;
    }

    void SetOutline(bool enabled)
    {
        if (rend == null || outlineMaterial == null) return;

        if (enabled)
            rend.material = outlineMaterial;
        else
            rend.material = originalMaterial;
    }
}


