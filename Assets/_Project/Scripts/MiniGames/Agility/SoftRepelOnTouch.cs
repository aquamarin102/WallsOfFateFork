using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SoftRepelOnTouch : MonoBehaviour
{
    [SerializeField] private float touchImpulse = 2.5f;
    [SerializeField] private float repelPerSecond = 8.5f;
    [SerializeField] private float maxDistanceBias = 1.1f;

    private Collider _collider;
    private readonly Dictionary<Collider, PlayerMotor> _motorCache = new();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        _motorCache.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        ApplyRepel(other, touchImpulse);
    }

    private void OnTriggerStay(Collider other)
    {
        ApplyRepel(other, repelPerSecond * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        ApplyRepel(collision.collider, touchImpulse);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision == null)
            return;

        ApplyRepel(collision.collider, repelPerSecond * Time.deltaTime);
    }

    private void ApplyRepel(Collider other, float strength)
    {
        var motor = ResolveMotor(other);
        if (motor == null)
            return;

        Vector3 from = ResolveHazardPoint(other);
        Vector3 to = ResolvePlayerPoint(other);
        Vector3 direction = to - from;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = other.transform.position - transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float distanceFactor = Mathf.Clamp01(maxDistanceBias - direction.magnitude);
        Vector3 push = direction.normalized * (strength * Mathf.Max(0.25f, distanceFactor));
        motor.AddExternalVelocity(push);
    }

    private PlayerMotor ResolveMotor(Collider other)
    {
        if (other == null)
            return null;

        if (_motorCache.TryGetValue(other, out PlayerMotor cachedMotor) && cachedMotor != null)
            return cachedMotor;

        PlayerMotor motor = other.GetComponentInParent<PlayerMotor>();
        if (motor != null)
            _motorCache[other] = motor;
        else
            _motorCache.Remove(other);

        return motor;
    }

    private Vector3 ResolveHazardPoint(Collider other)
    {
        if (_collider == null)
            return transform.position;

        return _collider.ClosestPoint(other.bounds.center);
    }

    private static Vector3 ResolvePlayerPoint(Collider other)
    {
        return other.ClosestPoint(other.bounds.center);
    }
}
