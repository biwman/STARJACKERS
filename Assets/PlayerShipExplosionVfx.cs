using UnityEngine;

public sealed class PlayerShipExplosionVfx : MonoBehaviour
{
    const float Lifetime = 0.82f;
    const float EffectZ = -0.32f;

    static Material sharedMaterial;
    static Sprite smokeSprite;

    struct Spark
    {
        public LineRenderer line;
        public Vector3 direction;
        public float speed;
        public float length;
        public Color color;
    }

    struct Smoke
    {
        public SpriteRenderer renderer;
        public Vector3 direction;
        public float speed;
        public float startScale;
        public float endScale;
    }

    Spark[] sparks;
    Smoke[] smoke;
    LineRenderer flashRing;
    Vector3 center;
    float age;

    public static void Spawn(Vector3 position, SpriteRenderer referenceRenderer = null)
    {
        GameObject effect = new GameObject("PlayerShipExplosionVfx");
        PlayerShipExplosionVfx vfx = effect.AddComponent<PlayerShipExplosionVfx>();
        vfx.Initialize(position, referenceRenderer);
    }

    void Initialize(Vector3 position, SpriteRenderer referenceRenderer)
    {
        center = new Vector3(position.x, position.y, EffectZ);
        transform.position = center;

        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
        int sortingOrder = referenceRenderer != null ? referenceRenderer.sortingOrder + 170 : 1900;
        float radius = referenceRenderer != null
            ? Mathf.Clamp(Mathf.Max(referenceRenderer.bounds.extents.x, referenceRenderer.bounds.extents.y), 0.45f, 1.25f)
            : 0.72f;

        CreateFlashRing(sortingLayerId, sortingOrder, radius);
        CreateSparks(sortingLayerId, sortingOrder + 1, radius);
        CreateSmoke(sortingLayerId, sortingOrder - 1, radius);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Lifetime);

        UpdateFlashRing(t);
        UpdateSparks(t);
        UpdateSmoke(t);

        if (age >= Lifetime)
            Destroy(gameObject);
    }

    void CreateFlashRing(int sortingLayerId, int sortingOrder, float radius)
    {
        GameObject ringObject = new GameObject("ExplosionPressureRing");
        ringObject.transform.SetParent(transform, false);

        flashRing = ringObject.AddComponent<LineRenderer>();
        flashRing.useWorldSpace = false;
        flashRing.loop = true;
        flashRing.positionCount = 56;
        flashRing.widthMultiplier = 0.1f;
        flashRing.numCapVertices = 8;
        flashRing.numCornerVertices = 8;
        flashRing.alignment = LineAlignment.View;
        flashRing.material = GetMaterial();
        flashRing.sortingLayerID = sortingLayerId;
        flashRing.sortingOrder = sortingOrder;

        for (int i = 0; i < flashRing.positionCount; i++)
        {
            float a = (i / (float)flashRing.positionCount) * Mathf.PI * 2f;
            flashRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius * 0.22f, Mathf.Sin(a) * radius * 0.18f, 0f));
        }
    }

    void CreateSparks(int sortingLayerId, int sortingOrder, float radius)
    {
        sparks = new Spark[14];
        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = (i / (float)sparks.Length) * Mathf.PI * 2f + Random.Range(-0.18f, 0.18f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f).normalized;
            GameObject sparkObject = new GameObject("ExplosionSpark" + i);
            sparkObject.transform.SetParent(transform, false);

            LineRenderer line = sparkObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = Random.Range(0.025f, 0.055f);
            line.numCapVertices = 5;
            line.alignment = LineAlignment.View;
            line.material = GetMaterial();
            line.sortingLayerID = sortingLayerId;
            line.sortingOrder = sortingOrder;

            sparks[i] = new Spark
            {
                line = line,
                direction = direction,
                speed = Random.Range(radius * 2.2f, radius * 4.4f),
                length = Random.Range(radius * 0.24f, radius * 0.54f),
                color = Color.Lerp(new Color(1f, 0.45f, 0.12f, 1f), new Color(0.55f, 0.9f, 1f, 1f), Random.value * 0.35f)
            };
        }
    }

    void CreateSmoke(int sortingLayerId, int sortingOrder, float radius)
    {
        smoke = new Smoke[6];
        for (int i = 0; i < smoke.Length; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f).normalized;
            GameObject smokeObject = new GameObject("ExplosionSmoke" + i);
            smokeObject.transform.SetParent(transform, false);
            smokeObject.transform.localPosition = direction * Random.Range(0f, radius * 0.16f);

            SpriteRenderer renderer = smokeObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSmokeSprite();
            renderer.material = GetMaterial();
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            renderer.color = new Color(0.2f, 0.22f, 0.25f, 0.28f);

            smoke[i] = new Smoke
            {
                renderer = renderer,
                direction = direction,
                speed = Random.Range(radius * 0.35f, radius * 0.88f),
                startScale = Random.Range(radius * 0.18f, radius * 0.32f),
                endScale = Random.Range(radius * 0.48f, radius * 0.82f)
            };
        }
    }

    void UpdateFlashRing(float t)
    {
        if (flashRing == null)
            return;

        float radius = Mathf.Lerp(0.18f, 1.35f, Mathf.SmoothStep(0f, 1f, t));
        for (int i = 0; i < flashRing.positionCount; i++)
        {
            float a = (i / (float)flashRing.positionCount) * Mathf.PI * 2f;
            flashRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.74f, 0f));
        }

        Color color = new Color(0.9f, 0.95f, 1f, Mathf.Lerp(0.62f, 0f, t));
        flashRing.startColor = color;
        flashRing.endColor = color;
        flashRing.widthMultiplier = Mathf.Lerp(0.16f, 0.015f, t);
    }

    void UpdateSparks(float t)
    {
        if (sparks == null)
            return;

        float sparkT = Mathf.Clamp01(t / 0.72f);
        for (int i = 0; i < sparks.Length; i++)
        {
            LineRenderer line = sparks[i].line;
            if (line == null)
                continue;

            Vector3 head = center + sparks[i].direction * sparks[i].speed * sparkT * (1f - 0.28f * sparkT);
            Vector3 tail = head - sparks[i].direction * sparks[i].length * (1f - sparkT);
            line.SetPosition(0, tail);
            line.SetPosition(1, head);

            Color color = sparks[i].color;
            color.a = Mathf.Lerp(1f, 0f, sparkT);
            line.startColor = color;
            line.endColor = new Color(1f, 1f, 1f, color.a * 0.78f);
        }
    }

    void UpdateSmoke(float t)
    {
        if (smoke == null)
            return;

        float smokeT = Mathf.SmoothStep(0f, 1f, t);
        for (int i = 0; i < smoke.Length; i++)
        {
            SpriteRenderer renderer = smoke[i].renderer;
            if (renderer == null)
                continue;

            renderer.transform.localPosition += smoke[i].direction * smoke[i].speed * Time.deltaTime;
            float scale = Mathf.Lerp(smoke[i].startScale, smoke[i].endScale, smokeT);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.color = new Color(0.18f, 0.2f, 0.22f, Mathf.Lerp(0.34f, 0f, t));
        }
    }

    static Sprite GetSmokeSprite()
    {
        if (smokeSprite != null)
            return smokeSprite;

        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 centerPixel = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), centerPixel) / (size * 0.5f);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (3f - 2f * alpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        smokeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return smokeSprite;
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
            name = "PlayerShipExplosionVfxMaterial",
            color = Color.white
        };
        sharedMaterial.renderQueue = 3200;
        return sharedMaterial;
    }
}
