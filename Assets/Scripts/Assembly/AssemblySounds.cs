using UnityEngine;

/// <summary>
/// Procedural assembly sounds in the style of AudioGenerator (no audio files): a mechanical click when a piece
/// locks, a soft pop when a recipe turns into its result, and a low buzz for a wrong pair. Built once and cached.
/// </summary>
public static class AssemblySounds
{
    const int Rate = 44100;
    const float TwoPi = 6.2831853f;

    static AudioClip _click, _pop, _wrong;

    /// <summary>Two fast metallic ticks, like a latch closing.</summary>
    public static AudioClip Click()
    {
        if (_click == null)
            _click = Make("Assembly Click", 0.12f, t =>
                Tick(t, 2400f, 90f) * 0.7f + Tick(t - 0.035f, 1800f, 110f) * 0.5f);
        return _click;
    }

    /// <summary>Rising bubble with a bright tail.</summary>
    public static AudioClip Pop()
    {
        if (_pop == null)
            _pop = Make("Assembly Pop", 0.4f, t =>
                Mathf.Sin(TwoPi * (220f * t + 900f * t * t)) * Mathf.Exp(-t * 9f) * 0.6f +
                Mathf.Sin(TwoPi * 1320f * t) * Mathf.Exp(-t * 14f) * 0.2f);
        return _pop;
    }

    /// <summary>Short low double buzz for parts that do not fit.</summary>
    public static AudioClip Wrong()
    {
        if (_wrong == null)
            _wrong = Make("Assembly Wrong", 0.3f, t =>
                Mathf.Sign(Mathf.Sin(TwoPi * 140f * t)) * 0.25f * (t < 0.12f || t > 0.17f ? 1f : 0f) * Mathf.Exp(-t * 6f));
        return _wrong;
    }

    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 0.8f)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    static float Tick(float t, float frequency, float decay) =>
        t < 0f ? 0f : (Mathf.Sin(TwoPi * frequency * t) + (Random.value * 2f - 1f) * 0.4f) * Mathf.Exp(-t * decay);

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
