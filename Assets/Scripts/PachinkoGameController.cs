using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PachinkoGameController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform launchPoint;
    [SerializeField] private Transform ballContainer;

    [Header("Starting values")]
    [SerializeField] private int startingBalls = 30;
    [SerializeField] private float launchPower = 14f;
    [SerializeField] private int autoLaunchInterval = 2;

    private readonly HashSet<PachinkoBall> activeBalls = new HashSet<PachinkoBall>();
    private readonly int[] lanePayouts = { 1, 2, 3, 8, 3, 2, 1 };
    private readonly string[] laneNames = { "入賞口", "チューリップ", "アタッカー", "ヘソ抽選", "アタッカー", "チューリップ", "入賞口" };
    private readonly int[] categoryLevels = new int[3];

    private int balls;
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
    private int kakuhenBallsRemaining;
    private int consecutiveWins;
    private bool isSpinning;
    private float spinEndsAt;
    private string lcdMessage = "保留を待っています";
    private int reincarnationPoints;
    private int transcendencePoints;
    private int reincarnationUpgradeLevel;
    private int transcendenceUpgradeLevel;
    private int currentTab;
    private int purchaseAmount = 1;
    private string lastResult = "スペースキーまたは「玉を打つ」で発射";

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

    private void Start()
    {
        EnsureOuterWallColliders();
        EnsureHesoVisual();
        balls = startingBalls;
        if (ballPrefab == null)
        {
            Debug.LogError("PachinkoGameController: Ball Prefab が設定されていません。", this);
        }

        if (launchPoint == null)
        {
            Debug.LogError("PachinkoGameController: Launch Point が設定されていません。", this);
        }
    }

    private void EnsureHesoVisual()
    {
        if (transform.Find("Heso Visual") != null) return;

        GameObject root = new GameObject("Heso Visual");
        root.transform.SetParent(transform, false);
        Material gold = CreateVisualMaterial("Heso Gold", new Color(1f, 0.66f, 0.12f));
        Material cyan = CreateVisualMaterial("Heso Cyan", new Color(0.12f, 0.8f, 1f));
        CreateHesoPart(root.transform, "Heso left guide", new Vector3(-0.58f, 0.4f, 0.18f), new Vector3(0.16f, 0.32f, 0.12f), gold);
        CreateHesoPart(root.transform, "Heso right guide", new Vector3(0.58f, 0.4f, 0.18f), new Vector3(0.16f, 0.32f, 0.12f), gold);
        CreateHesoPart(root.transform, "Heso lower lip", new Vector3(0f, 0.23f, 0.18f), new Vector3(1.08f, 0.08f, 0.12f), cyan);
        CreateHesoPart(root.transform, "Heso indicator", new Vector3(0f, 0.53f, 0.18f), new Vector3(0.55f, 0.07f, 0.12f), gold);
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
        CreateOuterWall("Boundary Left", new Vector3(-3.86f, 0f, 0.1f), new Vector3(0.35f, 10f, 1.2f));
        CreateOuterWall("Boundary Right", new Vector3(3.86f, 0f, 0.1f), new Vector3(0.35f, 10f, 1.2f));
        CreateOuterWall("Boundary Top", new Vector3(0f, 4.84f, 0.1f), new Vector3(8f, 0.42f, 1.2f));
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

        if (isSpinning && Time.time >= spinEndsAt) isSpinning = false;

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
    }

    public void LaunchBatch()
    {
        if (ballPrefab == null || launchPoint == null || Time.time < nextManualLaunch)
        {
            return;
        }

        nextManualLaunch = Time.time + 0.12f;
        int count = Mathf.Min(1 + launchCountLevel, balls);
        if (count <= 0)
        {
            lastResult = "玉がありません。入賞で玉を増やしてください。";
            return;
        }

        for (int i = 0; i < count; i++)
        {
            SpawnBall();
        }

        balls -= count;
        lastResult = count > 1 ? $"{count}発発射！" : "発射！";
    }

    private void SpawnBall()
    {
        Vector3 jitter = new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(-0.04f, 0.04f), 0f);
        GameObject instance = Instantiate(ballPrefab, launchPoint.position + jitter, Quaternion.identity, ballContainer);
        instance.name = "PachinkoBall";

        Rigidbody body = instance.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = instance.AddComponent<Rigidbody>();
        }

        body.useGravity = true;
        body.linearVelocity = new Vector3(0.48f, 0.88f, 0f).normalized * (launchPower + launchPowerLevel * 1.2f);
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        PachinkoBall ball = instance.GetComponent<PachinkoBall>();
        if (ball == null)
        {
            ball = instance.AddComponent<PachinkoBall>();
        }

        activeBalls.Add(ball);
    }

    public void AwardPayout(PachinkoBall ball, int laneIndex, string gateName)
    {
        if (ball == null || !activeBalls.Remove(ball))
        {
            return;
        }

        int category = laneIndex == 1 || laneIndex == 5 ? 1 : laneIndex == 2 || laneIndex == 4 ? 2 : 0;
        float multiplier = (1f + ballValueLevel * 0.5f)
            * (1f + categoryLevels[category] * 0.5f)
            * (1f + reincarnationUpgradeLevel * 0.15f)
            * (1f + transcendenceUpgradeLevel * 0.5f);

        if (laneIndex == 3)
        {
            if (pendingBalls < 4)
            {
                pendingBalls++;
                lcdMessage = "ヘソ入賞！ 保留 +1";
                StartCoroutine(ResolveHesoSpin(multiplier));
            }
            else
            {
                balls++;
                lifetimePayout++;
                runPayout++;
                lastResult = "保留が満タン　玉 +1";
            }

            Destroy(ball.gameObject, 0.05f);
            return;
        }

        int payout = Mathf.Max(1, Mathf.RoundToInt(lanePayouts[Mathf.Clamp(laneIndex, 0, lanePayouts.Length - 1)] * multiplier));
        lastResult = $"{gateName} +{payout:N0} 玉";

        balls += payout;
        lifetimePayout += payout;
        runPayout += payout;
        Destroy(ball.gameObject, 0.05f);
    }

    private IEnumerator ResolveHesoSpin(float multiplier)
    {
        isSpinning = true;
        spinEndsAt = Time.time + 0.9f;
        lcdMessage = "回転中…";
        yield return new WaitForSeconds(0.9f);
        pendingBalls = Mathf.Max(0, pendingBalls - 1);

        bool alreadyInKakuhen = kakuhenBallsRemaining > 0;
        bool jackpot = alreadyInKakuhen || Random.value < JackpotChance;
        int payout;
        if (jackpot)
        {
            consecutiveWins++;
            payout = Mathf.RoundToInt(80f * multiplier * (1f + Mathf.Min(consecutiveWins - 1, 10) * 0.25f));
            if (alreadyInKakuhen) kakuhenBallsRemaining--;
            bool kakuhen = alreadyInKakuhen || Random.value < KakuhenChance;
            if (kakuhen && !alreadyInKakuhen) kakuhenBallsRemaining = 8;
            lcdMessage = kakuhen ? "777　大当たり！　確変突入" : "777　大当たり！";
            lastResult = kakuhen ? $"大当たり！確変突入　+{payout:N0} 玉" : $"大当たり！　+{payout:N0} 玉";
        }
        else
        {
            payout = Mathf.Max(0, Mathf.RoundToInt(2f * multiplier));
            consecutiveWins = 0;
            lcdMessage = "リーチ…　残念！";
            lastResult = "ヘソ抽選　ハズレ";
        }

        balls += payout;
        lifetimePayout += payout;
        runPayout += payout;
        isSpinning = pendingBalls > 0;
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

    private float JackpotChance => Mathf.Clamp(0.05f + jackpotLevel * 0.02f, 0.05f, 0.5f);
    private float KakuhenChance => Mathf.Clamp(0.1f + probabilityChangeLevel * 0.05f, 0.1f, 0.8f);

    private void OnGUI()
    {
        EnsureStyles();
        float w = Screen.width;
        float h = Screen.height;
        float top = Mathf.Clamp(h * 0.085f, 58f, 82f);
        float boardWidth = w * 0.55f;
        DrawHud(new Rect(0f, 0f, w, top));
        float lcdWidth = boardWidth * 0.7f;
        DrawLCD(new Rect((boardWidth - lcdWidth) * 0.5f, h * 0.365f, lcdWidth, h * 0.12f));
        GUI.Label(new Rect(boardWidth * 0.32f, h * 0.48f, boardWidth * 0.36f, 30f), "▼　ヘソ　保留 +1　▼", hesoLabelStyle);
        DrawBoardControls(new Rect(12f, h - 72f, boardWidth - 24f, 58f));
        DrawSidePanel(new Rect(boardWidth + 8f, top + 8f, w - boardWidth - 20f, h - top - 18f));
    }

    private void DrawLCD(Rect rect)
    {
        Color oldColor = GUI.color;
        GUI.color = isSpinning ? new Color(0.12f, 0.28f, 0.68f, 0.97f) : new Color(0.035f, 0.08f, 0.22f, 0.94f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.color = oldColor;
        string state = kakuhenBallsRemaining > 0 ? $"確変中　連チャン {consecutiveWins}" : "液晶抽選";
        string holds = $"保留　{(pendingBalls > 0 ? "●" : "○")} {(pendingBalls > 1 ? "●" : "○")} {(pendingBalls > 2 ? "●" : "○")} {(pendingBalls > 3 ? "●" : "○")}";
        GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, rect.height * 0.35f), state, eyebrowStyle);
        GUI.Label(new Rect(rect.x + 10f, rect.y + rect.height * 0.31f, rect.width - 20f, rect.height * 0.62f),
            isSpinning ? "●　●　●　リーチ！　" + holds : lcdMessage + "　" + holds, resultStyle);
    }

    private void DrawHud(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        float pad = 14f;
        float unit = (rect.width - pad * 2f) / 5f;
        GUI.Label(new Rect(rect.x + pad, rect.y + 7f, unit * 1.05f, rect.height - 12f),
            $"転生・超越\nLV.{1 + ballValueLevel + launchCountLevel}　連チャン {consecutiveWins}", eyebrowStyle);
        GUI.Label(new Rect(rect.x + pad + unit * 1.05f, rect.y + 5f, unit * 1.05f, rect.height - 10f),
            $"所持玉　● {balls:N0}\n+{(autoLaunch ? 1f / Mathf.Max(0.2f, autoLaunchInterval * 0.25f) : 0f):0}/秒", statValueStyle);
        GUI.Label(new Rect(rect.x + pad + unit * 2.2f, rect.y + 10f, unit * 0.9f, rect.height - 16f),
            $"転生P  {reincarnationPoints}", sectionStyle);
        GUI.Label(new Rect(rect.x + pad + unit * 3.15f, rect.y + 10f, unit * 0.8f, rect.height - 16f),
            $"超越P  {transcendencePoints}", sectionStyle);
        GUI.Label(new Rect(rect.x + rect.width - unit * 0.85f, rect.y + 10f, unit * 0.8f, rect.height - 16f),
            $"創世P  {transcendenceUpgradeLevel}", sectionStyle);
    }

    private void DrawBoardControls(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        if (GUI.Button(new Rect(rect.x + 6f, rect.y + 5f, rect.width * 0.43f, rect.height - 10f), "● 発射！", launchButtonStyle))
        {
            LaunchBatch();
        }

        GUI.Label(new Rect(rect.x + rect.width * 0.46f, rect.y + 8f, rect.width * 0.26f, rect.height - 14f),
            $"玉の残り　{balls}", bodyStyle);
        GUI.enabled = autoLaunch || balls >= 15;
        if (GUI.Button(new Rect(rect.x + rect.width * 0.73f, rect.y + 7f, rect.width * 0.25f, rect.height - 14f),
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
        DrawUpgrade("玉の価値", ballValueLevel, 10, () => ballValueLevel++, "1玉あたりの出玉を増やす", $"×{1f + ballValueLevel * 0.5f:0.0} → ×{1f + (ballValueLevel + 1) * 0.5f:0.0}");
        DrawUpgrade("オート発射速度", 2 - autoLaunchInterval, 12, () => autoLaunchInterval = Mathf.Max(1, autoLaunchInterval - 1), autoLaunch ? "自動発射中" : "発射間隔を短縮", autoLaunchInterval > 1 ? $"{autoLaunchInterval}秒 → {autoLaunchInterval - 1}秒" : "最大強化");
        DrawUpgrade("連射", launchCountLevel, 18, () => launchCountLevel++, "1回の発射で出る玉の数", $"{1 + launchCountLevel} → {2 + launchCountLevel}発");
        DrawUpgrade("入賞口強化", categoryLevels[0], 16, () => categoryLevels[0]++, "入賞口の倍率", $"×{1f + categoryLevels[0] * 0.5f:0.0} → ×{1f + (categoryLevels[0] + 1) * 0.5f:0.0}");
        DrawUpgrade("チューリップ強化", categoryLevels[1], 16, () => categoryLevels[1]++, "チューリップの倍率", $"×{1f + categoryLevels[1] * 0.5f:0.0} → ×{1f + (categoryLevels[1] + 1) * 0.5f:0.0}");
        DrawUpgrade("ヘソ拡張", launchPowerLevel, 12, () => launchPowerLevel++, "発射パワーを上げて盤面へ届ける", $"Lv.{launchPowerLevel} → Lv.{launchPowerLevel + 1}");
        DrawUpgrade("大当たり確率", jackpotLevel, 24, () => jackpotLevel++, "液晶抽選の大当たり確率", $"{JackpotChance:P0} → {Mathf.Min(0.5f, JackpotChance + 0.02f):P0}");
        DrawUpgrade("アタッカー倍率", categoryLevels[2], 20, () => categoryLevels[2]++, "アタッカー入賞時の出玉倍率", $"×{1f + categoryLevels[2] * 0.5f:0.0} → ×{1f + (categoryLevels[2] + 1) * 0.5f:0.0}");
        DrawUpgrade("確変突入率", probabilityChangeLevel, 24, () => probabilityChangeLevel++, "大当たり後に確変へ入りやすくする", $"{KakuhenChance:P0} → {Mathf.Min(0.8f, KakuhenChance + 0.05f):P0}");
    }

    private void DrawRebirthTab()
    {
        GUILayout.Label("転生", titleStyle);
        GUILayout.Label("累計出玉を使って転生し、通常強化をやり直します。", bodyStyle);
        DrawProgressBar(runPayout, 250, $"転生条件　{runPayout:N0} / 250");
        GUILayout.Label($"転生ポイント　{reincarnationPoints}", sectionStyle);
        GUI.enabled = runPayout >= 250;
        if (GUILayout.Button("転生する", buttonStyle, GUILayout.Height(46f))) Reincarnate();
        GUI.enabled = reincarnationPoints > reincarnationUpgradeLevel;
        if (GUILayout.Button("転生強化：全倍率アップ", buttonStyle, GUILayout.Height(38f)))
        {
            reincarnationUpgradeLevel++;
            reincarnationPoints--;
        }
        GUI.enabled = true;
    }

    private void DrawTranscendTab()
    {
        GUILayout.Label("超越", titleStyle);
        GUILayout.Label("転生ポイントを重ねて、さらに強い強化倍率を解放します。", bodyStyle);
        GUILayout.Label($"転生ポイント　{reincarnationPoints} / 5", sectionStyle);
        GUI.enabled = reincarnationPoints >= 5;
        if (GUILayout.Button("超越する（5ポイント消費）", buttonStyle, GUILayout.Height(44f)))
        {
            reincarnationPoints -= 5;
            transcendencePoints++;
        }
        GUI.enabled = transcendencePoints > transcendenceUpgradeLevel;
        if (GUILayout.Button("超越強化：全倍率アップ", buttonStyle, GUILayout.Height(40f)))
        {
            transcendenceUpgradeLevel++;
            transcendencePoints--;
        }
        GUI.enabled = true;
        GUILayout.Label($"超越ポイント　{transcendencePoints}", bodyStyle);
    }

    private void DrawRecordTab()
    {
        GUILayout.Label("プレイ記録", titleStyle);
        GUILayout.Label($"累計出玉　{lifetimePayout:N0}", sectionStyle);
        GUILayout.Label($"今回の出玉　{runPayout:N0}", sectionStyle);
        GUILayout.Label($"盤面上の玉　{activeBalls.Count}", sectionStyle);
        GUILayout.Label($"大当たり確率　{JackpotChance:P0}\n確変突入率　{KakuhenChance:P0}", bodyStyle);
    }

    private void DrawUpgrade(string label, int level, int baseCost, System.Action purchase, string description, string change)
    {
        int count = purchaseAmount;
        bool capped = (label == "オート発射速度" && autoLaunchInterval <= 1)
            || (label == "大当たり確率" && jackpotLevel >= 22)
            || (label == "確変突入率" && probabilityChangeLevel >= 14);
        int cost = 0;
        for (int i = 0; i < count; i++) cost += UpgradeCost(baseCost, level + i);
        if (purchaseAmount == 100)
        {
            count = 0;
            cost = 0;
            while (count < 100 && cost + UpgradeCost(baseCost, level + count) <= balls)
                cost += UpgradeCost(baseCost, level + count++);
        }
        if (capped)
        {
            count = 0;
            cost = 0;
        }
        GUILayout.BeginHorizontal(cardStyle, GUILayout.MinHeight(74f));
        GUILayout.BeginVertical();
        GUILayout.Label($"{label}　Lv.{level}", sectionStyle);
        GUILayout.Label(description, bodyStyle);
        GUILayout.Label(change, eyebrowStyle);
        GUILayout.EndVertical();
        GUI.enabled = count > 0 && balls >= cost;
        if (GUILayout.Button($"強化 ×{count}\n● {cost:N0}", subButtonStyle, GUILayout.Width(110f), GUILayout.Height(58f)))
        {
            BuyUpgrade(cost, () => { for (int i = 0; i < count; i++) purchase(); }, label);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void BuyUpgrade(int cost, System.Action purchase, string label)
    {
        if (balls < cost)
        {
            lastResult = "玉が足りません。";
            return;
        }

        balls -= cost;
        purchase();
        lastResult = $"{label}を強化しました。";
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
        int bestCost = int.MaxValue;
        System.Action bestPurchase = null;
        string bestLabel = "";
        ConsiderAutoUpgrade("玉の価値", UpgradeCost(10, ballValueLevel), () => ballValueLevel++, ref bestCost, ref bestPurchase, ref bestLabel);
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

    private void ConsiderAutoUpgrade(string label, int cost, System.Action purchase, ref int bestCost, ref System.Action bestPurchase, ref string bestLabel)
    {
        if (cost <= balls && cost < bestCost)
        {
            bestCost = cost;
            bestPurchase = purchase;
            bestLabel = label;
        }
    }

    private void Reincarnate()
    {
        if (runPayout < 250)
        {
            return;
        }

        runPayout = 0;
        balls = startingBalls;
        ballValueLevel = 0;
        launchCountLevel = 0;
        launchPowerLevel = 0;
        jackpotLevel = 0;
        probabilityChangeLevel = 0;
        for (int i = 0; i < categoryLevels.Length; i++)
        {
            categoryLevels[i] = 0;
        }

        reincarnationPoints++;
        lastResult = "転生しました。転生ポイントを獲得！";
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
        statValueStyle = new GUIStyle(GUI.skin.label) { fontSize = 28 * scale, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.82f, 0.33f) } };
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

    private static int UpgradeCost(int baseCost, int level)
    {
        return Mathf.RoundToInt(baseCost * Mathf.Pow(1.32f, level));
    }

    private int AutoIntervalCost => UpgradeCost(12, 2 - autoLaunchInterval);

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
