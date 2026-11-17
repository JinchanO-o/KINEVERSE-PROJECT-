using UnityEngine;

/// <summary>
/// Prevents a first-person hand from clipping through walls. Each frame it draws a ray
/// from the camera to where the hand WANTS to be (as set by HandController) and, if an
/// obstacle is in the way, slides the hand back to sit just in front of the surface.
/// Attach to each hand transform (Left_Hand and Right_Hand).
/// Runs AFTER HandController so its result sticks for the frame.
/// </summary>
[DefaultExecutionOrder(100)]
public class HandClippingPrevention : MonoBehaviour
{
    [Tooltip("Gap kept between the hand and the surface (metres).")]
    public float skin = 0.08f;
    [Tooltip("How quickly the hand slides back/forward. Higher = snappier.")]
    public float followSpeed = 20f;
    [Tooltip("Layers that count as obstacles. Player and Ignore Raycast are removed automatically.")]
    public LayerMask obstacleMask = ~0;

    Transform _cam;
    Vector3 _smoothedWorldPos;
    bool _hasSmoothed;

    void Awake()
    {
        _cam = Camera.main != null ? Camera.main.transform : (transform.parent != null ? transform.parent : null);

        int player = LayerMask.NameToLayer("Player");
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (player >= 0) obstacleMask &= ~(1 << player);
        if (ignore >= 0) obstacleMask &= ~(1 << ignore);
    }

    void LateUpdate()
    {
        if (_cam == null) return;

        // HandController has already written transform.position this frame.
        // We treat that as the "wanted" world position and pull it back if a wall is between camera and hand.
        Vector3 wantedWorld = transform.position;
        Vector3 camPos = _cam.position;
        Vector3 toHand = wantedWorld - camPos;
        float wantedDist = toHand.magnitude;
        if (wantedDist < 0.0001f) return;

        Vector3 dir = toHand / wantedDist;
        Vector3 safeWorld = wantedWorld;

        if (Physics.Raycast(camPos, dir, out RaycastHit hit, wantedDist + skin, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            float safeDist = Mathf.Max(0f, hit.distance - skin);
            safeWorld = camPos + dir * safeDist;
        }

        if (!_hasSmoothed) { _smoothedWorldPos = safeWorld; _hasSmoothed = true; }
        float dt = Time.deltaTime;
        float k = dt > 0f ? 1f - Mathf.Exp(-followSpeed * dt) : 1f;
        _smoothedWorldPos = Vector3.Lerp(_smoothedWorldPos, safeWorld, k);
        transform.position = _smoothedWorldPos;
    }
}
