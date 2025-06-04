using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
   

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class SourceMovement : UdonSharpBehaviour
{
    [Header("Source Movement Settings")]
    public float moveSpeed = 7.0f;
    public float maxGroundSpeed = 30.0f;
    public float maxAirSpeed = 30.0f;
    public float airAccelerate = 10.0f;     // sv_airaccelerate
    public float airControl = 0.3f;         // similar to sv_aircontrol
    public float groundAccelerate = 5.0f;   // sv_accelerate
    public float friction = 6.0f;           // sv_friction
    public float jumpForce = 5.0f;
    public float sensitivity = 2.0f;
    public float groundCheckDistance = 0.1f;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Transform body;

    private Vector3 moveInput;
    private Vector3 wishDir;
    private bool grounded = false;
    private bool jumpQueued = false;

    private float lastYaw;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        body = transform;

        rb.freezeRotation = true;
        lastYaw = body.eulerAngles.y;
    }

    void Update()
    {
        ConstantlyTeleportOwner(); // Ensure the owner is always teleported to the local position
        HandleMouseLook();
        HandleMovementInput();
        HandleJumpInput();
        CheckGrounded();
    }

    void FixedUpdate()
    {
        if (grounded)
        {
            ApplyFriction();
            Accelerate(wishDir, moveSpeed, groundAccelerate, maxGroundSpeed);
            if (jumpQueued)
            {
                rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
                jumpQueued = false;
                grounded = false;
            }
        }
        else
        {
            Accelerate(wishDir, moveSpeed, airAccelerate, maxAirSpeed);
            ApplyAirStrafe();
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivity;
        body.Rotate(Vector3.up * mouseX);
    }

    void HandleMovementInput()
    {
        float forward = Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0;
        float strafe = Input.GetKey(KeyCode.D) ? 1 : Input.GetKey(KeyCode.A) ? -1 : 0;

        moveInput = new Vector3(strafe, 0, forward).normalized;
        wishDir = body.TransformDirection(moveInput);
    }

    void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && grounded)
        {
            jumpQueued = true;
        }
    }

    void CheckGrounded()
    {
        float rayLength = (capsule.height / 2) + groundCheckDistance;
        grounded = Physics.Raycast(transform.position, Vector3.down, rayLength + 0.01f);
    }

    void ApplyFriction()
    {
        Vector3 velocity = rb.velocity;
        Vector3 lateral = new Vector3(velocity.x, 0, velocity.z);
        float speed = lateral.magnitude;

        if (speed != 0)
        {
            float drop = speed * friction * Time.fixedDeltaTime;
            float newSpeed = Mathf.Max(speed - drop, 0);
            if (newSpeed != speed)
            {
                newSpeed /= speed;
                rb.velocity = new Vector3(lateral.x * newSpeed, velocity.y, lateral.z * newSpeed);
            }
        }
    }

    void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float maxSpeed)
    {
        if (wishSpeed == 0 || wishDir == Vector3.zero)
            return;

        Vector3 velocity = rb.velocity;
        Vector3 horizontalVel = new Vector3(velocity.x, 0, velocity.z);

        float currentSpeed = Vector3.Dot(horizontalVel, wishDir);
        float addSpeed = Mathf.Min(wishSpeed, maxSpeed) - currentSpeed;

        if (addSpeed <= 0)
            return;

        float accelSpeed = accel * Time.fixedDeltaTime * wishSpeed;
        if (accelSpeed > addSpeed)
            accelSpeed = addSpeed;

        rb.velocity += new Vector3(wishDir.x * accelSpeed, 0, wishDir.z * accelSpeed);
    }

    void ApplyAirStrafe()
    {
        float yawDelta = Mathf.DeltaAngle(lastYaw, body.eulerAngles.y);
        lastYaw = body.eulerAngles.y;

        bool pressingA = Input.GetKey(KeyCode.A);
        bool pressingD = Input.GetKey(KeyCode.D);
        bool pressingW = Input.GetKey(KeyCode.W);
        bool pressingS = Input.GetKey(KeyCode.S);

        if (!(pressingA || pressingD) || (pressingW || pressingS))
            return;

        Vector3 velocity = rb.velocity;
        Vector3 horizontalVel = new Vector3(velocity.x, 0, velocity.z);
        float speed = horizontalVel.magnitude;

        if (speed < 0.1f)
            return;

        Vector3 forward = body.forward;
        Vector3 right = body.right;

        float strafeDir = pressingA ? -1f : 1f;
        float inputYawInfluence = yawDelta * strafeDir;

        float control = airControl * Time.fixedDeltaTime * speed;
        Vector3 add = right * inputYawInfluence * control;

        rb.velocity += new Vector3(add.x, 0, add.z);
    }

    void ConstantlyTeleportOwner()
    {
        VRCPlayerApi localPlayer = Networking.GetOwner(gameObject);
        Vector3 localPosition = transform.position; 
        Quaternion localRotation = transform.rotation; 
        localPlayer.TeleportTo(localPosition, localRotation);
    }
}
