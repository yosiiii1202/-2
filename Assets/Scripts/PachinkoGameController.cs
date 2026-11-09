using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BigInteger = System.Numerics.BigInteger;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PachinkoGameController : MonoBehaviour
{
    private enum PachinkoMode
    {
        Normal,
        Rush,
        KomaChallenge
    }

    private static Texture2D celebrationCircleTexture;
    private static Texture2D aquariumBackdropTexture;
    private static Texture2D spotlightBeamTexture;
    private readonly Sprite[] aquariumFishSprites = new Sprite[4];
    private readonly Texture2D[] jackpotCharacterSprites = new Texture2D[4];
    private static readonly BigInteger ReincarnationBaseRequirement = new BigInteger(250_000);
    private const int ReincarnationRequirementMultiplier = 3;
    private static readonly BigInteger MaxCurrencyAward = BigInteger.Pow(10, 300);
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
    private readonly int[] lanePayouts = { 1, 2, 3, 8, 3, 2, 1 };
    private readonly string[] laneNames = { "入賞口", "チューリップ", "アタッカー", "ヘソ抽選", "アタッカー", "チューリップ", "入賞口" };
    private readonly int[] categoryLevels = new int[3];

    private BigInteger money;
    private BigInteger lifetimePayout;
    private BigInteger runPayout;
    private int ballValueLevel;
    private int launchCountLevel;
    private int launchPowerLevel;
    private int jackpotLevel;
    private int probabilityChangeLevel;
    private bool autoLaunch = true;
    private bool autoPurchase;
    private bool isLaunchingBatch;
    private float nextAutoLaunch;
    private float nextAutoPurchase;
    private float nextManualLaunch;
    private int pendingBalls;
    private readonly Queue<float> hesoSpinQueue = new Queue<float>();
    private PachinkoMode gameMode;
    private int challengeSpinsRemaining;
    private int consecutiveWins;
    private bool isSpinning;
    private int lcdStoppedDigits;
    private float jackpotFlashUntil;
    private BigInteger jackpotPayoutDisplay;
    private int jackpotStreakDisplay;
    private double jackpotPayoutMultiplierDisplay = 1d;
    private float jackpotCelebrationDuration = 4.5f;
    private string jackpotCelebrationMessage = "大当たり！";
    private int celebrationCharacterIndex;
    private int celebrationCharacterSide;
    private float jackpotEffectStartedAt = -10f;
    private float jackpotShakeUntil;
    private Camera jackpotCamera;
    private Vector3 jackpotCameraBasePosition;
    private bool jackpotCameraShaking;
    private string lcdMessage = "保留を待っています";
    private readonly int[] lcdDigits = { 7, 7, 7 };
    private int lcdChanceUpLevel;
    private int lcdChanceCharacterIndex;
    private float lcdSpinInterval = 0.068f;
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
    private int divineIncomeLevel;
    private int divineVolleyLevel;
    private int divineJackpotLevel;
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
    private AudioClip jackpotMaleVoice;
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
    private GUIStyle hudIconStyle;
    private GUIStyle upgradeGlyphStyle;
    private Vector2 scrollPosition;
    private Transform shooterPusher;
    private bool shooterAnimating;

    private void Start()
    {
        InitializeAudio();
        EnsureCabinetVisuals();
        EnsureCabinetCrown();
        RemoveAquariumDecorations();
        EnsureOuterAquariumBackground();
        Ensure2DJackpotCharacters();
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
                CreateCabinetCube("LCD Bezel", new Vector3(0f, -0.05f, 0.34f), new Vector3(5.12f, 2.22f, 0.18f), CreateVisualMaterial("Pachinko LCD Frame", new Color(0.05f, 0.16f, 0.23f)));
            if (transform.Find("LCD Screen") == null)
            {
                GameObject existingScreen = CreateCabinetCube("LCD Screen", new Vector3(0f, -0.05f, 0.22f), new Vector3(4.8f, 1.9f, 0.08f), CreateVisualMaterial("Pachinko LCD Glass", new Color(0.015f, 0.06f, 0.12f)));
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

        CreateCabinetCube("LCD Bezel", new Vector3(0f, -0.05f, 0.34f), new Vector3(5.12f, 2.22f, 0.18f), CreateVisualMaterial("Pachinko LCD Frame", new Color(0.05f, 0.16f, 0.23f)));
        GameObject screen = CreateCabinetCube("LCD Screen", new Vector3(0f, -0.05f, 0.22f), new Vector3(4.8f, 1.9f, 0.08f), CreateVisualMaterial("Pachinko LCD Glass", new Color(0.015f, 0.06f, 0.12f)));
        screen.GetComponent<Collider>().enabled = false;
        CreateCabinetOrnaments(brass, blue);
        PositionLCDParts();
        MoveExistingCabinetParts();
    }

    private void PositionLCDParts()
    {
        Transform bezel = transform.Find("LCD Bezel");
        if (bezel != null)
        {
            bezel.localPosition = new Vector3(0f, -0.05f, 0.34f);
            bezel.localScale = new Vector3(5.12f, 2.22f, 0.18f);
        }
        Transform screen = transform.Find("LCD Screen");
        if (screen != null)
        {
            screen.localPosition = new Vector3(0f, -0.05f, 0.22f);
            screen.localScale = new Vector3(4.8f, 1.9f, 0.08f);
        }
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
            float u = x / (float)(size - 1);
            float v = y / (float)(size - 1);
            float dx = u - 0.5f;
            float dy = v - 0.55f;
            float radius = Mathf.Sqrt(dx * dx + dy * dy);
            Color deep = new Color(0.012f, 0.035f, 0.12f);
            Color sapphire = new Color(0.025f, 0.18f, 0.42f);
            Color baseColor = Color.Lerp(deep, sapphire, Mathf.Clamp01(v * 0.52f + 0.24f));
            float bloom = Mathf.Clamp01(1f - radius * 1.65f);
            float angle = Mathf.Atan2(dy, dx);
            float rays = Mathf.Pow(Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos(angle * 12f + radius * 2.2f)), 18f);
            baseColor = Color.Lerp(baseColor, new Color(0.035f, 0.31f, 0.7f), bloom * (0.28f + rays * 0.58f));
            float shimmer = Mathf.Sin(x * 0.25f + y * 0.17f) * Mathf.Sin(y * 0.21f - x * 0.08f);
            baseColor += new Color(0.02f, 0.065f, 0.12f) * Mathf.Max(0f, shimmer) * 0.24f;
            int sparkle = (x * 37 + y * 61) % 263;
            if (sparkle == 0 || sparkle == 1)
                baseColor = Color.Lerp(baseColor, new Color(0.46f, 0.86f, 1f), 0.82f);
            else if ((x * 13 + y * 29) % 997 == 0)
                baseColor = Color.Lerp(baseColor, new Color(0.92f, 0.31f, 0.79f), 0.55f);
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

    private void EnsureCabinetCrown()
    {
        if (transform.Find("Cabinet Crown Base") != null) return;
        Material gold = CreateVisualMaterial("Cabinet Crown Gold", new Color(1f, 0.62f, 0.12f));
        Material blue = CreateVisualMaterial("Cabinet Crown Sapphire", new Color(0.04f, 0.55f, 1f));
        Material ice = CreateVisualMaterial("Cabinet Crown Ice", new Color(0.48f, 0.9f, 1f));

        GameObject crownBase = CreateCabinetCube("Cabinet Crown Base", new Vector3(0f, 4.88f, 0.035f), new Vector3(1.28f, 0.22f, 0.28f), gold);
        crownBase.GetComponent<Collider>().enabled = false;
        GameObject crownTop = CreateCabinetCube("Cabinet Crown Top", new Vector3(0f, 5.02f, 0.035f), new Vector3(0.66f, 0.12f, 0.24f), blue);
        crownTop.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        crownTop.GetComponent<Collider>().enabled = false;
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject wing = CreateCabinetCube("Cabinet Crown Wing " + side, new Vector3(side * 0.48f, 4.99f, 0.04f), new Vector3(0.58f, 0.105f, 0.22f), gold);
            wing.transform.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
            wing.GetComponent<Collider>().enabled = false;
        }

        GameObject centerGem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        centerGem.name = "Cabinet Crown Center Gem";
        centerGem.transform.SetParent(transform, false);
        centerGem.transform.localPosition = new Vector3(0f, 4.93f, -0.08f);
        centerGem.transform.localScale = new Vector3(0.28f, 0.28f, 0.14f);
        centerGem.GetComponent<Collider>().enabled = false;
        centerGem.GetComponent<Renderer>().sharedMaterial = ice;

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject sideGem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sideGem.name = "Cabinet Crown Side Gem " + side;
            sideGem.transform.SetParent(transform, false);
            sideGem.transform.localPosition = new Vector3(side * 0.77f, 4.82f, 0.02f);
            sideGem.transform.localScale = new Vector3(0.16f, 0.16f, 0.11f);
            sideGem.GetComponent<Collider>().enabled = false;
            sideGem.GetComponent<Renderer>().sharedMaterial = blue;
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
            nextLcdDigitAt = Time.time + lcdSpinInterval;
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
            nextAutoPurchase = Time.time + 0.05f;
            TryAutoPurchase();
        }

        RemoveExpiredBalls();
        AnimateAquariumFish();
        AnimateJackpotCameraShake();
    }

    private void EnsureAquariumBackground()
    {
        Transform[] previousFish = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in previousFish)
            if (child != transform && child.name.StartsWith("3D Aquarium Fish ")) child.gameObject.SetActive(false);

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
        EnsurePixelFishSprites();
        for (int i = 0; i < homes.Length; i++)
        {
            GameObject fish = new GameObject("Pixel Aquarium Fish " + i);
            fish.transform.SetParent(transform, false);
            fish.transform.localPosition = homes[i];
            float scale = i % 3 == 0 ? 0.72f : 0.58f;
            fish.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = fish.AddComponent<SpriteRenderer>();
            renderer.sprite = aquariumFishSprites[i % aquariumFishSprites.Length];
            renderer.sortingOrder = 3;
            aquariumFish.Add(fish.transform);
            aquariumFishHomes.Add(homes[i]);
            aquariumFishSpeeds.Add(0.22f + (i % 4) * 0.055f);
        }
    }

    private void RemoveAquariumDecorations()
    {
        Transform[] sceneParts = GetComponentsInChildren<Transform>(true);
        foreach (Transform part in sceneParts)
        {
            if (part == transform) continue;
            string partName = part.name;
            if (partName.StartsWith("Pixel Aquarium Fish ")
                || partName.StartsWith("3D Aquarium Fish ")
                || partName.StartsWith("3D Aquarium Coral ")
                || partName.StartsWith("3D Aquarium Bubble "))
                part.gameObject.SetActive(false);
        }
        aquariumFish.Clear();
        aquariumFishHomes.Clear();
        aquariumFishSpeeds.Clear();
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

    private void EnsurePixelFishSprites()
    {
        if (aquariumFishSprites[0] != null) return;
        Color[] bodies = { new Color(1f, 0.42f, 0.25f), new Color(0.15f, 0.82f, 0.9f), new Color(1f, 0.3f, 0.65f), new Color(0.98f, 0.78f, 0.2f) };
        Color[] stripes = { new Color(1f, 0.91f, 0.68f), new Color(1f, 0.25f, 0.72f), new Color(0.18f, 0.96f, 0.77f), new Color(0.15f, 0.55f, 0.95f) };
        Color ink = new Color(0.025f, 0.07f, 0.18f);
        for (int i = 0; i < aquariumFishSprites.Length; i++)
        {
            Texture2D texture = new Texture2D(64, 40, TextureFormat.RGBA32, false)
            {
                name = "Pixel Tropical Fish " + i,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < 40; y++) for (int x = 0; x < 64; x++) texture.SetPixel(x, y, clear);
            PaintPixelPolygon(texture, new[] { new Vector2(8, 20), new Vector2(1, 29), new Vector2(2, 20), new Vector2(1, 11) }, ink);
            PaintPixelEllipse(texture, 34, 20, 22, 14, ink);
            PaintPixelPolygon(texture, new[] { new Vector2(8, 20), new Vector2(2, 27), new Vector2(3, 20), new Vector2(2, 13) }, stripes[i]);
            PaintPixelPolygon(texture, new[] { new Vector2(25, 28), new Vector2(36, 34), new Vector2(43, 27) }, ink);
            PaintPixelPolygon(texture, new[] { new Vector2(27, 28), new Vector2(36, 32), new Vector2(41, 27) }, stripes[i]);
            PaintPixelEllipse(texture, 34, 20, 19, 11, bodies[i]);
            if (i == 0)
            {
                PaintPixelPolygon(texture, new[] { new Vector2(25, 10), new Vector2(32, 20), new Vector2(26, 30), new Vector2(22, 20) }, stripes[i]);
                PaintPixelPolygon(texture, new[] { new Vector2(41, 11), new Vector2(47, 20), new Vector2(41, 29), new Vector2(37, 20) }, stripes[i]);
            }
            else
            {
                for (int band = 0; band < 3; band++)
                {
                    float cx = 25 + band * 9;
                    PaintPixelLine(texture, new Vector2(cx - 2, 11), new Vector2(cx + 2, 29), stripes[i], 2);
                }
            }
            PaintPixelPolygon(texture, new[] { new Vector2(30, 29), new Vector2(35, 36), new Vector2(41, 28) }, ink);
            PaintPixelPolygon(texture, new[] { new Vector2(32, 29), new Vector2(35, 33), new Vector2(39, 28) }, stripes[i]);
            PaintPixelEllipse(texture, 48, 23, 3, 3, new Color(1f, 0.95f, 0.75f));
            PaintPixelEllipse(texture, 49, 23, 1.5f, 2, ink);
            texture.Apply(false, true);
            aquariumFishSprites[i] = Sprite.Create(texture, new Rect(0, 0, 64, 40), new Vector2(0.5f, 0.5f), 64f);
            aquariumFishSprites[i].name = "Outlined Pixel Fish " + i;
        }
    }

    private static void PaintPixelEllipse(Texture2D texture, float cx, float cy, float rx, float ry, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
        int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(cx + rx));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - ry));
        int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(cy + ry));
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1f) texture.SetPixel(x, y, color);
            }
    }

    private static void PaintPixelPolygon(Texture2D texture, Vector2[] points, Color color)
    {
        float minXf = points[0].x, maxXf = points[0].x, minYf = points[0].y, maxYf = points[0].y;
        for (int i = 1; i < points.Length; i++)
        {
            minXf = Mathf.Min(minXf, points[i].x); maxXf = Mathf.Max(maxXf, points[i].x);
            minYf = Mathf.Min(minYf, points[i].y); maxYf = Mathf.Max(maxYf, points[i].y);
        }
        for (int y = Mathf.Max(0, Mathf.FloorToInt(minYf)); y <= Mathf.Min(texture.height - 1, Mathf.CeilToInt(maxYf)); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(minXf)); x <= Mathf.Min(texture.width - 1, Mathf.CeilToInt(maxXf)); x++)
            {
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                    if ((points[i].y > y) != (points[j].y > y) && x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                        inside = !inside;
                if (inside) texture.SetPixel(x, y, color);
            }
    }

    private static void PaintPixelLine(Texture2D texture, Vector2 from, Vector2 to, Color color, int thickness)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(from, to, steps == 0 ? 0f : (float)i / steps);
            PaintPixelEllipse(texture, point.x, point.y, thickness, thickness, color);
        }
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
            float travelDirection = i % 2 == 0 ? 1f : -1f;
            float phase = Time.time * aquariumFishSpeeds[i] + i * 1.7f;
            float swim = Mathf.Sin(phase) * 0.72f;
            Vector3 position = home + new Vector3(swim * travelDirection, Mathf.Sin(Time.time * 1.3f + i) * 0.12f, 0f);
            fish.localPosition = position;
            float facingDirection = Mathf.Cos(phase) * travelDirection >= 0f ? 1f : -1f;
            fish.localScale = new Vector3(Mathf.Abs(fish.localScale.x) * facingDirection, fish.localScale.y, 1f);
        }
    }

    public void LaunchBatch()
    {
        if (ballPrefab == null || launchPoint == null || isLaunchingBatch || Time.time < nextManualLaunch)
        {
            return;
        }

        nextManualLaunch = Time.time + Mathf.Max(0.08f, 0.22f - reincarnationLaunchSpeedLevel * 0.015f);
        int count = 1 + launchCountLevel + transcendenceVolleyLevel * 2 + divineVolleyLevel * 10;
        PlaySound(launchSound, 0.58f, Random.Range(0.96f, 1.04f));
        isLaunchingBatch = true;
        if (shooterPusher != null) StartCoroutine(AnimateShooter());
        StartCoroutine(LaunchBallsInSequence(count));
        lastResult = count > 1 ? $"{count}発発射！" : "発射！";
    }

    private IEnumerator LaunchBallsInSequence(int count)
    {
        float spacing = Mathf.Clamp(0.06f - reincarnationLaunchSpeedLevel * 0.004f, 0.03f, 0.06f);
        for (int i = 0; i < count; i++)
        {
            SpawnBall();
            if (i < count - 1) yield return new WaitForSeconds(spacing);
        }
        isLaunchingBatch = false;
    }

    private void SpawnBall()
    {
        Vector3 jitter = new Vector3(Random.Range(-0.004f, 0.004f), 0f, 0f);
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
            * Mathf.Pow(2f, transcendenceIncomeLevel)
            * Mathf.Pow(10f, divineIncomeLevel);

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
                money += BigInteger.One;
                lifetimePayout += BigInteger.One;
                runPayout += BigInteger.One;
                lastResult = "保留が満タン　+¥1";
            }

            Destroy(ball.gameObject, 0.05f);
            return;
        }

        BigInteger payout = BigInteger.Max(BigInteger.One, ScaleCurrency(lanePayouts[Mathf.Clamp(laneIndex, 0, lanePayouts.Length - 1)], multiplier));
        lastResult = $"{gateName} +¥{FormatCurrency(payout)}";

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
            PachinkoMode modeAtSpin = gameMode;
            float activeJackpotChance = modeAtSpin == PachinkoMode.Rush ? RushJackpotChance
                : modeAtSpin == PachinkoMode.KomaChallenge ? KomaChallengeChance : JackpotChance;
            bool jackpot = Random.value < activeJackpotChance;
            bool rushFall = modeAtSpin == PachinkoMode.Rush && !jackpot && Random.value < 1f / 117.3f;
            bool entersRush = jackpot && modeAtSpin != PachinkoMode.Normal;
            int rewardBalls = 0;
            bool ubauneJackpot = false;
            if (jackpot)
            {
                if (modeAtSpin == PachinkoMode.Normal)
                {
                    bool specialRushHit = Random.value < 0.005f;
                    rewardBalls = specialRushHit ? 3000 : 300;
                    entersRush = specialRushHit || Random.value < Mathf.Clamp01((KakuhenChance - 0.005f) / 0.995f);
                }
                else if (modeAtSpin == PachinkoMode.KomaChallenge)
                    rewardBalls = 300;
                else if (Random.value < 0.057f)
                {
                    ubauneJackpot = true;
                    float payoutRoll = Random.value;
                    rewardBalls = payoutRoll < 0.5f ? 3000 : payoutRoll < 0.75f ? 4500 : payoutRoll < 0.875f ? 6000 : payoutRoll < 0.9375f ? 7500 : 9000;
                }
                else
                    rewardBalls = Random.value < 0.227f / (1f - 0.057f) ? 3000 : 1500;
            }
            int resultSymbol = jackpot ? Random.Range(1, 10) : 0;
            int[] resultDigits = jackpot
                ? new[] { resultSymbol, resultSymbol, resultSymbol }
                : new[] { Random.Range(1, 10), Random.Range(1, 10), Random.Range(1, 10) };
            if (!jackpot && resultDigits[0] == resultDigits[1] && resultDigits[1] == resultDigits[2])
                resultDigits[2] = resultDigits[2] % 9 + 1;

            lcdChanceUpLevel = RollChanceUpLevel(jackpot);
            lcdChanceCharacterIndex = Random.Range(0, jackpotCharacterSprites.Length);
            // Winning spins run and settle nearly twice as quickly for a sharper hit reveal.
            lcdSpinInterval = jackpot ? 0.035f : 0.068f;
            lcdStoppedDigits = 0;
            nextLcdDigitAt = Time.time;
            RollLcdDigits();
            lcdMessage = GetChanceUpMessage(lcdChanceUpLevel);
            yield return new WaitForSeconds(jackpot ? 1.05f : 2f);

            lcdStoppedDigits = 1;
            lcdDigits[0] = resultDigits[0];
            lcdMessage = resultDigits[0] == resultDigits[1]
                ? $"リーチ！　{GetChanceUpMessage(lcdChanceUpLevel)}" : "数字停止…";
            yield return new WaitForSeconds(jackpot ? 0.28f : 0.55f);
            lcdStoppedDigits = 2;
            lcdDigits[1] = resultDigits[1];
            lcdMessage = resultDigits[0] == resultDigits[1]
                ? $"最終図柄に注目！　{GetChanceUpMessage(lcdChanceUpLevel)}" : "数字停止…";
            yield return new WaitForSeconds(jackpot ? 0.4f : 0.8f);
            lcdStoppedDigits = 3;
            lcdDigits[2] = resultDigits[2];
            // Leave the completed combination on screen before the outcome overlay begins.
            yield return new WaitForSeconds(jackpot ? 0.25f : 0.45f);

            BigInteger payout;
            if (jackpot)
            {
                consecutiveWins++;
                double streakMultiplier = System.Math.Pow(1.35d, consecutiveWins - 1);
                payout = BigInteger.Max(BigInteger.One, ScaleCurrency(rewardBalls * multiplier, streakMultiplier));
                jackpotStreakDisplay = consecutiveWins;
                jackpotPayoutMultiplierDisplay = streakMultiplier;
                if (modeAtSpin == PachinkoMode.Normal)
                    gameMode = entersRush ? PachinkoMode.Rush : PachinkoMode.KomaChallenge;
                else if (modeAtSpin == PachinkoMode.KomaChallenge)
                    gameMode = PachinkoMode.Rush;
                else
                    gameMode = PachinkoMode.Rush;
                if (modeAtSpin == PachinkoMode.Normal && !entersRush) challengeSpinsRemaining = 100;
                if (modeAtSpin == PachinkoMode.Rush && ubauneJackpot) gameMode = PachinkoMode.Rush;
                string alignedDigits = $"{resultSymbol}{resultSymbol}{resultSymbol}";
                lcdMessage = ubauneJackpot ? $"{alignedDigits}　ウバウネ決戦！"
                    : resultSymbol == 7 ? "777　プレミア大当たり！" : $"{alignedDigits}　大当たり！";
                string winStatus = modeAtSpin == PachinkoMode.Normal
                    ? (entersRush ? "RUSH突入！" : "コマチャレンジ突入！")
                    : modeAtSpin == PachinkoMode.KomaChallenge ? "RUSH引き戻し！"
                    : ubauneJackpot ? "ウバウネ決戦！" : "RUSH継続！";
                lastResult = $"大当たり！{winStatus} +¥{FormatCurrency(payout)}";
                jackpotPayoutDisplay = payout;
                jackpotCelebrationMessage = modeAtSpin == PachinkoMode.Normal && entersRush ? "確変突入！"
                    : modeAtSpin == PachinkoMode.Normal ? "コマチャレンジ突入！"
                    : ubauneJackpot ? "ウバウネ決戦！" : "大当たり！";
                celebrationCharacterIndex = Random.Range(0, 4);
                AudioClip characterVoice = celebrationCharacterIndex == 2 ? jackpotMaleVoice : jackpotVoice;
                if (jackpotVoiceSource != null && characterVoice != null)
                    jackpotVoiceSource.PlayOneShot(characterVoice, 1f);
                celebrationCharacterSide = Random.Range(0, 2);
                jackpotCelebrationDuration = 4.5f + Mathf.Min(consecutiveWins - 1, 12) * 0.35f;
                jackpotFlashUntil = Time.time + jackpotCelebrationDuration;
                jackpotEffectStartedAt = Time.time;
                jackpotShakeUntil = Time.time + Mathf.Min(0.75f + consecutiveWins * 0.1f, 1.8f);
                if (jackpotSoundSource != null && jackpotSound != null)
                {
                    jackpotSoundSource.pitch = Mathf.Clamp(1f + (consecutiveWins - 1) * 0.035f, 1f, 1.45f);
                    jackpotSoundSource.PlayOneShot(jackpotSound, 0.62f);
                }
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
                payout = ScaleCurrency(2d, multiplier);
                if (modeAtSpin == PachinkoMode.Rush)
                {
                    if (rushFall)
                    {
                        gameMode = PachinkoMode.KomaChallenge;
                        challengeSpinsRemaining = 100;
                        consecutiveWins = 0;
                        lastResult = "最終決戦敗北…　コマチャレンジ突入！";
                    }
                    else lastResult = "RUSH継続中…";
                }
                else if (modeAtSpin == PachinkoMode.KomaChallenge)
                {
                    challengeSpinsRemaining = Mathf.Max(0, challengeSpinsRemaining - 1);
                    if (challengeSpinsRemaining == 0)
                    {
                        gameMode = PachinkoMode.Normal;
                        lastResult = "コマチャレンジ終了… 通常時へ";
                    }
                    else lastResult = $"コマチャレンジ残り{challengeSpinsRemaining}回";
                    consecutiveWins = 0;
                }
                else
                {
                    consecutiveWins = 0;
                    lastResult = "ヘソ抽選　ハズレ";
                }
                lcdMessage = rushFall ? "最終決戦…　敗北" : resultDigits[0] == resultDigits[1] ? "リーチ…　残念！" : "ハズレ";
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
        jackpotMaleVoice = Resources.Load<AudioClip>("Audio/jackpot_voice_male");
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

    private static BigInteger ScaleCurrency(double baseAmount, double multiplier)
    {
        if (double.IsNaN(baseAmount) || double.IsNaN(multiplier) || baseAmount <= 0d || multiplier <= 0d) return BigInteger.Zero;
        double scaled = baseAmount * multiplier;
        if (double.IsInfinity(scaled) || scaled >= 1e300d) return MaxCurrencyAward;
        return BigInteger.Min(MaxCurrencyAward, new BigInteger(System.Math.Max(0d, System.Math.Round(scaled))));
    }

    private static string FormatCurrency(BigInteger amount)
    {
        return amount.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatMultiplier(double multiplier)
    {
        return multiplier >= 1_000d ? multiplier.ToString("0.##E+0") : multiplier.ToString("0.00");
    }

    private const float BaseNormalJackpotChance = 1f / 319.7f;
    private float JackpotChance => GetJackpotChance(jackpotLevel);
    private float RushJackpotChance => Mathf.Clamp(1f / 41.9f + JackpotChance - BaseNormalJackpotChance, 0.0001f, 0.9f);
    private float KomaChallengeChance => Mathf.Clamp(1f / 399.6f + JackpotChance - BaseNormalJackpotChance, 0.0001f, 0.9f);
    private float GetJackpotChance(int level) => GetJackpotChance(level, divineJackpotLevel);
    private float GetJackpotChance(int level, int divineLevel) => Mathf.Clamp(BaseNormalJackpotChance + level * 0.02f + jackpotMemoryUpgradeLevel * 0.01f + transcendenceJackpotLevel * 0.05f + divineLevel * 0.1f, BaseNormalJackpotChance, 0.9f);
    private int GetJackpotUpgradeLimit()
    {
        int level = jackpotLevel;
        while (level < 22 && GetJackpotChance(level + 1) > GetJackpotChance(level)) level++;
        return level;
    }
    private float KakuhenChance => GetKakuhenChance(probabilityChangeLevel);
    private static float GetKakuhenChance(int level) => Mathf.Clamp(0.5f + level * 0.05f, 0.5f, 0.95f);
    private int GetKakuhenUpgradeLimit()
    {
        int level = probabilityChangeLevel;
        while (level < 14 && GetKakuhenChance(level + 1) > GetKakuhenChance(level)) level++;
        return level;
    }
    private int GetDivineJackpotUpgradeLimit()
    {
        int level = divineJackpotLevel;
        while (level < 5 && GetJackpotChance(jackpotLevel, level + 1) > GetJackpotChance(jackpotLevel, level))
        {
            level++;
        }
        return level;
    }
    private int MaxPendingBalls => 4 + holdCapacityUpgradeLevel;

    private void OnGUI()
    {
        EnsureStyles();
        float w = Screen.width;
        float h = Screen.height;
        float top = Mathf.Clamp(h * 0.09f, 62f, 92f);
        float boardWidth = w * 0.60f;
        DrawCabinetBackdrop(w, h);
        DrawHud(new Rect(0f, 0f, w, top));
        DrawPlayfieldFrame(new Rect(4f, top + 3f, boardWidth - 8f, h - top - 80f));
        Rect lcdRect = GetLCDScreenRect(boardWidth, h);
        DrawLCD(lcdRect);
        float hesoLabelY = Mathf.Min(lcdRect.yMax + 2f, h - 112f);
        GUI.Label(new Rect(boardWidth * 0.32f, hesoLabelY, boardWidth * 0.36f, 30f), "▼　ヘソ　保留 +1　▼", hesoLabelStyle);
        DrawBoardControls(new Rect(8f, h - 76f, boardWidth - 16f, 68f));
        DrawSidePanel(new Rect(boardWidth + 6f, top + 4f, w - boardWidth - 10f, h - top - 10f));
        if (Time.time < jackpotFlashUntil) DrawJackpotCelebration(boardWidth, h);
    }

    private void DrawCabinetBackdrop(float width, float height)
    {
        Color old = GUI.color;
        float split = width * 0.60f;
        float top = Mathf.Clamp(height * 0.09f, 62f, 92f);
        GUI.color = new Color(0.006f, 0.012f, 0.035f, 0.035f);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);

        // Tint the live 3D playfield lightly; keep its pegs, balls and new backdrop visible.
        GUI.color = new Color(0.018f, 0.095f, 0.28f, 0.12f);
        GUI.DrawTexture(new Rect(0f, top, split, height - top), Texture2D.whiteTexture);
        GUI.color = new Color(0.009f, 0.026f, 0.075f, 0.99f);
        GUI.DrawTexture(new Rect(split, 0f, width - split, height), Texture2D.whiteTexture);
        GUI.color = new Color(0.04f, 0.18f, 0.34f, 0.86f);
        GUI.DrawTexture(new Rect(0f, top, split, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(split - 3f, 0f, 3f, height), Texture2D.whiteTexture);

        // Subtle luminous facets make the empty outer space read as part of the cabinet.
        float glow = 0.25f + Mathf.PingPong(Time.time * 0.16f, 0.18f);
        GUI.color = new Color(0.03f, 0.42f, 0.82f, glow);
        GUI.DrawTexture(new Rect(0f, top + 4f, 5f, height - top - 80f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(split - 7f, top + 4f, 5f, height - top - 80f), Texture2D.whiteTexture);
        for (int i = 0; i < 12; i++)
        {
            float x = Mathf.Repeat(i * 139f + 41f, width);
            float y = top + 26f + Mathf.Repeat(i * 83f, Mathf.Max(1f, height - top - 125f));
            float size = i % 3 == 0 ? 4f : 2f;
            GUI.color = new Color(0.15f, 0.68f, 1f, 0.18f + 0.12f * Mathf.Sin(Time.time * 1.5f + i));
            GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
        }
        GUI.color = old;
    }

    private void DrawPlayfieldFrame(Rect rect)
    {
        Color old = GUI.color;
        float t = Mathf.Clamp(Screen.height * 0.019f, 12f, 22f);
        GUI.color = new Color(0.38f, 0.2f, 0.07f, 1f);
        const float shadow = 2f;
        GUI.DrawTexture(new Rect(rect.x - shadow, rect.y - shadow, rect.width + shadow * 2f, shadow), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x - shadow, rect.yMax, rect.width + shadow * 2f, shadow), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x - shadow, rect.y, shadow, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax, rect.y, shadow, rect.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.62f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - t, rect.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, t, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - t, rect.y, t, rect.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.035f, 0.11f, 0.23f, 1f);
        float bevel = Mathf.Max(2f, t * 0.22f);
        GUI.DrawTexture(new Rect(rect.x + bevel, rect.y + bevel, rect.width - bevel * 2f, t - bevel), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x + bevel, rect.yMax - t + bevel, rect.width - bevel * 2f, t - bevel), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x + bevel, rect.y + t, t - bevel, rect.height - t * 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - t + bevel, rect.y + t, t - bevel, rect.height - t * 2f), Texture2D.whiteTexture);
        GUI.color = new Color(0.12f, 0.68f, 1f, 0.96f);
        GUI.DrawTexture(new Rect(rect.x + t, rect.y + t, rect.width - t * 2f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x + t, rect.yMax - t - 2f, rect.width - t * 2f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x + t, rect.y + t, 2f, rect.height - t * 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - t - 2f, rect.y + t, 2f, rect.height - t * 2f), Texture2D.whiteTexture);
        for (int i = 0; i < 7; i++)
        {
            float y = Mathf.Lerp(rect.y + t * 1.5f, rect.yMax - t * 1.5f, i / 6f);
            DrawFrameGem(rect.x + t * 0.5f, y, t * 0.6f);
            DrawFrameGem(rect.xMax - t * 0.5f, y, t * 0.6f);
        }
        DrawFrameGem(rect.center.x, rect.y + t * 0.5f, t * 0.72f);
        DrawFrameGem(rect.center.x, rect.yMax - t * 0.5f, t * 0.72f);
        GUI.color = old;
    }

    private void DrawFrameGem(float x, float y, float size)
    {
        Color old = GUI.color;
        Rect glow = new Rect(x - size * 0.72f, y - size * 0.72f, size * 1.44f, size * 1.44f);
        GUI.color = new Color(0.05f, 0.58f, 1f, 0.28f);
        GUI.DrawTexture(glow, GetCelebrationCircle());
        Rect gem = new Rect(x - size * 0.5f, y - size * 0.5f, size, size);
        GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
        GUI.color = new Color(0.94f, 0.59f, 0.15f, 1f);
        GUI.DrawTexture(gem, Texture2D.whiteTexture);
        GUI.color = new Color(0.08f, 0.31f, 0.59f, 1f);
        float inset = size * 0.16f;
        GUI.DrawTexture(new Rect(gem.x + inset, gem.y + inset, size - inset * 2f, size - inset * 2f), Texture2D.whiteTexture);
        GUI.color = new Color(0.5f, 0.92f, 1f, 1f);
        float core = size * 0.29f;
        GUI.DrawTexture(new Rect(x - core * 0.5f, y - core * 0.5f, core, core), Texture2D.whiteTexture);
        GUIUtility.RotateAroundPivot(-45f, new Vector2(x, y));
        GUI.color = old;
    }

    private void DrawMetalPanel(Rect rect, float thickness = 3f)
    {
        Color old = GUI.color;
        GUI.color = new Color(0.5f, 0.28f, 0.08f, 1f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.96f, 0.63f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(rect.x + thickness, rect.y + thickness, rect.width - thickness * 2f, rect.height - thickness * 2f), Texture2D.whiteTexture);
        GUI.color = new Color(0.12f, 0.34f, 0.62f, 1f);
        GUI.DrawTexture(new Rect(rect.x + thickness * 2f, rect.y + thickness * 2f, rect.width - thickness * 4f, rect.height - thickness * 4f), Texture2D.whiteTexture);
        GUI.color = new Color(0.012f, 0.035f, 0.09f, 0.99f);
        GUI.DrawTexture(new Rect(rect.x + thickness * 3f, rect.y + thickness * 3f, rect.width - thickness * 6f, rect.height - thickness * 6f), Texture2D.whiteTexture);
        GUI.color = new Color(0.38f, 0.83f, 1f, 0.76f);
        GUI.DrawTexture(new Rect(rect.x + thickness * 2f, rect.y + thickness * 2f, rect.width - thickness * 4f, 1f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.82f, 0.39f, 0.8f);
        GUI.DrawTexture(new Rect(rect.x + thickness * 2f, rect.y + thickness * 2f, rect.width - thickness * 4f, 1f), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void DrawJackpotCelebration(float boardWidth, float screenHeight)
    {
        float remaining = Mathf.Clamp01((jackpotFlashUntil - Time.time) / Mathf.Max(0.1f, jackpotCelebrationDuration));
        float elapsed = Mathf.Max(0f, Time.time - jackpotEffectStartedAt);
        float streakTier = Mathf.Clamp(jackpotStreakDisplay - 1, 0, 10);
        float pulse = 0.82f + Mathf.PingPong(Time.time * (6f + streakTier * 0.7f), 0.34f + streakTier * 0.035f);
        Color oldColor = GUI.color;

        DrawJackpotImpactFlash(boardWidth, screenHeight, elapsed);
        DrawJackpotStarburst(boardWidth, screenHeight, elapsed, remaining);
        DrawPFlashFrame(boardWidth, screenHeight, elapsed);
        GUI.color = new Color(0.08f, 0.28f, 0.62f, 0.012f + 0.022f * pulse * remaining);
        float effectTop = Mathf.Clamp(screenHeight * 0.085f + 8f, 60f, 98f);
        float effectBottom = screenHeight - 74f;
        GUI.DrawTexture(new Rect(8f, effectTop, Mathf.Max(0f, boardWidth - 16f), Mathf.Max(0f, effectBottom - effectTop)), Texture2D.whiteTexture);
        DrawJackpotSpotlights(boardWidth, screenHeight, elapsed, remaining);
        int petalCount = 64 + Mathf.Min(jackpotStreakDisplay - 1, 8) * 12;
        for (int i = 0; i < petalCount; i++)
        {
            float fall = Mathf.Repeat(elapsed * (0.42f + (i % 5) * 0.035f) + i * 0.091f, 1f);
            float size = 8f + (i % 5) * 2.5f;
            float fadeIn = Mathf.SmoothStep(0f, 0.14f, fall);
            float fadeOut = 1f - Mathf.SmoothStep(0.84f, 1f, fall);
            float xMargin = 24f;
            float x = xMargin + Mathf.Repeat(i * 0.173f + Mathf.Sin(elapsed * 2.2f + i) * 0.06f, 1f) * Mathf.Max(1f, boardWidth - xMargin * 2f);
            float safeTop = effectTop + 18f;
            float safeBottom = effectBottom - 22f;
            float travelHeight = Mathf.Max(1f, safeBottom - safeTop - size * 2.8f);
            float y = safeTop + size + fall * travelHeight + Mathf.Sin(elapsed * 2f + i * 0.8f) * 10f;
            float angle = Mathf.Sin(Time.time * 3.2f + i * 1.7f) * 70f;
            GUIUtility.RotateAroundPivot(angle, new Vector2(x, y));
            float petalAlpha = remaining * fadeIn * fadeOut;
            GUI.color = i % 4 == 0 ? new Color(1f, 0.76f, 0.2f, petalAlpha) : i % 3 == 0 ? new Color(1f, 0.42f, 0.69f, petalAlpha) : new Color(1f, 0.84f, 0.9f, petalAlpha);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size, size, size * 1.8f), GetCelebrationCircle());
            GUIUtility.RotateAroundPivot(-angle, new Vector2(x, y));
        }

        float panelWidth = Mathf.Min(Mathf.Max(0f, boardWidth - 12f), 760f);
        float panelHeight = Mathf.Clamp(screenHeight * 0.3f, 106f, 166f);
        float bubbleHeight = Mathf.Clamp(screenHeight * 0.11f, 30f, 54f);
        bool compactCelebration = screenHeight < 400f;
        float headerBottom = Mathf.Clamp(screenHeight * 0.085f, 58f, 82f);
        float panelTopMin = compactCelebration ? headerBottom + 4f : bubbleHeight + 14f;
        float panelTopMax = Mathf.Max(panelTopMin, screenHeight - panelHeight - 76f);
        float panelTop = Mathf.Clamp(screenHeight * 0.24f - panelHeight * 0.5f, panelTopMin, panelTopMax);
        Rect panel = new Rect((boardWidth - panelWidth) * 0.5f, panelTop, panelWidth, panelHeight);
        GUI.color = new Color(1f, 0.34f + Mathf.PingPong(Time.time * 3.8f, 0.48f), 0.04f, 0.99f * remaining);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.025f, 0.045f, 0.12f, 0.98f * remaining);
        GUI.DrawTexture(new Rect(panel.x + 5f, panel.y + 5f, panel.width - 10f, panel.height - 10f), Texture2D.whiteTexture);

        float titleRightPadding = 18f;
        float titleY = compactCelebration ? panel.y + bubbleHeight + 4f : panel.y + 8f;
        float titleHeight = compactCelebration ? panel.height * 0.28f : panel.height * 0.5f;
        Rect titleRect = new Rect(panel.x + 10f, titleY, Mathf.Max(40f, panel.width - titleRightPadding - 18f), titleHeight);
        float headlineSize = Mathf.Min(titleRect.width / 5.7f, titleRect.height / 1.22f, 72f);
        float headlinePulse = 0.95f + Mathf.PingPong(Time.time * (3f + streakTier * 0.2f), 0.05f);
        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, Mathf.RoundToInt(headlineSize * headlinePulse)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            normal = { textColor = new Color(1f, 0.88f, 0.2f, remaining) }
        };
        float detailY = compactCelebration ? panel.y + panel.height * 0.66f : panel.y + panel.height * 0.62f;
        float detailHeight = compactCelebration ? panel.height * 0.31f : panel.height * 0.34f;
        Rect detailRect = new Rect(panel.x + 8f, detailY, Mathf.Max(40f, panel.width - titleRightPadding - 12f), detailHeight);
        GUIStyle detail = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(detailRect.width / 20f, detailRect.height / 2.2f)), 9, 22),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            normal = { textColor = new Color(0.72f, 0.91f, 1f, remaining) }
        };
        GUI.color = Color.white;
        DrawOutlinedCelebrationLabel(titleRect, "大当たり!!", title, new Color(0.82f, 0.12f, 0.2f, remaining), 3f);
        string kakuhenTag = !compactCelebration && jackpotCelebrationMessage == "確変突入！" ? "確変突入！　" : "";
        string streakDetail = $"{kakuhenTag}連チャン {jackpotStreakDisplay}回　倍率×{FormatMultiplier(jackpotPayoutMultiplierDisplay)}\n+¥{FormatCurrency(jackpotPayoutDisplay)}";
        GUI.Label(new Rect(detailRect.x + 2f, detailRect.y + 2f, detailRect.width, detailRect.height), streakDetail, detail);
        GUI.Label(detailRect, streakDetail, detail);
        float characterTopLimit = panel.yMax + 8f;
        float characterHeight = Mathf.Min(290f, screenHeight * 0.43f, Mathf.Max(0f, screenHeight - 82f - characterTopLimit));
        float characterWidth = Mathf.Min(boardWidth * 0.32f, characterHeight * 0.46f);
        float characterX = celebrationCharacterSide == 0 ? boardWidth * 0.035f : boardWidth - characterWidth - boardWidth * 0.035f;
        float characterY = screenHeight - 82f - characterHeight;
        DrawCelebrationCharacter(new Rect(characterX, characterY, characterWidth, characterHeight), pulse);
        DrawCharacterSpeechBubble(panel, remaining, headlinePulse, bubbleHeight);
        GUI.color = oldColor;
    }

    private void DrawPFlashFrame(float boardWidth, float screenHeight, float elapsed)
    {
        float chainTier = Mathf.Min(jackpotStreakDisplay - 1, 10);
        if (elapsed > 1.6f + chainTier * 0.16f) return;
        float pulse = Mathf.PingPong(elapsed * 8f, 1f);
        float alpha = Mathf.Clamp(0.35f + pulse * 0.55f + chainTier * 0.025f, 0f, 0.98f);
        float thickness = Mathf.Clamp(screenHeight * (0.005f + chainTier * 0.00015f), 3f, 9f);
        float top = Mathf.Clamp(screenHeight * 0.085f + 8f, 60f, 98f);
        float bottom = screenHeight - 74f;
        Color oldColor = GUI.color;
        Color frameColor = Color.Lerp(new Color(0.12f, 0.72f, 1f), new Color(0.98f, 0.18f, 0.68f), Mathf.Clamp01(chainTier / 8f));
        GUI.color = new Color(frameColor.r, frameColor.g, frameColor.b, alpha);
        GUI.DrawTexture(new Rect(8f, top, thickness, bottom - top), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boardWidth - thickness - 8f, top, thickness, bottom - top), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(8f, top, boardWidth - 16f, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(8f, bottom - thickness, boardWidth - 16f, thickness), Texture2D.whiteTexture);
        GUI.color = new Color(0.88f, 1f, 0.94f, alpha * 0.85f);
        GUI.DrawTexture(new Rect(thickness + 9f, top + thickness, 2f, bottom - top - thickness * 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(boardWidth - thickness - 11f, top + thickness, 2f, bottom - top - thickness * 2f), Texture2D.whiteTexture);
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

    private void DrawCharacterSpeechBubble(Rect panel, float remaining, float pulse, float height)
    {
        if (jackpotCelebrationMessage == "確変突入！")
        {
            DrawKakuhenEntryBanner(panel, height);
            return;
        }

        float width = Mathf.Min(panel.width * 0.82f, 460f);
        float bubbleY = Screen.height < 400f ? panel.y + 2f : Mathf.Max(6f, panel.y - height - 8f);
        Rect bubble = new Rect(panel.x + (panel.width - width) * 0.5f, bubbleY, width, height);
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
        string fullSpeech = jackpotCelebrationMessage == "確変突入！" ? "確変突入！いっくよー！！"
            : jackpotStreakDisplay <= 1 ? "大当たりー！！"
            : jackpotStreakDisplay < 4 ? $"連チャン {jackpotStreakDisplay}回！まだまだー！！"
            : $"{jackpotStreakDisplay}連チャン！止まらないー！！";
        float speechSize = Mathf.Min((width - 20f) / fullSpeech.Length, (height - 8f) / 1.25f, 34f);
        speech.fontSize = Mathf.Max(9, Mathf.RoundToInt(speechSize * pulse));
        int shown = Mathf.Clamp(Mathf.FloorToInt((Time.time - jackpotEffectStartedAt) * 18f) + 1, 1, fullSpeech.Length);
        DrawOutlinedCelebrationLabel(new Rect(bubble.x + 5f, bubble.y + 2f, bubble.width - 10f, bubble.height - 4f),
            fullSpeech.Substring(0, shown), speech, new Color(1f, 0.74f, 0.15f, remaining), 1.5f);
    }

    private void DrawKakuhenEntryBanner(Rect panel, float height)
    {
        const float enterDuration = 0.38f;
        const float holdDuration = 0.5f;
        const float exitDuration = 0.42f;
        float elapsed = Mathf.Max(0f, Time.time - jackpotEffectStartedAt);
        float totalDuration = enterDuration + holdDuration + exitDuration;
        if (elapsed >= totalDuration) return;

        float width = Mathf.Min(panel.width * 0.82f, 460f);
        float centerX = panel.x + (panel.width - width) * 0.5f;
        float x;
        float alpha = 1f;
        if (elapsed < enterDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / enterDuration);
            x = Mathf.Lerp(-width, centerX, t);
            alpha = Mathf.Clamp01(elapsed / 0.1f);
        }
        else if (elapsed < enterDuration + holdDuration)
        {
            x = centerX;
        }
        else
        {
            float t = Mathf.SmoothStep(0f, 1f, (elapsed - enterDuration - holdDuration) / exitDuration);
            x = Mathf.Lerp(centerX, panel.xMax + width, t);
            alpha = 1f - Mathf.Clamp01((elapsed - enterDuration - holdDuration) / (exitDuration * 0.72f));
        }

        float y = Mathf.Max(6f, panel.y - height - 8f);
        Rect banner = new Rect(x, y, width, height);
        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 0.62f, 0.06f, 0.98f * alpha);
        GUI.DrawTexture(new Rect(banner.x - 3f, banner.y - 3f, banner.width + 6f, banner.height + 6f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 0.94f, 0.99f * alpha);
        GUI.DrawTexture(banner, Texture2D.whiteTexture);
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(height * 0.52f), 16, 34),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            normal = { textColor = new Color(0.82f, 0.12f, 0.08f, alpha) }
        };
        GUI.Label(banner, "確変突入!!", style);
        GUI.color = oldColor;
    }

    private void DrawJackpotImpactFlash(float boardWidth, float screenHeight, float elapsed)
    {
        float tier = Mathf.Min(jackpotStreakDisplay - 1, 10);
        float duration = 0.95f + tier * 0.07f;
        if (elapsed > duration) return;
        float wave = Mathf.Max(0f, Mathf.Sin((elapsed + 0.04f) * (11f + tier * 0.8f)));
        float envelope = 1f - elapsed / duration;
        float alpha = Mathf.Pow(wave, 2.2f) * envelope * Mathf.Min(0.48f + tier * 0.035f, 0.78f);
        if (alpha <= 0.01f) return;
        Color oldColor = GUI.color;
        Color flashColor = Color.Lerp(new Color(0.22f, 0.82f, 1f), new Color(1f, 0.24f, 0.66f), Mathf.Clamp01(tier / 8f));
        GUI.color = elapsed < 0.2f ? new Color(1f, 1f, 1f, alpha) : new Color(flashColor.r, flashColor.g, flashColor.b, alpha * 0.9f);
        float top = Mathf.Clamp(screenHeight * 0.085f + 8f, 60f, 98f);
        float bottom = screenHeight - 74f;
        GUI.DrawTexture(new Rect(8f, top, Mathf.Max(0f, boardWidth - 16f), Mathf.Max(0f, bottom - top)), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawJackpotSpotlights(float boardWidth, float screenHeight, float elapsed, float remaining)
    {
        EnsureSpotlightBeamTexture();
        Color oldColor = GUI.color;
        Rect lcd = GetLCDScreenRect(boardWidth, screenHeight);
        Vector2 focalPoint = lcd.center;
        float top = Mathf.Clamp(screenHeight * 0.085f + 10f, 62f, 102f);
        float tier = Mathf.Min(jackpotStreakDisplay - 1, 10);
        Color[] colors = { new Color(0.2f, 0.88f, 1f), new Color(1f, 0.24f, 0.62f), new Color(1f, 0.78f, 0.2f), new Color(0.62f, 0.34f, 1f) };
        const int beamCount = 8;
        for (int i = 0; i < beamCount; i++)
        {
            float originX = boardWidth * (0.04f + i * 0.92f / (beamCount - 1));
            Vector2 origin = new Vector2(originX, top);
            float sweep = Mathf.Sin(elapsed * (1.7f + i % 3 * 0.25f) + i * 1.43f);
            Vector2 target = focalPoint + new Vector2(sweep * boardWidth * 0.22f, Mathf.Sin(elapsed * 2.1f + i) * screenHeight * 0.055f);
            Vector2 ray = target - origin;
            float length = ray.magnitude;
            float width = Mathf.Lerp(14f, 38f, (0.5f + 0.5f * Mathf.Sin(elapsed * 2.8f + i * 1.1f))) + tier * 1.6f;
            float alpha = (0.075f + tier * 0.012f) * (0.55f + 0.45f * Mathf.Sin(elapsed * 5f + i * 0.9f) * 0.5f + 0.225f) * remaining;
            float angle = Mathf.Atan2(ray.x, ray.y) * Mathf.Rad2Deg;
            Vector2 center = (origin + target) * 0.5f;
            Rect beam = new Rect(center.x - width * 0.5f, center.y - length * 0.5f, width, length);
            GUIUtility.RotateAroundPivot(angle, center);
            Color beamColor = colors[i % colors.Length];
            GUI.color = new Color(beamColor.r, beamColor.g, beamColor.b, alpha);
            GUI.DrawTexture(beam, spotlightBeamTexture, ScaleMode.StretchToFill, true);
            GUIUtility.RotateAroundPivot(-angle, center);
        }
        GUI.color = oldColor;
    }

    private static void EnsureSpotlightBeamTexture()
    {
        if (spotlightBeamTexture != null) return;
        const int width = 48;
        const int height = 128;
        spotlightBeamTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Soft Pachi Spotlight Beam",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float across = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                float edgeFade = Mathf.Pow(Mathf.Clamp01(1f - across), 2.2f);
                float endFade = Mathf.SmoothStep(0.15f, 1f, (float)y / (height - 1)) * 0.8f + 0.2f;
                pixels[y * width + x] = new Color(1f, 1f, 1f, edgeFade * endFade);
            }
        spotlightBeamTexture.SetPixels(pixels);
        spotlightBeamTexture.Apply(false, true);
    }

    private void DrawJackpotStarburst(float boardWidth, float screenHeight, float elapsed, float remaining)
    {
        float tier = Mathf.Min(jackpotStreakDisplay - 1, 10);
        float burst = Mathf.Clamp01(1f - elapsed / (1.2f + tier * 0.1f));
        if (burst <= 0f) return;
        Color oldColor = GUI.color;
        Vector2 center = GetLCDScreenRect(boardWidth, screenHeight).center;
        float rotation = Time.time * 28f;
        int rayCount = 20 + Mathf.RoundToInt(tier * 4f);
        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * (360f / rayCount) + rotation;
            float length = (38f + Mathf.PingPong(Time.time * (110f + tier * 8f) + i * 13f, 76f + tier * 8f)) * burst;
            float width = (3f + (i % 3) + tier * 0.45f) * burst;
            GUIUtility.RotateAroundPivot(angle, center);
            Color rayColor = Color.Lerp(new Color(1f, 0.83f, 0.24f), new Color(0.35f, 0.92f, 1f), Mathf.Clamp01(tier / 8f));
            GUI.color = i % 3 == 0 ? new Color(1f, 0.38f, 0.62f, burst * remaining) : new Color(rayColor.r, rayColor.g, rayColor.b, burst * remaining);
            GUI.DrawTexture(new Rect(center.x - width * 0.5f, center.y - 118f - length, width, length), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, center);
        }
        GUI.color = oldColor;
    }

    private void AnimateJackpotCameraShake()
    {
        if (!jackpotCameraShaking || jackpotCamera == null) return;
        if (Time.time < jackpotShakeUntil)
        {
            float strength = (jackpotShakeUntil - Time.time) * Mathf.Min(0.13f + (jackpotStreakDisplay - 1) * 0.025f, 0.34f);
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
        Texture2D sprite = jackpotCharacterSprites[Mathf.Clamp(celebrationCharacterIndex, 0, jackpotCharacterSprites.Length - 1)];
        if (sprite == null) return;
        float elapsed = Mathf.Max(0f, Time.time - jackpotEffectStartedAt);
        float bounce = Mathf.Abs(Mathf.Sin(elapsed * 7.2f));
        float scale = 0.94f + bounce * 0.11f + (pulse - 0.82f) * 0.05f;
        float tilt = Mathf.Sin(elapsed * 4.8f) * 3.5f;
        Rect characterRect = new Rect(rect.center.x - rect.width * scale * 0.5f,
            rect.y + rect.height * (1f - scale) * 0.5f - bounce * 5f, rect.width * scale, rect.height * scale);
        Color oldColor = GUI.color;
        GUI.color = Color.white;
        GUIUtility.RotateAroundPivot(tilt, characterRect.center);
        GUI.DrawTexture(characterRect, sprite, ScaleMode.ScaleToFit, true);
        GUIUtility.RotateAroundPivot(-tilt, characterRect.center);
        GUI.color = oldColor;
    }

    private void Ensure2DJackpotCharacters()
    {
        for (int i = 0; i < jackpotCharacterSprites.Length; i++)
            if (jackpotCharacterSprites[i] == null)
                jackpotCharacterSprites[i] = Resources.Load<Texture2D>("JackpotCharacters/Character" + i) ?? CreatePixelCharacterSprite(i);
    }

    private static Texture2D CreatePixelCharacterSprite(int variant)
    {
        const int width = 48;
        const int height = 72;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color ink = new Color(0.08f, 0.045f, 0.12f);
        Color skin = new Color(1f, 0.72f, 0.57f);
        Color[] hairs = { new Color(0.22f, 0.075f, 0.18f), new Color(0.94f, 0.47f, 0.12f), new Color(0.18f, 0.12f, 0.32f), new Color(0.08f, 0.34f, 0.38f) };
        Color[] outfits = { new Color(0.84f, 0.12f, 0.27f), new Color(0.12f, 0.42f, 0.88f), new Color(0.72f, 0.18f, 0.68f), new Color(0.08f, 0.62f, 0.48f) };
        Color[] accents = { new Color(1f, 0.72f, 0.12f), new Color(0.34f, 0.92f, 1f), new Color(1f, 0.5f, 0.82f), new Color(0.8f, 1f, 0.38f) };
        Color hair = hairs[variant % hairs.Length], outfit = outfits[variant % outfits.Length], accent = accents[variant % accents.Length];
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Pixel Jackpot Character " + variant,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) texture.SetPixel(x, y, clear);

        // Hair behind the body, then the sleeves, legs and full-length festival kimono.
        PaintPixelEllipse(texture, 24, 52, 14, 16, ink);
        PaintPixelEllipse(texture, 24, 52, 12, 15, hair);
        if (variant == 0 || variant == 2)
        {
            PaintPixelEllipse(texture, 10, 43, 5, 16, ink); PaintPixelEllipse(texture, 10, 44, 3, 15, hair);
            PaintPixelEllipse(texture, 38, 42, 5, 17, ink); PaintPixelEllipse(texture, 38, 43, 3, 16, hair);
        }
        else if (variant == 1)
        {
            PaintPixelEllipse(texture, 8, 53, 5, 7, ink); PaintPixelEllipse(texture, 8, 53, 3, 5, hair);
            PaintPixelEllipse(texture, 40, 53, 5, 7, ink); PaintPixelEllipse(texture, 40, 53, 3, 5, hair);
        }
        PaintPixelEllipse(texture, 19, 12, 4, 8, ink); PaintPixelEllipse(texture, 19, 12, 2, 7, skin);
        PaintPixelEllipse(texture, 29, 12, 4, 8, ink); PaintPixelEllipse(texture, 29, 12, 2, 7, skin);
        PaintPixelPolygon(texture, new[] { new Vector2(15, 5), new Vector2(23, 5), new Vector2(22, 13), new Vector2(17, 13) }, ink);
        PaintPixelPolygon(texture, new[] { new Vector2(25, 5), new Vector2(33, 5), new Vector2(31, 13), new Vector2(26, 13) }, ink);
        PaintPixelPolygon(texture, new[] { new Vector2(14, 4), new Vector2(23, 4), new Vector2(22, 11), new Vector2(17, 11) }, new Color(0.25f, 0.18f, 0.3f));
        PaintPixelPolygon(texture, new[] { new Vector2(25, 4), new Vector2(34, 4), new Vector2(31, 11), new Vector2(26, 11) }, new Color(0.25f, 0.18f, 0.3f));
        PaintPixelEllipse(texture, 11, 33, 9, 11, ink); PaintPixelEllipse(texture, 11, 34, 7, 9, outfit);
        PaintPixelEllipse(texture, 37, 33, 9, 11, ink); PaintPixelEllipse(texture, 37, 34, 7, 9, outfit);
        PaintPixelEllipse(texture, 12, 27, 3, 3, skin); PaintPixelEllipse(texture, 36, 27, 3, 3, skin);
        PaintPixelPolygon(texture, new[] { new Vector2(15, 24), new Vector2(33, 24), new Vector2(40, 4), new Vector2(8, 4) }, ink);
        PaintPixelPolygon(texture, new[] { new Vector2(16, 23), new Vector2(32, 23), new Vector2(37, 6), new Vector2(11, 6) }, outfit);
        PaintPixelPolygon(texture, new[] { new Vector2(17, 23), new Vector2(31, 23), new Vector2(32, 19), new Vector2(16, 19) }, accent);
        PaintPixelLine(texture, new Vector2(24, 7), new Vector2(24, 22), new Color(1f, 0.84f, 0.35f), 1);
        PaintPixelEllipse(texture, 24, 38, 13, 8, ink); PaintPixelEllipse(texture, 24, 39, 11, 7, outfit);
        PaintPixelEllipse(texture, 24, 51, 10, 12, ink); PaintPixelEllipse(texture, 24, 51, 8, 10, skin);
        PaintPixelEllipse(texture, 14, 50, 2, 3, skin); PaintPixelEllipse(texture, 34, 50, 2, 3, skin);
        PaintPixelEllipse(texture, 24, 59, 12, 8, ink); PaintPixelEllipse(texture, 24, 59, 10, 6, hair);
        PaintPixelPolygon(texture, new[] { new Vector2(14, 58), new Vector2(21, 59), new Vector2(19, 52), new Vector2(15, 55) }, hair);
        PaintPixelPolygon(texture, new[] { new Vector2(34, 58), new Vector2(27, 59), new Vector2(29, 52), new Vector2(33, 55) }, hair);
        PaintPixelEllipse(texture, 20, 51, 1.6f, 2.4f, new Color(0.22f, 0.08f, 0.16f));
        PaintPixelEllipse(texture, 28, 51, 1.6f, 2.4f, new Color(0.22f, 0.08f, 0.16f));
        PaintPixelEllipse(texture, 20, 52, 0.6f, 0.8f, Color.white); PaintPixelEllipse(texture, 28, 52, 0.6f, 0.8f, Color.white);
        PaintPixelEllipse(texture, 17, 47, 2f, 1f, new Color(1f, 0.38f, 0.48f, 0.85f));
        PaintPixelEllipse(texture, 31, 47, 2f, 1f, new Color(1f, 0.38f, 0.48f, 0.85f));
        PaintPixelEllipse(texture, 24, 46, 1f, 0.7f, new Color(0.55f, 0.18f, 0.25f));
        if (variant == 0 || variant == 2)
        {
            PaintPixelEllipse(texture, 8, 58, 4, 3, ink); PaintPixelEllipse(texture, 8, 58, 2, 2, accent);
            PaintPixelEllipse(texture, 40, 58, 4, 3, ink); PaintPixelEllipse(texture, 40, 58, 2, 2, accent);
        }
        PaintPixelEllipse(texture, variant % 2 == 0 ? 32 : 16, 63, 4, 4, ink);
        PaintPixelEllipse(texture, variant % 2 == 0 ? 32 : 16, 63, 2, 2, accent);
        texture.Apply(false, true);
        return texture;
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
        Color spinColor = new Color(0.12f, 0.28f, 0.68f, 0.97f);
        if (lcdChanceUpLevel == 1) spinColor = new Color(0.08f, 0.42f, 0.46f, 0.97f);
        if (lcdChanceUpLevel == 2) spinColor = new Color(0.62f, 0.12f, 0.1f, 0.98f);
        if (lcdChanceUpLevel >= 3)
        {
            float pulse = 0.75f + Mathf.PingPong(Time.time * 3f, 0.25f);
            spinColor = new Color(0.68f * pulse, 0.38f * pulse, 0.06f, 0.99f);
        }
        GUI.color = jackpotFlash
            ? new Color(0.08f, 0.48f * flashPulse, 0.62f * flashPulse, 0.98f)
            : isSpinning ? spinColor : new Color(0.035f, 0.08f, 0.22f, 0.94f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.color = oldColor;
        string state = gameMode == PachinkoMode.Rush ? $"ヨーデルSONIC怪闘　連チャン {consecutiveWins}"
            : gameMode == PachinkoMode.KomaChallenge ? $"コマチャレンジ　残り {challengeSpinsRemaining}回" : "通常時　液晶抽選";
        string holds = $"保留　{pendingBalls}/{MaxPendingBalls}";
        GUIStyle lcdStateStyle = new GUIStyle(eyebrowStyle)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.11f), 12, 24),
            alignment = TextAnchor.MiddleLeft
        };
        GUI.Label(new Rect(rect.x + 12f, rect.y + 3f, rect.width - 24f, rect.height * 0.2f), state, lcdStateStyle);
        float gap = Mathf.Max(3f, rect.width * 0.018f);
        float characterWidth = isSpinning && lcdChanceUpLevel >= 3 ? Mathf.Min(rect.width * 0.17f, rect.height * 0.56f) : 0f;
        float digitWidth = (rect.width - 16f - characterWidth - gap * 2f) / 3f;
        float digitY = rect.y + rect.height * 0.22f;
        float digitHeight = rect.height * 0.56f;
        GUIStyle digitStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.58f), 12, 76),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = jackpotFlash || isSpinning && lcdChanceUpLevel >= 3 ? new Color(1f, 0.9f, 0.35f) : isSpinning && lcdChanceUpLevel == 2 ? new Color(1f, 0.68f, 0.55f) : isSpinning ? new Color(0.65f, 0.9f, 1f) : new Color(1f, 0.82f, 0.28f) }
        };
        for (int i = 0; i < lcdDigits.Length; i++)
        {
            Rect digitRect = new Rect(rect.x + 8f + i * (digitWidth + gap), digitY, digitWidth, digitHeight);
            GUI.color = new Color(0.015f, 0.035f, 0.1f, 0.92f);
            GUI.DrawTexture(digitRect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(digitRect, lcdDigits[i].ToString(), digitStyle);
        }
        if (characterWidth > 0f)
        {
            Texture2D character = jackpotCharacterSprites[Mathf.Clamp(lcdChanceCharacterIndex, 0, jackpotCharacterSprites.Length - 1)];
            if (character != null)
            {
                Rect characterRect = new Rect(rect.xMax - characterWidth - 6f, digitY, characterWidth, digitHeight);
                GUI.color = Color.white;
                GUI.DrawTexture(characterRect, character, ScaleMode.ScaleToFit, true);
            }
        }
        GUIStyle messageStyle = new GUIStyle(eyebrowStyle)
        {
            fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.09f), 10, 20),
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(new Rect(rect.x + 8f, digitY + digitHeight, rect.width - 16f, rect.height * 0.18f),
            lcdMessage + "　" + holds, messageStyle);
        GUI.color = oldColor;
    }

    private static int RollChanceUpLevel(bool jackpot)
    {
        float roll = Random.value;
        if (jackpot)
            return roll < 0.03f ? 0 : roll < 0.2f ? 1 : roll < 0.55f ? 2 : 3;
        return roll < 0.82f ? 0 : roll < 0.97f ? 1 : roll < 0.999f ? 2 : 3;
    }

    private static string GetChanceUpMessage(int level)
    {
        switch (level)
        {
            case 1: return "保留変化！チャンスアップ";
            case 2: return "赤演出！大チャンス！";
            case 3: return "キャラ参戦！激アツ！！";
            default: return "数字回転中…";
        }
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
            float fallbackWidth = boardWidth * 0.58f;
            float fallbackHeight = screenHeight * 0.18f;
            return new Rect((boardWidth - fallbackWidth) * 0.5f, screenHeight * 0.42f, fallbackWidth, fallbackHeight);
        }

        Vector3 lowerLeft = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(-2.4f, -1f, 0.22f)));
        Vector3 upperRight = mainCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(2.4f, 0.9f, 0.22f)));
        if (lowerLeft.z <= 0f || upperRight.z <= 0f)
        {
            float fallbackWidth = boardWidth * 0.58f;
            float fallbackHeight = screenHeight * 0.18f;
            return new Rect((boardWidth - fallbackWidth) * 0.5f, screenHeight * 0.42f, fallbackWidth, fallbackHeight);
        }

        return Rect.MinMaxRect(lowerLeft.x, screenHeight - upperRight.y, upperRight.x, screenHeight - lowerLeft.y);
    }

    private void DrawHud(Rect rect)
    {
        Color old = GUI.color;
        DrawMetalPanel(rect, 3f);
        float pad = Mathf.Clamp(rect.width * 0.012f, 10f, 24f);
        float unit = (rect.width - pad * 2f) / 5f;
        Rect[] cells =
        {
            new Rect(rect.x + pad, rect.y + 7f, unit, rect.height - 14f),
            new Rect(rect.x + pad + unit, rect.y + 7f, unit, rect.height - 14f),
            new Rect(rect.x + pad + unit * 2f, rect.y + 7f, unit, rect.height - 14f),
            new Rect(rect.x + pad + unit * 3f, rect.y + 7f, unit, rect.height - 14f),
            new Rect(rect.x + pad + unit * 4f, rect.y + 7f, unit, rect.height - 14f)
        };
        Color[] iconColors =
        {
            new Color(0.15f, 0.72f, 1f), new Color(1f, 0.67f, 0.12f),
            new Color(0.12f, 0.78f, 1f), new Color(0.75f, 0.24f, 1f),
            new Color(0.25f, 0.52f, 1f)
        };
        string[] icons = { "↻", "¥", "✦", "◆", "★" };
        for (int i = 0; i < cells.Length; i++)
        {
            GUI.color = new Color(0.006f, 0.019f, 0.052f, 0.95f);
            GUI.DrawTexture(cells[i], Texture2D.whiteTexture);
            GUI.color = new Color(iconColors[i].r, iconColors[i].g, iconColors[i].b, 0.18f);
            GUI.DrawTexture(new Rect(cells[i].x, cells[i].y, cells[i].width, cells[i].height), Texture2D.whiteTexture);
            GUI.color = iconColors[i];
            GUI.DrawTexture(new Rect(cells[i].x, cells[i].yMax - 2f, cells[i].width, 2f), Texture2D.whiteTexture);
            float iconSize = Mathf.Min(30f, cells[i].height * 0.62f);
            Rect iconRect = new Rect(cells[i].x + 8f, cells[i].center.y - iconSize * 0.5f, iconSize, iconSize);
            GUI.color = new Color(iconColors[i].r, iconColors[i].g, iconColors[i].b, 0.2f);
            GUI.DrawTexture(new Rect(iconRect.x - 3f, iconRect.y - 3f, iconSize + 6f, iconSize + 6f), GetCelebrationCircle());
            GUI.color = iconColors[i];
            GUI.Label(iconRect, icons[i], hudIconStyle);
        }
        GUI.color = Color.white;
        Rect[] valueRects = new Rect[cells.Length];
        for (int i = 0; i < cells.Length; i++)
            valueRects[i] = new Rect(cells[i].x + 44f, cells[i].y + 1f, cells[i].width - 48f, cells[i].height - 2f);
        GUI.Label(valueRects[0],
            $"転生・超越\nLV.{1 + ballValueLevel + launchCountLevel}　連チャン {consecutiveWins}", eyebrowStyle);
        GUI.Label(valueRects[1],
            $"所持金　¥{FormatCurrency(money)}\n玉　∞", statValueStyle);
        GUI.Label(valueRects[2],
            $"転生P  {reincarnationPoints}", sectionStyle);
        GUI.Label(valueRects[3],
            $"超越P  {transcendencePoints}", sectionStyle);
        GUI.Label(valueRects[4],
            $"超越強化  {transcendenceIncomeLevel + transcendenceVolleyLevel + transcendenceJackpotLevel}", sectionStyle);
        GUI.color = old;
    }

    private void DrawBoardControls(Rect rect)
    {
        DrawMetalPanel(rect, 3f);
        float inset = Mathf.Clamp(rect.height * 0.14f, 5f, 9f);
        if (GUI.Button(new Rect(rect.x + inset, rect.y + inset, rect.width * 0.46f, rect.height - inset * 2f), "▶ 発射！", launchButtonStyle))
        {
            LaunchBatch();
        }

        GUI.Label(new Rect(rect.x + rect.width * 0.48f, rect.y + inset, rect.width * 0.17f, rect.height - inset * 2f), "玉 ∞", sectionStyle);
        if (GUI.Button(new Rect(rect.x + rect.width * 0.68f, rect.y + inset, rect.width * 0.30f, rect.height - inset * 2f),
            autoLaunch ? "自動発射 ON　●" : "自動発射 OFF　○", subButtonStyle)) ToggleAutoLaunch();
    }

    private void DrawSidePanel(Rect rect)
    {
        DrawMetalPanel(rect, 4f);
        float pad = Mathf.Clamp(rect.width * 0.018f, 8f, 16f);
        float tabY = rect.y + 12f;
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

        if (GUI.Button(new Rect(rect.xMax - pad - rect.width * 0.35f, y, rect.width * 0.34f, 28f), autoPurchase ? "自動購入 ON　●" : "自動購入 OFF　○", subButtonStyle))
        {
            autoPurchase = !autoPurchase;
            if (autoPurchase) nextAutoPurchase = Time.time;
        }

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
        DrawUpgrade("玉の価値", ballValueLevel, 5, () => ballValueLevel++, "1玉あたりの獲得金額を増やす", $"×{GetBallValueMultiplier():0.0} → ×{2f + ballValueLevel:0.0}", costMultiplier: 1.2f);
        DrawUpgrade("オート発射速度", 2 - autoLaunchInterval, 12, () => autoLaunchInterval = Mathf.Max(1, autoLaunchInterval - 1), autoLaunch ? "自動発射中" : "発射間隔を短縮", autoLaunchInterval > 1 ? $"{autoLaunchInterval}秒 → {autoLaunchInterval - 1}秒" : "最大強化");
        DrawUpgrade("連射", launchCountLevel, 18, () => launchCountLevel++, "1回の発射で出る玉の数", $"{1 + launchCountLevel} → {2 + launchCountLevel}発", maxLevel: 5);
        DrawUpgrade("入賞口強化", categoryLevels[0], 16, () => categoryLevels[0]++, "入賞口の倍率", $"×{1f + categoryLevels[0] * 0.5f:0.0} → ×{1f + (categoryLevels[0] + 1) * 0.5f:0.0}");
        DrawUpgrade("チューリップ強化", categoryLevels[1], 16, () => categoryLevels[1]++, "チューリップの倍率", $"×{1f + categoryLevels[1] * 0.5f:0.0} → ×{1f + (categoryLevels[1] + 1) * 0.5f:0.0}");
        DrawUpgrade("ヘソ拡張", launchPowerLevel, 12, () => launchPowerLevel++, "発射パワーを上げて盤面へ届ける", $"Lv.{launchPowerLevel} → Lv.{launchPowerLevel + 1}");
        DrawUpgrade("大当たり確率", jackpotLevel, 24, () => jackpotLevel++, "液晶抽選の大当たり確率", $"{JackpotChance:P2} → {GetJackpotChance(jackpotLevel + 1):P2}", maxLevel: GetJackpotUpgradeLimit());
        DrawUpgrade("アタッカー倍率", categoryLevels[2], 20, () => categoryLevels[2]++, "アタッカー入賞時の出玉倍率", $"×{1f + categoryLevels[2] * 0.5f:0.0} → ×{1f + (categoryLevels[2] + 1) * 0.5f:0.0}");
        DrawUpgrade("確変突入率", probabilityChangeLevel, 24, () => probabilityChangeLevel++, "大当たり後にRUSHへ入りやすくする", $"{KakuhenChance:P0} → {GetKakuhenChance(probabilityChangeLevel + 1):P0}", maxLevel: GetKakuhenUpgradeLimit());
    }

    private void DrawRebirthTab()
    {
        GUILayout.Label("転生", titleStyle);
        GUILayout.Label("転生すると所持金と通常強化がリセットされます。", bodyStyle);
        BigInteger reincarnationRequirement = GetNextReincarnationRequirement();
        DrawProgressBar(runPayout, reincarnationRequirement, $"転生条件　¥{FormatCurrency(runPayout)} / ¥{FormatCurrency(reincarnationRequirement)}");
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
        GUILayout.Label($"超越効果：獲得金額 ×{Mathf.Pow(2f, transcendenceIncomeLevel):0} ／ 1回 {1 + launchCountLevel + transcendenceVolleyLevel * 2 + divineVolleyLevel * 10}発 ／ 大当たり {JackpotChance:P2}", bodyStyle);

        GUILayout.Space(12f);
        GUILayout.Label("神域スキル　超越を超える力", sectionStyle);
        GUILayout.Label("超越ポイントを複数消費して、さらに強力な恒久スキルを獲得します。", bodyStyle);

        GUI.enabled = divineIncomeLevel < 5 && transcendencePoints >= 5;
        if (GUILayout.Button($"神域の富　獲得金額 ×10　Lv.{divineIncomeLevel}/5　（5超越P）", buttonStyle, GUILayout.Height(40f)))
        {
            divineIncomeLevel++;
            transcendencePoints -= 5;
        }

        GUI.enabled = divineVolleyLevel < 5 && transcendencePoints >= 4;
        if (GUILayout.Button($"神域の連射　一度に発射 +10玉　Lv.{divineVolleyLevel}/5　（4超越P）", buttonStyle, GUILayout.Height(40f)))
        {
            divineVolleyLevel++;
            transcendencePoints -= 4;
        }

        int divineJackpotLimit = GetDivineJackpotUpgradeLimit();
        GUI.enabled = divineJackpotLevel < divineJackpotLimit && transcendencePoints >= 4;
        if (GUILayout.Button($"神域の幸運　大当たり確率 +10%　Lv.{divineJackpotLevel}/{divineJackpotLimit}　（4超越P）", buttonStyle, GUILayout.Height(40f)))
        {
            divineJackpotLevel++;
            transcendencePoints -= 4;
        }
        GUI.enabled = true;
        GUILayout.Label($"神域効果：獲得金額 ×{Mathf.Pow(10f, divineIncomeLevel):0} ／ 追加発射 {divineVolleyLevel * 10}玉 ／ 神域大当たり強化 {divineJackpotLevel * 10}%", bodyStyle);
    }

    private void DrawRecordTab()
    {
        GUILayout.Label("プレイ記録", titleStyle);
        GUILayout.Label($"累計獲得金額　¥{FormatCurrency(lifetimePayout)}", sectionStyle);
        GUILayout.Label($"今回の獲得金額　¥{FormatCurrency(runPayout)}", sectionStyle);
        GUILayout.Label($"盤面上の玉　{activeBalls.Count}", sectionStyle);
        GUILayout.Label($"通常時　1/{1f / JackpotChance:0.0}\nRUSH中　1/{1f / RushJackpotChance:0.0}\nコマチャレンジ　1/{1f / KomaChallengeChance:0.0}\nRUSH突入率　{KakuhenChance:P0}", bodyStyle);
    }

    private void DrawUpgrade(string label, int level, int baseCost, System.Action purchase, string description, string change, int maxLevel = -1, float costMultiplier = 1.32f)
    {
        int availableLevels = maxLevel >= 0 ? Mathf.Max(0, maxLevel - level) : int.MaxValue;
        int count = Mathf.Min(purchaseAmount, availableLevels);
        bool capped = (label == "オート発射速度" && autoLaunchInterval <= 1)
            || (maxLevel >= 0 && level >= maxLevel);
        BigInteger CostAtLevel(int targetLevel)
        {
            if (costMultiplier == 1.2f) return GeometricCost(baseCost, targetLevel, 6, 5);
            return UpgradeCost(baseCost, targetLevel);
        }
        BigInteger cost = BigInteger.Zero;
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
        Rect glyphRect = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(52f), GUILayout.Height(52f));
        DrawUpgradeGlyph(glyphRect, label);
        GUILayout.Space(4f);
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
        if (!stackPurchaseButton && GUILayout.Button($"強化 ×{count}\n¥{FormatCurrency(cost)}", purchaseStyle, GUILayout.Width(Mathf.Clamp(availableCardWidth * 0.25f, 108f, 132f)), GUILayout.Height(58f)))
        {
            BuyUpgrade(cost, () => { for (int i = 0; i < count; i++) purchase(); }, label);
        }
        GUILayout.EndHorizontal();
        if (stackPurchaseButton && GUILayout.Button($"強化 ×{count}　¥{FormatCurrency(cost)}", purchaseStyle, GUILayout.ExpandWidth(true), GUILayout.Height(38f)))
        {
            BuyUpgrade(cost, () => { for (int i = 0; i < count; i++) purchase(); }, label);
        }
        GUI.enabled = true;
        GUILayout.EndVertical();
    }

    private void DrawUpgradeGlyph(Rect rect, string label)
    {
        Color old = GUI.color;
        Color accent = label == "玉の価値" || label == "大当たり確率" ? new Color(1f, 0.68f, 0.12f)
            : label.Contains("発射") || label == "連射" ? new Color(0.5f, 0.86f, 1f)
            : label.Contains("入賞") || label.Contains("ヘソ") ? new Color(0.2f, 0.82f, 1f)
            : new Color(0.78f, 0.32f, 1f);
        GUI.color = new Color(accent.r, accent.g, accent.b, 0.2f);
        GUI.DrawTexture(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), GetCelebrationCircle());
        GUI.color = new Color(0.48f, 0.28f, 0.1f, 1f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.98f, 0.67f, 0.19f, 1f);
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), Texture2D.whiteTexture);
        GUI.color = new Color(0.015f, 0.065f, 0.15f, 1f);
        GUI.DrawTexture(new Rect(rect.x + 5f, rect.y + 5f, rect.width - 10f, rect.height - 10f), Texture2D.whiteTexture);
        GUI.color = accent;
        string mark = label == "玉の価値" ? "●" : label.Contains("発射") ? "▶" : label == "連射" ? "≫"
            : label.Contains("入賞") ? "◎" : label.Contains("チューリップ") ? "✿" : label.Contains("ヘソ") ? "◇"
            : label.Contains("確変") ? "♧" : label.Contains("大当たり") ? "★" : "✦";
        GUI.Label(rect, mark, upgradeGlyphStyle);
        GUI.color = old;
    }

    private void BuyUpgrade(BigInteger cost, System.Action purchase, string label)
    {
        if (money < cost)
        {
            lastResult = "お金が足りません。";
            return;
        }

        money -= cost;
        purchase();
        lastResult = $"{label}を強化しました（-¥{FormatCurrency(cost)}）。";
    }

    private void ToggleAutoLaunch()
    {
        autoLaunch = !autoLaunch;
    }

    private void TryAutoPurchase()
    {
        BigInteger bestCost = BigInteger.Zero;
        System.Action bestPurchase = null;
        string bestLabel = "";
        ConsiderAutoUpgrade("玉の価値", BallValueUpgradeCost(ballValueLevel), () => ballValueLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        if (launchCountLevel < 5)
            ConsiderAutoUpgrade("連射", UpgradeCost(18, launchCountLevel), () => launchCountLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        ConsiderAutoUpgrade("発射パワー", UpgradeCost(12, launchPowerLevel), () => launchPowerLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        if (jackpotLevel < 22 && GetJackpotChance(jackpotLevel + 1) > JackpotChance)
            ConsiderAutoUpgrade("大当たり率", UpgradeCost(24, jackpotLevel), () => jackpotLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        if (probabilityChangeLevel < GetKakuhenUpgradeLimit() && GetKakuhenChance(probabilityChangeLevel + 1) > KakuhenChance)
            ConsiderAutoUpgrade("確変率", UpgradeCost(24, probabilityChangeLevel), () => probabilityChangeLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
        for (int i = 0; i < categoryLevels.Length; i++)
        {
            int index = i;
            int baseCost = i == 2 ? 20 : 16;
            ConsiderAutoUpgrade("入賞倍率", UpgradeCost(baseCost, categoryLevels[index]), () => categoryLevels[index]++, ref bestCost, ref bestPurchase, ref bestLabel);
        }

        if (bestPurchase != null) BuyUpgrade(bestCost, bestPurchase, bestLabel);
    }

    private void ConsiderAutoUpgrade(string label, BigInteger cost, System.Action purchase, ref BigInteger bestCost, ref System.Action bestPurchase, ref string bestLabel)
    {
        if (cost <= money && (bestPurchase == null || cost < bestCost))
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
        pendingBalls = 0;
        hesoSpinQueue.Clear();
        gameMode = PachinkoMode.Normal;
        challengeSpinsRemaining = 0;
        consecutiveWins = 0;
        isSpinning = false;
        lcdChanceUpLevel = 0;
        lcdMessage = "保留を待っています";
        lcdStoppedDigits = 0;
        isLaunchingBatch = false;
        nextManualLaunch = 0f;
        nextAutoLaunch = Time.time;

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

    private int CalculateReincarnationPoints(BigInteger payout)
    {
        BigInteger remainingPayout = payout;
        BigInteger nextRequirement = GetNextReincarnationRequirement();
        int pointsEarned = 0;
        while (remainingPayout >= nextRequirement)
        {
            remainingPayout -= nextRequirement;
            pointsEarned++;
            nextRequirement *= ReincarnationRequirementMultiplier;
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

    private BigInteger GetNextReincarnationRequirement()
    {
        BigInteger requirement = ReincarnationBaseRequirement;
        for (int i = 0; i < reincarnationPoints; i++)
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
            normal = { background = MakeTexture(new Color(0.018f, 0.055f, 0.14f, 0.99f), new Color(0.2f, 0.78f, 1f, 0.96f)) }
        };
        int scale = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 1080f), 1, 2);
        eyebrowStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 * scale, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.43f, 0.88f, 1f) } };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32 * scale, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 23 * scale, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.67f, 0.91f, 1f) } };
        hudIconStyle = new GUIStyle(sectionStyle) { fontSize = 22 * scale, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        upgradeGlyphStyle = new GUIStyle(sectionStyle) { fontSize = 27 * scale, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 * scale, wordWrap = true, normal = { textColor = new Color(0.91f, 0.95f, 1f) } };
        statLabelStyle = new GUIStyle(bodyStyle) { fontSize = 18 * scale, alignment = TextAnchor.MiddleLeft };
        statValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 * scale, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.76f, 0.18f) } };
        resultStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 * scale, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        hesoLabelStyle = new GUIStyle(sectionStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.78f, 0.24f) } };
        buttonStyle = MakeButtonStyle(new Color(0.035f, 0.19f, 0.43f), new Color(0.07f, 0.42f, 0.8f));
        launchButtonStyle = MakeButtonStyle(new Color(0.025f, 0.34f, 0.82f), new Color(0.03f, 0.58f, 1f));
        launchButtonStyle.fontSize = 30 * scale;
        buttonStyle.fontSize = 20 * scale;
        subButtonStyle = MakeButtonStyle(new Color(0.018f, 0.07f, 0.17f), new Color(0.04f, 0.23f, 0.48f));
        subButtonStyle.fontSize = 18 * scale;
        cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 10, 8, 8),
            margin = new RectOffset(0, 0, 4, 4),
            normal = { background = MakeTexture(new Color(0.025f, 0.07f, 0.16f, 0.99f), new Color(0.91f, 0.58f, 0.16f, 0.96f)) }
        };
    }

    private static BigInteger UpgradeCost(int baseCost, int level)
    {
        return GeometricCost(baseCost, level, 132, 100);
    }

    private float GetBallValueMultiplier() => 1f + ballValueLevel;

    private static BigInteger BallValueUpgradeCost(int level)
    {
        return GeometricCost(5, level, 6, 5);
    }

    private static BigInteger GeometricCost(int baseCost, int level, int numerator, int denominator)
    {
        int safeLevel = System.Math.Max(0, level);
        BigInteger numeratorValue = (BigInteger)baseCost * BigInteger.Pow(numerator, safeLevel);
        BigInteger denominatorValue = BigInteger.Pow(denominator, safeLevel);
        BigInteger rounded = (numeratorValue + denominatorValue / 2) / denominatorValue;
        return BigInteger.Max(BigInteger.One, rounded);
    }

    private BigInteger AutoIntervalCost => UpgradeCost(12, 2 - autoLaunchInterval);

    private void DrawProgressBar(BigInteger value, BigInteger target, string label)
    {
        GUILayout.Label(label, bodyStyle);
        Rect rect = GUILayoutUtility.GetRect(1f, 24f, GUILayout.ExpandWidth(true));
        GUI.color = new Color(0.09f, 0.14f, 0.2f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.68f, 0.2f);
        int progressBasisPoints = target <= BigInteger.Zero ? 10000 : value >= target ? 10000 : (int)(value * 10000 / target);
        rect.width *= progressBasisPoints / 10000f;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static GUIStyle MakeButtonStyle(Color normalColor, Color hoverColor)
    {
        Color edge = normalColor.b > normalColor.r * 1.2f
            ? new Color(0.42f, 0.78f, 1f, 1f)
            : new Color(1f, 0.65f, 0.18f, 1f);
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            border = new RectOffset(5, 5, 5, 5),
            normal = { background = MakeTexture(normalColor, edge), textColor = Color.white },
            hover = { background = MakeTexture(hoverColor, new Color(1f, 0.9f, 0.5f, 1f)), textColor = Color.white },
            active = { background = MakeTexture(hoverColor * 0.85f, edge), textColor = Color.white },
            focused = { background = MakeTexture(normalColor, edge), textColor = Color.white }
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
