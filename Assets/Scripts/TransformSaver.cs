using UnityEngine;
using System.Collections; // Добавьте эту строку в самый верх файла

public class TransformSaver : MonoBehaviour
{
    [System.Serializable]
    public class TransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public Vector3 pointer1Position;
        public Quaternion pointer1Rotation;
        public Vector3 pointer2Position;
        public Quaternion pointer2Rotation;
        public Vector3 savedDirection;
        public Vector3 movementDirection; // Добавляем сохранение направления
    }

    [Header("Required References")]
    public DickControlledCube cubeController;
    public Transform mainPointer;
    public Transform visualPointer;

    [Header("Reset Settings")]
    [Tooltip("Automatically disable movement when resetting")]
    public bool disableMovementOnReset = true;

    private TransformData savedTransforms = new TransformData();

    void Awake()
    {
        SaveCurrentTransforms();
    }

    [ContextMenu("Save Current Transforms")]
    public void SaveCurrentTransforms()
    {
        if (!CheckReferences()) return;

        savedTransforms.position = cubeController.transform.position;
        savedTransforms.rotation = cubeController.transform.rotation;
        savedTransforms.localScale = cubeController.transform.localScale;

        savedTransforms.pointer1Position = mainPointer.localPosition;
        savedTransforms.pointer1Rotation = mainPointer.localRotation;
        
        savedTransforms.pointer2Position = visualPointer.localPosition;
        savedTransforms.pointer2Rotation = visualPointer.localRotation;

        // Сохраняем текущее направление движения
        savedTransforms.movementDirection = mainPointer.forward;
    }

[ContextMenu("Reset Transforms")]
public void ResetTransforms()
{
    if (!CheckReferences()) return;

    // 1. Полное отключение
    cubeController.enabled = false;
    cubeController.DisableMovement();

    // 2. Получаем компоненты
    var rb = cubeController.GetComponent<Rigidbody>();
    var colliders = cubeController.GetComponents<Collider>();

    // 3. Жёсткий сброс
    rb.isKinematic = true;
    cubeController.transform.position = savedTransforms.position;
    cubeController.transform.rotation = savedTransforms.rotation;
    Physics.SyncTransforms(); // Принудительно обновляем физику
rb.WakeUp(); // Будим Rigidbody

    // 4. Перезагрузка коллайдеров
    foreach (var col in colliders)
    {
        col.enabled = false;
        col.enabled = true;
    }

    // 5. Восстановление указателей
    mainPointer.localPosition = savedTransforms.pointer1Position;
    mainPointer.localRotation = savedTransforms.pointer1Rotation;
    visualPointer.localPosition = savedTransforms.pointer2Position;
    visualPointer.localRotation = savedTransforms.pointer2Rotation;

    // 6. Обновление направления
    cubeController.ForceUpdateDirection(savedTransforms.movementDirection);

    // 7. Включение обратно
    rb.isKinematic = false;
    cubeController.enabled = true;
    cubeController.Revive(); // Вызовет ResetCollisionEffect внутри себя
    cubeController.movementEnabled = !disableMovementOnReset;

    Debug.Log("Complete reset with color reset");
}

private IEnumerator ReviveCube()
{
    yield return new WaitForFixedUpdate();
    
    var rb = cubeController.GetComponent<Rigidbody>();
    rb.WakeUp();
    
    // Принудительная проверка коллизий
    Physics.SyncTransforms();
    
    var colorChanger = cubeController.GetComponent<CollisionColorChanger>();
if (colorChanger != null) 
{
    colorChanger.ResetCollisionEffect();
}
    cubeController.SetRotatingState(false);
    cubeController.movementEnabled = !disableMovementOnReset;
}

    private bool CheckReferences()
    {
        if (cubeController == null || mainPointer == null || visualPointer == null)
        {
            Debug.LogError("Missing references in TransformSaver!", this);
            return false;
        }
        return true;
    }
}