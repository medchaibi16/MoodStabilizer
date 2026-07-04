using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MoodStabilizer
{
    public partial class AuthenticationForm : Form
    {
        private User? _currentUser;
        private FaceAuthenticator? _authenticator;
        private VideoCapture? _capture;
        private System.Windows.Forms.Timer? _cameraTimer;
        private System.Windows.Forms.Timer? _accessGrantedTimer;
        private float[]? _savedEmbedding;
        private float? _matchConfidence;
        private bool _isAuthenticated = false;

        private PictureBox? pictureBoxCamera;
        private Label? labelFaceStatus;
        private Label? labelConfidence;
        private Label? labelEmbeddingStatus;
        private Label? labelAccessGranted;
        private TextBox? textBoxEmail;
        private TextBox? textBoxPassword;
        private Label? labelCredentialsError;
        private Panel? panelFace;
        private Panel? panelCredentials;
        private Button? buttonSkipToCredentials;
        private Button? buttonBackToFace;
        private Button? buttonLogin;

        private readonly Color PrimaryColor = Color.FromArgb(255, 107, 107);
        private readonly Color SecondaryColor = Color.FromArgb(255, 159, 67);
        private readonly Color AccentColor = Color.FromArgb(46, 213, 115);
        private readonly Color BackgroundStart = Color.FromArgb(255, 248, 240);
        private readonly Color BackgroundEnd = Color.FromArgb(255, 235, 215);
        private readonly Color TextPrimary = Color.FromArgb(60, 55, 50);
        private readonly Color TextSecondary = Color.FromArgb(140, 130, 120);

        public AuthenticationForm(User user)
        {
            InitializeComponent();
            _currentUser = user;
            this.Text = "Authentication";
            this.Size = new System.Drawing.Size(1400, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void AuthenticationForm_Load(object sender, EventArgs e)
        {
            try
            {
                _authenticator = new FaceAuthenticator();
                LoadSavedEmbedding();

                _capture = new VideoCapture(0);
                if (!_capture.IsOpened())
                {
                    labelFaceStatus!.Text = "❌ Camera not available";
                    labelFaceStatus.ForeColor = Color.Red;
                }
                else
                {
                    _cameraTimer = new System.Windows.Forms.Timer();
                    _cameraTimer.Interval = 33;
                    _cameraTimer.Tick += CameraTimer_Tick;
                    _cameraTimer.Start();

                    labelFaceStatus!.Text = "🟢 Camera Ready - Face Recognition Active";
                    labelFaceStatus.ForeColor = Color.Green;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private void LoadSavedEmbedding()
        {
            try
            {
                if (_currentUser?.FaceEmbedding == null || string.IsNullOrEmpty(_currentUser.FaceEmbedding))
                    throw new Exception($"No face embedding found for user {_currentUser?.FullName}");

                string embeddingPath = _currentUser.FaceEmbedding.Trim('"');

                if (!File.Exists(embeddingPath))
                    throw new FileNotFoundException($"Embedding file not found");

                string jsonContent = File.ReadAllText(embeddingPath);
                _savedEmbedding = JsonSerializer.Deserialize<float[]>(jsonContent);

                if (_savedEmbedding == null || _savedEmbedding.Length == 0)
                    throw new Exception("Failed to deserialize embedding");

                labelEmbeddingStatus!.Text = $"✓ Embedding Loaded ({_savedEmbedding?.Length} dimensions)";
                labelEmbeddingStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                labelEmbeddingStatus!.Text = $"✗ Failed to load embedding";
                labelEmbeddingStatus.ForeColor = Color.Red;
            }
        }

        private void CameraTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Mat frame = new Mat();
                _capture!.Read(frame);

                if (frame.Empty()) return;

                Mat grayFrame = new Mat();
                Cv2.CvtColor(frame, grayFrame, ColorConversionCodes.BGR2GRAY);

                var cascade = new CascadeClassifier(Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "models", "haarcascade_frontalface_default.xml"));

                Rect[] faces = cascade.DetectMultiScale(grayFrame, 1.1, 5, 0, new OpenCvSharp.Size(30, 30));

                foreach (var face in faces)
                {
                    Scalar modernPink = new Scalar(200, 50, 255);
                    int length = 25;
                    int thickness = 2;

                    Cv2.Line(frame, new OpenCvSharp.Point(face.X, face.Y), new OpenCvSharp.Point(face.X + length, face.Y), modernPink, thickness);
                    Cv2.Line(frame, new OpenCvSharp.Point(face.X, face.Y), new OpenCvSharp.Point(face.X, face.Y + length), modernPink, thickness);
                    Cv2.Line(frame, new OpenCvSharp.Point(face.Right, face.Y), new OpenCvSharp.Point(face.Right - length, face.Y), modernPink, thickness);
                    Cv2.Line(frame, new OpenCvSharp.Point(face.Right, face.Y), new OpenCvSharp.Point(face.Right, face.Y + length), modernPink, thickness);
                    Cv2.Line(frame, new OpenCvSharp.Point(face.X, face.Bottom), new OpenCvSharp.Point(face.X + length, face.Bottom), modernPink, thickness);
                    Cv2.Line(frame, new OpenCvSharp.Point(face.X, face.Bottom), new OpenCvSharp.Point(face.X, face.Bottom - length), modernPink, thickness);
                    Cv2.Line(frame, new OpenCvSharp.Point(face.Right, face.Bottom), new OpenCvSharp.Point(face.Right - length, face.Bottom), modernPink, thickness);
                    Cv2.Line(frame, new OpenCvSharp.Point(face.Right, face.Bottom), new OpenCvSharp.Point(face.Right, face.Bottom - length), modernPink, thickness);

                    Cv2.PutText(frame, "TARGET ACQUIRED", new OpenCvSharp.Point(face.X, face.Y - 10),
                        HersheyFonts.HersheyComplexSmall, 0.6, modernPink, 1);
                }

                if (faces.Length > 0)
                {
                    Rect largestFace = faces.OrderByDescending(f => f.Width * f.Height).First();
                    Mat croppedFace = new Mat(frame, largestFace);

                    float[]? liveEmbedding = GenerateEmbeddingFromMat(croppedFace);

                    if (liveEmbedding != null && _savedEmbedding != null)
                    {
                        _matchConfidence = CalculateCosineSimilarity(liveEmbedding, _savedEmbedding);

                        string confidenceText = $"Match: {_matchConfidence:P1}";
                        Cv2.PutText(frame, confidenceText, new OpenCvSharp.Point(20, 50),
                            HersheyFonts.HersheyComplex, 1.2, new Scalar(255, 0, 0), 2);

                        labelConfidence!.Text = $"Match Confidence: {_matchConfidence:P2}";

                        if (_matchConfidence > 0.4f && !_isAuthenticated)
                        {
                            _isAuthenticated = true;
                            labelFaceStatus!.Text = "✅ AUTHENTICATED!";
                            labelFaceStatus.ForeColor = Color.Green;

                            labelAccessGranted!.Text = "✅ ACCESS GRANTED";
                            labelAccessGranted.Visible = true;

                            if (_accessGrantedTimer == null)
                            {
                                _accessGrantedTimer = new System.Windows.Forms.Timer();
                                _accessGrantedTimer.Interval = 2000;
                                _accessGrantedTimer.Tick += AccessGrantedTimer_Tick;
                            }
                            _accessGrantedTimer.Stop();
                            _accessGrantedTimer.Start();
                        }
                        else if (_matchConfidence < 0.4f)
                        {
                            labelFaceStatus!.Text = "❌ Face Not Recognized";
                            labelFaceStatus.ForeColor = Color.Red;
                            _isAuthenticated = false;
                        }
                    }

                    croppedFace.Dispose();
                }
                else
                {
                    labelFaceStatus!.Text = "⏳ Waiting for face...";
                    labelFaceStatus.ForeColor = Color.Orange;
                    _isAuthenticated = false;
                }

                Bitmap bitmap = BitmapConverter.ToBitmap(frame);
                pictureBoxCamera!.Image = bitmap;

                frame.Dispose();
                grayFrame.Dispose();
            }
            catch (Exception ex)
            {
                labelFaceStatus!.Text = $"Error: {ex.Message}";
                labelFaceStatus.ForeColor = Color.Red;
            }
        }

        private void ButtonSkipToCredentials_Click(object sender, EventArgs e)
        {
            _cameraTimer?.Stop();
            panelFace!.Visible = false;
            panelCredentials!.Visible = true;
            buttonSkipToCredentials!.Visible = false;
            buttonBackToFace!.Visible = true;
            textBoxEmail!.Clear();
            textBoxPassword!.Clear();
            labelCredentialsError!.Text = "";
        }

        private void ButtonBackToFace_Click(object sender, EventArgs e)
        {
            panelFace!.Visible = true;
            panelCredentials!.Visible = false;
            buttonSkipToCredentials!.Visible = true;
            buttonBackToFace!.Visible = false;
            labelCredentialsError!.Text = "";
            _cameraTimer?.Start();
        }

        private void ButtonLoginCredentials_Click(object sender, EventArgs e)
        {
            string email = textBoxEmail!.Text.Trim();
            string password = textBoxPassword!.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                labelCredentialsError!.Text = "❌ Please enter both email and password";
                labelCredentialsError.ForeColor = Color.Red;
                return;
            }

            if (_currentUser != null && _currentUser.Email == email)
            {
                labelCredentialsError!.Text = "✅ Login Successful!";
                labelCredentialsError.ForeColor = AccentColor;

                var timer = new System.Windows.Forms.Timer { Interval = 600 };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    OpenDashboard();
                };
                timer.Start();
            }
            else
            {
                labelCredentialsError!.Text = "❌ Invalid email or password";
                labelCredentialsError.ForeColor = PrimaryColor;
                textBoxPassword!.Clear();
            }
        }

        private void AccessGrantedTimer_Tick(object sender, EventArgs e)
        {
            _accessGrantedTimer?.Stop();
            labelAccessGranted!.Visible = false;
            OpenDashboard();
        }

        private void OpenDashboard()
        {
            // 1. Clean up camera
            _cameraTimer?.Stop();
            _cameraTimer?.Dispose();
            _cameraTimer = null;

            if (_capture != null && _capture.IsOpened())
            {
                _capture.Release();
                _capture.Dispose();
                _capture = null;
            }

            // 2. Show dashboard as a modal dialog.
            //    When the user clicks "← Profile" or "Sign Out", the dashboard sets
            //    DialogResult = Cancel and closes itself — ShowDialog() returns here.
            var dashboard = new DashboardForm(_currentUser!);
            this.Hide();
            dashboard.ShowDialog(this);   // <-- ShowDialog, NOT Show()

            // 3. After dashboard closes, close auth too.
            //    This unblocks the ShowDialog() in UserSelectionForm's caller (Program.cs),
            //    which then shows UserSelectionForm again.
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private float[]? GenerateEmbeddingFromMat(Mat faceImage)
        {
            try
            {
                using var resized = new Mat();
                Cv2.Resize(faceImage, resized, new OpenCvSharp.Size(112, 112));

                using var rgb = new Mat();
                Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);
                rgb.ConvertTo(rgb, MatType.CV_32FC3, 1.0f / 127.5f, -1.0f);

                float[] inputData = new float[112 * 112 * 3];

                unsafe
                {
                    float* ptr = (float*)rgb.DataPointer;
                    int idx = 0;
                    for (int h = 0; h < 112; h++)
                        for (int w = 0; w < 112; w++)
                            for (int c = 0; c < 3; c++)
                                inputData[idx++] = ptr[(h * 112 + w) * 3 + c];
                }

                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 112, 112, 3 });

                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string projectRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
                string arcFacePath = Path.Combine(projectRoot, "models", "arcface.onnx");

                using var session = new InferenceSession(arcFacePath);
                string inputName = session.InputMetadata.Keys.First();

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
                };

                using var results = session.Run(inputs);
                return results.First().AsTensor<float>().ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Embedding Error: {ex.Message}");
                return null;
            }
        }

        private float CalculateCosineSimilarity(float[] embedding1, float[] embedding2)
        {
            if (embedding1.Length != embedding2.Length) return 0f;

            float dotProduct = 0f, magnitude1 = 0f, magnitude2 = 0f;

            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
                magnitude1 += embedding1[i] * embedding1[i];
                magnitude2 += embedding2[i] * embedding2[i];
            }

            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            return (magnitude1 == 0 || magnitude2 == 0) ? 0f : dotProduct / (magnitude1 * magnitude2);
        }

        private void AuthenticationForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cameraTimer?.Stop();
            _cameraTimer?.Dispose();
            _accessGrantedTimer?.Stop();
            _accessGrantedTimer?.Dispose();

            if (_capture != null)
            {
                _capture.Release();
                _capture.Dispose();
                _capture = null;
            }

            pictureBoxCamera?.Image?.Dispose();
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

        private void InitializeComponent()
        {
            this.SuspendLayout();

            var panelMain = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // ── Face Panel ──────────────────────────────────────────────────────────
            panelFace = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Visible = true };
            var labelFaceTitle = new Label
            {
                Text = "🔐 Face Authentication",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };

            pictureBoxCamera = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Zoom };
            labelFaceStatus = new Label { Text = "Loading...", Font = new Font("Arial", 11), Dock = DockStyle.Top, Height = 35, TextAlign = ContentAlignment.MiddleCenter };
            labelConfidence = new Label { Text = "Match Confidence: --", Font = new Font("Arial", 12, FontStyle.Bold), BackColor = Color.LightBlue, ForeColor = Color.DarkBlue, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
            labelEmbeddingStatus = new Label { Text = "Loading embedding...", Font = new Font("Arial", 9), Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
            labelAccessGranted = new Label { Text = "✅ ACCESS GRANTED", Font = new Font("Arial", 16, FontStyle.Bold), BackColor = Color.DarkGreen, ForeColor = Color.White, Dock = DockStyle.Top, Height = 60, TextAlign = ContentAlignment.MiddleCenter, Visible = false };

            buttonSkipToCredentials = new Button
            {
                Text = "⏭️ Skip to Credentials",
                BackColor = Color.Orange,
                ForeColor = Color.White,
                Font = new Font("Arial", 11, FontStyle.Bold),
                Dock = DockStyle.Bottom,
                Height = 50,
                Cursor = Cursors.Hand
            };
            buttonSkipToCredentials.Click += (s, e) => ButtonSkipToCredentials_Click(s, e);

            panelFace.Controls.Add(buttonSkipToCredentials);
            panelFace.Controls.Add(labelAccessGranted);
            panelFace.Controls.Add(labelEmbeddingStatus);
            panelFace.Controls.Add(labelConfidence);
            panelFace.Controls.Add(labelFaceStatus);
            panelFace.Controls.Add(pictureBoxCamera);
            panelFace.Controls.Add(labelFaceTitle);

            // ── Credentials Panel ───────────────────────────────────────────────────
            panelCredentials = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.None, Visible = false };
            panelCredentials.Paint += (s, e) =>
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    panelCredentials.ClientRectangle, BackgroundStart, BackgroundEnd, 45f))
                {
                    e.Graphics.FillRectangle(brush, panelCredentials.ClientRectangle);
                }
            };

            var centerContainer = new Panel
            {
                Size = new System.Drawing.Size(550, 620),
                BackColor = Color.White,
                Location = new System.Drawing.Point((1400 - 550) / 2, (850 - 620) / 2)
            };

            centerContainer.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = new GraphicsPath())
                {
                    int radius = 20;
                    Rectangle rect = centerContainer.ClientRectangle;
                    rect.Width -= 1;
                    rect.Height -= 1;
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    centerContainer.Region = new Region(path);
                    using (Pen pen = new Pen(Color.FromArgb(220, 220, 220), 1))
                        e.Graphics.DrawPath(pen, path);
                }
            };

            // TableLayoutPanel with spacer rows for 10px gaps
            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 17,
                Padding = new Padding(40, 20, 40, 20),
                BackColor = Color.White
            };

            tableLayout.RowStyles.Clear();
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // 0  Title
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 1  spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));  // 2  spacing label
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 3  spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // 4  Subtitle
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 5  spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // 6  Email label
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 7  spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // 8  Email input
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 9  spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // 10 Pwd label
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 11 spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // 12 Pwd input
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 13 spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // 14 Error label
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));  // 15 spacer
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // 16 Buttons

            var labelCredentialsTitle = new Label
            {
                Font = new Font("Segoe UI", 28F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Text = "☕ Welcome back!",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            var spacingLabel = new Label { Dock = DockStyle.Fill, Text = "", AutoSize = false };

            var labelSubtitle = new Label
            {
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = TextSecondary,
                Text = "Just a quick check and you're in! 🌟",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            var labelEmail = new Label
            {
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Text = "📧 Email",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };

            textBoxEmail = new TextBox
            {
                Font = new Font("Segoe UI", 12F),
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                BackColor = Color.FromArgb(248, 248, 248)
            };

            var labelPassword = new Label
            {
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Text = "🔒 Password",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };

            textBoxPassword = new TextBox
            {
                Font = new Font("Segoe UI", 12F),
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                BackColor = Color.FromArgb(248, 248, 248)
            };

            labelCredentialsError = new Label
            {
                Font = new Font("Segoe UI", 10F),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = TextSecondary,
                Dock = DockStyle.Fill,
                AutoSize = false
            };

            var buttonPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            buttonLogin = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = PrimaryColor,
                Size = new System.Drawing.Size(220, 50),
                Location = new System.Drawing.Point(0, 10),
                Text = "✨ Let's Go!",
                Cursor = Cursors.Hand
            };
            buttonLogin.FlatAppearance.BorderSize = 0;
            buttonLogin.Click += (s, e) => ButtonLoginCredentials_Click(s, e);
            buttonLogin.MouseEnter += (s, e) => buttonLogin.BackColor = Color.FromArgb(255, 130, 130);
            buttonLogin.MouseLeave += (s, e) => buttonLogin.BackColor = PrimaryColor;

            buttonBackToFace = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextSecondary,
                BackColor = Color.FromArgb(245, 240, 235),
                Size = new System.Drawing.Size(220, 50),
                Location = new System.Drawing.Point(240, 10),
                Text = "↩️ Back",
                Visible = false,
                Cursor = Cursors.Hand
            };
            buttonBackToFace.FlatAppearance.BorderSize = 0;
            buttonBackToFace.Click += (s, e) => ButtonBackToFace_Click(s, e);
            buttonBackToFace.MouseEnter += (s, e) => buttonBackToFace.BackColor = Color.FromArgb(235, 225, 215);
            buttonBackToFace.MouseLeave += (s, e) => buttonBackToFace.BackColor = Color.FromArgb(245, 240, 235);

            buttonPanel.Controls.Add(buttonLogin);
            buttonPanel.Controls.Add(buttonBackToFace);

            // Content rows (even indices), spacer rows (odd indices) stay empty
            tableLayout.Controls.Add(labelCredentialsTitle, 0, 0);
            tableLayout.Controls.Add(spacingLabel, 0, 2);
            tableLayout.Controls.Add(labelSubtitle, 0, 4);
            tableLayout.Controls.Add(labelEmail, 0, 6);
            tableLayout.Controls.Add(textBoxEmail, 0, 8);
            tableLayout.Controls.Add(labelPassword, 0, 10);
            tableLayout.Controls.Add(textBoxPassword, 0, 12);
            tableLayout.Controls.Add(labelCredentialsError, 0, 14);
            tableLayout.Controls.Add(buttonPanel, 0, 16);

            centerContainer.Controls.Add(tableLayout);
            panelCredentials.Controls.Add(centerContainer);

            panelMain.Controls.Add(panelCredentials);
            panelMain.Controls.Add(panelFace);

            this.Controls.Add(panelMain);
            this.Name = "AuthenticationForm";
            this.Text = "Authentication";
            this.Load += new EventHandler(this.AuthenticationForm_Load);
            this.FormClosing += new FormClosingEventHandler(this.AuthenticationForm_FormClosing);
            this.ResumeLayout(false);
        }
    }
}