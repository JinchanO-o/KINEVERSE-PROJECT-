using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Screen UI and post-processing: centre crosshair, context prompt, pause menu (Esc),
/// vignette, and subtle depth of field that focuses on the targeted item.
/// Builds its own Canvas, EventSystem and Volume at runtime. Attach to a "HUD" object.
/// </summary>
public class HUD : MonoBehaviour
{
    /// <summary>True while the pause menu is open. Other scripts check this to stop input and time.</summary>
    public static bool IsPaused { get; private set; }

    [Tooltip("Interactor that supplies the target and held item. Auto-found if empty.")]
    public PlayerInteractor interactor;
    [Tooltip("Camera used for focus distance. Taken from the interactor if empty.")]
    public Camera playerCamera;

    [Header("Post-processing")]
    [Tooltip("Vignette and depth of field. Requires the URP asset to be active.")] public bool enablePostProcessing = true;
    [Tooltip("Vignette strength, 0 = off.")] [Range(0f, 1f)] public float vignetteIntensity = 0.35f;
    [Tooltip("Depth-of-field f-stop. Higher = subtler blur.")] public float apertureFStop = 5.6f;
    [Tooltip("Focus distance (metres) when nothing is targeted.")] public float defaultFocusDistance = 4f;
    [Tooltip("How quickly focus follows the target. Higher = faster.")] public float focusSpeed = 6f;

    // Short message shown above the prompt (assembly hints and completion), see ShowMessage.
    static string s_message;
    static float s_messageUntil;

    Text _prompt;
    GameObject _pauseMenu;
    GameObject _controls;
    DepthOfField _dof;
    float _focus;

    // The controls panel's two columns. A row with a null action is a section heading, coloured like the
    // matching mode on the Lab Controls keymap page; ("", "") is a blank line.
    static readonly (string keys, string action)[][] ControlColumns =
    {
        new (string, string)[]
        {
            ("<color=#c3c7cf>Walk</color>", null),
            ("W A S D", "walk (S resizes when selected)"),
            ("Shift + W", "sprint"),
            ("Ctrl", "crouch while held"),
            ("Mouse", "look around"),
            ("", ""),
            ("<color=#ff1493>Select and carry</color>", null),
            ("Right-click", "select what the crosshair is on"),
            ("Right-click", "deselect: again, or on empty space"),
            ("Hold LMB", "carry the selected object"),
            ("Scroll wheel", "carry distance, 0.8 to 6 m"),
            ("", ""),
            ("<color=#ff6a13>Free rotate</color>", null),
            ("Hold R, then RMB", "tumble the object with the mouse"),
        },
        new (string, string)[]
        {
            ("<color=#f2c200>Axis tools</color>", null),
            ("", "gizmo: <color=#ff4d52>X red</color>, <color=#5b86ff>Y blue</color>, <color=#2ecc71>Z green</color>"),
            ("Hold X/Y/Z + LMB", "drag to move along that axis"),
            ("Hold X/Y/Z + RMB", "drag to rotate around it"),
            ("", ""),
            ("<color=#19c3c6>Resize</color>", null),
            ("Hold S, + or −", "resize 5% per press, hold to repeat"),
            ("Hold S + X/Y/Z, + or −", "resize along one axis"),
            ("", ""),
            ("<color=#8fd3ff>Assemble</color>", null),
            ("Carry or X/Y/Z drag", "matching parts snap along the guide line"),
            ("F9", "split the last assembly (testing)"),
            ("", ""),
            ("<color=#8d929b>Menu</color>", null),
            ("Esc", "pause or resume"),
            ("F1", "show or hide this list"),
        },
    };

    void Awake()
    {
        // Reset static state in case the scene was reloaded while paused.
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        s_message = null;

        if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
        if (playerCamera == null && interactor != null) playerCamera = interactor.playerCamera;
        if (playerCamera == null) playerCamera = Camera.main;

        BuildEventSystem();
        BuildCanvas();
        BuildPostProcessing();

        if (playerCamera != null)
        {
            var urp = playerCamera.GetComponent<UniversalAdditionalCameraData>();
            if (urp != null) urp.renderPostProcessing = true;
        }

        _focus = defaultFocusDistance;
        SetPaused(false);
    }

    void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Update()
    {
        if (InputBridge.EscapePressed()) SetPaused(!IsPaused);
        if (InputBridge.ControlsPressed()) ToggleControls(); // works while paused too
        if (IsPaused || interactor == null) return;

        RefreshPrompt();
        RefreshFocus();
    }

    // ---------- Pause ----------

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        if (_pauseMenu != null) _pauseMenu.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;           // silences ambience and footsteps
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    void ToggleControls()
    {
        if (_controls != null) _controls.SetActive(!_controls.activeSelf);
    }

    // ---------- Prompt and focus ----------

    void RefreshPrompt()
    {
        Pickable selected = HighlightManager.Instance != null ? HighlightManager.Instance.Selected : null;
        string axis = interactor.ActiveAxis.ToString();
        if (interactor.Held != null)
            _prompt.text = $"Scroll: hold distance {interactor.holdDistance:0.0} m\nRelease LMB to drop, RMB to deselect";
        else if (interactor.IsScaling && interactor.ActiveAxis == Axis.None)
            _prompt.text = "Hold S: press +/- to resize\nAdd X, Y or Z to resize along one axis";
        else if (interactor.IsScaling)
            _prompt.text = $"Hold S + {axis}: press +/- to resize along {axis}";
        else if (selected != null && interactor.ActiveAxis != Axis.None)
            _prompt.text = $"Hold {axis}: LMB drag to move, RMB drag to rotate";
        else if (interactor.IsRotating)
            _prompt.text = "Move the mouse to rotate\nRelease R or RMB to stop";
        else if (interactor.RotateArmed)
            _prompt.text = "Hold RMB and move the mouse to rotate";
        else if (interactor.Target != null && interactor.Target == selected)
            _prompt.text = "Hold LMB to move, R + RMB to rotate\nHold X/Y/Z for axis tools, S to resize, RMB to deselect";
        else if (interactor.Target != null)
            _prompt.text = "RMB to select, hold LMB to move";
        else if (selected != null)
            _prompt.text = "Hold X/Y/Z for axis tools, S to resize, R + RMB to rotate\nRMB on empty space to deselect";
        else
            _prompt.text = "";

        if (s_message != null && Time.unscaledTime < s_messageUntil)
            _prompt.text = $"<color=#f2c200>{s_message}</color>" + (_prompt.text.Length > 0 ? "\n" + _prompt.text : "");
    }

    /// <summary>Shows a short message above the context prompt for a few seconds (assembly hints, completion).</summary>
    public static void ShowMessage(string text, float seconds = 3f)
    {
        s_message = text;
        s_messageUntil = Time.unscaledTime + seconds;
    }

    void RefreshFocus()
    {
        if (_dof == null || playerCamera == null) return;

        float target = defaultFocusDistance;
        if (interactor.Target != null)
            target = Vector3.Distance(playerCamera.transform.position, interactor.Target.Center);

        _focus = Mathf.Lerp(_focus, target, 1f - Mathf.Exp(-focusSpeed * Time.deltaTime));
        _dof.focusDistance.value = _focus;
    }

    // ---------- Construction ----------

    void BuildEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void BuildCanvas()
    {
        var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        // Crosshair: four small bars around the exact screen centre.
        RectTransform crosshair = MakeRect("Crosshair", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        MakeBar(crosshair, new Vector2(-9f, 0f), new Vector2(8f, 2f));
        MakeBar(crosshair, new Vector2(9f, 0f), new Vector2(8f, 2f));
        MakeBar(crosshair, new Vector2(0f, 9f), new Vector2(2f, 8f));
        MakeBar(crosshair, new Vector2(0f, -9f), new Vector2(2f, 8f));

        // Context prompt near the bottom of the screen.
        _prompt = MakeText(root, "Prompt", new Vector2(0.5f, 0.12f), new Vector2(900f, 120f), Vector2.zero, 26, TextAnchor.UpperCenter);

        // Reminder in the bottom-left corner.
        Text hint = MakeText(root, "Controls Hint", new Vector2(0f, 0f), new Vector2(300f, 40f), new Vector2(24f, 20f), 20, TextAnchor.LowerLeft);
        hint.text = "<b>F1</b>  Controls";
        hint.color = new Color(1f, 1f, 1f, 0.6f);

        // Pause menu: full-screen dim panel with Resume, Controls and Quit.
        var panel = new GameObject("Pause Menu", typeof(RectTransform), typeof(Image));
        var pr = (RectTransform)panel.transform;
        pr.SetParent(root, false);
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero;
        pr.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        _pauseMenu = panel;

        MakeText(pr, "Title", new Vector2(0.5f, 0.5f), new Vector2(600f, 80f), new Vector2(0f, 140f), 48, TextAnchor.MiddleCenter).text = "PAUSED";
        MakeButton(pr, "Resume", new Vector2(0f, 40f), () => SetPaused(false));
        MakeButton(pr, "Controls", new Vector2(0f, -40f), ToggleControls);
        MakeButton(pr, "Quit", new Vector2(0f, -120f), () => Application.Quit());

        // Built last so it draws over the pause menu.
        BuildControls(root);
    }

    /// <summary>Centred list of every key, two columns of key and action, hidden until F1 or the Controls button.</summary>
    void BuildControls(Transform root)
    {
        var panel = new GameObject("Controls", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)panel.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1420f, 660f);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.08f, 0.94f);
        _controls = panel;

        var topLeft = new Vector2(0f, 1f);
        MakeText(rt, "Title", topLeft, new Vector2(600f, 60f), new Vector2(48f, -32f), 40, TextAnchor.UpperLeft).text = "<b>Controls</b>";
        MakeText(rt, "Close Hint", new Vector2(1f, 1f), new Vector2(400f, 40f), new Vector2(-48f, -44f), 22, TextAnchor.UpperRight).text = "F1 to close";

        // Keys and actions are separate Text objects per column; same font size on every line keeps rows aligned.
        for (int c = 0; c < ControlColumns.Length; c++)
        {
            var keys = new System.Text.StringBuilder();
            var actions = new System.Text.StringBuilder();
            foreach (var (key, action) in ControlColumns[c])
            {
                keys.Append("<b>").Append(key).Append("</b>\n");
                actions.Append(action ?? "").Append('\n');
            }
            float x = 48f + c * 690f;
            Text k = MakeText(rt, "Keys " + (c + 1), topLeft, new Vector2(270f, 440f), new Vector2(x, -118f), 21, TextAnchor.UpperLeft);
            Text a = MakeText(rt, "Actions " + (c + 1), topLeft, new Vector2(370f, 440f), new Vector2(x + 275f, -118f), 21, TextAnchor.UpperLeft);
            k.text = keys.ToString();
            a.text = actions.ToString();
            k.horizontalOverflow = a.horizontalOverflow = HorizontalWrapMode.Overflow; // never wrap, so lines stay paired
        }

        MakeButton(rt, "Close", new Vector2(0f, -280f), ToggleControls);
        panel.SetActive(false);
    }

    void BuildPostProcessing()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.45f);

        _dof = profile.Add<DepthOfField>(true);
        _dof.mode.Override(DepthOfFieldMode.Bokeh);
        _dof.focusDistance.Override(defaultFocusDistance);
        _dof.aperture.Override(apertureFStop);
        _dof.focalLength.Override(50f);

        if (!enablePostProcessing) return;
        var volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.profile = profile;
    }

    // ---------- UI helpers (named to avoid clashing with UnityEngine.UI.Text / Rect) ----------

    static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    static void MakeBar(RectTransform parent, Vector2 pos, Vector2 size)
    {
        var rt = MakeRect("Bar", parent, new Vector2(0.5f, 0.5f), size, pos);
        rt.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
    }

    static Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 pos, int fontSize, TextAnchor align)
    {
        var rt = MakeRect(name, parent, anchor, size, pos);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = new Color(1f, 1f, 1f, 0.9f);
        t.text = "";
        return t;
    }

    static void MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var rt = MakeRect(label + " Button", parent, new Vector2(0.5f, 0.5f), new Vector2(260f, 48f), pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        MakeText(rt, "Label", new Vector2(0.5f, 0.5f), new Vector2(260f, 48f), Vector2.zero, 22, TextAnchor.MiddleCenter).text = label;
    }
}
