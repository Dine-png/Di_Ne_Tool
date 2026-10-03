using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class DiNeTransplantRequiresCollider : MonoBehaviour
{
    public BoxCollider colliderReference;
    public int marker;
}
