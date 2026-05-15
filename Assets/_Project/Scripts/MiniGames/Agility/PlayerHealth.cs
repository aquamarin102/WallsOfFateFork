using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int MaxHp = 3;
    public int Hp = 3;

    public bool IsDead => Hp <= 0;
    public bool IsInvulnerable => Time.time < _invulnerableUntil;

    public event Action<int, int> OnHpChanged; // (hp, max)
    public event Action OnDied;

    private float _iFrames = 0.8f;
    private float _invulnerableUntil;

    public void ResetTo(int hp, float iFramesSeconds)
    {
        MaxHp = hp;
        Hp = hp;
        _iFrames = iFramesSeconds;
        _invulnerableUntil = 0f;
        OnHpChanged?.Invoke(Hp, MaxHp);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvulnerable) return;

        Hp = Mathf.Max(0, Hp - amount);
        OnHpChanged?.Invoke(Hp, MaxHp);

        if (Hp <= 0)
        {
            OnDied?.Invoke();
            return;
        }

        _invulnerableUntil = Time.time + Mathf.Max(0f, _iFrames);
    }
}
