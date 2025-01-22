using System;
using UnityEngine;
using UnityEngine.Serialization;

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
     * Last Modified Oct 23, 2024.
     */
    
    // 4 'Movement States', an enum that helps when getting or setting what the player is currently doing
    private enum MovementState
    {
        walking,
        sprinting,
        crouching,
        jumping
    }
    // These determine how fast the player moves, and some other required variables
    [Header("Movement Parameters")] 
    [SerializeField] private float moveSpeed;
    [SerializeField] private float walkSpeed = 3.0f;
    [SerializeField] private float sprintSpeed = 6.0f;
    [SerializeField] private Vector3 moveDirection;
    [SerializeField] private Rigidbody playerRigidBody;
    [SerializeField] private float jumpForce;
    [SerializeField] private float jumpCooldown;
     [SerializeField] private float airSpeedMultiplier;
    [SerializeField] private bool readyToJump = true;
    [SerializeField] MovementState currentState;
    [SerializeField] private Transform orientationObj;

    [Header("Grounded Parameters")] 
    [SerializeField] private float groundDrag;
    [SerializeField] private float playerHeight;
    [SerializeField] private float playerWidth;
     [SerializeField] private LayerMask ground;
    [SerializeField] private bool isGrounded;

    [Header("Crouch Parameters")] 
    [SerializeField] private float crouchSpeed;
    [SerializeField] private float crouchYScale;
    [SerializeField] private float startYScale;
    
    [Header("Slope Parameters")]
    [SerializeField] private float maxSlopeAngle;
    private RaycastHit slopeHit;
    [SerializeField] private bool exitingSlope;
    

    // This is less self-explanatory. PlayerControls is the input system we have for all the input action maps in the game. It needs to be initialised in void Awake to function properly.
    // Read up on the Unity Input System to learn more.
    private PlayerControls playerControls;
    [SerializeField] Vector2 currentInput;

    private void Awake()
    {
        playerControls = new PlayerControls();
        playerControls.Player.Enable();
    }

    private void Start()
    {
        playerRigidBody.freezeRotation = true;
        startYScale = transform.localScale.y;
        currentState = MovementState.walking;
    }

    private void Update()
    {
        // Check whether the player is grounded
        isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, ground);
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
    private void HandleMovementInput()
    {
        // Converts wasd inputs into a vector2 of the inputs. Very cool!
        currentInput = playerControls.Player.Movement.ReadValue<Vector2>();
        
        // If player is pressing the jump key, is ready to jump and is grounded, jump
        if (playerControls.Player.Jump.IsInProgress() && readyToJump && isGrounded)
        {
            readyToJump = false;

            Jump();
            
            // This lets you hold down the jump key and keep jumping
            Invoke(nameof(ResetJump), jumpCooldown);
        }

        //if (playerControls.Player.Crouch.IsInProgress() && playerControls.Player.Movement.IsInProgress())
        //{
        //    Debug.Log("Is Crouch Walking");
        //}
        
        // Start crouch if crouch key is pressed and player is not currently crouching
        if (playerControls.Player.Crouch.WasPressedThisFrame() && currentState != MovementState.crouching)
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            // Force the player to the ground
            playerRigidBody.AddForce(Vector3.down * 30f, ForceMode.Impulse);
        }
        // Stop crouch
        else if (currentState != MovementState.crouching && CanStand())
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }

    private void MovePlayer()
    {
        moveDirection = orientationObj.forward * currentInput.y + orientationObj.right * currentInput.x;

        // If climbing up a slope, move the player in line with the slope instead of straight into it
        if (OnSlope() && !exitingSlope)
        {
            playerRigidBody.AddForce(GetSlopeMoveDirection() * (moveSpeed * 10f), ForceMode.Force);

            //if (playerRigidBody.velocity.y > 0)
                //playerRigidBody.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        
        // If the player is up against a wall, don't push the player into the wall which makes them stick to it
        else if(!Physics.Raycast(transform.position, moveDirection, playerWidth+0.3f)) 
        {
            switch (isGrounded)
            {
                case true:
                    playerRigidBody.AddForce(moveDirection.normalized * (moveSpeed * 10f), ForceMode.Force);
                    break;
                case false:
                    playerRigidBody.AddForce(moveDirection.normalized * (moveSpeed * (airSpeedMultiplier * 10f)), ForceMode.Force);
                    break;
            }
        }

        playerRigidBody.useGravity = !OnSlope();
    }

    // Limits the player speed to make sure the player is never going too fast
    private void SpeedControl()
    {
        if (OnSlope() && !exitingSlope)
        {
            // Stops the player from moving too quickly on slopes
            if (playerRigidBody.velocity.magnitude > moveSpeed)
                playerRigidBody.velocity = playerRigidBody.velocity.normalized * moveSpeed;
        }
        else
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
    }

    // Makes the player stop when not moving on the ground, but not while in the air
    // Remove this and simply set drag to big number all the time to have super snappy control at all times
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

    // Handles what movement state the player is in
    // A better implementation would not require this, as every event that switches between states would change the 'currentState' enum properly, but that's too hard
    // If optimization is needed, remove this and employ the above
    private void HandleState()
    {
        switch (isGrounded)
        {
            case true when playerControls.Player.Crouch.IsInProgress() || !CanStand():
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
                currentState = MovementState.jumping;
                break;
        }
    }
    
    private void Jump()
    {
        exitingSlope = true;
        // Reset y velocity to 0
        playerRigidBody.velocity = new Vector3(playerRigidBody.velocity.x, 0f, playerRigidBody.velocity.z);
        
        // Jump by adding an impulse up
        playerRigidBody.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }

    private void ResetJump()
    {
        readyToJump = true;
        exitingSlope = false;
    }
    
    // Determines whether the player is on a slope or not
    private bool OnSlope()
    {
        if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
        {
            var angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false;
    }
    
    // Returns the proper direction to move in while on a slope
    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    // Send out 5 raycasts in a cross formation to determine whether there is a roof blocking the crouch
    // There's almost definitely a sleeker way to code this, I just can't figure out how. This ain't expensive it's just obtuse code
    private bool CanStand()
    {
        // If the player is standing (at full scale), there's no case where they should be crouching
        if (transform.localScale == new Vector3(transform.localScale.x, startYScale, transform.localScale.z))
        {
            return true;
        }
        // Middle
        if ((Physics.Raycast(orientationObj.position, Vector3.up, out slopeHit, playerHeight * crouchYScale + 0.3f)))
        {
            return false;
        }
        // Right
        if ((Physics.Raycast(orientationObj.position + Vector3.right * playerWidth, Vector3.up, out slopeHit, playerHeight * crouchYScale + 0.3f)))
        {
            return false;
        }
        // Left
        if ((Physics.Raycast(orientationObj.position + Vector3.left * playerWidth, Vector3.up, out slopeHit, playerHeight * crouchYScale + 0.3f)))
        {
            return false;
        }
        // Forward
        if ((Physics.Raycast(orientationObj.position + Vector3.forward * playerWidth, Vector3.up, out slopeHit, playerHeight * crouchYScale + 0.3f)))
        {
            return false;
        }
        // Back
        return !(Physics.Raycast(orientationObj.position + Vector3.back * playerWidth, Vector3.up, out slopeHit, playerHeight * crouchYScale + 0.3f));
    }
}