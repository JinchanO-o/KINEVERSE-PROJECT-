using System;
using UnityEngine;

/// <summary>
/// The one system that poses the first-person hands. Each frame it reads PlayerInteractor.HandState and
/// PlayerInteractor.ReachTarget, blends toward that state's pose, adds the reach toward the item, idle
/// breathing / walk bob (Idle and Targeting only) and a small lean against fast looks, then lets
/// HandClippingPrevention pull each hand out of walls.
/// Split: PlayerInteractor decides WHAT the hands do (state and reach point); this component decides
/// HOW they look (the final local position and rotation). Nothing else writes the hand transforms.
/// Attach to the hand root (a child of the player camera) and assign one or both hands.
/// </summary>
public class HandController : MonoBehaviour
{
    [Serializable]
    public struct HandPose
    {
        [Tooltip("Position offset from the rest pose (hand root space: x = right, y = up, z = forward). Mirrored on x for the left hand.")]
        public Vector3 position;
        [Tooltip("Rotation offset in degrees, added to the rest rotation. Y and Z are mirrored for the left hand.")]
        public Vector3 rotation;
        [Tooltip("How far the right hand reaches toward the item: 0 = stays at rest, 1 = all the way.")]
        [Range(0f, 1f)] public float reach;

        public HandPose(Vector3 position, Vector3 rotation, float reach)
        {
            this.position = position;
            this.rotation = rotation;
            this.reach = reach;
        }
    }

    [Header("References")]
    [Tooltip("Source of the hand state and reach point. Found in the parents if empty.")]
    public PlayerInteractor interactor;
    [Tooltip("Source of the walking speed for the walk bob. Found in the parents if empty.")]
    public PlayerController player;

    [Header("Hands")]
    public Transform leftHand;
    [Tooltip("The hand that reaches for items.")]
    public Transform rightHand;

    [Header("Rest pose (local to the hand root)")]
    [Tooltip("x = right, y = up, z = forward.")]
    public Vector3 leftRestPos  = new Vector3(-0.22f, -0.28f, 0.45f);
    public Vector3 leftRestRot  = new Vector3(-10f,  25f,  10f);
    public Vector3 rightRestPos = new Vector3( 0.22f, -0.28f, 0.45f);
    public Vector3 rightRestRot = new Vector3(-10f, -25f, -10f);

    [Header("State poses")]
    public HandPose idle       = new HandPose(Vector3.zero, Vector3.zero, 0f);
    public HandPose targeting  = new HandPose(new Vector3(0f, 0.02f, 0.03f), new Vector3(-6f, 0f, 0f), 0.5f);
    public HandPose selected   = new HandPose(new Vector3(0f, 0.03f, 0.05f), new Vector3(-10f, 0f, 5f), 0.6f);
    public HandPose holding    = new HandPose(new Vector3(0f, 0.05f, 0.15f), new Vector3(20f, -10f, -30f), 1f);
    public HandPose axisMoving = new HandPose(new Vector3(0f, 0.04f, 0.12f), new Vector3(10f, 0f, -15f), 0.8f);
    public HandPose rotating   = new HandPose(new Vector3(0f, 0.04f, 0.10f), new Vector3(0f, 0f, -45f), 0.8f);
    public HandPose scaling    = new HandPose(new Vector3(0f, 0.02f, 0.08f), new Vector3(-15f, 0f, 10f), 0.6f);
    [Tooltip("How quickly the hands blend from one state pose to the next. Higher = snappier.")]
    public float poseBlendSpeed = 12f;

    [Header("Reach")]
    [Tooltip("Camera-space point the reach is measured from: at full reach the right hand moves by (item - this point).")]
    public Vector3 reachOrigin = new Vector3(0.28f, -0.32f, 0.5f);

    [Header("Idle breathing (Idle and Targeting only)")]
    [Tooltip("Metres of vertical sway at idle. 0 disables it.")] public float breathAmount = 0.004f;
    [Tooltip("Cycles per second at idle.")]                      public float breathSpeed  = 1.3f;

    [Header("Walk bob (Idle and Targeting only)")]
    public float bobAmount = 0.02f;
    public float bobSpeed  = 7f;
    [Tooltip("Walking speed in m/s above which the walk bob replaces breathing.")]
    public float moveThreshold = 0.1f;

    [Header("Look lean")]
    [Tooltip("How quickly the lean catches up with the camera. Higher = less lean.")]
    public float rotationLag = 12f;
    [Tooltip("How strongly the hands lean opposite to a fast look.")]
    public float lookLeanStrength = 0.004f;

    // The item reach is clamped to this box in camera space, so the hand never leaves the view.
    static readonly Vector3 ReachMin = new Vector3(-0.4f, -0.45f, 0.45f);
    static readonly Vector3 ReachMax = new Vector3( 0.4f,  0.1f,  1f);

    Transform _cam;
    HandClippingPrevention _leftClip, _rightClip;
    Vector3 _posOffset, _rotOffset, _reach;
    float _bobWeight;
    Vector3 _laggedCamEuler;
    float _bobPhase;

    void Awake()
    {
        if (interactor == null) interactor = GetComponentInParent<PlayerInteractor>();
        if (player == null) player = GetComponentInParent<PlayerController>();
        Camera cam = interactor != null && interactor.playerCamera != null ? interactor.playerCamera : Camera.main;
        _cam = cam != null ? cam.transform : transform.parent;
        if (interactor == null)
            Debug.LogWarning("HandController: no PlayerInteractor in the parents, so the hands stay in the idle pose.", this);

        _leftClip = ClipperFor(leftHand);
        _rightClip = ClipperFor(rightHand);
        _bobWeight = 1f;
        _laggedCamEuler = _cam != null ? _cam.eulerAngles : Vector3.zero;
    }

    static HandClippingPrevention ClipperFor(Transform hand)
    {
        if (hand == null) return null;
        var clip = hand.GetComponent<HandClippingPrevention>();
        return clip != null ? clip : hand.gameObject.AddComponent<HandClippingPrevention>();
    }

    void LateUpdate()
    {
        if (leftHand == null && rightHand == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused

        HandState state = interactor != null ? interactor.HandState : HandState.Idle;
        HandPose pose = PoseFor(state);
        float k = 1f - Mathf.Exp(-poseBlendSpeed * dt);

        _posOffset = Vector3.Lerp(_posOffset, pose.position, k);
        _rotOffset = Vector3.Lerp(_rotOffset, pose.rotation, k);
        _bobWeight = Mathf.Lerp(_bobWeight, state == HandState.Idle || state == HandState.Targeting ? 1f : 0f, k);
        _reach = Vector3.Lerp(_reach, ReachOffset(pose.reach), k);

        // Breathing at rest, walk bob while moving.
        bool moving = player != null && player.HorizontalSpeed > moveThreshold;
        _bobPhase += (moving ? bobSpeed : breathSpeed) * dt;
        float amp = (moving ? bobAmount : breathAmount) * _bobWeight;
        float bobY = Mathf.Sin(_bobPhase * 2f) * amp;
        float bobX = Mathf.Cos(_bobPhase) * amp * 0.5f;

        Vector3 lean = LookLean(dt);
        // Reach is measured in camera space; the hands live in the hand root's space.
        Vector3 reachLocal = _cam != null ? transform.InverseTransformVector(_cam.TransformVector(_reach)) : Vector3.zero;

        if (leftHand != null)
        {
            Vector3 mirrored = new Vector3(-_posOffset.x, _posOffset.y, _posOffset.z);
            Vector3 rot = new Vector3(_rotOffset.x, -_rotOffset.y, -_rotOffset.z);
            // Mirror the bob on x so the two hands don't move in lockstep.
            Place(leftHand, _leftClip, leftRestPos + mirrored + new Vector3(bobX, bobY, 0f) + lean, leftRestRot + rot, dt);
        }
        if (rightHand != null)
            Place(rightHand, _rightClip, rightRestPos + _posOffset + reachLocal + new Vector3(-bobX, bobY, 0f) + lean, rightRestRot + _rotOffset, dt);
    }

    HandPose PoseFor(HandState state)
    {
        switch (state)
        {
            case HandState.Targeting:  return targeting;
            case HandState.Selected:   return selected;
            case HandState.Holding:    return holding;
            case HandState.AxisMoving: return axisMoving;
            case HandState.Rotating:   return rotating;
            case HandState.Scaling:    return scaling;
            default:                   return idle;
        }
    }

    /// <summary>Camera-space offset that moves the right hand toward the reach point, scaled by the state's reach.</summary>
    Vector3 ReachOffset(float weight)
    {
        if (weight <= 0f || _cam == null || interactor == null || !interactor.ReachTarget.HasValue) return Vector3.zero;
        Vector3 local = _cam.InverseTransformPoint(interactor.ReachTarget.Value);
        Vector3 goal = new Vector3(
            Mathf.Clamp(local.x, ReachMin.x, ReachMax.x),
            Mathf.Clamp(local.y, ReachMin.y, ReachMax.y),
            Mathf.Clamp(local.z, ReachMin.z, ReachMax.z));
        return (goal - reachOrigin) * weight;
    }

    /// <summary>The hands trail a fast look by a few millimetres and lean opposite to it.</summary>
    Vector3 LookLean(float dt)
    {
        if (_cam == null) return Vector3.zero;
        Vector3 camEuler = _cam.eulerAngles;
        float k = 1f - Mathf.Exp(-rotationLag * dt);
        _laggedCamEuler.x = Mathf.LerpAngle(_laggedCamEuler.x, camEuler.x, k);
        _laggedCamEuler.y = Mathf.LerpAngle(_laggedCamEuler.y, camEuler.y, k);
        _laggedCamEuler.z = Mathf.LerpAngle(_laggedCamEuler.z, camEuler.z, k);
        float yawDelta = Mathf.DeltaAngle(_laggedCamEuler.y, camEuler.y);
        float pitchDelta = Mathf.DeltaAngle(_laggedCamEuler.x, camEuler.x);
        return new Vector3(-yawDelta * lookLeanStrength, pitchDelta * lookLeanStrength, 0f);
    }

    void Place(Transform hand, HandClippingPrevention clip, Vector3 localPos, Vector3 localEuler, float dt)
    {
        hand.localPosition = localPos;
        hand.localRotation = Quaternion.Euler(localEuler);
        if (clip != null && _cam != null)
            hand.position = clip.Resolve(_cam.position, hand.position, dt);
    }
}
