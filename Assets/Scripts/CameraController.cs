using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 15, -10);
    public Transform planningView;
    public Transform gameplayView;
     public void SwitchToPlanningView()
    {
        if (planningView != null)
        {
            transform.position = planningView.position;
            transform.rotation = planningView.rotation;
        }
    }

    public void SwitchToGameplayView()
    {
        if (gameplayView != null)
        {
            transform.position = gameplayView.position;
            transform.rotation = gameplayView.rotation;
        }
    }
    void LateUpdate()
    {
        transform.position = target.position + offset;
    }
}