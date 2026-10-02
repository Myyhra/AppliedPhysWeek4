using System.Collections.Generic;
using UnityEngine;

public class PlayerPhys : MonoBehaviour
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

    [Header("Wall")]
    [SerializeField] float wallSlideSpeed = 2f;

    Vector3 velocity = Vector3.zero;

    bool gravityswitch;
    float gravityDirection => gravityswitch ? -1f : 1f;
    Vector3 gravityUp => Vector3.up * gravityDirection;

    [Header("Surface Detection")]
    [SerializeField] float rayDist = 1f;
    [SerializeField] float wallRayDist = 0.5f;
    [SerializeField] float maxWalkableSlope = 45f;
    [SerializeField] float wallDotThreshold = 0.1f;
    [Header("Slope")]
    [SerializeField] float slopeSlideSpeed = 6f;
    [SerializeField] private float forcedSlopeSlideSpeed = 12f;

    float walkableDot => Mathf.Cos(maxWalkableSlope * Mathf.Deg2Rad);

    RaycastHit[] hitThisFrame = new RaycastHit[10];


    private HitType currentHitType;
    private Vector3 currentSurfaceNormal;
    private bool isGrounded => currentHitType == HitType.Ground;

    void Update()
    {
        currentHitType = GroundHit();

        bool touchingWall = WallHit(out RaycastHit wallHit);

        HorizontalMovement();
        SlopeSlide();
        ForcedSlopeSlide();
        WallStop(touchingWall, wallHit);
        Jumping();
        WallSlide(touchingWall);

        transform.position += velocity * Time.deltaTime;

        if (touchingWall)
            Debug.DrawRay(transform.position, wallHit.point - transform.position, Color.blue);

    }

    private void Jumping()
    {
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            jumpStartY = transform.position.y;
            velocity.y += heldJumpVelocity * gravityDirection;

            currentJumpHeight = heldJumpHeight;
            isJumping = true;
            holdingJump = true;
            hasDoubleJumped = false;

            Debug.Log("Jump");
        }

        if (isGrounded)
            return;

        if (isJumping && holdingJump && Input.GetButtonUp("Jump") && velocity.y * gravityDirection > 0f)
        {
            holdingJump = false;

            float currentHeight = Mathf.Abs(transform.position.y - jumpStartY);
            float remainingHeight = jumpHeight - currentHeight;

            if (remainingHeight <= 0f)
            {
                velocity.y = 0f;
                currentJumpHeight = currentHeight;
            }
            else
            {
                velocity.y += Mathf.Sqrt(2f * riseG * remainingHeight) * gravityDirection;
                currentJumpHeight = jumpHeight;
            }
        }

        if (!isGrounded && Input.GetButtonDown("Jump") && !hasDoubleJumped)
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
                velocity.y -= heldRiseG * gravityDirection * Time.deltaTime;
            else
                velocity.y -= riseG * gravityDirection * Time.deltaTime;
        }
        else
        {
            velocity.y -= fallG * gravityDirection * Time.deltaTime;
        }
    }

    private void HorizontalMovement()
    {
        float input = Input.GetAxisRaw("Horizontal");

        if (currentHitType == HitType.Slope)
            return;

        float targetSpeed = maxSpeed * input;

        if (currentHitType == HitType.Ground && !Mathf.Approximately(input, 0f))
        {
            float slopeDot = Vector3.Dot(currentSurfaceNormal, gravityUp);

            if (slopeDot < 0.999f)
            {
                Vector3 downhillDirection = Vector3.ProjectOnPlane(
                    -gravityUp,
                    currentSurfaceNormal
                ).normalized;

                Vector3 uphillDirection = -downhillDirection;
                Vector3 movementDirection = transform.right * Mathf.Sign(input);

                float uphillAmount = Vector3.Dot(
                    movementDirection,
                    uphillDirection
                );

                if (uphillAmount > 0f)
                {
                    float slopeAngle = Mathf.Acos(
                        Mathf.Clamp(slopeDot, -1f, 1f)
                    ) * Mathf.Rad2Deg;

                    float uphillMultiplier = Mathf.Clamp01(
                        1f - slopeAngle / maxWalkableSlope
                    );

                    targetSpeed *= uphillMultiplier;
                }
            }
        }

        float targetAccel = ChooseAccel(input);

        velocity.x = Mathf.MoveTowards(
            velocity.x,
            targetSpeed,
            targetAccel * Time.deltaTime
        );

        if (!isGrounded)
            return;

        Vector3 surfaceVelocity = Vector3.ProjectOnPlane(
            velocity,
            currentSurfaceNormal
        );

        velocity.x = surfaceVelocity.x;
        velocity.y = surfaceVelocity.y;

        Debug.Log($"Moving: {input}");
    }

    float ChooseAccel(float input)
    {
        bool isTurning = !Mathf.Approximately(Mathf.Sign(input), Mathf.Sign(velocity.x));

        if (Mathf.Abs(input) > 0)
        {
            if (!isTurning)
                return acceleration;

            return turnRate;
        }

        return deceleration;
    }

    void WallStop(bool touchingWall, RaycastHit wallHit)
    {
        if (!touchingWall)
            return;

        float velocityIntoWall = Vector3.Dot(velocity, -wallHit.normal);

        if (velocityIntoWall > 0f)
            velocity -= -wallHit.normal * velocityIntoWall;
    }

    void WallSlide(bool touchingWall)
    {
        if (isGrounded)
            return;

        if (!touchingWall)
            return;

        float fallSpeed = Vector3.Dot(velocity, -gravityUp);

        if (fallSpeed > wallSlideSpeed)
            velocity += gravityUp * (fallSpeed - wallSlideSpeed);
    }

    bool WallHit(out RaycastHit wallHit)
    {
        wallHit = default;

        float input = Input.GetAxisRaw("Horizontal");

        if (Mathf.Approximately(input, 0f))
            return false;

        Vector3 wallDirection = transform.right * Mathf.Sign(input);

        if (!Physics.Raycast(transform.position, wallDirection, out wallHit, wallRayDist))
            return false;

        float dot = Vector3.Dot(wallHit.normal, gravityUp);

        if (Mathf.Abs(dot) <= wallDotThreshold)
            return true;

        if (dot < walkableDot)
            return true;

        return false;
    }

    HitType GroundHit()
    {
        currentSurfaceNormal = Vector3.zero;

        Vector3 rayDirection = -gravityUp;
        Ray ray = new Ray(transform.position, rayDirection);
        int hits = Physics.RaycastNonAlloc(ray, hitThisFrame, rayDist);

        if (hits > 0)
        {
            RaycastHit hit = hitThisFrame[0];
            currentSurfaceNormal = hit.normal;

            float dot = Vector3.Dot(hit.normal, gravityUp);

            if (dot >= walkableDot)
                return HitType.Ground;

            if (Mathf.Abs(dot) <= wallDotThreshold)
                return HitType.Wall;

            return HitType.Slope;
        }

        return HitType.Air;
    }
    void SlopeSlide()
    {
        if (currentHitType != HitType.Slope)
            return;

        velocity = Vector3.ProjectOnPlane(
            velocity,
            currentSurfaceNormal
        );

        Vector3 slideDirection = Vector3.ProjectOnPlane(
            -gravityUp,
            currentSurfaceNormal
        ).normalized;

        velocity = Vector3.MoveTowards(
            velocity,
            slideDirection * slopeSlideSpeed,
            slopeSlideSpeed * Time.deltaTime
        );
    }
    private void ForcedSlopeSlide()
    {
        if (currentHitType != HitType.Ground)
            return;

        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            return;

        float surfaceDot = Vector3.Dot(currentSurfaceNormal, gravityUp);

        if (surfaceDot >= 0.999f)
            return;

        Vector3 slideDirection = Vector3.ProjectOnPlane(
            -gravityUp,
            currentSurfaceNormal
        ).normalized;

        velocity = Vector3.MoveTowards(
            velocity,
            slideDirection * forcedSlopeSlideSpeed,
            forcedSlopeSlideSpeed * Time.deltaTime
        );

        Debug.Log($"Sliding");
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

}

enum HitType
{
    Ground,
    Slope,
    Wall,
    Air
}

