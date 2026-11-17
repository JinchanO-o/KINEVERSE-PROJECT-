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

    Text _prompt;
    GameObject _pauseMenu;
    DepthOfField _dof;
    float _focus;

    void Awake()
    {
        // Reset static state in case the scene was reloaded while paused.
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

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

    // ---------- Prompt and focus ----------

    void RefreshPrompt()
    {
        if (interactor.Held != null)
            _prompt.text = $"Scroll: hold distance {interactor.holdDistance:0.0} m\nRMB to highlight";
        else if (interactor.Target != null)
            _prompt.text = "Hold LMB to grab\nRMB to highlight";
        else
            _prompt.text = "";
    }

    void RefreshFocus()
    {
        if (_dof == null || playerCamera == null) return;

        float target = defaultFocusDistance;
        if (interactor.Target != null)
            target = Vector3.Distance(playerCamera.transform.position, interactor.Target.transform.position);

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

        // Pause menu: full-screen dim panel with Resume and Quit.
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
        MakeButton(pr, "Quit", new Vector2(0f, -40f), () => Application.Quit());
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
