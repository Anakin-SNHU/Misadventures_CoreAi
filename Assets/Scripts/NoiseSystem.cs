using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public struct NoiseEvent
{
    public Vector3 position;
    public float radius;     // max radius for legacy users (kept)
    public int sourceId;
    public float time;

    public NoiseEvent(Vector3 pos, float r, int id)
    {
        position = pos;
        radius = r;
        sourceId = id;
        time = Time.time;
    }
}

// New: live pulse tick carrying expanding radius
public struct NoisePulse
{
    public int pulseId;
    public Vector3 position;
    public float currentRadius;
    public int sourceId;
    public float time;

    public NoisePulse(int id, Vector3 pos, float r, int src)
    {
        pulseId = id;
        position = pos;
        currentRadius = r;
        sourceId = src;
        time = Time.time;
    }
}

public static class NoiseSystem
{
    public static event Action<NoiseEvent> OnNoiseEmitted;

    // New: fired every frame by AlertPulse as the sphere expands
    public static event Action<NoisePulse> OnNoisePulse;

    public static void Emit(NoiseEvent e) => OnNoiseEmitted?.Invoke(e);

    public static void EmitPulse(NoisePulse p) => OnNoisePulse?.Invoke(p);
}
