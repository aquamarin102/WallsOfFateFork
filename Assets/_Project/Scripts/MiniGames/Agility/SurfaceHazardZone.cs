using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SurfaceHazardZone : MonoBehaviour
{
    public enum ZoneEffect
    {
        Slow,
        Slip,
        Burn
    }

    [SerializeField] private float lifetime = 2.5f;
    [SerializeField] private float speedMultiplier = 0.65f;
    [SerializeField] private float controlMultiplier = 0.65f;
    [SerializeField] private float damageInterval = 0.5f;
    [SerializeField] private int burnDamage = 1;

    private readonly Dictionary<PlayerHealth, float> _nextBurnTick = new();
    private readonly Dictionary<Collider, PlayerHealth> _healthCache = new();
    private readonly Dictionary<Collider, MovementModifiers> _modifierCache = new();
    private ZoneEffect _zoneEffect;
    private float _expireAt;

    public void Configure(ZoneEffect effect, float seconds, float speedMult = 0.65f, float controlMult = 0.65f)
    {
        _zoneEffect = effect;
        lifetime = Mathf.Max(0.1f, seconds);
        speedMultiplier = speedMult;
        controlMultiplier = controlMult;
        _expireAt = Time.time + lifetime;
    }

    private void Awake()
    {
        var collider = GetComponent<Collider>();
        collider.isTrigger = true;
        _expireAt = Time.time + lifetime;
    }

    private void Update()
    {
        if (Time.time >= _expireAt)
            Destroy(gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        var hp = ResolveHealth(other);
        var mods = ResolveModifiers(other);
        if (hp == null && mods == null)
            return;

        switch (_zoneEffect)
        {
            case ZoneEffect.Slow:
                if (mods != null)
                    mods.ApplySpeedMultiplier(speedMultiplier, 0.15f);
                break;
            case ZoneEffect.Slip:
                if (mods != null)
                    mods.ApplyControlMultiplier(controlMultiplier, 0.15f);
                break;
            case ZoneEffect.Burn:
                if (hp == null)
                    return;

                float now = Time.time;
                if (!_nextBurnTick.TryGetValue(hp, out float nextTick))
                    nextTick = 0f;

                if (now >= nextTick)
                {
                    hp.TakeDamage(burnDamage);
                    _nextBurnTick[hp] = now + damageInterval;
                }
                break;
        }
    }

    private void OnDisable()
    {
        _nextBurnTick.Clear();
        _healthCache.Clear();
        _modifierCache.Clear();
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

    private MovementModifiers ResolveModifiers(Collider other)
    {
        if (other == null)
            return null;

        if (_modifierCache.TryGetValue(other, out MovementModifiers cachedModifiers) && cachedModifiers != null)
            return cachedModifiers;

        MovementModifiers modifiers = other.GetComponentInParent<MovementModifiers>();
        if (modifiers != null)
            _modifierCache[other] = modifiers;
        else
            _modifierCache.Remove(other);

        return modifiers;
    }
}
