using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Misadventures.AI
{
    [RequireComponent(typeof(Collider))]
    public class SpawnerAnchor : MonoBehaviour
    {
        public Material visualMaterial;
        public float radius = 0.4f;
        public float height = 0.15f;

        void Reset()
        {
            gameObject.name = "SpawnerAnchor";
        }

        void Start()
        {
            if (GetComponent<MeshRenderer>() == null)
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "Spawner_Vis";
                cyl.transform.SetParent(transform);
                cyl.transform.localPosition = Vector3.zero;
                cyl.transform.localRotation = Quaternion.identity;
                cyl.layer = LayerMask.NameToLayer("Ignore Raycast");

                var col = cyl.GetComponent<Collider>();
                if (col) Destroy(col);

                var mr = cyl.GetComponent<MeshRenderer>();
                if (mr && visualMaterial != null) mr.sharedMaterial = visualMaterial;

                cyl.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
            }

            if (!TryGetComponent<RuntimeDraggable>(out _))
                gameObject.AddComponent<RuntimeDraggable>();
        }
    }
}

