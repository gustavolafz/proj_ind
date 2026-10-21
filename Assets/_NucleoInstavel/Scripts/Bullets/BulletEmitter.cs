using System;
using UnityEngine;

namespace NucleoInstavel
{
    public sealed class BulletEmitter : MonoBehaviour
    {
        [SerializeField]
        private BulletPool pool;

        [SerializeField]
        private GameSettings settings;

        [SerializeField]
        private Transform origin;

        private PatternSettings pattern;
        private float timeUntilNextVolley,
            angle;
        private bool emitting;

        public int SkippedVolleyCount { get; private set; }

        public event Action VolleyEmitted;

        public bool ValidateSetup(out string error)
        {
            error =
                pool && settings && origin
                    ? null
                    : "BulletEmitter: conecte pool, settings e origem.";

            return error == null;
        }

        public void Configure(PatternSettings value)
        {
            pattern = value;
            timeUntilNextVolley = value.firstShotDelaySeconds;
            angle = value.initialAngleDegrees;
            emitting = value.enabled;
            SkippedVolleyCount = 0;
        }

        public void Stop() => emitting = false;

        public void Tick(float deltaTime, Vector2 targetPosition)
        {
            if (!emitting)
                return;

            timeUntilNextVolley -= deltaTime;

            if (timeUntilNextVolley > 0)
                return;

            TryFireVolley(targetPosition);

            timeUntilNextVolley = pattern.intervalSeconds;
        }

        public int GetVolleyBulletCount() =>
            pattern.patternType == BulletPattern.Ring
                ? pattern.bulletCount - pattern.gapSlots
                : pattern.bulletCount;

        public bool TryFireVolley(Vector2 targetPosition)
        {
            if (!emitting)
                return false;

            if (!pool.CanSpawn(GetVolleyBulletCount()))
            {
                SkippedVolleyCount++;

                return false;
            }

            if (pattern.patternType == BulletPattern.Ring)
                EmitRing();
            else
                EmitAimedFan(targetPosition);

            if (pattern.patternType == BulletPattern.Ring)
                angle = Mathf.Repeat(angle + pattern.rotationPerVolleyDegrees, 360);

            VolleyEmitted?.Invoke();

            return true;
        }

        private void EmitRing()
        {
            for (int i = pattern.gapSlots; i < pattern.bulletCount; i++)
                SpawnBullet(BulletMath.GetRingDirection(i, pattern.bulletCount, angle));
        }

        private void EmitAimedFan(Vector2 target)
        {
            float aim = BulletMath.GetAimAngle(
                origin.position,
                target,
                pattern.initialAngleDegrees
            );

            for (int i = 0; i < pattern.bulletCount; i++)
                SpawnBullet(
                    BulletMath.GetFanDirection(
                        i,
                        pattern.bulletCount,
                        aim,
                        pattern.fanSpreadDegrees
                    )
                );
        }

        private void SpawnBullet(Vector2 direction)
        {
            if (
                !pool.TrySpawn(
                    new BulletSpawnData(
                        origin.position,
                        direction * pattern.bulletSpeed,
                        settings.BulletLifetimeSeconds
                    )
                )
            )
                throw new InvalidOperationException(
                    "Capacidade do pool mudou durante emissão síncrona."
                );
        }
    }
}
