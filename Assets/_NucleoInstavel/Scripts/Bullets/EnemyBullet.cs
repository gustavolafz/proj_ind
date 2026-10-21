using UnityEngine;

namespace NucleoInstavel
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class EnemyBullet : MonoBehaviour
    {
        private BulletPool owner;
        private ArenaBounds arena;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private float remainingLifetime;

        public bool IsActive { get; private set; }

        public void Initialize(BulletPool pool, ArenaBounds bounds)
        {
            owner = pool;
            arena = bounds;

            CacheComponents();
        }

        private void CacheComponents()
        {
            if (!body)
                body = GetComponent<Rigidbody2D>();

            if (!visual)
                visual = GetComponentInChildren<SpriteRenderer>(true);
        }

        public void Launch(BulletSpawnData data)
        {
            CacheComponents();

            body.position = data.Position;
            body.linearVelocity = data.Velocity;
            body.angularVelocity = 0;
            remainingLifetime = data.LifetimeSeconds;

            if (visual)
                visual.color = Color.white;

            IsActive = true;
        }

        private void FixedUpdate()
        {
            if (!IsActive)
                return;

            remainingLifetime -= Time.fixedDeltaTime;

            if (remainingLifetime <= 0 || arena.IsOutsideBulletArea(body.position))
                Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsActive || !other.TryGetComponent<PlayerHealth>(out var health))
                return;

            Despawn(); // Release before Died can synchronously release every other bullet.
            health.TryTakeHit();
        }

        private void Despawn()
        {
            if (IsActive)
                owner.Release(this);
        }

        public void ResetForPool()
        {
            CacheComponents();

            IsActive = false;
            remainingLifetime = 0;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0;

            if (visual)
                visual.color = Color.white;
        }
    }
}
