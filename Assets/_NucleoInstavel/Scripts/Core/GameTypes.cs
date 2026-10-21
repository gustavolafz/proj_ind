using System;
using UnityEngine;

namespace NucleoInstavel
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        Won,
        Lost,
    }

    public enum EnemyMoveMode
    {
        Patrol,
        TrackPlayerX,
    }

    public enum BulletPattern
    {
        Ring,
        AimedFan,
    }

    public enum SfxId
    {
        UiClick,
        Volley,
        Hit,
        PhaseChange,
        Victory,
        Defeat,
    }

    public readonly struct RunResult
    {
        public bool Won { get; }
        public float ElapsedSeconds { get; }
        public int FinalScore { get; }
        public int RemainingHealth { get; }

        public RunResult(bool won, float elapsed, int score, int health)
        {
            Won = won;
            ElapsedSeconds = elapsed;
            FinalScore = score;
            RemainingHealth = health;
        }
    }

    [Serializable]
    public struct PatternSettings
    {
        public bool enabled;
        public BulletPattern patternType;
        public int bulletCount;
        public float intervalSeconds,
            bulletSpeed,
            initialAngleDegrees,
            rotationPerVolleyDegrees;
        public int gapSlots;
        public float fanSpreadDegrees,
            firstShotDelaySeconds;
    }

    [Serializable]
    public struct PhaseSettings
    {
        public float startTime;
        public EnemyMoveMode movementMode;
        public float enemyMoveSpeed;
        public PatternSettings primaryPattern,
            secondaryPattern;
    }

    public readonly struct BulletSpawnData
    {
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public float LifetimeSeconds { get; }

        public BulletSpawnData(Vector2 position, Vector2 velocity, float lifetime)
        {
            Position = position;
            Velocity = velocity;
            LifetimeSeconds = lifetime;
        }
    }
}
