using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RuntimeDraggable : MonoBehaviour
{
    [SerializeField] Camera cam;   // optional override in Inspector
    Plane dragPlane;
    Vector3 grabOffset;
    bool dragging;

    void Awake()
    {
        // Prefer the serialized reference; else try main; else find any camera.
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
    }

    void OnMouseDown()
    {
        // Ensure we actually have a camera; else bail with a clear message.
        if (cam == null)
        {
            Debug.LogError("RuntimeDraggable: No Camera found. Tag your camera as MainCamera or assign it in the component.");
            return;
        }

        // Plane through current object Y so dragging stays level
        dragPlane = new Plane(Vector3.up, transform.position);

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out var enter))
        {
            var hit = ray.GetPoint(enter);
            grabOffset = transform.position - hit; // preserve offset to avoid snapping
            dragging = true;
        }
    }

    void OnMouseDrag()
    {
        if (!dragging || cam == null) return;

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out var enter))
        {
            var hit = ray.GetPoint(enter);
            transform.position = hit + grabOffset; // follow mouse on plane
        }
    }

    void OnMouseUp() => dragging = false;
}


