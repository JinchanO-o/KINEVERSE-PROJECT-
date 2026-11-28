using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor window that builds the first-person hands rig:
///   Main Camera
///     Hands_Root            (HandController: poses both hands from PlayerInteractor.HandState)
///       Left_Hand           (HandClippingPrevention, called by HandController)
///       Right_Hand          (HandClippingPrevention; this hand also reaches for items)
///
/// Open from the menu: Tools > Player > Setup Hands.
/// Assign a camera and both hand prefabs, then click Build Hand Rig.
/// Re-running destroys and rebuilds the rig under the chosen camera.
/// </summary>
public class PlayerSetup : EditorWindow
{
    Camera _targetCamera;
    GameObject _leftHandPrefab;
    GameObject _rightHandPrefab;
    Vector3 _leftRestPos  = new Vector3(-0.22f, -0.28f, 0.45f);
    Vector3 _leftRestRot  = new Vector3(-10f,  25f,  10f);
    Vector3 _rightRestPos = new Vector3( 0.22f, -0.28f, 0.45f);
    Vector3 _rightRestRot = new Vector3(-10f, -25f, -10f);
    float _handScale = 1f;

    [MenuItem("Tools/Player/Setup Hands")]
    public static void Open() => GetWindow<PlayerSetup>("Setup Hands");

    void OnEnable()
    {
        if (_targetCamera == null) _targetCamera = Camera.main;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("First-Person Hand Rig", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Assign a camera and both hand prefabs. Click Build Hand Rig.\n" +
            "The script creates Hands_Root under the camera, instantiates the prefabs as Left_Hand and Right_Hand,\n" +
            "and wires up HandController + HandClippingPrevention.\n" +
            "Use the player camera, so HandController finds PlayerInteractor and PlayerController in its parents.", MessageType.Info);

        _targetCamera    = (Camera)EditorGUILayout.ObjectField("Target Camera", _targetCamera, typeof(Camera), true);
        _leftHandPrefab  = (GameObject)EditorGUILayout.ObjectField("Left Hand Prefab",  _leftHandPrefab,  typeof(GameObject), false);
        _rightHandPrefab = (GameObject)EditorGUILayout.ObjectField("Right Hand Prefab", _rightHandPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rest pose (local to Hands_Root)", EditorStyles.boldLabel);
        _leftRestPos  = EditorGUILayout.Vector3Field("Left Rest Pos",  _leftRestPos);
        _leftRestRot  = EditorGUILayout.Vector3Field("Left Rest Rot",  _leftRestRot);
        _rightRestPos = EditorGUILayout.Vector3Field("Right Rest Pos", _rightRestPos);
        _rightRestRot = EditorGUILayout.Vector3Field("Right Rest Rot", _rightRestRot);
        _handScale    = EditorGUILayout.FloatField("Hand Scale", _handScale);

        EditorGUILayout.Space();
        GUI.enabled = _targetCamera != null && (_leftHandPrefab != null || _rightHandPrefab != null);
        if (GUILayout.Button("Build Hand Rig", GUILayout.Height(28)))
            Build(_targetCamera, _leftHandPrefab, _rightHandPrefab,
                  _leftRestPos, _leftRestRot, _rightRestPos, _rightRestRot, _handScale);
        GUI.enabled = true;

        EditorGUILayout.Space();
        if (GUILayout.Button("Remove Existing Hands_Root"))
            RemoveExisting(_targetCamera);
    }

    public static void Build(Camera cam,
        GameObject leftPrefab, GameObject rightPrefab,
        Vector3 leftPos, Vector3 leftRot,
        Vector3 rightPos, Vector3 rightRot,
        float scale)
    {
        if (cam == null) { Debug.LogError("PlayerSetup: no camera assigned."); return; }

        RemoveExisting(cam);

        var root = new GameObject("Hands_Root");
        root.transform.SetParent(cam.transform, false);

        var ctrl = root.AddComponent<HandController>();
        ctrl.interactor = cam.GetComponentInParent<PlayerInteractor>();
        ctrl.player = cam.GetComponentInParent<PlayerController>();
        if (ctrl.interactor == null)
            Debug.LogWarning("PlayerSetup: no PlayerInteractor above '" + cam.name + "', so the hands will stay in the idle pose.");
        ctrl.leftRestPos = leftPos;
        ctrl.leftRestRot = leftRot;
        ctrl.rightRestPos = rightPos;
        ctrl.rightRestRot = rightRot;

        if (leftPrefab != null)
        {
            var lh = (GameObject)PrefabUtility.InstantiatePrefab(leftPrefab, root.transform);
            lh.name = "Left_Hand";
            lh.transform.localPosition = leftPos;
            lh.transform.localRotation = Quaternion.Euler(leftRot);
            lh.transform.localScale = Vector3.one * scale;
            if (lh.GetComponent<HandClippingPrevention>() == null) lh.AddComponent<HandClippingPrevention>();
            ctrl.leftHand = lh.transform;
        }
        if (rightPrefab != null)
        {
            var rh = (GameObject)PrefabUtility.InstantiatePrefab(rightPrefab, root.transform);
            rh.name = "Right_Hand";
            rh.transform.localPosition = rightPos;
            rh.transform.localRotation = Quaternion.Euler(rightRot);
            rh.transform.localScale = Vector3.one * scale;
            if (rh.GetComponent<HandClippingPrevention>() == null) rh.AddComponent<HandClippingPrevention>();
            ctrl.rightHand = rh.transform;
        }

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(cam.gameObject);
        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log("PlayerSetup: built Hands_Root under '" + cam.name + "'.");
    }

    public static void RemoveExisting(Camera cam)
    {
        if (cam == null) return;
        var existing = cam.transform.Find("Hands_Root");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        }
    }
}
