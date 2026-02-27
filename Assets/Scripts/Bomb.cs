using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour, IResettable
{
    [Header("Настройки")]
    public Color bombColor = Color.magenta;
    public float explosionDelay = 0.8f;
    public float collisionDelay = 0.2f;
    public int explosionSize = 3;  // Размер квадрата взрыва (3 = 3x3 клетки)
    public float tileSize = 1f;     // Размер клетки
    
    [Header("Эффекты")]
    public ParticleSystem explosionEffect;
    public AudioClip explosionSound;
    
    [Header("Уничтожение")]
    public LayerMask destructibleLayer;
    public LayerMask playerLayer;
    
    public bool isActivated = false;
    private Renderer bombRenderer;
    private Color originalColor;
    private Coroutine countdownRoutine;
    
    // Для ресета
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    
    // Для визуализации радиуса
    private GameObject radiusObj;
    
    void Start()
    {
        bombRenderer = GetComponent<Renderer>();
        originalColor = bombRenderer.material.color;
        
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        CreateRadiusVisual();
    }
    
    void CreateRadiusVisual()
    {
        radiusObj = new GameObject("RadiusVisual");
        radiusObj.transform.SetParent(transform);
        radiusObj.transform.localPosition = Vector3.zero;
        radiusObj.transform.localRotation = Quaternion.identity;
        
        float boxSize = (explosionSize - 1) * tileSize;
        float halfSize = boxSize * 0.5f;
        
        // ЗАЛИВКА (КРАСНЫЙ ПОЛУПРОЗРАЧНЫЙ)
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(radiusObj.transform);
        fillObj.transform.localPosition = Vector3.zero;
        
        MeshFilter fillMesh = fillObj.AddComponent<MeshFilter>();
        MeshRenderer fillRenderer = fillObj.AddComponent<MeshRenderer>();
        
        Mesh fillMeshData = new Mesh();
        fillMeshData.vertices = new Vector3[]
        {
            new Vector3(-halfSize, 0.1f, -halfSize),
            new Vector3( halfSize, 0.1f, -halfSize),
            new Vector3( halfSize, 0.1f,  halfSize),
            new Vector3(-halfSize, 0.1f,  halfSize)
        };
        fillMeshData.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        fillMeshData.RecalculateNormals();
        fillMesh.mesh = fillMeshData;
        
        Material fillMat = new Material(Shader.Find("Sprites/Default"));
        fillMat.color = new Color(1, 0, 0, 0.3f);
        fillRenderer.material = fillMat;
        
        // КОНТУР (ЧЕРНЫЙ)
        LineRenderer line = radiusObj.AddComponent<LineRenderer>();
        line.startWidth = 0.1f;
        line.endWidth = 0.1f;
        line.loop = true;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.black;
        line.endColor = Color.black;
        line.positionCount = 5;
        line.useWorldSpace = false;
        
        line.SetPosition(0, new Vector3(-halfSize, 0.11f, -halfSize));
        line.SetPosition(1, new Vector3( halfSize, 0.11f, -halfSize));
        line.SetPosition(2, new Vector3( halfSize, 0.11f,  halfSize));
        line.SetPosition(3, new Vector3(-halfSize, 0.11f,  halfSize));
        line.SetPosition(4, new Vector3(-halfSize, 0.11f, -halfSize));
        
        radiusObj.SetActive(false);
    }
    
    public void ShowRadius(bool show)
    {
        if (radiusObj != null)
        {
            radiusObj.SetActive(show);
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        DickControlledCube cube = collision.collider.GetComponent<DickControlledCube>();
        if (cube != null && !isActivated)
        {
            Debug.Log("💥 Куб врезался! Быстрый взрыв!");
            StartCoroutine(QuickExplode());
        }
    }
    
    public void Activate()
    {
        if (isActivated) return;
        isActivated = true;
        
        countdownRoutine = StartCoroutine(ExplodeCountdown());
    }
    
    public IEnumerator QuickExplode()
    {
        isActivated = true;
        
        float timer = 0f;
        while (timer < collisionDelay)
        {
            float t = Mathf.PingPong(Time.time * 20f, 1f);
            bombRenderer.material.color = Color.Lerp(originalColor, Color.white, t);
            timer += Time.deltaTime;
            yield return null;
        }
        
        Explode();
    }
    
    IEnumerator ExplodeCountdown()
    {
        float timer = 0f;
        
        while (timer < explosionDelay)
        {
            float t = Mathf.PingPong(Time.time * 10f, 1f);
            bombRenderer.material.color = Color.Lerp(originalColor, Color.white, t);
            timer += Time.deltaTime;
            yield return null;
        }
        
        Explode();
    }
    
    void Explode()
    {
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        
        float boxSize = (explosionSize - 1) * tileSize;
        Vector3 boxCenter = transform.position;
        Vector3 halfExtents = new Vector3(boxSize * 0.5f, 1f, boxSize * 0.5f);
        
        // Уничтожаем ящики
        Collider[] hitObjects = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, destructibleLayer);
        foreach (Collider obj in hitObjects)
        {
            Crate crate = obj.GetComponent<Crate>();
            if (crate != null)
            {
                crate.gameObject.SetActive(false);
            }
            else
            {
                obj.gameObject.SetActive(false);
            }
        }
        
        // Проверяем игрока
        Collider[] players = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.identity, playerLayer);
        foreach (Collider player in players)
        {
            DickControlledCube cube = player.GetComponent<DickControlledCube>();
            if (cube != null)
            {
                cube.GameOver();
            }
        }
        
        gameObject.SetActive(false);
    }
    
    public void Highlight(bool highlight)
    {
        if (bombRenderer != null)
        {
            bombRenderer.material.color = highlight ? bombColor : originalColor;
        }
    }
    
    public void ResetObject()
    {
        isActivated = false;
        
        Vector3 pos = transform.position;
        pos.y = initialPosition.y;
        transform.position = pos;
        
        gameObject.SetActive(true);
        GetComponent<Renderer>().enabled = true;
        GetComponent<Collider>().enabled = true;
        
        StopAllCoroutines();
        
        if (bombRenderer != null)
            bombRenderer.material.color = originalColor;
        
        Debug.Log($"Bomb reset to active");
    }
    
    void OnDrawGizmosSelected()
    {
        float boxSize = (explosionSize - 1) * tileSize;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(boxSize, 0.1f, boxSize));
    }
}