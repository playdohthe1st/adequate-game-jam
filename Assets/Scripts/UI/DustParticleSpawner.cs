using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DustParticleSpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] Sprite dustSprite;
    [SerializeField] int gridColumns = 10;
    [SerializeField] int gridRows = 10;

    [Header("Parenting")]
    [SerializeField] Transform particleBehindAddons;
    [SerializeField] Transform particleAboveAddons;
    [SerializeField, Range(0f, 1f)] float behindAddonsRatio = 0.8f;

    [Header("Scale")]
    [SerializeField] float minScale = 0.1f;
    [SerializeField] float maxScale = 0.4f;

    [Header("Opacity")]
    [SerializeField] float fadeInDuration = 0.5f;
    [SerializeField] float minOpacity = 0.1f;
    [SerializeField] float maxOpacity = 0.45f;
    [SerializeField] Color dustTint = new Color(1f, 0.9f, 0.6f, 1f);

    [Header("Movement")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float moveSpeedVariance = 4f;
    [SerializeField] float noiseScale = 0.3f;
    [SerializeField] float directionChangeSpeed = 0.5f;

    [Header("Lifetime")]
    [SerializeField] float minLifetime = 8f;
    [SerializeField] float maxLifetime = 15f;
    [SerializeField] float fadeOutDuration = 1f;

    [Header("Spawning")]
    [SerializeField] float spawnInterval = 0.3f;
    [SerializeField] int poolSize = 40;
    [SerializeField] int maxActiveParticles = 30;

    [Header("Sorting")]
    [SerializeField] Canvas canvas;

    Queue<GameObject> particlePool = new Queue<GameObject>();
    List<GameObject> activeParticles = new List<GameObject>();

    float nextSpawnTime;
    float cellWidth;
    float cellHeight;
    RectTransform canvasRectTransform;

    void Start()
    {
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            canvasRectTransform = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRectTransform.sizeDelta;
            cellWidth = canvasSize.x / gridColumns;
            cellHeight = canvasSize.y / gridRows;
        }

        if (particleBehindAddons == null)
            particleBehindAddons = transform.Find("ParticleBehindAddons");

        if (particleAboveAddons == null)
            particleAboveAddons = transform.Find("ParticleAboveAddons");

        InitializePool();
        nextSpawnTime = Time.time + Random.Range(0f, spawnInterval);
    }

    void Update()
    {
        if (Time.time >= nextSpawnTime && activeParticles.Count < maxActiveParticles)
        {
            SpawnParticle();
            nextSpawnTime = Time.time + spawnInterval + Random.Range(-spawnInterval * 0.3f, spawnInterval * 0.3f);
        }

        CleanupInactiveParticles();
    }

    void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject particle = CreateNewParticle();
            particle.SetActive(false);
            particlePool.Enqueue(particle);
        }
    }

    GameObject CreateNewParticle()
    {
        GameObject particle = new GameObject("Dust");

        Transform parent = Random.value < behindAddonsRatio ? particleBehindAddons : particleAboveAddons;
        particle.transform.SetParent(parent, false);

        Image image = particle.AddComponent<Image>();
        image.sprite = dustSprite;
        image.raycastTarget = false;
        image.color = new Color(dustTint.r, dustTint.g, dustTint.b, 0f);

        RectTransform rectTransform = particle.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        particle.AddComponent<DustParticleBehavior>();

        return particle;
    }

    GameObject GetParticleFromPool()
    {
        GameObject particle;

        if (particlePool.Count > 0)
        {
            particle = particlePool.Dequeue();
        }
        else
        {
            particle = CreateNewParticle();
        }

        Transform parent = Random.value < behindAddonsRatio ? particleBehindAddons : particleAboveAddons;
        particle.transform.SetParent(parent, false);

        particle.SetActive(true);
        return particle;
    }

    public void ReturnParticleToPool(GameObject particle)
    {
        if (particle == null) return;
        particle.SetActive(false);
        particlePool.Enqueue(particle);
    }

    void SpawnParticle()
    {
        if (dustSprite == null) return;

        GameObject particle = GetParticleFromPool();

        int gridX = Random.Range(0, gridColumns);
        int gridY = Random.Range(0, gridRows);

        float randomX = Random.Range(gridX * cellWidth, (gridX + 1) * cellWidth);
        float randomY = Random.Range(gridY * cellHeight, (gridY + 1) * cellHeight);

        Vector2 canvasSize = canvasRectTransform.sizeDelta;
        Vector2 anchoredPos = new Vector2(
            randomX - canvasSize.x * 0.5f,
            randomY - canvasSize.y * 0.5f
        );

        RectTransform rectTransform = particle.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = anchoredPos;

        float scale = Random.Range(minScale, maxScale);
        rectTransform.localScale = Vector3.one * scale;

        float targetOpacity = Random.Range(minOpacity, maxOpacity);
        float lifetime = Random.Range(minLifetime, maxLifetime);
        float particleMoveSpeed = moveSpeed + Random.Range(-moveSpeedVariance, moveSpeedVariance);
        float noiseOffsetX = Random.Range(0f, 1000f);
        float noiseOffsetY = Random.Range(0f, 1000f);

        DustParticleBehavior behavior = particle.GetComponent<DustParticleBehavior>();
        behavior.Initialize(
            this,
            particleMoveSpeed,
            noiseScale,
            directionChangeSpeed,
            noiseOffsetX,
            noiseOffsetY,
            lifetime,
            fadeInDuration,
            fadeOutDuration,
            targetOpacity,
            dustTint
        );

        activeParticles.Add(particle);
    }

    void CleanupInactiveParticles()
    {
        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            if (activeParticles[i] == null || !activeParticles[i].activeInHierarchy)
                activeParticles.RemoveAt(i);
        }
    }
}

public class DustParticleBehavior : MonoBehaviour
{
    DustParticleSpawner spawner;
    float moveSpeed;
    float noiseScale;
    float directionChangeSpeed;
    float noiseOffsetX;
    float noiseOffsetY;
    float lifetime;
    float fadeInDuration;
    float fadeOutDuration;
    float targetOpacity;
    Color tintColor;

    float aliveTime;
    Image _image;
    RectTransform _rectTransform;

    Image ImageComponent
    {
        get { if (_image == null) _image = GetComponent<Image>(); return _image; }
    }

    RectTransform RectTransform
    {
        get { if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>(); return _rectTransform; }
    }

    public void Initialize(
        DustParticleSpawner particleSpawner,
        float speed, float noise, float changeSpeed,
        float offsetX, float offsetY, float life,
        float fadeIn, float fadeOut, float opacity, Color tint)
    {
        spawner = particleSpawner;
        moveSpeed = speed;
        noiseScale = noise;
        directionChangeSpeed = changeSpeed;
        noiseOffsetX = offsetX;
        noiseOffsetY = offsetY;
        lifetime = life;
        fadeInDuration = fadeIn;
        fadeOutDuration = fadeOut;
        targetOpacity = opacity;
        tintColor = tint;
        aliveTime = 0f;
        ImageComponent.color = new Color(tintColor.r, tintColor.g, tintColor.b, 0f);
    }

    void Update()
    {
        aliveTime += Time.deltaTime;

        float noiseTime = aliveTime * directionChangeSpeed;
        float noiseX = Mathf.PerlinNoise(noiseOffsetX + noiseTime, noiseOffsetX);
        float noiseY = Mathf.PerlinNoise(noiseOffsetY, noiseOffsetY + noiseTime);

        float dirX = (noiseX - 0.5f) * 2f;
        float dirY = (noiseY - 0.5f) * 2f;

        Vector2 movement = new Vector2(dirX, dirY) * noiseScale * moveSpeed * Time.deltaTime;
        RectTransform.anchoredPosition += movement;

        float currentAlpha;
        if (aliveTime < fadeInDuration)
            currentAlpha = Mathf.Lerp(0f, targetOpacity, aliveTime / fadeInDuration);
        else if (aliveTime >= lifetime - fadeOutDuration)
            currentAlpha = Mathf.Lerp(targetOpacity, 0f, (aliveTime - (lifetime - fadeOutDuration)) / fadeOutDuration);
        else
            currentAlpha = targetOpacity;

        ImageComponent.color = new Color(tintColor.r, tintColor.g, tintColor.b, currentAlpha);

        if (aliveTime >= lifetime)
        {
            if (spawner != null) spawner.ReturnParticleToPool(gameObject);
            else Destroy(gameObject);
        }
    }
}
