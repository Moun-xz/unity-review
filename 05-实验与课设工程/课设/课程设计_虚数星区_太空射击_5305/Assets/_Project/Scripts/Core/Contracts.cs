namespace PixelGravity
{
    /// <summary>
    /// 对象池对象的统一契约。
    /// 约定：OnSpawn 必须把「所有可变状态」重置回初始值，
    /// 否则会出现复用旧数据造成的灵异现象（尤其是子弹的位置、速度和计时器）。
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary>
    /// 可受伤对象。子弹不需要知道打的是敌人还是 Boss，只认这个接口，
    /// 这样以后新增敌人类型不用改子弹代码。
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
        bool IsAlive { get; }
    }
}
