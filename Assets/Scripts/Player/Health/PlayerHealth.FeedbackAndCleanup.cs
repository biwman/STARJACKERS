using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PlayerHealth : MonoBehaviourPun
{
    [PunRPC]
    void ShowDeathMessage()
    {
        HideLegacyDeathMessage();
        RoundMessageLayer.ShowStatusFeed(
            "PILOT DOWN",
            "Someone is dead",
            RoundMessagePriority.Warning,
            4.6f,
            new Color(1f, 0.32f, 0.16f, 1f));
    }

    void HideLegacyDeathMessage()
    {
        GameObject obj = FindObjectEvenIfDisabled("DeathMessage");
        if (obj != null)
            obj.SetActive(false);
    }

    [PunRPC]
    void PlayDeathExplosion()
    {
        AudioManager.Instance.PlayExplosionAt(transform.position);

        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (IsBotControlled)
        {
            EnemyBot bot = GetComponent<EnemyBot>();
            EnemyDeathBoomVfx.Spawn(transform.position, renderer, bot != null ? bot.VisualTargetSize : GameVisualTheme.PlayerTargetSize);
            return;
        }

        if (!IsNeutralRiderControlled && !IsAstronautControlled)
        {
            PlayerShipExplosionVfx.Spawn(transform.position, renderer);
        }
    }

    [PunRPC]
    void PlayShieldHitAudio()
    {
        AudioManager.Instance.PlayShieldHitAt(transform.position);
    }

    [PunRPC]
    void PlayShieldHitVisual(float x, float y)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        Vector2 visualPosition = ResolveVisibleShieldHitPosition(new Vector2(x, y), renderer);
        ShieldHitVfx.Spawn(new Vector3(visualPosition.x, visualPosition.y, 0f), renderer);
    }

    Vector2 ResolveVisibleShieldHitPosition(Vector2 requestedPosition, SpriteRenderer renderer)
    {
        Collider2D ownCollider = GetComponentInChildren<Collider2D>();
        if (ownCollider != null)
        {
            Vector2 closestPoint = ownCollider.ClosestPoint(requestedPosition);
            if (Vector2.Distance(closestPoint, transform.position) > 0.02f)
                return closestPoint;
        }

        if (renderer != null)
        {
            Vector3 closestPoint = renderer.bounds.ClosestPoint(requestedPosition);
            if (Vector2.Distance(closestPoint, transform.position) > 0.02f)
                return closestPoint;
        }

        return transform.position;
    }

    [PunRPC]
    void PlayBatteryShieldChargeAudio()
    {
        AudioManager.Instance.PlayShieldChargeAt(transform.position);
    }

    [PunRPC]
    void PlayShieldFullPowerAudio()
    {
        AudioManager.Instance.PlayShieldFullPowerAt(transform.position);
    }

    [PunRPC]
    void PlayHpHitAudio()
    {
        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null && EnemyBot.IsSpaceAnimalKind(bot.Kind))
        {
            AudioManager.Instance.PlayAnimalHitAt(transform.position);
            return;
        }

        AudioManager.Instance.PlayHpHitAt(transform.position);
    }

    [PunRPC]
    void PlayHpHitVisual(float x, float y)
    {
        if (!RoomSettings.AreVisualEffectsEnabled())
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        Vector2 visualPosition = ResolveVisibleShieldHitPosition(new Vector2(x, y), renderer);
        HpHitSparksVfx.Spawn(new Vector3(visualPosition.x, visualPosition.y, 0f), transform.position, renderer);
    }

    [PunRPC]
    public async void OnTimeUp()
    {
        if (!photonView.IsMine)
            return;

        PlayerProfileService.Instance.DiscardPendingAstronautCargo();
        try
        {
            await PlayerProfileService.Instance.FailAvengerTheftAttemptAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to clear Avenger theft attempt after time up: " + ex);
        }

        try
        {
            await PlayerProfileService.Instance.FailArrowFinalRunAsync();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to clear Arrow final run after time up: " + ex);
        }
        finally
        {
            ArrowRacePlotController.ClearLocalFinalRunState();
        }

        ShowTimeUpMessage();
        StartCoroutine(DieAfterDelay());
    }

    void ShowTimeUpMessage()
    {
        GameObject obj = GameObject.Find("TimeUpMessage");
        if (obj != null)
        {
            obj.SetActive(true);
        }
    }

    IEnumerator DieAfterDelay()
    {
        yield return new WaitForSeconds(1.5f);

        if (photonView.IsMine)
        {
            TryDestroyOwnedPhotonObject();
        }
    }

    IEnumerator DestroyBotSafely()
    {
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StopEngineAudioImmediately();
            movement.enabled = false;
        }

        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
            shooting.enabled = false;

        EnemyBot bot = GetComponent<EnemyBot>();
        if (bot != null)
            bot.enabled = false;

        TreasureCollector collector = GetComponent<TreasureCollector>();
        if (collector != null)
            collector.enabled = false;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        yield return null;

        if (PhotonNetwork.IsConnected && photonView.IsMine)
            TryDestroyOwnedPhotonObject();
        else if (!PhotonNetwork.IsConnected)
            Destroy(gameObject);
    }

    void TryDestroyOwnedPhotonObject()
    {
        if (destroyRequested || gameObject == null)
            return;

        destroyRequested = true;

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            if (photonView.IsMine)
                PhotonNetwork.Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    Sprite LoadPlayerWreckSprite(int shipSkinIndex)
    {
        string resourcePath = ShipCatalog.GetWreckResourcePathForSkin(shipSkinIndex);
        if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            Sprite resourceSprite = Resources.Load<Sprite>(resourcePath);
            if (resourceSprite != null)
                return resourceSprite;
        }

#if UNITY_EDITOR
        string editorPath = ShipCatalog.GetWreckEditorResourcePathForSkin(shipSkinIndex);
        if (!string.IsNullOrWhiteSpace(editorPath))
        {
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(editorPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                    return sprite;
            }

            Sprite directSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorPath);
            if (directSprite != null)
                return directSprite;
        }

        string fallbackPath = ShipCatalog.GetWreckEditorFallbackPathForSkin(shipSkinIndex);
        if (!string.IsNullOrWhiteSpace(fallbackPath))
        {
            Sprite fallbackSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
            if (fallbackSprite != null)
                return fallbackSprite;
        }
#endif

        return null;
    }

    GameObject FindObjectEvenIfDisabled(string name)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject go in all)
        {
            if (go.name == name)
                return go;
        }

        return null;
    }
}
