using UnityEngine;

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

        // Временное отключение физики
        bool wasKinematic = cubeController.GetComponent<Rigidbody>().isKinematic;
        cubeController.GetComponent<Rigidbody>().isKinematic = true;

        // Восстановление трансформов
        cubeController.transform.position = savedTransforms.position;
        cubeController.transform.rotation = savedTransforms.rotation;
        cubeController.transform.localScale = savedTransforms.localScale;

        // Восстановление указателей
        mainPointer.localPosition = savedTransforms.pointer1Position;
        mainPointer.localRotation = savedTransforms.pointer1Rotation;
        visualPointer.localPosition = savedTransforms.pointer2Position;
        visualPointer.localRotation = savedTransforms.pointer2Rotation;

        // Принудительная синхронизация направления
        cubeController.currentDirection = savedTransforms.movementDirection;
        mainPointer.forward = savedTransforms.movementDirection;
        visualPointer.forward = savedTransforms.movementDirection;

        // Возвращаем исходное состояние физики
        cubeController.GetComponent<Rigidbody>().isKinematic = wasKinematic;

        if (disableMovementOnReset)
        {
            cubeController.movementEnabled = false;
            cubeController.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        }

        Debug.Log($"Direction after reset: {cubeController.currentDirection}");
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