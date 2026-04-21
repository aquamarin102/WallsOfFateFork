using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class DamageOnTouch : MonoBehaviour
{
    public int damage = 1;
    public GameObject explosionEffect; // Префаб эффекта взрыва (VFX)

    [Tooltip("Если true — ожидаем, что коллайдер уронщика Trigger.")]
    public bool requireTrigger = true;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (requireTrigger && !GetComponent<Collider>().isTrigger) return;
        explosionEffect = Resources.Load<GameObject>("MiniGames/Agility/Agility HitFX");

        var hp = other.GetComponentInParent<PlayerHealth>();
        if (hp != null)
        {
            hp.TakeDamage(damage);

            // Создание эффекта взрыва в точке контакта
            if (explosionEffect != null)
            {
                Vector3 contactPoint = GetContactPoint(transform, other.transform);
                GameObject effectInstance = Instantiate(explosionEffect, contactPoint, Quaternion.identity);
                StartCoroutine(DestroyAfterDelay(effectInstance, 10f));
            }
        }
    }

    private Vector3 GetContactPoint(Transform triggerTransform, Transform otherTransform)
    {
        // Используем позицию вошедшего объекта как приблизительную точку контакта
        return otherTransform.position;
    }

    private IEnumerator DestroyAfterDelay(GameObject target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null)
            Destroy(target);
    }
}