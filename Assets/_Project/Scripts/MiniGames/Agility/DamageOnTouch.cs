using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DamageOnTouch : MonoBehaviour
{
    private const string ExplosionEffectPath = "MiniGames/Agility/Agility HitFX";
    private static GameObject _cachedExplosionEffect;

    public int damage = 1;
    public GameObject explosionEffect;

    [Tooltip("If true, the damaging collider is expected to be a trigger.")]
    public bool requireTrigger = true;

    private Collider _collider;
    private readonly Dictionary<Collider, PlayerHealth> _healthCache = new();

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        ResolveExplosionEffect();
    }

    private void Reset()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;
    }

    private void OnDisable()
    {
        _healthCache.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (requireTrigger && (_collider == null || !_collider.isTrigger))
            return;

        PlayerHealth hp = ResolveHealth(other);
        if (hp == null)
            return;

        hp.TakeDamage(damage);

        GameObject effectPrefab = ResolveExplosionEffect();
        if (effectPrefab == null)
            return;

        Vector3 contactPoint = other.ClosestPoint(transform.position);
        GameObject effectInstance = Instantiate(effectPrefab, contactPoint, Quaternion.identity);
        Destroy(effectInstance, 10f);
    }

    private PlayerHealth ResolveHealth(Collider other)
    {
        if (other == null)
            return null;

        if (_healthCache.TryGetValue(other, out PlayerHealth cachedHealth) && cachedHealth != null)
            return cachedHealth;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
            _healthCache[other] = health;
        else
            _healthCache.Remove(other);

        return health;
    }

    private GameObject ResolveExplosionEffect()
    {
        if (explosionEffect != null)
            return explosionEffect;

        if (_cachedExplosionEffect == null)
            _cachedExplosionEffect = Resources.Load<GameObject>(ExplosionEffectPath);

        explosionEffect = _cachedExplosionEffect;
        return explosionEffect;
    }
}
