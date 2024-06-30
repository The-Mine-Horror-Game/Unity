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
    
    // Parameters that change how the looking around feels. These are what we think are good numbers.
    [Header("Look Parameters")]
    [SerializeField, Range(1, 500)] private float lookSensX = 30.0f;
    [SerializeField, Range(1, 500)] private float lookSensY = 30.0f;
    [SerializeField, Range(1, 50)] private float leanSens = 30.0f;
    [SerializeField, Range(1, 10)] private float leanSensSlerp = 5.0f;
    [SerializeField, Range(-180, 180)] private float upperLookLimit = 80.0f;
    [SerializeField, Range(-180, 180)] private float lowerLookLimit = -80.0f;
    [SerializeField] private float xRotation; // The current rotations from the mouse, representing x and y rotation of the player. These are degree values.
    [SerializeField] private float yRotation;
    public bool isLeaning { get; private set; } = false;
    [SerializeField] private Transform playerObjOrientation; // this is technically unnecessary, but I got tired of writing orientationObj.transform.whatever so I made this.


    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // get mouse input
        var mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * lookSensX;
        var mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * lookSensY;

        yRotation += mouseX;
        xRotation -= mouseY;
        // Stops the camera from rotating more than the set limit up or down
        xRotation = Mathf.Clamp(xRotation, lowerLookLimit, upperLookLimit);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        playerObjOrientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }
}
