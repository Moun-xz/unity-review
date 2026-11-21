using UnityEngine;

/// <summary>
/// 挂在攻击判定框上：命中对手时播放受伤动画 + 打击音效 + 火花粒子。
/// </summary>
public class HitboxTrigger : MonoBehaviour
{
    [Tooltip("被打中时要触发受伤动画的那个 Animator（也就是对手的 Animator）")]
    public Animator enemyAnimator;

    private void OnTriggerEnter(Collider other)
    {
        // 只处理对手：必须带 Player 标签，且不能是自己身上的碰撞体
        if (!other.CompareTag("Player")) return;
        if (other.transform.root == transform.root) return;

        // 取判定框与对手碰撞体之间最近的那个点作为命中位置，火花更贴合
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        // 1) 打击音效 + 火花粒子
        HitFeedback.PlayHit(hitPoint);

        // 2) 对手受伤动画
        if (enemyAnimator == null)
        {
            Debug.LogWarning(gameObject.name + "：enemyAnimator 未绑定，无法播放受伤动画");
            return;
        }

        Debug.Log("=====击中对手=====");
        enemyAnimator.ResetTrigger("Hurt");
        enemyAnimator.SetTrigger("Hurt");
    }
}
