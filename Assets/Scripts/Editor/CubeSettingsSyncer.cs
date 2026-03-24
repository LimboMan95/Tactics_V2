using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class CubeSettingsSyncer : EditorWindow
{
    [MenuItem("Tools/Sync Cube Settings to All Scenes")]
    public static void ShowWindow()
    {
        GetWindow<CubeSettingsSyncer>("Sync Cube Settings");
    }

    private void OnGUI()
    {
        GUILayout.Label("Sync DickControlledCube Settings", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        if (GUILayout.Button("Apply Level 9 Settings to All Scenes", GUILayout.Height(40)))
        {
            SyncSettings();
        }
    }

    private static void SyncSettings()
    {
        // 1. Get source values from Level_9
        string sourceScenePath = "Assets/Scenes/Level_9.unity";
        var originalScene = EditorSceneManager.GetActiveScene().path;
        
        EditorSceneManager.OpenScene(sourceScenePath);
        var sourceCube = FindObjectOfType<DickControlledCube>();
        
        if (sourceCube == null)
        {
            Debug.LogError("Could not find DickControlledCube in Level_9!");
            return;
        }

        // Copy values to a temp object or just remember them
        // We use a simplified approach: we define the values we want to sync
        float speed = sourceCube.speed;
        float rotationSpeed = sourceCube.rotationSpeed;
        LayerMask obstacleMask = sourceCube.obstacleMask;
        float checkDistance = sourceCube.checkDistance;
        LayerMask groundMask = sourceCube.groundMask;
        float groundCheckDistance = sourceCube.groundCheckDistance;
        float cubeSize = sourceCube.cubeSize;
        float rigidbodyLinearDamping = sourceCube.rigidbodyLinearDamping;
        float rigidbodyAngularDamping = sourceCube.rigidbodyAngularDamping;
        bool applyZeroFrictionColliderMaterial = sourceCube.applyZeroFrictionColliderMaterial;
        bool enableFallDeath = sourceCube.enableFallDeath;
        float fallDeathWorldY = sourceCube.fallDeathWorldY;
        float maxFallBelowSpawnY = sourceCube.maxFallBelowSpawnY;
        string directionTileTag = sourceCube.directionTileTag;
        float triggerCenterThreshold = sourceCube.triggerCenterThreshold;
        float jumpHeight = sourceCube.jumpHeight;
        float jumpDistance = sourceCube.jumpDistance;
        float jumpDuration = sourceCube.jumpDuration;
        AnimationCurve jumpCurve = sourceCube.jumpCurve;
        string fragileTileTag = sourceCube.fragileTileTag;
        string speedTileTag = sourceCube.speedTileTag;
        float speedMultiplier = sourceCube.speedMultiplier;
        float speedBoostDuration = sourceCube.speedBoostDuration;
        string jumpTileTag = sourceCube.jumpTileTag;
        Color jumpTileHighlightColor = sourceCube.jumpTileHighlightColor;
        Color speedTileHighlightColor = sourceCube.speedTileHighlightColor;
        float smallColliderSize = sourceCube.smallColliderSize;

        // 2. Find all scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        
        foreach (var guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            if (scenePath == sourceScenePath) continue;

            Debug.Log($"Syncing scene: {scenePath}");
            var scene = EditorSceneManager.OpenScene(scenePath);
            var cubes = FindObjectsOfType<DickControlledCube>();

            bool changed = false;
            foreach (var cube in cubes)
            {
                cube.speed = speed;
                cube.rotationSpeed = rotationSpeed;
                cube.obstacleMask = obstacleMask;
                cube.checkDistance = checkDistance;
                cube.groundMask = groundMask;
                cube.groundCheckDistance = groundCheckDistance;
                cube.cubeSize = cubeSize;
                cube.rigidbodyLinearDamping = rigidbodyLinearDamping;
                cube.rigidbodyAngularDamping = rigidbodyAngularDamping;
                cube.applyZeroFrictionColliderMaterial = applyZeroFrictionColliderMaterial;
                cube.enableFallDeath = enableFallDeath;
                cube.fallDeathWorldY = fallDeathWorldY;
                cube.maxFallBelowSpawnY = maxFallBelowSpawnY;
                cube.directionTileTag = directionTileTag;
                cube.triggerCenterThreshold = triggerCenterThreshold;
                cube.jumpHeight = jumpHeight;
                cube.jumpDistance = jumpDistance;
                cube.jumpDuration = jumpDuration;
                cube.jumpCurve = new AnimationCurve(jumpCurve.keys);
                cube.fragileTileTag = fragileTileTag;
                cube.speedTileTag = speedTileTag;
                cube.speedMultiplier = speedMultiplier;
                cube.speedBoostDuration = speedBoostDuration;
                cube.jumpTileTag = jumpTileTag;
                cube.jumpTileHighlightColor = jumpTileHighlightColor;
                cube.speedTileHighlightColor = speedTileHighlightColor;
                cube.smallColliderSize = smallColliderSize;
                
                EditorUtility.SetDirty(cube);
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        // Restore original scene
        if (!string.IsNullOrEmpty(originalScene))
            EditorSceneManager.OpenScene(originalScene);
            
        Debug.Log("Sync completed successfully!");
    }
}
