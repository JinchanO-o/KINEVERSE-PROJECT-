using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single source of truth for target poses: a finished model (the template root, e.g. the A3 instance)
/// whose children sit at their assembled positions, plus optional extra sources (e.g. the mat_cau.fbx asset)
/// whose roots line up with the template root. Every node is described in "template units", the template
/// root's local space, by its rotation, the centre of its visual bounds and their size.
/// Using visual centres instead of pivots keeps the maths right when a loose piece comes from a different
/// fbx than the template (other pivot or mesh scale), as long as its geometry has the same shape.
/// </summary>
public class AssemblyTemplate
{
    public class Node
    {
        public string name;
        /// <summary>The template transform, used to instantiate results and ghosts.</summary>
        public Transform source;
        /// <summary>Position of the node's pivot, in template units.</summary>
        public Vector3 position;
        /// <summary>Rotation of the node in the template frame.</summary>
        public Quaternion rotation;
        /// <summary>Centre of the node's visual bounds, in template units.</summary>
        public Vector3 center;
        /// <summary>Diagonal of the node's visual bounds (in its own frame), in template units.</summary>
        public float size;
        /// <summary>The node's lossy scale relative to the template root.</summary>
        public Vector3 scale;
    }

    public Transform Root { get; }

    readonly List<Transform> _sources = new List<Transform>();
    readonly Dictionary<string, Node> _nodes = new Dictionary<string, Node>();

    public AssemblyTemplate(Transform root, IEnumerable<GameObject> extraSources)
    {
        Root = root;
        _sources.Add(root);
        if (extraSources == null) return;
        foreach (GameObject go in extraSources)
            if (go != null) _sources.Add(go.transform);
    }

    /// <summary>The node with this name (the template root itself included), or null.</summary>
    public Node Find(string nodeName)
    {
        if (string.IsNullOrEmpty(nodeName)) return null;
        if (_nodes.TryGetValue(nodeName, out Node cached)) return cached;

        foreach (Transform source in _sources)
        {
            // An extra source's root is only an alignment frame (the fbx root is named after the file, e.g. "mat_cau",
            // like one of its pieces), so only the template root itself may match by its own name.
            Transform t = FindBreadthFirst(source, nodeName, source == Root);
            if (t == null) continue;
            Node node = Describe(t, source);
            _nodes[nodeName] = node;
            return node;
        }
        return null;
    }

    static Node Describe(Transform t, Transform sourceRoot)
    {
        AssemblyBounds.Local(t, out Vector3 localCenter, out Vector3 localSize);
        Vector3 rootScale = sourceRoot.lossyScale;
        Vector3 s = t.lossyScale;
        var scale = new Vector3(s.x / rootScale.x, s.y / rootScale.y, s.z / rootScale.z);
        return new Node
        {
            name = t.name,
            source = t,
            position = sourceRoot.InverseTransformPoint(t.position),
            rotation = Quaternion.Inverse(sourceRoot.rotation) * t.rotation,
            center = sourceRoot.InverseTransformPoint(t.TransformPoint(localCenter)),
            size = AssemblyBounds.Abs(Vector3.Scale(localSize, scale)).magnitude,
            scale = scale,
        };
    }

    static Transform FindBreadthFirst(Transform root, string nodeName, bool includeRoot)
    {
        var queue = new Queue<Transform>();
        if (includeRoot) queue.Enqueue(root);
        else foreach (Transform child in root) queue.Enqueue(child);
        while (queue.Count > 0)
        {
            Transform t = queue.Dequeue();
            if (t.name == nodeName) return t;
            foreach (Transform child in t) queue.Enqueue(child);
        }
        return null;
    }
}

/// <summary>Visual bounds helpers shared by the template and the live pieces, so both measure the same way.</summary>
public static class AssemblyBounds
{
    /// <summary>
    /// Bounds of every mesh under frame (inactive ones included, selection outlines skipped), as an AABB in
    /// frame's local space. Same corner method as Pickable, so a piece and its template node agree.
    /// </summary>
    public static bool Local(Transform frame, out Vector3 center, out Vector3 size)
    {
        bool any = false;
        var bounds = new Bounds();
        foreach (MeshFilter mf in frame.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || IsOutline(mf.transform)) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = frame.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (any) bounds.Encapsulate(p);
                else { bounds = new Bounds(p, Vector3.zero); any = true; }
            }
        }
        center = bounds.center;
        size = bounds.size;
        return any;
    }

    /// <summary>Selection outlines are copies named "Outline" that Pickable adds under each mesh.</summary>
    public static bool IsOutline(Transform t) =>
        t.name == "Outline" && t.parent != null && t.parent.GetComponent<MeshFilter>() != null;

    public static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
