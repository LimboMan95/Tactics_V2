using UnityEngine;
using System.Collections.Generic;

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
    }

    [Header("References")]
    public DickControlledCube cubeController;
    public Transform mainPointer;
    public Transform visualPointer;

    private TransformData initialTransforms = new TransformData();

    void Awake()
    {
        // Сохраняем все трансформы при запуске сцены
        SaveTransforms();
    }

    public void SaveTransforms()
    {
        if (cubeController == null || mainPointer == null || visualPointer == null)
        {
            Debug.LogError("Не все ссылки назначены в TransformSaver!");
            return;
        }

        // Сохраняем трансформы куба
        initialTransforms.position = cubeController.transform.position;
        initialTransforms.rotation = cubeController.transform.rotation;
        initialTransforms.localScale = cubeController.transform.localScale;

        // Сохраняем трансформы указателей
        initialTransforms.pointer1Position = mainPointer.localPosition;
        initialTransforms.pointer1Rotation = mainPointer.localRotation;
        
        initialTransforms.pointer2Position = visualPointer.localPosition;
        initialTransforms.pointer2Rotation = visualPointer.localRotation;

        Debug.Log("Все трансформы сохранены");
    }

    public void RestoreTransforms()
    {
        if (cubeController == null || mainPointer == null || visualPointer == null)
        {
            Debug.LogError("Не все ссылки назначены в TransformSaver!");
            return;
        }

        // Восстанавливаем куб
        cubeController.transform.position = initialTransforms.position;
        cubeController.transform.rotation = initialTransforms.rotation;
        cubeController.transform.localScale = initialTransforms.localScale;

        // Восстанавливаем указатели
        mainPointer.localPosition = initialTransforms.pointer1Position;
        mainPointer.localRotation = initialTransforms.pointer1Rotation;
        
        visualPointer.localPosition = initialTransforms.pointer2Position;
        visualPointer.localRotation = initialTransforms.pointer2Rotation;

        Debug.Log("Все трансформы восстановлены");
    }

    // Для вызова из других скриптов
    public TransformData GetSavedTransforms()
    {
        return initialTransforms;
    }
}