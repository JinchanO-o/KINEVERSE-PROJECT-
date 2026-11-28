using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First-person locomotion and camera feel: smoothed mouse look, accelerated WASD movement,
/// sprint/crouch stances, optional jump, head bob, sprint FOV, held-item sway, camera shake and
/// speed-synced footsteps. Attach to the Player root (requires CharacterController).
/// Hierarchy: Player (this) > Head > Camera > hand root (HandController).
/// The Head carries pitch and stance height; the Camera carries only the layered feel offsets (see HandleCameraFeel).
/// The cursor lock is owned by HUD (locked while playing, free while paused).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pivot that holds the camera. Receives pitch and crouch height.")]
    public Transform head;
    public Camera playerCamera;
    [Tooltip("Source for footsteps. Created automatically if empty.")]
    public AudioSource footstepSource;

    [Header("Look")]
    [Tooltip("Degrees per unit of mouse movement. Raise for faster look.")]
    [Range(0.02f, 1f)] public float mouseSensitivity = 0.15f;
    [Tooltip("How quickly the view catches up with the mouse. Lower = floatier.")]
    [Range(2f, 60f)] public float lookSmoothing = 25f;
    [Tooltip("Maximum up/down look angle in degrees.")]
    [Range(40f, 89f)] public float maxPitch = 85f;

    [Header("Movement")]
    [Tooltip("Walking speed in m/s.")] public float walkSpeed = 2.5f;
    [Tooltip("Sprint speed in m/s (Shift, forward only).")] public float sprintSpeed = 5f;
    [Tooltip("Crouch-walk speed in m/s (Ctrl).")] public float crouchSpeed = 1.3f;
    [Tooltip("Acceleration in m/s^2 while a direction is held. Lower = slower pickup.")]
    public float acceleration = 14f;
    [Tooltip("Deceleration in m/s^2 when input is released. Lower = longer glide, never an instant stop.")]
    public float deceleration = 10f;
    [Tooltip("Downward acceleration in m/s^2.")] public float gravity = 24f;
    [Tooltip("Space jumps when on. Off by default: the lab is designed without jumping.")]
    public bool jumpEnabled = false;
    [Tooltip("Jump apex height in metres.")] public float jumpHeight = 1f;
    [Tooltip("Layers that block the held item and crouch headroom. Player and Pickable are removed automatically.")]
    public LayerMask obstacleMask = ~0;

    [Header("Stance")]
    [Tooltip("CharacterController height when standing.")] public float standHeight = 1.8f;
    [Tooltip("CharacterController height when crouching.")] public float crouchHeight = 1.1f;
    [Tooltip("Head (camera pivot) height when standing.")] public float standHeadY = 1.6f;
    [Tooltip("Head height when crouching.")] public float crouchHeadY = 0.95f;
    [Tooltip("How fast stance changes blend. Higher = snappier.")] public float stanceSpeed = 10f;

    [Header("Character Controller")]
    [Tooltip("Capsule radius in metres. Also sizes the headroom check before standing up.")]
    public float radius = 0.3f;
    [Tooltip("Tallest step climbed without jumping (metres). Keep below the crouch height.")]
    public float stepOffset = 0.3f;
    [Tooltip("Steepest walkable slope in degrees.")]
    [Range(0f, 90f)] public float slopeLimit = 45f;
    [Tooltip("Contact skin in metres. Unity recommends about 10% of the radius; too small and the capsule sticks.")]
    public float skinWidth = 0.08f;

    [Header("Head Bob")]
    [Tooltip("Vertical bob in metres at full speed. 0 disables it.")] public float bobAmount = 0.045f;
    [Tooltip("Side-to-side sway in metres at full speed.")] public float bobSway = 0.02f;
    [Tooltip("Bob cycles per metre walked. Higher = faster bob.")] public float bobPerMetre = 0.5f;

    [Header("Camera Feel")]
    [Tooltip("FOV while walking.")] public float baseFov = 75f;
    [Tooltip("FOV while sprinting.")] public float sprintFov = 85f;
    [Tooltip("How quickly the FOV transitions. Higher = faster.")] public float fovSpeed = 5f;
    [Tooltip("Sway strength while carrying an item (metres).")] public float heldSway = 0.006f;

    [Header("Footsteps")]
    [Tooltip("Clip for one step. Generated procedurally if empty.")] public AudioClip footstepClip;
    [Tooltip("Metres walked between steps when walking.")] public float walkStride = 1.2f;
    [Tooltip("Metres walked between steps when sprinting.")] public float sprintStride = 1.7f;
    [Tooltip("Metres walked between steps when crouching.")] public float crouchStride = 0.75f;
    [Range(0f, 1f)] public float walkVolume = 0.5f;
    [Range(0f, 1f)] public float sprintVolume = 0.9f;
    [Range(0f, 1f)] public float crouchVolume = 0.2f;

    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public float HorizontalSpeed { get; private set; }

    /// <summary>Set by PlayerInteractor while an item is held; enables sway.</summary>
    public bool IsHoldingItem { get; set; }

    /// <summary>Set by PlayerInteractor while the mouse rotates or drags an item; mouse look is paused.</summary>
    public bool LookLocked { get; set; }

    /// <summary>Set by PlayerInteractor while S resizes an item (S is also walk-back); walking is paused.</summary>
    public bool MovementLocked { get; set; }

    CharacterController _cc;
    Vector3 _velocity;
    float _yawTarget, _pitchTarget;
    float _yawSmooth, _pitchSmooth;
    float _headY;
    Vector3 _headRest, _cameraRest; // local positions as placed in the scene
    float _bobPhase;
    float _stepDistance;
    float _shake;
    bool _grounded;

    // Items moved into the capsule by scripts, ignored by the capsule until they are clear of it (see IgnoreItemsInside).
    // Shrink of the capsule radius, in metres, when looking for items inside it: an item only touching it does not count.
    const float ItemOverlapMargin = 0.01f;
    static readonly Collider[] s_itemOverlaps = new Collider[32];
    readonly HashSet<Collider> _ignoredItems = new HashSet<Collider>();
    readonly List<Collider> _ignoredScratch = new List<Collider>();
    int _pickableMask;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (head == null) head = playerCamera.transform.parent;

        // The player never collides with its own body or with items through this mask.
        obstacleMask &= ~LayerMask.GetMask("Player", "Pickable");
        _pickableMask = LayerMask.GetMask("Pickable");

        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 0f; // 2D: footsteps are the player's own body
        }
        if (footstepClip == null) footstepClip = AudioGenerator.Footstep();

        _cc.height = standHeight;
        _cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
        _cc.radius = radius;
        _cc.stepOffset = stepOffset;
        _cc.slopeLimit = slopeLimit;
        _cc.skinWidth = skinWidth;

        _yawTarget = _yawSmooth = transform.eulerAngles.y;
        _headY = standHeadY;
        _headRest = head.localPosition;
        _cameraRest = playerCamera.transform.localPosition;
    }

    void Update()
    {
        if (HUD.IsPaused) return;
        float dt = Time.deltaTime;
        HandleLook(dt);
        HandleStance(dt);
        HandleMovement(dt);
        HandleCameraFeel(dt);
        HandleFootsteps(dt);
    }

    /// <summary>Adds camera trauma in 0..1. Used when a held item hits an obstacle.</summary>
    public void AddShake(float amount)
    {
        _shake = Mathf.Min(1f, _shake + Mathf.Max(0f, amount));
    }

    void HandleLook(float dt)
    {
        Vector2 look = LookLocked ? Vector2.zero : InputBridge.Look();
        _yawTarget += look.x * mouseSensitivity;
        _pitchTarget = Mathf.Clamp(_pitchTarget - look.y * mouseSensitivity, -maxPitch, maxPitch);

        // Smoothing: exponential follow toward the raw mouse target.
        float k = 1f - Mathf.Exp(-lookSmoothing * dt);
        _yawSmooth = Mathf.LerpAngle(_yawSmooth, _yawTarget, k);
        _pitchSmooth = Mathf.Lerp(_pitchSmooth, _pitchTarget, k);

        transform.rotation = Quaternion.Euler(0f, _yawSmooth, 0f);
        head.localRotation = Quaternion.Euler(_pitchSmooth, 0f, 0f);
    }

    void HandleStance(float dt)
    {
        bool wantCrouch = InputBridge.Crouch();
        if (!wantCrouch && IsCrouching)
        {
            // Only stand back up when the top of the standing capsule would be clear. The capsule may sit up to
            // skinWidth inside a wall it is pressed against, so the probe is that much thinner.
            float probe = Mathf.Max(0.05f, radius - skinWidth);
            Vector3 top = transform.position + Vector3.up * (standHeight - probe);
            if (Physics.CheckSphere(top, probe, obstacleMask, QueryTriggerInteraction.Ignore))
                wantCrouch = true;
        }
        IsCrouching = wantCrouch;

        float k = 1f - Mathf.Exp(-stanceSpeed * dt);
        _cc.height = Mathf.Lerp(_cc.height, IsCrouching ? crouchHeight : standHeight, k);
        _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
        _headY = Mathf.Lerp(_headY, IsCrouching ? crouchHeadY : standHeadY, k);
        head.localPosition = new Vector3(_headRest.x, _headY, _headRest.z);
    }

    void HandleMovement(float dt)
    {
        Vector2 input = MovementLocked ? Vector2.zero : InputBridge.Move();
        Vector3 wish = transform.right * input.x + transform.forward * input.y;
        if (wish.sqrMagnitude > 1f) wish.Normalize();

        IsSprinting = InputBridge.Sprint() && input.y > 0.1f && !IsCrouching && wish.sqrMagnitude > 0.01f;
        float maxSpeed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
        Vector3 desired = wish * maxSpeed;

        // Accelerate toward the desired velocity; decelerate when input is released.
        float rate = wish.sqrMagnitude > 0.01f ? acceleration : deceleration;
        Vector3 horizontal = Vector3.MoveTowards(new Vector3(_velocity.x, 0f, _velocity.z), desired, rate * dt);

        _grounded = _cc.isGrounded;
        if (_grounded && _velocity.y < 0f) _velocity.y = -2f;
        else _velocity.y -= gravity * dt;
        if (jumpEnabled && _grounded && !IsCrouching && !MovementLocked && InputBridge.JumpPressed())
            _velocity.y = Mathf.Sqrt(2f * gravity * jumpHeight);

        _velocity = new Vector3(horizontal.x, _velocity.y, horizontal.z);
        IgnoreItemsInside();
        _cc.Move(_velocity * dt); // CharacterController resolves walls, table and floor

        HorizontalSpeed = horizontal.magnitude;
        if (_grounded && HorizontalSpeed > 0.01f)
            _bobPhase += HorizontalSpeed * dt * bobPerMetre * Mathf.PI * 2f;
    }

    /// <summary>
    /// Items are moved by scripts that ignore the player (carrying, axis moves, assembly snaps, magnets and effects),
    /// while the Player and Pickable layers collide. CharacterController.Move pushes the capsule out of an item moved
    /// into it, and since the hold point follows the camera, that push repeats every frame and drags the player away.
    /// So an item that really overlaps the capsule (radius shrunk by ItemOverlapMargin, so an item resting against it
    /// at skin distance does not count) stops colliding with the player, until it is clear of the capsule grown by
    /// the skin width again. Walking into a resting item is still blocked as before. Allocates nothing per frame.
    /// </summary>
    void IgnoreItemsInside()
    {
        Vector3 s = transform.lossyScale;
        float widthScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        float r = _cc.radius * widthScale;
        float height = Mathf.Max(_cc.height * Mathf.Abs(s.y), 2f * r);
        Vector3 center = transform.TransformPoint(_cc.center);
        Vector3 half = transform.up * (height * 0.5f - r);
        Vector3 bottom = center - half, top = center + half;

        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, Mathf.Max(0.001f, r - ItemOverlapMargin),
                                                   s_itemOverlaps, _pickableMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (_ignoredItems.Add(s_itemOverlaps[i])) Physics.IgnoreCollision(_cc, s_itemOverlaps[i], true);
        if (_ignoredItems.Count == 0) return;

        // Hysteresis: collide again only once the item is clear of the capsule grown by the skin width.
        int near = Physics.OverlapCapsuleNonAlloc(bottom, top, r + _cc.skinWidth * widthScale,
                                                  s_itemOverlaps, _pickableMask, QueryTriggerInteraction.Ignore);
        _ignoredScratch.Clear();
        _ignoredScratch.AddRange(_ignoredItems);
        foreach (Collider c in _ignoredScratch)
        {
            // A destroyed or deactivated collider has lost its ignore state already.
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
            {
                _ignoredItems.Remove(c);
                continue;
            }
            bool stillNear = false;
            for (int i = 0; i < near && !stillNear; i++) stillNear = s_itemOverlaps[i] == c;
            if (stillNear) continue;
            Physics.IgnoreCollision(_cc, c, false);
            _ignoredItems.Remove(c);
        }
    }

    /// <summary>
    /// Camera local position = rest (as placed in the scene) + head bob + held-item sway + shake.
    /// Each layer is computed on its own and they are only summed here, so none overwrites another.
    /// </summary>
    void HandleCameraFeel(float dt)
    {
        playerCamera.transform.localPosition = _cameraRest + HeadBobOffset() + HeldSwayOffset() + ShakeOffset(dt);

        float targetFov = IsSprinting ? sprintFov : baseFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSpeed * dt));
    }

    Vector3 HeadBobOffset()
    {
        float moveAmount = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.01f, sprintSpeed));
        return new Vector3(Mathf.Cos(_bobPhase) * bobSway, Mathf.Sin(_bobPhase * 2f) * bobAmount, 0f) * moveAmount;
    }

    Vector3 HeldSwayOffset()
    {
        if (!IsHoldingItem) return Vector3.zero;
        return new Vector3(Mathf.Sin(Time.time * 1.1f) * heldSway, Mathf.Cos(Time.time * 1.7f) * heldSway * 0.6f, 0f);
    }

    Vector3 ShakeOffset(float dt)
    {
        _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.5f);
        return _shake > 0f ? Random.insideUnitSphere * (_shake * _shake * 0.04f) : Vector3.zero;
    }

    void HandleFootsteps(float dt)
    {
        if (!_grounded || HorizontalSpeed < 0.3f)
        {
            _stepDistance = 0f;
            return;
        }

        _stepDistance += HorizontalSpeed * dt;
        float stride = IsCrouching ? crouchStride : (IsSprinting ? sprintStride : walkStride);
        if (_stepDistance < stride) return;
        _stepDistance -= stride;

        float volume = IsCrouching ? crouchVolume : (IsSprinting ? sprintVolume : walkVolume);
        footstepSource.pitch = Random.Range(0.9f, 1.1f) * (IsSprinting ? 1.15f : (IsCrouching ? 0.85f : 1f));
        footstepSource.PlayOneShot(footstepClip, volume);
    }
}
