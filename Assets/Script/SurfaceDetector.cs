using UnityEngine;

public class SurfaceDetector : MonoBehaviour
{
    [SerializeField] private float rayDistance = 0.2f;
    [SerializeField] private LayerMask surfaceMask;
    private RaycastHit[] hits = new RaycastHit[10];

    public SurfaceHit Detect(Vector3 rayDirection)
    {
        Ray ray = new Ray(transform.position, rayDirection);

        int hitCount = Physics.RaycastNonAlloc(ray, hits, rayDistance, surfaceMask);

        if (hitCount == 0)
        {
            return new SurfaceHit { HasHit = false };
        }

        RaycastHit closestHit = hits[0];

        for (int i = 1; i < hitCount; i++)
        {
            if (hits[i].distance < closestHit.distance)
            {
                closestHit = hits[i];
            }
        }

        return new SurfaceHit
        {
            HasHit = true,
            Point = closestHit.point,
            Normal = closestHit.normal,
            Angle = Vector3.Angle(closestHit.normal, -rayDirection),
            Collider = closestHit.collider
        };
    }
}