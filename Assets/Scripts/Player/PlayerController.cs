using UnityEngine;

/// <summary>
/// First-person locomotion and camera feel: smoothed mouse look, accelerated WASD movement,
/// sprint/crouch stances, head bob, sprint FOV, held-item sway, camera shake and
/// speed-synced footsteps. Attach to the Player root (requires CharacterController).
/// Hierarchy: Player (this) > Head > Camera (ArmRig is parented to the Camera).
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
    [Tooltip("Layers that block the held item and crouch headroom. Player and Pickable are removed automatically.")]
    public LayerMask obstacleMask = ~0;

    [Header("Stance")]
    [Tooltip("CharacterController height when standing.")] public float standHeight = 1.8f;
    [Tooltip("CharacterController height when crouching.")] public float crouchHeight = 1.1f;
    [Tooltip("Head (camera pivot) height when standing.")] public float standHeadY = 1.6f;
    [Tooltip("Head height when crouching.")] public float crouchHeadY = 0.95f;
    [Tooltip("How fast stance changes blend. Higher = snappier.")] public float stanceSpeed = 10f;

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

    CharacterController _cc;
    Vector3 _velocity;
    float _yawTarget, _pitchTarget;
    float _yawSmooth, _pitchSmooth;
    float _headY;
    float _bobPhase;
    float _stepDistance;
    float _shake;
    bool _grounded;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (head == null) head = playerCamera.transform.parent;

        // The player never collides with its own body or with items through this mask.
        obstacleMask &= ~LayerMask.GetMask("Player", "Pickable");

        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 0f; // 2D: footsteps are the player's own body
        }
        if (footstepClip == null) footstepClip = AudioGenerator.Footstep();

        _cc.height = standHeight;
        _cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
        _cc.radius = 0.3f;

        _yawTarget = _yawSmooth = transform.eulerAngles.y;
        _headY = standHeadY;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        Vector2 look = InputBridge.Look();
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
            // Only stand back up when there is headroom.
            Vector3 headPos = transform.position + Vector3.up * (standHeight - 0.25f);
            if (Physics.CheckSphere(headPos, 0.25f, obstacleMask, QueryTriggerInteraction.Ignore))
                wantCrouch = true;
        }
        IsCrouching = wantCrouch;

        float k = 1f - Mathf.Exp(-stanceSpeed * dt);
        _cc.height = Mathf.Lerp(_cc.height, IsCrouching ? crouchHeight : standHeight, k);
        _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
        _headY = Mathf.Lerp(_headY, IsCrouching ? crouchHeadY : standHeadY, k);
        head.localPosition = new Vector3(0f, _headY, 0f);
    }

    void HandleMovement(float dt)
    {
        Vector2 input = InputBridge.Move();
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

        _velocity = new Vector3(horizontal.x, _velocity.y, horizontal.z);
        _cc.Move(_velocity * dt); // CharacterController resolves walls, table and floor

        HorizontalSpeed = horizontal.magnitude;
        if (_grounded && HorizontalSpeed > 0.01f)
            _bobPhase += HorizontalSpeed * dt * bobPerMetre * Mathf.PI * 2f;
    }

    void HandleCameraFeel(float dt)
    {
        float moveAmount = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.01f, sprintSpeed));
        float bobY = Mathf.Sin(_bobPhase * 2f) * bobAmount * moveAmount;
        float bobX = Mathf.Cos(_bobPhase) * bobSway * moveAmount;

        float swayX = 0f, swayY = 0f;
        if (IsHoldingItem)
        {
            swayX = Mathf.Sin(Time.time * 1.1f) * heldSway;
            swayY = Mathf.Cos(Time.time * 1.7f) * heldSway * 0.6f;
        }

        _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.5f);
        Vector3 shakeOffset = _shake > 0f ? Random.insideUnitSphere * (_shake * _shake * 0.04f) : Vector3.zero;

        playerCamera.transform.localPosition = new Vector3(bobX + swayX, bobY + swayY, 0f) + shakeOffset;

        float targetFov = IsSprinting ? sprintFov : baseFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSpeed * dt));
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
