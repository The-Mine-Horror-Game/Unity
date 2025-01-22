using UnityEngine;
using Quaternion = UnityEngine.Quaternion;

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
    [SerializeField] private float degreesToLean;
    [SerializeField] private float leanSens;
    private PlayerControls playerControls;
    [SerializeField] float timeElapsed;
    private void Start()
    {
        playerControls = new PlayerControls();
        playerControls.Player.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        var xRotation = playerObjOrientation.rotation.eulerAngles.x;
        var yRotation = playerObjOrientation.rotation.eulerAngles.y;
        var zRotation = playerObjOrientation.rotation.eulerAngles.z;
        if (playerControls.Player.Lean.IsInProgress())
        {
            timeElapsed += Time.deltaTime;
            // Leaning left
            if (playerControls.Player.Lean.ReadValue<float>() < 0)
            {
                /*
                 * Pseudo code:
                 * slerp between current position and a "leaning position" rotated to the left and transformed down and to the left
                 * Easiest way to do this is to separate 
                 */
                //transform.rotation = Quaternion.Euler(xRotation, yRotation, sensLean);
                //transform.rotation = Quaternion.Euler(xRotation, yRotation, transform.localRotation.z + sensLean);
                //orientation.rotation = Quaternion.Euler(0, yRotation, 0);
                Quaternion newRot = Quaternion.Euler(xRotation, yRotation, degreesToLean);
                playerObjOrientation.rotation = Quaternion.Slerp(transform.localRotation, newRot, Time.deltaTime * leanSens);
            }
            // Leaning right
            else
            {
                
            }
        }
        playerObjOrientation.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
    }
}
