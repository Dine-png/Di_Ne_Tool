using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Test-only types. They deliberately use Unity's real serialization rather than
// standing in for any VRChat, Modular Avatar, or other SDK component.
[Serializable]
public sealed class DiNeTransplantReferenceGroup
{
    public GameObject owner;
    public Transform transform;
    public Component component;
    public UnityEngine.Object[] items;
}

[Serializable]
public sealed class DiNeTransplantManagedLink
{
    public UnityEngine.Object reference;
    [SerializeReference] public DiNeTransplantManagedLink next;
    public Bounds bounds;
    public AnimationCurve curve;
    public Gradient gradient;
    [SerializeField] private int privateValue;

    public int PrivateValue => privateValue;
    public void SetPrivateValue(int value) { privateValue = value; }
}

public sealed class DiNeTransplantProbe : MonoBehaviour
{
    public static Transform ForbiddenSourceRoot;
    public static int TransientSourceReferenceObservations;
    public int marker;
    public string text;
    public GameObject owner;
    public Transform bone;
    public Component sibling;
    public Mesh mesh;
    public UnityEngine.Object external;
    public UnityEngine.Object[] items;
    public List<DiNeTransplantReferenceGroup> groups;
    [SerializeReference] public DiNeTransplantManagedLink managed;
    [SerializeReference] public DiNeTransplantManagedLink managedAlias;
    public UnityEvent<GameObject> onObject = new UnityEvent<GameObject>();
    public GameObject captured;

    public void CaptureObject(GameObject value) { captured = value; }

    private void OnValidate()
    {
        if (ForbiddenSourceRoot == null || transform.root == ForbiddenSourceRoot) return;
        if ((bone != null && bone.IsChildOf(ForbiddenSourceRoot)) ||
            (sibling != null && sibling.transform.IsChildOf(ForbiddenSourceRoot)))
            TransientSourceReferenceObservations++;
        var visited = new HashSet<DiNeTransplantManagedLink>();
        for (var node = managed; node != null && visited.Add(node); node = node.next)
        {
            var objectTransform = node.reference is GameObject ownerObject ? ownerObject.transform : (node.reference as Component)?.transform;
            if (objectTransform != null && objectTransform.IsChildOf(ForbiddenSourceRoot))
                TransientSourceReferenceObservations++;
        }
    }
}
