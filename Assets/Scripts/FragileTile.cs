using UnityEngine;

public class FragileTile : MonoBehaviour
{
    [Header("Настройки хрупкого тайла")]
    public float breakDelay = 1f;     // Через 1 секунду разрушится
    public float tileLength = 3f;     // Длина тайла в юнитах

    [Header("Визуал")]
    public Color warningColor = Color.red;
    public ParticleSystem breakParticles;

    private Renderer tileRenderer;
    private Collider tileCollider;
    private Color originalColor;
    private bool isBroken = false;
    private bool isCubeOnTile = false;
    private float cubeEnterTime;
    private DickControlledCube cube;

    void Start()
    {
        tileRenderer = GetComponent<Renderer>();
        tileCollider = GetComponent<Collider>();
        originalColor = tileRenderer.material.color;
    }

    void Update()
    {
        GridObjectMover editModeChecker = FindAnyObjectByType<GridObjectMover>();
        if (editModeChecker != null && editModeChecker.isInEditMode) return;
        
        if (isCubeOnTile && !isBroken && cube != null)
        {
            // Рассчитываем успеет ли куб проехать
            float cubeSpeed = cube.GetCurrentSpeed();
            float timeToCross = tileLength / cubeSpeed;
            
            // Мигаем красным если не успеет
            if (timeToCross > breakDelay)
            {
                float blinkSpeed = Mathf.PingPong(Time.time * 10f, 1f);
                tileRenderer.material.color = Color.Lerp(originalColor, warningColor, blinkSpeed);
            }

            // Проверяем разрушение
            if (Time.time - cubeEnterTime >= breakDelay)
            {
                BreakTilePermanently(); // ← Теперь навсегда!
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        GridObjectMover editModeChecker = FindAnyObjectByType<GridObjectMover>();
        if (editModeChecker != null && editModeChecker.isInEditMode) return;
        if (isBroken) return;
        
        cube = other.GetComponent<DickControlledCube>();
        if (cube != null)
        {
            isCubeOnTile = true;
            cubeEnterTime = Time.time;
            
            float cubeSpeed = cube.GetCurrentSpeed();
            float normalSpeed = cube.GetBaseSpeed();
            
            if (cubeSpeed > normalSpeed + 0.1f)
            {
                Debug.Log("Куб на ускорении! Успеет проехать! 🚀");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (isBroken) return;
        
        if (other.GetComponent<DickControlledCube>() != null)
        {
            isCubeOnTile = false;
            cube = null;
            tileRenderer.material.color = originalColor;
        }
    }

    // ЛОМАЕМ НАВСЕГДА (без восстановления)
    private void BreakTilePermanently()
    {
        isBroken = true;
        isCubeOnTile = false;
        
        // Выключаем коллайдер и рендер
        tileCollider.enabled = false;
        tileRenderer.enabled = false;
        
        // Эффекты разрушения
        if (breakParticles != null) breakParticles.Play();
        Debug.Log("💥 ТАЙЛ РАЗРУШЕН НАВСЕГДА! 💥");
        
        // НЕТ Invoke для восстановления!
    }

    // ВОССТАНАВЛИВАЕМ ТОЛЬКО ПРИНУДИТЕЛЬНО (из куба)
    public void ForceRespawn()
    {
        tileRenderer.enabled = true;
        tileCollider.enabled = true;
        tileRenderer.material.color = originalColor;
        isBroken = false;
        isCubeOnTile = false;
        cube = null;
        
        if (breakParticles != null && breakParticles.isPlaying)
            breakParticles.Stop();
        
        Debug.Log("Тайл восстановлен 🔄");
    }

    // Автоматически вызывается при уничтожении объекта
    void OnDestroy()
    {
        CancelAllProcesses();
    }

    public void CancelAllProcesses()
    {
        CancelInvoke();
        isCubeOnTile = false;
        cube = null;
        
        if (tileRenderer != null)
            tileRenderer.material.color = originalColor;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 0.1f, tileLength));
    }
}