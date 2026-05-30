using System.Threading.Tasks;
using UnityEngine;

public sealed class MenuWarmupService : MonoBehaviour
{
    static MenuWarmupService instance;
    bool warmupStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("MenuWarmupService");
        instance = root.AddComponent<MenuWarmupService>();
        DontDestroyOnLoad(root);
    }

    async void Start()
    {
        await WarmupAsync();
    }

    async Task WarmupAsync()
    {
        if (warmupStarted)
            return;

        warmupStarted = true;

        UIRuntimeStyler.PrewarmRuntimeSprites();
        InventoryItemCatalog.PrewarmIcons();

        await PlayerProfileService.Instance.EnsureInitializedAsync();
        await PlayerProfilePanelUI.PrewarmAsync();
        SessionBrowserPanelUI.Prewarm();
    }
}
