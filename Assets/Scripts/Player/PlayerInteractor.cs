using UnityEngine;

/// <summary>What the first-person hands are doing. Set by PlayerInteractor, shown by HandController.</summary>
public enum HandState { Idle, Targeting, Selected, Holding, AxisMoving, Rotating, Scaling }

/// <summary>
/// Crosshair targeting and every way the player handles items:
/// RMB select / deselect, LMB grab-and-drag of the selected item, R + RMB free rotation,
/// hold X / Y / Z for axis moves (LMB drag) and axis rotation (RMB drag), hold S for resizing with + / -,
/// the axis gizmo and wheel hold distance. It also publishes HandState and ReachTarget for HandController,
/// which alone poses the hands.
/// Attach to the Player root next to PlayerController. Needs a HighlightManager in the scene.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    public Camera playerCamera;

    [Header("Targeting")]
    [Tooltip("Maximum centre-screen raycast distance in metres.")]
    public float reach = 4f;

    [Header("Hold")]
    [Tooltip("Distance from camera to the held item in metres. Changed by the mouse wheel.")]
    public float holdDistance = 1.8f;
    [Tooltip("Closest the held item can be (metres).")] public float minHoldDistance = 0.8f;
    [Tooltip("Farthest the held item can be (metres).")] public float maxHoldDistance = 6f;
    [Tooltip("Metres added or removed per wheel notch while holding.")] public float wheelStep = 0.25f;
    [Tooltip("Hold point offset in camera space: x = right, y = up.")]
    public Vector2 holdOffset = new Vector2(0.18f, -0.12f);
    [Tooltip("Gap kept between a held item and any surface it stops against (metres).")]
    public float skin = 0.02f;

    [Header("Rotate")]
    [Tooltip("Degrees the selected item turns per pixel of mouse movement (R + RMB, and axis rotation).")]
    public float rotationSensitivity = 0.3f;
    [Tooltip("How quickly the item catches up with the mouse while rotating. Lower = smoother and floatier, higher = snappier.")]
    public float rotationSmoothing = 12f;

    [Header("Scale")]
    [Tooltip("Size change per + or - press: 0.05 = 5%.")]
    [Range(0.01f, 0.5f)] public float scaleStep = 0.05f;

    [Header("Axis Gizmo")]
    [Tooltip("Arrow length as a fraction of the camera distance, so the gizmo keeps its size on screen.")]
    public float gizmoScreenSize = 0.12f;
    public Color xAxisColor = Color.red;
    public Color yAxisColor = Color.blue;
    public Color zAxisColor = Color.green;
    [Tooltip("Colour of the arrow whose axis key is held.")]
    public Color activeAxisColor = Color.yellow;

    public Pickable Target { get; private set; }
    public Pickable Held { get; private set; }
    /// <summary>R is held with an item selected: the right mouse button rotates freely instead of selecting.</summary>
    public bool RotateArmed { get; private set; }
    /// <summary>The selected item is being rotated freely (R + RMB) this frame.</summary>
    public bool IsRotating { get; private set; }
    /// <summary>The held axis key (X, Y or Z), or None.</summary>
    public Axis ActiveAxis { get; private set; } = Axis.None;
    /// <summary>S is held with an item selected: + and - resize it.</summary>
    public bool IsScaling { get; private set; }
    /// <summary>What the hands should show this frame.</summary>
    public HandState HandState { get; private set; }
    /// <summary>World point the hand reaches toward (centre of the item being handled or aimed at), or null.</summary>
    public Vector3? ReachTarget { get; private set; }

    const float MaxPendingRotation = 170f;
    const float ClickDragThreshold = 6f;   // pixels of RMB movement that turn a click into a drag
    const float ScaleRepeatDelay = 0.4f;   // seconds before a held + or - starts repeating
    const float ScaleRepeatInterval = 0.08f;
    // Below this fraction of a camera-facing unit length on screen, an axis points too nearly at the
    // camera for its screen direction to be used.
    const float MinAxisScreenFraction = 0.15f;

    LayerMask _rayMask;       // everything except the player's own body
    LayerMask _obstacleMask;  // walls, table, floor: no items pass through these
    LayerMask _blockMask;     // obstacles plus other items: an item being edited may touch neither
    AxisGizmo _gizmo;
    readonly bool[] _axisKeyWasHeld = new bool[3];
    // Rotation the mouse has asked for but the item has not reached yet (world space, about the item's centre).
    Quaternion _pendingRotation = Quaternion.identity;
    Pickable _pendingItem;
    bool _pendingFromAxis;
    bool _rightClickPending;  // RMB went down in axis mode; a click or a drag has not been decided yet
    float _rightClickTravel;
    bool _axisRotateDrag;     // RMB drag in axis mode, decided
    int _scaleDirection;
    float _scaleRepeatAt;
    Pickable _manipulated;
    bool _blocked;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (playerCamera == null) playerCamera = player.playerCamera;
        _rayMask = ~LayerMask.GetMask("Player");
        _obstacleMask = player.obstacleMask & ~LayerMask.GetMask("Player", "Pickable");
        _blockMask = _obstacleMask | LayerMask.GetMask("Pickable");
        _gizmo = AxisGizmo.Create(playerCamera, gizmoScreenSize, xAxisColor, yAxisColor, zAxisColor, activeAxisColor);
    }

    void Start()
    {
        if (HighlightManager.Instance == null)
            Debug.LogWarning("PlayerInteractor: no HighlightManager in the scene, so items cannot be selected or moved.", this);
    }

    void OnDestroy()
    {
        if (_gizmo != null) Destroy(_gizmo.gameObject);
    }

    void Update()
    {
        if (HUD.IsPaused)
        {
            StopEditing();
            return;
        }
        float dt = Time.deltaTime;
        HighlightManager selection = HighlightManager.Instance;
        Pickable selected = selection != null ? selection.Selected : null;
        Vector2 mouse = InputBridge.Look();

        UpdateTarget();
        UpdateActiveAxis();

        IsScaling = selected != null && Held == null && InputBridge.ScaleKeyHeld();
        // Axis mode: an axis key held with a selection. With S held, the axis key only picks the resize axis.
        bool axisMode = selected != null && Held == null && !IsScaling && ActiveAxis != Axis.None;
        // Axis rotation replaces free rotation while an axis key is held: R arming is ignored, and both
        // stay available otherwise (R + RMB = free tumble, X/Y/Z + RMB = rotation about one local axis).
        UpdateRotateArming(axisMode ? null : selected);

        if (selection != null && !RotateArmed)
            HandleRightButton(selection, axisMode, mouse);
        selected = selection != null ? selection.Selected : null;
        if (!InputBridge.RotateMouseHeld()) _axisRotateDrag = false;

        // Rotating and moving never overlap: free rotation is ignored while LMB is held or an item is carried,
        // and no grab starts while rotating, in axis mode or in scale mode.
        IsRotating = RotateArmed && selected != null && InputBridge.RotateMouseHeld()
                     && Held == null && !InputBridge.GrabHeld();
        bool axisMove = axisMode && InputBridge.GrabHeld();
        bool axisRotate = axisMode && !axisMove && _axisRotateDrag;

        if (IsRotating) QueueFreeRotation(selected, mouse);
        else if (axisRotate) QueueRotation(selected, AxisRotationAngle(selected, ActiveAxis, mouse), AxisDirection(selected, ActiveAxis), true);
        // Axis rotation stops at its current pose as soon as the drag or the axis key ends.
        if (_pendingFromAxis && !axisRotate) _pendingRotation = Quaternion.identity;
        ApplyPendingRotation(selected, dt);

        if (axisMove) selected.TryTranslate(AxisTranslation(selected, ActiveAxis, mouse), _blockMask);
        if (IsScaling) UpdateScaling(selected);
        else _scaleDirection = 0;

        player.LookLocked = IsRotating || (axisMode && (InputBridge.GrabHeld() || InputBridge.RotateMouseHeld()));
        player.MovementLocked = IsScaling; // S doubles as walk-back, so walking pauses while resizing
        SetManipulated(IsRotating || axisMove || axisRotate || IsScaling ? selected : null);

        // Only the selected item may move, so deselecting the held item also drops it.
        if (Held != null && (Held != selected || !InputBridge.GrabHeld()))
            EndGrab();
        if (Held == null && !IsRotating && !axisMode && !IsScaling && Target != null && Target == selected && InputBridge.GrabPressed())
            BeginGrab(Target);

        if (Held != null)
        {
            float wheel = InputBridge.Wheel();
            if (Mathf.Abs(wheel) > 0.001f)
                holdDistance = Mathf.Clamp(holdDistance + wheel * wheelStep, minHoldDistance, maxHoldDistance);
            MoveHeldItem(dt);
        }

        if (_gizmo != null) _gizmo.Show(selected, selected != null ? ActiveAxis : Axis.None);
        UpdateHandState(selected, axisMove, axisRotate);
    }

    /// <summary>
    /// One hand state per frame, highest priority first. The reach point is the item being worked on, or else
    /// the item under the crosshair.
    /// </summary>
    void UpdateHandState(Pickable selected, bool axisMove, bool axisRotate)
    {
        bool editing = Held == null && (IsScaling || IsRotating || axisRotate || axisMove);
        HandState = Held != null ? HandState.Holding
                  : IsScaling ? HandState.Scaling
                  : IsRotating || axisRotate ? HandState.Rotating
                  : axisMove ? HandState.AxisMoving
                  : selected != null ? HandState.Selected
                  : Target != null ? HandState.Targeting
                  : HandState.Idle;

        Pickable focus = Held != null ? Held : editing ? selected : Target;
        ReachTarget = focus != null ? focus.Center : (Vector3?)null;
    }

    /// <summary>Pause: end every edit so nothing keeps turning or stays kinematic.</summary>
    void StopEditing()
    {
        RotateArmed = false;
        IsRotating = false;
        IsScaling = false;
        _pendingRotation = Quaternion.identity;
        _rightClickPending = false;
        _axisRotateDrag = false;
        player.LookLocked = false;
        player.MovementLocked = false;
        SetManipulated(null);
    }

    /// <summary>Raycast from the screen centre; the first blocking surface decides the target.</summary>
    void UpdateTarget()
    {
        Pickable found = Held;
        if (Held == null)
        {
            Transform cam = playerCamera.transform;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, reach, _rayMask, QueryTriggerInteraction.Ignore))
                found = hit.collider.GetComponentInParent<Pickable>();
        }

        Target = found;
    }

    /// <summary>
    /// Axis keys are hold-to-activate. If several are held, the first one pressed stays active; releasing it
    /// does not hand over to another key that is still held (press that key again).
    /// </summary>
    void UpdateActiveAxis()
    {
        for (int i = 0; i < 3; i++)
        {
            bool held = InputBridge.AxisKeyHeld((Axis)i);
            if (!held && ActiveAxis == (Axis)i) ActiveAxis = Axis.None;
            if (held && !_axisKeyWasHeld[i] && ActiveAxis == Axis.None) ActiveAxis = (Axis)i;
            _axisKeyWasHeld[i] = held;
        }
    }

    /// <summary>
    /// Right button: outside axis mode a press selects at once. In axis mode a press waits: released without
    /// moving it is a click and selects; moved past ClickDragThreshold it becomes an axis-rotation drag and the
    /// selection stays as it is.
    /// </summary>
    void HandleRightButton(HighlightManager selection, bool axisMode, Vector2 mouse)
    {
        if (InputBridge.SelectPressed())
        {
            if (axisMode)
            {
                _rightClickPending = true;
                _rightClickTravel = 0f;
            }
            else
            {
                ClickSelect(selection);
            }
        }

        if (!_rightClickPending) return;
        if (!InputBridge.RotateMouseHeld())
        {
            _rightClickPending = false;
            ClickSelect(selection);
            return;
        }
        _rightClickTravel += mouse.magnitude;
        if (_rightClickTravel > ClickDragThreshold)
        {
            _rightClickPending = false;
            _axisRotateDrag = true;
        }
    }

    void ClickSelect(HighlightManager selection)
    {
        if (Target != null) selection.Toggle(Target);
        else selection.Select(null); // right-click on empty space
    }

    /// <summary>
    /// Arming rule: R arms free rotation only while an item is selected and the right button is up.
    /// If R is pressed while the right button is already held, rotation stays disarmed until that
    /// button is released (R still held) and pressed again. Releasing R disarms at once.
    /// </summary>
    void UpdateRotateArming(Pickable selected)
    {
        if (!InputBridge.RotateKeyHeld() || selected == null)
            RotateArmed = false;
        else if (!InputBridge.RotateMouseHeld())
            RotateArmed = true;
    }

    /// <summary>Makes the item kinematic while it is edited (see Pickable.SetManipulated).</summary>
    void SetManipulated(Pickable item)
    {
        if (item == _manipulated) return;
        if (_manipulated != null) _manipulated.SetManipulated(false);
        _manipulated = item;
        if (item != null) item.SetManipulated(true);
    }

    // ---------- Rotation ----------

    /// <summary>
    /// Free rotation, camera-relative: horizontal mouse movement spins the item around the camera's up axis and
    /// vertical movement around the camera's right axis, so it always follows the mouse on screen.
    /// </summary>
    void QueueFreeRotation(Pickable item, Vector2 mouse)
    {
        Transform cam = playerCamera.transform;
        Quaternion q = Quaternion.AngleAxis(-mouse.x * rotationSensitivity, cam.up)
                     * Quaternion.AngleAxis(mouse.y * rotationSensitivity, cam.right);
        q.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        QueueRotation(item, angle, axis, false);
    }

    /// <summary>
    /// Adds a turn to the pending rotation, kept under 180 degrees: beyond that a quaternion takes the short way
    /// round, so a very fast flick would spin the item backwards.
    /// </summary>
    void QueueRotation(Pickable item, float angle, Vector3 axis, bool fromAxis)
    {
        if (item != _pendingItem || fromAxis != _pendingFromAxis)
        {
            _pendingItem = item;
            _pendingFromAxis = fromAxis;
            _pendingRotation = Quaternion.identity;
        }
        if (Mathf.Abs(angle) < 0.0001f) return;

        _pendingRotation.ToAngleAxis(out float pending, out _);
        if (pending > 180f) pending = 360f - pending;
        float room = Mathf.Max(0f, MaxPendingRotation - pending);
        angle = Mathf.Clamp(angle, -room, room);
        _pendingRotation = Quaternion.AngleAxis(angle, axis) * _pendingRotation;
    }

    /// <summary>
    /// Eases the selected item toward the pending rotation (exponential follow, like the camera's mouse look),
    /// so uneven mouse input still turns it smoothly. Free rotation eases out after R or RMB is released.
    /// Hitting an obstacle, grabbing, deselecting or pausing drops the remainder.
    /// </summary>
    void ApplyPendingRotation(Pickable selected, float dt)
    {
        if (selected == null || selected != _pendingItem || Held != null)
        {
            _pendingRotation = Quaternion.identity;
            return;
        }

        _pendingRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) < 0.01f)
        {
            _pendingRotation = Quaternion.identity;
            return;
        }

        float applied = angle * (1f - Mathf.Exp(-rotationSmoothing * dt));
        if (selected.TryRotate(applied, axis, _blockMask))
            _pendingRotation = Quaternion.AngleAxis(angle - applied, axis);
        else
            _pendingRotation = Quaternion.identity; // stopped against an obstacle
    }

    // ---------- Axis tools ----------

    static Vector3 AxisDirection(Pickable item, Axis axis)
    {
        Transform t = item.transform;
        return axis == Axis.X ? t.right : axis == Axis.Y ? t.up : t.forward;
    }

    /// <summary>
    /// Screen-space direction of a 1 m step along the axis from the item's centre, or false when the axis
    /// points too nearly at the camera to have a usable direction on screen.
    /// </summary>
    bool AxisOnScreen(Pickable item, Vector3 axis, out Vector2 screenStep)
    {
        Vector3 o = item.Center;
        Vector2 origin = playerCamera.WorldToScreenPoint(o);
        screenStep = (Vector2)playerCamera.WorldToScreenPoint(o + axis) - origin;
        float reference = ((Vector2)playerCamera.WorldToScreenPoint(o + playerCamera.transform.right) - origin).magnitude;
        return screenStep.magnitude >= reference * MinAxisScreenFraction;
    }

    /// <summary>Mouse movement projected onto the axis's screen direction, converted to metres along the axis.</summary>
    Vector3 AxisTranslation(Pickable item, Axis axis, Vector2 mouse)
    {
        Vector3 dir = AxisDirection(item, axis);
        if (!AxisOnScreen(item, dir, out Vector2 step)) return Vector3.zero;
        return dir * (Vector2.Dot(mouse, step) / step.sqrMagnitude);
    }

    /// <summary>
    /// Degrees to turn about the axis: mouse movement across the axis on screen drags the near side of the item
    /// with the mouse. When the axis points along the view, horizontal movement spins it instead.
    /// </summary>
    float AxisRotationAngle(Pickable item, Axis axis, Vector2 mouse)
    {
        Vector3 dir = AxisDirection(item, axis);
        if (AxisOnScreen(item, dir, out Vector2 step))
        {
            Vector2 across = new Vector2(-step.y, step.x).normalized;
            return Vector2.Dot(mouse, across) * rotationSensitivity;
        }
        float facing = Vector3.Dot(dir, playerCamera.transform.forward) > 0f ? -1f : 1f;
        return mouse.x * rotationSensitivity * facing;
    }

    /// <summary>One resize step per + or - press, repeating while the key stays down.</summary>
    void UpdateScaling(Pickable item)
    {
        int direction = InputBridge.ScaleUpHeld() ? 1 : InputBridge.ScaleDownHeld() ? -1 : 0;
        bool step = false;
        if (direction != 0 && direction != _scaleDirection)
        {
            step = true;
            _scaleRepeatAt = Time.time + ScaleRepeatDelay;
        }
        else if (direction != 0 && Time.time >= _scaleRepeatAt)
        {
            step = true;
            _scaleRepeatAt += ScaleRepeatInterval;
        }
        _scaleDirection = direction;
        if (!step) return;

        float factor = direction > 0 ? 1f + scaleStep : 1f / (1f + scaleStep);
        Vector3 scale = item.transform.localScale;
        if (ActiveAxis == Axis.None) scale *= factor;
        else scale[(int)ActiveAxis] *= factor;
        item.TryScale(scale, _blockMask);
    }

    // ---------- Grab ----------

    void BeginGrab(Pickable item)
    {
        Held = item;
        holdDistance = Mathf.Clamp(Vector3.Distance(playerCamera.transform.position, item.Center),
                                   minHoldDistance, maxHoldDistance);
        _blocked = false;
        player.IsHoldingItem = true;
        item.NotifyGrabbed();
    }

    void EndGrab()
    {
        // The item keeps its current position: nothing else moves it.
        Pickable item = Held;
        Held = null;
        player.IsHoldingItem = false;
        item.NotifyReleased();
    }

    /// <summary>
    /// Moves the held item's centre toward the hold point with weight-based lag, after two obstacle checks:
    /// (1) a sphere raycast from the camera to the hold point, (2) a sweep of the item itself.
    /// </summary>
    void MoveHeldItem(float dt)
    {
        Transform cam = playerCamera.transform;
        float radius = Held.Radius;

        // Hold point in front of the camera.
        Vector3 origin = cam.position;
        Vector3 holdPoint = origin + cam.forward * holdDistance
                          + cam.right * holdOffset.x + cam.up * holdOffset.y;

        // Check 1: camera -> hold point. Stop short of any wall, table or floor.
        Vector3 toHold = holdPoint - origin;
        float len = toHold.magnitude;
        if (len > 0.0001f &&
            Physics.SphereCast(origin, radius, toHold / len, out RaycastHit camHit, len, _obstacleMask, QueryTriggerInteraction.Ignore))
        {
            holdPoint = origin + (toHold / len) * Mathf.Max(0f, camHit.distance - skin);
        }

        // Lag: exponential follow, heavier items have a lower follow speed.
        Vector3 current = Held.Center;
        float k = 1f - Mathf.Exp(-Held.FollowSpeed * dt);
        Vector3 next = Vector3.Lerp(current, holdPoint, k);

        // Check 2: sweep the item from its current spot to the new spot.
        Vector3 delta = next - current;
        float dist = delta.magnitude;
        bool hitNow = false;
        if (dist > 0.0001f &&
            Physics.SphereCast(current, radius, delta / dist, out RaycastHit itemHit, dist, _obstacleMask, QueryTriggerInteraction.Ignore))
        {
            next = current + (delta / dist) * Mathf.Max(0f, itemHit.distance - skin);
            hitNow = true;
        }
        // Shift the root by the centre's motion, since the pivot may be far from the geometry.
        Held.transform.position += next - current;

        // Collision feedback fires on the first contact frame only.
        if (hitNow && !_blocked)
        {
            float impact = dist / Mathf.Max(dt, 0.0001f);
            Held.NotifyCollided(impact);
            player.AddShake(Mathf.Clamp01(impact / 3f) * 0.6f);
        }
        _blocked = hitNow;
    }
}
