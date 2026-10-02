using System;
using Unity.VisualScripting;
using UnityEngine;

public class orig : MonoBehaviour
{
    [SerializeField] public float timeToApex = 0.35f;
    [SerializeField] public float timeToFall = .25f;
    [SerializeField] public float jumpHeight = 3;

    [SerializeField] private float maxSpeed = 8;
    [SerializeField] private float maxSpeedOnAir = 4;

    [SerializeField] private float timeToReachMax = .3f;

    [SerializeField] private float timeToStop = .1f;

    [SerializeField] private float turnSpeed = .5f;

    [SerializeField] private float turnSpeedScalar = 2;

    float acceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToReachMax;
    float deceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToStop;

    float turnRate => (maxSpeed * turnSpeedScalar) / turnSpeed;

    float riseG => 2f * jumpHeight / (timeToApex * timeToApex);
    float fallG => 2f * jumpHeight / (timeToFall * timeToFall);
    float jumpVelocity => 2f * jumpHeight / timeToApex;

    Vector3 velocity = Vector3.zero;

    private float groundPos;
    private bool isGrounded = true;

    private RaycastHit[] hitThisFrame = new RaycastHit[10];
    private RaycastHit[] hitLastFrame = new RaycastHit[10];

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        groundPos = transform.position.y;
    }

    // Update is called once per frame
    void Update()
    {
        HorizontalMovement();

        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            velocity.y += jumpVelocity;
        }

        if (!isGrounded)
            velocity.y -= (velocity.y < 0 ? fallG : riseG) * Time.deltaTime;

        transform.position += velocity * Time.deltaTime;
    }


    private void HorizontalMovement()
    {
        float input = Input.GetAxisRaw("Horizontal");
        float targetSpeed = maxSpeed * input;
        float targetAccel = ChooseAccel(input);
        var normal = Vector3.zero;
        var hitType = RaycastDown(out normal);
        Debug.Log($"HorizontalMovement {hitType}: {normal}");

        if (hitType == HitType.Air) isGrounded = false;
        switch (hitType)
        {
            case HitType.WalkableSlope:
            {
                var direction = Vector3.ProjectOnPlane(Vector3.right, normal) * targetSpeed;
                velocity = Vector3.MoveTowards(velocity, direction, targetAccel * Time.deltaTime);
                Debug.Log($"WalkableSlope {direction} : {velocity}");
                break;
            }
            case HitType.Ground:
                velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, targetAccel * Time.deltaTime);
                break;

        }
    }

    float ChooseAccel(float input)
    {
        bool isTurning = !Mathf.Approximately(Mathf.Sign(input), Mathf.Sign(velocity.x));
        //Debug.Log($"{input}, {isTurning}");
        if (Mathf.Abs(input) > 0)
        {
            if (!isTurning)
                return acceleration;

            return turnRate;
        }
        else
            return deceleration;
    }

    private HitType RaycastDown(out Vector3 normal)
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        var hits = Physics.RaycastNonAlloc(ray, hitThisFrame, fallG * timeToFall);
        if (hits > 0)
        {
            var text = "Collided with ";
            foreach (var hit in hitThisFrame)
            {
                if (hit.collider == null) continue;
                var dot = Vector3.Dot(hit.normal, Vector3.up);
                var angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
                text += $"{hit.collider.name} Dot: {dot} Angle: {angle}\n";
                normal = hit.normal;
                var lerpedAngle = Mathf.InverseLerp(0, 45, angle);
                var lerpedSpeed = Mathf.Lerp(3,5, lerpedAngle);
                if (angle == 0)
                {
                    return HitType.Ground;
                }
                else if (angle <= 45)
                {
                    return HitType.WalkableSlope;
                }
                else
                {
                    return HitType.Slope;
                }

            }

            Debug.Log($"{text}");
            hitLastFrame = hitThisFrame;

        }

        normal = Vector3.zero;
        return HitType.Air;
    }

    enum HitType
    {
        Ground = 0,
        Air = 1,
        Slope = 2,
        WalkableSlope = 3,
        Wall = 4
    }
}   