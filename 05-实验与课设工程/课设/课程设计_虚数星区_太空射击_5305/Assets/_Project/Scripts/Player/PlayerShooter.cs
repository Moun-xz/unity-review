using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 玩家射击与武器切换。
    /// 两种武器的差异完全由 WeaponConfig 描述，这里只有一套发射流程：
    /// 按射速计时 → 每个炮口取一颗子弹 → 赋予初速。
    /// 连射武器的"连续"不是循环发数，就是射速本身（0.075 秒一发）；
    /// </summary>
    public class PlayerShooter : MonoBehaviour
    {
        public WeaponConfig[] weapons;

        private int currentIndex;
        private float fireTimer;
        private float switchCooldown;
        private Transform[] muzzles;
        private PlayerBeam beam;

        // 船只加成：武器数值是共用的，船决定「打得快还是打得疼」
        private float damageScale = 1f;
        private float fireRateScale = 1f;

        public int CurrentIndex { get { return currentIndex; } }
        public WeaponConfig Current
        {
            get { return weapons != null && weapons.Length > 0 ? weapons[currentIndex] : null; }
        }

        /// <summary>当前武器显示名，随事件一起广播给界面</summary>
        private string CurrentName
        {
            get
            {
                WeaponConfig weapon = Current;
                return weapon != null ? weapon.displayName : string.Empty;
            }
        }

        private void Awake()
        {
            // 双炮口位置按素材飞船（112×75 像素、100 PPU）的机炮挂点取：
            // 图里的两组黄色炮管大约在 x=±0.30、y=-0.05 处。
            muzzles = new Transform[2];
            muzzles[0] = CreateMuzzle("MuzzleL", new Vector3(-0.30f, -0.05f, 0f));
            muzzles[1] = CreateMuzzle("MuzzleR", new Vector3(0.30f, -0.05f, 0f));

            // 连续光束挂在机身下，不挂在炮口上：
            // 两根光束并排会读成"两条"，而激光应该是一道
            beam = PlayerBeam.Create(transform);
        }

        private Transform CreateMuzzle(string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        public void Setup(WeaponConfig[] configs)
        {
            weapons = configs;
            currentIndex = 0;
            ApplyWeapon();
        }

        /// <summary>
        /// 套用战机加成。由 ObjectFactory 在造玩家时调用一次。
        ///
        /// 炮口位置也要跟着改：每艘船机炮挂点不一样（0.22~0.30），
        /// 沿用同一组坐标的话，换船后子弹会从机身里冒出来或者从机身外面飞出来。
        /// </summary>
        public void ApplyShip(ShipConfig ship)
        {
            if (ship == null) return;

            damageScale = Mathf.Max(0.1f, ship.damageScale);
            fireRateScale = Mathf.Max(0.1f, ship.fireRateScale);

            if (muzzles == null) return;

            for (int i = 0; i < muzzles.Length; i++)
            {
                if (muzzles[i] == null) continue;

                float x = (i == 0 ? -1f : 1f) * ship.muzzleSpread;
                Vector3 local = muzzles[i].localPosition;
                muzzles[i].localPosition = new Vector3(x, local.y, local.z);
            }

            // 光束伤害吃 damageScale，换船后必须重新推一遍
            ApplyWeapon();
        }

        public void ResetForGame()
        {
            currentIndex = 0;
            fireTimer = 0f;
            ApplyWeapon();
            if (beam != null) beam.ResetForGame();
            EventBus.RaiseWeapon(currentIndex, CurrentName);
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (weapons == null || weapons.Length == 0) return;

            // 武器切换：1/2、Q/E、小键盘 1/2、鼠标滚轮、Tab 循环。
            // 原来只认主键盘的 1/2——笔记本没小键盘、玩家也没被告知，
            // 结果"切了没反应"。现在凡是手边能想到的键都能切。
            //
            // 加 0.16 秒冷却：滚轮的一格在有些鼠标上会连着报好几帧，
            // 两把武器的话连切两次正好切回去，看起来就是"没生效"。
            if (switchCooldown > 0f) switchCooldown -= Time.deltaTime;

            if (switchCooldown <= 0f)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ||
                    Input.GetKeyDown(KeyCode.Q)) SwitchTo(0);

                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ||
                    Input.GetKeyDown(KeyCode.E)) SwitchTo(1);

                if (Input.GetKeyDown(KeyCode.Tab)) SwitchTo(currentIndex + 1);

                float wheel = Input.mouseScrollDelta.y;
                if (wheel > 0.01f) SwitchTo(currentIndex - 1);
                else if (wheel < -0.01f) SwitchTo(currentIndex + 1);
            }

            if (fireTimer > 0f) fireTimer -= Time.deltaTime;

            bool firing = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);

            WeaponConfig weapon = Current;
            if (weapon != null && weapon.kind == WeaponKind.Laser)
            {
                // 光束：按住就是一直亮着，松开才断
                if (beam != null) beam.SetFiring(firing);
                return;
            }

            if (beam != null) beam.SetFiring(false);
            if (firing && fireTimer <= 0f) Fire();
        }

        public void SwitchTo(int index)
        {
            if (weapons == null || weapons.Length == 0) return;

            // 取模而不是拦掉：Tab 和滚轮要能绕回来
            index = ((index % weapons.Length) + weapons.Length) % weapons.Length;
            if (index == currentIndex) return;

            currentIndex = index;
            ApplyWeapon();
            switchCooldown = 0.16f;

            EventBus.RaiseWeapon(currentIndex, CurrentName);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            // 切武器给个手感反馈：机身上炸一小圈同色光点。
            // 不然只有 HUD 一行小字变了，切了没切一样。
            if (FXManager.Instance != null)
            {
                WeaponConfig weapon = Current;
                Color c = weapon != null ? weapon.bulletColor : Color.white;
                FXManager.Instance.SpawnHit(transform.position, c, 0.55f);
            }
        }

        /// <summary>
        /// 把当前武器的参数推给光束。切武器、换船、重开一局都要调一次：
        /// 光束的 DPS 吃船只的 damageScale，漏掉这一步换船后光束伤害就不变了。
        /// </summary>
        private void ApplyWeapon()
        {
            WeaponConfig weapon = Current;
            if (weapon == null || beam == null) return;

            beam.Configure(weapon.beamDps * damageScale, weapon.bulletColor);
        }

        private void Fire()
        {
            WeaponConfig weapon = Current;
            if (weapon == null || muzzles == null) return;

            // 累加 + 保底，而不是直接赋值。
            // 0.055 秒的间隔在 60 帧下是 3.3 帧：直接赋值等于每次都补到第 4 帧（0.0667 秒），
            // 射速凭空慢 20%，而且 30/60/144 帧下快慢还不一样。累加把这一帧多出来的时间带到下一发。
            // 保底 0 是为了"憋了很久再开火"：fireTimer 在待机时一路减到很负，
            // 不夹住的话会一次把这攒下的负时间全部补射出去。
            fireTimer += weapon.fireRate * fireRateScale;
            if (fireTimer < 0f) fireTimer = 0f;

            if (AudioManager.Instance != null)
            {
                if (weapon.kind == WeaponKind.Laser) AudioManager.Instance.PlayLaser();
                else AudioManager.Instance.PlaySpread();
            }

            // 两个炮口各打一发，弹道**平行**向上，不做扇形。
            // 机炮是连射：按住就是两条不断线的弹链，每 0.055 秒补一对。
            // 弹道平行而不是扇形——扇形六条弹道互相插空，远处糊成一团，
            // 玩家也判断不出自己这条线能不能打中；平行双轨把两门炮的间距直接画在屏幕上。
            string key = PoolKeys.BulletOf(weapon.kind);

            for (int m = 0; m < muzzles.Length; m++)
            {
                Transform muzzle = muzzles[m];
                Vector3 origin = muzzle != null ? muzzle.position : transform.position;

                GameObject go = PoolManager.Instance.Spawn(key, origin, Quaternion.identity);
                if (go != null)
                {
                    BulletBase bullet = go.GetComponent<BulletBase>();
                    if (bullet != null)
                        bullet.Launch(Vector2.up, weapon.damage * damageScale,
                                      weapon.bulletSpeed, weapon.bulletLife, true);
                }

                if (FXManager.Instance != null) FXManager.Instance.SpawnMuzzle(origin, weapon.bulletColor);
            }

            // 后坐力和震屏按射速折算：连射一秒十几发，每发都给"一轮齐射"的力度，
            // 画面会一直在抖、机身也会一路往下沉。间隔越短、单发越轻，连续射击的总量才不变。
            float kick = Mathf.Clamp01(weapon.fireRate / 0.45f);

            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.05f * kick, 0.06f);
            transform.position += Vector3.down * (0.02f * kick);
        }
    }
}
