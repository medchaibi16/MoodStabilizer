using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MoodStabilizer
{
    public partial class DashboardForm : Form
    {
        private User? _authenticatedUser;
        private bool isAutoMode = true;
        private bool dragging = false;
        private Point startPoint = Point.Empty;

        private readonly Color PrimaryDark = Color.FromArgb(25, 35, 55);
        private readonly Color SecondaryDark = Color.FromArgb(35, 45, 70);
        private readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
        private readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
        private readonly Color CardWhite = Color.FromArgb(248, 250, 252);
        private readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
        private readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        private readonly Color TextLight = Color.FromArgb(203, 213, 225);

        private SplitContainer _mainSplit = default!;
        private Panel sidebarPanel = default!;
        private Panel mainContent = default!;
        private Panel togglePanel = default!;
        private Label toggleLabelAuto = default!;
        private Label toggleLabelManual = default!;
        private TrackBar brightnessSlider = default!;
        private TrackBar contrastSlider = default!;
        private TrackBar warmthSlider = default!;
        private ComboBox musicComboBox = default!;
        private Panel chartPanel = default!;

        private readonly Color AngerColor = Color.FromArgb(239, 68, 68);
        private readonly Color FrustrationColor = Color.FromArgb(249, 115, 22);
        private readonly Color ExcitedColor = Color.FromArgb(234, 179, 8);
        private readonly Color NeutralColor = Color.FromArgb(148, 163, 184);
        private readonly Color SadnessColor = Color.FromArgb(59, 130, 246);
        private readonly Color HappinessColor = Color.FromArgb(34, 197, 94);

        public DashboardForm(User user)
        {
            _authenticatedUser = user;
            BuildForm();
            this.Load += (s, e) => UpdateControlStates();
        }

        private void BuildForm()
        {
            this.Text = "Mood Stabilizer";
            this.Size = new Size(1800, 1000);
            this.MinimumSize = new Size(1200, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = PrimaryDark;
            this.DoubleBuffered = true;

            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(60, 100, 140, 200), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            // ── Header (Dock Top) ──────────────────────────────────────────────
            var header = BuildHeader();

            // ── SplitContainer — same pattern as EmotionalAnalysisForm ─────────
            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 2,
                BackColor = PrimaryDark,
                FixedPanel = FixedPanel.Panel1
            };

            // Set sidebar to 25% of screen width (same as EmotionalAnalysisForm)
            _mainSplit.SplitterDistance = (int)(Screen.PrimaryScreen.Bounds.Width * 0.17);
    

            // Keep 17% on resize
            this.Resize += (s, e) =>
            {
                int desired = (int)(this.ClientSize.Width * 0.17);
                if (_mainSplit.SplitterDistance != desired)
                    _mainSplit.SplitterDistance = desired;
           
            };

            // ── Left panel (sidebar) ───────────────────────────────────────────
            sidebarPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SecondaryDark,
                Padding = new Padding(12)
            };
            BuildSidebar();
            _mainSplit.Panel1.Controls.Add(sidebarPanel);

            // ── Right panel (main content) ─────────────────────────────────────
            mainContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardWhite,
                Padding = new Padding(25)
            };
            BuildMainContent();
            _mainSplit.Panel2.Controls.Add(mainContent);

            this.Controls.Add(_mainSplit);
            this.Controls.Add(header);   // Top goes last in Controls.Add
        }

        // ── Header ────────────────────────────────────────────────────────────
        private Panel BuildHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(20, 30, 50)
            };

            var title = new Label
            {
                Text = "Mood Stabilizer",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextLight,
                Location = new Point(18, 0),
                Size = new Size(230, 52),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var userLabel = new Label
            {
                Text = $"👤  {_authenticatedUser?.FullName}",
                Font = new Font("Segoe UI", 10),
                ForeColor = TextSecondary,
                Size = new Size(220, 52),
                TextAlign = ContentAlignment.MiddleRight
            };

            var btnClose = MkBtn("✕", Color.FromArgb(180, 50, 50));
            var btnBack = MkBtn("← Profile", Color.FromArgb(55, 65, 90));
            btnBack.Size = new Size(90, 32);
            btnClose.Click += (s, e) => Application.Exit();
            btnBack.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            header.Controls.AddRange(new Control[] { title, userLabel, btnBack, btnClose });

            Action pos = () =>
            {
                btnClose.Location = new Point(header.Width - 42, 10);
                btnBack.Location = new Point(header.Width - 140, 10);
                userLabel.Location = new Point(header.Width - 370, 0);
            };
            header.Resize += (s, e) => pos();
            header.HandleCreated += (s, e) => pos();

            header.MouseDown += (s, e) => { dragging = true; startPoint = new Point(e.X, e.Y); };
            header.MouseMove += (s, e) =>
            {
                if (!dragging) return;
                var p = PointToScreen(e.Location);
                Location = new Point(p.X - startPoint.X, p.Y - startPoint.Y);
            };
            header.MouseUp += (s, e) => dragging = false;

            return header;
        }

        private Button MkBtn(string text, Color bg)
        {
            var b = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = bg,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(32, 32),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (s, e) => b.BackColor = Color.FromArgb(
                Math.Min(bg.R + 30, 255), Math.Min(bg.G + 30, 255), Math.Min(bg.B + 30, 255));
            b.MouseLeave += (s, e) => b.BackColor = bg;
            return b;
        }

        // ── Sidebar ───────────────────────────────────────────────────────────
        // Uses absolute Y positioning — same reliable approach as EmotionalAnalysisForm
        private void BuildSidebar()
        {
            int y = 10;
            int pad = 12;   // left padding (already in sidebarPanel.Padding)

            // We draw into sidebarPanel which has Padding=12 on all sides,
            // so effective width = sidebarPanel.ClientSize.Width (Dock=Fill handles it).
            // We use a nested Panel to get the real width after layout.

            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            sidebarPanel.Controls.Add(inner);

            // Rebuild sidebar contents once the panel has its real size
            inner.HandleCreated += (s, e) => PopulateSidebar(inner);
            inner.Resize += (s, e) => PopulateSidebar(inner);
        }

        private void PopulateSidebar(Panel inner)
        {
            inner.Controls.Clear();

            int w = inner.ClientSize.Width;
            if (w <= 20) return;   // not laid out yet

            int y = 10;
            int lh = 18;   // label height
            int th = 36;   // trackbar height
            int gap = 6;    // gap between label and control
            int sec = 10;   // section gap above title

            void SectionTitle(string text)
            {
                inner.Controls.Add(new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = TextSecondary,
                    Location = new Point(0, y),
                    Size = new Size(w, lh)
                });
                y += lh + gap;
            }

            void Sep()
            {
                inner.Controls.Add(new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 1),
                    BackColor = Color.FromArgb(55, 65, 90)
                });
                y += 12;
            }

            void CtrlLabel(string text)
            {
                inner.Controls.Add(new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 9),
                    ForeColor = TextLight,
                    Location = new Point(0, y),
                    Size = new Size(w, lh)
                });
                y += lh + 2;
            }

            // ── CONTROL MODE ──────────────────────────────────────────────────
            SectionTitle("CONTROL MODE");

            togglePanel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 36),
                BackColor = Color.FromArgb(45, 55, 80),
                Cursor = Cursors.Hand
            };
            RoundCorners(togglePanel, 18);
            y += 46;

            int half = w / 2;
            toggleLabelAuto = new Label
            {
                Text = "AUTO",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                Size = new Size(half, 36),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            toggleLabelManual = new Label
            {
                Text = "MANUAL",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextSecondary,
                Size = new Size(w - half, 36),
                Location = new Point(half, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            var sliderKnob = new Panel
            {
                Size = new Size(half - 6, 30),
                Location = new Point(3, 3),
                BackColor = AccentBlue
            };
            RoundCorners(sliderKnob, 15);
            togglePanel.Controls.Add(toggleLabelAuto);
            togglePanel.Controls.Add(toggleLabelManual);
            togglePanel.Controls.Add(sliderKnob);
            toggleLabelAuto.Click += TogglePanel_Click;
            toggleLabelManual.Click += TogglePanel_Click;
            togglePanel.Click += TogglePanel_Click;
            inner.Controls.Add(togglePanel);

            Sep();

            // ── LIGHTING ──────────────────────────────────────────────────────
            SectionTitle("LIGHTING");

            CtrlLabel("Brightness");
            brightnessSlider = new TrackBar
            {
                Location = new Point(0, y),
                Size = new Size(w, th),
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                TickStyle = TickStyle.None,
                BackColor = SecondaryDark
            };
            inner.Controls.Add(brightnessSlider);
            y += th + gap;

            CtrlLabel("Warmth (Cool to Warm)");
            warmthSlider = new TrackBar
            {
                Location = new Point(0, y),
                Size = new Size(w, th),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                TickStyle = TickStyle.None,
                BackColor = SecondaryDark
            };
            inner.Controls.Add(warmthSlider);
            y += th + gap;

            CtrlLabel("Contrast");
            contrastSlider = new TrackBar
            {
                Location = new Point(0, y),
                Size = new Size(w, th),
                Minimum = 0,
                Maximum = 100,
                Value = 60,
                TickStyle = TickStyle.None,
                BackColor = SecondaryDark
            };
            inner.Controls.Add(contrastSlider);
            y += th + gap;

            Sep();

            // ── SOUND ─────────────────────────────────────────────────────────
            SectionTitle("SOUND");

            musicComboBox = new ComboBox
            {
                Location = new Point(0, y),
                Size = new Size(w, 28),
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 55, 80),
                ForeColor = TextLight,
                FlatStyle = FlatStyle.Flat
            };
            musicComboBox.Items.AddRange(new[] { "None", "Ocean Waves", "Rain Sounds", "Forest Ambience", "Soft Piano" });
            musicComboBox.SelectedIndex = 0;
            inner.Controls.Add(musicComboBox);
            y += 38;

            Sep();

            // ── SIGN OUT ──────────────────────────────────────────────────────
            var btnSignOut = new Button
            {
                Text = "Sign Out",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TextLight,
                BackColor = Color.FromArgb(45, 55, 80),
                Location = new Point(0, y),
                Size = new Size(w, 40),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSignOut.FlatAppearance.BorderSize = 0;
            btnSignOut.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnSignOut.MouseEnter += (s, e) => btnSignOut.BackColor = Color.FromArgb(200, 60, 60);
            btnSignOut.MouseLeave += (s, e) => btnSignOut.BackColor = Color.FromArgb(45, 55, 80);
            RoundCorners(btnSignOut, 8);
            inner.Controls.Add(btnSignOut);

            UpdateControlStates();
        }

        // ── Main content ──────────────────────────────────────────────────────
        private void BuildMainContent()
        {
            var titleLabel = new Label
            {
                Text = "Emotion Analytics",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 0),
                Size = new Size(500, 42)
            };

            var subtitleLabel = new Label
            {
                Text = "Real-time emotional response over the last 5 minutes",
                Font = new Font("Segoe UI", 10),
                ForeColor = TextSecondary,
                Location = new Point(0, 46),
                Size = new Size(700, 22)
            };

            chartPanel = new Panel { Location = new Point(0, 76), BackColor = Color.White };
            chartPanel.Paint += ChartPanel_Paint;

            var legendPanel = new Panel { Height = 28, BackColor = Color.Transparent };
            BuildLegend(legendPanel);

            mainContent.Controls.AddRange(new Control[] { titleLabel, subtitleLabel, chartPanel, legendPanel });

            Action resize = () =>
            {
                int w = mainContent.ClientSize.Width - mainContent.Padding.Horizontal;
                int h = mainContent.ClientSize.Height - mainContent.Padding.Vertical;
                int lh = 28, ct = 76;
                chartPanel.Location = new Point(0, ct);
                chartPanel.Size = new Size(w, Math.Max(80, h - ct - lh - 10));
                legendPanel.Location = new Point(0, h - lh);
                legendPanel.Size = new Size(w, lh);
                chartPanel.Invalidate();
            };

            mainContent.Resize += (s, e) => resize();
            mainContent.HandleCreated += (s, e) => resize();
        }

        private void BuildLegend(Panel p)
        {
            var items = new[]
            {
                ("ANGER", AngerColor), ("FRUSTRATION", FrustrationColor),
                ("EXCITED", ExcitedColor), ("NEUTRAL", NeutralColor),
                ("SADNESS", SadnessColor), ("HAPPY", HappinessColor)
            };
            int x = 0;
            foreach (var (name, color) in items)
            {
                var dot = new Panel { Location = new Point(x, 10), Size = new Size(10, 10), BackColor = color };
                RoundCorners(dot, 5);
                var lbl = new Label
                {
                    Text = name,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = TextSecondary,
                    Location = new Point(x + 14, 4),
                    Size = new Size(105, 18)
                };
                p.Controls.Add(dot);
                p.Controls.Add(lbl);
                x += 128;
            }
        }

        // ── Chart ─────────────────────────────────────────────────────────────
        private void ChartPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            int pad = 48, cw = chartPanel.Width - pad * 2, ch = chartPanel.Height - pad * 2;
            if (cw < 10 || ch < 10) return;

            using (var gp = new Pen(Color.FromArgb(25, 0, 0, 0), 1))
            {
                for (int i = 0; i <= 6; i++) g.DrawLine(gp, pad, pad + ch * i / 6, pad + cw, pad + ch * i / 6);
                for (int i = 0; i <= 10; i++) g.DrawLine(gp, pad + cw * i / 10, pad, pad + cw * i / 10, pad + ch);
            }

            using var lf = new Font("Segoe UI", 9);
            using var lb = new SolidBrush(TextSecondary);
            for (int i = 0; i <= 4; i++)
                g.DrawString($"{100 - i * 25}%", lf, lb, pad - 42, pad + ch * i / 4 - 9);
            for (int i = 0; i <= 5; i++)
                g.DrawString($"{i}:00", lf, lb, pad + cw * i / 5 - 14, pad + ch + 6);

            DrawEmotionBars(g, pad, pad, cw, ch);
        }

        private void DrawEmotionBars(Graphics g, int x, int y, int w, int h)
        {
            var timeline = new List<(string e, int d, int i)>
            {
                ("Neutral",30,35),("Happiness",10,85),("Excited",20,70),
                ("Anger",60,90),("Frustration",90,75),("Sadness",20,60),("Neutral",70,30)
            };
            var colors = new Dictionary<string, Color>
            {
                {"Anger",AngerColor},{"Frustration",FrustrationColor},{"Excited",ExcitedColor},
                {"Neutral",NeutralColor},{"Sadness",SadnessColor},{"Happiness",HappinessColor}
            };

            float pps = (float)w / 300f, cx = x;
            foreach (var (emotion, dur, intensity) in timeline)
            {
                float bw = dur * pps;
                int bh = (int)(h * intensity / 100f);
                var r = new Rectangle((int)cx, y + h - bh, (int)Math.Ceiling(bw), bh);
                if (r.Width > 0 && r.Height > 0)
                {
                    using (var grad = new LinearGradientBrush(r, colors[emotion], Color.FromArgb(100, colors[emotion]), 90f))
                        g.FillRectangle(grad, r);
                    using (var path = new GraphicsPath())
                    {
                        path.AddArc(r.X, r.Y, 6, 6, 180, 90);
                        path.AddArc(r.Right - 6, r.Y, 6, 6, 270, 90);
                        path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
                        path.CloseFigure();
                        using (var br = new SolidBrush(colors[emotion]))
                            g.FillPath(br, path);
                    }
                }
                cx += bw;
            }
        }

        // ── Toggle ────────────────────────────────────────────────────────────
        private void TogglePanel_Click(object sender, EventArgs e)
        {
            isAutoMode = !isAutoMode;
            if (togglePanel.Controls.Count < 3) return;
            var sl = (Panel)togglePanel.Controls[2];
            int half = togglePanel.Width / 2;
            int targetX = isAutoMode ? 3 : half;
            int step = isAutoMode ? -5 : 5;

            var t = new System.Windows.Forms.Timer { Interval = 1 };
            t.Tick += (s, ev) =>
            {
                sl.Left += step;
                if ((isAutoMode && sl.Left <= 3) || (!isAutoMode && sl.Left >= half))
                { sl.Left = targetX; t.Stop(); t.Dispose(); }
            };
            t.Start();

            toggleLabelAuto.ForeColor = isAutoMode ? Color.White : TextSecondary;
            toggleLabelManual.ForeColor = isAutoMode ? TextSecondary : Color.White;
            sl.BackColor = isAutoMode ? AccentBlue : AccentGreen;
            UpdateControlStates();
        }

        private void UpdateControlStates()
        {
            if (brightnessSlider == null) return;
            bool manual = !isAutoMode;
            var dis = Color.FromArgb(60, 70, 90);
            brightnessSlider.Enabled = manual;
            warmthSlider.Enabled = manual;
            contrastSlider.Enabled = manual;
            musicComboBox.Enabled = manual;
            brightnessSlider.BackColor = manual ? SecondaryDark : dis;
            warmthSlider.BackColor = manual ? SecondaryDark : dis;
            contrastSlider.BackColor = manual ? SecondaryDark : dis;
            musicComboBox.BackColor = manual ? Color.FromArgb(45, 55, 80) : dis;
        }

        private void RoundCorners(Control c, int r)
        {
            var path = new GraphicsPath();
            var rect = new Rectangle(0, 0, c.Width, c.Height);
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }
    }
}