using System;
using UnityEngine;

namespace NucleoInstavel
{
    [CreateAssetMenu(menuName = "Nucleo Instavel/Configuração")]
    public sealed class GameSettings : ScriptableObject
    {
        [SerializeField]
        private float durationSeconds = 60f;

        [SerializeField]
        private int maxHealth = 3;

        [SerializeField]
        private float moveSpeed = 5f,
            focusMoveSpeed = 2.5f;

        [SerializeField]
        private float invulnerabilitySeconds = 1f,
            bulletLifetimeSeconds = 8f;

        [SerializeField]
        private int pointsPerSecond = 10,
            bonusPerRemainingHealth = 100;

        [SerializeField]
        private int poolPrewarmCount = 128,
            maxActiveBullets = 256;

        [
            SerializeField,
            Range(1f, 3f),
            Tooltip(
                "1 = balanceamento original; aumenta a pressão em todas as fases sem alterar os dados base."
            )
        ]
        private float difficultyMultiplier = 1.7f;

        [SerializeField]
        private PhaseSettings[] phases =
        {
            new PhaseSettings
            {
                startTime = 0,
                enemyMoveSpeed = 1,
                primaryPattern = new PatternSettings
                {
                    enabled = true,
                    bulletCount = 24,
                    intervalSeconds = 1.6f,
                    bulletSpeed = 2.2f,
                    initialAngleDegrees = -112.5f,
                    gapSlots = 4,
                    firstShotDelaySeconds = .8f,
                },
            },
            new PhaseSettings
            {
                startTime = 20,
                enemyMoveSpeed = 1.5f,
                primaryPattern = new PatternSettings
                {
                    enabled = true,
                    bulletCount = 30,
                    intervalSeconds = 1.4f,
                    bulletSpeed = 2.5f,
                    initialAngleDegrees = -114,
                    rotationPerVolleyDegrees = 12,
                    gapSlots = 5,
                    firstShotDelaySeconds = .9f,
                },
            },
            new PhaseSettings
            {
                startTime = 40,
                movementMode = EnemyMoveMode.TrackPlayerX,
                enemyMoveSpeed = 1.2f,
                primaryPattern = new PatternSettings
                {
                    enabled = true,
                    bulletCount = 36,
                    intervalSeconds = 1.3f,
                    bulletSpeed = 2.8f,
                    initialAngleDegrees = -115,
                    rotationPerVolleyDegrees = -10,
                    gapSlots = 6,
                    firstShotDelaySeconds = 1,
                },
                secondaryPattern = new PatternSettings
                {
                    enabled = true,
                    patternType = BulletPattern.AimedFan,
                    bulletCount = 5,
                    intervalSeconds = 2.8f,
                    bulletSpeed = 3,
                    initialAngleDegrees = -90,
                    fanSpreadDegrees = 40,
                    firstShotDelaySeconds = 2.4f,
                },
            },
        };

        public float DurationSeconds => durationSeconds;
        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float FocusMoveSpeed => focusMoveSpeed;
        public float InvulnerabilitySeconds => invulnerabilitySeconds;
        public float BulletLifetimeSeconds => bulletLifetimeSeconds;
        public int PointsPerSecond => pointsPerSecond;
        public int BonusPerRemainingHealth => bonusPerRemainingHealth;
        public int PoolPrewarmCount => poolPrewarmCount;
        public int MaxActiveBullets => maxActiveBullets;
        public float DifficultyMultiplier => difficultyMultiplier;
        public int PhaseCount => phases == null ? 0 : phases.Length;

        public int GetPhaseIndex(float elapsedSeconds)
        {
            if (PhaseCount == 0)
                throw new InvalidOperationException("Configure ao menos uma fase.");

            int index = 0;

            for (int i = 1; i < phases.Length && phases[i].startTime <= elapsedSeconds; i++)
                index = i;

            return index;
        }

        public PhaseSettings GetPhase(int index)
        {
            // Return scaled copies: existing assets and Inspector edits remain the source data.
            var phase = phases[index];

            phase.enemyMoveSpeed *= difficultyMultiplier;
            phase.primaryPattern = ScalePattern(phase.primaryPattern);
            phase.secondaryPattern = ScalePattern(phase.secondaryPattern);

            return phase;
        }

        private PatternSettings ScalePattern(PatternSettings pattern)
        {
            if (!pattern.enabled || difficultyMultiplier == 1f)
                return pattern;

            pattern.bulletSpeed *= difficultyMultiplier;
            pattern.intervalSeconds /= difficultyMultiplier;
            pattern.firstShotDelaySeconds /= difficultyMultiplier;

            if (pattern.patternType == BulletPattern.Ring)
            {
                int oldGap = pattern.gapSlots;

                if (oldGap > 0)
                    pattern.gapSlots = Mathf.Max(
                        1,
                        Mathf.RoundToInt(oldGap / difficultyMultiplier),
                        pattern.bulletCount - maxActiveBullets
                    );

                // Keep the opening centered in the same direction when narrowing it.
                pattern.initialAngleDegrees +=
                    (oldGap - pattern.gapSlots) * 180f / pattern.bulletCount;
                pattern.rotationPerVolleyDegrees *= difficultyMultiplier;
            }
            else
            {
                int count = Mathf.Min(
                    maxActiveBullets,
                    Mathf.RoundToInt(pattern.bulletCount * difficultyMultiplier)
                );

                // An odd fan keeps a projectile aimed directly at the player.
                if (pattern.bulletCount % 2 == 1 && count % 2 == 0 && count < maxActiveBullets)
                    count++;

                pattern.bulletCount = count;
            }

            return pattern;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Positive(float value) => Finite(value) && value > 0;

        public bool TryValidate(out string error)
        {
            error = null;

            if (
                !Positive(durationSeconds)
                || maxHealth < 1
                || !Positive(moveSpeed)
                || !Positive(focusMoveSpeed)
                || focusMoveSpeed > moveSpeed
                || !Finite(invulnerabilitySeconds)
                || invulnerabilitySeconds < 0
                || !Positive(bulletLifetimeSeconds)
                || pointsPerSecond < 0
                || bonusPerRemainingHealth < 0
                || maxActiveBullets < 1
                || poolPrewarmCount < 0
                || poolPrewarmCount > maxActiveBullets
                || !Finite(difficultyMultiplier)
                || difficultyMultiplier < 1
                || difficultyMultiplier > 3
            )
                error =
                    "Configuração geral inválida: confira duração, vida, velocidades, proteção, score, capacidade do pool e dificuldade (1 a 3).";
            else if (PhaseCount == 0)
                error = "Adicione fases; a primeira deve iniciar em zero.";
            else
                for (int i = 0; i < phases.Length; i++)
                {
                    var p = phases[i];

                    if (
                        !Finite(p.startTime)
                        || (i == 0 ? p.startTime != 0 : p.startTime <= phases[i - 1].startTime)
                        || p.startTime >= durationSeconds
                        || !Positive(p.enemyMoveSpeed)
                        || !Enum.IsDefined(typeof(EnemyMoveMode), p.movementMode)
                    )
                    {
                        error =
                            $"Fase {i + 1}: confira início crescente e velocidade/modo do inimigo.";

                        break;
                    }

                    if (!ValidatePattern(p.primaryPattern) || !ValidatePattern(p.secondaryPattern))
                    {
                        error =
                            $"Fase {i + 1}: padrão inválido. Confira contagem, abertura, capacidade, velocidade e intervalos.";

                        break;
                    }

                    var scaled = GetPhase(i);

                    if (
                        !Positive(scaled.enemyMoveSpeed)
                        || !ValidatePattern(scaled.primaryPattern)
                        || !ValidatePattern(scaled.secondaryPattern)
                    )
                    {
                        error =
                            $"Fase {i + 1}: parâmetros fora dos limites após aplicar a dificuldade.";

                        break;
                    }
                }

            return error == null;
        }

        private bool ValidatePattern(PatternSettings p)
        {
            if (!p.enabled)
                return true;

            if (
                !Enum.IsDefined(typeof(BulletPattern), p.patternType)
                || p.bulletCount < 1
                || !Positive(p.intervalSeconds)
                || !Positive(p.bulletSpeed)
                || !Finite(p.initialAngleDegrees)
                || !Finite(p.rotationPerVolleyDegrees)
                || !Finite(p.firstShotDelaySeconds)
                || p.firstShotDelaySeconds < 0
            )
                return false;

            if (p.patternType == BulletPattern.Ring)
                return p.bulletCount >= 3
                    && p.gapSlots >= 0
                    && p.gapSlots < p.bulletCount
                    && p.bulletCount - p.gapSlots <= maxActiveBullets;

            return Finite(p.fanSpreadDegrees)
                && p.fanSpreadDegrees >= 0
                && p.fanSpreadDegrees <= 180
                && p.bulletCount <= maxActiveBullets;
        }

        private void OnValidate()
        {
            if (!TryValidate(out var error))
                Debug.LogWarning(error, this);
        }
    }
}
