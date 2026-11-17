using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the whole prototype: layers, materials, Item and Player prefabs, and Assets/Scenes/Main.unity.
/// Run it from the menu: Tools > Lab > Build Main Scene. Re-running overwrites the scene and prefabs.
/// </summary>
public static class LabSceneBuilder
{
    /// <summary>
    /// The project was running the Built-in pipeline, so URP materials rendered magenta.
    /// This creates a URP asset in Assets/Settings and makes it the active pipeline.
    /// Run once: Tools > Lab > Use URP. Then run Build Main Scene again.
    /// </summary>
    [MenuItem("Tools/Lab/Use URP")]
    public static void UseUrp()
    {
        EnsureFolder("Assets/Settings");
        var existing = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        if (existing.Length == 0)
        {
            var folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings");
            Selection.activeObject = folder;
            EditorApplication.ExecuteMenuItem("Assets/Create/Rendering/URP Asset (with Universal Renderer)");
            existing = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        }
        if (existing.Length == 0)
        {
            Debug.LogError("LabSceneBuilder: could not create a URP asset. Create one manually: right-click Assets/Settings > Create > Rendering > URP Asset (with Universal Renderer), then assign it in Project Settings > Graphics.");
            return;
        }

        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(existing[0]));
        GraphicsSettings.defaultRenderPipeline = urpAsset;
        QualitySettings.renderPipeline = urpAsset;
        AssetDatabase.SaveAssets();
        Debug.Log("LabSceneBuilder: URP is now the active render pipeline (" + AssetDatabase.GetAssetPath(urpAsset) + ").");
    }
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string PrefabsDir = "Assets/Prefabs";
    const string MaterialsDir = "Assets/Materials";
    const float LightIntensity = 350f; // candela (physical lights). Tune if the room looks too dark or bright.

    [MenuItem("Tools/Lab/Build Main Scene")]
    public static void Build()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder(PrefabsDir);
        EnsureFolder(MaterialsDir);
        EnsureLayer(6, "Player");
        EnsureLayer(7, "Pickable");

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader outlineShader = Shader.Find("Lab/InvertedHullOutline");
        if (lit == null || outlineShader == null)
        {
            Debug.LogError("LabSceneBuilder: URP Lit or Lab/InvertedHullOutline shader not found. Check the URP install and that Assets/Shaders compiled.");
            return;
        }

        Material wall = Mat("Wall", lit, new Color(0.55f, 0.6f, 0.58f));
        Material floor = Mat("Floor", lit, new Color(0.2f, 0.2f, 0.22f));
        Material steel = Mat("Steel", lit, new Color(0.45f, 0.47f, 0.5f));
        Material fixture = Mat("Fixture", lit, new Color(0.95f, 0.95f, 0.85f));
        Material arm = Mat("Arm", lit, new Color(0.8f, 0.6f, 0.5f));
        Material outline = Mat("Outline", outlineShader, new Color(1f, 0.5333f, 0f));
        Material itemBlue = Mat("ItemBlue", lit, new Color(0.25f, 0.45f, 0.9f));
        Material itemGrey = Mat("ItemGrey", lit, new Color(0.7f, 0.7f, 0.72f));
        Material itemDark = Mat("ItemDark", lit, new Color(0.15f, 0.15f, 0.17f));
        Material itemWarm = Mat("ItemWarm", lit, new Color(0.9f, 0.6f, 0.3f));
        Material itemCamera = Mat("ItemCamera", lit, new Color(0.05f, 0.05f, 0.06f));
        Material itemCrate = Mat("ItemCrate", lit, new Color(0.55f, 0.4f, 0.25f));
        AssetDatabase.SaveAssets();

        GameObject itemPrefab = MakeItemPrefab();
        GameObject playerPrefab = MakePlayerPrefab(arm);

        // ----- Scene -----
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.03f, 0.035f, 0.04f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = new Color(0.04f, 0.045f, 0.05f);

        var lab = new GameObject("Lab");
        lab.AddComponent<RoomAmbience>();
        Transform room = new GameObject("Room").transform;
        room.SetParent(lab.transform, false);

        // Room shell: floor top at y = 0, ceiling at y = 3.
        Prim(PrimitiveType.Cube, "Floor", room, new Vector3(0f, -0.1f, 0f), new Vector3(10f, 0.2f, 8f), floor);
        Prim(PrimitiveType.Cube, "Ceiling", room, new Vector3(0f, 3.1f, 0f), new Vector3(10f, 0.2f, 8f), wall);
        Prim(PrimitiveType.Cube, "Wall North", room, new Vector3(0f, 1.5f, 4.1f), new Vector3(10f, 3f, 0.2f), wall);
        Prim(PrimitiveType.Cube, "Wall South", room, new Vector3(0f, 1.5f, -4.1f), new Vector3(10f, 3f, 0.2f), wall);
        Prim(PrimitiveType.Cube, "Wall East", room, new Vector3(5.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 8.2f), wall);
        Prim(PrimitiveType.Cube, "Wall West", room, new Vector3(-5.1f, 1.5f, 0f), new Vector3(0.2f, 3f, 8.2f), wall);

        // Long central table (top at y = 0.9) that casts long shadows under the lights.
        Transform table = new GameObject("Table").transform;
        table.SetParent(lab.transform, false);
        Prim(PrimitiveType.Cube, "Top", table, new Vector3(0f, 0.86f, 0f), new Vector3(2.4f, 0.08f, 1f), steel);
        foreach (float x in new[] { -1.1f, 1.1f })
            foreach (float z in new[] { -0.42f, 0.42f })
                Prim(PrimitiveType.Cube, "Leg", table, new Vector3(x, 0.42f, z), new Vector3(0.08f, 0.84f, 0.08f), steel);

        // Ceiling lights. The middle one flickers.
        Transform lights = new GameObject("Ceiling Lights").transform;
        lights.SetParent(lab.transform, false);
        MakeCeilingLight("Light A", new Vector3(-2.4f, 2.98f, 1.5f), false, lights, fixture);
        MakeCeilingLight("Light B (flicker)", new Vector3(0f, 2.98f, 0f), true, lights, fixture);
        MakeCeilingLight("Light C", new Vector3(2.4f, 2.98f, -2f), false, lights, fixture);

        // Items: floating above the table (no gravity) plus one on the floor.
        Transform items = new GameObject("Items").transform;
        items.SetParent(lab.transform, false);
        Spawn(itemPrefab, "Cube Light", new Vector3(-0.8f, 1.3f, 0.15f), Vector3.one * 0.18f, 0.6f, itemBlue, items);
        Spawn(itemPrefab, "Cube Mid", new Vector3(-0.3f, 1.55f, -0.2f), Vector3.one * 0.24f, 2f, itemGrey, items);
        Spawn(itemPrefab, "Cube Heavy", new Vector3(0.35f, 1.2f, 0.12f), Vector3.one * 0.34f, 8f, itemDark, items);
        Spawn(itemPrefab, "Cube Small", new Vector3(0.8f, 1.6f, -0.1f), Vector3.one * 0.14f, 0.5f, itemWarm, items);
        Spawn(itemPrefab, "Camera Prop", new Vector3(0f, 1.45f, 0.3f), new Vector3(0.34f, 0.22f, 0.22f), 2.5f, itemCamera, items);
        Spawn(itemPrefab, "Floor Crate", new Vector3(1.8f, 0.25f, -1.5f), Vector3.one * 0.5f, 6f, itemCrate, items);

        // Player: spawns south of the table, facing it.
        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = new Vector3(0f, 0f, -3f);
        player.transform.rotation = Quaternion.identity;

        // Singletons.
        var highlight = new GameObject("Highlight Manager").AddComponent<HighlightManager>();
        highlight.outlineMaterial = outline;

        var hud = new GameObject("HUD").AddComponent<HUD>();
        hud.interactor = player.GetComponent<PlayerInteractor>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LabSceneBuilder: built " + ScenePath + ", Item.prefab and Player.prefab.");
    }

    // ---------- Prefabs ----------

    static GameObject MakeItemPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Item";
        go.layer = LayerMask.NameToLayer("Pickable");
        go.AddComponent<Pickable>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabsDir + "/Item.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject MakePlayerPrefab(Material armMat)
    {
        var player = new GameObject("Player");
        player.layer = LayerMask.NameToLayer("Player");

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        var pc = player.AddComponent<PlayerController>();
        var pi = player.AddComponent<PlayerInteractor>();

        // Head pivot, then camera.
        var head = new GameObject("Head").transform;
        head.SetParent(player.transform, false);
        head.localPosition = new Vector3(0f, 1.6f, 0f);

        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(head, false);
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 75f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();

        // First-person arm: hand root on the camera, palm and forearm as placeholder cubes.
        var handRoot = new GameObject("Hand Rig").transform;
        handRoot.SetParent(camGo.transform, false);
        handRoot.localPosition = pi.restPose;
        Prim(PrimitiveType.Cube, "Palm", handRoot, Vector3.zero, new Vector3(0.1f, 0.08f, 0.12f), armMat, false);
        Prim(PrimitiveType.Cube, "Forearm", handRoot, new Vector3(0f, -0.12f, -0.22f), new Vector3(0.09f, 0.08f, 0.4f), armMat, false);

        pc.head = head;
        pc.playerCamera = cam;
        pi.player = pc;
        pi.playerCamera = cam;
        pi.hand = handRoot;

        var prefab = PrefabUtility.SaveAsPrefabAsset(player, PrefabsDir + "/Player.prefab");
        Object.DestroyImmediate(player);
        return prefab;
    }

    static void Spawn(GameObject prefab, string itemName, Vector3 pos, Vector3 scale, float mass, Material mat, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = itemName;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.GetComponent<Pickable>().mass = mass;
    }

    static void MakeCeilingLight(string name, Vector3 pos, bool flicker, Transform parent, Material fixtureMat)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = pos;

        Prim(PrimitiveType.Cube, "Panel", root, Vector3.zero, new Vector3(0.9f, 0.04f, 0.4f), fixtureMat, false);

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // point straight down

        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = 9f;
        light.spotAngle = 95f;
        light.color = new Color(1f, 0.88f, 0.7f);
        light.intensity = LightIntensity;
        light.shadows = LightShadows.Soft;

        if (flicker) lightGo.AddComponent<FlickerLight>();
    }

    // ---------- Helpers ----------

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material Mat(string matName, Shader shader, Color color)
    {
        string path = MaterialsDir + "/" + matName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    /// <summary>Writes a layer name into the project's TagManager (Player = 6, Pickable = 7).</summary>
    static void EnsureLayer(int index, string layerName)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        var element = layers.GetArrayElementAtIndex(index);
        if (element.stringValue == layerName) return;
        element.stringValue = layerName;
        tagManager.ApplyModifiedProperties();
    }
}
