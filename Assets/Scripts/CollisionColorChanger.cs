using UnityEngine;
using System.Collections;


[RequireComponent(typeof(Rigidbody), typeof(MeshRenderer))]
public class CollisionColorChanger : MonoBehaviour
{
    [Header("Settings")]
    public LayerMask collisionLayer;
    public Color collisionColor = Color.red;
    public float colorResetDelay = 2f;
    public float raycastDistance = 1f;

    [Header("Debug")]
    public bool debugMode = true;

    private MeshRenderer meshRenderer;
    private Rigidbody rb;
    private Color originalColor;
    private MaterialPropertyBlock propBlock;
    private bool isInCollisionState = false;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        rb = GetComponent<Rigidbody>();
        
        propBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propBlock);
        originalColor = meshRenderer.material.color;

        if (debugMode)
            Debug.Log("CollisionColorChanger initialized");
    }

    void FixedUpdate()
    {
        // Проверяем столкновения через raycast в FixedUpdate
        if (!isInCollisionState && Physics.Raycast(
            transform.position, 
            GetComponent<DickControlledCube>().currentDirection, 
            raycastDistance, 
            collisionLayer))
        {
            ProcessCollision();
        }
    }

    public void ProcessCollision()
    {
        if (isInCollisionState) return;

        if (debugMode)
            Debug.Log("Collision detected via raycast");

        StopMovement();
        SetCollisionColor(collisionColor);
        isInCollisionState = true;
        StartCoroutine(ResetAfterDelay());
    }

    private void StopMovement()
    {
        if (TryGetComponent<DickControlledCube>(out var controller))
        {
            controller.DisableMovement();
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    private void SetCollisionColor(Color color)
    {
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(propBlock);
    }

    private IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(colorResetDelay);
        ResetCollisionEffect();
    }

    public void ResetCollisionEffect()
    {
        SetCollisionColor(originalColor);
        isInCollisionState = false;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        ResetCollisionEffect();
    }

    // Для визуализации raycast в редакторе
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !debugMode) return;
        
        var cube = GetComponent<DickControlledCube>();
        if (cube != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, cube.currentDirection * raycastDistance);
        }
    }
}