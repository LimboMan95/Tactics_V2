using UnityEngine;
using System.Collections;

public class Bomb : MonoBehaviour
{
    [Header("Настройки")]
    public Color bombColor = Color.magenta;
    public float explosionDelay = 0.8f;
    public float explosionRadius = 1.5f;
    
    [Header("Эффекты")]
    public ParticleSystem explosionEffect;
    public AudioClip explosionSound;
    
    [Header("Уничтожение")]
    public LayerMask destructibleLayer;
    public LayerMask playerLayer;
    
    private bool isActivated = false;
    private Renderer bombRenderer;
    private Color originalColor;
    private Coroutine countdownRoutine;
    
    void Start()
    {
        bombRenderer = GetComponent<Renderer>();
        originalColor = bombRenderer.material.color;
    }
    
    public void Activate()
    {
        if (isActivated) return;
        isActivated = true;
        
        countdownRoutine = StartCoroutine(ExplodeCountdown());
    }
    
    IEnumerator ExplodeCountdown()
    {
        float timer = 0f;
        
        // Мигаем
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
        // Эффекты
        if (explosionEffect != null)
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        
        // Уничтожаем ящики
        Collider[] hitObjects = Physics.OverlapSphere(transform.position, explosionRadius, destructibleLayer);
        foreach (Collider obj in hitObjects)
        {
            Destroy(obj.gameObject);
        }
        
        // Проверяем игрока
        Collider[] players = Physics.OverlapSphere(transform.position, explosionRadius, playerLayer);
        foreach (Collider player in players)
        {
            DickControlledCube cube = player.GetComponent<DickControlledCube>();
            if (cube != null)
            {
                cube.GameOver();
            }
        }
        
        // Уничтожаем бомбу
        Destroy(gameObject);
    }
    
    public void Highlight(bool highlight)
    {
        if (bombRenderer != null)
        {
            bombRenderer.material.color = highlight ? bombColor : originalColor;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}