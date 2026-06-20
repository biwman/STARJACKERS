using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class ValuableCargoCarrierOverlay : MonoBehaviourPun
{
    sealed class CargoMarkerTarget
    {
        public Transform Target;
        public Color Color;
    }

    sealed class CargoMarkerArrow
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
    }

    const float RefreshInterval = 0.2f;
    const float ArrowDistance = 2.12f;
    const float BaseScale = 0.34f;
    static Sprite arrowSprite;

    readonly List<CargoMarkerTarget> markerTargets = new List<CargoMarkerTarget>();
    readonly List<CargoMarkerArrow> arrows = new List<CargoMarkerArrow>();
    float nextRefreshTime;

    void Update()
    {
        if (photonView != null && !photonView.IsMine)
        {
            SetAllVisible(false);
            return;
        }

        if (!PhotonNetwork.InRoom || GameplayHudVisibility.IsExtractionCinematicSuppressed)
        {
            SetAllVisible(false);
            return;
        }

        if (Time.time >= nextRefreshTime)
        {
            nextRefreshTime = Time.time + RefreshInterval;
            RefreshTargets();
        }

        UpdateArrows();
    }

    void RefreshTargets()
    {
        markerTargets.Clear();

        Player[] players = PhotonNetwork.PlayerList;
        Player localPlayer = PhotonNetwork.LocalPlayer;
        if (players == null || localPlayer == null)
            return;

        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player == null || player.ActorNumber == localPlayer.ActorNumber)
                continue;

            PlayerHealth carrier = ValuableCargoCarrierUtility.FindActiveHumanShipForPlayer(player);
            if (carrier == null)
                continue;

            AddCargoMarkersForPlayer(player, carrier.transform, InventoryItemCatalog.PirateCaseId);
            AddCargoMarkersForPlayer(player, carrier.transform, InventoryItemCatalog.CashSuitcaseId);
        }

        EnsureArrowCount(markerTargets.Count);
    }

    void AddCargoMarkersForPlayer(Player player, Transform target, string itemId)
    {
        int count = ValuableCargoCarrierUtility.CountCargoItem(player, itemId);
        if (count <= 0 || target == null)
            return;

        if (!ValuableCargoCarrierUtility.TryGetTrackedCargoMarkerColor(itemId, out Color color))
            return;

        for (int i = 0; i < count; i++)
            markerTargets.Add(new CargoMarkerTarget { Target = target, Color = color });
    }

    void EnsureArrowCount(int count)
    {
        arrowSprite ??= RuntimeSpriteUtility.CreateArrowSprite();
        while (arrows.Count < count)
        {
            GameObject root = new GameObject("ValuableCargoCarrierArrow_" + arrows.Count.ToString("00"));
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = arrowSprite;
            renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
            renderer.sortingOrder = 94;
            root.transform.localScale = Vector3.one * BaseScale;
            root.SetActive(false);
            arrows.Add(new CargoMarkerArrow { Root = root, Renderer = renderer });
        }
    }

    void UpdateArrows()
    {
        EnsureArrowCount(markerTargets.Count);
        int targetCount = markerTargets.Count;
        Vector2 origin = transform.position;

        for (int i = 0; i < arrows.Count; i++)
        {
            CargoMarkerArrow arrow = arrows[i];
            if (arrow == null || arrow.Root == null)
                continue;

            bool visible = i < targetCount && markerTargets[i]?.Target != null;
            arrow.Root.SetActive(visible);
            if (!visible)
                continue;

            CargoMarkerTarget target = markerTargets[i];
            Vector2 direction = (Vector2)target.Target.position - origin;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.up;

            direction.Normalize();
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            float sideIndex = i - (targetCount - 1) * 0.5f;
            float sideOffset = Mathf.Clamp(sideIndex * 0.28f, -1.15f, 1.15f);
            Vector2 position = origin + direction * (ArrowDistance + Mathf.Abs(sideIndex) * 0.015f) + tangent * sideOffset;

            arrow.Root.transform.position = new Vector3(position.x, position.y, -0.36f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            arrow.Root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            float pulse = 0.88f + Mathf.Sin(Time.time * 6.2f + i * 0.73f) * 0.08f;
            arrow.Root.transform.localScale = Vector3.one * (BaseScale * pulse);

            if (arrow.Renderer != null)
                arrow.Renderer.color = target.Color;
        }
    }

    void SetAllVisible(bool visible)
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i]?.Root != null)
                arrows[i].Root.SetActive(visible);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i]?.Root != null)
                Destroy(arrows[i].Root);
        }

        arrows.Clear();
        markerTargets.Clear();
    }
}
