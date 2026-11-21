using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 玩家移动控制。
    ///
    /// 手感三要素：
    /// 1. 加速而不是瞬移——MoveTowards 让速度平滑逼近目标速度，起步和刹车都有惯性；
    /// 2. 斜向不加速——输入向量长度归一化，避免斜着走比直着走快约 41%；
    /// 3. 横向移动时机身轻微倾斜，给玩家"在开飞船"的反馈。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("最大移动速度（单位/秒）")]
        public float moveSpeed = 8f;

        [Tooltip("加速度，越大手感越紧")]
        public float acceleration = 55f;

        [Tooltip("横向移动时的最大倾斜角度")]
        public float maxTilt = 16f;

        private Rigidbody2D rb;
        private Vector2 currentVelocity;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            // 只有在战斗状态下才响应移动，菜单/暂停时把速度清零，避免飞船继续滑行
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                currentVelocity = Vector2.zero;
                if (rb != null) rb.velocity = Vector2.zero;
                return;
            }

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector2 target = input * moveSpeed;
            currentVelocity = Vector2.MoveTowards(currentVelocity, target, acceleration * Time.deltaTime);
            rb.velocity = currentVelocity;

            float tilt = -currentVelocity.x / Mathf.Max(0.01f, moveSpeed) * maxTilt;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, 0f, tilt), Time.deltaTime * 10f);

            // 边界限制：钳制位置而不是用碰撞体挡住，手感更"贴边"而不是"顶墙"
            Vector3 clamped = GameArea.ClampPlayer(transform.position);
            if (clamped != transform.position) transform.position = clamped;
        }
    }
}
