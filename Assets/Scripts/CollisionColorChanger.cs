using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody), typeof(MeshRenderer))]
public class CollisionColorChanger : MonoBehaviour
{
    [Header("Collision Settings")]
    public LayerMask collisionLayer;
    public Color collisionColor = Color.red;
    public float colorResetDelay = 2f;

    [Header("Debug")]
    public bool debugMode = true;
    public float debugRayDuration = 2f;

    private MeshRenderer meshRenderer;
    private Rigidbody rb;
    private Color originalColor;
    private bool isInCollisionState = false;
    private MaterialPropertyBlock materialPropertyBlock;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        rb = GetComponent<Rigidbody>();
        
        // Инициализация MaterialPropertyBlock для безопасного изменения цвета
        materialPropertyBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(materialPropertyBlock);
        originalColor = meshRenderer.material.color;

        if (debugMode)
            Debug.Log($"CollisionColorChanger initialized on {gameObject.name}. Original color: {originalColor}", this);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isInCollisionState && IsValidCollision(collision.gameObject))
        {
            ProcessCollision(collision);
        }
    }

    private bool IsValidCollision(GameObject otherObject)
    {
        bool layerMatches = (collisionLayer.value & (1 << otherObject.layer)) != 0;
        
        if (debugMode)
        {
            Debug.Log($"Collision with {otherObject.name} (Layer: {LayerMask.LayerToName(otherObject.layer)})");
            Debug.Log($"Layer match: {layerMatches}");
        }

        return layerMatches;
    }

    private void ProcessCollision(Collision collision)
    {
        if (debugMode)
        {
            Debug.Log($"Processing valid collision with {collision.gameObject.name}", collision.gameObject);
            Debug.DrawRay(collision.contacts[0].point, collision.contacts[0].normal * 2, Color.yellow, debugRayDuration);
        }

        // Остановка физического движения
        StopMovement();

        // Изменение цвета с использованием MaterialPropertyBlock
        SetCollisionColor(collisionColor);
        isInCollisionState = true;

        // Запуск таймера сброса
        StartCoroutine(ResetAfterDelay());
    }

    private void StopMovement()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        if (debugMode) Debug.Log("Movement stopped");
    }

    private void SetCollisionColor(Color color)
    {
        materialPropertyBlock.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(materialPropertyBlock);
        if (debugMode) Debug.Log($"Color changed to {color}");
    }

    private void ResetColor()
    {
        materialPropertyBlock.SetColor("_Color", originalColor);
        meshRenderer.SetPropertyBlock(materialPropertyBlock);
        isInCollisionState = false;
        if (debugMode) Debug.Log("Color reset to original");
    }

    IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(colorResetDelay);
        ResetCollisionEffect();
    }

    public void ResetCollisionEffect()
    {
        ResetColor();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        ResetColor();
    }

    public void TestColorChange()
    {
        if (!isInCollisionState)
        {
            SetCollisionColor(collisionColor);
            isInCollisionState = true;
            StartCoroutine(ResetAfterDelay());
        }
    }
}