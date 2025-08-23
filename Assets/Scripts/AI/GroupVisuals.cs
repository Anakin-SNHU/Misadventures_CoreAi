using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Misadventures.AI
{
    [RequireComponent(typeof(EnemyGroup))]
    public class GroupVisuals : MonoBehaviour
    {
        // Material used for the patrol area visualization
        public Material areaMaterial;

        // Material used for the spawn point visualization
        public Material spawnMaterial;

        // Height (Y scale) of the visualization cylinders
        public float areaHeight = 0.1f;

        // Base diameter for the spawn point visual when patrol radius is small
        public float spawnSize = 0.4f;

        // External read-only access to the area MeshRenderer
        public MeshRenderer AreaRenderer { get { return areaMr; } }

        // Cached references
        private EnemyGroup group;      // Source of patrol radius and spawn point data
        private Transform areaVis;     // Transform of the patrol area visual
        private Transform spawnVis;    // Transform of the spawn point visual
        private MeshRenderer areaMr;   // MeshRenderer for the patrol area visual

        private void Awake()
        {
            // Cache required component
            group = GetComponent<EnemyGroup>();

            // Create patrol area cylinder as a visual-only child
            var area = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            area.name = "PatrolArea_Vis";
            area.transform.SetParent(transform);
            area.transform.localPosition = Vector3.zero;
            area.transform.localRotation = Quaternion.identity;
            area.layer = LayerMask.NameToLayer("Ignore Raycast");

            // Remove collider to prevent physics/raycast interaction
            var colA = area.GetComponent<Collider>();
            if (colA) Destroy(colA);

            // Assign material if provided
            areaMr = area.GetComponent<MeshRenderer>();
            if (areaMr && areaMaterial != null) areaMr.sharedMaterial = areaMaterial;

            areaVis = area.transform;

            // Create spawn point cylinder as a visual-only child
            var sp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sp.name = "SpawnPoint_Vis";
            sp.transform.SetParent(transform);
            sp.transform.localRotation = Quaternion.identity;
            sp.layer = LayerMask.NameToLayer("Ignore Raycast");

            // Remove collider to prevent physics/raycast interaction
            var colS = sp.GetComponent<Collider>();
            if (colS) Destroy(colS);

            // Assign material if provided
            var mrS = sp.GetComponent<MeshRenderer>();
            if (mrS && spawnMaterial != null) mrS.sharedMaterial = spawnMaterial;

            spawnVis = sp.transform;

            // Ensure presence of a collider for selection/handles and a runtime drag helper
            if (!GetComponent<Collider>()) gameObject.AddComponent<SphereCollider>();
            if (!GetComponent<RuntimeDraggable>()) gameObject.AddComponent<RuntimeDraggable>();
        }

        private void LateUpdate()
        {
            if (group == null) return;

            // Compute patrol area diameter from group radius and position the area visual slightly above ground
            float diameter = Mathf.Max(0.1f, group.patrolRadius * 2f);
            areaVis.position = new Vector3(
                transform.position.x,
                transform.position.y + areaHeight * 0.5f,
                transform.position.z
            );
            areaVis.localScale = new Vector3(diameter, areaHeight, diameter);

            // Determine spawn point position (fallback: forward offset from group)
            Vector3 spPos = (group.spawnPoint != null)
                ? group.spawnPoint.position
                : transform.position + transform.forward * 2f;

            spawnVis.position = spPos + Vector3.up * (areaHeight * 0.5f);

            // Scale spawn visual: larger when patrol radius is significant, otherwise based on base size
            float spawnDiameter = Mathf.Max(0.05f, group.patrolRadius > 1f ? 0.8f : spawnSize * 2f);
            spawnVis.localScale = new Vector3(spawnDiameter, areaHeight, spawnDiameter);
        }
    }
}
