using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a completed recipe turns into its result.</summary>
public enum AssemblyEffect
{
    /// <summary>The pieces fly together toward their centre, then a pop, then the result appears.</summary>
    FlyToCenter,
    /// <summary>The pieces glow and pulse in place, then a pop swaps them for the result.</summary>
    GlowSwap,
}

/// <summary>The length the snap thresholds of a recipe are fractions of.</summary>
public enum ThresholdSize
{
    /// <summary>Bounding-sphere diameter of the smaller of the two parts (suits pieces of similar size).</summary>
    SmallerPartDiameter,
    /// <summary>Longest side of the larger part (suits joining big sub-assemblies, e.g. dragon onto bridge).</summary>
    LargerPartLength,
}

/// <summary>Which side of the stationary piece the moving piece must come from, along the insertion axis.</summary>
public enum ApproachSide { Either, Positive, Negative }

/// <summary>
/// One step of an assembly tree, e.g. "4 deck pieces along X -> mat_cau_1". Inputs are template node names
/// (bound to scene pieces of the same name) or the results of other recipes, so a tree like
/// "X+Y->Q, Z+C->W, V+B->E, Q+W+E->A1" needs only new recipe assets, no code.
/// Target poses are never stored here: they come from the template (see AssemblyManager).
/// </summary>
[CreateAssetMenu(menuName = "Assembly/Recipe", fileName = "Recipe")]
public class AssemblyRecipe : ScriptableObject
{
    [Serializable]
    public class Input
    {
        [Tooltip("Name of a node in the template, e.g. mat_cau.002. Ignored when From Recipe is set.")]
        public string templateNode;
        [Tooltip("Use the result of another recipe as this input instead of a template node.")]
        public AssemblyRecipe fromRecipe;

        public string Label => fromRecipe != null ? fromRecipe.DisplayName : templateNode;
    }

    /// <summary>Two inputs that may join directly. Only adjacent inputs snap together.</summary>
    [Serializable]
    public class Joint
    {
        [Tooltip("Index into Inputs.")] public int a;
        [Tooltip("Index into Inputs.")] public int b = 1;
        [Tooltip("Insertion axis in the template frame. Every piece is turned so its local axes are the template's, " +
                 "so this is also the X / Y / Z key and gizmo arrow the player sees.")]
        public Axis axis = Axis.X;
        [Tooltip("Side of input b that input a must come from along the axis. Either = both ways. " +
                 "When b is the moving one, the opposite side applies.")]
        public ApproachSide approach = ApproachSide.Either;
    }

    [Tooltip("Shown in messages and used to name the assembled group. Defaults to the asset name.")]
    public string displayName;
    public List<Input> inputs = new List<Input>();
    public List<Joint> joints = new List<Joint>();

    [Header("Snap (fractions of the length picked by Threshold Size)")]
    [Tooltip("Length the corridor, snap and guide distances are fractions of.")]
    public ThresholdSize thresholdSize = ThresholdSize.SmallerPartDiameter;
    [Tooltip("Largest sideways distance from the insertion line.")]
    [Range(0.01f, 1f)] public float corridorRadius = 0.15f;
    [Tooltip("Largest distance along the insertion axis from the target pose at which the part snaps.")]
    [Range(0.01f, 2f)] public float snapDistance = 0.35f;
    [Tooltip("Distance along the axis within which the guide line shows and the parts may pass through each other.")]
    [Range(0.05f, 5f)] public float guideDistance = 1.5f;
    [Tooltip("Largest rotation error, in degrees, versus the target relative rotation.")]
    [Range(1f, 60f)] public float maxAngleDegrees = 15f;
    [Tooltip("Snap only while the player holds the X / Y / Z key matching the insertion axis.")]
    public bool requireAxisMode;

    [Header("Magnet assist")]
    [Tooltip("Pull the moving part into place once it is near its target on the right side, even if the player lets go.")]
    public bool magnet;
    [Tooltip("Distance from the target pose, as a fraction of the larger part's length, within which the magnet pulls.")]
    [Range(0.02f, 1f)] public float magnetRadius = 0.25f;
    [Tooltip("Largest rotation error, in degrees, the magnet still corrects. Beyond it the HUD asks the player to rotate.")]
    [Range(1f, 90f)] public float magnetMaxAngle = 45f;
    [Tooltip("Seconds the pull takes to ease the part into its exact pose.")]
    [Range(0.1f, 2f)] public float magnetSeconds = 0.4f;
    [Tooltip("Dragging the part away from its target faster than this (metres per second) breaks the pull.")]
    [Min(0.1f)] public float magnetBreakSpeed = 6f;

    [Header("Result")]
    [Tooltip("Template node that replaces the pieces when the recipe is complete (the template root's name for the " +
             "whole model). Empty: the locked group itself is the result.")]
    public string resultTemplateNode;
    public AssemblyEffect effect = AssemblyEffect.FlyToCenter;
    [Tooltip("HUD message when the recipe is completed. Empty: \"<name> assembled\".")]
    public string completionMessage;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>The joint linking inputs i and j (either order), or null if they are not adjacent.</summary>
    public Joint FindJoint(int i, int j)
    {
        foreach (Joint joint in joints)
            if ((joint.a == i && joint.b == j) || (joint.a == j && joint.b == i)) return joint;
        return null;
    }

    void OnValidate()
    {
        foreach (Joint joint in joints)
        {
            if (joint.a < 0 || joint.a >= inputs.Count || joint.b < 0 || joint.b >= inputs.Count || joint.a == joint.b)
                Debug.LogWarning($"{name}: joint {joint.a}-{joint.b} does not link two different inputs.", this);
        }
        foreach (Input input in inputs)
            if (input.fromRecipe == this) Debug.LogWarning($"{name}: an input uses this recipe's own result.", this);
    }
}
