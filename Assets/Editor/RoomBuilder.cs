using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedurally builds a ~44 m² office room with window and door openings, L-desk setup,
/// chair, plant, trash can, lighting and two cameras (top-down + eye-level).
/// Run from menu: Tools > Build > Office Room 3D.
/// Everything is parented under a single root GameObject "OfficeRoom_3D".
/// </summary>
public static class RoomBuilder
{
    // Room shell (metres).
    const float W = 6f;    // width  (X)
    const float L = 7.3f;  // length (Z)
    const float H = 3f;    // height (Y)
    const float T = 0.1f;  // wall / floor / ceiling thickness

    // Back-wall openings.
    const float WinNorthW = 2f, WinNorthH = 1f, WinNorthSill = 1f;
    const float DoorW = 1f, DoorH = 2.1f;
    const float DoorFromRight = 0.5f;

    // Right-wall opening.
    const float WinEastW = 2f, WinEastH = 1.5f, WinEastSill = 1f;

    static Shader _lit;
    static Shader Lit => _lit != null ? _lit : (_lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

    [MenuItem("Tools/Build/Office Room 3D")]
    public static void Build()
    {
        var old = GameObject.Find("OfficeRoom_3D");
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject("OfficeRoom_3D").transform;

        // Materials
        var mFloor   = Mat("Office_Floor",   new Color(0.78f, 0.78f, 0.78f), 0.15f, 0f);
        var mWall    = Mat("Office_Wall",    new Color(0.88f, 0.88f, 0.88f), 0.6f, 0f);
        var mCeiling = Mat("Office_Ceiling", new Color(0.95f, 0.95f, 0.95f), 0.4f, 0f);
        var mBlack   = Mat("Office_Black",   new Color(0.08f, 0.08f, 0.09f), 0.3f, 0f);
        var mChair   = Mat("Office_ChairGrey", new Color(0.72f, 0.72f, 0.74f), 0.3f, 0f);
        var mFrame   = Mat("Office_FrameWhite", new Color(0.95f, 0.95f, 0.95f), 0.3f, 0f);
        var mScreen  = Mat("Office_Screen",  new Color(0.02f, 0.02f, 0.02f), 0.1f, 0f);
        var mLeaf    = Mat("Office_PlantGreen", new Color(0.18f, 0.48f, 0.22f), 0.6f, 0f);
        var mPot     = Mat("Office_PotDark", new Color(0.17f, 0.11f, 0.08f), 0.5f, 0f);
        var mGlass   = MakeGlass("Office_Glass");
        AssetDatabase.SaveAssets();

        // --- Shell ---
        Prim(PrimitiveType.Cube, "Floor",   root, new Vector3(0, -T * 0.5f, 0),      new Vector3(W, T, L), mFloor);
        Prim(PrimitiveType.Cube, "Ceiling", root, new Vector3(0,  H + T * 0.5f, 0),  new Vector3(W, T, L), mCeiling);

        BuildNorthWall(root, mWall, mFrame, mGlass);
        BuildEastWall(root, mWall, mFrame, mGlass);
        Prim(PrimitiveType.Cube, "Wall_West",  root, new Vector3(-W * 0.5f - T * 0.5f, H * 0.5f, 0), new Vector3(T, H, L), mWall);
        Prim(PrimitiveType.Cube, "Wall_South", root, new Vector3(0, H * 0.5f, -L * 0.5f - T * 0.5f), new Vector3(W, H, T), mWall);

        BuildDoor(root, mBlack);
        BuildFurniture(root, mBlack, mChair, mScreen);
        BuildPlant(root, mLeaf, mPot);
        BuildTrashCan(root, mBlack);

        BuildLighting(root);
        BuildCameras(root);

        Selection.activeGameObject = root.gameObject;
        Debug.Log("RoomBuilder: built OfficeRoom_3D (" + W + "m × " + L + "m).");
    }

    // ---------- Walls ----------
    static void BuildNorthWall(Transform root, Material mWall, Material mFrame, Material mGlass)
    {
        float zc = L * 0.5f + T * 0.5f;
        float doorR = W * 0.5f - DoorFromRight;
        float doorL = doorR - DoorW;
        float winCX = ((-W * 0.5f) + doorL) * 0.5f;
        float winL = winCX - WinNorthW * 0.5f;
        float winR = winCX + WinNorthW * 0.5f;
        float winB = WinNorthSill, winT = WinNorthSill + WinNorthH;

        var wall = new GameObject("Wall_North").transform; wall.SetParent(root, false);

        if (winL > -W * 0.5f)
            Prim(PrimitiveType.Cube, "N_Left", wall,
                new Vector3(((-W * 0.5f) + winL) * 0.5f, H * 0.5f, zc),
                new Vector3(winL + W * 0.5f, H, T), mWall);
        if (doorL > winR)
            Prim(PrimitiveType.Cube, "N_Mid", wall,
                new Vector3((winR + doorL) * 0.5f, H * 0.5f, zc),
                new Vector3(doorL - winR, H, T), mWall);
        if (W * 0.5f > doorR)
            Prim(PrimitiveType.Cube, "N_Right", wall,
                new Vector3((doorR + W * 0.5f) * 0.5f, H * 0.5f, zc),
                new Vector3(W * 0.5f - doorR, H, T), mWall);
        Prim(PrimitiveType.Cube, "N_Sill", wall,
            new Vector3(winCX, winB * 0.5f, zc),
            new Vector3(WinNorthW, winB, T), mWall);
        Prim(PrimitiveType.Cube, "N_Lintel", wall,
            new Vector3(winCX, (winT + H) * 0.5f, zc),
            new Vector3(WinNorthW, H - winT, T), mWall);
        Prim(PrimitiveType.Cube, "N_DoorHead", wall,
            new Vector3((doorL + doorR) * 0.5f, (DoorH + H) * 0.5f, zc),
            new Vector3(DoorW, H - DoorH, T), mWall);

        BuildWindow("Window_North", wall, mFrame, mGlass,
            new Vector3(winCX, winB + WinNorthH * 0.5f, zc),
            Quaternion.identity, WinNorthW, WinNorthH);
    }

    static void BuildEastWall(Transform root, Material mWall, Material mFrame, Material mGlass)
    {
        float xc = W * 0.5f + T * 0.5f;
        float winCZ = 0f;
        float winL = winCZ - WinEastW * 0.5f;
        float winR = winCZ + WinEastW * 0.5f;
        float winB = WinEastSill, winT = WinEastSill + WinEastH;

        var wall = new GameObject("Wall_East").transform; wall.SetParent(root, false);

        if (winL > -L * 0.5f)
            Prim(PrimitiveType.Cube, "E_Neg", wall,
                new Vector3(xc, H * 0.5f, ((-L * 0.5f) + winL) * 0.5f),
                new Vector3(T, H, winL + L * 0.5f), mWall);
        if (L * 0.5f > winR)
            Prim(PrimitiveType.Cube, "E_Pos", wall,
                new Vector3(xc, H * 0.5f, (winR + L * 0.5f) * 0.5f),
                new Vector3(T, H, L * 0.5f - winR), mWall);
        Prim(PrimitiveType.Cube, "E_Sill", wall,
            new Vector3(xc, winB * 0.5f, winCZ),
            new Vector3(T, winB, WinEastW), mWall);
        Prim(PrimitiveType.Cube, "E_Lintel", wall,
            new Vector3(xc, (winT + H) * 0.5f, winCZ),
            new Vector3(T, H - winT, WinEastW), mWall);

        BuildWindow("Window_East", wall, mFrame, mGlass,
            new Vector3(xc, winB + WinEastH * 0.5f, winCZ),
            Quaternion.Euler(0, 90f, 0), WinEastW, WinEastH);
    }

    static void BuildWindow(string name, Transform parent, Material mFrame, Material mGlass,
        Vector3 worldPos, Quaternion worldRot, float w, float h)
    {
        var win = new GameObject(name).transform;
        win.SetParent(parent, false);
        win.position = worldPos;
        win.rotation = worldRot;

        float f = 0.06f;
        Prim(PrimitiveType.Cube, "Frame_L", win, new Vector3(-w * 0.5f + f * 0.5f, 0, 0), new Vector3(f, h, T * 1.2f), mFrame);
        Prim(PrimitiveType.Cube, "Frame_R", win, new Vector3( w * 0.5f - f * 0.5f, 0, 0), new Vector3(f, h, T * 1.2f), mFrame);
        Prim(PrimitiveType.Cube, "Frame_B", win, new Vector3(0, -h * 0.5f + f * 0.5f, 0), new Vector3(w, f, T * 1.2f), mFrame);
        Prim(PrimitiveType.Cube, "Frame_T", win, new Vector3(0,  h * 0.5f - f * 0.5f, 0), new Vector3(w, f, T * 1.2f), mFrame);
        Prim(PrimitiveType.Cube, "Crossbar", win, Vector3.zero, new Vector3(w, f * 0.5f, T * 1.1f), mFrame);
        Prim(PrimitiveType.Cube, "Mullion",  win, Vector3.zero, new Vector3(f * 0.5f, h, T * 1.1f), mFrame);
        Prim(PrimitiveType.Cube, "Glass",    win, Vector3.zero, new Vector3(w - f * 1.5f, h - f * 1.5f, T * 0.25f), mGlass);
    }

    // ---------- Door ----------
    static void BuildDoor(Transform root, Material mBlack)
    {
        float zc = L * 0.5f + T * 0.5f;
        float doorR = W * 0.5f - DoorFromRight;
        float doorL = doorR - DoorW;
        float cx = (doorL + doorR) * 0.5f;

        var door = new GameObject("Door").transform; door.SetParent(root, false);
        door.position = new Vector3(cx, 0, zc);

        float ft = 0.06f;
        Prim(PrimitiveType.Cube, "Frame_L", door, new Vector3(-DoorW * 0.5f, DoorH * 0.5f, 0), new Vector3(ft, DoorH, T * 1.4f), mBlack);
        Prim(PrimitiveType.Cube, "Frame_R", door, new Vector3( DoorW * 0.5f, DoorH * 0.5f, 0), new Vector3(ft, DoorH, T * 1.4f), mBlack);
        Prim(PrimitiveType.Cube, "Frame_Top", door, new Vector3(0, DoorH, 0), new Vector3(DoorW + ft * 2, ft, T * 1.4f), mBlack);

        // Leaf hinged on left, swung inward (into the room, -Z side) ~110°.
        var hinge = new GameObject("Hinge").transform; hinge.SetParent(door, false);
        hinge.localPosition = new Vector3(-DoorW * 0.5f + ft * 0.5f, 0, -T * 0.5f);
        hinge.localRotation = Quaternion.Euler(0, 110f, 0);
        Prim(PrimitiveType.Cube, "Leaf", hinge, new Vector3((DoorW - ft) * 0.5f, DoorH * 0.5f, 0),
            new Vector3(DoorW - ft, DoorH, 0.05f), mBlack);
    }

    // ---------- Furniture ----------
    static void BuildFurniture(Transform root, Material mBlack, Material mChair, Material mScreen)
    {
        var fRoot = new GameObject("Furniture").transform; fRoot.SetParent(root, false);

        float topH = 0.75f, topT = 0.04f, legT = 0.05f;
        var desk = new GameObject("Desk").transform; desk.SetParent(fRoot, false);
        desk.position = new Vector3(-1.5f, 0f, 2.0f);

        // L-shape: long arm along X, short arm extending back (+Z) from the +X end.
        Prim(PrimitiveType.Cube, "Top_Long",  desk, new Vector3(0, topH, 0),          new Vector3(1.8f, topT, 0.7f), mBlack);
        Prim(PrimitiveType.Cube, "Top_Short", desk, new Vector3(1.25f, topH, 0.5f),   new Vector3(0.7f, topT, 1.0f), mBlack);
        Prim(PrimitiveType.Cube, "Leg_1", desk, new Vector3(-0.85f, topH * 0.5f,  0.3f), new Vector3(legT, topH, legT), mBlack);
        Prim(PrimitiveType.Cube, "Leg_2", desk, new Vector3(-0.85f, topH * 0.5f, -0.3f), new Vector3(legT, topH, legT), mBlack);
        Prim(PrimitiveType.Cube, "Leg_3", desk, new Vector3( 0.85f, topH * 0.5f, -0.3f), new Vector3(legT, topH, legT), mBlack);
        Prim(PrimitiveType.Cube, "Leg_4", desk, new Vector3( 0.85f, topH * 0.5f,  0.9f), new Vector3(legT, topH, legT), mBlack);
        Prim(PrimitiveType.Cube, "Leg_5", desk, new Vector3( 1.55f, topH * 0.5f, -0.0f), new Vector3(legT, topH, legT), mBlack);
        Prim(PrimitiveType.Cube, "Leg_6", desk, new Vector3( 1.55f, topH * 0.5f,  0.9f), new Vector3(legT, topH, legT), mBlack);

        BuildMonitor(fRoot, mBlack, mScreen, new Vector3(-1.5f, topH + topT, 2.0f - 0.2f));
        BuildLaptop (fRoot, mBlack, mScreen, new Vector3(-1.5f - 0.65f, topH + topT, 2.0f - 0.1f));

        Prim(PrimitiveType.Cube, "Keyboard", fRoot,
            new Vector3(-1.5f, topH + topT + 0.02f, 2.0f + 0.1f),
            new Vector3(0.42f, 0.03f, 0.14f), mBlack);
        Prim(PrimitiveType.Cube, "Mouse", fRoot,
            new Vector3(-1.5f + 0.4f, topH + topT + 0.02f, 2.0f + 0.1f),
            new Vector3(0.07f, 0.035f, 0.11f), mBlack);

        BuildChair(fRoot, mChair, mBlack, new Vector3(-1.5f, 0f, 2.0f - 1.0f));
    }

    static void BuildMonitor(Transform parent, Material mBlack, Material mScreen, Vector3 basePos)
    {
        var mon = new GameObject("Monitor").transform; mon.SetParent(parent, false);
        mon.position = basePos;
        Prim(PrimitiveType.Cylinder, "Stand_Base", mon, new Vector3(0, 0.01f, 0), new Vector3(0.22f, 0.01f, 0.16f), mBlack);
        Prim(PrimitiveType.Cube,     "Stand_Col",  mon, new Vector3(0, 0.15f, 0), new Vector3(0.05f, 0.3f, 0.05f), mBlack);
        Prim(PrimitiveType.Cube,     "Screen_Back",  mon, new Vector3(0, 0.46f, 0),       new Vector3(0.62f, 0.38f, 0.04f), mBlack);
        Prim(PrimitiveType.Cube,     "Screen_Front", mon, new Vector3(0, 0.46f, -0.022f), new Vector3(0.58f, 0.34f, 0.01f), mScreen);
    }

    static void BuildLaptop(Transform parent, Material mBlack, Material mScreen, Vector3 basePos)
    {
        var lap = new GameObject("Laptop").transform; lap.SetParent(parent, false);
        lap.position = basePos;
        Prim(PrimitiveType.Cube, "Base", lap, new Vector3(0, 0.015f, 0), new Vector3(0.36f, 0.025f, 0.26f), mBlack);
        var hinge = new GameObject("Hinge").transform; hinge.SetParent(lap, false);
        hinge.localPosition = new Vector3(0, 0.03f, -0.13f);
        hinge.localRotation = Quaternion.Euler(-100f, 0, 0);
        Prim(PrimitiveType.Cube, "Lid",    hinge, new Vector3(0, 0.13f, 0.01f), new Vector3(0.36f, 0.26f, 0.015f), mBlack);
        Prim(PrimitiveType.Cube, "Screen", hinge, new Vector3(0, 0.13f, 0.02f), new Vector3(0.32f, 0.22f, 0.005f), mScreen);
    }

    static void BuildChair(Transform parent, Material mSeat, Material mBase, Vector3 pos)
    {
        var chair = new GameObject("Chair").transform; chair.SetParent(parent, false);
        chair.position = pos;
        chair.rotation = Quaternion.Euler(0, 180f, 0); // faces back wall (+Z)

        Prim(PrimitiveType.Cylinder, "Base_Disc", chair, new Vector3(0, 0.03f, 0), new Vector3(0.55f, 0.03f, 0.55f), mBase);
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f;
            float r = 0.28f;
            Prim(PrimitiveType.Sphere, "Wheel_" + i, chair,
                new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r),
                new Vector3(0.08f, 0.08f, 0.08f), mBase);
        }
        Prim(PrimitiveType.Cylinder, "Col",  chair, new Vector3(0, 0.22f, 0), new Vector3(0.08f, 0.17f, 0.08f), mBase);
        Prim(PrimitiveType.Cube,     "Seat", chair, new Vector3(0, 0.48f, 0), new Vector3(0.55f, 0.08f, 0.52f), mSeat);
        Prim(PrimitiveType.Cube,     "Back", chair, new Vector3(0, 0.86f, -0.22f), new Vector3(0.55f, 0.65f, 0.08f), mSeat);
        Prim(PrimitiveType.Cube,     "Arm_L", chair, new Vector3(-0.3f, 0.62f, 0), new Vector3(0.05f, 0.04f, 0.4f), mBase);
        Prim(PrimitiveType.Cube,     "Arm_R", chair, new Vector3( 0.3f, 0.62f, 0), new Vector3(0.05f, 0.04f, 0.4f), mBase);
    }

    static void BuildPlant(Transform root, Material mLeaf, Material mPot)
    {
        var plant = new GameObject("Plant").transform; plant.SetParent(root, false);
        plant.position = new Vector3(-0.2f, 0, 2.6f);
        Prim(PrimitiveType.Cylinder, "Pot", plant, new Vector3(0, 0.2f, 0), new Vector3(0.3f, 0.2f, 0.3f), mPot);
        Prim(PrimitiveType.Cube,     "Soil", plant, new Vector3(0, 0.4f, 0), new Vector3(0.28f, 0.02f, 0.28f), mPot);

        // Fronds (overlapping thin cubes rotated outward).
        Random.InitState(42);
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.PI * 2f / 14f + Random.value * 0.2f;
            float r = 0.04f + Random.value * 0.06f;
            float ly = 0.5f + Random.value * 0.3f;
            var lf = Prim(PrimitiveType.Cube, "Leaf_" + i, plant,
                new Vector3(Mathf.Cos(a) * r, ly, Mathf.Sin(a) * r),
                new Vector3(0.06f, 0.55f, 0.14f), mLeaf);
            lf.transform.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), a * Mathf.Rad2Deg, Random.Range(-35f, 35f));
        }
    }

    static void BuildTrashCan(Transform root, Material mBlack)
    {
        Prim(PrimitiveType.Cylinder, "TrashCan", root,
            new Vector3(-2.5f, 0.22f, 1.0f),
            new Vector3(0.28f, 0.22f, 0.28f), mBlack);
    }

    // ---------- Lighting ----------
    static void BuildLighting(Transform root)
    {
        var lights = new GameObject("Lighting").transform; lights.SetParent(root, false);

        // Sun through the back window: aimed to come in from +Z/-X.
        var dir = new GameObject("Directional Sun");
        dir.transform.SetParent(lights, false);
        dir.transform.position = new Vector3(0, 5, L);
        dir.transform.rotation = Quaternion.Euler(45f, -165f, 0f);
        var sun = dir.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.shadows = LightShadows.Soft;

        // Ceiling fill: wide spot pointing straight down.
        var ceil = new GameObject("Ceiling Fill");
        ceil.transform.SetParent(lights, false);
        ceil.transform.position = new Vector3(0, H - 0.05f, 0);
        ceil.transform.rotation = Quaternion.Euler(90f, 0, 0);
        var fill = ceil.AddComponent<Light>();
        fill.type = LightType.Spot;
        fill.spotAngle = 140f;
        fill.range = 9f;
        fill.intensity = 2500f;
        fill.color = new Color(1f, 0.97f, 0.92f);
        fill.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.4f, 0.42f, 0.45f);
        RenderSettings.fog = false;
    }

    // ---------- Cameras ----------
    static void BuildCameras(Transform root)
    {
        var cams = new GameObject("Cameras").transform; cams.SetParent(root, false);

        // Top-down isometric view
        var top = new GameObject("Camera_TopDown");
        top.transform.SetParent(cams, false);
        top.transform.position = new Vector3(0, 10f, -5f);
        top.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        var tcam = top.AddComponent<Camera>();
        tcam.fieldOfView = 45f;
        tcam.clearFlags = CameraClearFlags.SolidColor;
        tcam.backgroundColor = Color.white;
        tcam.depth = 0f;

        // Eye-level view from back-right corner, looking at the desk.
        var eye = new GameObject("Camera_Eye");
        eye.transform.SetParent(cams, false);
        eye.transform.position = new Vector3(2.5f, 1.6f, -3.0f);
        eye.transform.LookAt(new Vector3(-1.0f, 1.0f, 2.0f));
        var ecam = eye.AddComponent<Camera>();
        ecam.fieldOfView = 70f;
        ecam.clearFlags = CameraClearFlags.SolidColor;
        ecam.backgroundColor = Color.white;
        ecam.depth = -1f;
        eye.tag = "MainCamera";
    }

    // ---------- Helpers ----------
    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        return go;
    }

    static Material Mat(string name, Color color, float smoothness, float metallic)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Lit); AssetDatabase.CreateAsset(m, path); }
        m.shader = Lit;
        SetColor(m, color);
        SetFloat(m, "_Smoothness", smoothness);
        SetFloat(m, "_Metallic",   metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MakeGlass(string name)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Lit); AssetDatabase.CreateAsset(m, path); }
        m.shader = Lit;
        SetColor(m, new Color(0.75f, 0.85f, 0.95f, 0.25f));
        if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite"))   m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        SetFloat(m, "_Smoothness", 0.95f);
        SetFloat(m, "_Metallic",   0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color",     c);
    }
    static void SetFloat(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
}
