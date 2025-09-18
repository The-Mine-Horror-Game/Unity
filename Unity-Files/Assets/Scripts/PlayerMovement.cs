using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Player
{ 
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
         * Last Modified Sept 14, 2024.  // Updated: removed jump; collider-based crouch
         */
        
        // Movement States (Jumping removed)
        private enum MovementState
        {
            Walking,
            Sprinting,
            Crouching,
            Falling
        }

        [Header("Movement Parameters")] 
        [SerializeField] private float moveSpeed;
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float sprintSpeed = 6.0f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float fallSpeed = 1.0f;
        [SerializeField] private Vector3 moveDirection;
        [SerializeField] private Rigidbody playerRigidBody;
        [SerializeField] private MovementState currentState;
        [SerializeField] private Transform orientation;

        [Header("Grounded Parameters")] 
        [SerializeField] private float groundDrag = 4f;
        [SerializeField] private float playerHeight = 2.0f;   // used for slope/ground rays
        [SerializeField] private float playerWidth = 0.5f;    // used for wall check rays
        [SerializeField] private LayerMask ground;
        [SerializeField] private bool isGrounded;
        
        [Header("Grounded Check Parameters")]
        [SerializeField] private float groundSkin = 0.02f;       // tiny inset to avoid false negatives
        [SerializeField] private float groundProbeHeight = 0.06f; // short vertical span near the feet
        
        [Header("Crouch (Collider-Based)")]
        [SerializeField] private CapsuleCollider capsuleCollider;          // assign in inspector or via GetComponent in Start
        [SerializeField] private float standHeight = 2.0f;
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float crouchLerpSpeed = 12f;  // how snappy the height change feels
        [SerializeField] private Transform headPivot; // assign to "Head" in inspector
        [SerializeField] private float headStandY = 1.65f;
        [SerializeField] private float headCrouchY = 0.65f;
        [SerializeField] private float headLerpSpeed = 12f;
        [SerializeField] private bool toggleCrouch = false;
        [SerializeField] private bool isCrouching = false;
        
        [Header("Lean Parameters")]
        [SerializeField] private Transform leanPivot;  // assign in Inspector
        [SerializeField] private Transform rollPivot;  // assign in Inspector
        [SerializeField] private Transform camTarget;  // assign in Inspector (Cinemachine follows this)

        [SerializeField] private float maxLeanAngleBody = 20f;    // degrees (roll around Z)
        [SerializeField] private float maxLeanAngleCamera = 12f;
        [SerializeField] private float leanRadius = 0.28f;    // camera orbital radius to the side
        [SerializeField] private float leanForward = 0.06f;   // small forward push while leaning
        [SerializeField] private float leanSpeed = 5f;       // how fast we reach target lean
        [SerializeField] private float leanReturnSpeed = 6.5f; // speed back to center
    
        // Collider displacement so lean is physically blocked by walls:
        [SerializeField] private float colliderLeanOffsetMax = 0.20f;  // meters sideways
        [SerializeField] private float colliderSkin = 0.02f;           // inset to avoid clipping
        [SerializeField] private bool TESTINGBOOL = false;

        private float lean;        // current smoothed [-1..1]
        private float leanTarget;  // desired input [-1..1]
        private float capsuleCenterBaseX; // remember the unleaned center
        
        [Header("Look Parameters")]
        [SerializeField] private Transform playerCam; // assigned in editor
        [SerializeField] private float mouseSensitivity = 0.001f;

        [Header("Slope Parameters")]
        [SerializeField] private float maxSlopeAngle = 45f;
        private RaycastHit slopeHit;
        [SerializeField] private bool exitingSlope = false;    // kept for slope logic (no jump sets it true now)

        // Input
        private PlayerControls playerControls;
        [SerializeField] private Vector2 currentInput;

        private void Awake()
        {
            playerControls = new PlayerControls();
            playerControls.Player.Enable();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Start()
        {
            if (!capsuleCollider) capsuleCollider = GetComponent<CapsuleCollider>();
            playerRigidBody.freezeRotation = true;
            currentState = MovementState.Walking;

            // Ensure collider starts at stand height
            if (capsuleCollider)
            {
                capsuleCollider.height = standHeight;
                // center so feet stay planted
                capsuleCollider.center = new Vector3(capsuleCollider.center.x, standHeight * 0.5f, capsuleCollider.center.z);
            }

            capsuleCenterBaseX = capsuleCollider.center.x;
        }

        private void Update()
        {
            // Check whether the player is grounded
            isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.7f, ground);

            HandleGrounded();
            HandleMovementInput();
            HandleDrag();
            SpeedControl();
            HandleRotate();
            HandleLean();
            HandleCrouch();
            HandleState();
            
        }

        private void FixedUpdate()
        {
            MovePlayer();
        }

        private void HandleGrounded()
        {
            if (!capsuleCollider)
            {
                isGrounded = false;
                return;
            }

            // World-space center of the capsule
            var centerWorld = transform.TransformPoint(capsuleCollider.center);
            var radius = Mathf.Max(0.01f, capsuleCollider.radius - groundSkin);

            // Compute the feet position (bottom of capsule)
            var halfHeight = Mathf.Max(capsuleCollider.height * 0.5f, radius + 0.001f);
            var feet = centerWorld + Vector3.down * (halfHeight - radius + groundSkin);

            // Make a *very short* capsule just above the feet. If it overlaps ground, we're grounded.
            var p1 = feet;                              // bottom sphere center
            var p2 = feet + Vector3.up * groundProbeHeight; // top sphere center (small span)

            isGrounded = Physics.CheckCapsule(p1, p2, radius, ground, QueryTriggerInteraction.Ignore);
        }
        
        // Handles keyboard inputs
        private void HandleMovementInput()
        {
            // Converts WASD inputs into a Vector2 of the inputs.
            currentInput = playerControls.Player.Movement.ReadValue<Vector2>();
        }

        private void MovePlayer()
        {
            moveDirection = orientation.forward * currentInput.y + orientation.right * currentInput.x;

            // If climbing up a slope, move the player in line with the slope instead of straight into it
            if (OnSlope() && !exitingSlope)
            {
                playerRigidBody.AddForce(GetSlopeMoveDirection() * (moveSpeed * 10f), ForceMode.Force);
            }
            // If the player is up against a wall, don't push the player into it
            else if (!Physics.Raycast(transform.position, moveDirection, playerWidth + 0.3f))
            {
                if (isGrounded)
                {
                    playerRigidBody.AddForce(moveDirection.normalized * (moveSpeed * 10f), ForceMode.Force);
                }
            }

            playerRigidBody.useGravity = !OnSlope();
        }

        // Limits the player speed to make sure the player is never going too fast
        private void SpeedControl()
        {
            if (OnSlope() && !exitingSlope)
            {
                if (playerRigidBody.velocity.magnitude > moveSpeed)
                    playerRigidBody.velocity = playerRigidBody.velocity.normalized * moveSpeed;
            }
            else
            {
                var flatVel = new Vector3(playerRigidBody.velocity.x, 0f, playerRigidBody.velocity.z);
                if (flatVel.magnitude < moveSpeed)
                    return;
                var limitedVel = flatVel.normalized * moveSpeed;
                playerRigidBody.velocity = new Vector3(limitedVel.x, playerRigidBody.velocity.y, limitedVel.z);
            }
        }
        
        private void HandleRotate()
        {
            transform.rotation = Quaternion.Euler(0, playerCam.rotation.eulerAngles.y, 0);
            Vector2 look = playerControls.Player.Look.ReadValue<Vector2>(); // your Input System axis

            var pitch = 0f;

            // PITCH: rotate only headPivot around local X
            pitch -= look.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -75, 80);
            if (headPivot) headPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Makes the player stop when not moving on the ground, but not while in the air
        private void HandleDrag()
        {
            playerRigidBody.drag = isGrounded ? groundDrag : 0f;
        }

        // State handling
        private void HandleState()
        {
            // Toggle mode
            if (toggleCrouch && playerControls.Player.Crouch.WasPressedThisFrame())
                isCrouching = !isCrouching;
            
            // Hold mode
            else if (!toggleCrouch) 
                isCrouching = playerControls.Player.Crouch.IsInProgress();
            
            // Force crouch if no headroom, regardless
            if (!HasHeadroom())
                isCrouching = true;
            
            // Decide state
            if (isGrounded && isCrouching)
            {
                currentState = MovementState.Crouching;
                moveSpeed = crouchSpeed; // keep crouch slow
            }
            else if (isGrounded && playerControls.Player.Sprint.IsInProgress())
            {
                currentState = MovementState.Sprinting;
                moveSpeed = sprintSpeed;
            }
            else if (isGrounded)
            {
                currentState = MovementState.Walking;
                moveSpeed = walkSpeed;
            }
            else
            {
                currentState = MovementState.Falling;
                moveSpeed = fallSpeed;
            }
        }

        // Handle collider-height crouch smoothing
        private void HandleCrouch()
        {
            // Smoothly adjust collider height/center (no transform scaling)
            if (capsuleCollider)
            {
                var targetHeight = (currentState == MovementState.Crouching) ? crouchHeight : standHeight;

                // If trying to stand but no headroom, force crouch target
                if (targetHeight > capsuleCollider.height && !HasHeadroom())
                    targetHeight = crouchHeight;

                var newHeight = Mathf.Lerp(capsuleCollider.height, targetHeight, Time.deltaTime * crouchLerpSpeed);
                var delta = newHeight - capsuleCollider.height;

                var targetY = (currentState == MovementState.Crouching) ? headCrouchY : headStandY;
                var pos = headPivot.localPosition;
                pos.y = Mathf.Lerp(pos.y, targetY, Time.deltaTime * headLerpSpeed);
                headPivot.localPosition = pos;

                // Move center so the feet stay anchored while height changes
                var c = capsuleCollider.center;
                c.y += delta * 0.5f;
                capsuleCollider.center = c;
                capsuleCollider.height = newHeight;
            }
        }

        private void HandleLean()
        {
            // Input 
            var leanAxis = playerControls.Player.Lean.ReadValue<float>();
            var raw = Mathf.Clamp(leanAxis, -1f, 1f);

            // Smooth
            var speed = Mathf.Approximately(raw, 0f) ? leanReturnSpeed : leanSpeed;
            leanTarget = raw;
            lean = Mathf.MoveTowards(lean, leanTarget, speed * Time.deltaTime);

            var bodyAngle = lean * maxLeanAngleBody;
            var camAngle = lean * maxLeanAngleCamera;

            // Body lean (keeps sideways aligned with player yaw)
            if (leanPivot) leanPivot.localRotation = Quaternion.Euler(0f, 0f, bodyAngle);
            
            // Camera horizon roll
            if (rollPivot) rollPivot.localRotation = Quaternion.Euler(0f, 0f, camAngle);

            // 4) Camera circular offset (in local space under LeanPivot)
            if (camTarget)
            {
                // Orbit sideways by rotating a base X offset around local Z by the *body* angle
                var baseOffset = new Vector3(leanRadius, 0f, 0f);
                var arc = Quaternion.AngleAxis(bodyAngle, Vector3.forward);
                var orbit = arc * baseOffset;

                var fwd = Mathf.Abs(lean) * leanForward;
                camTarget.localPosition = new Vector3(orbit.x, camTarget.localPosition.y, fwd);
                // (y stays whatever your crouch/head system sets)
            }

            // 5) Collider lateral shift (X-only!) so walls block the lean
            if (capsuleCollider)
            {
                var desiredDeltaX = capsuleCenterBaseX + colliderLeanOffsetMax * lean; // relative to current X
                var allowedDeltaX = ComputeAllowedLeanOffsetX(desiredDeltaX);
                var c = capsuleCollider.center;
                c.x -= allowedDeltaX;
                capsuleCollider.center = c;
            }
        }

        private float ComputeAllowedLeanOffsetX(float targetX)
        {
            if (!capsuleCollider) return targetX;

            float radius = Mathf.Max(0.01f, capsuleCollider.radius - colliderSkin);
            float halfH = Mathf.Max(capsuleCollider.height * 0.5f, radius + 0.001f);

            Vector3 centerWorld = transform.TransformPoint(capsuleCollider.center);
            Vector3 up = transform.up;
            Vector3 p1 = centerWorld + up * (halfH - radius);
            Vector3 p2 = centerWorld - up * (halfH - radius);

            float desiredDelta = targetX - capsuleCenterBaseX;
            Vector3 side = transform.right * Mathf.Sign(desiredDelta);
            float distance = Mathf.Abs(desiredDelta);

            if (Physics.CapsuleCast(p1, p2, radius, side, out RaycastHit hit, distance, ground, QueryTriggerInteraction.Ignore))
            {
                float allowed = Mathf.Max(0f, hit.distance - colliderSkin);
                return capsuleCenterBaseX + allowed * Mathf.Sign(desiredDelta);
            }
            return targetX;
        }
        
        // Determines whether the player is on a slope or not
        private bool OnSlope()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.3f))
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

        // CapsuleCast upward from crouch to stand to check headroom
        private bool HasHeadroom()
        {
            if (!capsuleCollider) return true;

            // Build a capsule roughly matching the current crouch body, then cast upward to stand height
            var radius = capsuleCollider.radius * 0.95f;
            var bottomY = transform.position.y + radius;
            var topYCurrent = transform.position.y + Mathf.Max(capsuleCollider.height - radius, radius);
            var p1 = new Vector3(transform.position.x, bottomY, transform.position.z);
            var p2 = new Vector3(transform.position.x, topYCurrent, transform.position.z);

            float castDistance = Mathf.Max(0f, standHeight - capsuleCollider.height);
            // Cast against anything in 'ground' (or replace with a broader obstacle mask if needed)
            return !Physics.CapsuleCast(p1, p2, radius, Vector3.up, castDistance, ground, QueryTriggerInteraction.Ignore);
        }
    }
}
