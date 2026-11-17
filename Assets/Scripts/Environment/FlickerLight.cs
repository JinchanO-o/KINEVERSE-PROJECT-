using UnityEngine;

/// <summary>
/// Makes a ceiling Light flicker at random intervals, with an optional buzz at its position.
/// Attach to a Light. The light's current intensity is treated as its stable value.
/// </summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Tooltip("Seconds between flicker events (min, max).")]
    public Vector2 intervalRange = new Vector2(4f, 10f);
    [Tooltip("Length of one flicker event in seconds (min, max).")]
    public Vector2 flickerDuration = new Vector2(0.15f, 0.6f);
    [Tooltip("Play a buzz sound when a flicker starts.")]
    public bool playSound = true;
    [Tooltip("Buzz volume.")][Range(0f, 1f)] public float soundVolume = 0.25f;

    Light _light;
    float _baseIntensity;
    float _nextEvent;
    float _eventEnd;
    bool _flickering;
    AudioClip _buzz;

    void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _buzz = AudioGenerator.Buzz();
        ScheduleNext();
    }

    void Update()
    {
        if (HUD.IsPaused) return;

        if (!_flickering)
        {
            // Stable state: a very faint wobble so the light never looks dead still.
            _light.intensity = _baseIntensity * (1f + 0.01f * Mathf.Sin(Time.time * 7f));
            if (Time.time >= _nextEvent) StartFlicker();
            return;
        }

        if (Time.time >= _eventEnd)
        {
            _flickering = false;
            _light.intensity = _baseIntensity;
            ScheduleNext();
        }
        else
        {
            bool dip = Random.value < 0.4f;
            _light.intensity = _baseIntensity * (dip ? Random.Range(0.05f, 0.4f) : Random.Range(0.8f, 1.15f));
        }
    }

    void ScheduleNext()
    {
        _nextEvent = Time.time + Random.Range(intervalRange.x, intervalRange.y);
    }

    void StartFlicker()
    {
        _flickering = true;
        _eventEnd = Time.time + Random.Range(flickerDuration.x, flickerDuration.y);
        if (playSound && _buzz != null)
            AudioSource.PlayClipAtPoint(_buzz, transform.position, soundVolume);
    }
}
