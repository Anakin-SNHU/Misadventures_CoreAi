using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

// Simple event payload other systems (AI) can listen to later
public struct NoiseEvent
{
    public Vector3 position; // where the sound originated
    public float radius;     // how far it should be "heard"
    public int sourceId;     // which player proxy made it
    public float time;       // when it happened

    public NoiseEvent(Vector3 pos, float r, int id)
    {
        position = pos;
        radius = r;
        sourceId = id;
        time = Time.time;
    }
}

public static class NoiseSystem
{
    // Subscribe: NoiseSystem.OnNoiseEmitted += (e) => { ... };
    public static event Action<NoiseEvent> OnNoiseEmitted;

    public static void Emit(NoiseEvent e)
    {
        // Null-propagation ensures no exception when no listeners are present
        OnNoiseEmitted?.Invoke(e);
    }
}

