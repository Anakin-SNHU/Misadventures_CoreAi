using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VisionCone : MonoBehaviour
{
    // Half-angle of the field of view in degrees (total FOV = angle * 2)
    public float angle = 60f;

    // Radius/length of the cone on the XZ plane
    public float distance = 10f;

    // Number of arc subdivisions; higher values create a smoother edge
    public int segments = 24;

    // Runtime-generated mesh instance assigned to the MeshFilter
    private Mesh mesh;

    private void Awake()
    {
        // Create a new mesh and assign it to this object's MeshFilter
        mesh = new Mesh { name = "VisionConeMesh" };
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    private void LateUpdate()
    {
        // Rebuild the cone each frame to reflect any runtime parameter changes
        BuildCone();
    }

    // Generates a flat, triangle-fan mesh oriented on the XZ plane in local space
    private void BuildCone()
    {
        // Vertex count includes the center plus one vertex per segment along the arc
        int vertexCount = segments + 2;
        Vector3[] vertices = new Vector3[vertexCount];

        // One triangle per segment, three indices each
        int[] triangles = new int[segments * 3];

        // Center of the fan at local origin
        vertices[0] = Vector3.zero;

        // Convert half-angle to radians for trigonometric functions
        float halfRadians = angle * Mathf.Deg2Rad;

        // Populate arc vertices from -half to +half around local forward (Z+)
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;             // Normalized position along the arc [0..1]
            float a = -halfRadians + t * (2f * halfRadians);
            float x = Mathf.Sin(a) * distance;         // Offset on X axis
            float z = Mathf.Cos(a) * distance;         // Offset on Z axis
            vertices[i + 1] = new Vector3(x, 0f, z);
        }

        // Build triangle indices for the fan (center -> edge i -> edge i+1)
        int triIndex = 0;
        for (int i = 0; i < segments; i++)
        {
            triangles[triIndex++] = 0;
            triangles[triIndex++] = i + 1;
            triangles[triIndex++] = i + 2;
        }

        // Commit mesh data and recalculate normals for correct lighting
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}
