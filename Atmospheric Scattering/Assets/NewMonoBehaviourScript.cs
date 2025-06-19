using UnityEngine;

public class Orbit : MonoBehaviour
{
    public Transform planet;                // The object to orbit around
    public float orbitRadius = 5f;          // Distance from the planet
    public float orbitSpeed = 10f;          // Degrees per second
    public Vector3 orbitRotationEuler;      // Rotation of the orbital plane

    private float angle;                    // Current angle around the orbit
    private Quaternion orbitRotation;       // Cached rotation as Quaternion

    void Start()
    {
        if (planet == null)
        {
            Debug.LogError("Planet not assigned.");
            enabled = false;
            return;
        }

        orbitRotation = Quaternion.Euler(orbitRotationEuler);
        UpdateOrbitPosition();
    }

    void Update()
    {
        angle += orbitSpeed * Mathf.Deg2Rad * Time.deltaTime;

        if (angle > Mathf.PI * 2f) angle -= Mathf.PI * 2f;

        UpdateOrbitPosition();
    }

    void UpdateOrbitPosition()
    {
        // Base circular orbit in XZ plane
        Vector3 localOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;

        // Rotate the plane
        Vector3 rotatedOffset = orbitRotation * localOffset;

        transform.position = planet.position + rotatedOffset;

        // Optional: Look at the planet
        transform.LookAt(planet);
    }
}
