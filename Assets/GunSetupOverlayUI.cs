using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GunSetupOverlayUI : MonoBehaviour
{
    const string RootName = "GunSetupOverlay";
    const float PanelWidth = 1320f;
    const float PanelHeight = 840f;
    const float LabelWidth = 270f;
    const float ColumnWidth = 270f;
    const float RowHeight = 48f;
    const float ContentTopPadding = 24f;
    const string CsvFileName = "gun_setup.csv";

    sealed class CellBinding
    {
        public string WeaponId;
        public WeaponAttackParameterDefinition Parameter;
        public TMP_InputField Input;
        public Button Button;
        public TMP_Text ButtonText;
        public Image ColorSwatch;
    }

    readonly Dictionary<string, WeaponAttackProfile> profilesByWeaponId = new Dictionary<string, WeaponAttackProfile>(System.StringComparer.Ordinal);
    readonly List<CellBinding> cells = new List<CellBinding>();
    GameObject rootObject;
    RectTransform contentRect;
    Button closeButton;
    Button resetButton;
    Button exportButton;
    Button importButton;
    TMP_Text statusText;
    string lastSignature = string.Empty;

    public static void Show()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find(RootName);
        GameObject root = existing != null ? existing.gameObject : new GameObject(RootName, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        GunSetupOverlayUI ui = root.GetComponent<GunSetupOverlayUI>();
        if (ui == null)
            ui = root.AddComponent<GunSetupOverlayUI>();

        ui.Open(root);
    }

    void Update()
    {
        if (rootObject == null || !rootObject.activeSelf)
            return;

        string signature = WeaponAttackCatalog.GetRoomSetupSignature() + "|host=" + PhotonNetwork.IsMasterClient;
        if (signature != lastSignature)
            RefreshCellsFromRoom();
    }

    void Open(GameObject root)
    {
        rootObject = root;
        BuildIfNeeded();
        rootObject.SetActive(true);
        rootObject.transform.SetAsLastSibling();
        RefreshCellsFromRoom();
    }

    void BuildIfNeeded()
    {
        if (rootObject == null)
            rootObject = gameObject;

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlayImage = rootObject.GetComponent<Image>();
        overlayImage.color = new Color(0.01f, 0.015f, 0.025f, 0.72f);
        overlayImage.raycastTarget = true;

        if (contentRect != null)
            return;

        GameObject panelObject = CreateChild(rootObject.transform, "GunSetupPanel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.07f, 0.1f, 0.14f, 0.97f);

        TMP_Text title = CreateLabel(panelObject.transform, "GunSetupTitle", "GUN SETUP", new Vector2(34f, -24f), new Vector2(360f, 36f), 30f, TextAlignmentOptions.Left);
        title.characterSpacing = 1.4f;

        TMP_Text subtitle = CreateLabel(panelObject.transform, "GunSetupSubtitle", "Complex shooting weapon parameters. These settings affect the round only when SHOOTING MODEL is COMPLEX.", new Vector2(36f, -68f), new Vector2(980f, 24f), 16f, TextAlignmentOptions.Left);
        subtitle.fontStyle = FontStyles.Normal;
        subtitle.color = new Color(0.78f, 0.84f, 0.91f, 0.94f);

        GameObject viewportObject = CreateChild(panelObject.transform, "GunSetupViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.5f, 1f);
        viewportRect.anchorMax = new Vector2(0.5f, 1f);
        viewportRect.pivot = new Vector2(0.5f, 1f);
        viewportRect.anchoredPosition = new Vector2(-22f, -116f);
        viewportRect.sizeDelta = new Vector2(1160f, 610f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0.04f, 0.06f, 0.09f, 0.72f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = CreateChild(viewportObject.transform, "GunSetupContent", typeof(RectTransform));
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.scrollSensitivity = 34f;

        BuildGrid();

        exportButton = CreateTextButton(panelObject.transform, "GunSetupExportButton", "EXPORT CSV", new Vector2(-635f, 34f), new Vector2(160f, 54f), new Color(0.13f, 0.31f, 0.34f, 0.98f));
        exportButton.onClick.AddListener(ExportCsv);

        importButton = CreateTextButton(panelObject.transform, "GunSetupImportButton", "IMPORT CSV", new Vector2(-455f, 34f), new Vector2(160f, 54f), new Color(0.18f, 0.34f, 0.18f, 0.98f));
        importButton.onClick.AddListener(ImportCsv);

        resetButton = CreateTextButton(panelObject.transform, "GunSetupResetButton", "RESET DEFAULTS", new Vector2(-205f, 34f), new Vector2(230f, 54f), new Color(0.32f, 0.16f, 0.18f, 0.98f));
        resetButton.onClick.AddListener(ResetDefaults);

        closeButton = CreateTextButton(panelObject.transform, "GunSetupCloseButton", "CLOSE", new Vector2(-34f, 34f), new Vector2(150f, 54f), new Color(0.16f, 0.34f, 0.58f, 0.98f));
        closeButton.onClick.AddListener(Close);

        statusText = CreateLabel(panelObject.transform, "GunSetupStatus", string.Empty, new Vector2(36f, -790f), new Vector2(500f, 24f), 14f, TextAlignmentOptions.Left);
        statusText.fontStyle = FontStyles.Normal;
        statusText.color = new Color(0.74f, 0.86f, 0.94f, 0.94f);
    }

    void BuildGrid()
    {
        IReadOnlyList<string> weaponIds = WeaponAttackCatalog.GetEditableWeaponIds();
        IReadOnlyList<WeaponAttackParameterDefinition> parameters = WeaponAttackCatalog.GetEditableParameters();

        float width = LabelWidth + weaponIds.Count * ColumnWidth + 40f;
        float height = ContentTopPadding + RowHeight * (parameters.Count + 1) + 28f;
        contentRect.sizeDelta = new Vector2(width, height);

        CreateLabel(contentRect, "GunSetupHeaderParam", "PARAMETER", new Vector2(18f, -ContentTopPadding), new Vector2(LabelWidth - 24f, 34f), 18f, TextAlignmentOptions.Left);

        for (int weaponIndex = 0; weaponIndex < weaponIds.Count; weaponIndex++)
        {
            string weaponId = weaponIds[weaponIndex];
            float x = LabelWidth + weaponIndex * ColumnWidth;
            TMP_Text header = CreateLabel(contentRect, "GunSetupHeader_" + weaponId, WeaponAttackCatalog.GetWeaponDisplayName(weaponId), new Vector2(x, -ContentTopPadding), new Vector2(ColumnWidth - 14f, 34f), 18f, TextAlignmentOptions.Center);
            header.color = new Color(0.72f, 1f, 0.88f, 1f);
        }

        for (int row = 0; row < parameters.Count; row++)
        {
            WeaponAttackParameterDefinition parameter = parameters[row];
            float y = -ContentTopPadding - RowHeight * (row + 1);
            CreateLabel(contentRect, "GunSetupParam_" + parameter.Key, parameter.Label, new Vector2(18f, y), new Vector2(LabelWidth - 24f, 36f), 16f, TextAlignmentOptions.Left);

            for (int weaponIndex = 0; weaponIndex < weaponIds.Count; weaponIndex++)
            {
                string weaponId = weaponIds[weaponIndex];
                float x = LabelWidth + weaponIndex * ColumnWidth;
                CreateCell(weaponId, parameter, new Vector2(x, y), new Vector2(ColumnWidth - 14f, 38f));
            }
        }
    }

    void CreateCell(string weaponId, WeaponAttackParameterDefinition parameter, Vector2 position, Vector2 size)
    {
        if (parameter.ValueType == WeaponAttackCatalog.ParameterTypeBool ||
            parameter.ValueType == WeaponAttackCatalog.ParameterTypeMarkerType)
        {
            Button button = CreateTextButton(contentRect, "GunSetupCell_" + weaponId + "_" + parameter.Key, string.Empty, position, size, new Color(0.11f, 0.16f, 0.23f, 0.96f));
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            string capturedWeaponId = weaponId;
            WeaponAttackParameterDefinition capturedParameter = parameter;
            button.onClick.AddListener(() => ToggleButtonParameter(capturedWeaponId, capturedParameter));
            cells.Add(new CellBinding { WeaponId = weaponId, Parameter = parameter, Button = button, ButtonText = text });
            return;
        }

        GameObject cellObject = CreateChild(contentRect, "GunSetupInput_" + weaponId + "_" + parameter.Key, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = cellObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = cellObject.GetComponent<Image>();
        image.color = new Color(0.1f, 0.14f, 0.2f, 0.94f);

        GameObject textAreaObject = CreateChild(cellObject.transform, "Text Area", typeof(RectTransform), typeof(RectMask2D));
        RectTransform textAreaRect = textAreaObject.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(6f, 2f);
        textAreaRect.offsetMax = new Vector2(-6f, -2f);

        GameObject textObject = CreateChild(textAreaObject.transform, "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 2f);
        textRect.offsetMax = parameter.ValueType == WeaponAttackCatalog.ParameterTypeColor ? new Vector2(-38f, -2f) : new Vector2(-4f, -2f);

        TMP_Text inputText = textObject.GetComponent<TMP_Text>();
        inputText.fontSize = 17f;
        inputText.fontStyle = FontStyles.Bold;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;
        inputText.textWrappingMode = TextWrappingModes.NoWrap;
        inputText.raycastTarget = false;
        ApplyReferenceFont(inputText);

        TMP_InputField input = cellObject.GetComponent<TMP_InputField>();
        input.textViewport = textAreaRect;
        input.textComponent = inputText;
        input.targetGraphic = image;
        input.characterLimit = parameter.ValueType == WeaponAttackCatalog.ParameterTypeColor ? 7 : 24;
        input.lineType = TMP_InputField.LineType.SingleLine;
        string inputWeaponId = weaponId;
        WeaponAttackParameterDefinition inputParameter = parameter;
        input.onEndEdit.AddListener(value => ApplyInputValue(inputWeaponId, inputParameter, value));

        Image swatch = null;
        if (parameter.ValueType == WeaponAttackCatalog.ParameterTypeColor)
        {
            GameObject swatchObject = CreateChild(cellObject.transform, "ColorSwatch", typeof(RectTransform), typeof(Image));
            RectTransform swatchRect = swatchObject.GetComponent<RectTransform>();
            swatchRect.anchorMin = new Vector2(1f, 0.5f);
            swatchRect.anchorMax = new Vector2(1f, 0.5f);
            swatchRect.pivot = new Vector2(1f, 0.5f);
            swatchRect.anchoredPosition = new Vector2(-8f, 0f);
            swatchRect.sizeDelta = new Vector2(30f, 24f);
            swatch = swatchObject.GetComponent<Image>();
        }

        cells.Add(new CellBinding { WeaponId = weaponId, Parameter = parameter, Input = input, ColorSwatch = swatch });
    }

    void RefreshCellsFromRoom()
    {
        profilesByWeaponId.Clear();
        IReadOnlyList<string> weaponIds = WeaponAttackCatalog.GetEditableWeaponIds();
        for (int i = 0; i < weaponIds.Count; i++)
        {
            string weaponId = weaponIds[i];
            profilesByWeaponId[weaponId] = WeaponAttackCatalog.GetNormalAttackByWeaponId(weaponId);
        }

        bool isHost = PhotonNetwork.IsMasterClient;
        for (int i = 0; i < cells.Count; i++)
        {
            CellBinding cell = cells[i];
            if (cell == null || cell.Parameter == null || !profilesByWeaponId.TryGetValue(cell.WeaponId, out WeaponAttackProfile profile))
                continue;

            string value = WeaponAttackCatalog.GetProfileValueText(profile, cell.Parameter);
            if (cell.Input != null)
            {
                cell.Input.SetTextWithoutNotify(value);
                cell.Input.interactable = isHost;
            }

            if (cell.Button != null)
                cell.Button.interactable = isHost;

            if (cell.ButtonText != null)
                cell.ButtonText.text = value;

            if (cell.ColorSwatch != null)
                cell.ColorSwatch.color = profile.MarkerColor;
        }

        if (resetButton != null)
            resetButton.interactable = isHost;

        if (exportButton != null)
            exportButton.interactable = true;

        if (importButton != null)
            importButton.interactable = isHost;

        lastSignature = WeaponAttackCatalog.GetRoomSetupSignature() + "|host=" + PhotonNetwork.IsMasterClient;
    }

    void ApplyInputValue(string weaponId, WeaponAttackParameterDefinition parameter, string value)
    {
        if (!PhotonNetwork.IsMasterClient || !profilesByWeaponId.TryGetValue(weaponId, out WeaponAttackProfile profile))
        {
            RefreshCellsFromRoom();
            return;
        }

        if (!WeaponAttackCatalog.TrySetProfileValue(profile, parameter, value))
        {
            RefreshCellsFromRoom();
            return;
        }

        SaveProfile(profile);
    }

    void ToggleButtonParameter(string weaponId, WeaponAttackParameterDefinition parameter)
    {
        if (!PhotonNetwork.IsMasterClient || !profilesByWeaponId.TryGetValue(weaponId, out WeaponAttackProfile profile))
            return;

        if (parameter.ValueType == WeaponAttackCatalog.ParameterTypeBool)
        {
            profile.Pierces = !profile.Pierces;
        }
        else if (parameter.ValueType == WeaponAttackCatalog.ParameterTypeMarkerType)
        {
            profile.MarkerType = profile.MarkerType == ComplexAttackMarkerType.Line
                ? ComplexAttackMarkerType.Arc
                : ComplexAttackMarkerType.Line;
        }

        SaveProfile(profile);
    }

    void SaveProfile(WeaponAttackProfile profile)
    {
        if (profile == null || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable
        {
            [RoomSettings.GetGunSetupRoomKey(profile.Id)] = WeaponAttackCatalog.SerializeProfile(profile)
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshCellsFromRoom();
    }

    void ExportCsv()
    {
        try
        {
            string path = GetCsvPath();
            File.WriteAllText(path, BuildCsv(), Encoding.UTF8);
            SetStatus("Exported: " + path);
        }
        catch (Exception ex)
        {
            Debug.LogError("Gun setup CSV export failed: " + ex);
            SetStatus("Export failed.");
        }
    }

    void ImportCsv()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
        {
            SetStatus("Only host can import gun setup.");
            return;
        }

        string path = GetCsvPath();
        if (!File.Exists(path))
        {
            SetStatus("CSV not found: " + path);
            Debug.LogWarning("Gun setup CSV import file not found: " + path);
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            int imported = ImportCsvLines(lines);
            if (imported <= 0)
            {
                SetStatus("No compatible weapon rows found.");
                return;
            }

            RefreshCellsFromRoom();
            SetStatus("Imported " + imported + " weapon rows.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Gun setup CSV import failed: " + ex);
            SetStatus("Import failed.");
        }
    }

    string BuildCsv()
    {
        StringBuilder builder = new StringBuilder();
        List<string> headers = new List<string> { "weapon_id", "weapon_name" };
        IReadOnlyList<WeaponAttackParameterDefinition> parameters = WeaponAttackCatalog.GetEditableParameters();
        for (int i = 0; i < parameters.Count; i++)
            headers.Add(parameters[i].Key);

        AppendCsvLine(builder, headers);

        IReadOnlyList<string> weaponIds = WeaponAttackCatalog.GetEditableWeaponIds();
        for (int weaponIndex = 0; weaponIndex < weaponIds.Count; weaponIndex++)
        {
            string weaponId = weaponIds[weaponIndex];
            WeaponAttackProfile profile = WeaponAttackCatalog.GetNormalAttackByWeaponId(weaponId);
            List<string> values = new List<string>
            {
                weaponId,
                profile != null ? profile.DisplayName : WeaponAttackCatalog.GetWeaponDisplayName(weaponId)
            };

            for (int parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
                values.Add(WeaponAttackCatalog.GetProfileValueText(profile, parameters[parameterIndex]));

            AppendCsvLine(builder, values);
        }

        return builder.ToString();
    }

    int ImportCsvLines(string[] lines)
    {
        if (lines == null || lines.Length < 2)
            return 0;

        List<string> headers = ParseCsvLine(lines[0]);
        if (headers.Count == 0)
            return 0;

        Dictionary<string, int> headerIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            string header = StripBom(headers[i]).Trim();
            if (!string.IsNullOrWhiteSpace(header) && !headerIndexes.ContainsKey(header))
                headerIndexes[header] = i;
        }

        if (!headerIndexes.TryGetValue("weapon_id", out int weaponIdIndex))
            return 0;

        IReadOnlyList<WeaponAttackParameterDefinition> parameters = WeaponAttackCatalog.GetEditableParameters();
        Hashtable props = new Hashtable();
        int imported = 0;
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                continue;

            List<string> columns = ParseCsvLine(lines[lineIndex]);
            if (weaponIdIndex < 0 || weaponIdIndex >= columns.Count)
                continue;

            string weaponId = columns[weaponIdIndex].Trim();
            if (!WeaponAttackCatalog.IsEditableWeaponId(weaponId))
                continue;

            WeaponAttackProfile profile = WeaponAttackCatalog.GetNormalAttackByWeaponId(weaponId);
            bool touched = false;
            for (int parameterIndex = 0; parameterIndex < parameters.Count; parameterIndex++)
            {
                WeaponAttackParameterDefinition parameter = parameters[parameterIndex];
                if (parameter == null ||
                    !headerIndexes.TryGetValue(parameter.Key, out int columnIndex) ||
                    columnIndex < 0 ||
                    columnIndex >= columns.Count)
                {
                    continue;
                }

                string rawValue = columns[columnIndex];
                if (WeaponAttackCatalog.TrySetProfileValue(profile, parameter, rawValue))
                    touched = true;
            }

            if (!touched)
                continue;

            props[RoomSettings.GetGunSetupRoomKey(weaponId)] = WeaponAttackCatalog.SerializeProfile(profile);
            imported++;
        }

        if (props.Count > 0)
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        return imported;
    }

    void AppendCsvLine(StringBuilder builder, List<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
                builder.Append(',');

            builder.Append(EscapeCsv(values[i]));
        }

        builder.AppendLine();
    }

    string EscapeCsv(string value)
    {
        value ??= string.Empty;
        bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        if (!needsQuotes)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    List<string> ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        if (line == null)
            return values;

        StringBuilder current = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Length = 0;
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    string StripBom(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value[0] == '\ufeff' ? value.Substring(1) : value;
    }

    string GetCsvPath()
    {
        return Path.Combine(Application.persistentDataPath, CsvFileName);
    }

    void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? string.Empty;
    }

    void ResetDefaults()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable();
        IReadOnlyList<string> weaponIds = WeaponAttackCatalog.GetEditableWeaponIds();
        for (int i = 0; i < weaponIds.Count; i++)
            props[RoomSettings.GetGunSetupRoomKey(weaponIds[i])] = string.Empty;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        RefreshCellsFromRoom();
    }

    void Close()
    {
        if (rootObject != null)
            rootObject.SetActive(false);
    }

    TMP_Text CreateLabel(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.97f, 1f, 1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        ApplyReferenceFont(text);
        return text;
    }

    Button CreateTextButton(Transform parent, string name, string value, Vector2 position, Vector2 size, Color color)
    {
        GameObject buttonObject = CreateChild(parent, name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);

        if (parent == contentRect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
        }

        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = image;

        GameObject textObject = CreateChild(buttonObject.transform, "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = 17f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        ApplyReferenceFont(text);
        return button;
    }

    GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(name, components);
        child.transform.SetParent(parent, false);
        return child;
    }

    void ApplyReferenceFont(TMP_Text text)
    {
        if (text == null)
            return;

        TMP_Text reference = FindAnyObjectByType<TMP_Text>();
        if (reference == null || reference == text)
            return;

        text.font = reference.font;
        text.fontSharedMaterial = reference.fontSharedMaterial;
    }
}
