using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StandaloneCow3D
{
    // 3D 向量結構
    public struct Vec3
    {
        public float x, y, z;
        public Vec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vec3 operator *(Vec3 a, float s) { return new Vec3(a.x * s, a.y * s, a.z * s); }

        public float Length() { return (float)Math.Sqrt(x * x + y * y + z * z); }
        public Vec3 Normalized()
        {
            float len = Length();
            if (len < 0.0001f) return new Vec3(0, 1, 0);
            return new Vec3(x / len, y / len, z / len);
        }

        public static float Dot(Vec3 a, Vec3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }

        public Vec3 RotateX(float rad)
        {
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            return new Vec3(x, y * c - z * s, y * s + z * c);
        }
        public Vec3 RotateY(float rad)
        {
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            return new Vec3(x * c + z * s, y, -x * s + z * c);
        }
    }

    // 3D 渲染面
    public class Face3D : IComparable<Face3D>
    {
        public Vec3[] verts;
        public Color color;
        public float depth;

        public int CompareTo(Face3D other)
        {
            return other.depth.CompareTo(this.depth);
        }
    }

    // 物理物件 (0:粉嶺菜箱, 1:西貢綠的, 2:市區紅的, 3:西貢露營帳篷, 4:追捕警車)
    public class PropObj
    {
        public int type;
        public Vec3 pos;
        public Vec3 vel;
        public float rotY;
        public float rotVel;
    }

    // 子彈結構 (DEEEER Simulator 牛角雙槍)
    public class Bullet3D
    {
        public Vec3 pos;
        public Vec3 vel;
        public float life;
    }

    // 地道美食收集物 (0:西貢菠蘿油, 1:粉嶺有機菜心)
    public class FoodItem
    {
        public int type;
        public Vec3 pos;
    }

    // 3D 粒子碎屑
    public class Particle3D
    {
        public Vec3 pos;
        public Vec3 vel;
        public float size;
        public float life;
        public Color color;
    }

    // 手機模擬器畫面圓形技能掣
    public class MobileButton
    {
        public string id;
        public string title;
        public string keyHint;
        public Rectangle rect;
        public Color baseColor;
        public bool isActive;
    }

    public class GameWindow : Form
    {
        private Timer gameTimer;
        private HashSet<Keys> keys = new HashSet<Keys>();
        private Random rng = new Random();

        // 玩家香港和牛狀態
        private Vec3 cowPos = new Vec3(0, 0, -18f);
        private float cowYaw = 0f;
        private float cowVelY = 0f;
        private bool isGrounded = true;
        private bool isDashing = false;
        private bool isWagyuSkin = true;
        private float stamina = 100f;
        private float walkCycle = 0f;
        private float headNod = 0f;

        // Goat Simulator + DEEEER Simulator 專屬狂暴狀態
        private bool isBipedalDeerMode = false; // 雙腳直立暴走模式
        private bool isDualGunEquipped = false; // 牛角雙格林機槍模式
        private bool isShooting = false;
        private int shootCooldown = 0;
        private bool isJetpackRagdoll = false;  // 噴射背包布偶翻滾
        private float ragdollPitch = 0f;
        private float neckStretch = 0f;         // 伸縮長頸程度
        private PropObj lickedTarget = null;    // 魔舌黐住嘅物件

        // 手機虛擬搖桿與觸控按鈕狀態
        private Point joyCenter = new Point(115, 610);
        private Point joyKnob = new Point(115, 610);
        private bool draggingJoy = false;
        private float joyInputX = 0f;
        private float joyInputY = 0f;
        private List<MobileButton> mobileBtns = new List<MobileButton>();
        private string heldMobileBtnId = "";

        // 鏡頭狀態
        private Vec3 camPos = new Vec3(0, 5.5f, -27f);
        private float camYaw = 0f;
        private float currentFov = 520f;
        private float screenShake = 0f;

        // 遊戲世界物件
        private List<PropObj> props = new List<PropObj>();
        private List<Bullet3D> bullets = new List<Bullet3D>();
        private List<FoodItem> foods = new List<FoodItem>();
        private List<Vec3> trees = new List<Vec3>();
        private List<Vec3> hexRocks = new List<Vec3>();
        private List<Particle3D> particles = new List<Particle3D>();
        private List<Face3D> renderQueue = new List<Face3D>(3500);

        // 計分、牛災等級與 UI
        private int score = 0;
        private int combo = 0;
        private float comboTimer = 0f;
        private float chaosMeter = 0f; // 0 ~ 100 牛災指數
        private string statusMsg = "歡迎玩《香港山羊x鹿模擬器》！試下撳 [F]伸長條脷拉的士、[G]雙腳暴走、[V]牛角雙槍！";
        private Vec3 lightDir = new Vec3(0.45f, 0.82f, -0.35f).Normalized();

        private static readonly int[][] BoxFaces = new int[][] {
            new int[] { 0, 1, 2, 3 },
            new int[] { 4, 5, 6, 7 },
            new int[] { 3, 2, 6, 7 },
            new int[] { 0, 1, 5, 4 },
            new int[] { 0, 3, 7, 4 },
            new int[] { 1, 2, 6, 5 }
        };

        public GameWindow()
        {
            this.Text = "🐐🦌 香港 3D 狂牛模擬器：Goat x DEEEER Simulator 手機狂暴版";
            this.ClientSize = new Size(1240, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;

            this.KeyDown += OnKeyDown;
            this.KeyUp += OnKeyUp;
            this.MouseDown += OnMouseDown;
            this.MouseMove += OnMouseMove;
            this.MouseUp += OnMouseUp;
            this.Resize += delegate { SetupMobileButtons(); };

            SetupMobileButtons();
            InitHongKongWorld();

            gameTimer = new Timer();
            gameTimer.Interval = 16; // ~60 FPS
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void SetupMobileButtons()
        {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            joyCenter = new Point(120, h - 130);
            if (!draggingJoy) joyKnob = joyCenter;

            mobileBtns.Clear();
            int bw = 84, bh = 84;

            // 右下角 6 個手機模擬器大圓掣
            AddMobileBtn("DASH", "💥衝撞", "Shift", w - 300, h - 115, bw, bh, Color.FromArgb(230, 120, 30));
            AddMobileBtn("JUMP", "⬆️跳躍", "Space", w - 200, h - 115, bw, bh, Color.FromArgb(45, 165, 235));
            AddMobileBtn("GUN",  "🔫雙槍", "V鍵",   w - 100, h - 115, bw, bh, Color.FromArgb(220, 45, 55));

            AddMobileBtn("LICK", "👅伸脷", "F鍵",   w - 300, h - 210, bw, bh, Color.FromArgb(235, 85, 155));
            AddMobileBtn("DEER", "🕺雙腳", "G鍵",   w - 200, h - 210, bw, bh, Color.FromArgb(155, 75, 225));
            AddMobileBtn("JET",  "🚀噴射", "Q鍵",   w - 100, h - 210, bw, bh, Color.FromArgb(40, 190, 100));
        }

        private void AddMobileBtn(string id, string title, string hint, int x, int y, int w, int h, Color col)
        {
            MobileButton b = new MobileButton();
            b.id = id;
            b.title = title;
            b.keyHint = hint;
            b.rect = new Rectangle(x, y, w, h);
            b.baseColor = col;
            b.isActive = false;
            mobileBtns.Add(b);
        }

        private void InitHongKongWorld()
        {
            props.Clear();
            bullets.Clear();
            foods.Clear();
            trees.Clear();
            hexRocks.Clear();
            particles.Clear();
            lickedTarget = null;
            chaosMeter = 0f;

            cowPos = new Vec3(0, 0, -20f);
            cowYaw = 0f;
            cowVelY = 0f;

            // 1. 西貢新界綠的車隊
            for (int i = 0; i < 7; i++)
            {
                PropObj taxi = new PropObj();
                taxi.type = 1; // 新界綠的
                taxi.pos = new Vec3(-18f + i * 6f, 0.75f, -12f - (i % 2) * 5f);
                taxi.rotY = (float)(rng.NextDouble() * Math.PI * 2);
                props.Add(taxi);
            }

            // 2. 西貢露營帳篷陣
            for (int i = 0; i < 10; i++)
            {
                PropObj tent = new PropObj();
                tent.type = 3; // 露營帳篷
                tent.pos = new Vec3(-28f + (i % 5) * 5.5f, 0.75f, -26f - (i / 5) * 6f);
                tent.rotY = (float)(rng.NextDouble() * Math.PI);
                props.Add(tent);
            }

            // 3. 西貢萬宜水庫六角火山岩柱石群
            for (int i = 0; i < 14; i++)
            {
                float rx = 22f + (i % 4) * 3.2f;
                float rz = -34f + (i / 4) * 6.5f;
                float height = 4.5f + (float)(rng.NextDouble() * 4.5);
                hexRocks.Add(new Vec3(rx, height, rz));
            }

            // 4. 粉嶺有機菜箱高塔 + 市區紅的
            for (int i = 0; i < 12; i++)
            {
                PropObj crate = new PropObj();
                crate.type = 0;
                if (i < 6)
                    crate.pos = new Vec3(-5f, 0.75f + i * 1.52f, 18f);
                else
                    crate.pos = new Vec3(5f, 0.75f + (i - 6) * 1.52f, 18f);
                props.Add(crate);
            }

            for (int i = 0; i < 10; i++)
            {
                PropObj p = new PropObj();
                p.type = (i % 2 == 0) ? 2 : 0; // 市區紅的 或 菜箱
                p.pos = new Vec3((float)(rng.NextDouble() * 54 - 27), 0.75f, (float)(rng.NextDouble() * 34 + 12));
                p.rotY = (float)(rng.NextDouble() * Math.PI * 2);
                props.Add(p);
            }

            // 5. 巡邏衝鋒警車 (DEEEER Simulator 通緝追捕)
            for (int i = 0; i < 4; i++)
            {
                PropObj cop = new PropObj();
                cop.type = 4; // 警車
                cop.pos = new Vec3(-20f + i * 14f, 0.75f, (i < 2) ? -28f : 28f);
                props.Add(cop);
            }

            // 6. 地道美食 (西貢菠蘿油 & 粉嶺菜心)
            for (int i = 0; i < 14; i++)
            {
                FoodItem f = new FoodItem();
                f.type = (i < 7) ? 0 : 1;
                f.pos = new Vec3(
                    (float)(rng.NextDouble() * 56 - 28),
                    1.0f,
                    (i < 7) ? (float)(rng.NextDouble() * 30 - 36) : (float)(rng.NextDouble() * 36 + 10)
                );
                foods.Add(f);
            }

            // 郊野公園樹木
            for (int i = 0; i < 18; i++)
            {
                float tx = (i % 2 == 0) ? -36f - (float)(rng.NextDouble() * 8) : 34f + (float)(rng.NextDouble() * 8);
                float tz = -35f + i * 4.5f;
                trees.Add(new Vec3(tx, 0f, tz));
            }
        }

        // ==========================================
        // 滑鼠觸控虛擬搖桿與手機技能掣
        // ==========================================
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            // 檢查係咪撳咗右下角手機技能掣
            for (int i = 0; i < mobileBtns.Count; i++)
            {
                if (mobileBtns[i].rect.Contains(e.Location))
                {
                    heldMobileBtnId = mobileBtns[i].id;
                    TriggerMobileAction(heldMobileBtnId, true);
                    return;
                }
            }

            // 檢查係咪拖拉緊左下角虛擬搖桿
            float dx = e.X - joyCenter.X;
            float dy = e.Y - joyCenter.Y;
            if (Math.Sqrt(dx * dx + dy * dy) <= 95)
            {
                draggingJoy = true;
                UpdateJoystick(e.Location);
                return;
            }

            // 點擊畫面其他位置：如果開咗雙槍就即刻開火
            isDualGunEquipped = true;
            isShooting = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (draggingJoy)
            {
                UpdateJoystick(e.Location);
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (draggingJoy)
            {
                draggingJoy = false;
                joyKnob = joyCenter;
                joyInputX = 0f;
                joyInputY = 0f;
            }
            if (!string.IsNullOrEmpty(heldMobileBtnId))
            {
                TriggerMobileAction(heldMobileBtnId, false);
                heldMobileBtnId = "";
            }
            isShooting = false;
        }

        private void UpdateJoystick(Point pt)
        {
            float dx = pt.X - joyCenter.X;
            float dy = pt.Y - joyCenter.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float maxR = 55f;
            if (dist > maxR)
            {
                dx = (dx / dist) * maxR;
                dy = (dy / dist) * maxR;
            }
            joyKnob = new Point((int)(joyCenter.X + dx), (int)(joyCenter.Y + dy));
            joyInputX = dx / maxR;
            joyInputY = -dy / maxR; // 向上推為前進正數
        }

        private void TriggerMobileAction(string id, bool pressed)
        {
            if (id == "JUMP" && pressed && isGrounded)
            {
                cowVelY = 0.38f;
                isGrounded = false;
            }
            else if (id == "LICK" && pressed)
            {
                ToggleTongueGrapple();
            }
            else if (id == "DEER" && pressed)
            {
                ToggleDeerBipedalMode();
            }
            else if (id == "GUN")
            {
                if (pressed) isDualGunEquipped = true;
                isShooting = pressed;
            }
            else if (id == "JET")
            {
                isJetpackRagdoll = pressed;
            }
        }

        // ==========================================
        // 鍵盤操作與 Goat / DEEEER 核心技能
        // ==========================================
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            keys.Add(e.KeyCode);

            if (e.KeyCode == Keys.Space && isGrounded)
            {
                cowVelY = 0.38f;
                isGrounded = false;
                headNod = -0.35f;
            }

            // F 鍵：Goat Simulator 伸長魔舌 + 鹿頸捉車/甩飛
            if (e.KeyCode == Keys.F)
            {
                ToggleTongueGrapple();
            }

            // G 鍵：DEEEER Simulator 雙腳直立暴走模式
            if (e.KeyCode == Keys.G)
            {
                ToggleDeerBipedalMode();
            }

            // V 鍵：裝備/開火 牛角雙格林機槍
            if (e.KeyCode == Keys.V)
            {
                isDualGunEquipped = true;
                isShooting = true;
            }

            // Q 鍵：噴射背包 + 布偶空中翻滾
            if (e.KeyCode == Keys.Q)
            {
                isJetpackRagdoll = true;
            }

            // E 鍵：震地波
            if (e.KeyCode == Keys.E && stamina >= 30f)
            {
                TriggerShockwave();
            }

            // C 鍵：切換 A5 和牛 / 西貢黃牛
            if (e.KeyCode == Keys.C)
            {
                isWagyuSkin = !isWagyuSkin;
                statusMsg = isWagyuSkin ? "✨ 切換為：【香港 A5 霜降和牛】！" : "🐂 切換為：【西貢野生霸王黃牛】！";
                SpawnParticles(cowPos + new Vec3(0, 1.5f, 0), Color.Gold, 18);
            }

            // 1 / 2 鍵：西貢 / 粉嶺傳送
            if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1)
            {
                cowPos = new Vec3(0, 1.5f, -20f);
                statusMsg = "🌊 已瞬間傳送至：【西貢海濱碼頭及露營區】！";
            }
            if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2)
            {
                cowPos = new Vec3(0, 1.5f, 14f);
                statusMsg = "🏯 已瞬間傳送至：【粉嶺龍躍頭圍村及農田區】！";
            }

            if (e.KeyCode == Keys.R)
            {
                InitHongKongWorld();
                statusMsg = "香港地圖已重置！";
            }

            if (e.KeyCode == Keys.F11)
            {
                if (this.FormBorderStyle == FormBorderStyle.None)
                {
                    this.FormBorderStyle = FormBorderStyle.Sizable;
                    this.WindowState = FormWindowState.Normal;
                }
                else
                {
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.WindowState = FormWindowState.Maximized;
                }
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            keys.Remove(e.KeyCode);
            if (e.KeyCode == Keys.V) isShooting = false;
            if (e.KeyCode == Keys.Q) isJetpackRagdoll = false;
        }

        // Goat Simulator 招牌技能：伸長魔舌捉住物件 / 再撳一次大力甩飛上天
        private void ToggleTongueGrapple()
        {
            if (lickedTarget != null)
            {
                // 將吸住嘅車或物件大大力甩飛出去！
                Vec3 fwd = new Vec3((float)Math.Sin(cowYaw), 0.65f, (float)Math.Cos(cowYaw)).Normalized();
                lickedTarget.vel = fwd * 1.15f;
                lickedTarget.rotVel = 0.45f;
                AddScore(80, "👅 魔舌大甩飛！");
                SpawnParticles(lickedTarget.pos, Color.HotPink, 16);
                lickedTarget = null;
                neckStretch = 0f;
                return;
            }

            // 伸長鹿頸同魔舌，尋找最近嘅的士/警車/帳篷/菜箱
            neckStretch = 2.4f;
            float bestDist = 14.5f;
            PropObj bestProp = null;
            for (int i = 0; i < props.Count; i++)
            {
                float d = (props[i].pos - cowPos).Length();
                if (d < bestDist)
                {
                    bestDist = d;
                    bestProp = props[i];
                }
            }

            if (bestProp != null)
            {
                lickedTarget = bestProp;
                AddScore(45, "👅 魔舌成功黐住目標！（行路拖住佢行，再撳 [F] 甩飛佢！）");
                SpawnParticles(bestProp.pos, Color.HotPink, 12);
            }
            else
            {
                statusMsg = "👅 伸長鹿頸同魔舌！（行近啲的士、警車或菜箱再撳 [F] 就可以黐住拖行）";
            }
        }

        // DEEEER Simulator 招牌技能：雙腳直立暴走
        private void ToggleDeerBipedalMode()
        {
            isBipedalDeerMode = !isBipedalDeerMode;
            SpawnParticles(cowPos + new Vec3(0, 1.5f, 0), Color.Violet, 16);
            statusMsg = isBipedalDeerMode
                ? "🦌 已開啟《DEEEER Simulator》雙腳直立暴走模式！跑速大增！"
                : "🐄 已回復四腳著地模式！";
        }

        private void FireDualGatlingGuns()
        {
            Vec3 fwd = new Vec3((float)Math.Sin(cowYaw), 0f, (float)Math.Cos(cowYaw));
            Vec3 right = new Vec3((float)Math.Cos(cowYaw), 0f, -(float)Math.Sin(cowYaw));
            float gunHeight = isBipedalDeerMode ? 3.1f : 2.35f;

            // 左右牛角各射出一發火球機槍彈
            for (int side = -1; side <= 1; side += 2)
            {
                Bullet3D b = new Bullet3D();
                b.pos = cowPos + new Vec3(0, gunHeight, 0) + fwd * 1.6f + right * (side * 0.65f);
                b.vel = fwd * 1.45f + new Vec3((float)(rng.NextDouble() - 0.5) * 0.08f, -0.02f, (float)(rng.NextDouble() - 0.5) * 0.08f);
                b.life = 1.0f;
                bullets.Add(b);
            }
            screenShake = Math.Max(screenShake, 0.12f);
        }

        private void TriggerShockwave()
        {
            stamina -= 30f;
            cowVelY = 0.28f;
            isGrounded = false;
            screenShake = 0.55f;

            SpawnParticles(cowPos + new Vec3(0, 0.8f, 0), Color.Gold, 26);

            int blasted = 0;
            for (int i = 0; i < props.Count; i++)
            {
                PropObj p = props[i];
                Vec3 diff = p.pos - cowPos;
                float dist = diff.Length();
                if (dist < 12f && dist > 0.01f)
                {
                    Vec3 dir = diff.Normalized();
                    float power = (12f - dist) * 0.14f;
                    p.vel = dir * power + new Vec3(0, 0.52f, 0);
                    p.rotVel = (float)(rng.NextDouble() - 0.5) * 0.5f;
                    blasted++;
                }
            }

            if (blasted > 0)
                AddScore(blasted * 35, "💥 狂牛震地波炸飛 " + blasted + " 架車/物件!");
        }

        private void AddScore(int basePts, string label)
        {
            combo++;
            comboTimer = 4.5f;
            int mult = Math.Min(combo, 15);
            int gain = basePts * mult;
            score += gain;
            chaosMeter = Math.Min(100f, chaosMeter + basePts * 0.18f);
            statusMsg = string.Format("{0}  +{1} 分 ({2}x 連擊!)", label, gain, combo);
        }

        private void SpawnParticles(Vec3 pos, Color col, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Particle3D p = new Particle3D();
                p.pos = pos;
                p.vel = new Vec3(
                    (float)(rng.NextDouble() - 0.5) * 0.7f,
                    (float)(rng.NextDouble() * 0.42 + 0.12),
                    (float)(rng.NextDouble() - 0.5) * 0.7f
                );
                p.size = (float)(rng.NextDouble() * 0.30 + 0.16);
                p.life = 1.0f;
                p.color = col;
                particles.Add(p);
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            UpdatePhysics();
            this.Invalidate();
        }

        private void UpdatePhysics()
        {
            bool shiftHeld = keys.Contains(Keys.ShiftKey) || keys.Contains(Keys.LShiftKey) || keys.Contains(Keys.RShiftKey) || heldMobileBtnId == "DASH";
            bool moveFwd = keys.Contains(Keys.W) || keys.Contains(Keys.Up) || joyInputY > 0.25f;
            bool moveBack = keys.Contains(Keys.S) || keys.Contains(Keys.Down) || joyInputY < -0.25f;
            bool turnLeft = keys.Contains(Keys.A) || keys.Contains(Keys.Left) || joyInputX < -0.25f;
            bool turnRight = keys.Contains(Keys.D) || keys.Contains(Keys.Right) || joyInputX > 0.25f;

            // 同步更新虛擬搖桿視覺位置（如果用鍵盤撳緊）
            if (!draggingJoy)
            {
                int kx = turnLeft ? -38 : (turnRight ? 38 : 0);
                int ky = moveFwd ? -38 : (moveBack ? 38 : 0);
                joyKnob = new Point(joyCenter.X + kx, joyCenter.Y + ky);
            }

            bool moving = moveFwd || moveBack;
            isDashing = shiftHeld && moveFwd && stamina > 4f;

            // 雙腳直立模式或衝刺模式速度加成
            float baseSpd = isBipedalDeerMode ? 0.34f : 0.22f;
            float moveSpeed = isDashing ? 0.50f : baseSpd;
            float turnSpeed = 0.058f;

            if (turnLeft) cowYaw -= turnSpeed;
            if (turnRight) cowYaw += turnSpeed;

            Vec3 fwd = new Vec3((float)Math.Sin(cowYaw), 0, (float)Math.Cos(cowYaw));
            Vec3 nextPos = cowPos;
            if (moveFwd) nextPos = nextPos + fwd * moveSpeed;
            if (moveBack) nextPos = nextPos - fwd * (moveSpeed * 0.6f);

            // 噴射背包 + 布偶翻滾 (Goat Simulator 經典失控飛天)
            if (isJetpackRagdoll)
            {
                cowVelY = 0.26f;
                isGrounded = false;
                ragdollPitch += 0.32f;
                nextPos = nextPos + fwd * 0.38f;
                SpawnParticles(cowPos + new Vec3(0, 1.2f, 0), Color.OrangeRed, 2);
            }
            else
            {
                ragdollPitch *= 0.8f;
            }

            // 梧桐河河道判定（飛起咗或者行橋都可以直接過河）
            if (Math.Abs(nextPos.z) < 2.6f && Math.Abs(nextPos.x) > 7.5f && cowPos.y < 0.6f)
            {
                statusMsg = "🌉 中間係梧桐河！可以行正中間條大橋，或者撳 [Q] 開噴射背包直接飛過去！";
            }
            else
            {
                cowPos.x = Math.Max(-42f, Math.Min(42f, nextPos.x));
                cowPos.z = Math.Max(-37f, Math.Min(50f, nextPos.z));
            }

            // 體力與頸部伸縮回彈
            if (isDashing)
            {
                stamina = Math.Max(0f, stamina - 0.38f);
                headNod = 0.45f;
            }
            else
            {
                stamina = Math.Min(100f, stamina + 0.32f);
                headNod *= 0.85f;
            }

            if (lickedTarget == null)
                neckStretch *= 0.88f;
            else
                neckStretch = 1.6f;

            if (moving)
                walkCycle += (isDashing || isBipedalDeerMode) ? 0.45f : 0.24f;
            else
                walkCycle = 0f;

            // 跳躍與重力
            cowPos.y += cowVelY;
            if (cowPos.y > 0f)
            {
                cowVelY -= 0.022f;
            }
            else
            {
                cowPos.y = 0f;
                cowVelY = 0f;
                isGrounded = true;
            }

            // 牛角雙格林機槍連射更新
            if (shootCooldown > 0) shootCooldown--;
            if (isShooting && shootCooldown <= 0)
            {
                FireDualGatlingGuns();
                shootCooldown = 4;
            }

            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                Bullet3D b = bullets[i];
                b.pos = b.pos + b.vel;
                b.life -= 0.035f;

                bool hitSomething = false;
                for (int j = 0; j < props.Count; j++)
                {
                    PropObj p = props[j];
                    if ((p.pos - b.pos).Length() < 2.2f)
                    {
                        p.vel = b.vel.Normalized() * 0.85f + new Vec3(0, 0.48f, 0);
                        p.rotVel = 0.45f;
                        SpawnParticles(p.pos, Color.OrangeRed, 10);
                        AddScore(50, "🔫 牛角機槍射爆目標!");
                        screenShake = 0.25f;
                        hitSomething = true;
                        break;
                    }
                }

                if (hitSomething || b.life <= 0f || b.pos.y < 0f)
                {
                    bullets.RemoveAt(i);
                }
            }

            // Goat Simulator 魔舌拖行物理 (Spring Tether)
            if (lickedTarget != null)
            {
                Vec3 mouthPos = GetCowMouthWorldPos();
                Vec3 pullDiff = (mouthPos + fwd * 2.5f) - lickedTarget.pos;
                if (pullDiff.Length() > 2.2f)
                {
                    lickedTarget.vel = lickedTarget.vel * 0.75f + pullDiff * 0.16f;
                    lickedTarget.rotVel += 0.05f;
                }
            }

            // 碰撞與物理物件 + 警車 AI 追捕更新
            for (int i = 0; i < props.Count; i++)
            {
                PropObj p = props[i];

                // 警車 AI：當牛災指數 > 15，警車會自動開車追撞玩家！
                if (p.type == 4 && chaosMeter >= 15f && p.pos.y <= 0.85f && p != lickedTarget)
                {
                    Vec3 toCow = cowPos - p.pos;
                    toCow.y = 0;
                    if (toCow.Length() < 32f && toCow.Length() > 2.5f)
                    {
                        Vec3 copDir = toCow.Normalized();
                        p.pos = p.pos + copDir * 0.16f;
                        p.rotY = (float)Math.Atan2(copDir.x, copDir.z);
                    }
                }

                Vec3 diff = p.pos - (cowPos + new Vec3(0, 1.0f, 0));
                float distXZ = (float)Math.Sqrt(diff.x * diff.x + diff.z * diff.z);
                float hitRadius = (p.type == 1 || p.type == 2 || p.type == 4) ? 2.6f : 2.2f;

                if (distXZ < hitRadius && Math.Abs(p.pos.y - (cowPos.y + 1.0f)) < 2.2f)
                {
                    Vec3 pushDir = new Vec3(diff.x, 0, diff.z).Normalized();
                    float hitPower = (isDashing || isBipedalDeerMode || isJetpackRagdoll) ? 0.82f : 0.30f;
                    p.vel = pushDir * hitPower + new Vec3(0, hitPower * 0.55f, 0);
                    p.rotVel = (float)(rng.NextDouble() - 0.5) * 0.42f;

                    if (p.pos.y < 1.3f)
                    {
                        string propName = "粉嶺菜箱";
                        Color pCol = Color.FromArgb(198, 126, 56);
                        if (p.type == 1) { propName = "西貢綠的"; pCol = Color.LimeGreen; }
                        else if (p.type == 2) { propName = "市區紅的"; pCol = Color.Crimson; }
                        else if (p.type == 3) { propName = "露營帳篷"; pCol = Color.DodgerBlue; }
                        else if (p.type == 4) { propName = "衝鋒警車"; pCol = Color.RoyalBlue; }

                        AddScore(40, "🔥 狂牛撞飛" + propName + "!");
                        screenShake = 0.25f;
                        SpawnParticles(p.pos, pCol, 9);
                    }
                }

                p.pos = p.pos + p.vel;
                p.rotY += p.rotVel;
                p.vel.x *= 0.92f;
                p.vel.z *= 0.92f;
                p.rotVel *= 0.92f;

                if (p.pos.y > 0.75f)
                {
                    p.vel.y -= 0.022f;
                }
                else
                {
                    p.pos.y = 0.75f;
                    if (p.vel.y < -0.1f)
                        p.vel.y = -p.vel.y * 0.38f;
                    else
                        p.vel.y = 0f;
                }
            }

            // 食菠蘿油 / 粉嶺菜心檢測
            for (int i = 0; i < foods.Count; i++)
            {
                FoodItem f = foods[i];
                Vec3 diff = f.pos - (cowPos + new Vec3(0, 1.0f, 0));
                if (diff.Length() < 2.4f)
                {
                    stamina = Math.Min(100f, stamina + 45f);
                    headNod = 0.55f;
                    if (f.type == 0)
                    {
                        AddScore(60, "🍍 食咗西貢冰火菠蘿油 (+45% 體力)");
                        SpawnParticles(f.pos, Color.Gold, 14);
                        f.pos = new Vec3((float)(rng.NextDouble() * 56 - 28), 1.0f, (float)(rng.NextDouble() * 30 - 36));
                    }
                    else
                    {
                        AddScore(60, "🥬 食咗粉嶺有機菜心 (+45% 體力)");
                        SpawnParticles(f.pos, Color.LimeGreen, 14);
                        f.pos = new Vec3((float)(rng.NextDouble() * 56 - 28), 1.0f, (float)(rng.NextDouble() * 36 + 10));
                    }
                }
            }

            // 更新粒子與 Combo
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle3D pt = particles[i];
                pt.pos = pt.pos + pt.vel;
                pt.vel.y -= 0.018f;
                pt.life -= 0.04f;
                if (pt.life <= 0f) particles.RemoveAt(i);
            }

            if (comboTimer > 0f)
            {
                comboTimer -= 0.016f;
                if (comboTimer <= 0f) combo = 0;
            }

            // 第三人稱平滑鏡頭跟隨
            float angleDiff = cowYaw - camYaw;
            camYaw += angleDiff * 0.12f;

            Vec3 camBack = new Vec3((float)Math.Sin(camYaw), 0, (float)Math.Cos(camYaw));
            Vec3 targetCamPos = cowPos - camBack * 10.2f + new Vec3(0, 5.6f, 0);
            camPos = camPos + (targetCamPos - camPos) * 0.15f;

            if (screenShake > 0.01f)
            {
                camPos.x += (float)(rng.NextDouble() - 0.5) * screenShake;
                camPos.y += (float)(rng.NextDouble() - 0.5) * screenShake;
                screenShake *= 0.82f;
            }

            float targetFov = (isDashing || isJetpackRagdoll) ? 430f : 535f;
            currentFov += (targetFov - currentFov) * 0.15f;
        }

        private Vec3 GetCowMouthWorldPos()
        {
            Vec3 localMouth = isBipedalDeerMode
                ? new Vec3(0, 3.3f, 0.8f + neckStretch)
                : new Vec3(0, 1.95f, 2.2f + neckStretch);
            return cowPos + localMouth.RotateY(cowYaw);
        }

        // ==========================================
        // 3D 渲染：西貢、粉嶺、魔舌、雙槍與直立狂牛
        // ==========================================
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            using (LinearGradientBrush skyBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h / 2 + 60),
                Color.FromArgb(75, 165, 235), Color.FromArgb(185, 230, 255), LinearGradientMode.Vertical))
            {
                g.FillRectangle(skyBrush, 0, 0, w, h / 2 + 60);
            }

            Color groundBaseCol = (cowPos.z < 0)
                ? Color.FromArgb(92, 165, 65)
                : Color.FromArgb(80, 148, 52);
            using (SolidBrush meadowBrush = new SolidBrush(groundBaseCol))
            {
                g.FillRectangle(meadowBrush, 0, h / 2, w, h / 2 + 10);
            }

            renderQueue.Clear();

            // 1. 地面棋盤與西貢海面 / 梧桐河
            int gxCenter = (int)(cowPos.x / 4f);
            int gzCenter = (int)(cowPos.z / 4f);
            for (int x = gxCenter - 8; x <= gxCenter + 8; x++)
            {
                for (int z = gzCenter - 8; z <= gzCenter + 8; z++)
                {
                    float wx = x * 4f;
                    float wz = z * 4f;

                    if (wz <= -40f)
                    {
                        Color seaCol = (((x + z) & 1) == 0) ? Color.FromArgb(25, 130, 210) : Color.FromArgb(40, 150, 225);
                        AddFlatTile(new Vec3(wx, -0.1f, wz), 4.0f, 4.0f, seaCol);
                    }
                    else if (z == -9)
                    {
                        AddFlatTile(new Vec3(wx, 0.02f, wz), 4.0f, 4.0f, Color.FromArgb(235, 215, 155));
                    }
                    else if (z == 0)
                    {
                        if (Math.Abs(wx) <= 7.5f)
                            AddFlatTile(new Vec3(wx, 0.08f, wz), 4.0f, 4.0f, Color.FromArgb(110, 115, 122));
                        else
                            AddFlatTile(new Vec3(wx, -0.08f, wz), 4.0f, 4.0f, Color.FromArgb(35, 145, 205));
                    }
                    else if (wz >= 12f && wx <= -12f)
                    {
                        Color farmCol = ((z & 1) == 0) ? Color.FromArgb(115, 78, 45) : Color.FromArgb(55, 155, 45);
                        AddFlatTile(new Vec3(wx, 0.03f, wz), 3.9f, 3.9f, farmCol);
                    }
                    else if (((x + z) & 1) == 0)
                    {
                        AddFlatTile(new Vec3(wx, 0f, wz), 3.9f, 3.9f, Color.FromArgb(104, 178, 72));
                    }
                }
            }

            // 2. 西貢與粉嶺地標建築
            DrawSaiKungAndFanlingLandmarks();

            // 3. 樹木
            for (int i = 0; i < trees.Count; i++)
            {
                Vec3 t = trees[i];
                AddCuboid(t + new Vec3(0, 1.5f, 0), new Vec3(0.9f, 3.0f, 0.9f), 0, 0, Color.FromArgb(115, 75, 42));
                AddCuboid(t + new Vec3(0, 4.0f, 0), new Vec3(3.2f, 2.6f, 3.2f), 0, 0, Color.FromArgb(52, 135, 45));
            }

            // 4. 香港特色物件 (綠的、紅的、警車、露營帳篷、菜箱)
            for (int i = 0; i < props.Count; i++)
            {
                DrawHongKongProp(props[i]);
            }

            // 5. 機槍火球子彈
            for (int i = 0; i < bullets.Count; i++)
            {
                AddCuboid(bullets[i].pos, new Vec3(0.35f, 0.35f, 0.75f), 0f, cowYaw, Color.Gold);
            }

            // 6. 菠蘿油 & 粉嶺菜心
            float spin = (float)(Environment.TickCount * 0.003);
            float bob = (float)Math.Sin(spin * 2f) * 0.2f;
            for (int i = 0; i < foods.Count; i++)
            {
                FoodItem f = foods[i];
                AddFlatTile(new Vec3(f.pos.x, 0.03f, f.pos.z), 1.1f, 1.1f, Color.FromArgb(50, 95, 35));
                if (f.type == 0)
                {
                    AddCuboid(f.pos + new Vec3(0, bob, 0), new Vec3(1.1f, 0.65f, 1.1f), 0.15f, spin, Color.FromArgb(245, 175, 45));
                    AddCuboid(f.pos + new Vec3(0, bob + 0.35f, 0), new Vec3(0.85f, 0.2f, 0.85f), 0.15f, spin, Color.FromArgb(255, 215, 65));
                }
                else
                {
                    AddCuboid(f.pos + new Vec3(0, bob, 0), new Vec3(0.65f, 1.05f, 0.65f), 0.2f, spin, Color.FromArgb(55, 185, 60));
                    AddCuboid(f.pos + new Vec3(0, bob + 0.6f, 0), new Vec3(0.45f, 0.25f, 0.45f), 0.2f, spin, Color.FromArgb(255, 225, 40));
                }
            }

            // 7. 粒子碎屑
            for (int i = 0; i < particles.Count; i++)
            {
                Particle3D p = particles[i];
                AddCuboid(p.pos, new Vec3(p.size, p.size, p.size), p.life * 4f, p.life * 4f, p.color);
            }

            // 8. Goat Simulator 超長粉紅魔舌 (3D 分段連接線)
            if (lickedTarget != null)
            {
                Vec3 mPos = GetCowMouthWorldPos();
                for (int s = 1; s <= 8; s++)
                {
                    float t = s / 8.0f;
                    Vec3 segPos = mPos + (lickedTarget.pos - mPos) * t;
                    AddCuboid(segPos, new Vec3(0.28f, 0.22f, 0.28f), 0f, cowYaw, Color.FromArgb(255, 105, 165));
                }
            }

            // 9. 繪製香港和牛主角 (支援四腳 / 雙腳直立 / 雙格林機槍 / 噴射背包)
            BuildHongKongWagyuCow();

            // 排序並投影繪製
            renderQueue.Sort();
            PointF[] polyPts = new PointF[4];
            float cxScreen = w * 0.5f;
            float cyScreen = h * 0.5f;
            float scaleFov = currentFov * (h / 750f);

            using (Pen edgePen = new Pen(Color.FromArgb(45, 0, 0, 0), 1f))
            {
                for (int i = 0; i < renderQueue.Count; i++)
                {
                    Face3D face = renderQueue[i];
                    for (int v = 0; v < 4; v++)
                    {
                        Vec3 cv = face.verts[v];
                        polyPts[v] = new PointF(
                            cxScreen + (cv.x / cv.z) * scaleFov,
                            cyScreen - (cv.y / cv.z) * scaleFov
                        );
                    }
                    using (SolidBrush b = new SolidBrush(face.color))
                    {
                        g.FillPolygon(b, polyPts);
                    }
                    g.DrawPolygon(edgePen, polyPts);
                }
            }

            DrawHUDAndMobileControls(g, w, h);
        }

        private void DrawSaiKungAndFanlingLandmarks()
        {
            // 西貢碼頭與街渡
            AddCuboid(new Vec3(0, 0.4f, -41f), new Vec3(5.5f, 0.8f, 12f), 0, 0, Color.FromArgb(145, 98, 55));
            float boatBob = (float)Math.Sin(Environment.TickCount * 0.004) * 0.18f;
            AddCuboid(new Vec3(-9f, 0.3f + boatBob, -44f), new Vec3(3.4f, 0.9f, 6.2f), 0, 0.15f, Color.FromArgb(240, 240, 245));
            AddCuboid(new Vec3(-9f, 1.4f + boatBob, -44f), new Vec3(2.8f, 1.2f, 3.6f), 0, 0.15f, Color.FromArgb(30, 115, 200));

            // 萬宜水庫六角火山岩柱石群
            for (int i = 0; i < hexRocks.Count; i++)
            {
                Vec3 r = hexRocks[i];
                AddCuboid(new Vec3(r.x, r.y * 0.5f, r.z), new Vec3(2.4f, r.y, 2.4f), 0, 0.4f, Color.FromArgb(195, 165, 115));
            }

            // 粉嶺公路跨河大橋護欄
            AddCuboid(new Vec3(-7.8f, 0.9f, 0f), new Vec3(0.6f, 1.8f, 6.5f), 0, 0, Color.FromArgb(180, 185, 190));
            AddCuboid(new Vec3(7.8f, 0.9f, 0f), new Vec3(0.6f, 1.8f, 6.5f), 0, 0, Color.FromArgb(180, 185, 190));

            // 粉嶺龍躍頭圍村
            Color brickGrey = Color.FromArgb(115, 122, 128);
            Color roofDark = Color.FromArgb(65, 70, 78);
            AddCuboid(new Vec3(26f, 2.5f, 24f), new Vec3(14f, 5.0f, 1.4f), 0, 0, brickGrey);
            AddCuboid(new Vec3(26f, 1.8f, 23.2f), new Vec3(3.6f, 3.6f, 0.4f), 0, 0, Color.FromArgb(185, 38, 38));
            AddCuboid(new Vec3(18.5f, 4.0f, 24f), new Vec3(3.4f, 8.0f, 3.4f), 0, 0, brickGrey);
            AddCuboid(new Vec3(18.5f, 8.5f, 24f), new Vec3(4.2f, 1.2f, 4.2f), 0, 0, roofDark);
            AddCuboid(new Vec3(33.5f, 4.0f, 24f), new Vec3(3.4f, 8.0f, 3.4f), 0, 0, brickGrey);
            AddCuboid(new Vec3(33.5f, 8.5f, 24f), new Vec3(4.2f, 1.2f, 4.2f), 0, 0, roofDark);

            // 粉嶺高爾夫球場紅旗
            AddCuboid(new Vec3(-24f, 2.5f, 32f), new Vec3(0.18f, 5.0f, 0.18f), 0, 0, Color.White);
            AddCuboid(new Vec3(-23.2f, 4.6f, 32f), new Vec3(1.5f, 0.9f, 0.15f), 0, 0, Color.Crimson);
        }

        private void DrawHongKongProp(PropObj p)
        {
            AddFlatTile(new Vec3(p.pos.x, 0.03f, p.pos.z), 1.8f, 1.8f, Color.FromArgb(50, 95, 35));

            if (p.type == 0)
            {
                AddCuboid(p.pos, new Vec3(1.5f, 1.5f, 1.5f), 0f, p.rotY, Color.FromArgb(198, 126, 56));
            }
            else if (p.type == 1 || p.type == 2 || p.type == 4)
            {
                Color bodyCol = Color.FromArgb(30, 175, 75);
                if (p.type == 2) bodyCol = Color.FromArgb(215, 35, 45);
                if (p.type == 4) bodyCol = Color.FromArgb(235, 240, 250); // 香港警車白底藍紋

                AddCuboid(p.pos, new Vec3(1.9f, 0.85f, 3.4f), 0f, p.rotY, bodyCol);
                AddCuboid(p.pos + new Vec3(0, 0.7f, -0.1f), new Vec3(1.6f, 0.65f, 1.8f), 0f, p.rotY, Color.FromArgb(240, 240, 245));

                if (p.type == 4)
                {
                    // 閃爍紅藍警燈
                    bool flash = ((Environment.TickCount / 150) % 2 == 0);
                    AddCuboid(p.pos + new Vec3(-0.35f, 1.15f, 0f), new Vec3(0.4f, 0.22f, 0.35f), 0f, p.rotY, flash ? Color.Red : Color.Blue);
                    AddCuboid(p.pos + new Vec3(0.35f, 1.15f, 0f), new Vec3(0.4f, 0.22f, 0.35f), 0f, p.rotY, flash ? Color.Blue : Color.Red);
                }
                else
                {
                    AddCuboid(p.pos + new Vec3(0, 1.12f, 0.1f), new Vec3(0.6f, 0.22f, 0.3f), 0f, p.rotY, Color.Gold);
                }
            }
            else if (p.type == 3)
            {
                AddCuboid(p.pos, new Vec3(2.2f, 1.1f, 2.4f), 0f, p.rotY, Color.FromArgb(245, 190, 30));
                AddCuboid(p.pos + new Vec3(0, 0.75f, 0), new Vec3(1.4f, 0.7f, 2.2f), 0f, p.rotY, Color.FromArgb(40, 135, 230));
            }
        }

        // ==========================================
        // 建立香港 A5 和牛 (支援雙腳直立 DEEEER 模式 + 牛角機槍 + 伸縮魔舌)
        // ==========================================
        private void BuildHongKongWagyuCow()
        {
            Color mainCoat = isWagyuSkin ? Color.FromArgb(42, 28, 26) : Color.FromArgb(172, 98, 38);
            Color spotOrMarbling = isWagyuSkin ? Color.FromArgb(245, 182, 195) : Color.FromArgb(95, 48, 20);
            Color snoutCol = isWagyuSkin ? Color.FromArgb(250, 175, 195) : Color.FromArgb(65, 50, 45);
            Color hornCol = Color.FromArgb(238, 210, 130);
            Color goldCol = Color.FromArgb(255, 210, 35);
            Color white = Color.FromArgb(245, 245, 250);
            Color black = Color.FromArgb(30, 30, 35);
            Color gunMetal = Color.FromArgb(65, 70, 80);

            AddFlatTile(new Vec3(cowPos.x, 0.04f, cowPos.z), 1.9f, 3.0f, Color.FromArgb(48, 90, 32));

            float bodyPitch = isJetpackRagdoll ? ragdollPitch : (isBipedalDeerMode ? -1.25f : 0f);
            float bodyHeight = isBipedalDeerMode ? 2.25f : 1.48f;

            // 1. 牛身與 A5 霜降紋
            AddCowPart(new Vec3(0, bodyHeight, 0), new Vec3(1.68f, 1.32f, 2.65f), bodyPitch, mainCoat);
            AddCowPart(new Vec3(0, bodyHeight + 0.75f, 0.45f), new Vec3(1.15f, 0.55f, 0.95f), bodyPitch, spotOrMarbling);
            AddCowPart(new Vec3(-0.42f, bodyHeight + 0.15f, -0.15f), new Vec3(0.92f, 0.75f, 0.85f), bodyPitch, spotOrMarbling);
            AddCowPart(new Vec3(0.42f, bodyHeight, -0.65f), new Vec3(0.90f, 0.72f, 0.80f), bodyPitch, spotOrMarbling);

            // 2. 噴射背包 (Jetpack)
            if (isJetpackRagdoll)
            {
                AddCowPart(new Vec3(-0.55f, bodyHeight + 0.4f, -0.2f), new Vec3(0.45f, 0.45f, 1.2f), bodyPitch, Color.Silver);
                AddCowPart(new Vec3(0.55f, bodyHeight + 0.4f, -0.2f), new Vec3(0.45f, 0.45f, 1.2f), bodyPitch, Color.Silver);
            }

            // 3. 伸縮鹿頸與牛頭
            Vec3 headPivot = isBipedalDeerMode
                ? new Vec3(0, 3.25f, 0.35f + neckStretch)
                : new Vec3(0, 1.98f, 1.48f + neckStretch);

            if (neckStretch > 0.15f)
            {
                // 繪製拉長咗嘅鹿頸
                AddCowPart(new Vec3(0, (bodyHeight + headPivot.y) * 0.5f, headPivot.z * 0.6f), new Vec3(0.75f, 0.75f, 1.4f + neckStretch), 0f, mainCoat);
            }

            AddCowPartAroundPivot(headPivot, new Vec3(0, 0.05f, 0.2f), new Vec3(1.12f, 1.02f, 1.02f), headNod, mainCoat);
            AddCowPartAroundPivot(headPivot, new Vec3(0, -0.18f, 0.78f), new Vec3(1.15f, 0.52f, 0.40f), headNod, snoutCol);
            AddCowPartAroundPivot(headPivot, new Vec3(-0.38f, 0.22f, 0.72f), new Vec3(0.18f, 0.18f, 0.08f), headNod, white);
            AddCowPartAroundPivot(headPivot, new Vec3(0.38f, 0.22f, 0.72f), new Vec3(0.18f, 0.18f, 0.08f), headNod, white);

            // 牛角與 A5 金牌
            AddCowPartAroundPivot(headPivot, new Vec3(-0.48f, 0.75f, 0.18f), new Vec3(0.20f, 0.55f, 0.20f), headNod + 0.25f, hornCol);
            AddCowPartAroundPivot(headPivot, new Vec3(0.48f, 0.75f, 0.18f), new Vec3(0.20f, 0.55f, 0.20f), headNod + 0.25f, hornCol);
            AddCowPartAroundPivot(headPivot, new Vec3(0, -0.65f, 0.1f), new Vec3(0.45f, 0.45f, 0.15f), 0f, goldCol);

            // 4. DEEEER Simulator 牛角雙格林機槍
            if (isDualGunEquipped)
            {
                AddCowPartAroundPivot(headPivot, new Vec3(-0.78f, 0.45f, 0.65f), new Vec3(0.32f, 0.32f, 1.65f), headNod, gunMetal);
                AddCowPartAroundPivot(headPivot, new Vec3(0.78f, 0.45f, 0.65f), new Vec3(0.32f, 0.32f, 1.65f), headNod, gunMetal);
                AddCowPartAroundPivot(headPivot, new Vec3(-0.78f, 0.45f, 1.55f), new Vec3(0.38f, 0.38f, 0.25f), headNod, Color.OrangeRed);
                AddCowPartAroundPivot(headPivot, new Vec3(0.78f, 0.45f, 1.55f), new Vec3(0.38f, 0.38f, 0.25f), headNod, Color.OrangeRed);
            }

            // 5. 四肢（支援四腳行路 同埋 DEEEER 雙腳直立跑動）
            float swing = (float)Math.Sin(walkCycle) * 0.65f;
            if (isBipedalDeerMode)
            {
                // 前肢變成雙手擺動，後肢直立奔跑
                AddCowLeg(new Vec3(-0.68f, 2.55f, 0.45f), swing - 0.8f, mainCoat, black);
                AddCowLeg(new Vec3(0.68f, 2.55f, 0.45f), -swing - 0.8f, mainCoat, black);
                AddCowLeg(new Vec3(-0.52f, 0.95f, -0.35f), -swing, mainCoat, black);
                AddCowLeg(new Vec3(0.52f, 0.95f, -0.35f), swing, mainCoat, black);
            }
            else
            {
                AddCowLeg(new Vec3(-0.52f, 0.9f, 0.88f), swing, mainCoat, black);
                AddCowLeg(new Vec3(0.52f, 0.9f, 0.88f), -swing, mainCoat, black);
                AddCowLeg(new Vec3(-0.52f, 0.9f, -0.88f), -swing, mainCoat, black);
                AddCowLeg(new Vec3(0.52f, 0.9f, -0.88f), swing, mainCoat, black);
            }
        }

        private void AddCowLeg(Vec3 hipLocal, float pitch, Color legCol, Color hoofCol)
        {
            AddCowPartAroundPivot(hipLocal, new Vec3(0, -0.38f, 0), new Vec3(0.38f, 0.75f, 0.38f), pitch, legCol);
            AddCowPartAroundPivot(hipLocal, new Vec3(0, -0.78f, 0), new Vec3(0.40f, 0.20f, 0.40f), pitch, hoofCol);
        }

        private void AddCowPart(Vec3 localOffset, Vec3 size, float localPitch, Color col)
        {
            Vec3 worldCenter = cowPos + localOffset.RotateY(cowYaw);
            AddCuboid(worldCenter, size, localPitch, cowYaw, col);
        }

        private void AddCowPartAroundPivot(Vec3 pivotLocal, Vec3 offsetFromPivot, Vec3 size, float pitch, Color col)
        {
            Vec3 rotatedOffset = offsetFromPivot.RotateX(pitch);
            Vec3 totalLocal = pivotLocal + rotatedOffset;
            Vec3 worldCenter = cowPos + totalLocal.RotateY(cowYaw);
            AddCuboid(worldCenter, size, pitch, cowYaw, col);
        }

        private void AddFlatTile(Vec3 center, float sizeX, float sizeZ, Color col)
        {
            float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
            Vec3[] wVerts = new Vec3[] {
                new Vec3(center.x - hx, center.y, center.z - hz),
                new Vec3(center.x + hx, center.y, center.z - hz),
                new Vec3(center.x + hx, center.y, center.z + hz),
                new Vec3(center.x - hx, center.y, center.z + hz)
            };
            SubmitQuad(wVerts, new Vec3(0, 1, 0), col);
        }

        private void AddCuboid(Vec3 center, Vec3 size, float pitchX, float yawY, Color baseCol)
        {
            Vec3 toCam = center - camPos;
            if (toCam.Length() > 85f) return;

            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            Vec3[] localVerts = new Vec3[] {
                new Vec3(-hx, -hy, -hz), new Vec3(hx, -hy, -hz), new Vec3(hx, -hy, hz), new Vec3(-hx, -hy, hz),
                new Vec3(-hx,  hy, -hz), new Vec3(hx,  hy, -hz), new Vec3(hx,  hy, hz), new Vec3(-hx,  hy, hz)
            };

            Vec3[] worldVerts = new Vec3[8];
            for (int i = 0; i < 8; i++)
            {
                Vec3 v = localVerts[i];
                if (pitchX != 0f) v = v.RotateX(pitchX);
                if (yawY != 0f) v = v.RotateY(yawY);
                worldVerts[i] = center + v;
            }

            for (int f = 0; f < 6; f++)
            {
                int[] idx = BoxFaces[f];
                Vec3 v0 = worldVerts[idx[0]], v1 = worldVerts[idx[1]], v2 = worldVerts[idx[2]], v3 = worldVerts[idx[3]];
                Vec3 faceCenter = (v0 + v1 + v2 + v3) * 0.25f;
                Vec3 normal = (faceCenter - center).Normalized();

                if (Vec3.Dot(normal, camPos - faceCenter) <= 0f) continue;

                SubmitQuad(new Vec3[] { v0, v1, v2, v3 }, normal, baseCol);
            }
        }

        private void SubmitQuad(Vec3[] worldVerts, Vec3 normal, Color baseCol)
        {
            float camPitch = 0.34f;
            float cosYaw = (float)Math.Cos(camYaw), sinYaw = (float)Math.Sin(camYaw);
            float cosPitch = (float)Math.Cos(camPitch), sinPitch = (float)Math.Sin(camPitch);

            Vec3[] camVerts = new Vec3[4];
            float avgZ = 0f;

            for (int i = 0; i < 4; i++)
            {
                Vec3 rel = worldVerts[i] - camPos;
                float cx = rel.x * cosYaw - rel.z * sinYaw;
                float cz1 = rel.x * sinYaw + rel.z * cosYaw;
                float cy1 = rel.y;

                float cy = cy1 * cosPitch + cz1 * sinPitch;
                float cz = -cy1 * sinPitch + cz1 * cosPitch;

                if (cz < 0.35f) return;

                camVerts[i] = new Vec3(cx, cy, cz);
                avgZ += cz;
            }

            float diffuse = Math.Max(0f, Vec3.Dot(normal, lightDir));
            float brightness = 0.58f + diffuse * 0.42f;

            int r = (int)Math.Min(255, baseCol.R * brightness);
            int g = (int)Math.Min(255, baseCol.G * brightness);
            int b = (int)Math.Min(255, baseCol.B * brightness);

            Face3D face = new Face3D();
            face.verts = camVerts;
            face.color = Color.FromArgb(r, g, b);
            face.depth = avgZ * 0.25f;
            renderQueue.Add(face);
        }

        // ==========================================
        // 手機版模擬器 UI：頂部牛災通緝條 + 虛擬搖桿 + 6 大圓掣
        // ==========================================
        private void DrawHUDAndMobileControls(Graphics g, int w, int h)
        {
            // 1. 左上角狀態面板
            using (SolidBrush panelBrush = new SolidBrush(Color.FromArgb(175, 15, 20, 28)))
            using (Pen borderPen = new Pen(Color.FromArgb(150, 255, 215, 0), 2f))
            {
                g.FillRectangle(panelBrush, 16, 16, 480, 190);
                g.DrawRectangle(borderPen, 16, 16, 480, 190);
            }

            string currentZone = (cowPos.z < -2f)
                ? "📍 西貢區 (海濱碼頭 / 萬宜柱石 / 露營區)"
                : (cowPos.z > 2f ? "📍 粉嶺區 (龍躍頭圍村 / 鶴藪菜田 / 高球場)" : "🌉 粉嶺公路跨河大橋 (梧桐河)");

            using (Font titleFont = new Font("Microsoft JhengHei", 13.5f, FontStyle.Bold))
            using (Font statFont = new Font("Microsoft JhengHei", 10.5f, FontStyle.Bold))
            using (Font hintFont = new Font("Microsoft JhengHei", 9f, FontStyle.Regular))
            {
                g.DrawString("🐐🦌 香港和牛模擬器：Goat x DEEEER 狂暴版", titleFont, Brushes.Gold, 28, 24);
                g.DrawString(currentZone, statFont, Brushes.Cyan, 28, 52);
                g.DrawString(string.Format("破壞總分: {0}     連擊 Combo: {1}x", score, combo), statFont, Brushes.White, 28, 78);
                g.DrawString(statusMsg, statFont, Brushes.LightGreen, 28, 106);
                g.DrawString(
                    "鍵盤 或 滑鼠點擊右下角手機圓掣都玩得！\n" +
                    "[F] 伸長魔舌拉的士  |  [G] 雙腳直立鹿模式  |  [V] 牛角雙格林機槍\n" +
                    "[Q] 噴射背包翻滾    |  [1/2] 傳送西貢/粉嶺  |  [C] 換牛皮  |  [R] 重置",
                    hintFont, Brushes.LightGray, 28, 134);
            }

            // 2. 畫面正上方：《DEEEER Simulator》風格「香港牛災警報等級」
            string chaosRank = "D級 普通香港牛";
            Color rankCol = Color.LimeGreen;
            if (chaosMeter >= 80f) { chaosRank = "S級 毀滅香港終極魔牛 (警車全面追捕!)"; rankCol = Color.Red; }
            else if (chaosMeter >= 55f) { chaosRank = "A級 暴走雙槍神牛 (警車出動!)"; rankCol = Color.OrangeRed; }
            else if (chaosMeter >= 30f) { chaosRank = "B級 新界狂牛 (警車巡邏中)"; rankCol = Color.Gold; }
            else if (chaosMeter >= 12f) { chaosRank = "C級 西貢惡霸牛"; rankCol = Color.Yellow; }

            int barW = 360;
            int barX = (w - barW) / 2 + 120;
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(180, 20, 20, 25)))
            using (SolidBrush fg = new SolidBrush(rankCol))
            using (Font rankFont = new Font("Microsoft JhengHei", 11f, FontStyle.Bold))
            {
                g.FillRectangle(bg, barX - 10, 16, barW + 20, 62);
                g.DrawRectangle(Pens.White, barX - 10, 16, barW + 20, 62);
                g.DrawString("🚨 牛災通緝等級: " + chaosRank, rankFont, fg, barX, 22);
                g.FillRectangle(Brushes.DarkSlateGray, barX, 48, barW, 18);
                g.FillRectangle(fg, barX, 48, (int)(barW * (chaosMeter / 100f)), 18);
                g.DrawRectangle(Pens.White, barX, 48, barW, 18);
            }

            // 3. 左下角：手機版虛擬搖桿 (Virtual Joystick)
            using (SolidBrush joyBg = new SolidBrush(Color.FromArgb(110, 255, 255, 255)))
            using (SolidBrush joyStick = new SolidBrush(Color.FromArgb(210, 255, 215, 0)))
            using (Pen joyPen = new Pen(Color.FromArgb(180, 255, 255, 255), 3f))
            {
                g.FillEllipse(joyBg, joyCenter.X - 65, joyCenter.Y - 65, 130, 130);
                g.DrawEllipse(joyPen, joyCenter.X - 65, joyCenter.Y - 65, 130, 130);
                g.FillEllipse(joyStick, joyKnob.X - 28, joyKnob.Y - 28, 56, 56);
                g.DrawEllipse(Pens.Black, joyKnob.X - 28, joyKnob.Y - 28, 56, 56);
            }

            // 4. 右下角：手機模擬器 6 大技能圓掣
            using (Font btnFont = new Font("Microsoft JhengHei", 11f, FontStyle.Bold))
            using (Font subFont = new Font("Microsoft JhengHei", 8.5f, FontStyle.Bold))
            using (Pen ringPen = new Pen(Color.White, 2.5f))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                for (int i = 0; i < mobileBtns.Count; i++)
                {
                    MobileButton mb = mobileBtns[i];
                    bool active = (mb.id == "DEER" && isBipedalDeerMode) ||
                                  (mb.id == "LICK" && lickedTarget != null) ||
                                  (mb.id == "GUN" && isShooting) ||
                                  (mb.id == "JET" && isJetpackRagdoll) ||
                                  (mb.id == "DASH" && isDashing);

                    Color fillCol = active
                        ? Color.FromArgb(245, 255, 230, 50)
                        : Color.FromArgb(200, mb.baseColor.R, mb.baseColor.G, mb.baseColor.B);

                    using (SolidBrush b = new SolidBrush(fillCol))
                    {
                        g.FillEllipse(b, mb.rect);
                    }
                    g.DrawEllipse(ringPen, mb.rect);

                    Brush txtBrush = active ? Brushes.Black : Brushes.White;
                    Rectangle topHalf = new Rectangle(mb.rect.X, mb.rect.Y + 12, mb.rect.Width, 32);
                    Rectangle botHalf = new Rectangle(mb.rect.X, mb.rect.Y + 44, mb.rect.Width, 24);
                    g.DrawString(mb.title, btnFont, txtBrush, topHalf, sf);
                    g.DrawString("[" + mb.keyHint + "]", subFont, txtBrush, botHalf, sf);
                }
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameWindow());
        }
    }
}
