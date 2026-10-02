using UnityEngine;

public struct SurfaceHit
{
    public bool HasHit;
    public Vector3 Point;
    public Vector3 Normal;
    public float Angle;
    public Collider Collider;
}