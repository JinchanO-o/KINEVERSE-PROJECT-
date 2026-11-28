using UnityEngine;

/// <summary>
/// Keeps one first-person hand out of walls. HandController calls Resolve after posing the hand: a ray from the
/// camera to the hand finds any obstacle in between, and the hand is pulled toward the camera to sit just in
/// front of it. Attach to each hand (HandController adds it if missing).
/// </summary>
[DisallowMultipleComponent]
public class HandClippingPrevention : MonoBehaviour
{
    [Tooltip("Gap kept between the hand and the surface (metres).")]
    public float skin = 0.08f;
    [Tooltip("How fast the hand slides back out once the obstacle is gone (metres per second). It always pulls in at once.")]
    public float releaseSpeed = 1.5f;
    [Tooltip("Layers that count as obstacles. Player, Pickable and Ignore Raycast are removed automatically: the hand may touch the items it reaches for.")]
    public LayerMask obstacleMask = ~0;

    float _pull; // metres the hand is pulled toward the camera

    void Awake()
    {
        obstacleMask &= ~LayerMask.GetMask("Player", "Pickable", "Ignore Raycast");
    }

    /// <summary>Returns the hand position, pulled toward the camera if an obstacle is between them.</summary>
    public Vector3 Resolve(Vector3 cameraPos, Vector3 wanted, float dt)
    {
        Vector3 toHand = wanted - cameraPos;
        float dist = toHand.magnitude;
        if (dist < 0.0001f) return wanted;
        Vector3 dir = toHand / dist;

        float pull = 0f;
        if (Physics.Raycast(cameraPos, dir, out RaycastHit hit, dist + skin, obstacleMask, QueryTriggerInteraction.Ignore))
            pull = dist - Mathf.Max(0f, hit.distance - skin);

        // Measured along the camera ray rather than smoothed in world space, so the hand never trails the camera.
        // Pulling in is instant (never visible inside a wall); sliding back out is eased so it does not pop.
        _pull = pull >= _pull ? pull : Mathf.MoveTowards(_pull, pull, releaseSpeed * dt);
        return cameraPos + dir * Mathf.Max(0f, dist - _pull);
    }
}
