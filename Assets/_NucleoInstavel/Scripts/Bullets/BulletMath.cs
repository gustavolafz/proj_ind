using System;
using UnityEngine;

namespace NucleoInstavel
{
    public static class BulletMath
    {
        public static Vector2 DirectionFromAngle(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                throw new ArgumentOutOfRangeException(nameof(degrees));

            float rad = (degrees % 360f) * Mathf.Deg2Rad;

            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        public static Vector2 GetRingDirection(int index, int count, float startAngle)
        {
            if (count < 1 || index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return DirectionFromAngle(startAngle + index * (360f / count));
        }

        public static float GetAimAngle(Vector2 origin, Vector2 target, float fallbackAngle)
        {
            Vector2 delta = target - origin;

            return delta.sqrMagnitude < .000001f
                ? fallbackAngle
                : Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        public static Vector2 GetFanDirection(int index, int count, float centerAngle, float spread)
        {
            if (
                count < 1
                || index < 0
                || index >= count
                || float.IsNaN(spread)
                || spread < 0
                || spread > 180
            )
                throw new ArgumentOutOfRangeException(nameof(index));

            return DirectionFromAngle(
                count == 1 ? centerAngle : centerAngle - spread / 2 + index * spread / (count - 1)
            );
        }
    }
}
