using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Misadventures.AI
{
    [RequireComponent(typeof(Collider))]
    public class SpawnerAnchor : MonoBehaviour
    {
        // Optional material applied to a runtime-created visual indicator
        public Material visualMaterial;

        // Cylinder radius in world units (X/Z scale)
        public float radius = 0.4f;

        // Cylinder half-height in world units (Y scale in Unity primitives)
        public float height = 0.15f;

        private void Reset()
        {
            // Set a consistent GameObject name when the component is added or reset
            gameObject.name = "SpawnerAnchor";
        }

        private void Start()
        {
            // Ensure a child cylinder exists to visualize the anchor in the scene
            if (GetComponent<MeshRenderer>() == null)
            {
                // Create a primitive cylinder as a visual-only child
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "Spawner_Vis";
                cyl.transform.SetParent(transform);
                cyl.transform.localPosition = Vector3.zero;
                cyl.transform.localRotation = Quaternion.identity;

                // Place on an ignore layer to avoid selection/raycast interference
                cyl.layer = LayerMask.NameToLayer("Ignore Raycast");

                // Remove the collider from the visual so it does not affect physics
                var col = cyl.GetComponent<Collider>();
                if (col) Destroy(col);

                // Assign visual material if provided
                var mr = cyl.GetComponent<MeshRenderer>();
                if (mr && visualMaterial != null) mr.sharedMaterial = visualMaterial;

                // Scale cylinder: Unity cylinder height is scaled by local Y
                cyl.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
            }

            // Ensure the anchor can be manipulated at runtime via a draggable helper
            if (!TryGetComponent<RuntimeDraggable>(out _))
                gameObject.AddComponent<RuntimeDraggable>();
        }
    }
}
