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
    Sprite LoadShipPreviewSprite(int skinIndex)
    {
        return LoadSpriteFromResourcesOrEditor(
            ShipCatalog.GetShipSkinResourcePath(skinIndex),
            ShipCatalog.GetShipSkinEditorResourcePath(skinIndex),
            ShipCatalog.GetShipSkinEditorFallbackPath(skinIndex));
    }

    Sprite LoadPilotPortraitSprite(PilotDefinition definition)
    {
        if (definition == null)
            definition = PilotCatalog.GetDefinition(PilotCatalog.JakeId);

        return LoadSpriteFromResourcesOrEditor(
            definition.PortraitResourcePath,
            definition.PortraitEditorResourcePath,
            definition.PortraitEditorFallbackPath);
    }

    Sprite GetGrayscalePilotPortraitSprite(PilotDefinition definition, Sprite source)
    {
        if (definition == null || source == null)
            return source;

        if (grayscalePilotPortraitCache.TryGetValue(definition.Id, out Sprite cached) && cached != null)
            return cached;

        Sprite grayscale = CreateGrayscaleSprite(source, definition.Id + "_locked");
        if (grayscale != null)
            grayscalePilotPortraitCache[definition.Id] = grayscale;

        return grayscale != null ? grayscale : source;
    }

    Sprite CreateGrayscaleSprite(Sprite source, string spriteName)
    {
        if (source == null || source.texture == null)
            return null;

        Rect rect = source.rect;
        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(source.texture.width, source.texture.height, 0, RenderTextureFormat.ARGB32);

        try
        {
            Graphics.Blit(source.texture, renderTexture);
            RenderTexture.active = renderTexture;

            Texture2D readable = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
            readable.ReadPixels(rect, 0, 0);
            readable.Apply();

            Color[] pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float gray = (pixels[i].r * 0.299f) + (pixels[i].g * 0.587f) + (pixels[i].b * 0.114f);
                pixels[i] = new Color(gray, gray, gray, pixels[i].a);
            }

            readable.SetPixels(pixels);
            readable.Apply();
            readable.name = spriteName;
            return Sprite.Create(readable, new Rect(0f, 0f, readable.width, readable.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to create grayscale pilot portrait: " + ex.Message);
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    void PrewarmProfileAssets()
    {
        UIRuntimeStyler.PrewarmRuntimeSprites();
        InventoryItemCatalog.PrewarmIcons();

        LoadStandaloneSprite("STARJACKERS_screen.png");
        LoadStandaloneSprite("hangar1_2D.png");
        LoadStandaloneSprite("hangar1_2D_przesuniety.png");
        LoadStandaloneSprite("PROJECTS_SCREEN.png");
        LoadStandaloneSprite("SUPPLY_TO_SURVIVE_PROJECT.png");
        LoadStandaloneSprite("SPACE_MAYHEM.png");
        LoadStandaloneSprite("omerta_screen.png");

        for (int skinIndex = 0; skinIndex <= ShipCatalog.MaxShipSkinIndex; skinIndex++)
            LoadShipPreviewSprite(skinIndex);

        for (int i = 0; i < PilotCatalog.AllDefinitions.Count; i++)
        {
            PilotDefinition definition = PilotCatalog.AllDefinitions[i];
            Sprite portrait = LoadPilotPortraitSprite(definition);
            GetGrayscalePilotPortraitSprite(definition, portrait);
        }

        for (int i = 0; i < TraderDefinitions.Length; i++)
            LoadTraderPortraitSprite(TraderDefinitions[i]);

        IReadOnlyList<ProjectDefinition> projects = ProjectCatalog.AllProjects;
        for (int i = 0; i < projects.Count; i++)
        {
            ProjectDefinition project = projects[i];
            if (project == null)
                continue;

            LoadSpriteFromResources(project.TileResourcePath);
            if (!string.IsNullOrWhiteSpace(project.BackgroundResourcePath))
                LoadSpriteFromResources(project.BackgroundResourcePath);
        }

        LoadSpriteFromResources("UI/icon_astrons_coin");
    }

    Sprite LoadStandaloneSprite(string fileName)
    {
        string resourcesPath = fileName switch
        {
            "STARJACKERS_screen.png" => "STARJACKERS_screen",
            "hangar1_2D.png" => "UI/hangar1_2D_profile",
            "hangar1_2D_przesuniety.png" => "UI/hangar1_2D_przesuniety_profile",
            "ship1.png" => "Visuals/Ships/ship1_resource",
            "ship2.png" => "Visuals/Ships/ship2_resource",
            "ship3.png" => "Visuals/Ships/ship3_resource",
            "ship4.png" => "ship4_resource",
            "PROJECTS_SCREEN.png" => "PROJECTS_SCREEN",
            "SUPPLY_TO_SURVIVE_PROJECT.png" => "SUPPLY_TO_SURVIVE_PROJECT",
            "SPACE_MAYHEM.png" => "SPACE_MAYHEM",
            "omerta_screen.png" => "omerta_screen",
            _ => null
        };

        string editorResourcePath = fileName switch
        {
            "STARJACKERS_screen.png" => "Assets/Resources/STARJACKERS_screen.png",
            "hangar1_2D.png" => "Assets/Resources/UI/hangar1_2D_profile.png",
            "hangar1_2D_przesuniety.png" => "Assets/Resources/UI/hangar1_2D_przesuniety_profile.png",
            "ship1.png" => "Assets/Resources/Visuals/Ships/ship1_resource.png",
            "ship2.png" => "Assets/Resources/Visuals/Ships/ship2_resource.png",
            "ship3.png" => "Assets/Resources/Visuals/Ships/ship3_resource.png",
            "ship4.png" => "Assets/Resources/ship4_resource.png",
            "PROJECTS_SCREEN.png" => "Assets/Resources/PROJECTS_SCREEN.png",
            "SUPPLY_TO_SURVIVE_PROJECT.png" => "Assets/Resources/SUPPLY_TO_SURVIVE_PROJECT.png",
            "SPACE_MAYHEM.png" => "Assets/Resources/SPACE_MAYHEM.png",
            "omerta_screen.png" => "Assets/Resources/omerta_screen.png",
            _ => null
        };

        string editorFallbackPath = string.IsNullOrWhiteSpace(fileName) ? null : "Assets/" + fileName;
        return LoadSpriteFromResourcesOrEditor(resourcesPath, editorResourcePath, editorFallbackPath);
    }

    Sprite LoadSpriteFromResourcesOrEditor(string resourcesPath, string editorPreferredPath, string editorFallbackPath = null)
    {
        Sprite sprite = LoadSpriteFromResources(resourcesPath);
        if (sprite != null)
            return sprite;

#if UNITY_EDITOR
        sprite = LoadEditorSprite(editorPreferredPath);
        if (sprite != null)
        {
            CacheLoadedSprite(resourcesPath, sprite);
            return sprite;
        }

        if (!string.IsNullOrWhiteSpace(editorFallbackPath))
        {
            sprite = LoadEditorSprite(editorFallbackPath);
            CacheLoadedSprite(resourcesPath, sprite);
            return sprite;
        }
#endif

        return null;
    }

    Sprite LoadSpriteFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return null;

        if (spriteCacheByResourcePath.TryGetValue(resourcesPath, out Sprite cachedSprite) && cachedSprite != null)
            return cachedSprite;
        if (missingSpriteResources.Contains(resourcesPath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
        {
            spriteCacheByResourcePath[resourcesPath] = sprite;
            return sprite;
        }

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        sprite = GetLargestSprite(sprites);
        if (sprite != null)
        {
            spriteCacheByResourcePath[resourcesPath] = sprite;
            return sprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
        {
            missingSpriteResources.Add(resourcesPath);
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        float pixelsPerUnit = Mathf.Max(100f, Mathf.Max(texture.width, texture.height));
        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        spriteCacheByResourcePath[resourcesPath] = sprite;
        return sprite;
    }

    void CacheLoadedSprite(string resourcesPath, Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath) || sprite == null)
            return;

        spriteCacheByResourcePath[resourcesPath] = sprite;
        missingSpriteResources.Remove(resourcesPath);
    }

    Sprite GetLargestSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            return null;

        Sprite best = null;
        float bestArea = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite candidate = sprites[i];
            if (candidate == null)
                continue;

            float area = candidate.rect.width * candidate.rect.height;
            if (best == null || area > bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    Sprite LoadEditorSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
            return sprite;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite loadedSprite)
                return loadedSprite;
        }

        return null;
    }
#endif
}
