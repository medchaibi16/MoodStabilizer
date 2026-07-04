using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MoodStabilizer
{
    public partial class CredentialsAuthenticationForm : Form
    {
        private User? _currentUser;
        private bool dragging = false;
        private Point startPoint = Point.Empty;

        // Same cozy color palette
        private readonly Color PrimaryColor = Color.FromArgb(255, 107, 107);
        private readonly Color SecondaryColor = Color.FromArgb(255, 159, 67);
        private readonly Color AccentColor = Color.FromArgb(46, 213, 115);
        private readonly Color BackgroundStart = Color.FromArgb(255, 248, 240);
        private readonly Color BackgroundEnd = Color.FromArgb(255, 235, 215);
        private readonly Color TextPrimary = Color.FromArgb(60, 55, 50);
        private readonly Color TextSecondary = Color.FromArgb(140, 130, 120);

        private Label labelTitle = default!;
        private Label labelEmail = default!;
        private TextBox textBoxEmail = default!;
        private Label labelPassword = default!;
        private TextBox textBoxPassword = default!;
        private Button buttonLogin = default!;
        private Button buttonCancel = default!;
        private Label labelError = default!;
        private Panel mainPanel = default!;

        public CredentialsAuthenticationForm(User? user)
        {
            _currentUser = user;
            BuildForm();
        }

        private void BuildForm()
        {
            this.Text = "🔐 Welcome Back!";
            this.Size = new System.Drawing.Size(600, 600);  // FIXED: Smaller size
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = BackgroundStart;

            // Main gradient panel
            mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };
            mainPanel.Paint += MainPanel_Paint;

            // Header
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.Transparent
            };

            Button closeButton = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = TextSecondary,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(35, 35),
                Location = new Point(535, 10),  // FIXED: Adjusted for 600 width
                Cursor = Cursors.Hand,
                TabStop = false
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 255, 107, 107);
            closeButton.MouseEnter += (s, e) => closeButton.ForeColor = PrimaryColor;
            closeButton.MouseLeave += (s, e) => closeButton.ForeColor = TextSecondary;
            closeButton.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            headerPanel.Controls.Add(closeButton);

            // Title
            labelTitle = new Label
            {
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(50, 20),
                Size = new Size(500, 40),
                Text = "☕ Welcome back!",
                TextAlign = ContentAlignment.MiddleCenter
            };

            // White card centered
            Panel cardPanel = new Panel
            {
                Width = 500,
                Height = 420,
                BackColor = Color.White,
                Location = new Point(50, 80)  // Centered in 600-wide form
            };
            ApplyRoundedCorners(cardPanel, 20);

            // Subtitle
            Label labelSubtitle = new Label
            {
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = TextSecondary,
                Location = new Point(30, 25),
                Size = new Size(440, 20),
                Text = "Just a quick check and you're in! 🌟",
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Email section
            Panel emailContainer = new Panel
            {
                Location = new Point(30, 60),
                Size = new Size(440, 70),
                BackColor = Color.FromArgb(248, 248, 248)
            };
            ApplyRoundedCorners(emailContainer, 10);

            labelEmail = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(15, 8),
                Size = new Size(200, 20),
                Text = "📧 Email"
            };

            textBoxEmail = new TextBox
            {
                Font = new Font("Segoe UI", 11F),
                Location = new Point(15, 30),
                Size = new Size(410, 30),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 248, 248)
            };

            emailContainer.Controls.Add(labelEmail);
            emailContainer.Controls.Add(textBoxEmail);

            // Password section
            Panel passwordContainer = new Panel
            {
                Location = new Point(30, 150),
                Size = new Size(440, 70),
                BackColor = Color.FromArgb(248, 248, 248)
            };
            ApplyRoundedCorners(passwordContainer, 10);

            labelPassword = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(15, 8),
                Size = new Size(200, 20),
                Text = "🔒 Password"
            };

            textBoxPassword = new TextBox
            {
                Font = new Font("Segoe UI", 11F),
                Location = new Point(15, 30),
                Size = new Size(410, 30),
                PasswordChar = '•',
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 248, 248)
            };

            passwordContainer.Controls.Add(labelPassword);
            passwordContainer.Controls.Add(textBoxPassword);

            // Error label
            labelError = new Label
            {
                Font = new Font("Segoe UI", 10F),
                Location = new Point(30, 240),
                Size = new Size(440, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = TextSecondary
            };

            // Login button
            buttonLogin = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = PrimaryColor,
                Location = new Point(40, 290),
                Size = new Size(190, 50),
                Text = "✨ Let's Go!",
                Cursor = Cursors.Hand
            };
            buttonLogin.FlatAppearance.BorderSize = 0;
            buttonLogin.Click += ButtonLogin_Click;
            buttonLogin.MouseEnter += (s, e) => buttonLogin.BackColor = Color.FromArgb(255, 130, 130);
            buttonLogin.MouseLeave += (s, e) => buttonLogin.BackColor = PrimaryColor;
            ApplyRoundedCorners(buttonLogin, 12);

            // Cancel button
            buttonCancel = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextSecondary,
                BackColor = Color.FromArgb(245, 240, 235),
                Location = new Point(270, 290),
                Size = new Size(190, 50),
                Text = "↩️ Back",
                Cursor = Cursors.Hand
            };
            buttonCancel.FlatAppearance.BorderSize = 0;
            buttonCancel.Click += ButtonCancel_Click;
            buttonCancel.MouseEnter += (s, e) => buttonCancel.BackColor = Color.FromArgb(235, 225, 215);
            buttonCancel.MouseLeave += (s, e) => buttonCancel.BackColor = Color.FromArgb(245, 240, 235);
            ApplyRoundedCorners(buttonCancel, 12);

            // Add controls to card
            cardPanel.Controls.Add(buttonCancel);
            cardPanel.Controls.Add(buttonLogin);
            cardPanel.Controls.Add(labelError);
            cardPanel.Controls.Add(passwordContainer);
            cardPanel.Controls.Add(emailContainer);
            cardPanel.Controls.Add(labelSubtitle);

            // Window dragging
            headerPanel.MouseDown += HeaderPanel_MouseDown;
            headerPanel.MouseMove += HeaderPanel_MouseMove;
            headerPanel.MouseUp += HeaderPanel_MouseUp;

            // Add to main panel
            mainPanel.Controls.Add(cardPanel);
            mainPanel.Controls.Add(labelTitle);
            mainPanel.Controls.Add(headerPanel);

            this.Controls.Add(mainPanel);
        }

        private void MainPanel_Paint(object sender, PaintEventArgs e)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                mainPanel.ClientRectangle, BackgroundStart, BackgroundEnd, 45f))
            {
                e.Graphics.FillRectangle(brush, mainPanel.ClientRectangle);
            }
        }

        private void HeaderPanel_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            startPoint = new Point(e.X, e.Y);
        }

        private void HeaderPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point p = PointToScreen(e.Location);
                this.Location = new Point(p.X - startPoint.X, p.Y - startPoint.Y);
            }
        }

        private void HeaderPanel_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private void ButtonLogin_Click(object sender, EventArgs e)
        {
            string email = textBoxEmail.Text.Trim();
            string password = textBoxPassword.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                labelError.Text = "Oops! Don't forget to fill in both fields! 🎈";
                labelError.ForeColor = SecondaryColor;
                return;
            }

            if (_currentUser != null && _currentUser.Email == email)
            {
                labelError.Text = "✨ Perfect! Let's go!";
                labelError.ForeColor = AccentColor;

                var timer = new System.Windows.Forms.Timer { Interval = 600 };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                };
                timer.Start();
            }
            else
            {
                labelError.Text = "🤔 Hmm, that doesn't seem right. Try again!";
                labelError.ForeColor = PrimaryColor;
                textBoxPassword.Clear();
            }
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen borderPen = new Pen(Color.FromArgb(50, 255, 200, 150), 2))
            {
                e.Graphics.DrawRectangle(borderPen, 1, 1, this.Width - 3, this.Height - 3);
            }
        }
    }
}