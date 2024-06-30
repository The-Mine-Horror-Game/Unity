using UnityEngine;

public class PlayerCam : MonoBehaviour
{
    /*
     * Function:
     * Handles transforming mouse input into camera rotation.
     *
     * Author(s):
     * Jacob Hubbard
     *
     * Created June 30, 2024.
     * Last modified June 30, 2024.
     */
    
    [SerializeField] private Transform playerObjOrientation; // this is technically unnecessary, but I got tired of writing orientationObj.transform.whatever so I made this.
    
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        playerObjOrientation.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
    }
}
