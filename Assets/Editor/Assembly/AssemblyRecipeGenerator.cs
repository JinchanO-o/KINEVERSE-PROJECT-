using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Assembly > Create Dragon Bridge Recipes: writes the 5 Dragon Bridge recipe assets (deck, pillar row,
/// bridge, dragon, final) and adds or updates an AssemblyManager in the open scene that uses them.
/// Existing assets are updated in place, so references and GUIDs survive a rerun; values edited in the
/// Inspector are reset to these defaults.
/// </summary>
public static class AssemblyRecipeGenerator
{
    const string Folder = "Assets/Assembly/Dragon Bridge";
    const string DeckModel = "Assets/Model 3d/mat_cau.fbx";

    [MenuItem("Tools/Assembly/Create Dragon Bridge Recipes")]
    public static void CreateDragonBridgeRecipes()
    {
        EnsureFolder(Folder);

        // Recipe 1: the 4 deck pieces in a chain along X, in template order, become the deck model.
        AssemblyRecipe deck = Recipe("1 Deck", "Bridge deck", Nodes("mat_cau", "mat_cau.003", "mat_cau.002", "mat_cau.001"),
                                     Chain(4, Axis.X), "mat_cau_1");
        Thresholds(deck, corridor: 0.15f, snap: 0.35f, guide: 1.5f);

        // Recipe 2: the 4 pillars along X lock into one group at the template spacing (no model of their own).
        // A scene object that already holds all 4 pillars as children (e.g. "tru_cau 1") starts as a finished pillar
        // row: AssemblyManager binds it as one unit and completes this recipe at Play start.
        AssemblyRecipe pillars = Recipe("2 Pillar Row", "Pillar row", Nodes("tru_cau", "tru_cau.001", "tru_cau.002", "tru_cau.003"),
                                        Chain(4, Axis.X), "");
        Thresholds(pillars, corridor: 0.3f, snap: 0.6f, guide: 1.5f);

        // Recipe 3: the deck is lowered onto the pillar row along Y, from above. Both parts are ~20 m long, so the
        // thresholds follow the larger part's length and a magnet pulls the deck in once it is roughly in place:
        // first straight above the pillars, then down along Y.
        AssemblyRecipe bridge = Recipe("3 Bridge", "Bridge", From(deck, pillars),
                                       new List<AssemblyRecipe.Joint> { Joint(0, 1, Axis.Y, ApproachSide.Positive) }, "");
        Thresholds(bridge, corridor: 0.06f, snap: 0.12f, guide: 0.4f);
        Magnet(bridge, largerThresholds: true, alignFirst: true);

        // Recipe 4: head, body and tail along X (head at -X, tail at +X, as in the template). The magnet pulls head
        // and tail onto the body; the thresholds stay those of similar-sized pieces.
        AssemblyRecipe dragon = Recipe("4 Dragon", "Dragon", Nodes("dau_rong", "than_rong", "duoi_rong"),
                                       Chain(3, Axis.X), "");
        Thresholds(dragon, corridor: 0.2f, snap: 0.4f, guide: 1.5f);
        Magnet(dragon, largerThresholds: false, alignFirst: false);

        // Recipe 5: the dragon is lowered onto the bridge along Y (magnet: first straight above, then down) and the
        // whole model (A3) appears.
        AssemblyRecipe final = Recipe("5 Dragon Bridge", "Dragon Bridge", From(dragon, bridge),
                                      new List<AssemblyRecipe.Joint> { Joint(0, 1, Axis.Y, ApproachSide.Positive) }, "A3");
        Thresholds(final, corridor: 0.06f, snap: 0.12f, guide: 0.4f);
        Magnet(final, largerThresholds: true, alignFirst: true);
        final.completionMessage = "Dragon Bridge complete!";
        EditorUtility.SetDirty(final);

        AssetDatabase.SaveAssets();
        var all = new List<AssemblyRecipe> { deck, pillars, bridge, dragon, final };
        SetUpManager(all);
        Debug.Log($"Assembly: wrote {all.Count} Dragon Bridge recipes to {Folder} and set up the AssemblyManager.");
    }

    /// <summary>Opens the folder holding the research log (AssemblyManager's CSV in Application.persistentDataPath).</summary>
    [MenuItem("Tools/Assembly/Open Log Folder")]
    public static void OpenLogFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath + "/");
    }

    static AssemblyRecipe Recipe(string file, string displayName, List<AssemblyRecipe.Input> inputs,
                                 List<AssemblyRecipe.Joint> joints, string result)
    {
        string path = $"{Folder}/{file}.asset";
        var recipe = AssetDatabase.LoadAssetAtPath<AssemblyRecipe>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<AssemblyRecipe>();
            AssetDatabase.CreateAsset(recipe, path);
        }
        recipe.displayName = displayName;
        recipe.inputs = inputs;
        recipe.joints = joints;
        recipe.resultTemplateNode = result;
        recipe.effect = AssemblyEffect.FlyToCenter;
        recipe.maxAngleDegrees = 15f;
        recipe.requireAxisMode = false;
        recipe.completionMessage = "";
        // Pieces of similar size: thresholds follow the smaller part, no magnet (Magnet() changes both).
        recipe.thresholdSize = ThresholdSize.SmallerPartDiameter;
        recipe.magnet = false;
        recipe.magnetRadius = 0.25f;
        recipe.magnetMaxAngle = 45f;
        recipe.magnetSeconds = 0.4f;
        recipe.magnetBreakSpeed = 6f;
        recipe.magnetAlignFirst = false;
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    static void Thresholds(AssemblyRecipe recipe, float corridor, float snap, float guide)
    {
        recipe.corridorRadius = corridor;
        recipe.snapDistance = snap;
        recipe.guideDistance = guide;
        EditorUtility.SetDirty(recipe);
    }

    /// <summary>
    /// Turns on the magnet: it pulls the part in from within a quarter of the larger part's length, correcting up to
    /// 45 degrees. largerThresholds also makes the snap thresholds follow the larger part's length (for joining big
    /// sub-assemblies); alignFirst pulls in two phases, lined up in front of the target first, then along the axis.
    /// </summary>
    static void Magnet(AssemblyRecipe recipe, bool largerThresholds, bool alignFirst)
    {
        recipe.thresholdSize = largerThresholds ? ThresholdSize.LargerPartLength : ThresholdSize.SmallerPartDiameter;
        recipe.magnetAlignFirst = alignFirst;
        recipe.magnet = true;
        recipe.magnetRadius = 0.25f;
        recipe.magnetMaxAngle = 45f;
        recipe.magnetSeconds = 0.4f;
        recipe.magnetBreakSpeed = 6f;
        EditorUtility.SetDirty(recipe);
    }

    static List<AssemblyRecipe.Input> Nodes(params string[] names)
    {
        var list = new List<AssemblyRecipe.Input>();
        foreach (string n in names) list.Add(new AssemblyRecipe.Input { templateNode = n });
        return list;
    }

    static List<AssemblyRecipe.Input> From(params AssemblyRecipe[] recipes)
    {
        var list = new List<AssemblyRecipe.Input>();
        foreach (AssemblyRecipe r in recipes) list.Add(new AssemblyRecipe.Input { fromRecipe = r });
        return list;
    }

    /// <summary>Inputs 0-1, 1-2, ... joined along one axis: only neighbours in the row may snap.</summary>
    static List<AssemblyRecipe.Joint> Chain(int count, Axis axis)
    {
        var list = new List<AssemblyRecipe.Joint>();
        for (int i = 0; i + 1 < count; i++) list.Add(Joint(i, i + 1, axis, ApproachSide.Either));
        return list;
    }

    static AssemblyRecipe.Joint Joint(int a, int b, Axis axis, ApproachSide approach) =>
        new AssemblyRecipe.Joint { a = a, b = b, axis = axis, approach = approach };

    static void SetUpManager(List<AssemblyRecipe> recipes)
    {
        var manager = Object.FindAnyObjectByType<AssemblyManager>();
        if (manager == null)
        {
            var go = new GameObject("Assembly Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Assembly Manager");
            manager = go.AddComponent<AssemblyManager>();
        }
        Undo.RecordObject(manager, "Set Up Assembly Manager");
        manager.recipes = recipes;
        manager.templateRootName = "A3";
        var deckModel = AssetDatabase.LoadAssetAtPath<GameObject>(DeckModel);
        if (deckModel == null) Debug.LogWarning($"Assembly: {DeckModel} not found; the deck pieces will have no template pose.");
        manager.extraTemplateSources = deckModel != null ? new[] { deckModel } : new GameObject[0];
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
