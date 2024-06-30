using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    
    /*
     * Function:
     * Handles player movement, including walking, crouching, and sprinting
     *
     * Author(s):
     * Jacob Hubbard
     *
     * Created June 6, 2024.
     * Last Modified June 6, 2024.
     */
    
    // If they can sprint, changes during runtime
    [Header("Functional Options")]
    [SerializeField] private bool canSprint = true;


    // These determine how fast the player moves, and some other required variables
    [Header("Movement Parameters")] 
    [SerializeField] private float moveSpeed;
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintSpeed = 6.0f;
    [SerializeField] private float gravity = 30.0f;
    [SerializeField] private Vector3 moveDirection;
    [SerializeField] private Rigidbody playerRigidBody;
    [SerializeField] private float jumpForce;
    [SerializeField] private float jumpCooldown;
    [SerializeField] private float airMultiplier;
    [SerializeField] private bool readyToJump = true;
    [SerializeField] private MovementState currentState;
    [SerializeField] private Transform orientationObj;

    private enum MovementState
    {
        walking,
        sprinting,
        crouching,
        air
    }

    [Header("Grounded Parameters")] 
    [SerializeField] private float groundDrag;
    [SerializeField] private float playerHeight;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private bool isGrounded;

    [Header("Crouch Parameters")] 
    [SerializeField] private float crouchSpeed;
    [SerializeField] private float crouchYScale;
    [SerializeField] private float startYScale;
    // This was a proposed feature to have a toggle crouch, it's still in the air whether we'll implement it. This bool would enable or disable the feature.
    //public bool toggleCrouch = false;
    

    // This is less self-explanatory. PlayerControls is the input system we have for all the input action maps in the game. It needs to be initialised in void Awake to function properly.
    // Read up on the Unity Input System to learn more.
    private PlayerControls playerControls;
    
    // These are 
    private Vector2 currentInput;
    
    private void Start()
    {
        playerControls = new PlayerControls();

        playerControls.Player.Enable();

        playerRigidBody.freezeRotation = true;
        startYScale = transform.localScale.y;
    }

    private void Update()
    {
        // Check whether the player is grounded
        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, whatIsGround);
        HandleMovementInput();
        HandleDrag();
        SpeedControl();
        HandleState();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    // Handles keyboard inputs
    // ReSharper disable Unity.PerformanceAnalysis
    private void HandleMovementInput()
    {
        // Converts wasd inputs into a vector2 of the inputs. Very cool!
        currentInput = playerControls.Player.Movement.ReadValue<Vector2>();

        // ReSharper disable once InvertIf
        if (playerControls.Player.Jump.IsInProgress() && readyToJump && isGrounded)
        {
            readyToJump = false;

            Jump();
            
            Invoke(nameof(ResetJump), jumpCooldown);
        }
        
        // Start crouch
        if (playerControls.Player.Crouch.WasPressedThisFrame())
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            playerRigidBody.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }
        // Stop crouch
        else if (playerControls.Player.Crouch.WasReleasedThisFrame())
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    private void MovePlayer()
    {
        moveDirection = orientationObj.forward * currentInput.y + orientationObj.right * currentInput.x;

        switch (isGrounded)
        {
            case true:
                playerRigidBody.AddForce(moveDirection.normalized * (moveSpeed * 10f), ForceMode.Force);
                break;
            case false:
                playerRigidBody.AddForce(moveDirection.normalized * (moveSpeed * (airMultiplier * 10f)), ForceMode.Force);
                break;
        }
    }

    private void SpeedControl()
    {
        // The velocity of the player without the y component
        var flatVel = new Vector3(playerRigidBody.velocity.x, 0f, playerRigidBody.velocity.z);
        
        // Limit velocity if needed
        
        // ReSharper disable once InvertIf
        if (flatVel.magnitude > moveSpeed)
        {
            var limitedVel = flatVel.normalized * moveSpeed;
            playerRigidBody.velocity = new Vector3(limitedVel.x, playerRigidBody.velocity.y, limitedVel.z);
        }
    }

    private void HandleDrag()
    {
        if (isGrounded)
        {
            playerRigidBody.drag = groundDrag;
        }
        else
        {
            playerRigidBody.drag = 0;
        }
    }

    private void HandleState()
    {
        switch (isGrounded)
        {
            case true when playerControls.Player.Crouch.IsInProgress():
                currentState = MovementState.crouching;
                moveSpeed = crouchSpeed;
                break;
            case true when playerControls.Player.Sprint.IsInProgress():
                currentState = MovementState.sprinting;
                moveSpeed = sprintSpeed;
                break;
            case true:
                currentState = MovementState.walking;
                moveSpeed = walkSpeed;
                break;
            default:
                currentState = MovementState.air;
                break;
        }
    }
    
    private void Jump()
    {
        // Reset y velocity
        playerRigidBody.velocity = new Vector3(playerRigidBody.velocity.x, 0f, playerRigidBody.velocity.z);
        
        // Jump
        playerRigidBody.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void ResetJump()
    {
        readyToJump = true;
    }
}