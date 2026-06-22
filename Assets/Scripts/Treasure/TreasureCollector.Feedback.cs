using Photon.Pun;
using UnityEngine;

public partial class TreasureCollector
{
    void StartCollectibleFeedback(PhotonView targetView)
    {
        photonView.RPC(nameof(StartDrillingLoopSfx), RpcTarget.All);
        if (targetView != null)
        {
            photonView.RPC(nameof(SetBeamTargetRpc), RpcTarget.All, targetView.ViewID, true);
        }
    }

    void StopCollectibleFeedback()
    {
        photonView.RPC(nameof(StopDrillingLoopSfx), RpcTarget.All);
        photonView.RPC(nameof(ClearBeamTargetRpc), RpcTarget.All);
    }

    void StartArtifactExamineFeedback(ArtifactAsteroid artifact)
    {
        if (artifact == null)
            return;

        photonView.RPC(nameof(SetArtifactExamineBeamTargetRpc), RpcTarget.All, artifact.StableId, true);
    }

    void StopArtifactExamineFeedback()
    {
        photonView.RPC(nameof(ClearArtifactExamineBeamTargetRpc), RpcTarget.All);
    }


    void SetupBeam()
    {
        Transform existing = transform.Find("TreasureBeam");
        GameObject beamObject = existing != null ? existing.gameObject : new GameObject("TreasureBeam");
        beamObject.transform.SetParent(transform, false);

        collectionBeam = beamObject.GetComponent<LineRenderer>();
        if (collectionBeam == null)
        {
            collectionBeam = beamObject.AddComponent<LineRenderer>();
        }

        collectionBeam.useWorldSpace = true;
        collectionBeam.alignment = LineAlignment.View;
        collectionBeam.positionCount = BeamPointCount;
        collectionBeam.widthMultiplier = BeamWidth;
        collectionBeam.startWidth = BeamWidth;
        collectionBeam.endWidth = BeamWidth * 0.55f;
        collectionBeam.numCapVertices = 12;
        collectionBeam.numCornerVertices = 10;
        collectionBeam.material = new Material(Shader.Find("Sprites/Default"));
        collectionBeam.colorGradient = BuildCollectionBeamGradient(1f);
        collectionBeam.widthCurve = BuildCollectionBeamWidthCurve();
        collectionBeam.textureMode = LineTextureMode.Stretch;

        SpriteRenderer referenceRenderer = GetComponent<SpriteRenderer>();
        if (referenceRenderer != null)
        {
            collectionBeam.sortingLayerID = referenceRenderer.sortingLayerID;
            collectionBeam.sortingOrder = referenceRenderer.sortingOrder + 36;
        }
        else
        {
            collectionBeam.sortingLayerName = "Default";
            collectionBeam.sortingOrder = 50;
        }

        collectionBeam.enabled = false;
    }

    void SetupArtifactExamineBeam()
    {
        Transform existing = transform.Find("ArtifactExamineBeam");
        GameObject beamObject = existing != null ? existing.gameObject : new GameObject("ArtifactExamineBeam");
        beamObject.transform.SetParent(transform, false);

        artifactExamineBeam = beamObject.GetComponent<LineRenderer>();
        if (artifactExamineBeam == null)
            artifactExamineBeam = beamObject.AddComponent<LineRenderer>();

        artifactExamineBeam.useWorldSpace = true;
        artifactExamineBeam.alignment = LineAlignment.View;
        artifactExamineBeam.positionCount = BeamPointCount;
        artifactExamineBeam.widthMultiplier = ArtifactBeamWidth;
        artifactExamineBeam.startWidth = ArtifactBeamWidth;
        artifactExamineBeam.endWidth = ArtifactBeamWidth * 0.62f;
        artifactExamineBeam.numCapVertices = 12;
        artifactExamineBeam.numCornerVertices = 10;
        artifactExamineBeam.material = new Material(Shader.Find("Sprites/Default"));
        artifactExamineBeam.colorGradient = BuildArtifactBeamGradient(1f);
        artifactExamineBeam.widthCurve = BuildCollectionBeamWidthCurve();
        artifactExamineBeam.textureMode = LineTextureMode.Stretch;

        SpriteRenderer referenceRenderer = GetComponent<SpriteRenderer>();
        if (referenceRenderer != null)
        {
            artifactExamineBeam.sortingLayerID = referenceRenderer.sortingLayerID;
            artifactExamineBeam.sortingOrder = referenceRenderer.sortingOrder + 38;
        }
        else
        {
            artifactExamineBeam.sortingLayerName = "Default";
            artifactExamineBeam.sortingOrder = 52;
        }

        artifactExamineBeam.enabled = false;
    }

    void UpdateCollectionBeam()
    {
        if (collectionBeam == null)
            return;

        bool shouldShow = beamActive && (currentTreasure != null || currentWreck != null || currentDroppedCargo != null);
        if (shouldShow && photonView.IsMine)
        {
            bool useKeepAliveRange = isCollecting;
            shouldShow = currentTreasure != null
                ? IsTreasureInCollectRange(currentTreasure, useKeepAliveRange)
                : currentWreck != null
                    ? IsWreckInCollectRange(currentWreck, useKeepAliveRange)
                    : IsDroppedCargoInCollectRange(currentDroppedCargo, useKeepAliveRange);
        }

        collectionBeam.enabled = shouldShow;

        if (!shouldShow)
            return;

        Vector2 start = GetShipTipPosition();
        Vector2 end = GetCollectibleBeamTarget(start);
        Vector2 delta = end - start;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : (Vector2)transform.up;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float pulse = Mathf.Sin(Time.time * 14f) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(0.72f, 1f, pulse);
        collectionBeam.colorGradient = BuildCollectionBeamGradient(alpha);
        collectionBeam.widthMultiplier = Mathf.Lerp(BeamWidth * 0.72f, BeamWidth * 1.3f, pulse);

        for (int i = 0; i < collectionBeam.positionCount; i++)
        {
            float t = i / (float)(collectionBeam.positionCount - 1);
            Vector2 point = Vector2.Lerp(start, end, t);
            float taper = Mathf.Sin(t * Mathf.PI);
            float waveA = Mathf.Sin((t * Mathf.PI * 5f) + Time.time * BeamJitterFrequency);
            float waveB = Mathf.Sin((t * Mathf.PI * 11f) - Time.time * 13f) * 0.45f;
            float jitter = (waveA + waveB) * BeamJitterAmplitude * taper;
            point += perpendicular * jitter;
            collectionBeam.SetPosition(i, new Vector3(point.x, point.y, BeamZOffset));
        }
    }

    void UpdateArtifactExamineBeam()
    {
        if (artifactExamineBeam == null)
            return;

        bool shouldShow = artifactBeamActive && currentArtifactAsteroid != null;
        if (shouldShow && photonView.IsMine)
            shouldShow = IsArtifactStillUsable(currentArtifactAsteroid);

        artifactExamineBeam.enabled = shouldShow;
        if (!shouldShow)
            return;

        Vector2 start = GetShipTipPosition();
        Vector2 end = currentArtifactAsteroid != null ? (Vector2)currentArtifactAsteroid.BeamTarget : start;
        Vector2 delta = end - start;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : (Vector2)transform.up;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float pulse = Mathf.Sin(Time.time * 18f) * 0.5f + 0.5f;
        float alpha = Mathf.Lerp(0.62f, 1f, pulse);
        artifactExamineBeam.colorGradient = BuildArtifactBeamGradient(alpha);
        artifactExamineBeam.widthMultiplier = Mathf.Lerp(ArtifactBeamWidth * 0.65f, ArtifactBeamWidth * 1.45f, pulse);

        for (int i = 0; i < artifactExamineBeam.positionCount; i++)
        {
            float t = i / (float)(artifactExamineBeam.positionCount - 1);
            Vector2 point = Vector2.Lerp(start, end, t);
            float taper = Mathf.Sin(t * Mathf.PI);
            float waveA = Mathf.Sin((t * Mathf.PI * 7f) + Time.time * 20f);
            float waveB = Mathf.Sin((t * Mathf.PI * 13f) - Time.time * 16f) * 0.5f;
            float jitter = (waveA + waveB) * BeamJitterAmplitude * 0.78f * taper;
            point += perpendicular * jitter;
            artifactExamineBeam.SetPosition(i, new Vector3(point.x, point.y, BeamZOffset - 0.03f));
        }
    }

    Gradient BuildCollectionBeamGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.96f, 1f, 0.86f), 0f),
                new GradientColorKey(new Color(0.28f, 1f, 0.66f), 0.38f),
                new GradientColorKey(new Color(0.1f, 0.74f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f * alpha, 0f),
                new GradientAlphaKey(0.72f * alpha, 0.55f),
                new GradientAlphaKey(0.18f * alpha, 1f)
            });
        return gradient;
    }

    Gradient BuildArtifactBeamGradient(float alpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.08f, 0.46f, 1f), 0f),
                new GradientColorKey(new Color(0.48f, 0.92f, 1f), 0.46f),
                new GradientColorKey(new Color(0.12f, 0.18f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.86f * alpha, 0f),
                new GradientAlphaKey(1f * alpha, 0.5f),
                new GradientAlphaKey(0.24f * alpha, 1f)
            });
        return gradient;
    }

    AnimationCurve BuildCollectionBeamWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.62f),
            new Keyframe(0.18f, 1.2f),
            new Keyframe(0.58f, 0.82f),
            new Keyframe(1f, 0.22f));
    }

    Vector2 GetCollectibleBeamTarget(Vector2 start)
    {
        Collider2D collider = currentTreasure != null
            ? currentTreasure.GetComponent<Collider2D>()
            : currentWreck != null ? currentWreck.GetComponent<Collider2D>() : currentDroppedCargo != null ? currentDroppedCargo.GetComponent<Collider2D>() : null;

        Vector2 fallbackPosition = currentTreasure != null
            ? currentTreasure.transform.position
            : currentWreck != null ? currentWreck.transform.position : currentDroppedCargo != null ? currentDroppedCargo.transform.position : start;

        if (collider != null)
        {
            return collider.ClosestPoint(start);
        }

        return fallbackPosition;
    }

    void SetBeamEnabled(bool enabled)
    {
        beamActive = enabled;
        if (collectionBeam != null)
            collectionBeam.enabled = enabled;
    }

    void SetArtifactBeamEnabled(bool enabled)
    {
        artifactBeamActive = enabled;
        if (artifactExamineBeam != null)
            artifactExamineBeam.enabled = enabled;
    }


    [PunRPC]
    void StartDrillingLoopSfx()
    {
        if (drillingAudioSource == null)
        {
            SetupDrillingAudio();
        }

        if (drillingAudioSource == null || drillingAudioSource.clip == null)
            return;

        drillingAudioSource.loop = true;
        if (!drillingAudioSource.isPlaying)
            drillingAudioSource.Play();
    }

    [PunRPC]
    void StopDrillingLoopSfx()
    {
        StopLocalDrillingLoop();
    }

    void SetupDrillingAudio()
    {
        AudioClip clip = AudioManager.Instance.DrillingClip;
        if (clip == null)
            return;

        Transform existing = transform.Find("DrillingAudioSource");
        GameObject audioObject = existing != null ? existing.gameObject : new GameObject("DrillingAudioSource");
        audioObject.transform.SetParent(transform, false);

        drillingAudioSource = audioObject.GetComponent<AudioSource>();
        if (drillingAudioSource == null)
        {
            drillingAudioSource = audioObject.AddComponent<AudioSource>();
        }

        drillingAudioSource.clip = clip;
        drillingAudioSource.loop = true;
        drillingAudioSource.playOnAwake = false;
        drillingAudioSource.volume = 0.455f;
        AudioManager.Instance.ConfigureSpatialSource(drillingAudioSource, 0.455f);
        drillingAudioSource.loop = true;
        drillingAudioSource.playOnAwake = false;
    }

    void StopLocalDrillingLoop()
    {
        if (drillingAudioSource != null && drillingAudioSource.isPlaying)
            drillingAudioSource.Stop();
    }


    [PunRPC]
    void SetBeamTargetRpc(int targetViewId, bool active)
    {
        PhotonView targetView = PhotonView.Find(targetViewId);
        currentTreasure = targetView != null ? targetView.GetComponent<Treasure>() : null;
        currentWreck = currentTreasure == null && targetView != null ? targetView.GetComponent<ShipWreck>() : null;
        currentDroppedCargo = currentTreasure == null && currentWreck == null && targetView != null ? targetView.GetComponent<DroppedCargoCrate>() : null;
        SetBeamEnabled(active && (currentTreasure != null || currentWreck != null || currentDroppedCargo != null));
    }

    [PunRPC]
    void ClearBeamTargetRpc()
    {
        SetBeamEnabled(false);
        currentTreasure = null;
        currentWreck = null;
        currentDroppedCargo = null;
    }

    [PunRPC]
    void SetArtifactExamineBeamTargetRpc(string artifactId, bool active)
    {
        currentArtifactAsteroid = ArtifactAsteroid.Find(artifactId);
        SetArtifactBeamEnabled(active && currentArtifactAsteroid != null);
    }

    [PunRPC]
    void ClearArtifactExamineBeamTargetRpc()
    {
        SetArtifactBeamEnabled(false);
        if (!isExaminingArtifact)
            currentArtifactAsteroid = null;
    }

}
