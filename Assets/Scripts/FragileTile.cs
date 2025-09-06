using UnityEngine;

public class FragileTile : MonoBehaviour
{
    [Header("Настройки хрупкого тайла")]
    public float breakDelay = 1f;     // Через 1 секунду разрушится
    public float respawnTime = 3f;    // Через 3 секунды возродится
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
         // Не обрабатываем логику в режиме редактирования
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
                BreakTile();
            }
        }
    }

    void OnTriggerEnter(Collider other)
{
    // Проверяем режим редактирования
    GridObjectMover editModeChecker = FindAnyObjectByType<GridObjectMover>();
    if (editModeChecker != null && editModeChecker.isInEditMode) return;
    if (isBroken) return;
    
    cube = other.GetComponent<DickControlledCube>();
    if (cube != null)
    {
        // ВАЖНО: Устанавливаем флаги и время входа!
        isCubeOnTile = true;
        cubeEnterTime = Time.time;
        
        // Дополнительная логика проверки скорости
        float cubeSpeed = cube.GetCurrentSpeed();
        float normalSpeed = cube.GetBaseSpeed();
        
        if (cubeSpeed > normalSpeed + 0.1f)
        {
            Debug.Log("Куб на ускорении! Успеет проехать! 🚀");
        }
        else
        {
            Debug.Log("Куб на обычной скорости... Рискует! 😰");
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
            Debug.Log("Куб свалил с тайла");
        }
    }

    void OnDisable()
{
    // Отменяем все процессы при выключении объекта
    CancelAllProcesses();
}

    public void ForceRespawn()
{
    // Отменяем все запланированные вызовы
    CancelInvoke();
    
    // Немедленно восстанавливаем тайл
    tileRenderer.enabled = true;
    tileCollider.enabled = true;
    tileRenderer.material.color = originalColor;
    isBroken = false;
    isCubeOnTile = false;
    cube = null;
    
    // Останавливаем частицы если они играют
    if (breakParticles != null && breakParticles.isPlaying)
        breakParticles.Stop();
    
    Debug.Log("Тайл принудительно восстановлен 🔄");
}

public void CancelAllProcesses()
{
    CancelInvoke(); // Отменяем все Invoke вызовы
    isCubeOnTile = false;
    cube = null;
    
    // Сбрасываем цвет на оригинальный
    if (tileRenderer != null)
        tileRenderer.material.color = originalColor;
}

    private void BreakTile()
    {
        isBroken = true;
        isCubeOnTile = false;
        
        // Выключаем коллайдер и рендер
        tileCollider.enabled = false;
        tileRenderer.enabled = false;
        
        // Эффекты разрушения
        if (breakParticles != null) breakParticles.Play();
        Debug.Log("💥 ТАЙЛ РУХНУЛ! 💥");
        
        // Восстанавливаем через время
        Invoke("RespawnTile", respawnTime);
    }

    private void RespawnTile()
    {
        tileRenderer.enabled = true;
        tileCollider.enabled = true;
        tileRenderer.material.color = originalColor;
        isBroken = false;
        
        Debug.Log("Тайл восстановился 🔄");
    }

    // Для визуализации в редакторе
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 0.1f, tileLength));
    }
}