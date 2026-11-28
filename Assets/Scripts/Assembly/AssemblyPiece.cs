using UnityEngine;

/// <summary>
/// A scene object bound to a template node: a loose piece (e.g. mat_cau.002) or a recipe's result model.
/// From its live pose and size it derives where the whole template would be if this piece were in place:
/// the "template frame" (rotation, origin and scale in metres per template unit). Two pieces are correctly
/// assembled exactly when their template frames agree. Added at runtime by AssemblyManager.
/// </summary>
public class AssemblyPiece : MonoBehaviour
{
    [Tooltip("Template node this piece stands for.")]
    public string templateNode;

    public AssemblyTemplate.Node Node { get; private set; }
    /// <summary>The unit (grabbable group) this piece currently belongs to.</summary>
    public AssemblyUnit Unit { get; set; }

    // Visual bounds in this transform's local space, measured once like the template node's.
    Vector3 _localCenter;
    Vector3 _localSize;

    public void Init(AssemblyTemplate.Node node)
    {
        Node = node;
        templateNode = node.name;
        AssemblyBounds.Local(transform, out _localCenter, out _localSize);
    }

    /// <summary>World centre of the piece's visual bounds.</summary>
    public Vector3 Center => transform.TransformPoint(_localCenter);

    /// <summary>Diagonal of the visual bounds at the current scale, in metres.</summary>
    public float Size => Vector3.Scale(_localSize, AssemblyBounds.Abs(transform.lossyScale)).magnitude;

    /// <summary>Metres per template unit: how much smaller or larger than the template this piece is.</summary>
    public float Scale => Node.size > 1e-6f ? Size / Node.size : 1f;

    /// <summary>Orientation of the template frame implied by this piece.</summary>
    public Quaternion FrameRotation => transform.rotation * Quaternion.Inverse(Node.rotation);

    /// <summary>World position of the template origin implied by this piece.</summary>
    public Vector3 FrameOrigin => Center - FrameRotation * (Node.center * Scale);

    /// <summary>Maps a point in template units to world space, using this piece's template frame.</summary>
    public Vector3 TemplateToWorld(Vector3 templatePoint) => FrameOrigin + FrameRotation * (templatePoint * Scale);

    /// <summary>Grows bounds (in frame's local space) by the 8 corners of this piece's visual bounds.</summary>
    public void EncapsulateInto(Transform frame, ref Bounds bounds, ref bool any)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = _localCenter + Vector3.Scale(_localSize * 0.5f,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 p = frame.InverseTransformPoint(transform.TransformPoint(corner));
            if (any) bounds.Encapsulate(p);
            else { bounds = new Bounds(p, Vector3.zero); any = true; }
        }
    }

    /// <summary>Gives the piece a BoxCollider fitted to its meshes if it has no solid collider yet.</summary>
    public void EnsureCollider()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger) return;
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = _localCenter;
        box.size = _localSize;
    }
}
