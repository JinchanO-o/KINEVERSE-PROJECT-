using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First-person hand animator: breathing idle, walk bob, grab pose, and a soft
/// camera-lag so the hands feel attached to a body, not glued to the camera.
/// Attach to "Hands_Root" (a child of the Main Camera). Assign both hand transforms.
/// Works with the new Input System when available, falls back to the legacy Input Manager.
/// </summary>
public class HandController : MonoBehaviour
{
    [Header("Hands")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Rest pose (local to Hands_Root, camera space)")]
    [Tooltip("x = right, y = up, z = forward.")]
    public Vector3 leftRestPos  = new Vector3(-0.22f, -0.28f, 0.45f);
    public Vector3 leftRestRot  = new Vector3(-10f,  25f,  10f);
    public Vector3 rightRestPos = new Vector3( 0.22f, -0.28f, 0.45f);
    public Vector3 rightRestRot = new Vector3(-10f, -25f, -10f);

    [Header("Idle breathing")]
    [Tooltip("Metres of vertical sway at idle. 0 disables it.")] public float breathAmount = 0.004f;
    [Tooltip("Cycles per second at idle.")]                      public float breathSpeed  = 1.3f;

    [Header("Walk bob (while WASD is held)")]
    public float bobAmount = 0.02f;
    public float bobSpeed  = 7f;
    [Tooltip("Minimum WASD magnitude to count as moving.")]
    public float moveThreshold = 0.1f;

    [Header("Grab (Left Mouse Button)")]
    [Tooltip("Position offset added while grabbing (camera space: y lifts, z pushes forward).")]
    public Vector3 grabOffsetPos       = new Vector3(0f, 0.05f, 0.15f);
    public Vector3 grabOffsetRotLeft   = new Vector3(20f,  10f,  30f);
    public Vector3 grabOffsetRotRight  = new Vector3(20f, -10f, -30f);
    [Tooltip("How quickly the grab pose blends in and out (units per second of the 0..1 blend).")]
    public float grabLerpSpeed = 14f;

    [Header("Camera lag (feels physical)")]
    [Tooltip("Higher = hands snap to the camera faster. 0 would freeze them.")]
    public float positionLag = 10f;
    public float rotationLag = 12f;
    [Tooltip("How strongly the hands lean opposite to a fast look.")]
    public float lookLeanStrength = 0.004f;

    Transform _cam;
    Vector3 _leftLocalPos, _rightLocalPos;
    Quaternion _leftLocalRot, _rightLocalRot;
    Vector3 _laggedCamEuler;
    float _bobPhase;
    float _grab;

    void Awake()
    {
        _cam = Camera.main != null ? Camera.main.transform : transform.parent;

        _leftLocalPos  = leftRestPos;
        _rightLocalPos = rightRestPos;
        _leftLocalRot  = Quaternion.Euler(leftRestRot);
        _rightLocalRot = Quaternion.Euler(rightRestRot);
        _laggedCamEuler = _cam != null ? _cam.eulerAngles : Vector3.zero;
    }

    void LateUpdate()
    {
        if (leftHand == null && rightHand == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector2 move = ReadMove();
        bool isMoving = move.sqrMagnitude > moveThreshold * moveThreshold;
        bool grabHeld = ReadGrab();

        // Grab blend 0..1.
        float grabTarget = grabHeld ? 1f : 0f;
        _grab = Mathf.MoveTowards(_grab, grabTarget, dt * grabLerpSpeed);

        // Breathing / bob phase.
        float freq = isMoving ? bobSpeed : breathSpeed;
        float amp  = isMoving ? bobAmount : breathAmount;
        _bobPhase += freq * dt;
        float bobY = Mathf.Sin(_bobPhase * 2f) * amp;
        float bobX = Mathf.Cos(_bobPhase) * amp * 0.5f;

        // Camera lag: hands lag a few degrees behind fast rotation and lean opposite.
        if (_cam != null)
        {
            Vector3 camEuler = _cam.eulerAngles;
            float kRot = 1f - Mathf.Exp(-rotationLag * dt);
            _laggedCamEuler.x = Mathf.LerpAngle(_laggedCamEuler.x, camEuler.x, kRot);
            _laggedCamEuler.y = Mathf.LerpAngle(_laggedCamEuler.y, camEuler.y, kRot);
            _laggedCamEuler.z = Mathf.LerpAngle(_laggedCamEuler.z, camEuler.z, kRot);
        }
        float yawDelta   = _cam != null ? Mathf.DeltaAngle(_laggedCamEuler.y, _cam.eulerAngles.y) : 0f;
        float pitchDelta = _cam != null ? Mathf.DeltaAngle(_laggedCamEuler.x, _cam.eulerAngles.x) : 0f;
        Vector3 lagOffset = new Vector3(-yawDelta * lookLeanStrength, pitchDelta * lookLeanStrength, 0f);

        // Apply per-hand.
        float kPos = 1f - Mathf.Exp(-positionLag * dt);
        float kRotApply = 1f - Mathf.Exp(-rotationLag * dt);

        if (leftHand != null)
        {
            Vector3 targetPos = leftRestPos + new Vector3(bobX, bobY, 0f) + lagOffset + grabOffsetPos * _grab;
            Quaternion targetRot = Quaternion.Euler(leftRestRot + grabOffsetRotLeft * _grab);
            _leftLocalPos = Vector3.Lerp(_leftLocalPos, targetPos, kPos);
            _leftLocalRot = Quaternion.Slerp(_leftLocalRot, targetRot, kRotApply);
            leftHand.localPosition = _leftLocalPos;
            leftHand.localRotation = _leftLocalRot;
        }
        if (rightHand != null)
        {
            // Mirror X bob so the two hands don't move in lockstep.
            Vector3 targetPos = rightRestPos + new Vector3(-bobX, bobY, 0f) + lagOffset + grabOffsetPos * _grab;
            Quaternion targetRot = Quaternion.Euler(rightRestRot + grabOffsetRotRight * _grab);
            _rightLocalPos = Vector3.Lerp(_rightLocalPos, targetPos, kPos);
            _rightLocalRot = Quaternion.Slerp(_rightLocalRot, targetRot, kRotApply);
            rightHand.localPosition = _rightLocalPos;
            rightHand.localRotation = _rightLocalRot;
        }
    }

    static Vector2 ReadMove()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
        return Vector2.zero;
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    static bool ReadGrab()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }
}
