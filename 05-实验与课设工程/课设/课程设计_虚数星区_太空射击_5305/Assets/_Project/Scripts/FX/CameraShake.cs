using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 镜头震动。用"创伤值 + 自动衰减"的方式实现：
    /// 每次触发只提高创伤值，位移量由创伤值乘以随机方向得到，因此连续受击会叠加但不会失控。
    /// 用 unscaledDeltaTime 衰减，保证暂停（timeScale = 0）时震动也能正常停下。
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Tooltip("最大偏移距离（世界单位）")]
        public float maxOffset = 0.45f;

        [Tooltip("每秒衰减多少创伤值")]
        public float decayPerSecond = 2.8f;

        private Vector3 basePosition;
        private float trauma;

        private void Awake()
        {
            Instance = this;
            basePosition = transform.localPosition;
        }

        /// <summary>摄像机被 Bootstrap 摆好位置后调用，记录静止位置作为震动基准</summary>
        public void CaptureBasePosition()
        {
            basePosition = transform.localPosition;
        }

        public void Shake(float amount, float duration)
        {
            trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));
        }

        private void LateUpdate()
        {
            if (trauma > 0f)
            {
                trauma -= decayPerSecond * Time.unscaledDeltaTime;
                if (trauma < 0f) trauma = 0f;

                Vector2 offset = Random.insideUnitCircle * trauma * maxOffset;
                transform.localPosition = basePosition + new Vector3(offset.x, offset.y, 0f);
            }
            else if (transform.localPosition != basePosition)
            {
                transform.localPosition = basePosition;
            }
        }
    }
}
