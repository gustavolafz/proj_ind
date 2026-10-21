using System;
using UnityEngine;

namespace NucleoInstavel
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField]
        private GameSettings settings;

        private bool damageEnabled;
        private float invulnerableUntilTime;

        public int CurrentHealth { get; private set; }
        public int MaxHealth => settings ? settings.MaxHealth : 0;
        public bool IsInvulnerable => CurrentHealth > 0 && Time.time < invulnerableUntilTime;

        public event Action<int, int> HealthChanged;
        public event Action Damaged,
            Died;

        public bool ValidateSetup(out string error)
        {
            error = settings ? null : "PlayerHealth: conecte GameSettings.";

            return error == null;
        }

        public bool TryTakeHit()
        {
            if (!damageEnabled || CurrentHealth <= 0 || IsInvulnerable)
                return false;

            CurrentHealth--;
            invulnerableUntilTime = Time.time + settings.InvulnerabilitySeconds;

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Damaged?.Invoke();

            if (CurrentHealth == 0)
                Died?.Invoke();

            return true;
        }

        public void ResetHealth()
        {
            CurrentHealth = MaxHealth;
            invulnerableUntilTime = 0;

            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetDamageEnabled(bool value) => damageEnabled = value;
    }
}
