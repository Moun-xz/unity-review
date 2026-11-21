using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家二控制器：方向键移动，K 或 小键盘 0 攻击。
/// 结构与 Controller1 完全对称，方便两人同屏对打。
/// </summary>
public class Controller2 : MonoBehaviour
{
    public Animator anim;
    public GameObject hitBox;
    public float moveSpeed = 3f;
    public float attackDelay = 0.15f;
    public float hitActiveTime = 0.2f;
    public float attackCooldown = 0.85f;

    private bool isAttacking = false;

    void Update()
    {
        // 玩家二：方向键
        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) h = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) h = 1f;
        if (Input.GetKey(KeyCode.DownArrow)) v = -1f;
        if (Input.GetKey(KeyCode.UpArrow)) v = 1f;

        Vector3 moveDir = new Vector3(h, 0f, v);
        if (moveDir.sqrMagnitude > 0.01f)
        {
            moveDir.Normalize();
            transform.forward = moveDir;
            transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
        }
        if (anim != null) anim.SetBool("IsWalk", moveDir.sqrMagnitude > 0.01f);

        // 玩家二攻击：K 或 小键盘 0
        if ((Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Keypad0)) && !isAttacking)
        {
            Attack();
        }
    }

    void Attack()
    {
        isAttacking = true;
        if (anim != null) anim.SetTrigger("Attack");
        HitFeedback.PlayWhoosh();
        StartCoroutine(HitboxCoroutine());
        Invoke(nameof(ResetAttack), attackCooldown);
    }

    IEnumerator HitboxCoroutine()
    {
        yield return new WaitForSeconds(attackDelay);
        if (hitBox != null) hitBox.SetActive(true);
        yield return new WaitForSeconds(hitActiveTime);
        if (hitBox != null) hitBox.SetActive(false);
    }

    void ResetAttack()
    {
        isAttacking = false;
    }
}
