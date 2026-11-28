using System;
using UnityEngine;

/// <summary>
/// Owns the single selected item: only one can be selected at a time, and only the selected item
/// can be moved. The selected item gets an outline (DeepPink #FF1493 by default) drawn by the
/// Lab/InvertedHullOutline shader.
/// </summary>
public class HighlightManager : MonoBehaviour
{
    public static HighlightManager Instance { get; private set; }

    /// <summary>Fires with the newly selected item, or null when the selection is cleared.</summary>
    public event Action<Pickable> SelectionChanged;

    [Tooltip("Material using the Lab/InvertedHullOutline shader. Copied at runtime, so the asset is never modified.")]
    public Material outlineMaterial;

    [Tooltip("Outline colour of the selected item. Default is DeepPink #FF1493.")]
    public Color selectionColor = new Color(1f, 0.0784f, 0.5765f, 1f);

    public Pickable Selected { get; private set; }

    /// <summary>Runtime copy of outlineMaterial tinted with selectionColor. Null if no material is assigned.</summary>
    public Material SelectionMaterial { get; private set; }

    void Awake()
    {
        Instance = this;
        if (outlineMaterial != null) SelectionMaterial = new Material(outlineMaterial);
        else Debug.LogWarning("HighlightManager: no outline material assigned, selected items will not be outlined.", this);
        ApplyColor();
    }

    // Lets the colour be tuned live in the Inspector during Play mode.
    void OnValidate() => ApplyColor();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (SelectionMaterial != null) Destroy(SelectionMaterial);
    }

    void ApplyColor()
    {
        if (SelectionMaterial != null) SelectionMaterial.SetColor("_OutlineColor", selectionColor);
    }

    /// <summary>Right-click on an item: select it, or clear the selection if it is already selected.</summary>
    public void Toggle(Pickable item)
    {
        Select(Selected == item ? null : item);
    }

    /// <summary>Selects the item (null clears). The previous selection loses its outline.</summary>
    public void Select(Pickable item)
    {
        if (Selected == item) return;
        if (Selected != null) Selected.SetSelected(false);
        Selected = item;
        if (Selected != null) Selected.SetSelected(true);
        SelectionChanged?.Invoke(Selected);
    }
}
