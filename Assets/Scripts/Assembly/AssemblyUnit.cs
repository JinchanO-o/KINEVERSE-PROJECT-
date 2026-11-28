using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One grabbable thing in an assembly: a single loose piece, a partial group of snapped pieces, or a finished
/// recipe result. It is the root of its pieces and carries the only Pickable, so the group is grabbed, moved,
/// rotated and resized as one. Its rotation is the template frame, so its gizmo axes (and the X / Y / Z keys)
/// are the template's axes. Created, merged and destroyed at runtime by AssemblyManager.
/// </summary>
public class AssemblyUnit : MonoBehaviour
{
    /// <summary>Recipe this unit is an input of (or a partial group of). Null once the final result is built.</summary>
    public AssemblyRecipe Recipe { get; set; }

    /// <summary>Pieces per filled input slot of Recipe.</summary>
    public readonly Dictionary<int, List<AssemblyPiece>> Slots = new Dictionary<int, List<AssemblyPiece>>();

    public Pickable Pickable { get; set; }

    /// <summary>Snapping or turning into a result: not selectable and not a snap partner.</summary>
    public bool Busy { get; set; }

    // ---------- Research log (see AssemblyManager) ----------

    /// <summary>Time.time when the player first grabbed or axis-moved this unit, or -1 if never.</summary>
    public float FirstHandledTime { get; set; } = -1f;
    /// <summary>How many times the player started grabbing, axis-moving or rotating this unit.</summary>
    public int Attempts { get; set; }
    /// <summary>The current (or last) attempt used axis mode: an X / Y / Z key with an LMB or RMB drag.</summary>
    public bool UsedAxisMode { get; set; }

    public IEnumerable<AssemblyPiece> Pieces
    {
        get
        {
            foreach (List<AssemblyPiece> list in Slots.Values)
                foreach (AssemblyPiece p in list)
                    if (p != null) yield return p;
        }
    }

    /// <summary>Any piece of the unit: all of them share one template frame.</summary>
    public AssemblyPiece Anchor
    {
        get
        {
            foreach (AssemblyPiece p in Pieces) return p;
            return null;
        }
    }

    public bool IsComplete => Recipe != null && Slots.Count == Recipe.inputs.Count;

    /// <summary>Size used to scale the snap thresholds: twice Pickable.BoundingRadius, independent of rotation.</summary>
    public float Size => Pickable != null ? Pickable.BoundingRadius * 2f : 1f;

    /// <summary>Longest side, in metres, of the pieces' visual bounds in the unit's own (template-aligned) frame.</summary>
    public float Length
    {
        get
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (AssemblyPiece p in Pieces) p.EncapsulateInto(transform, ref bounds, ref any);
            Vector3 size = Vector3.Scale(bounds.size, AssemblyBounds.Abs(transform.lossyScale));
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }
    }

    /// <summary>Average metres per template unit over all pieces.</summary>
    public float AverageScale
    {
        get
        {
            float sum = 0f;
            int n = 0;
            foreach (AssemblyPiece p in Pieces) { sum += p.Scale; n++; }
            return n > 0 ? sum / n : 1f;
        }
    }

    public void AddSlot(int slot, IEnumerable<AssemblyPiece> pieces)
    {
        if (!Slots.TryGetValue(slot, out List<AssemblyPiece> list))
            Slots[slot] = list = new List<AssemblyPiece>();
        foreach (AssemblyPiece p in pieces)
        {
            list.Add(p);
            p.Unit = this;
        }
    }
}
