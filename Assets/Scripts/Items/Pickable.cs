using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A grabbable prop. Items have NO gravity and no Rigidbody: PlayerInteractor moves them with
/// sphere sweeps, so they stay exactly where they are released.
/// Emits events (Grabbed, Released, Collided) so HUD, sound and camera shake stay decoupled.
/// Attach to any object with a Collider. Its layer is set to "Pickable" automatically.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Pickable : MonoBehaviour
{
    public event Action<Pickable> Grabbed;
    public event Action<Pickable> Released;
    public event Action<Pickable, float> Collided; // impact speed in m/s

    /// <summary>Registry of every item in the scene (a simple group).</summary>
    public static readonly List<Pickable> All = new List<Pickable>();

    [Header("Weight")]
    [Tooltip("Heavier items (higher mass) lag further behind the hand.")]
    [Min(0.1f)] public float mass = 1f;
    [Tooltip("Follow speed for the lightest item (mass 0.5). Higher = less lag.")]
    public float lightFollowSpeed = 18f;
    [Tooltip("Follow speed for the heaviest item (mass 10). Lower = more lag.")]
    public float heavyFollowSpeed = 4f;

    [Header("Sounds (generated procedurally when empty)")]
    public AudioClip grabSound;
    public AudioClip releaseSound;
    public AudioClip collisionSound;

    public bool IsHeld { get; private set; }

    /// <summary>Follow smoothing used by PlayerInteractor, derived from mass.</summary>
    public float FollowSpeed =>
        Mathf.Lerp(lightFollowSpeed, heavyFollowSpeed, Mathf.InverseLerp(0.5f, 10f, mass));

    /// <summary>Sweep radius: half of the smallest collider extent.</summary>
    public float Radius => _radius;

    Collider _col;
    float _radius;
    GameObject _outline;

    void Awake()
    {
        _col = GetComponent<Collider>();
        Vector3 e = _col.bounds.extents;
        _radius = Mathf.Max(0.01f, Mathf.Min(e.x, Mathf.Min(e.y, e.z)));

        int pickableLayer = LayerMask.NameToLayer("Pickable");
        if (pickableLayer >= 0) gameObject.layer = pickableLayer;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void NotifyGrabbed()
    {
        IsHeld = true;
        if (grabSound == null) grabSound = AudioGenerator.Grab();
        PlayAt(grabSound, 0.7f);
        Grabbed?.Invoke(this);
    }

    public void NotifyReleased()
    {
        IsHeld = false;
        if (releaseSound == null) releaseSound = AudioGenerator.Release();
        PlayAt(releaseSound, 0.6f);
        Released?.Invoke(this);
    }

    public void NotifyCollided(float impactSpeed)
    {
        if (impactSpeed < 0.1f) return;
        if (collisionSound == null) collisionSound = AudioGenerator.Collision();
        float v = Mathf.Clamp01(impactSpeed / 3f);
        PlayAt(collisionSound, Mathf.Lerp(0.2f, 1f, v));
        Collided?.Invoke(this, impactSpeed);
    }

    /// <summary>Shows or hides the inverted-hull outline child.</summary>
    public void SetHighlighted(bool on)
    {
        if (on) EnsureOutline();
        if (_outline != null) _outline.SetActive(on);
    }

    void EnsureOutline()
    {
        if (_outline != null) return;
        var source = GetComponent<MeshFilter>();
        var mat = HighlightManager.Instance != null ? HighlightManager.Instance.outlineMaterial : null;
        if (source == null || mat == null)
        {
            Debug.LogWarning($"{name}: cannot outline (needs a MeshFilter and a HighlightManager outline material).", this);
            return;
        }

        // Inverted hull: a child copy of the mesh rendered with Cull Front, see Lab/InvertedHullOutline.
        _outline = new GameObject("Outline");
        _outline.layer = LayerMask.NameToLayer("Ignore Raycast");
        _outline.transform.SetParent(transform, false);
        _outline.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        var r = _outline.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        _outline.SetActive(false);
    }

    /// <summary>Plays a one-shot clip at the item's position (3D, distance-attenuated).</summary>
    void PlayAt(AudioClip clip, float volume)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}
