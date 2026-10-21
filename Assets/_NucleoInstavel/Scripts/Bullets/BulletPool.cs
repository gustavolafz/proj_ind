using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace NucleoInstavel
{
    public sealed class BulletPool : MonoBehaviour
    {
        [SerializeField]
        private EnemyBullet prefab;

        [SerializeField]
        private GameSettings settings;

        [SerializeField]
        private ArenaBounds arena;

        [SerializeField]
        private Transform container;

        private ObjectPool<EnemyBullet> pool;
        private HashSet<EnemyBullet> active;
        private List<EnemyBullet> buffer;

        public int ActiveCount => active == null ? 0 : active.Count;

        public bool ValidateSetup(out string error)
        {
            error =
                prefab
                && settings
                && arena
                && container
                && !prefab.gameObject.activeSelf
                && prefab.GetComponent<Rigidbody2D>()
                && prefab.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic
                && prefab.GetComponent<Rigidbody2D>().gravityScale == 0
                && prefab.GetComponent<Rigidbody2D>().linearDamping == 0
                && prefab.GetComponent<CircleCollider2D>()
                && prefab.GetComponent<CircleCollider2D>().isTrigger
                && prefab.transform.localScale == Vector3.one
                    ? null
                    : "Pool: conecte prefab inativo com corpo Dynamic sem gravidade/atrito e collider trigger; confira settings, arena e container.";

            return error == null;
        }

        public void Initialize()
        {
            if (pool != null)
                return;

            if (!ValidateSetup(out var error))
                throw new System.InvalidOperationException(error);

            active = new HashSet<EnemyBullet>(settings.MaxActiveBullets);
            buffer = new List<EnemyBullet>(settings.MaxActiveBullets);
            pool = new ObjectPool<EnemyBullet>(
                CreateBullet,
                null,
                OnReleaseBullet,
                DestroyBullet,
                true,
                settings.MaxActiveBullets,
                settings.MaxActiveBullets
            );

            Prewarm(settings.PoolPrewarmCount);
        }

        public void Prewarm(int count)
        {
            if (count < 0 || count > settings.MaxActiveBullets || ActiveCount != 0)
                throw new System.ArgumentOutOfRangeException(nameof(count));

            for (int i = 0; i < count; i++)
                buffer.Add(pool.Get());

            foreach (var bullet in buffer)
                pool.Release(bullet);

            buffer.Clear();
        }

        public bool CanSpawn(int count) =>
            pool != null && count > 0 && count <= settings.MaxActiveBullets - ActiveCount;

        public bool TrySpawn(BulletSpawnData data)
        {
            if (!CanSpawn(1))
                return false;

            var bullet = pool.Get();

            active.Add(bullet);
            // Reactivation creates/restores the native 2D body. Assign its pose and
            // velocity afterwards so activation cannot replace the launch state.
            bullet.gameObject.SetActive(true);
            bullet.Launch(data);

            return true;
        }

        public void Release(EnemyBullet bullet)
        {
            if (bullet && active != null && active.Remove(bullet))
                pool.Release(bullet);
        }

        public void ReleaseAll()
        {
            if (pool == null)
                return;

            buffer.Clear();

            foreach (var bullet in active)
                buffer.Add(bullet);

            foreach (var bullet in buffer)
                Release(bullet);

            buffer.Clear();
        }

        private EnemyBullet CreateBullet()
        {
            var bullet = Instantiate(prefab, container);

            bullet.gameObject.SetActive(false);
            bullet.Initialize(this, arena);

            return bullet;
        }

        private void OnReleaseBullet(EnemyBullet bullet)
        {
            bullet.ResetForPool();
            bullet.gameObject.SetActive(false);
        }

        private void DestroyBullet(EnemyBullet bullet)
        {
            if (bullet)
                Destroy(bullet.gameObject);
        }

        private void OnDestroy()
        {
            ReleaseAll();
            pool?.Dispose();

            pool = null;
        }
    }
}
