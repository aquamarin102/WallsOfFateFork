using UnityEngine;

public class CursorController : MonoBehaviour
{
    private void Start()
    {
        ApplyCursorState();
    }

    private void Update()
    {
        ApplyCursorState();
    }

    private static void ApplyCursorState()
    {
        // Игра активно использует мышь для движения и взаимодействий,
        // поэтому курсор должен оставаться видимым и свободным.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
