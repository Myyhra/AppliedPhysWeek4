using System.Collections.Generic;

using UnityEngine;

public class move : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 8;
    [SerializeField] private float maxSpeedOnAir = 4;
    [SerializeField] private float timeToReachMax = .3f;
    [SerializeField] private float timeToStop = .1f;
    [SerializeField] private float turnSpeed = .5f;
    [SerializeField] private float turnSpeedScalar = 2;

    float acceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToReachMax;
    float deceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToStop;
    float turnRate => (maxSpeed * turnSpeedScalar) / turnSpeed;

    [Header("Jump")]
    [SerializeField] float jumpHeight = 3;
    [SerializeField] float heldJumpHeight = 5f;
    [SerializeField] float timeToApex = 0.35f;
    [SerializeField] float timeToFall = .25f;
    [SerializeField] float doubleJumpHeight = 3f;

    public float reverseGravityGroundPos = 10f;

    bool isJumping;
    bool holdingJump;
    bool hasDoubleJumped;

    float currentJumpHeight;
    float jumpStartY;

    float riseG => 2f * jumpHeight / (timeToApex * timeToApex);
    float fallG => 2f * jumpHeight / (timeToFall * timeToFall);
    float jumpVelocity => 2f * jumpHeight / timeToApex;
    float heldRiseG => 2f * heldJumpHeight / (timeToApex * timeToApex);
    float heldJumpVelocity => 2f * heldJumpHeight / timeToApex;
    float doubleJumpVelocity => 2f * doubleJumpHeight / timeToApex;

    Vector3 velocity = Vector3.zero;

    private float groundPos;
    private bool gravityswitch;

    // 1 = normal gravity, -1 = reverse gravity
    float gravityDirection => gravityswitch ? -1f : 1f;

    [SerializeField] float rayDist;

    RaycastHit[] hitThisFrame = new RaycastHit[10];
    RaycastHit[] hitLastFrame = new RaycastHit[10];

    // MovementDebugDraw
    List<Vector3> trailPositions = new List<Vector3>();

    private bool isGrounded
    {
        get
        {
            if (!gravityswitch)
                return Mathf.Approximately(transform.position.y, groundPos);

            return Mathf.Approximately(transform.position.y, reverseGravityGroundPos);
        }
    }

    void Start()
    {
        groundPos = transform.position.y;
    }

    void Update()
    {
        HorizontalMovement();
        Jumping();

        transform.position += velocity * Time.deltaTime;

        // Normal gravity
        if (!gravityswitch)
        {
            if (transform.position.y < groundPos)
            {
                transform.position = new Vector3(
                    transform.position.x,
                    groundPos,
                    transform.position.z
                );

                velocity.y = 0f;
                isJumping = false;
                holdingJump = false;
                hasDoubleJumped = false;
            }
        }

        // Reverse gravity
        else
        {
            if (transform.position.y > reverseGravityGroundPos)
            {
                transform.position = new Vector3(
                    transform.position.x,
                    reverseGravityGroundPos,
                    transform.position.z
                );

                velocity.y = 0f;
                isJumping = false;
                holdingJump = false;
                hasDoubleJumped = false;
            }
        }

        // Determine the apex of the current jump
        if (isJumping)
        {
            float maxJumpY =
                jumpStartY +
                (currentJumpHeight * gravityDirection);

            if (!gravityswitch && transform.position.y >= maxJumpY)
            {
                transform.position = new Vector3(
                    transform.position.x,
                    maxJumpY,
                    transform.position.z
                );

                velocity.y = 0f;
                isJumping = false;
                holdingJump = false;

                Debug.Log("Held Jump");
            }
            else if (gravityswitch && transform.position.y <= maxJumpY)
            {
                transform.position = new Vector3(
                    transform.position.x,
                    maxJumpY,
                    transform.position.z
                );

                velocity.y = 0f;
                isJumping = false;
                holdingJump = false;

                Debug.Log("Held Jump");
            }
        }

        DebugDrawTrail();
    }

    private void Jumping()
    {
        // Initial Jump
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            jumpStartY = transform.position.y;
            velocity.y = heldJumpVelocity * gravityDirection;
            currentJumpHeight = heldJumpHeight;

            isJumping = true;
            holdingJump = true;
            hasDoubleJumped = false;

            Debug.Log("Jump");
        }

        // Jump Release
        if (isJumping &&
            holdingJump &&
            Input.GetButtonUp("Jump") &&
            velocity.y * gravityDirection > 0f)
        {
            holdingJump = false;

            float currentHeight =
                Mathf.Abs(transform.position.y - jumpStartY);

            float remainingHeight =
                jumpHeight - currentHeight;

            if (remainingHeight <= 0f)
            {
                velocity.y = 0f;
                currentJumpHeight = currentHeight;
            }
            else
            {
                velocity.y =
                    Mathf.Sqrt(2f * riseG * remainingHeight)
                    * gravityDirection;

                currentJumpHeight = jumpHeight;
            }
        }

        // Double Jump
        if (!isGrounded &&
            Input.GetButtonDown("Jump") &&
            !hasDoubleJumped)
        {
            jumpStartY = transform.position.y;
            velocity.y = doubleJumpVelocity * gravityDirection;
            currentJumpHeight = doubleJumpHeight;

            isJumping = true;
            holdingJump = false;
            hasDoubleJumped = true;

            Debug.Log("Double Jump");
        }

        if (velocity.y * gravityDirection > 0f)
        {
            if (holdingJump)
            {
                velocity.y -=
                    heldRiseG *
                    gravityDirection *
                    Time.deltaTime;
            }
            else
            {
                velocity.y -=
                    riseG *
                    gravityDirection *
                    Time.deltaTime;
            }
        }
        else
        {
            velocity.y -=
                fallG *
                gravityDirection *
                Time.deltaTime;
        }
    }

    private void HorizontalMovement()
    {
        float input = Input.GetAxisRaw("Horizontal");

        float targetSpeed = maxSpeed * input;
        float targetAccel = ChooseAccel(input);

        velocity.x = Mathf.MoveTowards(
            velocity.x,
            targetSpeed,
            targetAccel * Time.deltaTime
        );
    }

    float ChooseAccel(float input)
    {
        bool isTurning =
            !Mathf.Approximately(
                Mathf.Sign(input),
                Mathf.Sign(velocity.x)
            );

        if (Mathf.Abs(input) > 0)
        {
            if (!isTurning)
                return acceleration;

            return turnRate;
        }
        else
        {
            return deceleration;
        }
    }

    HitType GroundHit()
    {
        Ray ray = new Ray(
            transform.position,
            Vector3.down
        );

        var hits = Physics.RaycastNonAlloc(
            ray,
            hitThisFrame,
            rayDist
        );

        return HitType.Air;
    }

    public void GravitySwitch()
    {
        gravityswitch = !gravityswitch;

        velocity.y = 0f;
        isJumping = false;
        holdingJump = false;
        hasDoubleJumped = false;

        Debug.Log("Gravity Switch");
    }

    void DebugDrawTrail()
    {
        float totalTime = 3f;
        float timer = 0f;

        trailPositions.Add(transform.position);

        if (trailPositions.Count > 1)
        {
            for (int i = 1; i < trailPositions.Count; i++)
            {
                Debug.DrawLine(
                    trailPositions[i - 1],
                    trailPositions[i],
                    Color.red
                );
            }
        }

        timer += Time.deltaTime;

        if (timer >= totalTime)
        {
            timer = 0f;
            trailPositions.Clear();
        }
    }
}

enum HitTypes
{
    Ground,
    Slope,
    Wall,
    Air
}