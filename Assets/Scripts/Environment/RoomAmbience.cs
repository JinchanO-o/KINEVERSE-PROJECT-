using UnityEngine;

/// <summary>
/// Room atmosphere: a looping room-tone bed (fan rumble and mains hum) and floating dust motes
/// lit by the ceiling lights. Attach to the lab root object.
/// </summary>
public class RoomAmbience : MonoBehaviour
{
    [Tooltip("Centre of the volume where dust is emitted.")]
    public Vector3 roomCentre = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Size of the dust volume in metres.")]
    public Vector3 roomSize = new Vector3(9.6f, 2.8f, 7.6f);
    [Tooltip("Room-tone loop volume.")][Range(0f, 1f)] public float toneVolume = 0.3f;
    [Tooltip("Dust particles emitted per second.")] public float dustRate = 12f;

    void Awake()
    {
        var tone = gameObject.AddComponent<AudioSource>();
        tone.clip = AudioGenerator.RoomTone();
        tone.loop = true;
        tone.spatialBlend = 0f;
        tone.volume = toneVolume;
        tone.Play();

        CreateDust();
    }

    void CreateDust()
    {
        var go = new GameObject("Dust Motes");
        go.transform.SetParent(transform, false);
        go.transform.position = roomCentre;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.03f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
        main.startColor = new Color(1f, 0.95f, 0.85f, 0.35f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = dustRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = roomSize;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.03f;
        noise.frequency = 0.3f;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        var mat = new Material(shader);
        // Transparent blending so the motes fade instead of showing as squares.
        mat.SetFloat("_Surface", 1f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        rend.sharedMaterial = mat;
    }
}
