using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
public partial class PlayerProfilePanelUI
{
    void RefreshLobbyUi()
    {
        LobbyManager lobby = FindAnyObjectByType<LobbyManager>();
        if (lobby != null)
        {
            lobby.ForceRefreshUi();
        }
    }

    void SetGameplayHudVisible(bool visible)
    {
        for (int i = 0; i < GameplayHudObjectNames.Length; i++)
        {
            string objectName = GameplayHudObjectNames[i];
            if (!gameplayHudObjectsByName.TryGetValue(objectName, out GameObject target) || target == null)
            {
                target = FindSceneObjectByName(objectName);
                if (target != null)
                    gameplayHudObjectsByName[objectName] = target;
            }

            if (target != null)
                ApplyHudVisibility(target, visible);
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
