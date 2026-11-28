using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A grabbable prop. PlayerInteractor moves it with sweeps and collision-checked steps, so it stays exactly
/// where it is released. Emits events (Grabbed, Released, Collided) so HUD, sound and camera shake stay decoupled.
/// Works with colliders and meshes on the root or on any child. If the object has no collider, a BoxCollider
/// fitted to its meshes is added. The whole hierarchy is moved to the "Pickable" layer.
/// All moving, rotating and scaling pivots on Center (the visual bounds centre), never on the transform pivot.
/// </summary>
public class Pickable : MonoBehaviour
{
    public event Action<Pickable> Grabbed;
    public event Action<Pickable> Released;
    public event Action<Pickable, float> Collided; // impact speed in m/s

    [Header("Weight")]
    [Tooltip("Heavier items (higher mass) lag further behind the hand.")]
    [Min(0.1f)] public float mass = 1f;
    [Tooltip("Follow speed for the lightest item (mass 0.5). Higher = less lag.")]
    public float lightFollowSpeed = 18f;
    [Tooltip("Follow speed for the heaviest item (mass 10). Lower = more lag.")]
    public float heavyFollowSpeed = 4f;

    [Header("Scale limits (multiples of the starting scale)")]
    [Tooltip("Smallest scale per local axis, as a multiple of the scale the item started with.")]
    public Vector3 minScale = new Vector3(0.25f, 0.25f, 0.25f);
    [Tooltip("Largest scale per local axis, as a multiple of the scale the item started with.")]
    public Vector3 maxScale = new Vector3(4f, 4f, 4f);

    [Header("Sounds (generated procedurally when empty)")]
    public AudioClip grabSound;
    public AudioClip releaseSound;
    public AudioClip collisionSound;

    /// <summary>Follow smoothing used by PlayerInteractor, derived from mass.</summary>
    public float FollowSpeed =>
        Mathf.Lerp(lightFollowSpeed, heavyFollowSpeed, Mathf.InverseLerp(0.5f, 10f, mass));

    /// <summary>World-space centre of the visual (mesh) bounds. Imported models often have their pivot
    /// metres away from the geometry, so all movement, rotation, scaling and the gizmo use this point.</summary>
    public Vector3 Center => transform.TransformPoint(_visualBounds.center);

    /// <summary>Sweep radius: the smallest half-extent of the combined collider bounds at the current scale.</summary>
    public float Radius
    {
        get
        {
            Vector3 e = WorldExtents(_colliderBounds);
            return Mathf.Max(0.01f, Mathf.Min(e.x, Mathf.Min(e.y, e.z)));
        }
    }

    /// <summary>Radius around Center that contains every collider at any rotation, at the current scale.</summary>
    public float BoundingRadius =>
        Vector3.Scale(_colliderBounds.center - _visualBounds.center, AbsScale).magnitude
        + WorldExtents(_colliderBounds).magnitude;

    /// <summary>The item's non-trigger colliders, on the root and its children.</summary>
    public IReadOnlyList<Collider> Colliders => _colliders;

    /// <summary>Colliders this item may move through in TryTranslate and TryRotate, e.g. the matching assembly
    /// piece while this one is inside its snap corridor. Filled and cleared by AssemblyManager.</summary>
    public readonly HashSet<Collider> PassThrough = new HashSet<Collider>();

    // Contact depth (metres) a moving item may reach; beyond it, overlaps may only shrink.
    const float PenetrationTolerance = 0.001f;
    // Depth change (metres) ignored as numerical noise when an overlap must not grow.
    const float PenetrationNoise = 0.000001f;
    // Largest rotation checked for collisions in one go, so thin walls cannot be skipped.
    const float MaxRotationStep = 5f;

    static readonly Collider[] s_overlaps = new Collider[32];
    static readonly List<float> s_depths = new List<float>();

    // Bounds in this transform's local space. Moving, rotating or scaling the item never changes them,
    // and Radius, BoundingRadius and Center are derived from them with the current transform on every read.
    Bounds _visualBounds;
    Bounds _colliderBounds;
    List<Collider> _colliders = new List<Collider>();
    Vector3 _startScale;
    Rigidbody _body;
    bool _bodyWasKinematic;
    bool _manipulated;
    readonly List<GameObject> _outlines = new List<GameObject>();

    Vector3 AbsScale
    {
        get { Vector3 s = transform.lossyScale; return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)); }
    }

    Vector3 WorldExtents(Bounds local) => Vector3.Scale(local.extents, AbsScale);

    void Awake()
    {
        int pickableLayer = LayerMask.NameToLayer("Pickable");
        if (pickableLayer >= 0)
        {
            // Every part must be on this layer, or the item's own child colliders would block its sweeps.
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = pickableLayer;
        }
        else
        {
            Debug.LogWarning("Pickable: layer \"Pickable\" is missing. Run Tools > Lab > Build Main Scene to create it.", this);
        }

        bool hasMeshes = TryGetMeshBounds(out _visualBounds);
        _colliders = SolidColliders();
        if (_colliders.Count == 0)
        {
            if (!hasMeshes)
            {
                Debug.LogWarning($"{name}: Pickable has no Collider and no mesh to fit one to, so it cannot be selected. Add a Collider to it or a child.", this);
                enabled = false;
                return;
            }
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = _visualBounds.center;
            box.size = _visualBounds.size;
            Debug.Log($"{name}: Pickable had no Collider, added a BoxCollider fitted to its meshes.", this);
            _colliders = SolidColliders();
        }

        _colliderBounds = ColliderBounds();
        if (!hasMeshes) _visualBounds = _colliderBounds;
        _startScale = transform.localScale;
        _body = GetComponent<Rigidbody>();
    }

    /// <summary>Non-trigger colliders on this object and its children (the ones the crosshair ray can hit).</summary>
    List<Collider> SolidColliders()
    {
        var result = new List<Collider>();
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger) result.Add(c);
        return result;
    }

    /// <summary>Combined mesh bounds in this transform's local space. False if the item has no renderer.</summary>
    bool TryGetMeshBounds(out Bounds local)
    {
        bool any = false;
        local = default;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            // Mesh-space bounds are tighter than renderer.bounds (a world AABB), so prefer them.
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) AddCorners(ref local, ref any, mf.sharedMesh.bounds, mf.transform);
            else AddCorners(ref local, ref any, r.bounds, null);
        }
        return any;
    }

    /// <summary>Combined collider bounds in this transform's local space.</summary>
    Bounds ColliderBounds()
    {
        bool any = false;
        Bounds local = default;
        foreach (Collider c in _colliders)
        {
            switch (c)
            {
                case BoxCollider b: AddCorners(ref local, ref any, new Bounds(b.center, b.size), c.transform); break;
                case SphereCollider s: AddCorners(ref local, ref any, new Bounds(s.center, Vector3.one * s.radius * 2f), c.transform); break;
                case CapsuleCollider cap:
                    Vector3 size = Vector3.one * cap.radius * 2f;
                    size[cap.direction] = Mathf.Max(cap.height, cap.radius * 2f);
                    AddCorners(ref local, ref any, new Bounds(cap.center, size), c.transform);
                    break;
                case MeshCollider m when m.sharedMesh != null: AddCorners(ref local, ref any, m.sharedMesh.bounds, c.transform); break;
                default: AddCorners(ref local, ref any, c.bounds, null); break;
            }
        }
        return local;
    }

    /// <summary>Grows local by the 8 corners of b, given in space's local coordinates (null = world).</summary>
    void AddCorners(ref Bounds local, ref bool any, Bounds b, Transform space)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 p = transform.InverseTransformPoint(space != null ? space.TransformPoint(corner) : corner);
            if (any) local.Encapsulate(p);
            else { local = new Bounds(p, Vector3.zero); any = true; }
        }
    }

    // ---------- Collision-checked transforms ----------

    /// <summary>
    /// Turns the item by angle degrees about axis through Center, in steps of at most MaxRotationStep.
    /// Stops as close to an obstacle as possible and returns false if one blocked it.
    /// </summary>
    public bool TryRotate(float angle, Vector3 axis, LayerMask obstacles)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(angle) / MaxRotationStep));
        float stepAngle = angle / steps;
        for (int i = 0; i < steps; i++)
        {
            if (TryPose(Vector3.zero, Quaternion.AngleAxis(stepAngle, axis), obstacles)) continue;
            HalveUntilFree(Vector3.zero, stepAngle, axis, obstacles);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Moves the item by delta in steps no longer than half its sweep radius, so thin walls cannot be skipped.
    /// Stops as close to an obstacle as possible and returns false if one blocked it.
    /// </summary>
    public bool TryTranslate(Vector3 delta, LayerMask obstacles)
    {
        float distance = delta.magnitude;
        if (distance < 0.00001f) return true;
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.005f, Radius * 0.5f)));
        Vector3 step = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            if (TryPose(step, Quaternion.identity, obstacles)) continue;
            HalveUntilFree(step, 0f, Vector3.up, obstacles);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Sets transform.localScale (clamped to minScale..maxScale of the starting scale) while keeping Center fixed.
    /// Local-axis scaling cannot skew a rotated item. Returns false, leaving the item unchanged, if the new size
    /// would push it into an obstacle or the clamp left nothing to change.
    /// </summary>
    public bool TryScale(Vector3 localScale, LayerMask obstacles)
    {
        Vector3 target = ClampScale(localScale);
        Vector3 oldScale = transform.localScale;
        if ((target - oldScale).sqrMagnitude < 1e-12f) return false;

        Vector3 oldPosition = transform.position;
        Vector3 pivot = Center;
        float ratio = 1f;
        for (int i = 0; i < 3; i++)
            if (Mathf.Abs(oldScale[i]) > 1e-6f) ratio = Mathf.Max(ratio, Mathf.Abs(target[i] / oldScale[i]));

        // Collider shapes only follow a new scale after a sync, so measure, apply, measure again and undo if needed.
        Physics.SyncTransforms();
        int count = Physics.OverlapSphereNonAlloc(pivot, BoundingRadius * ratio, s_overlaps, obstacles, QueryTriggerInteraction.Ignore);
        s_depths.Clear();
        ForEachContact(count, (own, other) => s_depths.Add(Depth(own, own.transform.position, own.transform.rotation, other)));

        transform.localScale = target;
        transform.position += pivot - Center; // grow or shrink around Center, not around the pivot
        Physics.SyncTransforms();

        int k = 0;
        bool blocked = false;
        ForEachContact(count, (own, other) =>
        {
            float after = Depth(own, own.transform.position, own.transform.rotation, other);
            if (after > PenetrationTolerance && after > s_depths[k] + PenetrationNoise) blocked = true;
            k++;
        });
        if (!blocked) return true;

        transform.localScale = oldScale;
        transform.position = oldPosition;
        Physics.SyncTransforms();
        return false;
    }

    Vector3 ClampScale(Vector3 s)
    {
        for (int i = 0; i < 3; i++)
        {
            float a = _startScale[i] * minScale[i], b = _startScale[i] * maxScale[i];
            s[i] = Mathf.Clamp(s[i], Mathf.Min(a, b), Mathf.Max(a, b));
        }
        return s;
    }

    /// <summary>After a blocked step, tries 1/2, 1/4, 1/8 and 1/16 of it to stop close to the contact.</summary>
    void HalveUntilFree(Vector3 step, float angle, Vector3 axis, LayerMask obstacles)
    {
        float fraction = 0.5f;
        for (int j = 0; j < 4; j++, fraction *= 0.5f)
            if (TryPose(step * fraction, Quaternion.AngleAxis(angle * fraction, axis), obstacles)) return;
    }

    /// <summary>Applies a translation plus a rotation about Center unless it would push the item into anything.</summary>
    bool TryPose(Vector3 translation, Quaternion rotation, LayerMask obstacles)
    {
        Vector3 pivot = Center;
        if (PoseBlocked(pivot, translation, rotation, obstacles)) return false;
        transform.SetPositionAndRotation(pivot + rotation * (transform.position - pivot) + translation,
                                         rotation * transform.rotation);
        return true;
    }

    /// <summary>
    /// Tests every collider at its new pose against nearby walls, the table and other items. A contact may reach
    /// PenetrationTolerance; past that it may not deepen at all, so pushing against a wall cannot creep into it
    /// and an item that starts out overlapping something can still be moved out of it.
    /// </summary>
    bool PoseBlocked(Vector3 pivot, Vector3 translation, Quaternion rotation, LayerMask obstacles)
    {
        int count = Physics.OverlapSphereNonAlloc(pivot + translation, BoundingRadius, s_overlaps, obstacles, QueryTriggerInteraction.Ignore);
        foreach (Collider own in _colliders)
        {
            Transform t = own.transform;
            Vector3 newPos = pivot + rotation * (t.position - pivot) + translation;
            Quaternion newRot = rotation * t.rotation;
            for (int i = 0; i < count; i++)
            {
                Collider other = s_overlaps[i];
                if (other.transform.IsChildOf(transform) || PassThrough.Contains(other)) continue;
                float before = Depth(own, t.position, t.rotation, other);
                float after = Depth(own, newPos, newRot, other);
                if (after > PenetrationTolerance && after > before + PenetrationNoise) return true;
            }
        }
        return false;
    }

    void ForEachContact(int count, Action<Collider, Collider> visit)
    {
        foreach (Collider own in _colliders)
            for (int i = 0; i < count; i++)
                if (!s_overlaps[i].transform.IsChildOf(transform)) visit(own, s_overlaps[i]);
    }

    static float Depth(Collider own, Vector3 position, Quaternion rotation, Collider other)
    {
        Transform o = other.transform;
        return Physics.ComputePenetration(own, position, rotation, other, o.position, o.rotation, out _, out float depth) ? depth : 0f;
    }

    /// <summary>
    /// While the player moves, rotates or scales the item, a Rigidbody on it is made kinematic so physics does not
    /// fight the edit; afterwards its previous mode is restored with no leftover velocity.
    /// </summary>
    public void SetManipulated(bool on)
    {
        if (on == _manipulated) return;
        _manipulated = on;
        if (_body == null) return;
        if (on)
        {
            _bodyWasKinematic = _body.isKinematic;
            _body.isKinematic = true;
            return;
        }
        _body.isKinematic = _bodyWasKinematic;
        if (!_body.isKinematic)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
    }

    // ---------- Feedback ----------

    public void NotifyGrabbed()
    {
        if (grabSound == null) grabSound = AudioGenerator.Grab();
        PlayAt(grabSound, 0.7f);
        Grabbed?.Invoke(this);
    }

    public void NotifyReleased()
    {
        if (releaseSound == null) releaseSound = AudioGenerator.Release();
        PlayAt(releaseSound, 0.6f);
        Released?.Invoke(this);
    }

    public void NotifyCollided(float impactSpeed)
    {
        if (impactSpeed < 0.1f) return;
        if (collisionSound == null) collisionSound = AudioGenerator.Collision();
        float v = Mathf.Clamp01(impactSpeed / 3f);
        PlayAt(collisionSound, Mathf.Lerp(0.2f, 1f, v));
        Collided?.Invoke(this, impactSpeed);
    }

    /// <summary>Shows or hides the selection outline on every mesh of this item.</summary>
    public void SetSelected(bool on)
    {
        if (on) EnsureOutlines();
        foreach (GameObject o in _outlines) o.SetActive(on);
    }

    void EnsureOutlines()
    {
        if (_outlines.Count > 0) return;
        Material mat = HighlightManager.Instance != null ? HighlightManager.Instance.SelectionMaterial : null;
        if (mat == null) return; // HighlightManager already warned about the missing material.

        // Inverted hull: a copy of each mesh rendered with Cull Front, see Lab/InvertedHullOutline.
        // Each copy is a child of its mesh, so it follows any later move, rotation or scale.
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        foreach (MeshFilter source in GetComponentsInChildren<MeshFilter>())
        {
            if (source.sharedMesh == null || source.GetComponent<MeshRenderer>() == null) continue;

            var outline = new GameObject("Outline");
            outline.layer = ignoreRaycast;
            outline.transform.SetParent(source.transform, false);
            outline.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            var r = outline.AddComponent<MeshRenderer>();
            var mats = new Material[source.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            outline.SetActive(false);
            _outlines.Add(outline);
        }

        if (_outlines.Count == 0)
            Debug.LogWarning($"{name}: cannot outline, it has no MeshFilter with a mesh on itself or its children.", this);
    }

    /// <summary>Plays a one-shot clip at the item's position (3D, distance-attenuated).</summary>
    void PlayAt(AudioClip clip, float volume)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, Center, volume);
    }
}
