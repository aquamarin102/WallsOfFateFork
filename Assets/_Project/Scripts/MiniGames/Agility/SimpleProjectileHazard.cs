using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(DamageOnTouch))]
public class SimpleProjectileHazard : MonoBehaviour
{
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private int maxBounces = 0;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.76f, 0.2f);
    [SerializeField] private Color activeColor = new Color(0.85f, 0.2f, 0.1f);

    private Collider _collider;
    private DamageOnTouch _damage;
    private Transform _cachedTransform;
    private Vector3 _direction;
    private Vector3 _arenaCenter;
    private float _arenaRadius;
    private float _arenaRadiusSqr;
    private float _activationDelay;
    private float _lifetimeEndTime;
    private int _bouncesUsed;
    private bool _isActive;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _damage = GetComponent<DamageOnTouch>();
        _cachedTransform = transform;
        _collider.isTrigger = true;
        SetActiveState(false);
    }

    public void Launch(
        Vector3 direction,
        float travelSpeed,
        Vector3 arenaCenter,
        float arenaRadius,
        float activationDelay,
        int allowedBounces,
        float maxLifetime)
    {
        _direction = direction.normalized;
        speed = travelSpeed;
        _arenaCenter = arenaCenter;
        _arenaRadius = arenaRadius;
        _arenaRadiusSqr = arenaRadius * arenaRadius;
        _activationDelay = Mathf.Max(0f, activationDelay);
        maxBounces = Mathf.Max(0, allowedBounces);
        lifetime = Mathf.Max(0.1f, maxLifetime);
        _lifetimeEndTime = Time.time + lifetime;
        _bouncesUsed = 0;
        SetActiveState(_activationDelay <= 0f);
    }

    private void Update()
    {
        if (Time.time >= _lifetimeEndTime)
        {
            Destroy(gameObject);
            return;
        }

        if (!_isActive)
        {
            _activationDelay -= Time.deltaTime;
            if (_activationDelay <= 0f)
                SetActiveState(true);
        }

        Vector3 nextPosition = _cachedTransform.position + _direction * speed * Time.deltaTime;
        Vector3 nextPlanar = nextPosition - _arenaCenter;
        nextPlanar.y = 0f;

        if (nextPlanar.sqrMagnitude > _arenaRadiusSqr)
        {
            if (_bouncesUsed >= maxBounces)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 normal = nextPlanar.normalized;
            _direction = Vector3.Reflect(_direction, normal).normalized;
            nextPosition = _arenaCenter + normal * (_arenaRadius - 0.05f);
            nextPosition.y = _cachedTransform.position.y;
            _bouncesUsed++;
        }

        _cachedTransform.position = nextPosition;
    }

    private void SetActiveState(bool active)
    {
        _isActive = active;
        _collider.enabled = active;
        _damage.enabled = active;
        AgilityHazardFactory.SetColor(gameObject, active ? activeColor : telegraphColor);
    }
}
