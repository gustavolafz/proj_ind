using UnityEngine;

namespace NucleoInstavel
{
    public sealed class ArenaBounds : MonoBehaviour
    {
        [SerializeField]
        private Rect playerArea = new Rect(-8, -4.5f, 16, 7);

        [SerializeField]
        private Rect bulletArea = new Rect(-11, -8, 22, 16);

        [SerializeField]
        private float enemyMinX = -5.5f,
            enemyMaxX = 5.5f,
            enemyY = 3.8f;

        [SerializeField]
        private Transform playerSpawn,
            enemySpawn;

        [SerializeField]
        private Camera worldCamera;

        private float lastAspect = -1;

        public Vector2 PlayerSpawnPosition => playerSpawn.position;
        public Vector2 EnemySpawnPosition => enemySpawn.position;

        public Vector2 ClampPlayerPosition(Vector2 p, float radius) =>
            new Vector2(
                Mathf.Clamp(p.x, playerArea.xMin + radius, playerArea.xMax - radius),
                Mathf.Clamp(p.y, playerArea.yMin + radius, playerArea.yMax - radius)
            );

        public float ClampEnemyX(float x) => Mathf.Clamp(x, enemyMinX, enemyMaxX);

        public bool IsOutsideBulletArea(Vector2 p) =>
            p.x < bulletArea.xMin
            || p.x > bulletArea.xMax
            || p.y < bulletArea.yMin
            || p.y > bulletArea.yMax;

        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);

        private static bool ValidRect(Rect r) =>
            Finite(r.x)
            && Finite(r.y)
            && Finite(r.width)
            && Finite(r.height)
            && r.width > .28f
            && r.height > .28f;

        public bool TryValidate(out string error)
        {
            error = null;

            if (!playerSpawn || !enemySpawn || !worldCamera)
                error = "Arena: conecte spawns e câmera.";
            else if (
                !ValidRect(playerArea)
                || !ValidRect(bulletArea)
                || !Finite(enemyMinX)
                || !Finite(enemyMaxX)
                || !Finite(enemyY)
                || enemyMinX >= enemyMaxX
                || enemyY <= playerArea.yMax
                || bulletArea.xMin > playerArea.xMin
                || bulletArea.xMax < playerArea.xMax
                || bulletArea.yMin > playerArea.yMin
                || bulletArea.yMax < enemyY
            )
                error = "Arena: retângulos ou faixa do inimigo inválidos.";
            else if (
                !Finite(playerSpawn.position.x)
                || !Finite(playerSpawn.position.y)
                || !Finite(enemySpawn.position.x)
                || !Finite(enemySpawn.position.y)
                || Vector2.Distance(
                    PlayerSpawnPosition,
                    ClampPlayerPosition(PlayerSpawnPosition, .14f)
                ) > .001f
                || EnemySpawnPosition.x < enemyMinX
                || EnemySpawnPosition.x > enemyMaxX
                || Mathf.Abs(EnemySpawnPosition.y - enemyY) > .001f
                || IsOutsideBulletArea(EnemySpawnPosition)
            )
                error = "Arena: reposicione spawns dentro das áreas válidas.";

            return error == null;
        }

        private void LateUpdate()
        {
            if (worldCamera && !Mathf.Approximately(lastAspect, worldCamera.aspect))
                FitCameraToViewport();
        }

        public void FitCameraToViewport()
        {
            if (!worldCamera || worldCamera.aspect <= 0)
                return;

            lastAspect = worldCamera.aspect;
            worldCamera.orthographic = true;
            worldCamera.orthographicSize = Mathf.Max(5.5f, (5.5f * 16f / 9f) / lastAspect);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireCube(playerArea.center, playerArea.size);

            Gizmos.color = Color.yellow;

            Gizmos.DrawWireCube(bulletArea.center, bulletArea.size);

            Gizmos.color = Color.magenta;

            Gizmos.DrawLine(new Vector3(enemyMinX, enemyY), new Vector3(enemyMaxX, enemyY));
        }
    }
}
