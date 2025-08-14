using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Misadventures.AI
{
    [RequireComponent(typeof(EnemyGroup))]
    public class GroupVisuals : MonoBehaviour
    {
        public Material areaMaterial;
        public Material spawnMaterial;

        public float areaHeight = 0.1f;
        public float spawnSize = 0.4f;

        public MeshRenderer AreaRenderer { get { return areaMr; } }

        private EnemyGroup group;
        private Transform areaVis;
        private Transform spawnVis;
        private MeshRenderer areaMr;

        void Awake()
        {
            group = GetComponent<EnemyGroup>();

            var area = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            area.name = "PatrolArea_Vis";
            area.transform.SetParent(transform);
            area.transform.localPosition = Vector3.zero;
            area.transform.localRotation = Quaternion.identity;
            area.layer = LayerMask.NameToLayer("Ignore Raycast");
            var colA = area.GetComponent<Collider>();
            if (colA) Destroy(colA);
            areaMr = area.GetComponent<MeshRenderer>();
            if (areaMr && areaMaterial != null) areaMr.sharedMaterial = areaMaterial;
            areaVis = area.transform;

            var sp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sp.name = "SpawnPoint_Vis";
            sp.transform.SetParent(transform);
            sp.transform.localRotation = Quaternion.identity;
            sp.layer = LayerMask.NameToLayer("Ignore Raycast");
            var colS = sp.GetComponent<Collider>();
            if (colS) Destroy(colS);
            var mrS = sp.GetComponent<MeshRenderer>();
            if (mrS && spawnMaterial != null) mrS.sharedMaterial = spawnMaterial;
            spawnVis = sp.transform;

            if (!GetComponent<Collider>()) gameObject.AddComponent<SphereCollider>();
            if (!GetComponent<RuntimeDraggable>()) gameObject.AddComponent<RuntimeDraggable>();
        }

        void LateUpdate()
        {
            if (group == null) return;

            float diameter = Mathf.Max(0.1f, group.patrolRadius * 2f);
            areaVis.position = new Vector3(transform.position.x, transform.position.y + areaHeight * 0.5f, transform.position.z);
            areaVis.localScale = new Vector3(diameter, areaHeight, diameter);

            Vector3 spPos = (group.spawnPoint != null) ? group.spawnPoint.position : transform.position + transform.forward * 2f;
            spawnVis.position = spPos + Vector3.up * (areaHeight * 0.5f);

            float spawnDiameter = Mathf.Max(0.05f, group.patrolRadius > 1f ? 0.8f : spawnSize * 2f);
            spawnVis.localScale = new Vector3(spawnDiameter, areaHeight, spawnDiameter);
        }
    }
}
