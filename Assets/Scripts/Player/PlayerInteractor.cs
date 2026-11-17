using System;
using UnityEngine;

/// <summary>
/// Crosshair targeting, LMB grab-and-drag, wheel hold distance, RMB highlight toggle,
/// and the first-person hand that reaches for or raises items.
/// Attach to the Player root next to PlayerController.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    /// <summary>Fires when the item under the crosshair changes (null = none).</summary>
    public event Action<Pickable> TargetChanged;

    [Header("References")]
    public PlayerController player;
    public Camera playerCamera;
    [Tooltip("Hand root (child of the camera). Moves to reach or raise items.")]
    public Transform hand;

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

    [Header("Arm")]
    [Tooltip("How quickly the hand follows its target. Higher = snappier.")] public float armSpeed = 14f;
    [Tooltip("Resting hand position in camera space.")]
    public Vector3 restPose = new Vector3(0.28f, -0.32f, 0.5f);

    public Pickable Target { get; private set; }
    public Pickable Held { get; private set; }

    LayerMask _rayMask;       // everything except the player's own body
    LayerMask _obstacleMask;  // walls, table, floor: no items pass through these
    bool _blocked;
    Vector3 _handLocal;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (playerCamera == null) playerCamera = player.playerCamera;
        _rayMask = ~LayerMask.GetMask("Player");
        _obstacleMask = player.obstacleMask & ~LayerMask.GetMask("Player", "Pickable");
        _handLocal = restPose;
    }

    void Update()
    {
        if (HUD.IsPaused) return;
        float dt = Time.deltaTime;

        UpdateTarget();

        if (Held == null && Target != null && InputBridge.GrabPressed())
            BeginGrab(Target);
        if (Held != null && !InputBridge.GrabHeld())
            EndGrab();

        if (Held != null)
        {
            float wheel = InputBridge.Wheel();
            if (Mathf.Abs(wheel) > 0.001f)
                holdDistance = Mathf.Clamp(holdDistance + wheel * wheelStep, minHoldDistance, maxHoldDistance);
            MoveHeldItem(dt);
        }

        if (Target != null && InputBridge.HighlightPressed() && HighlightManager.Instance != null)
            HighlightManager.Instance.Toggle(Target);

        UpdateHand(dt);
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

        if (found != Target)
        {
            Target = found;
            TargetChanged?.Invoke(Target);
        }
    }

    void BeginGrab(Pickable item)
    {
        Held = item;
        holdDistance = Mathf.Clamp(Vector3.Distance(playerCamera.transform.position, item.transform.position),
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
    /// Moves the held item toward the hold point with weight-based lag, after two obstacle checks:
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
        Vector3 current = Held.transform.position;
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
        Held.transform.position = next;

        // Collision feedback fires on the first contact frame only.
        if (hitNow && !_blocked)
        {
            float impact = dist / Mathf.Max(dt, 0.0001f);
            Held.NotifyCollided(impact);
            player.AddShake(Mathf.Clamp01(impact / 3f) * 0.6f);
        }
        _blocked = hitNow;
    }

    /// <summary>Hand reaches for the targeted item, raises the held item, otherwise rests.</summary>
    void UpdateHand(float dt)
    {
        if (hand == null) return;

        Vector3 goal = restPose;
        Pickable focus = Held != null ? Held : Target;
        if (focus != null)
        {
            Vector3 local = playerCamera.transform.InverseTransformPoint(focus.transform.position);
            goal = new Vector3(
                Mathf.Clamp(local.x, -0.4f, 0.4f),
                Mathf.Clamp(local.y, -0.45f, 0.1f),
                Mathf.Clamp(local.z, 0.45f, 1f));
            if (Held != null) goal.y = Mathf.Max(goal.y, -0.2f); // raised, holding pose
        }

        _handLocal = Vector3.Lerp(_handLocal, goal, 1f - Mathf.Exp(-armSpeed * dt));
        hand.localPosition = _handLocal;
    }
}
