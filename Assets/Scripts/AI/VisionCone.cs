using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach to a child under the enemy. It generates a flat cone mesh
// that faces the enemy's forward. Use a transparent material.

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VisionCone : MonoBehaviour
{
    public float angle = 60f;      // degrees (half-angle; total FOV is angle*2 if you prefer)
    public float distance = 10f;   // how far the cone extends
    public int segments = 24;      // smoothness

    Mesh mesh;

    void Awake()
    {
        mesh = new Mesh { name = "VisionConeMesh" };
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void LateUpdate()
    {
        // rebuild mesh each frame if values change at runtime
        BuildCone();
    }

    void BuildCone()
    {
        // fan of triangles: center at (0,0,0) in local space, arc on XZ plane
        int vCount = segments + 2;
        Vector3[] vtx = new Vector3[vCount];
        int[] tris = new int[segments * 3];

        vtx[0] = Vector3.zero; // center
        float half = angle * Mathf.Deg2Rad;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;                  // 0..1
            float a = -half + t * (2f * half);              // sweep from -half to +half
            vtx[i + 1] = new Vector3(Mathf.Sin(a) * distance, 0f, Mathf.Cos(a) * distance);
        }

        int ti = 0;
        for (int i = 0; i < segments; i++)
        {
            tris[ti++] = 0;
            tris[ti++] = i + 1;
            tris[ti++] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vtx;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
    }
}
