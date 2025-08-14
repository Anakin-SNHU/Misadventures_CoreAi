using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHP = 100;
    public int currentHP;

    void Awake()
    {
        currentHP = maxHP;
        // Ensure we have a collider for proximity checks
        if (!TryGetComponent<Collider>(out _)) gameObject.AddComponent<CapsuleCollider>();
        // Optional: tag for clarity
        gameObject.tag = "Player";
    }

    public void TakeDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - amount);
        if (currentHP == 0) Die();
    }

    void Die()
    {
        Debug.Log($"Player {name} died.");
        Destroy(gameObject);
    }
}
