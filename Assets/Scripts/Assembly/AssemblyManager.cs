using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>What happens to the template instance (e.g. A3) at Play start.</summary>
public enum TemplateDisplay { Ghost, Hidden }

/// <summary>When a recipe's result is shown as a see-through ghost at its target.</summary>
public enum GhostMode { Always, AfterFirstSnap, Hidden }

/// <summary>
/// Template-driven, axis-constrained assembly. At Play start it reads target poses from the template (the finished
/// model in the scene, plus extra sources such as mat_cau.fbx), splits grouped scene pieces into single ones, binds
/// every piece named in a recipe and wraps it in an AssemblyUnit whose axes are the template's. Every frame it
/// checks the held (or axis-moved) unit against the other units of its recipe: inside the insertion corridor the
/// matching pair may pass through each other and a guide line shows the axis; within the snap distance and angle
/// the unit slides into its exact pose, clicks and becomes one group with its partner. A complete recipe turns into
/// its result, which is an input of the next recipe up the tree. Recipes are data only (AssemblyRecipe assets).
/// Hints: a ghost of the recipe's result at its target, a red outline and HUD tip when parts touch that do not fit,
/// and a debug key that splits the last completed assembly again.
/// Put one in the scene; Tools > Assembly > Create Dragon Bridge Recipes sets it up for the Dragon Bridge.
/// </summary>
[DefaultExecutionOrder(100)] // after PlayerInteractor, so this frame's grab, axis move and selection are final
public class AssemblyManager : MonoBehaviour
{
    public static AssemblyManager Instance { get; private set; }

    [Header("Template")]
    [Tooltip("Finished model whose children sit at their assembled poses. Found by name among the scene roots if empty.")]
    public Transform templateRoot;
    [Tooltip("Scene root used as the template when Template Root is empty.")]
    public string templateRootName = "A3";
    [Tooltip("Models whose root lines up with the template root, for nodes the template lacks " +
             "(mat_cau.fbx holds the 4 deck pieces at the pose of mat_cau_1).")]
    public GameObject[] extraTemplateSources;
    [Tooltip("The template instance is a reference only: drawn as a see-through ghost, or hidden.")]
    public TemplateDisplay templateDisplay = TemplateDisplay.Ghost;

    [Header("Recipes")]
    [Tooltip("Every recipe of the tree, any order. Inputs naming template nodes are bound to scene objects of the same name.")]
    public List<AssemblyRecipe> recipes = new List<AssemblyRecipe>();
    [Tooltip("Largest size difference between the pieces of one recipe before a warning, 0.05 = 5%.")]
    [Range(0.005f, 0.5f)] public float scaleTolerance = 0.05f;
    [Tooltip("Pieces split from a scene group (e.g. the 4 deck pieces) start this many times farther from the group's " +
             "centre, since groups are often authored already assembled. 1 = keep the authored layout.")]
    [Range(1f, 3f)] public float splitSpread = 1.6f;

    [Header("References")]
    [Tooltip("Supplies the held item and the active axis key. Auto-found if empty.")]
    public PlayerInteractor interactor;

    [Header("Feel")]
    [Tooltip("Seconds a snapping part takes to slide into its exact pose.")]
    public float snapSeconds = 0.2f;
    [Tooltip("Seconds the pieces of a finished recipe take to fly together (FlyToCenter) or glow (GlowSwap).")]
    public float effectSeconds = 0.4f;
    [Tooltip("Seconds of the pop when the result appears.")]
    public float popSeconds = 0.3f;
    [Tooltip("Select the new group after a snap, so its outline shows what now moves together.")]
    public bool selectAfterSnap = true;
    [Tooltip("Opacity of the guide line along the insertion axis.")]
    [Range(0f, 1f)] public float guideAlpha = 0.45f;
    public Color ghostColor = new Color(0.55f, 0.85f, 1f, 0.08f);
    public Color ghostRimColor = new Color(0.7f, 0.92f, 1f, 0.45f);
    public Color glowColor = new Color(1f, 0.85f, 0.35f, 0.55f);

    [Header("Hints")]
    [Tooltip("Show each recipe's result as a ghost at its target: always, once a first pair has snapped, or never.")]
    public GhostMode ghostMode = GhostMode.AfterFirstSnap;
    [Tooltip("Gap between two parts, as a fraction of the smaller one's size, at which parts that do not snap count as touching.")]
    [Range(0.005f, 0.5f)] public float contactDistance = 0.04f;
    [Tooltip("Seconds the red outline shows on a part that does not fit the one being handled.")]
    public float wrongFlashSeconds = 0.6f;
    [Tooltip("Smallest time in seconds between two hints for touching parts.")]
    public float hintCooldown = 1.5f;
    public Color wrongOutlineColor = new Color(1f, 0.15f, 0.1f, 1f);
    [Tooltip("Gap, as a fraction of the larger part's length, at which holding a part next to one it cannot join yet " +
             "(a stage in between is unfinished) shows which recipe to finish first.")]
    [Range(0.01f, 1f)] public float stageHintDistance = 0.15f;
    [Tooltip("Recipe ghost hint: a little stronger than the template ghost, so the next target stands out.")]
    public Color hintColor = new Color(0.55f, 0.85f, 1f, 0.14f);
    public Color hintRimColor = new Color(0.75f, 0.95f, 1f, 0.75f);

    [Header("Research log")]
    [Tooltip("Append one CSV row per snap to Application.persistentDataPath.")]
    public bool logSnaps = true;
    [Tooltip("Written into every row, to tell participants apart in one file.")]
    public string participantId = "";
    [Tooltip("CSV file name in Application.persistentDataPath.")]
    public string logFileName = "assembly_log.csv";

    [Header("Debug")]
    [Tooltip("Splits the last completed assembly back into its scattered inputs, for repeated testing.")]
#if ENABLE_INPUT_SYSTEM
    public Key splitKey = Key.F9;
#else
    public KeyCode splitKey = KeyCode.F9;
#endif

    public AssemblyTemplate Template { get; private set; }
    public IReadOnlyList<AssemblyUnit> Units => _units;

    /// <summary>A possible snap of the moving unit onto a partner, measured this frame.</summary>
    class Candidate
    {
        public AssemblyUnit mover, partner;
        public AssemblyPiece moverPiece, partnerPiece;
        public AssemblyRecipe.Joint joint;
        public Vector3 targetCenter;  // where moverPiece's centre belongs
        public Vector3 axis;          // insertion axis, world space
        public Axis localAxis;        // mover's own axis closest to it (the key and gizmo arrow to use)
        public float lateral, axial, angle, guideLength, refSize;
        public bool canSnap;
        public bool onlyAxisKeyMissing; // everything fits but requireAxisMode wants the axis key held
        public float distance;          // |mover piece centre - target|
        public float along;             // axial, positive on the side input a comes from
        public float side;              // +1 when the mover holds the joint's input a, -1 when it holds b
        public float tolerance;         // overshoot past the target that still counts as the right side
        public float largerLength;      // longest side of the larger of the two units (magnet radius unit)

        public Candidate Copy() => (Candidate)MemberwiseClone();
    }

    /// <summary>A finished recipe, kept so the debug split can take it apart again.</summary>
    class Completion
    {
        public AssemblyRecipe recipe;
        public List<(int slot, List<AssemblyPiece> pieces)> inputs;
        public AssemblyPiece resultPiece; // a piece of the result: the model, or one of the locked pieces
    }

    readonly List<AssemblyUnit> _units = new List<AssemblyUnit>();
    readonly List<Completion> _completed = new List<Completion>();
    readonly Dictionary<AssemblyRecipe, GameObject> _recipeGhosts = new Dictionary<AssemblyRecipe, GameObject>();
    LineRenderer _guide;
    Material _guideMaterial, _ghostMaterial, _glowMaterial, _wrongMaterial;
    AssemblyUnit _passThroughUnit;
    AssemblyUnit _handledUnit; // unit of the current handling attempt, for the research log
    AssemblyLog _log;
    Material _hintMaterial;
    GameObject _templateGhost;
    Transform _stash; // inactive holder for pieces replaced by a result model, restored by the debug split
    float _nextHintTime;

    /// <summary>A magnet pull in progress: the mover eases from its start pose to its snap pose.</summary>
    class Pull
    {
        public Candidate c;
        public Candidate atStart;         // measurements when the pull began, logged only if it lands
        public bool alignFirst;           // two phases: line up in front of the target, then slide in along the axis
        public Vector3 alignPosition;     // root position at the end of the line-up phase
        public float time;
        public Vector3 startPosition, endPosition, startScale, endScale;
        public Quaternion startRotation, endRotation;
        public Vector3 lastSetPosition;   // where the pull left the root last frame (axis drags are measured from it)
        public Vector3 lastHoldPoint;     // the player's hold point last frame (carrying is measured from it)
        public bool hasHoldPoint;
    }

    Pull _pull;
    AssemblyUnit _magnetBlocked; // broke out of a pull; the magnet re-arms once it has left the radius

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Material m in new[] { _guideMaterial, _ghostMaterial, _glowMaterial, _wrongMaterial, _hintMaterial })
            if (m != null) Destroy(m);
    }

    void Start()
    {
        if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
        if (!ResolveTemplate())
        {
            enabled = false;
            return;
        }
        var stash = new GameObject("Assembly Stash");
        stash.SetActive(false);
        _stash = stash.transform;
        _stash.SetParent(transform, false);

        CreateVisuals();
        if (logSnaps)
        {
            _log = new AssemblyLog(logFileName, participantId);
            Debug.Log($"AssemblyManager: logging snaps to {_log.Path}", this);
        }
        BindPieces();
        CheckScales();
        ShowTemplate();
    }

    void Update()
    {
        if (HUD.IsPaused) return;
        if (SplitPressed()) SplitLast();

        HighlightManager selection = HighlightManager.Instance;
        Pickable selected = selection != null ? selection.Selected : null;
        AssemblyUnit active = selected != null ? selected.GetComponent<AssemblyUnit>() : null;
        if (active != null && active.Busy)
        {
            selection.Select(null); // a snapping or transforming unit cannot be handled
            active = null;
        }

        if (_pull != null)
        {
            UpdatePull(); // the magnet owns the unit until it lands or the player breaks free
            return;
        }

        // Snapping is checked only while the player is actually moving or turning the unit.
        bool handling = active != null && active.Recipe != null && interactor != null &&
                        (interactor.Held == selected || interactor.HandState == HandState.AxisMoving ||
                         interactor.HandState == HandState.Rotating);
        TrackAttempt(handling ? active : null);
        if (handling && active.Recipe.magnet && TryStartPull(active)) return;
        Candidate best = handling ? FindCandidate(active) : null;
        ShowCorridor(best);

        if (best != null && best.canSnap)
        {
            StartCoroutine(Snap(best));
            return;
        }
        if (best != null && best.onlyAxisKeyMissing) Hint($"Hold {best.localAxis} to lock it in");
        if (handling && best == null) StageHint(active);
        if (active != null && interactor != null && (handling || interactor.Held == selected)) CheckContacts(active, best);
    }

    // Ghosts follow their anchor after this frame's moves and snaps.
    void LateUpdate()
    {
        UpdateRecipeGhosts();
    }

    // ---------- Setup ----------

    bool ResolveTemplate()
    {
        if (templateRoot == null)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == templateRootName) { templateRoot = root.transform; break; }
        }
        if (templateRoot == null)
        {
            Debug.LogWarning($"AssemblyManager: no template. Assign Template Root or add a scene root named \"{templateRootName}\".", this);
            return false;
        }
        Template = new AssemblyTemplate(templateRoot, extraTemplateSources);
        return true;
    }

    void CreateVisuals()
    {
        // The guide reuses the axis gizmo's overlay material, so it reads like the gizmo's own arrows.
        var gizmo = Resources.Load<Material>("AxisGizmo");
        if (gizmo != null)
        {
            _guideMaterial = new Material(gizmo);
            var go = new GameObject("Assembly Guide");
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            go.transform.SetParent(transform, false);
            _guide = go.AddComponent<LineRenderer>();
            _guide.sharedMaterial = _guideMaterial;
            _guide.positionCount = 2;
            _guide.useWorldSpace = true;
            _guide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _guide.receiveShadows = false;
            _guide.numCapVertices = 4;
            _guide.enabled = false;
        }
        else
        {
            Debug.LogWarning("AssemblyManager: Resources/AxisGizmo material is missing, the guide line is disabled.", this);
        }

        // Loaded from Resources so the ghost shader is always included in builds.
        var ghost = Resources.Load<Material>("AssemblyGhost");
        if (ghost != null)
        {
            _ghostMaterial = new Material(ghost);
            _ghostMaterial.SetColor("_Color", ghostColor);
            _ghostMaterial.SetColor("_RimColor", ghostRimColor);
            _glowMaterial = new Material(ghost);
            _glowMaterial.SetColor("_Color", glowColor);
            _glowMaterial.SetColor("_RimColor", Color.white);
            _hintMaterial = new Material(ghost);
            _hintMaterial.SetColor("_Color", hintColor);
            _hintMaterial.SetColor("_RimColor", hintRimColor);
        }
        else
        {
            Debug.LogWarning("AssemblyManager: Resources/AssemblyGhost material is missing, ghosts and glow are disabled.", this);
        }

        if (HighlightManager.Instance != null && HighlightManager.Instance.outlineMaterial != null)
        {
            _wrongMaterial = new Material(HighlightManager.Instance.outlineMaterial);
            _wrongMaterial.SetColor("_OutlineColor", wrongOutlineColor);
        }
    }

    /// <summary>
    /// Binds every template node a recipe names to the scene root of the same name and wraps it in a unit.
    /// Scene groups (a root with no Pickable or mesh of its own whose children are recipe pieces, like the
    /// 4 deck pieces under "mat_cau") are split first, so each child becomes a piece. Only runtime objects
    /// change: the saved scene is untouched.
    /// </summary>
    void BindPieces()
    {
        var wanted = new Dictionary<string, (AssemblyRecipe recipe, int slot)>();
        foreach (AssemblyRecipe recipe in recipes)
        {
            if (recipe == null) continue;
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                AssemblyRecipe.Input input = recipe.inputs[i];
                if (input.fromRecipe != null || string.IsNullOrEmpty(input.templateNode)) continue;
                if (wanted.ContainsKey(input.templateNode))
                    Debug.LogWarning($"AssemblyManager: \"{input.templateNode}\" is an input of more than one recipe; using the first.", recipe);
                else
                    wanted[input.templateNode] = (recipe, i);
            }
        }

        int total = wanted.Count;
        int bound = BindPreassembled(wanted);
        List<List<Transform>> groups = SplitGroups(wanted);

        foreach (KeyValuePair<string, (AssemblyRecipe recipe, int slot)> entry in wanted)
        {
            AssemblyTemplate.Node node = Template.Find(entry.Key);
            if (node == null)
            {
                Debug.LogWarning($"AssemblyManager: the template has no node \"{entry.Key}\" ({entry.Value.recipe.DisplayName}).", entry.Value.recipe);
                continue;
            }
            GameObject go = FindScenePiece(entry.Key);
            if (go == null)
            {
                Debug.LogWarning($"AssemblyManager: no scene object named \"{entry.Key}\" to bind ({entry.Value.recipe.DisplayName}).", entry.Value.recipe);
                continue;
            }

            var piece = go.AddComponent<AssemblyPiece>();
            piece.Init(node);
            piece.EnsureCollider();
            CreateUnit(go.name, entry.Value.recipe, piece.FrameRotation,
                       new[] { (entry.Value.slot, new List<AssemblyPiece> { piece }) }, go.GetComponent<Pickable>());
            bound++;
        }
        foreach (List<Transform> group in groups) Spread(group);
        Debug.Log($"AssemblyManager: bound {bound} of {total} pieces for {recipes.Count} recipes.", this);
    }

    /// <summary>
    /// Binds scene objects the player already handles as one finished group: a root with its own Pickable whose direct
    /// children are every input of a recipe made only of template nodes (e.g. "tru_cau 1" holding the 4 pillars). The
    /// children become the pieces of one unit, the root loses its own Pickable and solid colliders (Pickable.Awake fits
    /// a box to the root, which would stay behind as an invisible wall) but keeps its other components, and the recipe
    /// is completed at once without effects, so its result is ready for the next recipe. Bound names are removed from
    /// wanted. Returns the number of pieces bound.
    /// </summary>
    int BindPreassembled(Dictionary<string, (AssemblyRecipe recipe, int slot)> wanted)
    {
        int bound = 0;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var rootPickable = root.GetComponent<Pickable>();
            if (rootPickable == null || IsTemplate(root) || root.GetComponent<AssemblyUnit>() != null) continue;

            foreach (AssemblyRecipe recipe in recipes)
            {
                if (recipe == null || recipe.inputs.Count == 0 || !AllTemplateInputs(recipe)) continue;
                var children = new List<(int slot, Transform child, AssemblyTemplate.Node node)>();
                for (int i = 0; i < recipe.inputs.Count; i++)
                {
                    string nodeName = recipe.inputs[i].templateNode;
                    Transform child = root.transform.Find(nodeName);
                    AssemblyTemplate.Node node = Template.Find(nodeName);
                    if (child == null || child.GetComponent<Renderer>() == null || node == null ||
                        !wanted.TryGetValue(nodeName, out var owner) || owner.recipe != recipe)
                    {
                        children = null;
                        break;
                    }
                    children.Add((i, child, node));
                }
                if (children == null) continue;

                var slots = new List<(int, List<AssemblyPiece>)>();
                foreach ((int slot, Transform child, AssemblyTemplate.Node node) in children)
                {
                    var piece = child.gameObject.AddComponent<AssemblyPiece>();
                    piece.Init(node);
                    piece.EnsureCollider();
                    slots.Add((slot, new List<AssemblyPiece> { piece }));
                    wanted.Remove(child.name);
                }
                foreach (Collider c in root.GetComponents<Collider>())
                    if (!c.isTrigger) Destroy(c);
                AssemblyUnit unit = CreateUnit(recipe.DisplayName, recipe, slots[0].Item2[0].FrameRotation, slots, rootPickable);
                Destroy(rootPickable);
                bound += children.Count;

                Debug.Log($"AssemblyManager: {root.name} already holds every input of {recipe.DisplayName}; it starts assembled.", root);
                StartCoroutine(Complete(unit, true)); // instant: runs to the end right here
                break;
            }
        }
        return bound;
    }

    /// <summary>True when every input of the recipe is a template node (none is another recipe's result).</summary>
    static bool AllTemplateInputs(AssemblyRecipe recipe)
    {
        foreach (AssemblyRecipe.Input input in recipe.inputs)
            if (input.fromRecipe != null || string.IsNullOrEmpty(input.templateNode)) return false;
        return true;
    }

    /// <summary>Detaches recipe pieces from their scene group roots. Returns the pieces of each split group.</summary>
    List<List<Transform>> SplitGroups(Dictionary<string, (AssemblyRecipe, int)> wanted)
    {
        var groups = new List<List<Transform>>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (IsTemplate(root) || root.GetComponent<Pickable>() != null || root.GetComponent<Renderer>() != null) continue;
            var parts = new List<Transform>();
            foreach (Transform child in root.transform)
                if (wanted.ContainsKey(child.name)) parts.Add(child);
            foreach (Transform part in parts)
                part.SetParent(null, true);
            if (parts.Count > 1) groups.Add(parts);
        }
        return groups;
    }

    /// <summary>Moves the units of a split group's pieces apart by splitSpread around the group's centre.</summary>
    void Spread(List<Transform> parts)
    {
        var units = new List<AssemblyUnit>();
        foreach (Transform part in parts)
        {
            var piece = part.GetComponent<AssemblyPiece>();
            if (piece != null && piece.Unit != null) units.Add(piece.Unit);
        }
        if (units.Count < 2) return;
        Vector3 center = Vector3.zero;
        foreach (AssemblyUnit u in units) center += u.Pickable.Center;
        center /= units.Count;
        foreach (AssemblyUnit u in units)
            u.Pickable.TryTranslate((u.Pickable.Center - center) * (splitSpread - 1f), ObstacleMask);
    }

    GameObject FindScenePiece(string pieceName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != pieceName || IsTemplate(root) || root.GetComponent<AssemblyUnit>() != null) continue;
            if (root.GetComponentInChildren<Renderer>(true) != null) return root;
        }
        return null;
    }

    bool IsTemplate(GameObject go) =>
        (templateRoot != null && go == templateRoot.gameObject) || go == _templateGhost || go == gameObject;

    /// <summary>Warns when the pieces of one recipe are not the same size relative to the template.</summary>
    void CheckScales()
    {
        float templateScale = Mathf.Abs(templateRoot.lossyScale.x);
        foreach (AssemblyRecipe recipe in recipes)
        {
            if (recipe == null) continue;
            var scales = new List<(string name, float scale)>();
            foreach (AssemblyUnit unit in _units)
                if (unit.Recipe == recipe) scales.Add((unit.name, unit.AverageScale));
            if (scales.Count == 0) continue;

            float mean = 0f;
            foreach (var s in scales) mean += s.scale;
            mean /= scales.Count;
            Debug.Log($"AssemblyManager: {recipe.DisplayName}: {mean:0.#####} m per template unit " +
                      $"({mean / Mathf.Max(1e-6f, templateScale):0.###}x the template instance).", recipe);
            foreach (var s in scales)
            {
                float error = Mathf.Abs(s.scale / mean - 1f);
                if (error > scaleTolerance)
                    Debug.LogWarning($"AssemblyManager: {recipe.DisplayName}: \"{s.name}\" is {error:P0} off the recipe's average size, " +
                                     "so it will be rescaled when it snaps.", recipe);
            }
        }
    }

    void ShowTemplate()
    {
        if (templateDisplay == TemplateDisplay.Ghost && _ghostMaterial != null)
            _templateGhost = MakeGhost(templateRoot.gameObject, _ghostMaterial);
        // The original stays in the scene, inactive, as the source of result models and ghosts.
        templateRoot.gameObject.SetActive(false);
    }

    // ---------- Units ----------

    /// <summary>
    /// New unit root at the pieces' centre, turned to the template frame, holding the pieces and a fresh Pickable
    /// (added after parenting, so it measures the whole group). Pickable settings are copied from settingsFrom,
    /// or from a piece's own Pickable, which is removed so only the root is grabbable.
    /// </summary>
    AssemblyUnit CreateUnit(string unitName, AssemblyRecipe recipe, Quaternion frame,
                            IEnumerable<(int slot, List<AssemblyPiece> pieces)> slots, Pickable settingsFrom)
    {
        var all = new List<AssemblyPiece>();
        foreach (var s in slots) all.AddRange(s.pieces);
        Vector3 center = Vector3.zero;
        foreach (AssemblyPiece p in all) center += p.Center;
        center /= Mathf.Max(1, all.Count);

        string settings = settingsFrom != null ? JsonUtility.ToJson(settingsFrom) : null;
        var root = new GameObject(unitName);
        root.transform.SetPositionAndRotation(center, frame);
        foreach (AssemblyPiece p in all)
        {
            var own = p.GetComponent<Pickable>();
            if (own != null)
            {
                if (settings == null) settings = JsonUtility.ToJson(own);
                DestroyImmediate(own);
            }
            RemoveOutlines(p.transform);
            p.transform.SetParent(root.transform, true);
        }

        var unit = root.AddComponent<AssemblyUnit>();
        unit.Recipe = recipe;
        foreach (var s in slots) unit.AddSlot(s.slot, s.pieces);
        var pickable = root.AddComponent<Pickable>();
        if (settings != null) JsonUtility.FromJsonOverwrite(settings, pickable);
        unit.Pickable = pickable;
        _units.Add(unit);
        return unit;
    }

    /// <summary>Removes the selection outline copies a previous Pickable left under the meshes; the new one makes its own.</summary>
    static void RemoveOutlines(Transform root)
    {
        var outlines = new List<GameObject>();
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            if (AssemblyBounds.IsOutline(mf.transform)) outlines.Add(mf.gameObject);
        foreach (GameObject o in outlines) Destroy(o);
    }

    AssemblyUnit Merge(AssemblyUnit a, AssemblyUnit b, Quaternion frame)
    {
        var slots = new List<(int, List<AssemblyPiece>)>();
        foreach (KeyValuePair<int, List<AssemblyPiece>> s in a.Slots) slots.Add((s.Key, new List<AssemblyPiece>(s.Value)));
        foreach (KeyValuePair<int, List<AssemblyPiece>> s in b.Slots) slots.Add((s.Key, new List<AssemblyPiece>(s.Value)));
        AssemblyUnit merged = CreateUnit($"{a.Recipe.DisplayName} (partial)", a.Recipe, frame, slots, b.Pickable);
        RemoveUnit(a);
        RemoveUnit(b);
        return merged;
    }

    void RemoveUnit(AssemblyUnit unit)
    {
        _units.Remove(unit);
        if (_passThroughUnit == unit) _passThroughUnit = null;
        HighlightManager selection = HighlightManager.Instance;
        if (selection != null && selection.Selected == unit.Pickable) selection.Select(null);
        Destroy(unit.gameObject);
    }

    /// <summary>Name for a unit holding one input slot: the piece's name, or the input recipe's name.</summary>
    static string SlotName(AssemblyRecipe recipe, int slot, List<AssemblyPiece> pieces)
    {
        AssemblyRecipe.Input input = slot >= 0 && slot < recipe.inputs.Count ? recipe.inputs[slot] : null;
        if (input != null && input.fromRecipe != null) return input.fromRecipe.DisplayName;
        return pieces.Count > 0 ? pieces[0].name : recipe.DisplayName;
    }

    // ---------- Snap candidates ----------

    Candidate FindCandidate(AssemblyUnit mover)
    {
        AssemblyRecipe recipe = mover.Recipe;
        Candidate best = null;
        foreach (AssemblyUnit partner in _units)
        {
            if (partner == mover || partner.Busy || partner.Recipe != recipe) continue;
            foreach (AssemblyRecipe.Joint joint in recipe.joints)
            {
                if (!JointSides(mover, partner, joint, out int mine, out int theirs, out float side)) continue;
                Candidate c = Evaluate(mover, partner, mover.Slots[mine][0], partner.Slots[theirs][0], joint, side, recipe);
                if (c == null) continue;
                float distance = c.lateral + Mathf.Abs(c.axial);
                float bestDistance = best != null ? best.lateral + Mathf.Abs(best.axial) : float.MaxValue;
                if (best == null || (c.canSnap && !best.canSnap) || (c.canSnap == best.canSnap && distance < bestDistance))
                    best = c;
            }
        }
        return best;
    }

    /// <summary>
    /// True if the joint links a slot of mover to a slot of partner. side is +1 when the mover holds the joint's
    /// input a and -1 when it holds b (the approach side flips).
    /// </summary>
    static bool JointSides(AssemblyUnit mover, AssemblyUnit partner, AssemblyRecipe.Joint joint,
                           out int mine, out int theirs, out float side)
    {
        if (mover.Slots.ContainsKey(joint.a) && partner.Slots.ContainsKey(joint.b)) { mine = joint.a; theirs = joint.b; side = 1f; return true; }
        if (mover.Slots.ContainsKey(joint.b) && partner.Slots.ContainsKey(joint.a)) { mine = joint.b; theirs = joint.a; side = -1f; return true; }
        mine = theirs = -1;
        side = 0f;
        return false;
    }

    /// <summary>
    /// Measures the mover piece against the pose the partner's template frame gives it: sideways offset from the
    /// insertion line, distance along it and rotation error. Null when it is outside the corridor.
    /// </summary>
    /// <summary>
    /// Measures the mover piece against the pose the partner's template frame gives it: sideways offset from the
    /// insertion line, distance along it and rotation error. No limits are applied here.
    /// side is +1 when the mover holds the joint's input a, -1 when it holds b.
    /// </summary>
    Candidate Measure(AssemblyUnit mover, AssemblyUnit partner, AssemblyPiece mine, AssemblyPiece theirs,
                      AssemblyRecipe.Joint joint, float side, AssemblyRecipe recipe)
    {
        Quaternion frame = theirs.FrameRotation;
        Vector3 target = theirs.TemplateToWorld(mine.Node.center);
        Vector3 axis = frame * AxisVector(joint.axis);
        Vector3 offset = mine.Center - target;
        float axial = Vector3.Dot(offset, axis);
        float refSize = ThresholdLength(recipe, mover, partner);
        return new Candidate
        {
            mover = mover, partner = partner, moverPiece = mine, partnerPiece = theirs, joint = joint,
            targetCenter = target, axis = axis, localAxis = LocalAxis(mover.transform, axis),
            lateral = (offset - axis * axial).magnitude, axial = axial, along = axial * side, side = side,
            distance = offset.magnitude, angle = Quaternion.Angle(mine.FrameRotation, frame),
            refSize = refSize, tolerance = recipe.corridorRadius * refSize * 0.5f, // a little overshoot still counts
            largerLength = Mathf.Max(mover.Length, partner.Length),
        };
    }

    /// <summary>The mover's measurement within the recipe's corridor and guide range, or null when outside them.</summary>
    Candidate Evaluate(AssemblyUnit mover, AssemblyUnit partner, AssemblyPiece mine, AssemblyPiece theirs,
                       AssemblyRecipe.Joint joint, float side, AssemblyRecipe recipe)
    {
        Candidate c = Measure(mover, partner, mine, theirs, joint, side, recipe);
        if (c.lateral > recipe.corridorRadius * c.refSize) return null;
        c.guideLength = recipe.guideDistance * c.refSize;
        if (!InRange(c, c.guideLength)) return null;

        bool fits = InRange(c, recipe.snapDistance * c.refSize) && c.angle <= recipe.maxAngleDegrees;
        bool axisHeld = !recipe.requireAxisMode || (interactor != null && interactor.ActiveAxis == c.localAxis);
        c.canSnap = fits && axisHeld;
        c.onlyAxisKeyMissing = fits && !axisHeld;
        return c;
    }

    /// <summary>On the joint's approach side and no farther than range along the axis.</summary>
    static bool InRange(Candidate c, float range)
    {
        switch (c.joint.approach)
        {
            case ApproachSide.Positive: return c.along >= -c.tolerance && c.along <= range;
            case ApproachSide.Negative: return -c.along >= -c.tolerance && -c.along <= range;
            default: return Mathf.Abs(c.axial) <= range;
        }
    }

    /// <summary>On the joint's approach side at any distance (Either: always).</summary>
    static bool OnApproachSide(Candidate c)
    {
        switch (c.joint.approach)
        {
            case ApproachSide.Positive: return c.along >= -c.tolerance;
            case ApproachSide.Negative: return -c.along >= -c.tolerance;
            default: return true;
        }
    }

    /// <summary>The length a recipe's corridor, snap and guide distances are fractions of.</summary>
    static float ThresholdLength(AssemblyRecipe recipe, AssemblyUnit mover, AssemblyUnit partner) =>
        recipe.thresholdSize == ThresholdSize.LargerPartLength
            ? Mathf.Max(mover.Length, partner.Length)
            : Mathf.Min(mover.Size, partner.Size);

    /// <summary>Guide line and pass-through for the current candidate; both off when there is none.</summary>
    void ShowCorridor(Candidate c)
    {
        AssemblyUnit passUnit = c != null ? c.mover : null;
        if (_passThroughUnit != null && (_passThroughUnit != passUnit || c == null))
        {
            _passThroughUnit.Pickable.PassThrough.Clear();
            _passThroughUnit = null;
        }
        if (c != null)
        {
            // Only this matching pair may overlap; every other item still blocks as before.
            c.mover.Pickable.PassThrough.Clear();
            foreach (Collider col in c.partner.Pickable.Colliders) c.mover.Pickable.PassThrough.Add(col);
            _passThroughUnit = c.mover;
        }

        if (_guide == null) return;
        _guide.enabled = c != null;
        if (c == null) return;
        _guide.SetPosition(0, c.targetCenter - c.axis * c.guideLength);
        _guide.SetPosition(1, c.targetCenter + c.axis * c.guideLength);
        _guide.widthMultiplier = Mathf.Clamp(c.refSize * 0.012f, 0.01f, 0.12f);
        Color color = AxisColor(c.localAxis);
        color.a = c.angle <= c.mover.Recipe.maxAngleDegrees ? guideAlpha : guideAlpha * 0.4f; // fainter until the rotation fits
        _guideMaterial.SetColor("_Color", color);
    }

    // ---------- Touching parts that do not snap ----------

    /// <summary>
    /// When the handled unit touches another one without being on its way into it: a matching part gets a HUD tip
    /// naming the right axis; a part that does not fit flashes a red outline and the tip names the right partner.
    /// </summary>
    void CheckContacts(AssemblyUnit mover, Candidate best)
    {
        if (Time.time < _nextHintTime || mover.Busy) return;
        foreach (AssemblyUnit other in _units)
        {
            if (other == mover || other.Busy || (best != null && best.partner == other)) continue;
            if (StageRelated(mover, other)) continue; // not a wrong part, just an early one: StageHint explains it
            float near = contactDistance * Mathf.Min(mover.Size, other.Size);
            if (Gap(mover, other, near) > near) continue;

            if (TryJoint(mover, other, out AssemblyRecipe.Joint joint, out AssemblyPiece mine, out AssemblyPiece theirs, out float side))
            {
                Hint("Try " + AxisTip(mover, mine, theirs, joint, side));
            }
            else
            {
                StartCoroutine(FlashWrong(other));
                AssemblySounds.PlayAt(AssemblySounds.Wrong(), other.Pickable.Center, 0.6f);
                string tip = $"{mover.name} does not fit {other.name}";
                AssemblyUnit partner = NearestPartner(mover, out joint, out mine, out theirs, out side);
                Hint(partner != null ? $"{tip}. Try {partner.name} {AxisTip(mover, mine, theirs, joint, side)}" : tip);
            }
            return;
        }
    }

    bool TryJoint(AssemblyUnit mover, AssemblyUnit other, out AssemblyRecipe.Joint joint,
                  out AssemblyPiece mine, out AssemblyPiece theirs, out float side)
    {
        joint = null;
        mine = theirs = null;
        side = 0f;
        if (mover.Recipe == null || other.Recipe != mover.Recipe) return false;
        foreach (AssemblyRecipe.Joint j in mover.Recipe.joints)
        {
            if (!JointSides(mover, other, j, out int a, out int b, out side)) continue;
            joint = j;
            mine = mover.Slots[a][0];
            theirs = other.Slots[b][0];
            return true;
        }
        return false;
    }

    /// <summary>The closest unit the mover can join, for the tip after a wrong contact.</summary>
    AssemblyUnit NearestPartner(AssemblyUnit mover, out AssemblyRecipe.Joint joint,
                                out AssemblyPiece mine, out AssemblyPiece theirs, out float side)
    {
        AssemblyUnit best = null;
        joint = null;
        mine = theirs = null;
        side = 0f;
        float bestDistance = float.MaxValue;
        foreach (AssemblyUnit other in _units)
        {
            if (other == mover || other.Busy) continue;
            if (!TryJoint(mover, other, out var j, out var a, out var b, out float s)) continue;
            float d = Vector3.Distance(mover.Pickable.Center, other.Pickable.Center);
            if (d >= bestDistance) continue;
            bestDistance = d;
            best = other;
            joint = j;
            mine = a;
            theirs = b;
            side = s;
        }
        return best;
    }

    /// <summary>"along the Y axis, from above" in the mover's own axis names (the keys and gizmo arrows).</summary>
    string AxisTip(AssemblyUnit mover, AssemblyPiece mine, AssemblyPiece theirs, AssemblyRecipe.Joint joint, float side)
    {
        Vector3 axis = theirs.FrameRotation * AxisVector(joint.axis);
        string tip = $"along the {LocalAxis(mover.transform, axis)} axis";
        if (joint.approach != ApproachSide.Either)
        {
            // Direction from the target toward where the mover must come from.
            Vector3 from = axis * side * (joint.approach == ApproachSide.Positive ? 1f : -1f);
            float up = Vector3.Dot(from, Vector3.up);
            if (up > 0.7f) tip += ", from above";
            else if (up < -0.7f) tip += ", from below";
        }
        if (Quaternion.Angle(mine.FrameRotation, theirs.FrameRotation) > mover.Recipe.maxAngleDegrees)
            tip += ", turned to match";
        return tip;
    }

    /// <summary>Smallest gap between the two units' colliders (0 when they overlap), or more than limit if clearly apart.</summary>
    static float Gap(AssemblyUnit a, AssemblyUnit b, float limit)
    {
        float reach = a.Pickable.BoundingRadius + b.Pickable.BoundingRadius + limit;
        if ((a.Pickable.Center - b.Pickable.Center).sqrMagnitude > reach * reach) return float.MaxValue;

        float best = float.MaxValue;
        foreach (Collider ca in a.Pickable.Colliders)
        {
            foreach (Collider cb in b.Pickable.Colliders)
            {
                Transform ta = ca.transform, tb = cb.transform;
                if (Physics.ComputePenetration(ca, ta.position, ta.rotation, cb, tb.position, tb.rotation, out _, out _)) return 0f;
                // Two rounds of closest points: close enough for boxes to tell "touching" from "apart".
                Vector3 pb = cb.ClosestPoint(ca.bounds.center);
                Vector3 pa = ca.ClosestPoint(pb);
                pb = cb.ClosestPoint(pa);
                best = Mathf.Min(best, Vector3.Distance(pa, pb));
            }
        }
        return best;
    }

    void Hint(string text)
    {
        if (Time.time < _nextHintTime) return;
        HUD.ShowMessage(text, 2.5f);
        _nextHintTime = Time.time + hintCooldown;
    }

    /// <summary>Brief red inverted-hull outline on every mesh of the unit, outside its hierarchy so its Pickable never sees it.</summary>
    IEnumerator FlashWrong(AssemblyUnit unit)
    {
        if (_wrongMaterial == null) yield break;
        var outlines = new List<GameObject>();
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (MeshFilter mf in unit.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || AssemblyBounds.IsOutline(mf.transform)) continue;
            var o = new GameObject("Wrong Fit Outline");
            o.layer = layer;
            o.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            o.transform.localScale = mf.transform.lossyScale;
            o.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = o.AddComponent<MeshRenderer>();
            var mats = new Material[mf.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = _wrongMaterial;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            outlines.Add(o);
        }
        yield return new WaitForSeconds(wrongFlashSeconds);
        foreach (GameObject o in outlines) Destroy(o);
    }

    // ---------- Magnet ----------

    /// <summary>
    /// Starts a magnet pull when the handled unit is within magnetRadius (of the larger part's length) of a target
    /// pose, on the joint's approach side. Corridor and snap limits do not apply, only magnetMaxAngle: beyond it the
    /// HUD asks the player to rotate. A unit that broke free re-arms once it has left 1.15 x the radius.
    /// </summary>
    bool TryStartPull(AssemblyUnit mover)
    {
        AssemblyRecipe recipe = mover.Recipe;
        float reach = _magnetBlocked == mover ? 1.15f : 1f;
        Candidate nearest = null;
        foreach (AssemblyUnit partner in _units)
        {
            if (partner == mover || partner.Busy || partner.Recipe != recipe) continue;
            foreach (AssemblyRecipe.Joint joint in recipe.joints)
            {
                if (!JointSides(mover, partner, joint, out int mine, out int theirs, out float side)) continue;
                Candidate c = Measure(mover, partner, mover.Slots[mine][0], partner.Slots[theirs][0], joint, side, recipe);
                if (!OnApproachSide(c) || c.distance > recipe.magnetRadius * c.largerLength * reach) continue;
                if (nearest == null || c.distance < nearest.distance) nearest = c;
            }
        }

        if (_magnetBlocked == mover)
        {
            if (nearest == null) _magnetBlocked = null;
            return false;
        }
        if (nearest == null) return false;
        if (nearest.angle > recipe.magnetMaxAngle)
        {
            Hint($"Rotate the {mover.name.ToLowerInvariant()} to match the {nearest.partner.name.ToLowerInvariant()}");
            return false;
        }

        SnapPose(nearest, out Vector3 position, out Quaternion rotation, out Vector3 scale);
        Transform root = mover.transform;
        _pull = new Pull
        {
            c = nearest,
            atStart = nearest.Copy(),
            startPosition = root.position, startRotation = root.rotation, startScale = root.localScale,
            endPosition = position, endRotation = rotation, endScale = scale,
            lastSetPosition = root.position,
        };
        if (recipe.magnetAlignFirst)
        {
            // Line up straight in front of the target on the side the part comes from, as far out along the axis as it
            // is now (a little at least), so the last phase is a pure slide along the axis.
            Vector3 from = ApproachDirection(nearest);
            float back = Mathf.Max(Mathf.Abs(nearest.axial), 0.02f * nearest.largerLength);
            _pull.alignFirst = true;
            _pull.alignPosition = position + from * back;
        }
        nearest.guideLength = recipe.magnetRadius * nearest.largerLength;
        nearest.partner.Busy = true; // the partner holds still and cannot be grabbed while it is the target
        return true;
    }

    /// <summary>
    /// Eases the pulled unit into its snap pose over magnetSeconds, whether or not the player still holds it.
    /// Dragging it away (the hold point while carrying, the drag in axis mode) faster than magnetBreakSpeed breaks
    /// free. When the pull lands, the normal snap ending runs: click, lock together, complete the recipe.
    /// </summary>
    void UpdatePull()
    {
        Pull p = _pull;
        Candidate c = p.c;
        AssemblyUnit mover = c.mover, partner = c.partner;
        if (mover == null || partner == null)
        {
            EndPull(false);
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        AssemblyRecipe recipe = mover.Recipe;
        Transform root = mover.transform;
        HighlightManager selection = HighlightManager.Instance;
        bool selected = selection != null && selection.Selected == mover.Pickable;

        // How the player is moving the unit this frame, if at all.
        Vector3 intent = Vector3.zero;
        bool hasIntent = false;
        if (interactor != null && interactor.Held == mover.Pickable)
        {
            Vector3 hold = HoldPoint();
            if (p.hasHoldPoint)
            {
                intent = (hold - p.lastHoldPoint) / dt;
                hasIntent = true;
            }
            p.lastHoldPoint = hold;
            p.hasHoldPoint = true;
        }
        else
        {
            p.hasHoldPoint = false;
            if (selected && interactor != null && interactor.HandState == HandState.AxisMoving)
            {
                intent = (root.position - p.lastSetPosition) / dt;
                hasIntent = true;
            }
        }
        Vector3 toward = c.targetCenter - c.moverPiece.Center;
        if (hasIntent && intent.magnitude > recipe.magnetBreakSpeed && Vector3.Dot(intent, toward) <= 0f)
        {
            _magnetBlocked = mover;
            EndPull(false);
            return;
        }

        p.time += dt;
        float seconds = recipe.magnetSeconds;
        if (p.alignFirst)
        {
            // Phase 1 (60%): to the line-up pose, turning to the final rotation. Phase 2: straight in along the axis.
            float split = seconds * 0.6f;
            if (p.time < split)
            {
                float k = Mathf.SmoothStep(0f, 1f, p.time / split);
                root.SetPositionAndRotation(Vector3.Lerp(p.startPosition, p.alignPosition, k), Quaternion.Slerp(p.startRotation, p.endRotation, k));
                root.localScale = Vector3.Lerp(p.startScale, p.endScale, k);
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p.time - split) / (seconds - split)));
                root.SetPositionAndRotation(Vector3.Lerp(p.alignPosition, p.endPosition, k), p.endRotation);
                root.localScale = p.endScale;
            }
        }
        else
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p.time / seconds));
            root.SetPositionAndRotation(Vector3.Lerp(p.startPosition, p.endPosition, k), Quaternion.Slerp(p.startRotation, p.endRotation, k));
            root.localScale = Vector3.Lerp(p.startScale, p.endScale, k);
        }
        p.lastSetPosition = root.position;
        c.angle = Quaternion.Angle(c.moverPiece.FrameRotation, c.partnerPiece.FrameRotation);
        c.localAxis = LocalAxis(root, c.axis);
        ShowCorridor(c); // guide line along the insertion axis, and pass-through with the partner

        if (p.time < recipe.magnetSeconds) return;
        LogSnap(p.atStart, "magnet"); // a pull the player broke is not a snap, so it is logged only now
        EndPull(true);
        mover.Busy = true;
        if (selected) selection.Select(null); // ends the grab
        StartCoroutine(FinishSnap(mover, partner, c.targetCenter, c.partnerPiece.FrameRotation));
    }

    /// <summary>
    /// Unit direction from the target toward the side the mover comes from: the joint's approach side (flipped when the
    /// mover holds input b), or for Either the side it is on now.
    /// </summary>
    static Vector3 ApproachDirection(Candidate c)
    {
        switch (c.joint.approach)
        {
            case ApproachSide.Positive: return c.axis * c.side;
            case ApproachSide.Negative: return -c.axis * c.side;
            default: return c.axis * (c.axial >= 0f ? 1f : -1f);
        }
    }

    void EndPull(bool keepPartnerBusy)
    {
        if (_pull == null) return;
        if (!keepPartnerBusy && _pull.c.partner != null) _pull.c.partner.Busy = false;
        _pull = null;
        ShowCorridor(null);
    }

    /// <summary>Where PlayerInteractor carries the held item: in front of the camera at the hold distance and offset.</summary>
    Vector3 HoldPoint()
    {
        Transform cam = interactor.playerCamera.transform;
        return cam.position + cam.forward * interactor.holdDistance
             + cam.right * interactor.holdOffset.x + cam.up * interactor.holdOffset.y;
    }

    // ---------- Unfinished stages ----------

    /// <summary>
    /// Holding a part next to one it cannot join yet because a stage in between is unfinished (e.g. the dragon over
    /// the deck while the pillar row is missing): the tip names the recipe to finish first and what it still needs.
    /// </summary>
    void StageHint(AssemblyUnit mover)
    {
        if (Time.time < _nextHintTime) return;
        foreach (AssemblyUnit other in _units)
        {
            if (other == mover || other.Busy || !StageRelated(mover, other)) continue;
            float near = stageHintDistance * Mathf.Max(mover.Length, other.Length);
            if (Gap(mover, other, near) > near) continue;

            AssemblyUnit early = Feeds(other.Recipe, mover.Recipe) ? other : mover;
            var missing = new List<string>();
            for (int i = 0; i < early.Recipe.inputs.Count; i++)
                if (!early.Slots.ContainsKey(i)) missing.Add(early.Recipe.inputs[i].Label);
            Hint($"Finish the {early.Recipe.DisplayName} first: it still needs the {string.Join(", ", missing)}");
            return;
        }
    }

    /// <summary>True when one unit's recipe leads, through later recipes, into the other's (an earlier stage).</summary>
    bool StageRelated(AssemblyUnit a, AssemblyUnit b) =>
        a.Recipe != null && b.Recipe != null && a.Recipe != b.Recipe && (Feeds(a.Recipe, b.Recipe) || Feeds(b.Recipe, a.Recipe));

    /// <summary>True when the result of from becomes, directly or further up the tree, an input of to.</summary>
    bool Feeds(AssemblyRecipe from, AssemblyRecipe to)
    {
        AssemblyRecipe r = from;
        for (int depth = 0; r != null && depth < 16; depth++)
        {
            r = FindParent(r).Item1;
            if (r == to) return true;
        }
        return false;
    }

    // ---------- Research log ----------

    /// <summary>
    /// Counts handling attempts per unit: a new attempt starts whenever the player starts grabbing, axis-moving or
    /// rotating a unit. Also notes whether the attempt used axis mode (an X / Y / Z key with a drag).
    /// </summary>
    void TrackAttempt(AssemblyUnit handled)
    {
        if (handled != _handledUnit)
        {
            _handledUnit = handled;
            if (handled != null)
            {
                handled.Attempts++;
                if (handled.FirstHandledTime < 0f) handled.FirstHandledTime = Time.time;
                handled.UsedAxisMode = false;
            }
        }
        if (handled != null && interactor.ActiveAxis != Axis.None &&
            (interactor.HandState == HandState.AxisMoving || interactor.HandState == HandState.Rotating))
            handled.UsedAxisMode = true;
    }

    /// <summary>One CSV row for a snap, measured at the moment it triggers (before the slide corrects the pose).</summary>
    /// <summary>
    /// One CSV row for a snap, measured at the moment it triggers (before the slide corrects the pose).
    /// assist is "none" for a plain snap and "magnet" when the magnet pull took over.
    /// </summary>
    void LogSnap(Candidate c, string assist)
    {
        if (_log == null) return;
        AssemblyRecipe recipe = c.mover.Recipe;
        float corridor = recipe.corridorRadius * c.refSize;
        _log.Write(new AssemblyLog.SnapRow
        {
            recipe = recipe.DisplayName,
            movingPart = c.mover.name,
            partner = c.partner.name,
            insertionAxis = c.joint.axis,
            sinceFirstGrab = c.mover.FirstHandledTime >= 0f ? Time.time - c.mover.FirstHandledTime : -1f,
            attempts = c.mover.Attempts,
            lateral = c.lateral,
            lateralOfCorridor = corridor > 1e-6f ? c.lateral / corridor : 0f,
            axial = Mathf.Abs(c.axial),
            angleError = c.angle,
            axisModeUsed = c.mover.UsedAxisMode,
            axisKey = interactor != null ? interactor.ActiveAxis : Axis.None,
            recipeCompleted = c.mover.Slots.Count + c.partner.Slots.Count == recipe.inputs.Count,
            assist = assist,
        });
    }

    // ---------- Snap ----------

    IEnumerator Snap(Candidate c)
    {
        AssemblyUnit mover = c.mover, partner = c.partner;
        mover.Busy = partner.Busy = true;
        LogSnap(c, "none");
        ShowCorridor(null);
        HighlightManager selection = HighlightManager.Instance;
        if (selection != null && selection.Selected == mover.Pickable) selection.Select(null); // ends the grab

        Quaternion frame = c.partnerPiece.FrameRotation;
        SnapPose(c, out Vector3 endPosition, out Quaternion endRotation, out Vector3 endScale);
        Transform root = mover.transform;
        Vector3 startPosition = root.position;
        Quaternion startRotation = root.rotation;
        Vector3 startScale = root.localScale;
        for (float t = 0f; t < snapSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / snapSeconds);
            root.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, k), Quaternion.Slerp(startRotation, endRotation, k));
            root.localScale = Vector3.Lerp(startScale, endScale, k);
            yield return null;
        }
        root.SetPositionAndRotation(endPosition, endRotation);
        root.localScale = endScale;
        yield return FinishSnap(mover, partner, c.targetCenter, frame);
    }

    /// <summary>
    /// Root pose that puts the mover piece exactly at its template pose relative to the partner piece, at the
    /// partner's size (a part the player resized is brought back to scale).
    /// </summary>
    static void SnapPose(Candidate c, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        AssemblyPiece mine = c.moverPiece, theirs = c.partnerPiece;
        Transform root = c.mover.transform;
        Quaternion pieceRotation = theirs.FrameRotation * mine.Node.rotation;
        rotation = pieceRotation * Quaternion.Inverse(mine.transform.rotation) * root.rotation;
        scale = root.localScale * (theirs.Scale / Mathf.Max(1e-6f, mine.Scale));
        Vector3 localCenter = root.InverseTransformPoint(mine.Center);
        position = c.targetCenter - rotation * Vector3.Scale(localCenter, scale);
    }

    /// <summary>The mover is in place: click, lock the two units together, and complete the recipe if it is full.</summary>
    IEnumerator FinishSnap(AssemblyUnit mover, AssemblyUnit partner, Vector3 at, Quaternion frame)
    {
        AssemblySounds.PlayAt(AssemblySounds.Click(), at);
        AssemblyUnit merged = Merge(mover, partner, frame);
        if (merged.IsComplete)
        {
            yield return Complete(merged);
        }
        else if (selectAfterSnap && HighlightManager.Instance != null)
        {
            HighlightManager.Instance.Select(merged.Pickable);
        }
    }

    // ---------- Completion ----------

    /// <summary>
    /// Turns a full unit into its recipe's result (a model, or the locked group itself) and hands it to the next recipe
    /// up the tree. instant skips every effect, sound, pulse and HUD message, so the coroutine finishes within the
    /// StartCoroutine call (used for groups that start the scene already assembled).
    /// </summary>
    IEnumerator Complete(AssemblyUnit unit, bool instant = false)
    {
        unit.Busy = true;
        AssemblyRecipe recipe = unit.Recipe;
        (AssemblyRecipe parent, int parentSlot) = FindParent(recipe);
        var record = new Completion { recipe = recipe, inputs = new List<(int, List<AssemblyPiece>)>() };
        foreach (KeyValuePair<int, List<AssemblyPiece>> s in unit.Slots) record.inputs.Add((s.Key, new List<AssemblyPiece>(s.Value)));

        AssemblyPiece anchor = unit.Anchor;
        Quaternion frame = anchor.FrameRotation;
        float scale = unit.AverageScale;
        Vector3 origin = anchor.Center - frame * (anchor.Node.center * scale);
        Vector3 center = unit.Pickable.Center;

        AssemblyTemplate.Node resultNode = null;
        if (!string.IsNullOrEmpty(recipe.resultTemplateNode))
        {
            resultNode = Template.Find(recipe.resultTemplateNode);
            if (resultNode == null)
                Debug.LogWarning($"AssemblyManager: {recipe.DisplayName}: result node \"{recipe.resultTemplateNode}\" is not in the template; " +
                                 "the locked group is kept as the result.", recipe);
        }

        AssemblyUnit result;
        if (resultNode == null)
        {
            // No model of its own (e.g. the pillar row): the locked group is the result.
            if (!instant)
            {
                yield return recipe.effect == AssemblyEffect.GlowSwap ? Glow(unit, true) : Pulse(unit.transform, 1.06f);
                AssemblySounds.PlayAt(AssemblySounds.Pop(), center);
            }
            var pieces = new List<AssemblyPiece>(unit.Pieces);
            unit.Slots.Clear();
            unit.AddSlot(parent != null ? parentSlot : 0, pieces);
            unit.Recipe = parent;
            unit.name = recipe.DisplayName;
            result = unit;
            record.resultPiece = anchor;
        }
        else
        {
            if (!instant)
            {
                yield return recipe.effect == AssemblyEffect.GlowSwap ? Glow(unit, false) : FlyTogether(unit, center);
                AssemblySounds.PlayAt(AssemblySounds.Pop(), center);
            }
            AssemblyPiece model = SpawnResult(resultNode, frame, origin, scale);
            result = CreateUnit(recipe.DisplayName, parent, frame,
                                new[] { (parent != null ? parentSlot : 0, new List<AssemblyPiece> { model }) }, unit.Pickable);
            record.resultPiece = model;

            // The pieces are kept, hidden, so the debug split can bring them back.
            foreach (AssemblyPiece p in new List<AssemblyPiece>(unit.Pieces))
            {
                RemoveOutlines(p.transform);
                p.transform.SetParent(_stash, true);
                p.Unit = null;
            }
            RemoveUnit(unit);
            if (!instant)
            {
                result.Busy = true;
                yield return Pulse(result.transform, 1.08f);
            }
        }

        _completed.Add(record);
        result.Busy = false;
        string message = !string.IsNullOrEmpty(recipe.completionMessage) ? recipe.completionMessage : $"{recipe.DisplayName} assembled";
        if (!instant) HUD.ShowMessage(message, parent == null ? 6f : 3f);
        Debug.Log($"AssemblyManager: {recipe.DisplayName} complete" + (parent != null ? $", now an input of {parent.DisplayName}." : "."), this);
    }

    /// <summary>The recipe that takes this recipe's result as an input, and that input's index.</summary>
    (AssemblyRecipe, int) FindParent(AssemblyRecipe recipe)
    {
        foreach (AssemblyRecipe r in recipes)
        {
            if (r == null) continue;
            for (int i = 0; i < r.inputs.Count; i++)
                if (r.inputs[i].fromRecipe == recipe) return (r, i);
        }
        return (null, -1);
    }

    /// <summary>Copy of a template node at the pose and size the template frame (frame, origin, scale) gives it.</summary>
    AssemblyPiece SpawnResult(AssemblyTemplate.Node node, Quaternion frame, Vector3 origin, float scale)
    {
        GameObject go = Instantiate(node.source.gameObject);
        go.name = node.name;
        StripToRenderers(go);
        go.SetActive(true);
        go.transform.rotation = frame * node.rotation;
        go.transform.localScale = node.scale * scale;
        var piece = go.AddComponent<AssemblyPiece>();
        piece.Init(node);
        go.transform.position += origin + frame * (node.center * scale) - piece.Center;
        piece.EnsureCollider();
        return piece;
    }

    /// <summary>Puts a piece (at the scene root) at its template pose and size in the given template frame.</summary>
    static void PlacePiece(AssemblyPiece piece, Quaternion frame, Vector3 origin, float scale)
    {
        Transform t = piece.transform;
        t.rotation = frame * piece.Node.rotation;
        float current = piece.Scale;
        if (current > 1e-6f) t.localScale *= scale / current;
        t.position += origin + frame * (piece.Node.center * scale) - piece.Center;
    }

    /// <summary>FlyToCenter: every piece moves toward the group centre while shrinking.</summary>
    IEnumerator FlyTogether(AssemblyUnit unit, Vector3 center)
    {
        var pieces = new List<AssemblyPiece>(unit.Pieces);
        var startCenters = new List<Vector3>();
        var startScales = new List<Vector3>();
        foreach (AssemblyPiece p in pieces)
        {
            startCenters.Add(p.Center);
            startScales.Add(p.transform.localScale);
        }
        for (float t = 0f; t < effectSeconds; t += Time.deltaTime)
        {
            float k = t / effectSeconds;
            k *= k; // ease in: slow start, fast finish into the pop
            for (int i = 0; i < pieces.Count; i++)
            {
                Transform pt = pieces[i].transform;
                pt.localScale = startScales[i] * Mathf.Lerp(1f, 0.35f, k);
                pt.position += Vector3.Lerp(startCenters[i], center, k * 0.9f) - pieces[i].Center;
            }
            yield return null;
        }
    }

    /// <summary>GlowSwap: the pieces turn into glowing ghosts that pulse. restore puts their materials back.</summary>
    IEnumerator Glow(AssemblyUnit unit, bool restore)
    {
        var renderers = new List<Renderer>(unit.GetComponentsInChildren<Renderer>());
        var originals = new List<Material[]>();
        foreach (Renderer r in renderers)
        {
            originals.Add(r.sharedMaterials);
            if (_glowMaterial == null) continue;
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = _glowMaterial;
            r.sharedMaterials = mats;
        }
        yield return Pulse(unit.transform, 1.05f, effectSeconds);
        if (!restore) yield break;
        for (int i = 0; i < renderers.Count; i++)
            if (renderers[i] != null) renderers[i].sharedMaterials = originals[i];
    }

    /// <summary>Scale bump of a unit root around its position (the unit's visual centre).</summary>
    IEnumerator Pulse(Transform t, float peak, float seconds = -1f)
    {
        if (seconds <= 0f) seconds = popSeconds;
        Vector3 baseScale = t.localScale;
        for (float time = 0f; time < seconds; time += Time.deltaTime)
        {
            float k = Mathf.Sin(Mathf.PI * time / seconds);
            if (t == null) yield break;
            t.localScale = baseScale * Mathf.Lerp(1f, peak, k);
            yield return null;
        }
        if (t != null) t.localScale = baseScale;
    }

    // ---------- Debug split ----------

    bool SplitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && splitKey != Key.None && k[splitKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(splitKey);
#endif
    }

    /// <summary>
    /// Takes the most recent completed assembly apart into its recipe's inputs (sub-assemblies stay whole), scattered
    /// around where it stood. Pressing again walks further back. Public for tests and editor tools.
    /// </summary>
    public void SplitLast()
    {
        for (int i = _completed.Count - 1; i >= 0; i--)
        {
            Completion record = _completed[i];
            AssemblyUnit unit = record.resultPiece != null ? record.resultPiece.Unit : null;
            if (unit == null)
            {
                _completed.RemoveAt(i); // its result is gone or hidden inside a later result that was not split
                continue;
            }
            if (unit.Busy) return;
            if (unit.Slots.Count > 1)
            {
                // The result already snapped into a partial group of the next recipe: undo that first.
                BreakIntoSlots(unit);
                unit = record.resultPiece.Unit;
            }
            _completed.RemoveAt(i);
            Split(record, unit);
            return;
        }
        HUD.ShowMessage("Nothing to split", 2f);
    }

    void Split(Completion record, AssemblyUnit result)
    {
        AssemblyPiece anchor = result.Anchor;
        Quaternion frame = anchor.FrameRotation;
        float scale = anchor.Scale;
        Vector3 origin = anchor.FrameOrigin;
        HighlightManager selection = HighlightManager.Instance;
        if (selection != null && selection.Selected == result.Pickable) selection.Select(null);

        var created = new List<AssemblyUnit>();
        foreach ((int slot, List<AssemblyPiece> pieces) in record.inputs)
        {
            foreach (AssemblyPiece p in pieces)
            {
                if (p.transform.parent != _stash) continue;
                p.transform.SetParent(null, true);
                PlacePiece(p, frame, origin, scale);
            }
            created.Add(CreateUnit(SlotName(record.recipe, slot, pieces), record.recipe, frame,
                                   new[] { (slot, pieces) }, result.Pickable));
        }
        RemoveUnit(result); // the result model goes with it; a locked group is empty by now
        Scatter(created, frame);
        AssemblySounds.PlayAt(AssemblySounds.Pop(), origin + frame * Vector3.zero, 0.5f);
        HUD.ShowMessage($"Split {record.recipe.DisplayName}", 2f);
        Debug.Log($"AssemblyManager: split {record.recipe.DisplayName} into {created.Count} parts.", this);
    }

    /// <summary>Turns a partial group back into one unit per filled slot, in place.</summary>
    void BreakIntoSlots(AssemblyUnit unit)
    {
        Quaternion frame = unit.transform.rotation;
        foreach (KeyValuePair<int, List<AssemblyPiece>> s in new Dictionary<int, List<AssemblyPiece>>(unit.Slots))
        {
            var pieces = new List<AssemblyPiece>(s.Value);
            CreateUnit(SlotName(unit.Recipe, s.Key, pieces), unit.Recipe, frame, new[] { (s.Key, pieces) }, unit.Pickable);
        }
        RemoveUnit(unit);
    }

    /// <summary>
    /// Pushes units apart horizontally from their common centre (splitSpread). Units stacked on top of each other
    /// (bridge on pillars, dragon on bridge) are pushed to alternate sides of the template's Z axis instead.
    /// </summary>
    void Scatter(List<AssemblyUnit> units, Quaternion frame)
    {
        Vector3 center = Vector3.zero;
        foreach (AssemblyUnit u in units) center += u.Pickable.Center;
        center /= Mathf.Max(1, units.Count);
        Vector3 sideways = Vector3.ProjectOnPlane(frame * Vector3.forward, Vector3.up).normalized;
        for (int i = 0; i < units.Count; i++)
        {
            AssemblyUnit u = units[i];
            Vector3 away = Vector3.ProjectOnPlane(u.Pickable.Center - center, Vector3.up);
            Vector3 move = away * (splitSpread - 1f);
            if (move.magnitude < u.Size * 0.15f)
                move = sideways * (u.Size * 0.35f) * (i % 2 == 0 ? 1f : -1f);
            u.Pickable.TryTranslate(move, ObstacleMask); // stops at walls and furniture, like a carried item
        }
    }

    // ---------- Ghosts ----------

    /// <summary>
    /// Each recipe's result as a ghost at the place the recipe's largest group says it belongs
    /// (AfterFirstSnap: only once a group of two or more exists; Always: from the first loose piece on).
    /// </summary>
    void UpdateRecipeGhosts()
    {
        foreach (AssemblyRecipe recipe in recipes)
        {
            if (recipe == null) continue;
            AssemblyUnit anchor = null;
            if (ghostMode != GhostMode.Hidden && _hintMaterial != null && _pull != null && _pull.c.partner.Recipe == recipe)
            {
                anchor = _pull.c.partner; // the magnet shows where the result will stand
            }
            else if (ghostMode != GhostMode.Hidden && _hintMaterial != null)
            {
                foreach (AssemblyUnit u in _units)
                {
                    if (u.Recipe != recipe || u.Busy) continue;
                    if (ghostMode == GhostMode.AfterFirstSnap && u.Slots.Count < 2) continue;
                    if (anchor == null || u.Slots.Count > anchor.Slots.Count) anchor = u;
                }
            }

            _recipeGhosts.TryGetValue(recipe, out GameObject ghost);
            if (anchor == null)
            {
                if (ghost != null && ghost.activeSelf) ghost.SetActive(false);
                continue;
            }
            if (ghost == null) _recipeGhosts[recipe] = ghost = BuildRecipeGhost(recipe);
            if (!ghost.activeSelf) ghost.SetActive(true);

            // The ghost root is a copy of the template root: placing it in the anchor's template frame puts every node right.
            AssemblyPiece p = anchor.Anchor;
            ghost.transform.SetPositionAndRotation(p.FrameOrigin, p.FrameRotation);
            ghost.transform.localScale = Vector3.one * p.Scale;
        }
    }

    GameObject BuildRecipeGhost(AssemblyRecipe recipe)
    {
        var root = new GameObject($"Assembly Ghost: {recipe.DisplayName}");
        var nodes = new List<AssemblyTemplate.Node>();
        CollectResultNodes(recipe, nodes, 0);
        foreach (AssemblyTemplate.Node node in nodes)
        {
            GameObject g = MakeGhost(node.source.gameObject, _hintMaterial);
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = node.position;
            g.transform.localRotation = node.rotation;
            g.transform.localScale = node.scale;
        }
        return root;
    }

    /// <summary>The template nodes that make up a recipe's result: its model, or else all its inputs' results.</summary>
    void CollectResultNodes(AssemblyRecipe recipe, List<AssemblyTemplate.Node> nodes, int depth)
    {
        if (recipe == null || depth > 16) return; // guards against a recipe loop
        AssemblyTemplate.Node model = Template.Find(recipe.resultTemplateNode);
        if (model != null)
        {
            nodes.Add(model);
            return;
        }
        foreach (AssemblyRecipe.Input input in recipe.inputs)
        {
            if (input.fromRecipe != null) CollectResultNodes(input.fromRecipe, nodes, depth + 1);
            else if (Template.Find(input.templateNode) is AssemblyTemplate.Node node) nodes.Add(node);
        }
    }

    /// <summary>See-through copy of source: only transforms and meshes, ghost material, no shadows or colliders.</summary>
    GameObject MakeGhost(GameObject source, Material material)
    {
        GameObject ghost = Instantiate(source);
        ghost.name = source.name + " (ghost)";
        StripToRenderers(ghost);
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        foreach (Transform t in ghost.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        foreach (MeshRenderer r in ghost.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        ghost.SetActive(true);
        return ghost;
    }

    /// <summary>Removes everything but transforms, mesh filters and renderers (colliders, scripts, assembly data).</summary>
    static void StripToRenderers(GameObject go)
    {
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
            if (!(c is Transform) && !(c is MeshFilter) && !(c is Renderer)) DestroyImmediate(c);
    }

    // ---------- Helpers ----------

    /// <summary>Walls, floor and furniture (the player's obstacles), without the player and the items.</summary>
    LayerMask ObstacleMask =>
        (interactor != null && interactor.player != null ? interactor.player.obstacleMask : (LayerMask)Physics.DefaultRaycastLayers)
        & ~LayerMask.GetMask("Player", "Pickable");

    static Vector3 AxisVector(Axis axis) =>
        axis == Axis.X ? Vector3.right : axis == Axis.Y ? Vector3.up : Vector3.forward;

    /// <summary>The local axis of t most nearly parallel to a world direction.</summary>
    static Axis LocalAxis(Transform t, Vector3 direction)
    {
        float x = Mathf.Abs(Vector3.Dot(t.right, direction));
        float y = Mathf.Abs(Vector3.Dot(t.up, direction));
        float z = Mathf.Abs(Vector3.Dot(t.forward, direction));
        return x >= y && x >= z ? Axis.X : y >= z ? Axis.Y : Axis.Z;
    }

    Color AxisColor(Axis axis)
    {
        if (interactor == null) return axis == Axis.X ? Color.red : axis == Axis.Y ? Color.blue : Color.green;
        return axis == Axis.X ? interactor.xAxisColor : axis == Axis.Y ? interactor.yAxisColor : interactor.zAxisColor;
    }
}
