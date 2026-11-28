using UnityEngine;

/// <summary>
/// Procedurally generated placeholder sounds, so the project needs no external audio files.
/// Each clip is built once and cached. Components call these when no clip is assigned.
/// </summary>
public static class AudioGenerator
{
    const int Rate = 44100;
    const float TwoPi = 6.2831853f;

    static AudioClip _footstep, _grab, _release, _collision, _buzz, _roomTone;

    public static AudioClip Footstep()
    {
        if (_footstep == null)
            _footstep = Make("Footstep", 0.2f, t =>
                (Random.value * 2f - 1f) * Mathf.Exp(-t * 45f) * 0.7f +
                Mathf.Sin(TwoPi * 95f * t) * Mathf.Exp(-t * 22f) * 0.5f);
        return _footstep;
    }

    /// <summary>Short rising click.</summary>
    public static AudioClip Grab()
    {
        if (_grab == null)
            _grab = Make("Grab", 0.15f, t =>
                Mathf.Sin(TwoPi * (600f * t + 2500f * t * t)) * Mathf.Exp(-t * 35f) * 0.6f);
        return _grab;
    }

    /// <summary>Soft falling tone with a little air.</summary>
    public static AudioClip Release()
    {
        if (_release == null)
            _release = Make("Release", 0.25f, t =>
                Mathf.Sin(TwoPi * (500f * t - 1500f * t * t)) * Mathf.Exp(-t * 18f) * 0.4f +
                (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.15f);
        return _release;
    }

    /// <summary>Low thud for an item hitting a surface.</summary>
    public static AudioClip Collision()
    {
        if (_collision == null)
            _collision = Make("Collision", 0.35f, t =>
                Mathf.Sin(TwoPi * 110f * t) * Mathf.Exp(-t * 14f) * 0.8f +
                (Random.value * 2f - 1f) * Mathf.Exp(-t * 30f) * 0.5f);
        return _collision;
    }

    /// <summary>Electrical buzz for a flickering light.</summary>
    public static AudioClip Buzz()
    {
        if (_buzz == null)
            _buzz = Make("Buzz", 0.4f, t =>
                (Mathf.Sin(TwoPi * 120f * t) * 0.5f + (Random.value * 2f - 1f) * 0.15f) *
                (0.6f + 0.4f * Mathf.Sin(TwoPi * 18f * t)));
        return _buzz;
    }

    /// <summary>
    /// 4-second seamless loop: brown-ish fan rumble plus 60 Hz mains hum and its harmonic.
    /// The hum uses whole cycles over the loop length, and the tail is crossfaded into the head.
    /// </summary>
    public static AudioClip RoomTone()
    {
        if (_roomTone != null) return _roomTone;

        const float seconds = 4f;
        int n = Mathf.RoundToInt(seconds * Rate);
        var d = new float[n];
        float rumble = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            rumble = rumble * 0.995f + (Random.value * 2f - 1f) * 0.02f;
            float hum = 0.04f * Mathf.Sin(TwoPi * 60f * t) + 0.015f * Mathf.Sin(TwoPi * 120f * t);
            d[i] = hum + rumble * 0.25f;
        }

        int fade = Mathf.RoundToInt(0.25f * Rate);
        int loopLen = n - fade;
        var outData = new float[loopLen];
        for (int i = 0; i < loopLen; i++)
        {
            outData[i] = i < fade
                ? Mathf.Lerp(d[n - fade + i], d[i], i / (float)fade)
                : d[i];
        }

        _roomTone = AudioClip.Create("RoomTone", loopLen, 1, Rate, false);
        _roomTone.SetData(outData, 0);
        return _roomTone;
    }

    static AudioClip Make(string clipName, float seconds, System.Func<float, float> sample)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Rate));
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);

        var clip = AudioClip.Create(clipName, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
