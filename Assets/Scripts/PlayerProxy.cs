using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerProxy : MonoBehaviour
{
    public KeyCode alertKey = KeyCode.F;
    public float baseAlertRadius = 12f;     // replaces alertRadius for clarity
    int noiseLevel = 2;                     // 1..3
    readonly float[] levelMul = { 0f, 0.5f, 1f, 2f }; // index by 1..3
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
        // pick level hotkeys
        if (this == Active)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { noiseLevel = 1; EmitAlert(); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { noiseLevel = 2; EmitAlert(); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { noiseLevel = 3; EmitAlert(); }

            // Keep F as "emit current level"
            if (Input.GetKeyDown(alertKey)) EmitAlert();
        }

    }

    void EmitAlert()
    {
        float mul = Mathf.Clamp(noiseLevel, 1, 3);
        float radius = baseAlertRadius * levelMul[(int)mul];

        // Broadcast gameplay event (legacy one-shot, optional)
        NoiseSystem.Emit(new NoiseEvent(transform.position, radius, id));

        // Visual pulse
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"AlertPulse_P{id}_L{noiseLevel}";
        sphere.transform.position = transform.position;

        var pulse = sphere.AddComponent<AlertPulse>();
        pulse.maxRadius = radius;
        pulse.lifetime = alertLifetime;
        pulse.pulseMaterial = alertPulseMaterial;

        Debug.Log($"PlayerProxy {id}: noise level {noiseLevel}, radius {radius}");
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


