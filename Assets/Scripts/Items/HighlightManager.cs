using System;
using UnityEngine;

/// <summary>
/// Owns the single highlighted item: only one can be outlined at a time.
/// The outline (orange #FF8800 by default) and its gentle pulse live in the
/// Lab/InvertedHullOutline shader on the outline material.
/// </summary>
public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance { get; private set; }

    /// <summary>Fires with the newly highlighted item, or null when cleared.</summary>
    public event Action<Pickable> HighlightChanged;

    [Tooltip("Material using the Lab/InvertedHullOutline shader. Created by Tools > Lab > Build Main Scene.")]
    public Material outlineMaterial;

    [Tooltip("Outline colour. Default is #FF8800.")]
    public Color outlineColor = new Color(1f, 0.5333f, 0f, 1f);

    public Pickable Current { get; private set; }

    void Awake()
    {
        Instance = this;
        ApplyColor();
    }

    void OnValidate() => ApplyColor();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void ApplyColor()
    {
        if (outlineMaterial != null) outlineMaterial.SetColor("_OutlineColor", outlineColor);
    }

    /// <summary>Right-click behaviour: highlight the item, or remove the highlight if it is already on.</summary>
    public void Toggle(Pickable item)
    {
        if (item == null) return;
        SetHighlight(Current == item ? null : item);
    }

    public void SetHighlight(Pickable item)
    {
        if (Current == item) return;
        if (Current != null) Current.SetHighlighted(false);
        Current = item;
        if (Current != null) Current.SetHighlighted(true);
        HighlightChanged?.Invoke(Current);
    }
}
