using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public sealed class GuidanceSystemOverlay : MonoBehaviourPun
{
    sealed class GuidanceArrow
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public Color Color;
        public Func<Vector2?> TargetResolver;
    }

    const float ArrowDistance = 1.65f;
    const float GuidanceDuration = 9f;
    static Sprite arrowSprite;

    readonly List<GuidanceArrow> arrows = new List<GuidanceArrow>();
    float activeUntil;
    float nextSoundTime;

    public static GuidanceSystemOverlay EnsureFor(GameObject player)
    {
        if (player == null)
            return null;

        GuidanceSystemOverlay overlay = player.GetComponent<GuidanceSystemOverlay>();
        if (overlay == null)
            overlay = player.AddComponent<GuidanceSystemOverlay>();

        return overlay;
    }

    public void ActivateGuidance(float duration = GuidanceDuration)
    {
        if (photonView != null && !photonView.IsMine)
            return;

        activeUntil = Time.time + Mathf.Max(0.1f, duration);
        EnsureArrows();
        SetVisible(!GameplayHudVisibility.IsExtractionCinematicSuppressed);
        nextSoundTime = 0f;
    }

    void Update()
    {
        if (photonView != null && !photonView.IsMine)
            return;

        if (GameplayHudVisibility.IsExtractionCinematicSuppressed)
        {
            SetVisible(false);
            return;
        }

        if (Time.time >= activeUntil)
        {
            SetVisible(false);
            return;
        }

        EnsureArrows();
        UpdateArrows();
        if (Time.time >= nextSoundTime)
        {
            AudioManager.Instance.PlayGuidanceSystemAt(transform.position);
            float wait = AudioManager.Instance.GuidanceSystemClip != null
                ? Mathf.Clamp(AudioManager.Instance.GuidanceSystemClip.length, 0.35f, 1.35f)
                : 0.75f;
            nextSoundTime = Time.time + wait;
        }
    }

    void EnsureArrows()
    {
        if (arrows.Count > 0)
            return;

        arrowSprite ??= RuntimeSpriteUtility.CreateArrowSprite();
        arrows.Add(CreateArrow("GuidanceExtractionArrow", new Color(0.2f, 1f, 0.34f, 0.92f), ResolveClosestExtraction));
        arrows.Add(CreateArrow("GuidanceLootArrow", new Color(1f, 0.76f, 0.16f, 0.94f), ResolveMostValuableLoot));
        arrows.Add(CreateArrow("GuidanceEnemyArrow", new Color(1f, 0.18f, 0.12f, 0.94f), ResolveClosestHostile));
    }

    GuidanceArrow CreateArrow(string name, Color color, Func<Vector2?> resolver)
    {
        GameObject root = new GameObject(name);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = arrowSprite;
        renderer.color = color;
        renderer.sortingLayerName = GameVisualTheme.WorldSortingLayerName;
        renderer.sortingOrder = 92;
        root.transform.localScale = Vector3.one * 0.42f;
        return new GuidanceArrow { Root = root, Renderer = renderer, Color = color, TargetResolver = resolver };
    }

    void SetVisible(bool visible)
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i]?.Root != null)
                arrows[i].Root.SetActive(visible);
        }
    }

    void UpdateArrows()
    {
        for (int i = 0; i < arrows.Count; i++)
        {
            GuidanceArrow arrow = arrows[i];
            if (arrow == null || arrow.Root == null)
                continue;

            Vector2? target = arrow.TargetResolver?.Invoke();
            bool visible = target.HasValue;
            arrow.Root.SetActive(visible);
            if (!visible)
                continue;

            Vector2 direction = target.Value - (Vector2)transform.position;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.up;
            direction.Normalize();

            float sideOffset = (i - 1) * 0.42f;
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            Vector2 position = (Vector2)transform.position + direction * ArrowDistance + tangent * sideOffset;
            arrow.Root.transform.position = new Vector3(position.x, position.y, -0.35f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            arrow.Root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            float pulse = 0.82f + Mathf.Sin(Time.time * 6.5f + i) * 0.08f;
            arrow.Root.transform.localScale = Vector3.one * (0.42f * pulse);
        }
    }

    Vector2? ResolveClosestExtraction()
    {
        ExtractionZone[] zones = FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        return ResolveClosestTransform(zones);
    }

    Vector2? ResolveMostValuableLoot()
    {
        Vector2 origin = transform.position;
        Vector2 bestPosition = Vector2.zero;
        int bestValue = -1;
        float bestDistance = float.MaxValue;

        Treasure[] treasures = FindObjectsByType<Treasure>(FindObjectsInactive.Exclude);
        for (int i = 0; i < treasures.Length; i++)
        {
            Treasure treasure = treasures[i];
            if (treasure == null)
                continue;

            string itemId = InventoryItemCatalog.ResolveAlienSecretItemId(treasure.itemId, treasure.visualVariantIndex);
            ConsiderLoot(treasure.transform.position, itemId, origin, ref bestPosition, ref bestValue, ref bestDistance);
        }

        ShipWreck[] wrecks = FindObjectsByType<ShipWreck>(FindObjectsInactive.Exclude);
        for (int i = 0; i < wrecks.Length; i++)
        {
            ShipWreck wreck = wrecks[i];
            if (wreck == null || !wreck.HasLoot)
                continue;

            ConsiderLoot(wreck.transform.position, wreck.GetLootItemAt(wreck.GetFirstLootIndex()), origin, ref bestPosition, ref bestValue, ref bestDistance);
        }

        DroppedCargoCrate[] crates = FindObjectsByType<DroppedCargoCrate>(FindObjectsInactive.Exclude);
        for (int i = 0; i < crates.Length; i++)
        {
            DroppedCargoCrate crate = crates[i];
            if (crate == null || !crate.HasLoot)
                continue;

            ConsiderLoot(crate.transform.position, crate.StoredItemId, origin, ref bestPosition, ref bestValue, ref bestDistance);
        }

        return bestValue >= 0 ? bestPosition : null;
    }

    void ConsiderLoot(Vector2 position, string itemId, Vector2 origin, ref Vector2 bestPosition, ref int bestValue, ref float bestDistance)
    {
        int value = InventoryItemCatalog.GetSellValueAstrons(itemId);
        float distance = Vector2.Distance(origin, position);
        if (value < bestValue || (value == bestValue && distance >= bestDistance))
            return;

        bestValue = value;
        bestDistance = distance;
        bestPosition = position;
    }

    Vector2? ResolveClosestHostile()
    {
        Vector2 origin = transform.position;
        PlayerHealth[] healths = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        PlayerHealth bestHuman = null;
        PlayerHealth bestBot = null;
        float bestHumanDistance = float.MaxValue;
        float bestBotDistance = float.MaxValue;

        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth candidate = healths[i];
            if (candidate == null || candidate.IsWreck || candidate.IsEvacuationAnimating || candidate.photonView == null || candidate.photonView == photonView)
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (candidate.IsBotControlled)
            {
                if (distance < bestBotDistance)
                {
                    bestBotDistance = distance;
                    bestBot = candidate;
                }
            }
            else if (distance < bestHumanDistance)
            {
                bestHumanDistance = distance;
                bestHuman = candidate;
            }
        }

        if (bestHuman != null)
            return bestHuman.transform.position;

        return bestBot != null ? bestBot.transform.position : null;
    }

    Vector2? ResolveClosestTransform(Component[] components)
    {
        if (components == null || components.Length == 0)
            return null;

        Vector2 origin = transform.position;
        Transform best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            float distance = Vector2.Distance(origin, component.transform.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = component.transform;
        }

        return best != null ? best.position : null;
    }
}
