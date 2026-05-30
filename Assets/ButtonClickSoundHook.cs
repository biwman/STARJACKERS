using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonClickSoundHook : MonoBehaviour
{
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    void HandleClick()
    {
        string objectName = gameObject.name.ToLowerInvariant();
        string label = string.Empty;

        TMPro.TMP_Text text = GetComponentInChildren<TMPro.TMP_Text>(true);
        if (text != null)
        {
            label = text.text.ToLowerInvariant();
        }

        if (ShouldSuppressClickSound(objectName, label))
            return;

        AudioManager.Instance.PlayClick();
    }

    public static bool ShouldSuppressClickSound(string objectName, string label)
    {
        objectName = (objectName ?? string.Empty).ToLowerInvariant();
        label = (label ?? string.Empty).Trim().ToLowerInvariant();

        if (objectName.Contains("collect") || label.Contains("use") || label.Contains("reload"))
            return true;

        return objectName.Contains("shopbuybutton") ||
               objectName.Contains("shoptradebutton") ||
               objectName.Contains("itempreviewsellbutton") ||
               objectName.Contains("playerinventoryextendconfirmbutton") ||
               label == "buy" ||
               label == "sell" ||
               label == "trade";
    }
}
