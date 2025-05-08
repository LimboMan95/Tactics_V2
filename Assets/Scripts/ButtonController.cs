// Скрипт ButtonController.cs
using UnityEngine;
using UnityEngine.Events;

public class ButtonController : MonoBehaviour
{
    [System.Serializable]
    public class ButtonEvents
    {
        public UnityEvent onClickEvents;
    }

    public ButtonEvents buttonActions;
}