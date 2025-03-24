using UnityEngine;

public class AtomParticle : MonoBehaviour
{
    public int typeIndex;
    public float mass = 1f;
    public float radius = 0.5f;
    public float bondingStrength = 1f;
    public Vector3 velocity;
    public bool bonded = false;
    public int bondCount = 0;
}