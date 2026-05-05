using UnityEngine;
using TMPro;

public class EndScreenUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text endMessage;
    public Transform playerListParent;
    public GameObject playerItemPrefab;

    public void Show()
    {
        if (panel == null)
        {
            Debug.LogError("PANEL NULL");
            return;
        }

        panel.SetActive(true);

        if (endMessage != null)
        {
            endMessage.text = "Wyniki rundy";
        }

        Transform resolvedListParent = ResolveSceneTransform(playerListParent, "PlayerListContent");
        if (resolvedListParent != null)
        {
            playerListParent = resolvedListParent;
        }

        GameObject resolvedItemPrefab = ResolvePlayerItemPrefab();
        if (resolvedItemPrefab != null)
        {
            playerItemPrefab = resolvedItemPrefab;
        }
    }

    Transform ResolveSceneTransform(Transform current, string objectName)
    {
        if (current != null && current.gameObject.scene.IsValid())
        {
            return current;
        }

        GameObject sceneObject = FindObjectEvenIfDisabled(objectName);
        if (sceneObject != null && sceneObject.scene.IsValid())
        {
            return sceneObject.transform;
        }

        return null;
    }

    GameObject ResolvePlayerItemPrefab()
    {
        if (playerItemPrefab != null)
        {
            return playerItemPrefab;
        }

        return Resources.Load<GameObject>("PlayerListItem");
    }

    GameObject FindObjectEvenIfDisabled(string name)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj.name == name)
            {
                return obj;
            }
        }

        return null;
    }
}
