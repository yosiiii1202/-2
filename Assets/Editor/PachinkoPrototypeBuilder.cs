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
        spawnerData.FindProperty("pinZ").floatValue = 0.16f;
        spawnerData.ApplyModifiedPropertiesWithoutUndo();

        GameObject ballRoot = new GameObject("LiveBalls");
        ballRoot.transform.SetParent(board.transform, false);
        GameObject launcher = new GameObject("Launcher");
        launcher.transform.SetParent(board.transform, false);
        Transform launchPoint = launcher.transform;
        launchPoint.localPosition = new Vector3(-3.05f, -3.52f, 0.16f);

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
        Material lcdFrame = MakeMaterial("Pachinko LCD Frame", new Color(0.05f, 0.16f, 0.23f), 0.55f);
        Material lcdGlass = MakeMaterial("Pachinko LCD Glass", new Color(0.015f, 0.06f, 0.12f), 0.2f);

        Vector2[] outline =
        {
            new Vector2(-3.35f, -4.9f), new Vector2(-3.55f, -3.35f),
            new Vector2(-3.68f, -1.9f), new Vector2(-3.72f, -0.2f),
            new Vector2(-3.7f, 1.65f), new Vector2(-3.45f, 2.85f),
            new Vector2(-2.9f, 3.85f), new Vector2(-2.05f, 4.55f),
            new Vector2(-1.05f, 4.82f), new Vector2(0f, 4.9f),
            new Vector2(1.05f, 4.82f), new Vector2(2.05f, 4.55f),
            new Vector2(2.9f, 3.85f), new Vector2(3.45f, 2.85f),
            new Vector2(3.7f, 1.65f), new Vector2(3.72f, -0.2f),
            new Vector2(3.68f, -1.9f), new Vector2(3.55f, -3.35f),
            new Vector2(3.35f, -4.9f)
        };

        CreateBoardMesh("Domed Playfield", root, outline, 0.68f, backplate);
        CreateContour("Outer Brass Frame", root, outline, 0.16f, 0.1f, rail);
        CreateContour("Inner Blue Pin Guide", root, ScaleOutline(outline, 0.96f), 0.07f, 0.18f, trim);

        // The LCD sits in a deliberate opening in the upper-middle pin field.
        CreateCube("LCD Bezel", root, new Vector3(0f, -0.05f, 0.34f), new Vector3(3.55f, 1.42f, 0.18f), lcdFrame);
        GameObject screen = CreateCube("LCD Screen", root, new Vector3(0f, -0.05f, 0.22f), new Vector3(3.22f, 1.12f, 0.08f), lcdGlass);
        screen.GetComponent<Collider>().enabled = false;

        BuildBoardOrnaments(root, rail, trim);

        for (int i = 0; i <= 7; i++)
        {
            float x = -3.0f + i * 1.0f;
            if (i == 0 || i == 7)
            {
                continue;
            }

            GameObject divider = CreateCube("Prize Lane Divider " + i, root, new Vector3(x, -4.35f, 0.1f), new Vector3(0.07f, 0.62f, 0.4f), rail);
            divider.GetComponent<BoxCollider>().isTrigger = false;
        }

        CreateCube("Prize Lane Floor", root, new Vector3(0f, -4.62f, 0.12f), new Vector3(6.35f, 0.14f, 0.5f), trim);
        for (int i = 0; i < 7; i++)
        {
            if (i == 3) continue;
            float x = -3.0f + i * 1.0f;
            GameObject label = CreateCube("Prize Marker " + (i + 1), root, new Vector3(x, -4.05f, 0.22f), new Vector3(0.82f, 0.12f, 0.12f), MakeMaterial("Prize " + i, LaneColor(i), 0.3f));
            label.GetComponent<BoxCollider>().enabled = false;
        }
    }

    private static Vector2[] ScaleOutline(Vector2[] outline, float scale)
    {
        Vector2[] result = new Vector2[outline.Length];
        for (int i = 0; i < outline.Length; i++) result[i] = outline[i] * scale;
        return result;
    }

    private static GameObject CreateBoardMesh(string name, Transform parent, Vector2[] outline, float z, Material material)
    {
        GameObject board = new GameObject(name);
        board.transform.SetParent(parent, false);
        board.transform.localPosition = new Vector3(0f, 0f, z);
        Mesh mesh = new Mesh { name = name + " Mesh" };
        Vector3[] vertices = new Vector3[outline.Length + 1];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < outline.Length; i++) vertices[i + 1] = outline[i];
        int[] triangles = new int[outline.Length * 3];
        for (int i = 0; i < outline.Length; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % outline.Length + 1;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        board.AddComponent<MeshFilter>().sharedMesh = mesh;
        board.AddComponent<MeshRenderer>().sharedMaterial = material;
        return board;
    }

    private static void CreateContour(string name, Transform parent, Vector2[] points, float thickness, float z, Material material)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            Vector2 delta = end - start;
            GameObject segment = CreateCube(name + " " + i, parent,
                new Vector3((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f, z),
                new Vector3(delta.magnitude + thickness, thickness, 0.42f), material);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }

    private static void BuildBoardOrnaments(Transform root, Material brass, Material blue)
    {
        Vector3[] jewelPositions =
        {
            new Vector3(-2.95f, 3.25f, 0.18f), new Vector3(2.95f, 3.25f, 0.18f),
            new Vector3(-3.28f, 1.65f, 0.18f), new Vector3(3.28f, 1.65f, 0.18f),
            new Vector3(-3.28f, -0.15f, 0.18f), new Vector3(3.28f, -0.15f, 0.18f),
            new Vector3(-2.7f, -1.8f, 0.18f), new Vector3(2.7f, -1.8f, 0.18f)
        };
        for (int i = 0; i < jewelPositions.Length; i++)
        {
            GameObject jewel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            jewel.name = "Side Jewel " + (i + 1);
            jewel.transform.SetParent(root, false);
            jewel.transform.localPosition = jewelPositions[i];
            jewel.transform.localScale = new Vector3(0.25f, 0.25f, 0.12f);
            jewel.GetComponent<Collider>().enabled = false;
            jewel.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? brass : blue;
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
            gate.transform.localPosition = new Vector3(-3.0f + i * 1.0f, -4.31f, 0f);
            BoxCollider trigger = gate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.92f, 0.5f, 0.6f);
            PachinkoPayoutGate payoutGate = gate.AddComponent<PachinkoPayoutGate>();
            payoutGate.Configure(game, i, names[i]);
        }
    }

    private static void BuildHeso(Transform root, PachinkoGameController game)
    {
        GameObject heso = new GameObject("Heso - Hold Trigger");
        heso.transform.SetParent(root, false);
        heso.transform.localPosition = new Vector3(0f, -1.45f, 0f);
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
        trigger.size = new Vector3(0.92f, 0.5f, 1f);
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
