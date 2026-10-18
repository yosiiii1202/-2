using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PachinkoPrototypeBuilder
{
    private const string ScenePath = "Assets/Scenes/PachinkoPrototype.unity";
    private const string BallPrefabPath = "Assets/Prefab/Ball.prefab";
    private const string PinPrefabPath = "Assets/Prefab/Pin.prefab";

    [MenuItem("Pachinko/Build Playable Prototype")]
    public static void Build()
    {
        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
        GameObject pinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PinPrefabPath);
        if (ballPrefab == null || pinPrefab == null)
        {
            Debug.LogError("Ball.prefab または Pin.prefab が見つかりません。Assets/Prefab を確認してください。");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildLighting();
        BuildCamera();
        GameObject board = new GameObject("PachinkoBoard");
        BuildBoardVisuals(board.transform);

        GameObject pinRoot = new GameObject("Pins");
        pinRoot.transform.SetParent(board.transform, false);
        PinSpawner3D spawner = pinRoot.AddComponent<PinSpawner3D>();
        SerializedObject spawnerData = new SerializedObject(spawner);
        spawnerData.FindProperty("pinPrefab").objectReferenceValue = pinPrefab;
        spawnerData.FindProperty("rows").intValue = 9;
        spawnerData.FindProperty("columns").intValue = 10;
        spawnerData.FindProperty("spacingX").floatValue = 0.68f;
        spawnerData.FindProperty("spacingY").floatValue = 0.68f;
        spawnerData.FindProperty("pinZ").floatValue = 0.16f;
        spawnerData.ApplyModifiedPropertiesWithoutUndo();

        GameObject ballRoot = new GameObject("LiveBalls");
        ballRoot.transform.SetParent(board.transform, false);
        GameObject launcher = new GameObject("Launcher");
        launcher.transform.SetParent(board.transform, false);
        Transform launchPoint = launcher.transform;
        launchPoint.localPosition = new Vector3(-3.3f, -4.15f, 0f);

        PachinkoGameController game = board.AddComponent<PachinkoGameController>();
        SerializedObject gameData = new SerializedObject(game);
        gameData.FindProperty("ballPrefab").objectReferenceValue = ballPrefab;
        gameData.FindProperty("launchPoint").objectReferenceValue = launchPoint;
        gameData.FindProperty("ballContainer").objectReferenceValue = ballRoot.transform;
        gameData.ApplyModifiedPropertiesWithoutUndo();

        BuildPayoutLanes(board.transform, game);
        BuildHeso(board.transform, game);
        BuildExitZone(board.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Playable pachinko prototype created at " + ScenePath);
    }

    private static void BuildLighting()
    {
        RenderSettings.ambientLight = new Color(0.28f, 0.34f, 0.48f);
        GameObject lightObject = new GameObject("Key Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.73f, 0.83f, 1f);
        light.intensity = 1.8f;
        lightObject.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
    }

    private static void BuildCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -14f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5.3f;
        camera.rect = new Rect(0f, 0f, 0.55f, 0.9f);
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.1f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<AudioListener>();
    }

    private static void BuildBoardVisuals(Transform root)
    {
        Material backplate = MakeMaterial("Pachinko Deep Navy", new Color(0.035f, 0.09f, 0.18f), 0.25f);
        Material rail = MakeMaterial("Pachinko Brass", new Color(0.9f, 0.56f, 0.18f), 0.45f);
        Material trim = MakeMaterial("Pachinko Blue Trim", new Color(0.12f, 0.58f, 0.92f), 0.35f);

        CreateCube("Backplate", root, Vector3.zero + new Vector3(0f, 0f, 0.68f), new Vector3(7.75f, 9.55f, 0.2f), backplate);
        CreateCube("Left Rail", root, new Vector3(-3.82f, 0f, 0.1f), new Vector3(0.18f, 9.7f, 0.5f), rail);
        CreateCube("Right Rail", root, new Vector3(3.82f, 0f, 0.1f), new Vector3(0.18f, 9.7f, 0.5f), rail);
        CreateCube("Top Rail", root, new Vector3(0f, 4.78f, 0.1f), new Vector3(7.75f, 0.18f, 0.5f), rail);
        CreateCube("Bottom Rail", root, new Vector3(0f, -4.78f, 0.1f), new Vector3(7.75f, 0.18f, 0.5f), rail);

        for (int i = 0; i <= 7; i++)
        {
            float x = -3.85f + i * 1.1f;
            if (i == 0 || i == 7)
            {
                continue;
            }

            GameObject divider = CreateCube("Prize Lane Divider " + i, root, new Vector3(x, -4.4f, 0.1f), new Vector3(0.07f, 0.72f, 0.4f), rail);
            divider.GetComponent<BoxCollider>().isTrigger = false;
        }

        CreateCube("Prize Lane Floor", root, new Vector3(0f, -4.72f, 0.12f), new Vector3(7.7f, 0.14f, 0.5f), trim);
        for (int i = 0; i < 7; i++)
        {
            if (i == 3) continue;
            float x = -3.3f + i * 1.1f;
            GameObject label = CreateCube("Prize Marker " + (i + 1), root, new Vector3(x, -4.02f, 0.22f), new Vector3(0.92f, 0.12f, 0.12f), MakeMaterial("Prize " + i, LaneColor(i), 0.3f));
            label.GetComponent<BoxCollider>().enabled = false;
        }
    }

    private static void BuildPayoutLanes(Transform root, PachinkoGameController game)
    {
        string[] names = { "入賞口", "チューリップ", "アタッカー", "ヘソ抽選", "アタッカー", "チューリップ", "入賞口" };
        for (int i = 0; i < 7; i++)
        {
            if (i == 3) continue;
            GameObject gate = new GameObject("Payout Gate " + (i + 1) + " - " + names[i]);
            gate.transform.SetParent(root, false);
            gate.transform.localPosition = new Vector3(-3.3f + i * 1.1f, -4.31f, 0f);
            BoxCollider trigger = gate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.02f, 0.5f, 0.6f);
            PachinkoPayoutGate payoutGate = gate.AddComponent<PachinkoPayoutGate>();
            payoutGate.Configure(game, i, names[i]);
        }
    }

    private static void BuildHeso(Transform root, PachinkoGameController game)
    {
        GameObject heso = new GameObject("Heso - Hold Trigger");
        heso.transform.SetParent(root, false);
        heso.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        BoxCollider trigger = heso.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.15f, 0.42f, 0.8f);
        PachinkoPayoutGate payoutGate = heso.AddComponent<PachinkoPayoutGate>();
        payoutGate.Configure(game, 3, "ヘソ");
    }

    private static void BuildExitZone(Transform root)
    {
        GameObject exit = new GameObject("Center Out Zone");
        exit.transform.SetParent(root, false);
        exit.transform.localPosition = new Vector3(0f, -4.31f, 0f);
        BoxCollider trigger = exit.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.02f, 0.5f, 1f);
        exit.AddComponent<PachinkoExitZone>();
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static Material MakeMaterial(string name, Color color, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        return material;
    }

    private static Color LaneColor(int index)
    {
        switch (index)
        {
            case 1:
            case 5:
                return new Color(0.15f, 0.72f, 0.82f);
            case 2:
            case 4:
                return new Color(0.95f, 0.47f, 0.18f);
            case 3:
                return new Color(1f, 0.82f, 0.24f);
            default:
                return new Color(0.25f, 0.72f, 0.44f);
        }
    }

    private static void AddSceneToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == path)
            {
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
