using UnityEngine;

/// <summary>A local axis of the selected item. The values index the gizmo's arrows.</summary>
public enum Axis { None = -1, X = 0, Y = 1, Z = 2 }

/// <summary>
/// Local axis arrows drawn at the selected item's visual centre (Pickable.Center), turned with the item.
/// Unlit, drawn on top of everything (Lab/GizmoOverlay), on the Ignore Raycast layer with no colliders,
/// and scaled with camera distance so it keeps the same size on screen. Created at runtime by PlayerInteractor.
/// </summary>
public class AxisGizmo : MonoBehaviour
{
    Camera _camera;
    float _screenSize;
    Color[] _colors;
    Color _activeColor;
    readonly MeshRenderer[] _arrows = new MeshRenderer[3];
    Pickable _target;
    Axis _active = Axis.None;

    /// <summary>Builds the gizmo, hidden. Returns null (with a warning) if the material is missing.</summary>
    public static AxisGizmo Create(Camera camera, float screenSize, Color x, Color y, Color z, Color active)
    {
        // Loaded from Resources so the overlay shader is always included in builds.
        var template = Resources.Load<Material>("AxisGizmo");
        if (template == null)
        {
            Debug.LogWarning("AxisGizmo: Resources/AxisGizmo material is missing, the axis gizmo is disabled.");
            return null;
        }

        var go = new GameObject("Axis Gizmo");
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        var gizmo = go.AddComponent<AxisGizmo>();
        gizmo._camera = camera;
        gizmo._screenSize = screenSize;
        gizmo._colors = new[] { x, y, z };
        gizmo._activeColor = active;

        Mesh arrow = BuildArrowMesh();
        Quaternion[] turns = { Quaternion.Euler(0f, 0f, -90f), Quaternion.identity, Quaternion.Euler(90f, 0f, 0f) }; // +Y arrow -> X, Y, Z
        for (int i = 0; i < 3; i++)
        {
            var a = new GameObject(((Axis)i).ToString());
            a.layer = go.layer;
            a.transform.SetParent(go.transform, false);
            a.transform.localRotation = turns[i];
            a.AddComponent<MeshFilter>().sharedMesh = arrow;
            var r = a.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(template);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            gizmo._arrows[i] = r;
        }
        go.SetActive(false);
        return gizmo;
    }

    /// <summary>Shows the gizmo on item (null hides it) with the given axis highlighted.</summary>
    public void Show(Pickable item, Axis active)
    {
        _target = item;
        _active = active;
        if (gameObject.activeSelf != (item != null)) gameObject.SetActive(item != null);
    }

    // LateUpdate, so the gizmo follows the item after this frame's move, rotation or scale.
    void LateUpdate()
    {
        if (_target == null)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 origin = _target.Center;
        float distance = Vector3.Distance(_camera.transform.position, origin);
        transform.SetPositionAndRotation(origin, _target.transform.rotation);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, distance * _screenSize);

        for (int i = 0; i < 3; i++)
            _arrows[i].sharedMaterial.SetColor("_Color", (int)_active == i ? _activeColor : _colors[i]);
    }

    void OnDestroy()
    {
        foreach (MeshRenderer r in _arrows)
            if (r != null) Destroy(r.sharedMaterial);
    }

    /// <summary>Arrow along +Y, length 1: a thin shaft and a cone tip.</summary>
    static Mesh BuildArrowMesh()
    {
        const int sides = 16;
        const float shaftRadius = 0.015f, headRadius = 0.06f, headStart = 0.78f;
        var vertices = new Vector3[sides * 3 + 2];
        var triangles = new int[sides * 12];
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            var ring = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vertices[i] = ring * shaftRadius;                                            // shaft bottom
            vertices[sides + i] = ring * shaftRadius + Vector3.up * headStart;           // shaft top
            vertices[sides * 2 + i] = ring * headRadius + Vector3.up * headStart;        // cone base
        }
        int tip = sides * 3, baseCentre = tip + 1;
        vertices[tip] = Vector3.up;
        vertices[baseCentre] = Vector3.up * headStart;

        int t = 0;
        for (int i = 0; i < sides; i++)
        {
            int n = (i + 1) % sides;
            // Shaft side (two triangles), cone side, cone base. Winding does not matter: the shader uses Cull Off.
            triangles[t++] = i; triangles[t++] = sides + i; triangles[t++] = sides + n;
            triangles[t++] = i; triangles[t++] = sides + n; triangles[t++] = n;
            triangles[t++] = sides * 2 + i; triangles[t++] = tip; triangles[t++] = sides * 2 + n;
            triangles[t++] = sides * 2 + n; triangles[t++] = baseCentre; triangles[t++] = sides * 2 + i;
        }

        var mesh = new Mesh { name = "Gizmo Arrow", vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
}
