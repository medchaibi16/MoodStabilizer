using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// Alias to avoid ambiguity
using CvPoint = OpenCvSharp.Point;
using CvSize = OpenCvSharp.Size;
using WinPoint = System.Drawing.Point;
using WinSize = System.Drawing.Size;

namespace MoodStabilizer
{
    public partial class FaceAuthenticationForm : Form
    {
        private User? _currentUser;
        private FaceAuthenticator? _authenticator;
        private VideoCapture? _capture;
        private System.Windows.Forms.Timer? _cameraTimer;
        private System.Windows.Forms.Timer? _accessGrantedTimer;
        private float[]? _savedEmbedding;
        private float? _matchConfidence;
        private bool _isAuthenticated = false;
        private Panel _mainPanel;
        private Panel _cameraPanel;
        private PictureBox _cameraPictureBox;
        private Panel _topOverlay;
        private Panel _bottomOverlay;
        private Label _labelStatus;
        private Label _labelConfidence;
        private Label _labelEmbeddingStatus;
        private Label _accessGrantedLabel;

        public FaceAuthenticationForm(User user)
        {
            _currentUser = user;
            InitializeComponent();
            this.Text = $"Face Authentication - {user.FullName}";
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
        }

        private void InitializeComponent()
        {
            // Main container that fills the form
            _mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            // TOP OVERLAY - Fixed height, contains status info
            _topOverlay = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(0, 0, 0, 180)
            };

            _labelStatus = new Label
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "Initializing...",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 10, 20, 5),
                Height = 50
            };

            _labelConfidence = new Label
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.Cyan,
                Text = "Match Confidence: --",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 5, 20, 5),
                Height = 35
            };

            _labelEmbeddingStatus = new Label
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.LightGray,
                Text = "Loading embedding...",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 5, 20, 5),
                Height = 15
            };

            _topOverlay.Controls.Add(_labelStatus);
            _topOverlay.Controls.Add(_labelConfidence);
            _topOverlay.Controls.Add(_labelEmbeddingStatus);

            // BOTTOM OVERLAY - Fixed height, contains buttons
            _bottomOverlay = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.FromArgb(0, 0, 0, 180)
            };

            Button skipButton = new Button
            {
                Text = "Skip to Credentials",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 100, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new WinSize(220, 50),
                Location = new WinPoint(20, 10)
            };
            skipButton.FlatAppearance.BorderSize = 0;
            skipButton.Click += (s, e) => ButtonSkipToCredentials_Click(s, e);

            _bottomOverlay.Controls.Add(skipButton);

            // CAMERA PANEL - This is DockStyle.Fill, so it takes remaining space (70%)
            _cameraPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = Padding.Empty  // No padding - ensure full coverage
            };

            _cameraPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            _cameraPanel.Controls.Add(_cameraPictureBox);

            // ACCESS GRANTED OVERLAY - Centered label (initially hidden)
            _accessGrantedLabel = new Label
            {
                Text = "? ACCESS GRANTED",
                Font = new Font("Segoe UI", 40, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0, 150, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new WinSize(900, 150),
                Visible = false,
                AutoSize = false
            };

            // Add controls to main panel in the correct order for docking
            // Order matters: Top first, then Fill, then Bottom
            _mainPanel.Controls.Add(_bottomOverlay);    // Add bottom last so it appears on top in Z-order
            _mainPanel.Controls.Add(_cameraPanel);      // Camera fills remaining space
            _mainPanel.Controls.Add(_topOverlay);       // Top overlay first
            _mainPanel.Controls.Add(_accessGrantedLabel); // Access granted overlay on top

            this.Controls.Add(_mainPanel);

            // Handle resize to keep access granted label centered
            this.Resize += (s, e) =>
            {
                if (_accessGrantedLabel.Visible)
                {
                    _accessGrantedLabel.Location = new WinPoint(
                        (this.ClientSize.Width - _accessGrantedLabel.Width) / 2,
                        (this.ClientSize.Height - _accessGrantedLabel.Height) / 2
                    );
                }
            };

            this.Load += FaceAuthenticationForm_Load;
        }

        private void FaceAuthenticationForm_Load(object sender, EventArgs e)
        {
            try
            {
                _authenticator = new FaceAuthenticator();
                LoadSavedEmbedding();

                _capture = new VideoCapture(0);
                if (!_capture.IsOpened())
                {
                    MessageBox.Show("Failed to open camera!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }

                // Set camera resolution to maximum
                _capture.Set(VideoCaptureProperties.FrameWidth, 1920);
                _capture.Set(VideoCaptureProperties.FrameHeight, 1080);

                _cameraTimer = new System.Windows.Forms.Timer();
                _cameraTimer.Interval = 33; // ~30 FPS
                _cameraTimer.Tick += CameraTimer_Tick;
                _cameraTimer.Start();

                _labelStatus.Text = "?? Camera Ready - Face Recognition Active";
                _labelStatus.ForeColor = Color.Green;
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
                {
                    throw new Exception($"No face embedding path found for user {_currentUser?.FullName}");
                }

                string embeddingPath = _currentUser.FaceEmbedding.Trim('"');

                // Try to find the file
                if (!File.Exists(embeddingPath))
                {
                    string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                    string altPath = Path.Combine(projectRoot, "data", "face_embeddings", Path.GetFileName(embeddingPath));
                    if (File.Exists(altPath))
                        embeddingPath = altPath;
                }

                if (!File.Exists(embeddingPath))
                {
                    throw new FileNotFoundException($"Embedding file not found");
                }

                string jsonContent = File.ReadAllText(embeddingPath);
                _savedEmbedding = JsonSerializer.Deserialize<float[]>(jsonContent);

                if (_savedEmbedding == null || _savedEmbedding.Length == 0)
                {
                    throw new Exception("Failed to deserialize embedding");
                }

                _labelEmbeddingStatus.Text = $"? Loaded {_currentUser?.FullName}'s Embedding ({_savedEmbedding?.Length} dimensions)";
                _labelEmbeddingStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                _labelEmbeddingStatus.Text = $"? Error: {ex.Message}";
                _labelEmbeddingStatus.ForeColor = Color.Red;
            }
        }

        private void CameraTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                Mat frame = new Mat();
                _capture!.Read(frame);

                if (frame.Empty())
                    return;

                // Resize frame to match picture box dimensions
                int width = _cameraPictureBox.Width;
                int height = _cameraPictureBox.Height;

                if (width > 0 && height > 0 && (frame.Width != width || frame.Height != height))
                {
                    Mat resized = new Mat();
                    Cv2.Resize(frame, resized, new CvSize(width, height));
                    frame.Dispose();
                    frame = resized;
                }

                // Face detection
                Mat grayFrame = new Mat();
                Cv2.CvtColor(frame, grayFrame, ColorConversionCodes.BGR2GRAY);

                string cascadePath = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "models", "haarcascade_frontalface_default.xml"));

                if (File.Exists(cascadePath))
                {
                    var cascade = new CascadeClassifier(cascadePath);
                    Rect[] faces = cascade.DetectMultiScale(grayFrame, 1.1, 5, 0, new CvSize(60, 60));

                    foreach (var face in faces)
                    {
                        Scalar color = new Scalar(200, 50, 255); // Pink color
                        int thickness = 3;

                        // Draw rectangle with label
                        Cv2.Rectangle(frame, face, color, thickness);
                        Cv2.PutText(frame, "FACE DETECTED",
                            new CvPoint(face.X, face.Y - 10),
                            HersheyFonts.HersheyComplexSmall, 0.7, color, 2);
                    }

                    if (faces.Length > 0)
                    {
                        Rect largestFace = faces.OrderByDescending(f => f.Width * f.Height).First();
                        Mat croppedFace = new Mat(frame, largestFace);

                        float[]? liveEmbedding = GenerateEmbeddingFromMat(croppedFace);

                        if (liveEmbedding != null && _savedEmbedding != null)
                        {
                            _matchConfidence = CalculateCosineSimilarity(liveEmbedding, _savedEmbedding);

                            string confidenceText = $"MATCH: {_matchConfidence:P1}";
                            Cv2.PutText(frame, confidenceText, new CvPoint(20, 50),
                                HersheyFonts.HersheyComplex, 1.2, new Scalar(0, 255, 0), 2);

                            _labelConfidence.Text = $"Match Confidence: {_matchConfidence:P2}";

                            if (_matchConfidence > 0.4f && !_isAuthenticated)
                            {
                                _isAuthenticated = true;
                                _labelStatus.Text = "? AUTHENTICATED! Access Granted";
                                _labelStatus.ForeColor = Color.Green;

                                _accessGrantedLabel.Visible = true;
                                _accessGrantedLabel.Location = new WinPoint(
                                    (this.ClientSize.Width - _accessGrantedLabel.Width) / 2,
                                    (this.ClientSize.Height - _accessGrantedLabel.Height) / 2
                                );

                                if (_accessGrantedTimer == null)
                                {
                                    _accessGrantedTimer = new System.Windows.Forms.Timer();
                                    _accessGrantedTimer.Interval = 2000;
                                    _accessGrantedTimer.Tick += (ts, te) =>
                                    {
                                        _accessGrantedTimer.Stop();
                                        _accessGrantedLabel.Visible = false;
                                        this.DialogResult = DialogResult.OK;
                                        this.Close();
                                    };
                                }
                                _accessGrantedTimer.Stop();
                                _accessGrantedTimer.Start();
                            }
                            else if (_matchConfidence < 0.4f)
                            {
                                _labelStatus.Text = "? Face Not Recognized";
                                _labelStatus.ForeColor = Color.Red;
                                _isAuthenticated = false;
                            }
                        }
                        croppedFace.Dispose();
                    }
                    else
                    {
                        _labelStatus.Text = "? Waiting for face...";
                        _labelStatus.ForeColor = Color.Orange;
                        _isAuthenticated = false;
                    }

                    cascade.Dispose();
                }

                // Display the frame
                Bitmap bitmap = BitmapConverter.ToBitmap(frame);

                if (_cameraPictureBox.Image != null)
                    _cameraPictureBox.Image.Dispose();

                _cameraPictureBox.Image = bitmap;

                frame.Dispose();
                grayFrame.Dispose();
            }
            catch (Exception ex)
            {
                _labelStatus.Text = $"Error: {ex.Message}";
                _labelStatus.ForeColor = Color.Red;
            }
        }

        private void ButtonSkipToCredentials_Click(object sender, EventArgs e)
        {
            StopCamera();

            CredentialsAuthenticationForm credentialsForm = new CredentialsAuthenticationForm(_currentUser);
            if (credentialsForm.ShowDialog() == DialogResult.OK)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void StopCamera()
        {
            if (_cameraTimer != null)
            {
                _cameraTimer.Stop();
                _cameraTimer.Dispose();
            }

            if (_capture != null)
            {
                _capture.Release();
                _capture.Dispose();
            }
        }

        private float[]? GenerateEmbeddingFromMat(Mat faceImage)
        {
            try
            {
                using var resized = new Mat();
                Cv2.Resize(faceImage, resized, new CvSize(112, 112));

                using var rgb = new Mat();
                Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);
                rgb.ConvertTo(rgb, MatType.CV_32FC3, 1.0f / 127.5f, -1.0f);

                float[] inputData = new float[112 * 112 * 3];
                System.Runtime.InteropServices.Marshal.Copy(rgb.Data, inputData, 0, inputData.Length);

                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 112, 112, 3 });

                string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
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
            if (embedding1.Length != embedding2.Length)
                return 0f;

            float dotProduct = 0f;
            float magnitude1 = 0f;
            float magnitude2 = 0f;

            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
                magnitude1 += embedding1[i] * embedding1[i];
                magnitude2 += embedding2[i] * embedding2[i];
            }

            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            if (magnitude1 == 0 || magnitude2 == 0)
                return 0f;

            return dotProduct / (magnitude1 * magnitude2);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCamera();
            if (_cameraPictureBox.Image != null)
                _cameraPictureBox.Image.Dispose();
            base.OnFormClosing(e);
        }
    }
}