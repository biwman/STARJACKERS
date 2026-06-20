using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class LobbyManager
{
    void SetLegacyLobbyUiActive(bool active)
    {
        if (legacyLobbyUiActive == active)
            return;

        legacyLobbyUiActive = active;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            if (active && IsDeprecatedLobbySettingObjectName(child.name))
            {
                child.gameObject.SetActive(false);
                continue;
            }

            child.gameObject.SetActive(active);
        }
    }

    void SetGameplayHudVisible(bool visible, bool force = false)
    {
        if (!force && gameplayHudVisibilityInitialized && lastGameplayHudVisible == visible)
            return;

        gameplayHudVisibilityInitialized = true;
        lastGameplayHudVisible = visible;
        CacheGameplayHudObjects();
        foreach (GameObject hudObject in gameplayHudObjectsByName.Values)
        {
            if (hudObject != null)
                ApplyHudVisibility(hudObject, visible);
        }
    }

    void CacheGameplayHudObjects()
    {
        bool needsScan = false;
        for (int i = 0; i < GameplayHudObjectNames.Length; i++)
        {
            string hudName = GameplayHudObjectNames[i];
            if (!gameplayHudObjectsByName.ContainsKey(hudName) || gameplayHudObjectsByName[hudName] == null)
            {
                gameplayHudObjectsByName[hudName] = null;
                needsScan = true;
            }
        }

        if (!needsScan)
            return;

        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject go = sceneObjects[i];
            if (go == null || !go.scene.IsValid())
                continue;

            for (int nameIndex = 0; nameIndex < GameplayHudObjectNames.Length; nameIndex++)
            {
                string hudName = GameplayHudObjectNames[nameIndex];
                if (go.name == hudName)
                {
                    gameplayHudObjectsByName[hudName] = go;
                    break;
                }
            }
        }
    }

    GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject go = sceneObjects[i];
            if (go != null && go.name == objectName && go.scene.IsValid())
                return go;
        }

        return null;
    }

    void HideDeprecatedSettingButton(string buttonName, string textName)
    {
        GameObject buttonObject = FindSceneObjectByName(buttonName);
        if (buttonObject != null)
            buttonObject.SetActive(false);

        GameObject textObject = FindSceneObjectByName(textName);
        if (textObject != null)
            textObject.SetActive(false);
    }

    bool IsDeprecatedLobbySettingObjectName(string objectName)
    {
        for (int i = 0; i < DeprecatedLobbySettingObjectNames.Length; i++)
        {
            if (objectName == DeprecatedLobbySettingObjectNames[i])
                return true;
        }

        return false;
    }

    void ApplyHudVisibility(GameObject hudObject, bool visible)
    {
        if (hudObject == null)
            return;

        RectTransform rectTransform = hudObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            CanvasGroup group = hudObject.GetComponent<CanvasGroup>();
            if (group == null)
                group = hudObject.AddComponent<CanvasGroup>();

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (!hudObject.activeSelf)
                hudObject.SetActive(true);
            return;
        }

        hudObject.SetActive(visible);
    }
}
