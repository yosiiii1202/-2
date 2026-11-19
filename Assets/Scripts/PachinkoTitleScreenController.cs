using System.Globalization;
using System.Numerics;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PachinkoTitleScreenController : MonoBehaviour
{
    [System.Serializable]
    private sealed class SavePreview
    {
        public int version;
        public string money;
        public int reincarnationPoints;
        public int transcendencePoints;
        public int consecutiveWins;
    }

    private readonly SavePreview[] saveSlots = new SavePreview[3];
    private int selectedSlot;
    private int pendingDeleteSlot = -1;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle slotNameStyle;
    private GUIStyle slotInfoStyle;
    private GUIStyle buttonStyle;
    private GUIStyle secondaryButtonStyle;
    private GUIStyle modalStyle;

    private void Start()
    {
        RefreshSaveSlots();
    }

    private void OnGUI()
    {
        EnsureStyles();
        GUI.color = Color.white;
        DrawBackground();
        GUI.color = Color.white;

        float width = Mathf.Min(Screen.width * 0.78f, 1120f);
        float height = Mathf.Min(Screen.height * 0.86f, 900f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        DrawFrame(panel, 8f);

        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + 20f, panel.y + height * 0.045f, panel.width - 40f, height * 0.105f), "PACHINKO EVOLUTION", titleStyle);
        GUI.Label(new Rect(panel.x + 20f, panel.y + height * 0.15f, panel.width - 40f, height * 0.055f), "転生・超越パチンコ", subtitleStyle);

        float pad = Mathf.Clamp(width * 0.035f, 22f, 42f);
        float rowTop = panel.y + height * 0.235f;
        float gap = Mathf.Clamp(height * 0.018f, 10f, 18f);
        float rowHeight = (panel.yMax - height * 0.16f - rowTop - gap * 2f) / 3f;
        for (int i = 0; i < saveSlots.Length; i++)
            DrawSaveSlot(panel, pad, rowTop, gap, rowHeight, i);

        float bottomY = panel.yMax - Mathf.Clamp(height * 0.105f, 64f, 92f);
        float bottomButtonWidth = Mathf.Min(260f, width * 0.31f);
        Rect startRect = new Rect(panel.center.x - bottomButtonWidth - 10f, bottomY, bottomButtonWidth, 58f);
        Rect endRect = new Rect(panel.center.x + 10f, bottomY, bottomButtonWidth, 58f);
        if (GUI.Button(startRect, "START", buttonStyle)) StartSelectedSlot();
        if (GUI.Button(endRect, "END", secondaryButtonStyle)) QuitGame();

        if (pendingDeleteSlot >= 0) DrawDeleteConfirmation(panel);
    }

    private void DrawBackground()
    {
        Color old = GUI.color;
        GUI.color = new Color(0.004f, 0.012f, 0.04f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.018f, 0.13f, 0.33f, 1f);
        GUI.DrawTexture(new Rect(0f, Screen.height * 0.18f, Screen.width, Screen.height * 0.64f), Texture2D.whiteTexture);
        GUI.color = new Color(0.08f, 0.62f, 1f, 0.17f);
        for (int i = 0; i < 11; i++)
        {
            float x = Screen.width * 0.5f + (i - 5) * Screen.width * 0.12f;
            GUI.DrawTexture(new Rect(x, 0f, 2f, Screen.height), Texture2D.whiteTexture);
        }
        for (int i = 0; i < 36; i++)
        {
            float x = Mathf.Repeat(i * 137f + 31f, Screen.width);
            float y = Mathf.Repeat(i * 83f + 19f, Screen.height);
            float size = i % 4 == 0 ? 4f : 2f;
            GUI.color = new Color(0.38f, 0.84f, 1f, 0.18f + 0.12f * Mathf.Sin(Time.time + i));
            GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
        }
        GUI.color = old;
    }

    private void DrawSaveSlot(Rect parent, float pad, float rowTop, float gap, float rowHeight, int index)
    {
        Rect row = new Rect(parent.x + pad, rowTop + index * (rowHeight + gap), parent.width - pad * 2f, rowHeight);
        bool selected = selectedSlot == index;
        DrawFrame(row, selected ? 4f : 3f, selected ? new Color(0.18f, 0.83f, 1f) : new Color(0.87f, 0.55f, 0.17f));
        GUI.color = new Color(0.008f, 0.035f, 0.09f, 1f);
        GUI.DrawTexture(new Rect(row.x + 5f, row.y + 5f, row.width - 10f, row.height - 10f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float left = row.x + 18f;
        float textWidth = row.width * 0.55f;
        GUI.Label(new Rect(left, row.y + 8f, textWidth, row.height * 0.38f), $"セーブスロット {index + 1}", slotNameStyle);
        SavePreview save = saveSlots[index];
        string info = save == null
            ? "空きスロット　この枠から新しく始められます"
            : $"所持金 ¥{FormatMoney(save.money)}　転生P {save.reincarnationPoints}　超越P {save.transcendencePoints}　連チャン {save.consecutiveWins}";
        GUI.Label(new Rect(left, row.y + row.height * 0.43f, textWidth, row.height * 0.48f), info, slotInfoStyle);

        float controlsX = row.xMax - 330f;
        Rect selectRect = new Rect(controlsX, row.center.y - 25f, 180f, 50f);
        if (GUI.Button(selectRect, selected ? (save == null ? "新規枠を選択中" : "このデータを選択中") : "選択", selected ? buttonStyle : secondaryButtonStyle))
            selectedSlot = index;
        if (save != null)
        {
            Rect deleteRect = new Rect(row.xMax - 138f, row.center.y - 25f, 118f, 50f);
            if (GUI.Button(deleteRect, "削除", secondaryButtonStyle)) pendingDeleteSlot = index;
        }
    }

    private void DrawDeleteConfirmation(Rect parent)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0.006f, 0.025f, 0.78f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float width = Mathf.Min(parent.width * 0.68f, 620f);
        float height = Mathf.Clamp(parent.height * 0.31f, 190f, 270f);
        Rect modal = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        DrawFrame(modal, 6f, new Color(1f, 0.65f, 0.16f));
        GUI.color = new Color(0.008f, 0.035f, 0.09f, 1f);
        GUI.DrawTexture(new Rect(modal.x + 6f, modal.y + 6f, modal.width - 12f, modal.height - 12f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(modal.x + 20f, modal.y + 20f, modal.width - 40f, 48f), "セーブデータを削除しますか？", modalStyle);
        GUI.Label(new Rect(modal.x + 20f, modal.y + 72f, modal.width - 40f, 40f), $"スロット {pendingDeleteSlot + 1} のデータは元に戻せません。", slotInfoStyle);

        float buttonWidth = Mathf.Min(210f, (modal.width - 60f) * 0.5f);
        float buttonY = modal.yMax - 66f;
        if (GUI.Button(new Rect(modal.center.x - buttonWidth - 8f, buttonY, buttonWidth, 46f), "削除する", buttonStyle))
            DeleteSlot(pendingDeleteSlot);
        if (GUI.Button(new Rect(modal.center.x + 8f, buttonY, buttonWidth, 46f), "キャンセル", secondaryButtonStyle))
            pendingDeleteSlot = -1;
        GUI.color = old;
    }

    private void DrawFrame(Rect rect, float thickness, Color accent = default)
    {
        if (accent == default) accent = new Color(1f, 0.63f, 0.16f);
        Color old = GUI.color;
        GUI.color = new Color(0.24f, 0.14f, 0.06f, 1f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = accent;
        GUI.DrawTexture(new Rect(rect.x + thickness, rect.y + thickness, rect.width - thickness * 2f, rect.height - thickness * 2f), Texture2D.whiteTexture);
        GUI.color = new Color(0.006f, 0.025f, 0.075f, 1f);
        GUI.DrawTexture(new Rect(rect.x + thickness * 2f, rect.y + thickness * 2f, rect.width - thickness * 4f, rect.height - thickness * 4f), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void RefreshSaveSlots()
    {
        for (int i = 0; i < saveSlots.Length; i++)
        {
            string json = PlayerPrefs.GetString("PachinkoSaveSlot_" + i, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                saveSlots[i] = null;
                continue;
            }
            try
            {
                SavePreview data = JsonUtility.FromJson<SavePreview>(json);
                saveSlots[i] = data != null && data.version == 1 ? data : null;
            }
            catch
            {
                saveSlots[i] = null;
            }
        }
    }

    private static string FormatMoney(string value)
    {
        return BigInteger.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger amount)
            ? BigInteger.Max(BigInteger.Zero, amount).ToString("N0", CultureInfo.InvariantCulture) : "0";
    }

    private void StartSelectedSlot()
    {
        SavePreview selected = saveSlots[selectedSlot];
        PachinkoGameController.RequestTitleLaunch(selectedSlot, selected == null);
        SceneManager.LoadScene("PachinkoPrototype");
    }

    private void DeleteSlot(int index)
    {
        if (index < 0 || index >= saveSlots.Length) return;
        PlayerPrefs.DeleteKey("PachinkoSaveSlot_" + index);
        PlayerPrefs.Save();
        saveSlots[index] = null;
        pendingDeleteSlot = -1;
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        int scale = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 1080f), 1, 2);
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 60 * scale,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.82f, 0.96f, 1f) }
        };
        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24 * scale,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.35f, 0.84f, 1f) }
        };
        slotNameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 25 * scale,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.86f, 0.96f, 1f) }
        };
        slotInfoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17 * scale,
            wordWrap = true,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.64f, 0.84f, 1f) }
        };
        buttonStyle = MakeButton(new Color(0.02f, 0.36f, 0.88f), new Color(0.44f, 0.88f, 1f));
        secondaryButtonStyle = MakeButton(new Color(0.018f, 0.085f, 0.19f), new Color(0.95f, 0.62f, 0.19f));
        modalStyle = new GUIStyle(slotNameStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 23 * scale };
    }

    private static GUIStyle MakeButton(Color fill, Color border)
    {
        Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[64];
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                pixels[y * 8 + x] = x == 0 || y == 0 || x == 7 || y == 7 ? border : fill;
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = 21,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = texture, textColor = Color.white },
            hover = { background = texture, textColor = Color.white },
            active = { background = texture, textColor = Color.white },
            focused = { background = texture, textColor = Color.white }
        };
        return style;
    }
}
