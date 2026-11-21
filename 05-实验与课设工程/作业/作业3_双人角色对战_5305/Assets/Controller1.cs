using System.Collections;
using UnityEngine;

/// <summary>
/// 玩家一控制器：WASD 移动，J 或 鼠标左键 攻击。
/// （改用固定键位是为了和玩家二的方向键区分开，支持两人同屏对打）
/// </summary>
public class Controller1 : MonoBehaviour
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
        // 玩家一：WASD
        float h = 0f, v = 0f;
        if (Input.GetKey(KeyCode.A)) h = -1f;
        if (Input.GetKey(KeyCode.D)) h = 1f;
        if (Input.GetKey(KeyCode.S)) v = -1f;
        if (Input.GetKey(KeyCode.W)) v = 1f;

        Vector3 moveDir = new Vector3(h, 0f, v);
        if (moveDir.sqrMagnitude > 0.01f)
        {
            moveDir.Normalize();
            transform.forward = moveDir;
            transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);
        }
        if (anim != null) anim.SetBool("IsWalk", moveDir.sqrMagnitude > 0.01f);

        // 玩家一攻击：J 或 鼠标左键
        if ((Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0)) && !isAttacking)
        {
            Attack();
        }
    }

    void Attack()
    {
        isAttacking = true;
        if (anim != null) anim.SetTrigger("Attack");
        HitFeedback.PlayWhoosh();          // 出拳破空声
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




