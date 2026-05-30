using Photon.Pun;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(PhotonView))]
public sealed class StartingShipEntryVfx : MonoBehaviourPun
{
    const float Duration = 2.35f;
    const float ApproachDistance = 18f;
    const float TrailLifetime = 1.25f;
    const float EffectZOffset = -0.08f;

    static Material sharedMaterial;

    Rigidbody2D body;
    SpriteRenderer shipRenderer;
    GameObject streakRoot;
    GameObject shockRingObject;
    TrailRenderer[] streaks;
    LineRenderer shockRing;
    Vector3 finalPosition;
    Vector3 startPosition;
    Quaternion finalRotation;
    Quaternion entryRotation;
    Vector2 approachDirection;
    float age;
    bool initialized;
    bool restoredBodySimulation;
    bool originalBodySimulated = true;

    public bool IsControllingMotion => initialized && photonView != null && photonView.IsMine && age < Duration;
    public bool IsEntryActive => initialized && age < Duration;

    void Start()
    {
        if (GetComponent<EnemyBot>() != null || GetComponent<AstronautSurvivor>() != null)
        {
            enabled = false;
            return;
        }

        body = GetComponent<Rigidbody2D>();
        shipRenderer = GetComponent<SpriteRenderer>();
        finalPosition = transform.position;
        finalRotation = transform.rotation;
        approachDirection = ResolveApproachDirection(finalPosition);
        startPosition = finalPosition + (Vector3)(approachDirection * ApproachDistance);
        entryRotation = GetRotationForDirection(-approachDirection);

        CreateStreaks();
        CreateShockRing();

        if (photonView != null && photonView.IsMine)
        {
            if (body != null)
            {
                originalBodySimulated = body.simulated;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }

            transform.position = startPosition;
            transform.rotation = entryRotation;
            if (body != null)
                body.rotation = entryRotation.eulerAngles.z;
        }

        initialized = true;
    }

    void Update()
    {
        if (!initialized)
            return;

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        if (photonView != null && photonView.IsMine && t < 1f)
        {
            transform.position = Vector3.Lerp(startPosition, finalPosition, eased);
            float rotationBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, t));
            transform.rotation = Quaternion.Slerp(entryRotation, finalRotation, rotationBlend);
        }

        UpdateStreaks(t);
        UpdateShockRing(t);

        if (t >= 1f)
            Finish();
    }

    void Finish()
    {
        if (!restoredBodySimulation && photonView != null && photonView.IsMine)
        {
            transform.position = finalPosition;
            transform.rotation = finalRotation;
            if (body != null)
            {
                body.simulated = originalBodySimulated;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            restoredBodySimulation = true;
        }

        if (streaks != null)
        {
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] != null)
                    streaks[i].emitting = false;
            }
        }

        if (age >= Duration + TrailLifetime)
        {
            if (streakRoot != null)
                Destroy(streakRoot);

            if (shockRingObject != null)
                Destroy(shockRingObject);

            Destroy(this);
        }
    }

    Vector2 ResolveApproachDirection(Vector3 destination)
    {
        Vector2 outward = new Vector2(destination.x, destination.y);
        if (outward.sqrMagnitude > 0.01f)
            return outward.normalized;

        int actorNumber = photonView != null && photonView.Owner != null ? photonView.Owner.ActorNumber : 1;
        float angle = (actorNumber * 93f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }

    Quaternion GetRotationForDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        direction.Normalize();
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        return Quaternion.Euler(0f, 0f, angle);
    }

    void CreateStreaks()
    {
        streakRoot = new GameObject("StartingHyperspaceStreaks");
        streakRoot.transform.SetParent(transform, false);
        streakRoot.transform.localPosition = Vector3.zero;

        int sortingLayerId = shipRenderer != null ? shipRenderer.sortingLayerID : 0;
        int sortingOrder = shipRenderer != null ? shipRenderer.sortingOrder + 18 : 1200;
        float shipWidth = shipRenderer != null ? shipRenderer.bounds.size.x : 1f;
        float shipHeight = shipRenderer != null ? shipRenderer.bounds.size.y : 1f;

        Vector3[] offsets =
        {
            new Vector3(-shipWidth * 0.42f, -shipHeight * 0.38f, EffectZOffset),
            new Vector3(-shipWidth * 0.22f, -shipHeight * 0.48f, EffectZOffset),
            new Vector3(0f, -shipHeight * 0.54f, EffectZOffset),
            new Vector3(shipWidth * 0.22f, -shipHeight * 0.48f, EffectZOffset),
            new Vector3(shipWidth * 0.42f, -shipHeight * 0.38f, EffectZOffset)
        };

        streaks = new TrailRenderer[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject streakObject = new GameObject("StartingStreak" + i);
            streakObject.transform.SetParent(streakRoot.transform, false);
            streakObject.transform.localPosition = offsets[i];

            TrailRenderer trail = streakObject.AddComponent<TrailRenderer>();
            trail.time = TrailLifetime;
            trail.minVertexDistance = 0.015f;
            trail.widthMultiplier = i == 2 ? 0.34f : 0.2f;
            trail.numCapVertices = 12;
            trail.numCornerVertices = 8;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.material = GetMaterial();
            trail.sortingLayerID = sortingLayerId;
            trail.sortingOrder = sortingOrder;
            trail.colorGradient = BuildStreakGradient();
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.42f, 0.55f),
                new Keyframe(1f, 0f));
            streaks[i] = trail;
        }
    }

    void CreateShockRing()
    {
        shockRingObject = new GameObject("StartingBrakeGlow");
        shockRingObject.transform.SetParent(transform, false);
        shockRingObject.transform.localPosition = new Vector3(0f, 0f, EffectZOffset);

        shockRing = shockRingObject.AddComponent<LineRenderer>();
        shockRing.useWorldSpace = false;
        shockRing.loop = true;
        shockRing.positionCount = 72;
        shockRing.widthMultiplier = 0.14f;
        shockRing.numCapVertices = 8;
        shockRing.numCornerVertices = 8;
        shockRing.alignment = LineAlignment.View;
        shockRing.material = GetMaterial();
        if (shipRenderer != null)
        {
            shockRing.sortingLayerID = shipRenderer.sortingLayerID;
            shockRing.sortingOrder = shipRenderer.sortingOrder + 32;
        }
    }

    void UpdateStreaks(float t)
    {
        if (streaks == null)
            return;

        float intensity = 1f - Mathf.SmoothStep(0.58f, 1f, t);
        for (int i = 0; i < streaks.Length; i++)
        {
            TrailRenderer trail = streaks[i];
            if (trail == null)
                continue;

            trail.emitting = t < 0.9f;
            trail.time = Mathf.Lerp(0.34f, TrailLifetime, intensity);
            trail.widthMultiplier = Mathf.Lerp(0.045f, i == 2 ? 0.38f : 0.24f, intensity);
        }
    }

    void UpdateShockRing(float t)
    {
        if (shockRing == null)
            return;

        float ringT = Mathf.InverseLerp(0.62f, 1f, t);
        float alpha = Mathf.Sin(Mathf.Clamp01(ringT) * Mathf.PI);
        float radius = Mathf.Lerp(0.24f, 1.75f, Mathf.SmoothStep(0f, 1f, ringT));
        for (int i = 0; i < shockRing.positionCount; i++)
        {
            float a = (i / (float)shockRing.positionCount) * Mathf.PI * 2f;
            shockRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.68f, 0f));
        }

        Color color = new Color(0.55f, 0.95f, 1f, alpha * 0.96f);
        shockRing.startColor = color;
        shockRing.endColor = color;
        shockRing.widthMultiplier = Mathf.Lerp(0.28f, 0.035f, ringT);
    }

    Gradient BuildStreakGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.9f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.18f, 0.72f, 1f), 0.28f),
                new GradientColorKey(new Color(0.18f, 0.22f, 0.95f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.78f, 0.38f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    static Material GetMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        sharedMaterial = new Material(shader)
        {
            name = "StartingShipEntryVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3200;
        return sharedMaterial;
    }
}
