using UnityEngine;

public class Crate : MonoBehaviour, IResettable
{
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    
    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }
    
    public void ResetObject()
{
    gameObject.SetActive(true);
    
    transform.position = initialPosition;
    transform.rotation = initialRotation;
    
    GetComponent<Renderer>().enabled = true;
    GetComponent<Collider>().enabled = true;
    
    Debug.Log($"Crate reset to {initialPosition}");
}

public void DestroyCrate()
{
    gameObject.SetActive(false); // Вместо Destroy
}
}