using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PachinkoGameController : MonoBehaviour
{
    private static Texture2D celebrationCircleTexture;
    private static Texture2D aquariumBackdropTexture;
    private const long ReincarnationBaseRequirement = 250_000L;
    private const long ReincarnationRequirementMultiplier = 3L;
    [Header("Scene references")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private Transform ballContainer;

    [Header("Starting values")]
    [SerializeField] private int autoLaunchInterval = 2;

    private readonly HashSet<PachinkoBall> activeBalls = new HashSet<PachinkoBall>();
    private readonly List<Transform> aquariumFish = new List<Transform>();
    private readonly List<Vector3> aquariumFishHomes = new List<Vector3>();
    private readonly List<float> aquariumFishSpeeds = new List<float>();
    private readonly List<Transform> aquariumFishTails = new List<Transform>();
    private readonly Transform[] jackpotCharacterModels = new Transform[4];
    private readonly int[] lanePayouts = { 1, 2, 3, 8, 3, 2, 1 };
    private readonly string[] laneNames = { "入賞口", "チューリップ", "アタッカー", "ヘソ抽選", "アタッカー", "チューリップ", "入賞口" };
    private readonly int[] categoryLevels = new int[3];

    private long money;
    private long lifetimePayout;
    private long runPayout;
    private int ballValueLevel;
    private int launchCountLevel;
    private int launchPowerLevel;
    private int jackpotLevel;
    private int probabilityChangeLevel;
    private bool autoLaunch;
    private bool autoPurchase;
    private float nextAutoLaunch;
    private float nextAutoPurchase;
    private float nextManualLaunch;
    private int pendingBalls;
    private readonly Queue<float> hesoSpinQueue = new Queue<float>();
    private int kakuhenBallsRemaining;
    private int consecutiveWins;
    private bool isSpinning;
    private int lcdStoppedDigits;
    private float jackpotFlashUntil;
    private int jackpotPayoutDisplay;
    private string jackpotCelebrationMessage = "大当たり！";
    private int celebrationCharacterIndex;
    private float jackpotEffectStartedAt = -10f;
    private float jackpotShakeUntil;
    private Camera jackpotCamera;
    private Vector3 jackpotCameraBasePosition;
    private bool jackpotCameraShaking;
    private string lcdMessage = "保留を待っています";
    private readonly int[] lcdDigits = { 7, 7, 7 };
    private float nextLcdDigitAt;
    private int reincarnationPoints;
    private int transcendencePoints;
    private int reincarnationUpgradeLevel;
    private int reincarnationLaunchSpeedLevel;
    private int jackpotMemoryUpgradeLevel;
    private int holdCapacityUpgradeLevel;
    private int transcendenceIncomeLevel;
    private int transcendenceVolleyLevel;
    private int transcendenceJackpotLevel;
    private int currentTab;
    private int purchaseAmount = 1;
    private string lastResult = "スペースキーまたは「玉を打つ」で発射";
    private AudioSource soundSource;
    private AudioSource jackpotSoundSource;
    private AudioSource jackpotVoiceSource;
    private AudioSource musicSource;
    private AudioClip launchSound;
    private AudioClip jackpotSound;
    private AudioClip jackpotVoice;
    private AudioClip backgroundMusic;

    private GUIStyle titleStyle;
    private GUIStyle eyebrowStyle;
    private GUIStyle sectionStyle;
    private GUIStyle bodyStyle;
    private GUIStyle statLabelStyle;
    private GUIStyle statValueStyle;
    private GUIStyle resultStyle;
    private GUIStyle hesoLabelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle launchButtonStyle;
    private GUIStyle panelStyle;
    private GUIStyle cardStyle;
    private GUIStyle subButtonStyle;
    private Vector2 scrollPosition;
    private Transform shooterPusher;
    private bool shooterAnimating;

    private void Start()
    {
        InitializeAudio();
        EnsureCabinetVisuals();
        EnsureAquariumBackground();
        EnsureOuterAquariumBackground();
        Ensure3DAquariumDecorations();
        Ensure3DJackpotCharacters();
        EnsureShooterVisual();
        EnsureOuterWallColliders();
        EnsureHesoVisual();
        if (ballPrefab == null)
        {
            Debug.LogError("PachinkoGameController: Ball Prefab が設定されていません。", this);
        }

        if (launchPoint == null)
        {
            Debug.LogError("PachinkoGameController: Launch Point が設定されていません。", this);
        }
    }

    private void EnsureCabinetVisuals()
    {
        Transform existingPlayfield = transform.Find("Domed Playfield");
        if (existingPlayfield != null)
        {
            MeshFilter existingFilter = existingPlayfield.GetComponent<MeshFilter>();
            if (existingFilter == null) existingFilter = existingPlayfield.gameObject.AddComponent<MeshFilter>();
            existingFilter.sharedMesh = CreatePlayfieldMesh(CabinetOutline());
            ApplyAquariumBackdrop(existingPlayfield.GetComponent<Renderer>());

            Material existingBrass = GetMaterial("Outer Brass Frame 0", new Color(0.9f, 0.56f, 0.18f));
            Material existingBlue = GetMaterial("Inner Blue Pin Guide 0", new Color(0.12f, 0.58f, 0.92f));
            Vector2[] existingOutline = CabinetOutline();
            if (transform.Find("Outer Brass Frame 0") == null) CreateCabinetContour("Outer Brass Frame", existingOutline, 0.16f, 0.1f, existingBrass);
            if (transform.Find("Inner Blue Pin Guide 0") == null)
            {
                Vector2[] existingInnerOutline = new Vector2[existingOutline.Length];
                for (int i = 0; i < existingOutline.Length; i++) existingInnerOutline[i] = existingOutline[i] * 0.96f;
                CreateCabinetContour("Inner Blue Pin Guide", existingInnerOutline, 0.07f, 0.18f, existingBlue);
            }
            if (transform.Find("LCD Bezel") == null)
                CreateCabinetCube("LCD Bezel", new Vector3(0f, -0.05f, 0.34f), new Vector3(3.55f, 1.42f, 0.18f), CreateVisualMaterial("Pachinko LCD Frame", new Color(0.05f, 0.16f, 0.23f)));
            if (transform.Find("LCD Screen") == null)
            {
                GameObject existingScreen = CreateCabinetCube("LCD Screen", new Vector3(0f, -0.05f, 0.22f), new Vector3(3.22f, 1.12f, 0.08f), CreateVisualMaterial("Pachinko LCD Glass", new Color(0.015f, 0.06f, 0.12f)));
                existingScreen.GetComponent<Collider>().enabled = false;
            }
            if (transform.Find("Side Jewel 1") == null) CreateCabinetOrnaments(existingBrass, existingBlue);
            PositionLCDParts();
            MoveExistingCabinetParts();
            return;
        }

        Transform backplate = transform.Find("Backplate");
        if (backplate == null) return;

        Renderer backplateRenderer = backplate.GetComponent<Renderer>();
        Material backplateMaterial = backplateRenderer != null ? backplateRenderer.sharedMaterial : CreateVisualMaterial("Pachinko Deep Navy", new Color(0.035f, 0.09f, 0.18f));
        Material brass = GetMaterial("Left Rail", new Color(0.9f, 0.56f, 0.18f));
        Material blue = GetMaterial("Prize Lane Floor", new Color(0.12f, 0.58f, 0.92f));

        Vector2[] outline = CabinetOutline();

        backplate.name = "Domed Playfield";
        backplate.localPosition = new Vector3(0f, 0f, 0.68f);
        backplate.localScale = Vector3.one;
        MeshFilter filter = backplate.GetComponent<MeshFilter>();
        if (filter == null) filter = backplate.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = CreatePlayfieldMesh(outline);
        if (backplateRenderer != null)
        {
            backplateRenderer.sharedMaterial = backplateMaterial;
            ApplyAquariumBackdrop(backplateRenderer);
        }
        Collider plateCollider = backplate.GetComponent<Collider>();
        if (plateCollider != null) plateCollider.enabled = false;

        DisableVisual("Left Rail");
        DisableVisual("Right Rail");
        DisableVisual("Top Rail");
        DisableVisual("Bottom Rail");
        CreateCabinetContour("Outer Brass Frame", outline, 0.16f, 0.1f, brass);

        Vector2[] innerOutline = new Vector2[outline.Length];
        for (int i = 0; i < outline.Length; i++) innerOutline[i] = outline[i] * 0.96f;
        CreateCabinetContour("Inner Blue Pin Guide", innerOutline, 0.07f, 0.18f, blue);

        CreateCabinetCube("LCD Bezel", new Vector3(0f, -0.05f, 0.34f), new Vector3(3.55f, 1.42f, 0.18f), CreateVisualMaterial("Pachinko LCD Frame", new Color(0.05f, 0.16f, 0.23f)));
        GameObject screen = CreateCabinetCube("LCD Screen", new Vector3(0f, -0.05f, 0.22f), new Vector3(3.22f, 1.12f, 0.08f), CreateVisualMaterial("Pachinko LCD Glass", new Color(0.015f, 0.06f, 0.12f)));
        screen.GetComponent<Collider>().enabled = false;
        CreateCabinetOrnaments(brass, blue);
        PositionLCDParts();
        MoveExistingCabinetParts();
    }

    private void PositionLCDParts()
    {
        Transform bezel = transform.Find("LCD Bezel");
        if (bezel != null) bezel.localPosition = new Vector3(0f, -0.05f, 0.34f);
        Transform screen = transform.Find("LCD Screen");
        if (screen != null) screen.localPosition = new Vector3(0f, -0.05f, 0.22f);
    }

    private static Vector2[] CabinetOutline()
    {
        return new[]
        {
            new Vector2(-3.35f, -4.9f), new Vector2(-3.55f, -3.35f), new Vector2(-3.68f, -1.9f),
            new Vector2(-3.72f, -0.2f), new Vector2(-3.7f, 1.65f), new Vector2(-3.45f, 2.85f),
            new Vector2(-2.9f, 3.85f), new Vector2(-2.05f, 4.55f), new Vector2(-1.05f, 4.82f),
            new Vector2(0f, 4.9f), new Vector2(1.05f, 4.82f), new Vector2(2.05f, 4.55f),
            new Vector2(2.9f, 3.85f), new Vector2(3.45f, 2.85f), new Vector2(3.7f, 1.65f),
            new Vector2(3.72f, -0.2f), new Vector2(3.68f, -1.9f), new Vector2(3.55f, -3.35f),
            new Vector2(3.35f, -4.9f)
        };
    }

    private void MoveExistingCabinetParts()
    {
        Transform launcher = transform.Find("Launcher");
        if (launcher != null) launcher.localPosition = new Vector3(-3.05f, -3.52f, 0.16f);
        Transform heso = transform.Find("Heso - Hold Trigger");
        if (heso != null) heso.localPosition = new Vector3(0f, -1.45f, 0f);

        for (int i = 1; i <= 6; i++)
        {
            Transform divider = transform.Find("Prize Lane Divider " + i);
            if (divider != null)
            {
                divider.localPosition = new Vector3(-3f + i, -4.35f, 0.1f);
                divider.localScale = new Vector3(0.07f, 0.62f, 0.4f);
            }
        }

        Transform floor = transform.Find("Prize Lane Floor");
        if (floor != null)
        {
            floor.localPosition = new Vector3(0f, -4.62f, 0.12f);
            floor.localScale = new Vector3(6.35f, 0.14f, 0.5f);
        }

        for (int i = 0; i < 7; i++)
        {
            float x = -3f + i;
            Transform marker = transform.Find("Prize Marker " + (i + 1));
            if (marker != null)
            {
                marker.localPosition = new Vector3(x, -4.05f, 0.22f);
                marker.localScale = new Vector3(0.82f, 0.12f, 0.12f);
            }

            Transform gate = transform.Find("Payout Gate " + (i + 1) + " - " + laneNames[i]);
            if (gate != null)
            {
                gate.localPosition = new Vector3(x, -4.31f, 0f);
                BoxCollider gateCollider = gate.GetComponent<BoxCollider>();
                if (gateCollider != null) gateCollider.size = new Vector3(0.92f, 0.5f, 0.6f);
            }
        }

        Transform exit = transform.Find("Center Out Zone");
        if (exit != null)
        {
            exit.localPosition = new Vector3(0f, -4.31f, 0f);
            BoxCollider exitCollider = exit.GetComponent<BoxCollider>();
            if (exitCollider != null) exitCollider.size = new Vector3(0.92f, 0.5f, 1f);
        }
    }

    private void EnsureShooterVisual()
    {
        Transform existingAssembly = transform.Find("Mechanical Shooter");
        if (existingAssembly != null)
        {
            existingAssembly.localPosition = new Vector3(-3.05f, -3.65f, 0f);
            shooterPusher = existingAssembly.Find("Spring pusher");
            if (launchPoint != null) launchPoint.localPosition = new Vector3(-3.05f, -3.52f, 0.16f);
            return;
        }

        Material brass = CreateVisualMaterial("Shooter Brass", new Color(0.78f, 0.48f, 0.16f));
        Material dark = CreateVisualMaterial("Shooter Housing", new Color(0.08f, 0.12f, 0.17f));
        Material steel = CreateVisualMaterial("Shooter Steel", new Color(0.55f, 0.68f, 0.76f));
        GameObject assembly = new GameObject("Mechanical Shooter");
        assembly.transform.SetParent(transform, false);
        assembly.transform.localPosition = new Vector3(-3.05f, -3.65f, 0f);

        CreateShooterPart(assembly.transform, "Shooter housing", new Vector3(0f, -0.7f, 0.1f), new Vector3(0.58f, 0.48f, 0.24f), dark);
        CreateShooterPart(assembly.transform, "Shooter barrel", new Vector3(0f, -0.22f, 0.12f), new Vector3(0.22f, 0.62f, 0.16f), brass);
        CreateShooterPart(assembly.transform, "Ball guide left", new Vector3(-0.2f, 0.48f, 0.16f), new Vector3(0.07f, 1.05f, 0.12f), steel);
        CreateShooterPart(assembly.transform, "Ball guide right", new Vector3(0.2f, 0.48f, 0.16f), new Vector3(0.07f, 1.05f, 0.12f), steel);
        CreateShooterPart(assembly.transform, "Shooter handle", new Vector3(-0.42f, -0.68f, 0.08f), new Vector3(0.3f, 0.12f, 0.2f), brass);
        shooterPusher = CreateShooterPart(assembly.transform, "Spring pusher", new Vector3(0f, -0.44f, 0.02f), new Vector3(0.12f, 0.24f, 0.14f), steel).transform;

        if (launchPoint != null) launchPoint.localPosition = new Vector3(-3.05f, -3.52f, 0.16f);
    }

    private GameObject CreateShooterPart(Transform parent, string partName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Collider>().enabled = false;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private IEnumerator AnimateShooter()
    {
        if (shooterPusher == null || shooterAnimating) yield break;
        shooterAnimating = true;
        Vector3 start = new Vector3(0f, -0.44f, 0.02f);
        shooterPusher.localPosition = start + Vector3.up * 0.45f;
        yield return new WaitForSeconds(0.08f);
        shooterPusher.localPosition = start;
        yield return new WaitForSeconds(0.12f);
        shooterAnimating = false;
    }

    private Material GetMaterial(string objectName, Color fallback)
    {
        Transform target = transform.Find(objectName);
        Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
        return renderer != null ? renderer.sharedMaterial : CreateVisualMaterial(objectName + " Material", fallback);
    }

    private void DisableVisual(string objectName)
    {
        Transform target = transform.Find(objectName);
        if (target == null) return;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        Collider collider = target.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
    }

    private Mesh CreatePlayfieldMesh(Vector2[] outline)
    {
        Mesh mesh = new Mesh { name = "Domed Playfield Mesh" };
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
        Vector2[] uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
            uv[i] = new Vector2(Mathf.InverseLerp(-3.72f, 3.72f, vertices[i].x), Mathf.InverseLerp(-4.9f, 4.9f, vertices[i].y));
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ApplyAquariumBackdrop(Renderer playfieldRenderer)
    {
        if (playfieldRenderer == null) return;
        EnsureAquariumBackdropTexture();
        Material backdropMaterial = playfieldRenderer.material;
        if (backdropMaterial.HasProperty("_BaseMap")) backdropMaterial.SetTexture("_BaseMap", aquariumBackdropTexture);
        else if (backdropMaterial.HasProperty("_MainTex")) backdropMaterial.SetTexture("_MainTex", aquariumBackdropTexture);
        if (backdropMaterial.HasProperty("_BaseColor")) backdropMaterial.SetColor("_BaseColor", Color.white);
        else if (backdropMaterial.HasProperty("_Color")) backdropMaterial.SetColor("_Color", Color.white);
        backdropMaterial.name = "Pixel Aquarium Water Backdrop";
    }

    private static void EnsureAquariumBackdropTexture()
    {
        if (aquariumBackdropTexture != null) return;
        const int size = 128;
        aquariumBackdropTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Pixel Aquarium Water",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float v = y / (float)(size - 1);
            Color deep = new Color(0.018f, 0.11f, 0.23f);
            Color mid = new Color(0.055f, 0.37f, 0.48f);
            Color surface = new Color(0.025f, 0.19f, 0.34f);
            Color baseColor = v < 0.55f
                ? Color.Lerp(deep, mid, v / 0.55f)
                : Color.Lerp(mid, surface, (v - 0.55f) / 0.45f);
            float wave = Mathf.Sin(x * 0.16f + y * 0.11f) + Mathf.Sin(x * 0.07f - y * 0.19f) * 0.45f;
            if (y < 34 && Mathf.Abs(wave - 0.8f) < 0.1f)
                baseColor = Color.Lerp(baseColor, new Color(0.14f, 0.66f, 0.72f), 0.3f);
            if (((x * 37 + y * 61) % 263) == 0)
                baseColor = Color.Lerp(baseColor, new Color(0.52f, 0.87f, 0.91f), 0.34f);
            pixels[y * size + x] = baseColor;
        }
        aquariumBackdropTexture.SetPixels(pixels);
        aquariumBackdropTexture.Apply(false, true);
    }

    private void CreateCabinetContour(string prefix, Vector2[] points, float thickness, float z, Material material)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % points.Length];
            Vector2 delta = end - start;
            GameObject segment = CreateCabinetCube(prefix + " " + i,
                new Vector3((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f, z),
                new Vector3(delta.magnitude + thickness, thickness, 0.42f), material);
            segment.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }

    private void CreateCabinetOrnaments(Material brass, Material blue)
    {
        Vector3[] positions =
        {
            new Vector3(-2.95f, 3.25f, 0.18f), new Vector3(2.95f, 3.25f, 0.18f),
            new Vector3(-3.28f, 1.65f, 0.18f), new Vector3(3.28f, 1.65f, 0.18f),
            new Vector3(-3.28f, -0.15f, 0.18f), new Vector3(3.28f, -0.15f, 0.18f),
            new Vector3(-2.7f, -1.8f, 0.18f), new Vector3(2.7f, -1.8f, 0.18f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject jewel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            jewel.name = "Side Jewel " + (i + 1);
            jewel.transform.SetParent(transform, false);
            jewel.transform.localPosition = positions[i];
            jewel.transform.localScale = new Vector3(0.25f, 0.25f, 0.12f);
            jewel.GetComponent<Collider>().enabled = false;
            jewel.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? brass : blue;
        }
    }

    private GameObject CreateCabinetCube(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = objectName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private void EnsureHesoVisual()
    {
        if (transform.Find("Heso Visual") != null) return;

        GameObject root = new GameObject("Heso Visual");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, -1.45f, 0f);
        Material gold = CreateVisualMaterial("Heso Gold", new Color(1f, 0.66f, 0.12f));
        Material cyan = CreateVisualMaterial("Heso Cyan", new Color(0.12f, 0.8f, 1f));
        CreateHesoPart(root.transform, "Heso left guide", new Vector3(-0.58f, 0f, 0.18f), new Vector3(0.16f, 0.32f, 0.12f), gold);
        CreateHesoPart(root.transform, "Heso right guide", new Vector3(0.58f, 0f, 0.18f), new Vector3(0.16f, 0.32f, 0.12f), gold);
        CreateHesoPart(root.transform, "Heso lower lip", new Vector3(0f, -0.18f, 0.18f), new Vector3(1.08f, 0.08f, 0.12f), cyan);
        CreateHesoPart(root.transform, "Heso indicator", new Vector3(0f, 0.22f, 0.18f), new Vector3(0.55f, 0.07f, 0.12f), gold);
    }

    private static Material CreateVisualMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName, color = color };
        return material;
    }

    private static void CreateHesoPart(Transform parent, string partName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<BoxCollider>().enabled = false;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void EnsureOuterWallColliders()
    {
        DisableOuterWall("Boundary Left");
        DisableOuterWall("Boundary Right");

        Vector2[] outline = CabinetOutline();
        for (int i = 0; i < 9; i++)
        {
            CreateRailWallSegment($"Boundary Left Segment {i}", outline[i] * 0.96f, outline[i + 1] * 0.96f);
            CreateRailWallSegment($"Boundary Right Segment {i}", outline[17 - i] * 0.96f, outline[18 - i] * 0.96f);
        }

        CreateOuterWall("Boundary Top", new Vector3(0f, 4.84f, 0.1f), new Vector3(8f, 0.42f, 1.2f));
    }

    private void DisableOuterWall(string objectName)
    {
        Transform existing = transform.Find(objectName);
        if (existing == null) return;
        Collider wallCollider = existing.GetComponent<Collider>();
        if (wallCollider != null) wallCollider.enabled = false;
    }

    private void CreateRailWallSegment(string objectName, Vector2 start, Vector2 end)
    {
        Transform existing = transform.Find(objectName);
        GameObject wall = existing != null ? existing.gameObject : new GameObject(objectName);
        wall.transform.SetParent(transform, false);

        Vector2 direction = end - start;
        wall.transform.localPosition = new Vector3((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f, 0.1f);
        wall.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        wall.transform.localScale = Vector3.one;

        BoxCollider wallCollider = wall.GetComponent<BoxCollider>();
        if (wallCollider == null) wallCollider = wall.AddComponent<BoxCollider>();
        wallCollider.size = new Vector3(direction.magnitude, 0.16f, 1.2f);
        wallCollider.isTrigger = false;
        wallCollider.enabled = true;
    }

    private void CreateOuterWall(string objectName, Vector3 localPosition, Vector3 colliderSize)
    {
        Transform existing = transform.Find(objectName);
        if (existing != null && existing.GetComponent<BoxCollider>() != null)
        {
            existing.GetComponent<BoxCollider>().enabled = true;
            existing.GetComponent<BoxCollider>().isTrigger = false;
            return;
        }

        GameObject wall = existing != null ? existing.gameObject : new GameObject(objectName);
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = Vector3.one;
        BoxCollider wallCollider = wall.GetComponent<BoxCollider>();
        if (wallCollider == null) wallCollider = wall.AddComponent<BoxCollider>();
        wallCollider.size = colliderSize;
        wallCollider.isTrigger = false;
        wallCollider.enabled = true;
    }

    private void Update()
    {
        if (isSpinning && Time.time >= nextLcdDigitAt)
        {
            RollLcdDigits();
            nextLcdDigitAt = Time.time + 0.055f;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
        {
            LaunchBatch();
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            if (pointer.x < Screen.width * 0.55f && pointer.y > Screen.height * 0.1f && pointer.y < Screen.height * 0.9f)
                LaunchBatch();
        }

        if (autoLaunch && Time.time >= nextAutoLaunch)
        {
            nextAutoLaunch = Time.time + Mathf.Max(0.2f, autoLaunchInterval * 0.25f);
            LaunchBatch();
        }

        if (autoPurchase && Time.time >= nextAutoPurchase)
        {
            nextAutoPurchase = Time.time + 1f;
            TryAutoPurchase();
        }

        RemoveExpiredBalls();
        AnimateAquariumFish();
        AnimateJackpotCharacters();
        AnimateJackpotCameraShake();
    }

    private void EnsureAquariumBackground()
    {
        Transform existing = transform.Find("3D Aquarium Fish 0");
        if (existing != null) return;

        Vector3[] homes =
        {
            new Vector3(-2.8f, 3.9f, 0.52f), new Vector3(-1.6f, 2.9f, 0.52f),
            new Vector3(2.8f, 3.7f, 0.52f), new Vector3(3.05f, 1.4f, 0.52f),
            new Vector3(-3.0f, 0.2f, 0.52f), new Vector3(2.7f, -1.8f, 0.52f),
            new Vector3(-2.3f, -2.8f, 0.52f), new Vector3(1.8f, -3.8f, 0.52f),
            new Vector3(-2.45f, 1.8f, 0.52f), new Vector3(2.45f, -0.4f, 0.52f),
            new Vector3(2.15f, 2.4f, 0.52f), new Vector3(-2.65f, -1.8f, 0.52f),
            new Vector3(-4.05f, 3.2f, 1.4f), new Vector3(4.05f, 1.2f, 1.4f),
            new Vector3(-4.08f, -1.8f, 1.4f), new Vector3(4.08f, -3.4f, 1.4f)
        };
        Color[] fishColors = { new Color(1f, 0.57f, 0.24f), new Color(0.26f, 0.82f, 0.9f), new Color(1f, 0.48f, 0.68f), new Color(0.88f, 0.77f, 0.34f) };
        Material[] fishBodyMaterials = new Material[fishColors.Length];
        Material[] fishFinMaterials = new Material[fishColors.Length];
        for (int i = 0; i < fishColors.Length; i++)
        {
            fishBodyMaterials[i] = CreateVisualMaterial("3D Aquarium Fish Body " + i, fishColors[i]);
            fishFinMaterials[i] = CreateVisualMaterial("3D Aquarium Fish Fin " + i, Color.Lerp(fishColors[i], new Color(0.08f, 0.22f, 0.32f), 0.28f));
        }
        Material fishEyeMaterial = CreateVisualMaterial("3D Aquarium Fish Eye", new Color(0.025f, 0.045f, 0.08f));
        for (int i = 0; i < homes.Length; i++)
        {
            GameObject fish = new GameObject("3D Aquarium Fish " + i);
            fish.transform.SetParent(transform, false);
            fish.transform.localPosition = homes[i];
            float scale = i % 3 == 0 ? 0.72f : 0.58f;
            fish.transform.localScale = Vector3.one * scale;
            Material body = fishBodyMaterials[i % fishBodyMaterials.Length];
            Material fin = fishFinMaterials[i % fishFinMaterials.Length];
            CreateFishPart(fish.transform, "Body", PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(0.72f, 0.4f, 0.3f), body);
            Transform tail = CreateFishPart(fish.transform, "Tail", PrimitiveType.Sphere, new Vector3(-0.48f, 0f, 0f), new Vector3(0.32f, 0.52f, 0.12f), fin);
            tail.localRotation = Quaternion.Euler(0f, 0f, 24f);
            CreateFishPart(fish.transform, "Top Fin", PrimitiveType.Sphere, new Vector3(-0.04f, 0.2f, 0f), new Vector3(0.27f, 0.18f, 0.1f), fin).localRotation = Quaternion.Euler(0f, 0f, -22f);
            CreateFishPart(fish.transform, "Lower Fin", PrimitiveType.Sphere, new Vector3(0.02f, -0.19f, 0f), new Vector3(0.22f, 0.15f, 0.1f), fin).localRotation = Quaternion.Euler(0f, 0f, 20f);
            CreateFishPart(fish.transform, "Eye", PrimitiveType.Sphere, new Vector3(0.23f, 0.08f, -0.16f), new Vector3(0.075f, 0.075f, 0.04f), fishEyeMaterial);
            aquariumFishTails.Add(tail);
            aquariumFish.Add(fish.transform);
            aquariumFishHomes.Add(homes[i]);
            aquariumFishSpeeds.Add(0.22f + (i % 4) * 0.055f);
        }
    }

    private Transform CreateFishPart(Transform parent, string partName, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) { renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; }
        return part.transform;
    }

    private void Ensure3DAquariumDecorations()
    {
        if (transform.Find("3D Aquarium Coral Left") != null) return;
        Material coral = CreateVisualMaterial("Aquarium Coral", new Color(0.88f, 0.34f, 0.27f));
        Material seaweed = CreateVisualMaterial("Aquarium Seaweed", new Color(0.1f, 0.58f, 0.42f));
        Material bubble = CreateVisualMaterial("Aquarium Bubble", new Color(0.44f, 0.84f, 0.96f));
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 4.12f;
            GameObject root = new GameObject(side < 0 ? "3D Aquarium Coral Left" : "3D Aquarium Coral Right");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(x, -3.95f, 1.1f);
            for (int stem = 0; stem < 3; stem++)
            {
                Transform branch = CreateFishPart(root.transform, "Coral Branch " + stem, PrimitiveType.Capsule,
                    new Vector3((stem - 1) * 0.16f, 0.4f + stem * 0.08f, 0f), new Vector3(0.09f, 0.38f + stem * 0.06f, 0.09f), stem == 1 ? coral : seaweed);
                branch.localRotation = Quaternion.Euler(0f, 0f, (stem - 1) * 24f);
                CreateFishPart(root.transform, "Coral Tip " + stem, PrimitiveType.Sphere,
                    new Vector3((stem - 1) * 0.26f, 0.83f + stem * 0.08f, 0f), new Vector3(0.16f, 0.15f, 0.1f), coral);
            }
        }
        for (int i = 0; i < 7; i++)
        {
            GameObject bubbleObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubbleObject.name = "3D Aquarium Bubble " + i;
            bubbleObject.transform.SetParent(transform, false);
            bubbleObject.transform.localPosition = new Vector3(i % 2 == 0 ? -3.8f : 3.8f, -2.4f + i * 0.72f, 1.25f);
            bubbleObject.transform.localScale = Vector3.one * (0.07f + (i % 3) * 0.025f);
            Collider bubbleCollider = bubbleObject.GetComponent<Collider>();
            if (bubbleCollider != null) bubbleCollider.enabled = false;
            bubbleObject.GetComponent<Renderer>().sharedMaterial = bubble;
        }
    }

    private void EnsureOuterAquariumBackground()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.15f, 0.22f, 1f);
        if (transform.Find("Pixel Aquarium Surrounding Water") != null) return;

        EnsureAquariumBackdropTexture();
        float viewHeight = camera.orthographic
            ? camera.orthographicSize * 2f
            : 2f * 18f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Rect viewport = camera.rect;
        float viewAspect = Screen.height > 0f
            ? Screen.width * viewport.width / (Screen.height * Mathf.Max(0.01f, viewport.height))
            : camera.aspect;
        float viewWidth = viewHeight * viewAspect;
        float distanceToBoard = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
        float backgroundDistance = distanceToBoard + 5f;

        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
        water.name = "Pixel Aquarium Surrounding Water";
        water.transform.SetParent(transform, false);
        water.transform.position = camera.transform.position + camera.transform.forward * backgroundDistance;
        water.transform.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
        water.transform.localScale = new Vector3(viewWidth * 1.12f, viewHeight * 1.12f, 1f);
        Collider waterCollider = water.GetComponent<Collider>();
        if (waterCollider != null) waterCollider.enabled = false;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        Material waterMaterial = shader != null
            ? new Material(shader) { name = "Pixel Aquarium Surrounding Water Material" }
            : CreateVisualMaterial("Pixel Aquarium Surrounding Water Material", Color.white);
        if (waterMaterial.HasProperty("_BaseMap")) waterMaterial.SetTexture("_BaseMap", aquariumBackdropTexture);
        else if (waterMaterial.HasProperty("_MainTex")) waterMaterial.SetTexture("_MainTex", aquariumBackdropTexture);
        if (waterMaterial.HasProperty("_BaseColor")) waterMaterial.SetColor("_BaseColor", Color.white);
        else if (waterMaterial.HasProperty("_Color")) waterMaterial.SetColor("_Color", Color.white);
        if (waterMaterial.HasProperty("_Cull")) waterMaterial.SetInt("_Cull", 0);
        water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
    }

    private void AnimateAquariumFish()
    {
        for (int i = 0; i < aquariumFish.Count; i++)
        {
            Transform fish = aquariumFish[i];
            if (fish == null) continue;
            Vector3 home = aquariumFishHomes[i];
            float direction = i % 2 == 0 ? 1f : -1f;
            float swim = Mathf.Sin(Time.time * aquariumFishSpeeds[i] + i * 1.7f) * 0.72f;
            Vector3 position = home + new Vector3(swim * direction, Mathf.Sin(Time.time * 1.3f + i) * 0.12f, 0f);
            fish.localPosition = position;
            fish.localScale = new Vector3(Mathf.Abs(fish.localScale.x) * direction, fish.localScale.y, 1f);
            if (i < aquariumFishTails.Count && aquariumFishTails[i] != null)
                aquariumFishTails[i].localRotation = Quaternion.Euler(0f, 0f, 20f + Mathf.Sin(Time.time * 7f + i) * 22f);
        }
    }

    public void LaunchBatch()
    {
        if (ballPrefab == null || launchPoint == null || Time.time < nextManualLaunch)
        {
            return;
        }

        nextManualLaunch = Time.time + Mathf.Max(0.08f, 0.22f - reincarnationLaunchSpeedLevel * 0.015f);
        int count = 1 + launchCountLevel + transcendenceVolleyLevel * 2;
        PlaySound(launchSound, 0.58f, Random.Range(0.96f, 1.04f));

        for (int i = 0; i < count; i++)
        {
            SpawnBall();
        }

        lastResult = count > 1 ? $"{count}発発射！" : "発射！";
    }

    private void SpawnBall()
    {
        if (shooterPusher != null) StartCoroutine(AnimateShooter());
        Vector3 jitter = new Vector3(Random.Range(-0.035f, 0.035f), Random.Range(-0.02f, 0.02f), 0f);
        GameObject instance = Instantiate(ballPrefab, launchPoint.position + jitter, Quaternion.identity, ballContainer);
        instance.name = "PachinkoBall";

        Rigidbody body = instance.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = instance.AddComponent<Rigidbody>();
        }

        body.constraints = RigidbodyConstraints.FreezePositionZ
            | RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationY;
        body.isKinematic = false;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        PachinkoBall ball = instance.GetComponent<PachinkoBall>();
        if (ball == null)
        {
            ball = instance.AddComponent<PachinkoBall>();
        }

        activeBalls.Add(ball);
        float startY = transform.InverseTransformPoint(launchPoint.position).y;
        float apexY = Mathf.Min(4.45f, 4.35f + launchPowerLevel * 0.02f);
        float targetHeight = Mathf.Max(0.1f, apexY - startY);
        // Leave enough energy to reach the dome even after small contacts near
        // the lower guide. The lane is kept clear until the ball reaches the top.
        float verticalSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * targetHeight) * 1.3f
            + launchPowerLevel * 0.1f;
        float horizontalSpeed = verticalSpeed * Random.Range(0.015f, 0.04f) + Random.Range(-0.08f, 0.08f);
        body.linearVelocity = new Vector3(horizontalSpeed, verticalSpeed * Random.Range(0.97f, 1f), 0f);
    }

    public void AwardPayout(PachinkoBall ball, int laneIndex, string gateName)
    {
        if (ball == null || !activeBalls.Remove(ball))
        {
            return;
        }

        int category = laneIndex == 1 || laneIndex == 5 ? 1 : laneIndex == 2 || laneIndex == 4 ? 2 : 0;
        float multiplier = GetBallValueMultiplier()
            * (1f + categoryLevels[category] * 0.5f)
            * (1f + reincarnationUpgradeLevel * 0.15f)
            * Mathf.Pow(2f, transcendenceIncomeLevel);

        if (laneIndex == 3)
        {
            if (pendingBalls < MaxPendingBalls)
            {
                pendingBalls++;
                lcdMessage = "ヘソ入賞！ 保留 +1";
                hesoSpinQueue.Enqueue(multiplier);
                if (!isSpinning) StartCoroutine(ResolveHesoSpins());
            }
            else
            {
                money++;
                lifetimePayout++;
                runPayout++;
                lastResult = "保留が満タン　+¥1";
            }

            Destroy(ball.gameObject, 0.05f);
            return;
        }

        int payout = Mathf.Max(1, Mathf.RoundToInt(lanePayouts[Mathf.Clamp(laneIndex, 0, lanePayouts.Length - 1)] * multiplier));
        lastResult = $"{gateName} +¥{payout:N0}";

        money += payout;
        lifetimePayout += payout;
        runPayout += payout;
        Destroy(ball.gameObject, 0.05f);
    }

    private IEnumerator ResolveHesoSpins()
    {
        isSpinning = true;
        while (hesoSpinQueue.Count > 0)
        {
            float multiplier = hesoSpinQueue.Dequeue();
            bool alreadyInKakuhen = kakuhenBallsRemaining > 0;
            bool jackpot = alreadyInKakuhen || Random.value < JackpotChance;
            bool kakuhen = jackpot && (alreadyInKakuhen || Random.value < KakuhenChance);
            int[] resultDigits = jackpot
                ? new[] { 7, 7, 7 }
                : new[] { Random.Range(1, 10), Random.Range(1, 10), Random.Range(1, 10) };
            if (!jackpot && resultDigits[0] == resultDigits[1] && resultDigits[1] == resultDigits[2])
                resultDigits[2] = resultDigits[2] % 9 + 1;

            lcdStoppedDigits = 0;
            nextLcdDigitAt = Time.time;
            RollLcdDigits();
            lcdMessage = "抽選中…";
            yield return new WaitForSeconds(1.15f);

            lcdStoppedDigits = 1;
            lcdDigits[0] = resultDigits[0];
            lcdMessage = resultDigits[0] == resultDigits[1] ? "リーチ！" : "数字停止…";
            yield return new WaitForSeconds(0.35f);
            lcdStoppedDigits = 2;
            lcdDigits[1] = resultDigits[1];
            lcdMessage = resultDigits[0] == resultDigits[1] ? "最後の数字に注目！" : "数字停止…";
            yield return new WaitForSeconds(0.4f);
            lcdStoppedDigits = 3;
            lcdDigits[2] = resultDigits[2];

            int payout;
            if (jackpot)
            {
                if (jackpotSoundSource != null && jackpotSound != null) jackpotSoundSource.PlayOneShot(jackpotSound, 0.62f);
                if (jackpotVoiceSource != null && jackpotVoice != null) jackpotVoiceSource.PlayOneShot(jackpotVoice, 1f);
                consecutiveWins++;
                payout = Mathf.RoundToInt(80f * multiplier * (1f + Mathf.Min(consecutiveWins - 1, 10) * 0.25f));
                if (alreadyInKakuhen) kakuhenBallsRemaining--;
                if (kakuhen && !alreadyInKakuhen) kakuhenBallsRemaining = 8;
                lcdMessage = kakuhen ? "777　大当たり！　確変突入" : "777　大当たり！";
                lastResult = kakuhen ? $"大当たり！確変突入　+¥{payout:N0}" : $"大当たり！　+¥{payout:N0}";
                jackpotPayoutDisplay = payout;
                jackpotCelebrationMessage = kakuhen ? "確変突入！" : "大当たり！";
                celebrationCharacterIndex = Random.Range(0, 4);
                jackpotFlashUntil = Time.time + 4.5f;
                jackpotEffectStartedAt = Time.time;
                jackpotShakeUntil = Time.time + 0.75f;
                if (!jackpotCameraShaking)
                {
                    jackpotCamera = Camera.main;
                    if (jackpotCamera != null)
                    {
                        jackpotCameraBasePosition = jackpotCamera.transform.position;
                        jackpotCameraShaking = true;
                    }
                }
            }
            else
            {
                payout = Mathf.Max(0, Mathf.RoundToInt(2f * multiplier));
                consecutiveWins = 0;
                lcdMessage = resultDigits[0] == resultDigits[1] ? "リーチ…　残念！" : "ハズレ";
                lastResult = "ヘソ抽選　ハズレ";
            }

            money += payout;
            lifetimePayout += payout;
            runPayout += payout;
            pendingBalls = Mathf.Max(0, pendingBalls - 1);
            yield return new WaitForSeconds(0.35f);
        }
        isSpinning = false;
        lcdStoppedDigits = 0;
    }

    private void InitializeAudio()
    {
        soundSource = GetComponent<AudioSource>();
        if (soundSource == null) soundSource = gameObject.AddComponent<AudioSource>();
        soundSource.playOnAwake = false;
        soundSource.spatialBlend = 0f;
        soundSource.dopplerLevel = 0f;
        jackpotSoundSource = gameObject.AddComponent<AudioSource>();
        jackpotSoundSource.playOnAwake = false;
        jackpotSoundSource.spatialBlend = 0f;
        jackpotSoundSource.dopplerLevel = 0f;
        jackpotVoiceSource = gameObject.AddComponent<AudioSource>();
        jackpotVoiceSource.playOnAwake = false;
        jackpotVoiceSource.spatialBlend = 0f;
        jackpotVoiceSource.dopplerLevel = 0f;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.dopplerLevel = 0f;
        musicSource.volume = 0.28f;
        launchSound = Resources.Load<AudioClip>("Audio/launch");
        jackpotSound = Resources.Load<AudioClip>("Audio/jackpot");
        jackpotVoice = Resources.Load<AudioClip>("Audio/jackpot_voice");
        backgroundMusic = Resources.Load<AudioClip>("Audio/bgm");
        if (backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.Play();
        }
    }

    private void PlaySound(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || soundSource == null) return;
        soundSource.pitch = pitch;
        soundSource.PlayOneShot(clip, volume);
    }

    public void LoseBall(PachinkoBall ball)
    {
        if (ball == null || !activeBalls.Remove(ball))
        {
            return;
        }

        Destroy(ball.gameObject);
    }

    private void RemoveExpiredBalls()
    {
        if (activeBalls.Count == 0)
        {
            return;
        }

        List<PachinkoBall> expired = null;
        foreach (PachinkoBall ball in activeBalls)
        {
            if (ball == null || ball.transform.position.y < -5.8f || Mathf.Abs(ball.transform.position.x) > 5.5f)
            {
                if (expired == null)
                {
                    expired = new List<PachinkoBall>();
                }

                expired.Add(ball);
            }
        }

        if (expired == null)
        {
            return;
        }

        foreach (PachinkoBall ball in expired)
        {
            if (ball != null)
            {
                activeBalls.Remove(ball);
                Destroy(ball.gameObject);
            }
        }
    }

    private float JackpotChance => Mathf.Clamp(Mathf.Clamp(0.05f + jackpotLevel * 0.02f, 0.05f, 0.5f) + jackpotMemoryUpgradeLevel * 0.01f + transcendenceJackpotLevel * 0.05f, 0.05f, 0.9f);
    private float KakuhenChance => Mathf.Clamp(0.1f + probabilityChangeLevel * 0.05f, 0.1f, 0.8f);
    private int MaxPendingBalls => 4 + holdCapacityUpgradeLevel;

    private void OnGUI()
    {
        EnsureStyles();
        float w = Screen.width;
        float h = Screen.height;
        float top = Mathf.Clamp(h * 0.085f, 58f, 82f);
        float boardWidth = w * 0.55f;
        DrawHud(new Rect(0f, 0f, w, top));
        DrawLCD(GetLCDScreenRect(boardWidth, h));
        GUI.Label(new Rect(boardWidth * 0.32f, h * 0.59f, boardWidth * 0.36f, 30f), "▼　ヘソ　保留 +1　▼", hesoLabelStyle);
        DrawBoardControls(new Rect(12f, h - 72f, boardWidth - 24f, 58f));
        DrawSidePanel(new Rect(boardWidth + 8f, top + 8f, w - boardWidth - 20f, h - top - 18f));
        if (Time.time < jackpotFlashUntil) DrawJackpotCelebration(boardWidth, h);
    }

    private void DrawJackpotCelebration(float boardWidth, float screenHeight)
    {
        float remaining = Mathf.Clamp01((jackpotFlashUntil - Time.time) / 4.5f);
        float elapsed = Mathf.Max(0f, Time.time - jackpotEffectStartedAt);
        float pulse = 0.82f + Mathf.PingPong(Time.time * 6f, 0.34f);
        Color oldColor = GUI.color;

        DrawJackpotImpactFlash(boardWidth, screenHeight, elapsed);
        DrawJackpotStarburst(boardWidth, screenHeight, elapsed, remaining);
        DrawPFlashFrame(boardWidth, screenHeight, elapsed);
        GUI.color = new Color(1f, 0.45f, 0.12f, 0.025f + 0.055f * pulse * remaining);
        GUI.DrawTexture(new Rect(0f, 0f, boardWidth, screenHeight), Texture2D.whiteTexture);
        for (int i = 0; i < 64; i++)
        {
            float fall = Mathf.Repeat(Time.time * (0.38f + (i % 5) * 0.025f) + i * 0.037f, 1f);
            float x = Mathf.Repeat(i * 0.173f + Mathf.Sin(Time.time * 2.2f + i) * 0.06f, 1f) * boardWidth;
            float y = Mathf.Repeat(fall + Mathf.Sin(Time.time * 2f + i * 0.8f) * 0.035f, 1f) * screenHeight;
            float size = 8f + (i % 5) * 2.5f;
            float angle = Mathf.Sin(Time.time * 3.2f + i * 1.7f) * 70f;
            GUIUtility.RotateAroundPivot(angle, new Vector2(x, y));
            GUI.color = i % 4 == 0 ? new Color(1f, 0.76f, 0.2f, remaining) : i % 3 == 0 ? new Color(1f, 0.42f, 0.69f, remaining) : new Color(1f, 0.84f, 0.9f, remaining);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size, size, size * 1.8f), GetCelebrationCircle());
            GUIUtility.RotateAroundPivot(-angle, new Vector2(x, y));
        }

        float panelWidth = Mathf.Min(boardWidth * 0.94f, 760f);
        float panelHeight = 166f;
        Rect panel = new Rect((boardWidth - panelWidth) * 0.5f,
            Mathf.Clamp(screenHeight * 0.34f, 100f, screenHeight - panelHeight - 80f), panelWidth, panelHeight);
        GUI.color = new Color(1f, 0.34f + Mathf.PingPong(Time.time * 3.8f, 0.48f), 0.04f, 0.99f * remaining);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.025f, 0.045f, 0.12f, 0.98f * remaining);
        GUI.DrawTexture(new Rect(panel.x + 5f, panel.y + 5f, panel.width - 10f, panel.height - 10f), Texture2D.whiteTexture);

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt((panel.width - 132f) / 5.35f * pulse), 42, 86),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            normal = { textColor = new Color(1f, 0.88f, 0.2f, remaining) }
        };
        GUIStyle detail = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(panelHeight * 0.2f), 18, 32),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.72f, 0.91f, 1f, remaining) }
        };
        Rect titleRect = new Rect(panel.x + 12f, panel.y + 12f, panel.width - 126f, panel.height * 0.57f);
        GUI.color = Color.white;
        DrawOutlinedCelebrationLabel(titleRect, "大当たり!!", title, new Color(0.82f, 0.12f, 0.2f, remaining), 3f);
        Rect detailRect = new Rect(panel.x + 16f, panel.y + panel.height * 0.61f, panel.width - 138f, panel.height * 0.27f);
        GUI.Label(new Rect(detailRect.x + 2f, detailRect.y + 2f, detailRect.width, detailRect.height),
            $"{jackpotCelebrationMessage}　払い出し +¥{jackpotPayoutDisplay:N0}", detail);
        GUI.Label(detailRect, $"{jackpotCelebrationMessage}　払い出し +¥{jackpotPayoutDisplay:N0}", detail);
        DrawCelebrationCharacter(new Rect(panel.xMax - 94f, panel.y + 7f, 82f, panel.height - 14f), pulse);
        DrawCharacterSpeechBubble(panel, remaining, pulse);
        GUI.color = oldColor;
    }

    private void DrawPFlashFrame(float boardWidth, float screenHeight, float elapsed)
    {
        if (elapsed > 1.6f) return;
        float pulse = Mathf.PingPong(elapsed * 8f, 1f);
        float alpha = 0.35f + pulse * 0.55f;
        float thickness = Mathf.Clamp(screenHeight * 0.014f, 10f, 22f);
        float top = Mathf.Clamp(screenHeight * 0.085f + 8f, 60f, 98f);
        float bottom = screenHeight - 74f;
        Color oldColor = GUI.color;
        GUI.color = new Color(0.1f, 1f, 0.48f, alpha);
        GUI.DrawTexture(new Rect(3f, top, thickness, bottom - top), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boardWidth - thickness - 3f, top, thickness, bottom - top), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(3f, top, boardWidth - 6f, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(3f, bottom - thickness, boardWidth - 6f, thickness), Texture2D.whiteTexture);
        GUI.color = new Color(0.88f, 1f, 0.94f, alpha * 0.85f);
        GUI.DrawTexture(new Rect(thickness + 4f, top + thickness, 3f, bottom - top - thickness * 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boardWidth - thickness - 7f, top + thickness, 3f, bottom - top - thickness * 2f), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawOutlinedCelebrationLabel(Rect rect, string text, GUIStyle style, Color outline, float thickness)
    {
        GUIStyle shadow = new GUIStyle(style);
        shadow.normal.textColor = outline;
        GUI.Label(new Rect(rect.x - thickness, rect.y, rect.width, rect.height), text, shadow);
        GUI.Label(new Rect(rect.x + thickness, rect.y, rect.width, rect.height), text, shadow);
        GUI.Label(new Rect(rect.x, rect.y - thickness, rect.width, rect.height), text, shadow);
        GUI.Label(new Rect(rect.x, rect.y + thickness, rect.width, rect.height), text, shadow);
        GUI.Label(rect, text, style);
    }

    private void DrawCharacterSpeechBubble(Rect panel, float remaining, float pulse)
    {
        float width = Mathf.Min(panel.width * 0.78f, 460f);
        const float height = 54f;
        Rect bubble = new Rect(panel.x + (panel.width - width) * 0.5f, panel.y - height - 12f, width, height);
        GUI.color = new Color(1f, 0.62f, 0.06f, 0.98f * remaining);
        GUI.DrawTexture(new Rect(bubble.x - 3f, bubble.y - 3f, bubble.width + 6f, bubble.height + 6f), Texture2D.whiteTexture);
        Vector2 tailPivot = new Vector2(bubble.xMax - 27f, bubble.yMax - 2f);
        GUIUtility.RotateAroundPivot(45f, tailPivot);
        GUI.DrawTexture(new Rect(tailPivot.x - 11f, tailPivot.y - 11f, 22f, 22f), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-45f, tailPivot);
        GUI.color = new Color(1f, 1f, 0.94f, 0.99f * remaining);
        GUI.DrawTexture(bubble, Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(45f, tailPivot);
        GUI.DrawTexture(new Rect(tailPivot.x - 8f, tailPivot.y - 8f, 16f, 16f), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-45f, tailPivot);
        GUIStyle speech = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(25f * pulse), 22, 34),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            normal = { textColor = new Color(0.72f, 0.04f, 0.18f, remaining) }
        };
        GUI.color = Color.white;
        const string fullSpeech = "きたぁー！大当たりーっ！！";
        int shown = Mathf.Clamp(Mathf.FloorToInt((Time.time - jackpotEffectStartedAt) * 18f) + 1, 1, fullSpeech.Length);
        DrawOutlinedCelebrationLabel(new Rect(bubble.x + 5f, bubble.y + 2f, bubble.width - 10f, bubble.height - 4f),
            fullSpeech.Substring(0, shown), speech, new Color(1f, 0.74f, 0.15f, remaining), 1.5f);
    }

    private void DrawJackpotImpactFlash(float boardWidth, float screenHeight, float elapsed)
    {
        if (elapsed > 0.95f) return;
        float wave = Mathf.Max(0f, Mathf.Sin((elapsed + 0.04f) * 11f));
        float envelope = 1f - elapsed / 0.95f;
        float alpha = Mathf.Pow(wave, 2.2f) * envelope * 0.48f;
        if (alpha <= 0.01f) return;
        GUI.color = elapsed < 0.2f ? new Color(1f, 1f, 1f, alpha) : new Color(1f, 0.72f, 0.16f, alpha * 0.82f);
        GUI.DrawTexture(new Rect(0f, 0f, boardWidth, screenHeight), Texture2D.whiteTexture);
    }

    private void DrawJackpotStarburst(float boardWidth, float screenHeight, float elapsed, float remaining)
    {
        float burst = Mathf.Clamp01(1f - elapsed / 1.2f);
        if (burst <= 0f) return;
        Vector2 center = new Vector2(boardWidth * 0.5f, screenHeight * 0.43f);
        float rotation = Time.time * 28f;
        for (int i = 0; i < 20; i++)
        {
            float angle = i * 18f + rotation;
            float length = (38f + Mathf.PingPong(Time.time * 110f + i * 13f, 76f)) * burst;
            float width = (3f + (i % 3)) * burst;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = i % 3 == 0 ? new Color(1f, 0.38f, 0.62f, burst * remaining) : new Color(1f, 0.83f, 0.24f, burst * remaining);
            GUI.DrawTexture(new Rect(center.x - width * 0.5f, center.y - 118f - length, width, length), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, center);
        }
    }

    private void AnimateJackpotCameraShake()
    {
        if (!jackpotCameraShaking || jackpotCamera == null) return;
        if (Time.time < jackpotShakeUntil)
        {
            float strength = (jackpotShakeUntil - Time.time) * 0.13f;
            Vector3 offset = new Vector3(Mathf.Sin(Time.time * 72f) * strength, Mathf.Cos(Time.time * 61f) * strength, 0f);
            jackpotCamera.transform.position = jackpotCameraBasePosition + offset;
        }
        else
        {
            jackpotCamera.transform.position = jackpotCameraBasePosition;
            jackpotCameraShaking = false;
            jackpotCamera = null;
        }
    }

    private void DrawCelebrationCharacter(Rect rect, float pulse)
    {
        PositionJackpotCharacter(rect, pulse);
    }

    private void Ensure3DJackpotCharacters()
    {
        if (transform.Find("3D Jackpot Character 0") != null) return;
        Color[] hairColors = { new Color(0.18f, 0.11f, 0.16f), new Color(0.88f, 0.57f, 0.22f), new Color(0.34f, 0.16f, 0.24f), new Color(0.12f, 0.31f, 0.4f) };
        Color[] outfitColors = { new Color(0.84f, 0.23f, 0.34f), new Color(0.18f, 0.52f, 0.82f), new Color(0.78f, 0.36f, 0.68f), new Color(0.24f, 0.66f, 0.5f) };
        Color[] accentColors = { new Color(1f, 0.82f, 0.35f), new Color(0.97f, 0.83f, 0.45f), new Color(1f, 0.65f, 0.78f), new Color(0.78f, 0.94f, 0.64f) };
        Material skin = CreateVisualMaterial("Jackpot Character Skin", new Color(1f, 0.76f, 0.61f));
        Material eye = CreateVisualMaterial("Jackpot Character Eyes", new Color(0.11f, 0.09f, 0.14f));
        for (int i = 0; i < jackpotCharacterModels.Length; i++)
        {
            Transform root = new GameObject("3D Jackpot Character " + i).transform;
            root.SetParent(transform, false);
            jackpotCharacterModels[i] = root;
            root.gameObject.SetActive(false);
            Material hair = CreateVisualMaterial("Jackpot Character Hair " + i, hairColors[i]);
            Material clothes = CreateVisualMaterial("Jackpot Character Outfit " + i, outfitColors[i]);
            Material accent = CreateVisualMaterial("Jackpot Character Accent " + i, accentColors[i]);
            CreateCharacterPart(root, "Body", PrimitiveType.Capsule, new Vector3(0f, -0.13f, 0f), new Vector3(0.48f, 0.42f, 0.3f), clothes);
            CreateCharacterPart(root, "Collar", PrimitiveType.Sphere, new Vector3(0f, 0.13f, -0.015f), new Vector3(0.42f, 0.14f, 0.31f), accent);
            CreateCharacterPart(root, "Skirt", PrimitiveType.Cylinder, new Vector3(0f, -0.42f, 0f), new Vector3(0.56f, 0.19f, 0.35f), clothes);
            CreateCharacterPart(root, "Left Leg", PrimitiveType.Capsule, new Vector3(-0.15f, -0.72f, 0f), new Vector3(0.14f, 0.34f, 0.14f), skin);
            CreateCharacterPart(root, "Right Leg", PrimitiveType.Capsule, new Vector3(0.15f, -0.72f, 0f), new Vector3(0.14f, 0.34f, 0.14f), skin);
            CreateCharacterPart(root, "Left Shoe", PrimitiveType.Sphere, new Vector3(-0.16f, -1.01f, -0.06f), new Vector3(0.25f, 0.12f, 0.3f), hair);
            CreateCharacterPart(root, "Right Shoe", PrimitiveType.Sphere, new Vector3(0.16f, -1.01f, -0.06f), new Vector3(0.25f, 0.12f, 0.3f), hair);
            Transform leftArm = CreateCharacterPart(root, "Left Arm", PrimitiveType.Capsule, new Vector3(-0.35f, -0.06f, 0f), new Vector3(0.13f, 0.33f, 0.14f), clothes);
            leftArm.localRotation = Quaternion.Euler(0f, 0f, 32f);
            Transform rightArm = CreateCharacterPart(root, "Right Arm", PrimitiveType.Capsule, new Vector3(0.35f, -0.06f, 0f), new Vector3(0.13f, 0.33f, 0.14f), clothes);
            rightArm.localRotation = Quaternion.Euler(0f, 0f, -32f);
            CreateCharacterPart(root, "Left Hand", PrimitiveType.Sphere, new Vector3(-0.48f, 0.18f, -0.02f), new Vector3(0.16f, 0.16f, 0.15f), skin);
            CreateCharacterPart(root, "Right Hand", PrimitiveType.Sphere, new Vector3(0.48f, 0.18f, -0.02f), new Vector3(0.16f, 0.16f, 0.15f), skin);
            CreateCharacterPart(root, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.27f, 0f), new Vector3(0.13f, 0.13f, 0.13f), skin);
            CreateCharacterPart(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.58f, 0f), new Vector3(0.62f, 0.68f, 0.46f), skin);
            CreateCharacterPart(root, "Hair Cap", PrimitiveType.Sphere, new Vector3(0f, 0.82f, 0.025f), new Vector3(0.67f, 0.48f, 0.5f), hair);
            CreateCharacterPart(root, "Hair Left", PrimitiveType.Capsule, new Vector3(-0.29f, 0.54f, 0.02f), new Vector3(0.18f, 0.48f, 0.2f), hair);
            CreateCharacterPart(root, "Hair Right", PrimitiveType.Capsule, new Vector3(0.29f, 0.54f, 0.02f), new Vector3(0.18f, 0.48f, 0.2f), hair);
            CreateCharacterPart(root, "Left Eye", PrimitiveType.Sphere, new Vector3(-0.16f, 0.59f, -0.222f), new Vector3(0.07f, 0.1f, 0.045f), eye);
            CreateCharacterPart(root, "Right Eye", PrimitiveType.Sphere, new Vector3(0.16f, 0.59f, -0.222f), new Vector3(0.07f, 0.1f, 0.045f), eye);
            CreateCharacterPart(root, "Hair Ornament", PrimitiveType.Sphere, new Vector3(0.33f, 0.91f, -0.13f), new Vector3(0.2f, 0.18f, 0.12f), accent);
            if (i == 1 || i == 2)
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform ponytail = CreateCharacterPart(root, "Ponytail " + side, PrimitiveType.Sphere, new Vector3(side * 0.48f, 0.48f, 0.1f), new Vector3(0.2f, 0.44f, 0.18f), hair);
                    ponytail.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
                    CreateCharacterPart(root, "Ribbon " + side, PrimitiveType.Sphere, new Vector3(side * 0.44f, 0.72f, -0.04f), new Vector3(0.24f, 0.12f, 0.1f), accent);
                }
            if (i == 3)
                CreateCharacterPart(root, "Side Ponytail", PrimitiveType.Sphere, new Vector3(0.45f, 0.35f, 0.12f), new Vector3(0.22f, 0.42f, 0.18f), hair);
        }
    }

    private Transform CreateCharacterPart(Transform parent, string partName, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) { renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; }
        return part.transform;
    }

    private void PositionJackpotCharacter(Rect rect, float pulse)
    {
        Camera camera = Camera.main;
        if (camera == null || jackpotCharacterModels[0] == null) return;
        bool visible = Time.time < jackpotFlashUntil;
        for (int i = 0; i < jackpotCharacterModels.Length; i++)
            if (jackpotCharacterModels[i] != null) jackpotCharacterModels[i].gameObject.SetActive(visible && i == celebrationCharacterIndex);
        if (!visible) return;
        Transform character = jackpotCharacterModels[Mathf.Clamp(celebrationCharacterIndex, 0, jackpotCharacterModels.Length - 1)];
        float screenX = rect.center.x;
        float screenY = Screen.height - rect.center.y + Mathf.Sin(Time.time * 7f) * 4f;
        float depth = Mathf.Max(0.2f, Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward) - 0.25f);
        character.position = camera.ScreenToWorldPoint(new Vector3(screenX, screenY, depth));
        character.rotation = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
        float pixelsToWorld = camera.orthographic ? camera.orthographicSize * 2f / Mathf.Max(1f, Screen.height) : (2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) / Mathf.Max(1f, Screen.height);
        character.localScale = Vector3.one * (rect.height * pixelsToWorld * (0.95f + (pulse - 0.82f) * 0.12f) / 2.05f);
    }

    private void AnimateJackpotCharacters()
    {
        if (Time.time < jackpotFlashUntil) return;
        for (int i = 0; i < jackpotCharacterModels.Length; i++)
            if (jackpotCharacterModels[i] != null && jackpotCharacterModels[i].gameObject.activeSelf)
                jackpotCharacterModels[i].gameObject.SetActive(false);
    }

    private static Texture2D GetCelebrationCircle()
    {
        if (celebrationCircleTexture != null) return celebrationCircleTexture;
        const int size = 48;
        celebrationCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Celebration Circle",
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            float alpha = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * size * 0.5f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        celebrationCircleTexture.SetPixels(pixels);
        celebrationCircleTexture.Apply(false, true);
        return celebrationCircleTexture;
    }

    private void DrawLCD(Rect rect)
    {
        rect = Rect.MinMaxRect(rect.xMin + 2f, rect.yMin + 2f, rect.xMax - 2f, rect.yMax - 2f);
        Color oldColor = GUI.color;
        bool jackpotFlash = Time.time < jackpotFlashUntil;
        float flashPulse = jackpotFlash ? 0.72f + Mathf.PingPong(Time.time * 4f, 0.28f) : 1f;
        GUI.color = jackpotFlash
            ? new Color(0.08f, 0.48f * flashPulse, 0.62f * flashPulse, 0.98f)
            : isSpinning ? new Color(0.12f, 0.28f, 0.68f, 0.97f) : new Color(0.035f, 0.08f, 0.22f, 0.94f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.color = oldColor;
        string state = kakuhenBallsRemaining > 0 ? $"確変中　連チャン {consecutiveWins}" : "液晶抽選";
        string holds = $"保留　{pendingBalls}/{MaxPendingBalls}";
        GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, rect.height * 0.2f), state, eyebrowStyle);
        float gap = Mathf.Max(3f, rect.width * 0.018f);
        float digitWidth = (rect.width - 16f - gap * 2f) / 3f;
        float digitY = rect.y + rect.height * 0.22f;
        float digitHeight = rect.height * 0.56f;
        GUIStyle digitStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.58f), 12, 42),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = jackpotFlash ? new Color(1f, 0.9f, 0.35f) : isSpinning ? new Color(0.65f, 0.9f, 1f) : new Color(1f, 0.82f, 0.28f) }
        };
        for (int i = 0; i < lcdDigits.Length; i++)
        {
            Rect digitRect = new Rect(rect.x + 8f + i * (digitWidth + gap), digitY, digitWidth, digitHeight);
            GUI.color = new Color(0.015f, 0.035f, 0.1f, 0.92f);
            GUI.DrawTexture(digitRect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(digitRect, lcdDigits[i].ToString(), digitStyle);
        }
        GUIStyle messageStyle = new GUIStyle(eyebrowStyle)
        {
            fontSize = Mathf.Clamp(eyebrowStyle.fontSize - 2, 10, 14),
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(rect.x + 8f, digitY + digitHeight, rect.width - 16f, rect.height * 0.18f),
            isSpinning ? "回転中…　" + holds : lcdMessage + "　" + holds, messageStyle);
    }

    private void RollLcdDigits()
    {
        for (int i = lcdStoppedDigits; i < lcdDigits.Length; i++) lcdDigits[i] = Random.Range(1, 10);
    }

    private Rect GetLCDScreenRect(float boardWidth, float screenHeight)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            float fallbackWidth = boardWidth * 0.38f;
            float fallbackHeight = screenHeight * 0.105f;
            return new Rect((boardWidth - fallbackWidth) * 0.5f, screenHeight * 0.42f, fallbackWidth, fallbackHeight);
        }

        Vector3 lowerLeft = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(-1.61f, -0.61f, 0.22f)));
        Vector3 upperRight = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(1.61f, 0.51f, 0.22f)));
        if (lowerLeft.z <= 0f || upperRight.z <= 0f)
        {
            float fallbackWidth = boardWidth * 0.38f;
            float fallbackHeight = screenHeight * 0.105f;
            return new Rect((boardWidth - fallbackWidth) * 0.5f, screenHeight * 0.42f, fallbackWidth, fallbackHeight);
        }

        return Rect.MinMaxRect(lowerLeft.x, screenHeight - upperRight.y, upperRight.x, screenHeight - lowerLeft.y);
    }

    private void DrawHud(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        float pad = 14f;
        float unit = (rect.width - pad * 2f) / 5f;
        GUI.Label(new Rect(rect.x + pad, rect.y + 7f, unit * 1.05f, rect.height - 12f),
            $"転生・超越\nLV.{1 + ballValueLevel + launchCountLevel}　連チャン {consecutiveWins}", eyebrowStyle);
        GUI.Label(new Rect(rect.x + pad + unit * 1.05f, rect.y + 5f, unit * 1.05f, rect.height - 10f),
            $"所持金　¥{money:N0}\n玉　∞", statValueStyle);
        GUI.Label(new Rect(rect.x + pad + unit * 2.2f, rect.y + 10f, unit * 0.9f, rect.height - 16f),
            $"転生P  {reincarnationPoints}", sectionStyle);
        GUI.Label(new Rect(rect.x + pad + unit * 3.15f, rect.y + 10f, unit * 0.8f, rect.height - 16f),
            $"超越P  {transcendencePoints}", sectionStyle);
        GUI.Label(new Rect(rect.x + rect.width - unit * 0.85f, rect.y + 10f, unit * 0.8f, rect.height - 16f),
            $"超越強化  {transcendenceIncomeLevel + transcendenceVolleyLevel + transcendenceJackpotLevel}", sectionStyle);
    }

    private void DrawBoardControls(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        if (GUI.Button(new Rect(rect.x + 6f, rect.y + 5f, rect.width * 0.43f, rect.height - 10f), "● 発射！", launchButtonStyle))
        {
            LaunchBatch();
        }

        GUI.Label(new Rect(rect.x + rect.width * 0.47f, rect.y + 8f, rect.width * 0.2f, rect.height - 14f), "玉 ∞", bodyStyle);
        GUI.enabled = autoLaunch || money >= 15;
        if (GUI.Button(new Rect(rect.x + rect.width * 0.78f, rect.y + 7f, rect.width * 0.2f, rect.height - 14f),
            autoLaunch ? "自動発射 ON" : "自動発射 OFF", subButtonStyle)) ToggleAutoLaunch();
        GUI.enabled = true;
    }

    private void DrawSidePanel(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        float pad = 10f;
        float tabY = rect.y + 8f;
        float tabW = (rect.width - pad * 2f) / 4f;
        string[] tabs = { "強化", "転生", "超越", "記録" };
        for (int i = 0; i < tabs.Length; i++)
        {
            GUIStyle style = currentTab == i ? launchButtonStyle : subButtonStyle;
            if (GUI.Button(new Rect(rect.x + pad + i * tabW, tabY, tabW - 4f, 40f), tabs[i], style))
            {
                currentTab = i;
                scrollPosition = Vector2.zero;
            }
        }

        float y = tabY + 48f;
        GUI.Label(new Rect(rect.x + pad, y, rect.width * 0.12f, 28f), "購入数", bodyStyle);
        int[] amounts = { 1, 10, 100 };
        string[] amountLabels = { "×1", "×10", "MAX" };
        for (int i = 0; i < amounts.Length; i++)
        {
            int index = i;
            float x = rect.x + pad + rect.width * (0.12f + i * 0.11f);
            if (GUI.Button(new Rect(x, y, rect.width * 0.105f, 28f), amountLabels[i], purchaseAmount == amounts[i] ? launchButtonStyle : subButtonStyle))
                purchaseAmount = amounts[index];
        }

        if (GUI.Button(new Rect(rect.xMax - pad - rect.width * 0.35f, y, rect.width * 0.34f, 28f), autoPurchase ? "自動購入 ON" : "自動購入 OFF", subButtonStyle))
            autoPurchase = !autoPurchase;

        Rect scrollRect = new Rect(rect.x + pad, y + 34f, rect.width - pad * 2f, rect.height - (y - rect.y) - 75f);
        GUILayout.BeginArea(scrollRect);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);
        if (currentTab == 0) DrawUpgradeTab();
        else if (currentTab == 1) DrawRebirthTab();
        else if (currentTab == 2) DrawTranscendTab();
        else DrawRecordTab();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.Label(new Rect(rect.x + pad, rect.yMax - 36f, rect.width - pad * 2f, 30f), lastResult, resultStyle);
    }

    private void DrawUpgradeTab()
    {
        DrawUpgrade("玉の価値", ballValueLevel, 5, () => ballValueLevel++, "1玉あたりの獲得金額を増やす", $"×{GetBallValueMultiplier():0.0} → ×{1f + (ballValueLevel + 1) * 1f:0.0}", costMultiplier: 1.2f);
        DrawUpgrade("オート発射速度", 2 - autoLaunchInterval, 12, () => autoLaunchInterval = Mathf.Max(1, autoLaunchInterval - 1), autoLaunch ? "自動発射中" : "発射間隔を短縮", autoLaunchInterval > 1 ? $"{autoLaunchInterval}秒 → {autoLaunchInterval - 1}秒" : "最大強化");
        DrawUpgrade("連射", launchCountLevel, 18, () => launchCountLevel++, "1回の発射で出る玉の数", $"{1 + launchCountLevel} → {2 + launchCountLevel}発", maxLevel: 5);
        DrawUpgrade("入賞口強化", categoryLevels[0], 16, () => categoryLevels[0]++, "入賞口の倍率", $"×{1f + categoryLevels[0] * 0.5f:0.0} → ×{1f + (categoryLevels[0] + 1) * 0.5f:0.0}");
        DrawUpgrade("チューリップ強化", categoryLevels[1], 16, () => categoryLevels[1]++, "チューリップの倍率", $"×{1f + categoryLevels[1] * 0.5f:0.0} → ×{1f + (categoryLevels[1] + 1) * 0.5f:0.0}");
        DrawUpgrade("ヘソ拡張", launchPowerLevel, 12, () => launchPowerLevel++, "発射パワーを上げて盤面へ届ける", $"Lv.{launchPowerLevel} → Lv.{launchPowerLevel + 1}");
        DrawUpgrade("大当たり確率", jackpotLevel, 24, () => jackpotLevel++, "液晶抽選の大当たり確率", $"{JackpotChance:P0} → {Mathf.Min(0.65f, JackpotChance + 0.02f):P0}");
        DrawUpgrade("アタッカー倍率", categoryLevels[2], 20, () => categoryLevels[2]++, "アタッカー入賞時の出玉倍率", $"×{1f + categoryLevels[2] * 0.5f:0.0} → ×{1f + (categoryLevels[2] + 1) * 0.5f:0.0}");
        DrawUpgrade("確変突入率", probabilityChangeLevel, 24, () => probabilityChangeLevel++, "大当たり後に確変へ入りやすくする", $"{KakuhenChance:P0} → {Mathf.Min(0.8f, KakuhenChance + 0.05f):P0}");
    }

    private void DrawRebirthTab()
    {
        GUILayout.Label("転生", titleStyle);
        GUILayout.Label("転生すると所持金と通常強化がリセットされます。", bodyStyle);
        long reincarnationRequirement = GetNextReincarnationRequirement();
        DrawProgressBar(runPayout, reincarnationRequirement, $"転生条件　¥{runPayout:N0} / ¥{reincarnationRequirement:N0}");
        int pointsAvailable = CalculateReincarnationPoints(runPayout);
        GUILayout.Label($"今回獲得予定　+{pointsAvailable} ポイント", sectionStyle);
        GUILayout.Label($"転生ポイント　{reincarnationPoints}", sectionStyle);
        GUI.enabled = runPayout >= reincarnationRequirement;
        if (GUILayout.Button("転生する", buttonStyle, GUILayout.Height(46f))) Reincarnate();
        GUILayout.Space(8f);
        GUILayout.Label("転生後も残る強化", sectionStyle);
        GUI.enabled = reincarnationPoints >= 1;
        if (GUILayout.Button($"全倍率アップ　Lv.{reincarnationUpgradeLevel} → {reincarnationUpgradeLevel + 1}（1P）", buttonStyle, GUILayout.Height(38f)))
        {
            reincarnationUpgradeLevel++;
            reincarnationPoints--;
        }
        int launchSpeedCost = reincarnationLaunchSpeedLevel + 1;
        GUI.enabled = reincarnationLaunchSpeedLevel < 8 && reincarnationPoints >= launchSpeedCost;
        if (GUILayout.Button($"発射間隔短縮　Lv.{reincarnationLaunchSpeedLevel}/8　（{launchSpeedCost}P）", buttonStyle, GUILayout.Height(38f)))
        {
            reincarnationLaunchSpeedLevel++;
            reincarnationPoints -= launchSpeedCost;
        }
        int memoryCost = jackpotMemoryUpgradeLevel + 2;
        GUI.enabled = jackpotMemoryUpgradeLevel < 15 && reincarnationPoints >= memoryCost;
        if (GUILayout.Button($"大当たり確率を引継ぎ +1%　Lv.{jackpotMemoryUpgradeLevel}/15　（{memoryCost}P）", buttonStyle, GUILayout.Height(38f)))
        {
            jackpotMemoryUpgradeLevel++;
            reincarnationPoints -= memoryCost;
        }
        int holdCost = holdCapacityUpgradeLevel + 3;
        GUI.enabled = holdCapacityUpgradeLevel < 4 && reincarnationPoints >= holdCost;
        if (GUILayout.Button($"保留上限 +1　Lv.{holdCapacityUpgradeLevel}/4　（{holdCost}P）", buttonStyle, GUILayout.Height(38f)))
        {
            holdCapacityUpgradeLevel++;
            reincarnationPoints -= holdCost;
        }
        GUI.enabled = true;
    }

    private void DrawTranscendTab()
    {
        GUILayout.Label("超越", titleStyle);
        GUILayout.Label("転生を超えた恒久強化。獲得金額・連射・大当たり確率を大幅に伸ばします。", bodyStyle);
        GUILayout.Label($"転生ポイント　{reincarnationPoints} / 5", sectionStyle);
        GUI.enabled = reincarnationPoints >= 5;
        if (GUILayout.Button("超越する（転生ポイント5消費 → 超越ポイント+1）", buttonStyle, GUILayout.Height(44f)))
        {
            reincarnationPoints -= 5;
            transcendencePoints++;
        }
        GUILayout.Space(8f);
        GUILayout.Label($"超越ポイント　{transcendencePoints}", sectionStyle);

        GUI.enabled = transcendenceIncomeLevel < 10 && transcendencePoints >= 1;
        if (GUILayout.Button($"獲得金額 ×2　Lv.{transcendenceIncomeLevel}/10　（1超越P）", buttonStyle, GUILayout.Height(40f)))
        {
            transcendenceIncomeLevel++;
            transcendencePoints--;
        }

        GUI.enabled = transcendenceVolleyLevel < 3 && transcendencePoints >= 1;
        if (GUILayout.Button($"一度に発射する玉 +2　Lv.{transcendenceVolleyLevel}/3　（1超越P）", buttonStyle, GUILayout.Height(40f)))
        {
            transcendenceVolleyLevel++;
            transcendencePoints--;
        }

        GUI.enabled = transcendenceJackpotLevel < 5 && transcendencePoints >= 1;
        if (GUILayout.Button($"大当たり確率 +5%　Lv.{transcendenceJackpotLevel}/5　（1超越P）", buttonStyle, GUILayout.Height(40f)))
        {
            transcendenceJackpotLevel++;
            transcendencePoints--;
        }
        GUI.enabled = true;
        GUILayout.Label($"超越効果：獲得金額 ×{Mathf.Pow(2f, transcendenceIncomeLevel):0} ／ 1回 {1 + launchCountLevel + transcendenceVolleyLevel * 2}発 ／ 大当たり {JackpotChance:P0}", bodyStyle);
    }

    private void DrawRecordTab()
    {
        GUILayout.Label("プレイ記録", titleStyle);
        GUILayout.Label($"累計獲得金額　¥{lifetimePayout:N0}", sectionStyle);
        GUILayout.Label($"今回の獲得金額　¥{runPayout:N0}", sectionStyle);
        GUILayout.Label($"盤面上の玉　{activeBalls.Count}", sectionStyle);
        GUILayout.Label($"大当たり確率　{JackpotChance:P0}\n確変突入率　{KakuhenChance:P0}", bodyStyle);
    }

    private void DrawUpgrade(string label, int level, int baseCost, System.Action purchase, string description, string change, int maxLevel = -1, float costMultiplier = 1.32f)
    {
        int availableLevels = maxLevel >= 0 ? Mathf.Max(0, maxLevel - level) : int.MaxValue;
        int count = Mathf.Min(purchaseAmount, availableLevels);
        bool capped = (label == "オート発射速度" && autoLaunchInterval <= 1)
            || (label == "大当たり確率" && jackpotLevel >= 22)
            || (label == "確変突入率" && probabilityChangeLevel >= 14)
            || (maxLevel >= 0 && level >= maxLevel);
        long CostAtLevel(int targetLevel)
        {
            if (costMultiplier == 1.32f) return UpgradeCost(baseCost, targetLevel);
            double costAtLevel = baseCost * System.Math.Pow(costMultiplier, System.Math.Max(0, targetLevel));
            return costAtLevel >= long.MaxValue ? long.MaxValue : System.Math.Max(1L, (long)System.Math.Round(costAtLevel));
        }
        long cost = 0;
        for (int i = 0; i < count; i++) cost += CostAtLevel(level + i);
        if (purchaseAmount == 100)
        {
            count = 0;
            cost = 0;
            while (count < availableLevels && count < 100 && cost + CostAtLevel(level + count) <= money)
                cost += CostAtLevel(level + count++);
        }
        if (capped)
        {
            count = 0;
            cost = 0;
        }
        float availableCardWidth = Screen.width * 0.45f - 60f;
        bool stackPurchaseButton = availableCardWidth < 520f;
        GUILayout.BeginVertical(cardStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(74f));
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        GUILayout.Label($"{label}　Lv.{level}", sectionStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label(description, bodyStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label(change, eyebrowStyle, GUILayout.ExpandWidth(true));
        GUILayout.EndVertical();
        GUI.enabled = count > 0 && money >= cost;
        GUIStyle purchaseStyle = new GUIStyle(subButtonStyle)
        {
            fontSize = Mathf.Clamp(subButtonStyle.fontSize - 3, 10, 14),
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter
        };
        if (!stackPurchaseButton && GUILayout.Button($"強化 ×{count}\n¥{cost:N0}", purchaseStyle, GUILayout.Width(Mathf.Clamp(availableCardWidth * 0.25f, 108f, 132f)), GUILayout.Height(58f)))
        {
            BuyUpgrade(cost, () => { for (int i = 0; i < count; i++) purchase(); }, label);
        }
        GUILayout.EndHorizontal();
        if (stackPurchaseButton && GUILayout.Button($"強化 ×{count}　¥{cost:N0}", purchaseStyle, GUILayout.ExpandWidth(true), GUILayout.Height(38f)))
        {
            BuyUpgrade(cost, () => { for (int i = 0; i < count; i++) purchase(); }, label);
        }
        GUI.enabled = true;
        GUILayout.EndVertical();
    }

    private void BuyUpgrade(long cost, System.Action purchase, string label)
    {
        if (money < cost)
        {
            lastResult = "お金が足りません。";
            return;
        }

        money -= cost;
        purchase();
        lastResult = $"{label}を強化しました（-¥{cost:N0}）。";
    }

    private void ToggleAutoLaunch()
    {
        if (autoLaunch)
        {
            autoLaunch = false;
            return;
        }

        BuyUpgrade(15, () => autoLaunch = true, "自動発射");
    }

    private void TryAutoPurchase()
    {
        long bestCost = long.MaxValue;
        System.Action bestPurchase = null;
        string bestLabel = "";
        ConsiderAutoUpgrade("玉の価値", BallValueUpgradeCost(ballValueLevel), () => ballValueLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        if (launchCountLevel < 5)
            ConsiderAutoUpgrade("連射", UpgradeCost(18, launchCountLevel), () => launchCountLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        ConsiderAutoUpgrade("発射パワー", UpgradeCost(12, launchPowerLevel), () => launchPowerLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        ConsiderAutoUpgrade("大当たり率", UpgradeCost(24, jackpotLevel), () => jackpotLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        ConsiderAutoUpgrade("確変率", UpgradeCost(24, probabilityChangeLevel), () => probabilityChangeLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        for (int i = 0; i < categoryLevels.Length; i++)
        {
            int index = i;
            int baseCost = i == 2 ? 20 : 16;
            ConsiderAutoUpgrade("入賞倍率", UpgradeCost(baseCost, categoryLevels[index]), () => categoryLevels[index]++, ref bestCost, ref bestPurchase, ref bestLabel);
        }

        if (bestPurchase != null) BuyUpgrade(bestCost, bestPurchase, bestLabel);
    }

    private void ConsiderAutoUpgrade(string label, long cost, System.Action purchase, ref long bestCost, ref System.Action bestPurchase, ref string bestLabel)
    {
        if (cost <= money && cost < bestCost)
        {
            bestCost = cost;
            bestPurchase = purchase;
            bestLabel = label;
        }
    }

    private void Reincarnate()
    {
        int pointsEarned = CalculateReincarnationPoints(runPayout);
        if (pointsEarned == 0)
        {
            return;
        }

        StopAllCoroutines();
        shooterAnimating = false;
        if (shooterPusher != null) shooterPusher.localPosition = new Vector3(0f, -0.44f, 0.02f);
        ClearActiveBalls();
        autoLaunch = false;
        pendingBalls = 0;
        hesoSpinQueue.Clear();
        kakuhenBallsRemaining = 0;
        consecutiveWins = 0;
        isSpinning = false;

        runPayout = 0;
        money = 0;
        ballValueLevel = 0;
        launchCountLevel = 0;
        launchPowerLevel = 0;
        jackpotLevel = 0;
        probabilityChangeLevel = 0;
        for (int i = 0; i < categoryLevels.Length; i++)
        {
            categoryLevels[i] = 0;
        }

        reincarnationPoints += pointsEarned;
        lastResult = $"転生しました。転生ポイント +{pointsEarned}！";
    }

    private int CalculateReincarnationPoints(long payout)
    {
        long remainingPayout = payout;
        long nextRequirement = GetNextReincarnationRequirement();
        int pointsEarned = 0;
        while (remainingPayout >= nextRequirement)
        {
            remainingPayout -= nextRequirement;
            pointsEarned++;
            nextRequirement = nextRequirement > long.MaxValue / ReincarnationRequirementMultiplier
                ? long.MaxValue
                : nextRequirement * ReincarnationRequirementMultiplier;
        }
        return pointsEarned;
    }

    private void ClearActiveBalls()
    {
        // Clean up tracked balls and any balls created by an older/duplicate launcher.
        foreach (PachinkoBall ball in activeBalls)
        {
            if (ball == null) continue;
            ball.gameObject.SetActive(false);
            Destroy(ball.gameObject);
        }
        activeBalls.Clear();

        PachinkoBall[] sceneBalls = FindObjectsOfType<PachinkoBall>();
        foreach (PachinkoBall ball in sceneBalls)
        {
            if (ball == null) continue;
            ball.gameObject.SetActive(false);
            Destroy(ball.gameObject);
        }

        if (ballContainer != null)
        {
            for (int i = ballContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = ballContainer.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }

    private long GetNextReincarnationRequirement()
    {
        long requirement = ReincarnationBaseRequirement;
        for (int i = 0; i < reincarnationPoints && requirement < long.MaxValue / ReincarnationRequirementMultiplier; i++)
        {
            requirement *= ReincarnationRequirementMultiplier;
        }
        return requirement;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(16, 16, 14, 14),
            normal = { background = MakeTexture(new Color(0.025f, 0.045f, 0.09f, 0.97f), new Color(0.18f, 0.55f, 0.72f, 0.9f)) }
        };
        int scale = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 1080f), 1, 2);
        eyebrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 * scale, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.34f, 0.84f, 0.9f) } };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32 * scale, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 23 * scale, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.48f, 0.86f, 1f) } };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 * scale, wordWrap = true, normal = { textColor = new Color(0.78f, 0.84f, 0.93f) } };
        statLabelStyle = new GUIStyle(bodyStyle) { fontSize = 18 * scale, alignment = TextAnchor.MiddleLeft };
        statValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 * scale, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.82f, 0.33f) } };
        resultStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 * scale, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        hesoLabelStyle = new GUIStyle(sectionStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.78f, 0.26f) } };
        buttonStyle = MakeButtonStyle(new Color(0.12f, 0.32f, 0.47f), new Color(0.17f, 0.43f, 0.6f));
        launchButtonStyle = MakeButtonStyle(new Color(0.04f, 0.48f, 0.58f), new Color(0.06f, 0.64f, 0.69f));
        launchButtonStyle.fontSize = 30 * scale;
        buttonStyle.fontSize = 20 * scale;
        subButtonStyle = MakeButtonStyle(new Color(0.13f, 0.2f, 0.31f), new Color(0.17f, 0.32f, 0.44f));
        subButtonStyle.fontSize = 18 * scale;
        cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 7, 7),
            margin = new RectOffset(0, 0, 3, 3),
            normal = { background = MakeTexture(new Color(0.055f, 0.09f, 0.16f, 0.98f), new Color(0.12f, 0.2f, 0.29f, 0.9f)) }
        };
    }

    private static long UpgradeCost(int baseCost, int level)
    {
        double cost = baseCost * System.Math.Pow(1.32d, System.Math.Max(0, level));
        if (double.IsInfinity(cost) || cost >= long.MaxValue) return long.MaxValue;
        return System.Math.Max(1L, (long)System.Math.Round(cost));
    }

    private float GetBallValueMultiplier() => 1f + ballValueLevel;

    private static long BallValueUpgradeCost(int level)
    {
        double cost = 5d * System.Math.Pow(1.2d, System.Math.Max(0, level));
        if (double.IsInfinity(cost) || cost >= long.MaxValue) return long.MaxValue;
        return System.Math.Max(1L, (long)System.Math.Round(cost));
    }

    private long AutoIntervalCost => UpgradeCost(12, 2 - autoLaunchInterval);

    private void DrawProgressBar(long value, long target, string label)
    {
        GUILayout.Label(label, bodyStyle);
        Rect rect = GUILayoutUtility.GetRect(1f, 24f, GUILayout.ExpandWidth(true));
        GUI.color = new Color(0.09f, 0.14f, 0.2f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.68f, 0.2f);
        rect.width *= Mathf.Clamp01((float)value / target);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static GUIStyle MakeButtonStyle(Color normalColor, Color hoverColor)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { background = MakeTexture(normalColor), textColor = Color.white },
            hover = { background = MakeTexture(hoverColor), textColor = Color.white },
            active = { background = MakeTexture(hoverColor * 0.85f), textColor = Color.white },
            focused = { background = MakeTexture(normalColor), textColor = Color.white }
        };
        return style;
    }

    private static Texture2D MakeTexture(Color color, Color borderColor = default)
    {
        Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        Color[] pixels = new Color[64];
        bool hasBorder = borderColor.a > 0f;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                bool border = hasBorder && (x == 0 || y == 0 || x == 7 || y == 7);
                pixels[y * 8 + x] = border ? borderColor : color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
