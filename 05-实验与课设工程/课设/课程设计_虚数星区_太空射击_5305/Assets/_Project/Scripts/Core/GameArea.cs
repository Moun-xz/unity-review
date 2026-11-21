using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 战场边界。所有「飞出屏幕就回收」的判断都走这里，
    /// 而不是在各处写死 ±10 之类的魔法数字——以后改分辨率只改一处。
    /// </summary>
    public static class GameArea
    {
        public static float HalfWidth { get; private set; }
        public static float HalfHeight { get; private set; }

        /// <summary>玩家可活动的下边（留一点边距，避免贴屏幕边）</summary>
        public static float PlayerMinY { get { return -HalfHeight + 0.9f; } }

        /// <summary>玩家可活动的上边（只占屏幕下半区，给敌人留空间）</summary>
        public static float PlayerMaxY { get { return HalfHeight * 0.42f; } }

        public static float PlayerLimitX { get { return HalfWidth - 1.0f; } }

        public static void Refresh(Camera cam)
        {
            if (cam == null) { HalfHeight = 5.6f; HalfWidth = 9.95f; return; }
            HalfHeight = cam.orthographicSize;
            HalfWidth = HalfHeight * ((float)Screen.width / Mathf.Max(1, Screen.height));
        }

        /// <summary>把位置钳制在玩家活动区内</summary>
        public static Vector3 ClampPlayer(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, -PlayerLimitX, PlayerLimitX);
            position.y = Mathf.Clamp(position.y, PlayerMinY, PlayerMaxY);
            return position;
        }

        /// <summary>判断某点是否已经飞出可视区（用于回收子弹与敌机）</summary>
        public static bool IsOutside(Vector3 position, float margin)
        {
            return position.x < -HalfWidth - margin
                || position.x > HalfWidth + margin
                || position.y < -HalfHeight - margin
                || position.y > HalfHeight + margin;
        }

        /// <summary>屏幕顶部随机取一个出生点</summary>
        public static Vector3 RandomSpawnPoint(float insideMargin)
        {
            float x = Random.Range(-HalfWidth + insideMargin, HalfWidth - insideMargin);
            float y = HalfHeight + 0.8f;
            return new Vector3(x, y, 0f);
        }
    }
}
