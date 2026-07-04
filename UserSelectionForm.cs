using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MoodStabilizer
{
    public partial class UserSelectionForm : Form
    {
        private List<User> _users;
        private User? _selectedUser;
        private FlowLayoutPanel _flowLayoutPanel = default!;
        private Label labelTitle = default!;
        private Label labelStatus = default!;
        private Panel headerPanel = default!;
        private Panel gradientPanel = default!;

        public User? SelectedUser => _selectedUser;

        // Warm colors
        private readonly Color PrimaryColor = Color.FromArgb(255, 107, 107);
        private readonly Color SecondaryColor = Color.FromArgb(255, 159, 67);
        private readonly Color AccentColor = Color.FromArgb(46, 213, 115);
        private readonly Color BackgroundStart = Color.FromArgb(255, 248, 240);
        private readonly Color BackgroundEnd = Color.FromArgb(255, 235, 215);
        private readonly Color CardBackground = Color.FromArgb(255, 255, 250);
        private readonly Color TextPrimary = Color.FromArgb(60, 55, 50);
        private readonly Color TextSecondary = Color.FromArgb(140, 130, 120);
        private readonly Color CardAccent1 = Color.FromArgb(255, 190, 118);
        private readonly Color CardAccent2 = Color.FromArgb(255, 154, 162);
        private readonly Color CardAccent3 = Color.FromArgb(167, 209, 255);
        private readonly Color CardAccent4 = Color.FromArgb(178, 223, 138);
        private readonly Color[] CardAccents;

        public UserSelectionForm()
        {
            CardAccents = new[] { CardAccent1, CardAccent2, CardAccent3, CardAccent4 };
            InitializeComponent();
            this.Text = "☕ Welcome!";
            this.Size = new System.Drawing.Size(1600, 1000);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
        }

        private void UserSelectionForm_Load(object sender, EventArgs e)
        {
            try
            {
                labelStatus.Text = "🎈 Getting things ready for you...";
                labelStatus.ForeColor = PrimaryColor;
                this.Refresh();

                _users = UserDatabaseService.GetAllUsers();

                if (_users.Count == 0)
                {
                    labelStatus.Text = "👋 Hey! Looks like it's just you and me. Let's get started!";
                    labelStatus.ForeColor = SecondaryColor;
                    return;
                }

                _flowLayoutPanel.Controls.Clear();
                int colorIndex = 0;
                foreach (var user in _users)
                {
                    var userBox = CreateUserBox(user, CardAccents[colorIndex % CardAccents.Length]);
                    _flowLayoutPanel.Controls.Add(userBox);
                    colorIndex++;
                }

                labelStatus.Text = $"🌟 {_users.Count} friendly face(s) waiting for you! Pick one & let's go!";
                labelStatus.ForeColor = AccentColor;
            }
            catch (Exception ex)
            {
                labelStatus.Text = $"🤔 Oops! Small hiccup: {ex.Message}";
                labelStatus.ForeColor = PrimaryColor;
            }
        }

        private Panel CreateUserBox(User user, Color accentColor)
        {
            // SMALLER CARDS: 280x340
            Panel mainContainer = new Panel
            {
                Width = 280,
                Height = 340,
                BackColor = Color.Transparent,
                Margin = new Padding(15),
                Padding = new Padding(2)
            };

            Panel userPanel = new Panel
            {
                Width = 276,
                Height = 336,
                BackColor = CardBackground,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill
            };

            ApplyRoundedCorners(userPanel, 20);

            Panel patternPanel = new Panel
            {
                Width = 276,
                Height = 50,
                BackColor = accentColor,
                Dock = DockStyle.Top
            };
            ApplyRoundedCornersTop(patternPanel, 20);

            patternPanel.Paint += (sender, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush dotBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                {
                    for (int x = 15; x < 260; x += 30)
                    {
                        for (int y = 12; y < 60; y += 25)
                        {
                            e.Graphics.FillEllipse(dotBrush, x, y, 8, 8);
                        }
                    }
                }
            };

            // AVATAR: 100x100
            Panel avatarFrame = new Panel
            {
                Width = 100,
                Height = 100,
                Location = new Point(88, 55),
                BackColor = Color.White,
                Padding = new Padding(3)
            };
            ApplyRoundedCorners(avatarFrame, 18);

            avatarFrame.Paint += (sender, e) =>
            {
                using (Pen shadowPen = new Pen(Color.FromArgb(15, 0, 0, 0), 1))
                {
                    e.Graphics.DrawRectangle(shadowPen, 1, 1, 97, 97);
                }
            };

            PictureBox pictureBox = new PictureBox
            {
                Width = 94,
                Height = 94,
                Location = new Point(3, 3),
                BackColor = Color.FromArgb(245, 240, 235),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BorderStyle = BorderStyle.None
            };
            ApplyRoundedCorners(pictureBox, 15);

            if (!string.IsNullOrEmpty(user.FacePhotoPath) && System.IO.File.Exists(user.FacePhotoPath))
            {
                try
                {
                    pictureBox.Image = Image.FromFile(user.FacePhotoPath);
                }
                catch
                {
                    DrawFunAvatar(pictureBox, user.FullName, accentColor, 94);
                }
            }
            else
            {
                DrawFunAvatar(pictureBox, user.FullName, accentColor, 94);
            }

            // ✅ SMALLER NAME TEXT - Reduced font size
            Label nameLabel = new Label
            {
                Text = user.FullName ?? "Mystery Friend",
                Location = new Point(10, 165),
                Width = 256,
                Height = 35,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),  // Reduced from 14
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                ForeColor = TextPrimary,
                Padding = new Padding(2)
            };

            // Adjust font size if name is very long
            if ((user.FullName?.Length ?? 0) > 20)
            {
                nameLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);  // Even smaller for long names
            }
            else if ((user.FullName?.Length ?? 0) > 15)
            {
                nameLabel.Font = new Font("Segoe UI", 11, FontStyle.Bold);  // Slightly smaller for medium names
            }

            string[] funTaglines = {
                "Ready! ⭐",
                "Let's go! 🚀",
                "Awesome! ✨",
                "Hello! 🌞",
                "Vibes! 🎵"
            };
            Random rnd = new Random(user.FullName?.GetHashCode() ?? 0);

            // ✅ SMALLER TAGLINE TEXT
            Label taglineLabel = new Label
            {
                Text = funTaglines[rnd.Next(funTaglines.Length)],
                Location = new Point(10, 205),
                Width = 256,
                Height = 22,
                Font = new Font("Segoe UI", 8, FontStyle.Regular),  // Reduced from 9
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = TextSecondary
            };

            // ✅ SMALLER EMAIL TEXT
            Label emailLabel = new Label
            {
                Text = user.Email ?? "",
                Location = new Point(10, 230),
                Width = 256,
                Height = 18,
                Font = new Font("Segoe UI", 7, FontStyle.Italic),  // Reduced from 8
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(170, 160, 150)
            };

            avatarFrame.Controls.Add(pictureBox);
            userPanel.Controls.Add(patternPanel);
            userPanel.Controls.Add(avatarFrame);
            userPanel.Controls.Add(nameLabel);
            userPanel.Controls.Add(taglineLabel);
            userPanel.Controls.Add(emailLabel);
            mainContainer.Controls.Add(userPanel);

            userPanel.Tag = user;

            EventHandler clickHandler = (sender, e) => SelectUserWithBounce(user, userPanel);
            userPanel.Click += clickHandler;
            pictureBox.Click += clickHandler;
            nameLabel.Click += clickHandler;
            emailLabel.Click += clickHandler;
            avatarFrame.Click += clickHandler;

            userPanel.MouseEnter += (sender, e) =>
            {
                userPanel.BackColor = Color.FromArgb(255, 252, 245);
                avatarFrame.BackColor = Color.FromArgb(255, 250, 240);
            };

            userPanel.MouseLeave += (sender, e) =>
            {
                userPanel.BackColor = CardBackground;
                avatarFrame.BackColor = Color.White;
            };

            return mainContainer;
        }

        private void DrawFunAvatar(PictureBox pictureBox, string fullName, Color accentColor, int size = 94)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Point(0, 0), new Point(size, size),
                    accentColor, Color.FromArgb(
                        Math.Min(accentColor.R + 30, 255),
                        Math.Min(accentColor.G + 30, 255),
                        Math.Min(accentColor.B + 30, 255))))
                {
                    g.FillRectangle(brush, 0, 0, size, size);
                }

                string initials = GetInitials(fullName ?? "Hi");
                // ✅ SMALLER INITIALS IN AVATAR
                int fontSize = size / 4;  // Reduced from size/3
                using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(initials, font);
                    float x = (size - textSize.Width) / 2;
                    float y = (size - textSize.Height) / 2 + 2;

                    using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                    {
                        g.DrawString(initials, font, shadowBrush, x + 1, y + 1);
                    }

                    using (SolidBrush textBrush = new SolidBrush(Color.White))
                    {
                        g.DrawString(initials, font, textBrush, x, y);
                    }
                }

                using (Pen borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 2))
                {
                    g.DrawRectangle(borderPen, 2, 2, size - 5, size - 5);
                }
            }
            pictureBox.Image = bmp;
        }

        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "🙂";
            string[] parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            }
            return fullName.Substring(0, Math.Min(2, fullName.Length)).ToUpper();
        }

        private void SelectUserWithBounce(User user, Panel userPanel)
        {
            var bounceTimer = new System.Windows.Forms.Timer { Interval = 30 };
            int originalWidth = userPanel.Width;
            int originalHeight = userPanel.Height;
            int step = 0;
            bool growing = true;

            bounceTimer.Tick += (sender, e) =>
            {
                if (growing)
                {
                    userPanel.Width = originalWidth + step;
                    userPanel.Height = originalHeight + step;
                    userPanel.Left -= step / 2;
                    userPanel.Top -= step / 2;
                    step += 3;

                    if (step >= 8)
                    {
                        growing = false;
                    }
                }
                else
                {
                    userPanel.Width = originalWidth + step;
                    userPanel.Height = originalHeight + step;
                    userPanel.Left -= step / 2;
                    userPanel.Top -= step / 2;
                    step -= 3;

                    if (step <= 0)
                    {
                        userPanel.Width = originalWidth;
                        userPanel.Height = originalHeight;
                        bounceTimer.Stop();
                        bounceTimer.Dispose();

                        _selectedUser = user;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
            };

            bounceTimer.Start();
        }

        private void ApplyRoundedCorners(Control control, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            Rectangle rect = new Rectangle(0, 0, control.Width, control.Height);
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            control.Region = new Region(path);
        }

        private void ApplyRoundedCornersTop(Control control, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            Rectangle rect = new Rectangle(0, 0, control.Width, control.Height);
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
            path.AddLine(rect.Right, rect.Height, rect.X, rect.Height);
            path.CloseFigure();
            control.Region = new Region(path);
        }

        private void InitializeComponent()
        {
            this._flowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.labelTitle = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.headerPanel = new System.Windows.Forms.Panel();
            this.gradientPanel = new System.Windows.Forms.Panel();

            this.SuspendLayout();
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);

            this.gradientPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gradientPanel.Paint += (sender, e) =>
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    gradientPanel.ClientRectangle, BackgroundStart, BackgroundEnd, 45f))
                {
                    e.Graphics.FillRectangle(brush, gradientPanel.ClientRectangle);
                }
            };

            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Height = 120;
            this.headerPanel.BackColor = Color.Transparent;
            this.headerPanel.Padding = new Padding(50, 20, 50, 15);

            this.labelTitle.AutoSize = false;
            this.labelTitle.Font = new System.Drawing.Font("Segoe UI", 32F, System.Drawing.FontStyle.Bold);
            this.labelTitle.ForeColor = TextPrimary;
            this.labelTitle.Location = new System.Drawing.Point(50, 15);
            this.labelTitle.Size = new System.Drawing.Size(800, 60);
            this.labelTitle.Text = "☕ Welcome Back!";
            this.labelTitle.TextAlign = ContentAlignment.MiddleLeft;

            Label labelSubtitle = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 13F, FontStyle.Regular),
                ForeColor = TextSecondary,
                Location = new Point(50, 75),
                Size = new Size(800, 30),
                Text = "Select your profile to continue your journey",
                TextAlign = ContentAlignment.MiddleLeft
            };

            this._flowLayoutPanel.AutoScroll = true;
            this._flowLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._flowLayoutPanel.BackColor = Color.Transparent;
            this._flowLayoutPanel.Padding = new Padding(40, 20, 40, 20);
            this._flowLayoutPanel.WrapContents = true;

            Panel statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(255, 255, 250, 220)
            };

            this.labelStatus.BackColor = Color.Transparent;
            this.labelStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelStatus.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.labelStatus.ForeColor = TextPrimary;
            this.labelStatus.Padding = new System.Windows.Forms.Padding(50, 15, 50, 15);
            this.labelStatus.TextAlign = ContentAlignment.MiddleLeft;
            this.labelStatus.Text = "🌈 Let's find your happy place!";

            statusBar.Controls.Add(this.labelStatus);

            // Analysis button - CENTERED
            Panel buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.FromArgb(255, 248, 240)
            };

            Button analysisButton = new Button
            {
                Text = "📊 View Analysis",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AccentColor,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 50),
                Cursor = Cursors.Hand
            };
            analysisButton.FlatAppearance.BorderSize = 0;

            // Center the button
            analysisButton.Location = new Point((buttonPanel.Width - analysisButton.Width) / 2, 15);

            analysisButton.MouseEnter += (s, e) => analysisButton.BackColor = Color.FromArgb(76, 243, 145);
            analysisButton.MouseLeave += (s, e) => analysisButton.BackColor = AccentColor;
            analysisButton.Click += (sender, e) =>
            {
                using (EmotionalAnalysisForm analysisForm = new EmotionalAnalysisForm())
                {
                    analysisForm.ShowDialog();
                }
            };

            // Handle button centering when panel resizes
            buttonPanel.Resize += (sender, e) =>
            {
                analysisButton.Location = new Point((buttonPanel.Width - analysisButton.Width) / 2, 15);
            };

            buttonPanel.Controls.Add(analysisButton);

            this.headerPanel.Controls.Add(this.labelTitle);
            this.headerPanel.Controls.Add(labelSubtitle);

            this.gradientPanel.Controls.Add(this._flowLayoutPanel);
            this.gradientPanel.Controls.Add(buttonPanel);
            this.gradientPanel.Controls.Add(statusBar);
            this.gradientPanel.Controls.Add(this.headerPanel);

            this.Controls.Add(this.gradientPanel);

            bool dragging = false;
            Point startPoint = Point.Empty;

            this.headerPanel.MouseDown += (sender, e) =>
            {
                dragging = true;
                startPoint = new Point(e.X, e.Y);
            };

            this.headerPanel.MouseMove += (sender, e) =>
            {
                if (dragging)
                {
                    Point p = PointToScreen(e.Location);
                    this.Location = new Point(p.X - startPoint.X, p.Y - startPoint.Y);
                }
            };

            this.headerPanel.MouseUp += (sender, e) =>
            {
                dragging = false;
            };

            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1600, 1000);
            this.Font = new Font("Segoe UI", 10F);
            this.FormBorderStyle = FormBorderStyle.None;
            this.Name = "UserSelectionForm";
            this.Load += new System.EventHandler(this.UserSelectionForm_Load);

            this.ResumeLayout(false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen borderPen = new Pen(Color.FromArgb(50, 255, 200, 150), 3))
            {
                e.Graphics.DrawRectangle(borderPen, 1, 1, this.Width - 3, this.Height - 3);
            }
        }
    }
}