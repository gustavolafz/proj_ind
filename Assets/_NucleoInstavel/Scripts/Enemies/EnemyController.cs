using UnityEngine;

namespace NucleoInstavel
{
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField]
        private PlayerMotor player;

        [SerializeField]
        private ArenaBounds arena;

        [SerializeField]
        private BulletEmitter primaryEmitter,
            secondaryEmitter;

        private PhaseSettings phase;
        private float direction = 1;

        public bool ValidateSetup(out string error)
        {
            error =
                player
                && arena
                && primaryEmitter
                && secondaryEmitter
                && primaryEmitter != secondaryEmitter
                    ? null
                    : "Enemy: conecte jogador, arena e dois emissores distintos.";

            if (error != null)
                return false;

            return primaryEmitter.ValidateSetup(out error)
                && secondaryEmitter.ValidateSetup(out error);
        }

        public void ConfigurePhase(PhaseSettings value)
        {
            phase = value;

            primaryEmitter.Configure(value.primaryPattern);
            secondaryEmitter.Configure(value.secondaryPattern);
        }

        public void Tick(float deltaTime)
        {
            if (phase.movementMode == EnemyMoveMode.Patrol)
                MovePatrol(deltaTime);
            else
                MoveTracking(deltaTime);

            primaryEmitter.Tick(deltaTime, player.Position);
            secondaryEmitter.Tick(deltaTime, player.Position);
        }

        public void ResetForRun()
        {
            transform.position = arena.EnemySpawnPosition;
            direction = 1;

            StopAttacks();
        }

        public void StopAttacks()
        {
            primaryEmitter.Stop();
            secondaryEmitter.Stop();
        }

        private void MovePatrol(float dt)
        {
            float x = transform.position.x + direction * phase.enemyMoveSpeed * dt;
            float clamped = arena.ClampEnemyX(x);

            if (x != clamped)
                direction = -direction;

            transform.position = new Vector3(clamped, arena.EnemySpawnPosition.y, 0);
        }

        private void MoveTracking(float dt) =>
            transform.position = new Vector3(
                Mathf.MoveTowards(
                    transform.position.x,
                    arena.ClampEnemyX(player.Position.x),
                    phase.enemyMoveSpeed * dt
                ),
                arena.EnemySpawnPosition.y,
                0
            );
    }
}
