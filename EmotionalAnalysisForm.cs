using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace MoodStabilizer
{
    public class EmotionalAnalysisForm : Form
    {
        private List<EmotionalClipData> _allClips = new();
        private List<VideoGroup> _videoGroups = new();
        private EmotionalClipData? _selectedClip;
        private Panel _chartContainer;
        private Panel _chartPanel;
        private HScrollBar _horizontalScroll;
        private TreeView _videoTreeView;
        private Panel _statsPanel;
        private Label _statsLabel;
        private Panel _legendPanel;
        private SplitContainer _mainSplit;
        private int _scrollPosition = 0;
        private bool _isPainting = false;

        // Professional color scheme
        private readonly Color PrimaryDark = Color.FromArgb(18, 22, 35);
        private readonly Color SecondaryDark = Color.FromArgb(28, 33, 48);
        private readonly Color CardWhite = Color.FromArgb(248, 250, 252);
        private readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
        private readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
        private readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
        private readonly Color SuccessGreen = Color.FromArgb(34, 197, 94);

        // Emotion colors
        private readonly Color AngerColor = Color.FromArgb(239, 68, 68);
        private readonly Color FrustrationColor = Color.FromArgb(249, 115, 22);
        private readonly Color ExcitedColor = Color.FromArgb(234, 179, 8);
        private readonly Color NeutralColor = Color.FromArgb(148, 163, 184);
        private readonly Color SadnessColor = Color.FromArgb(59, 130, 246);
        private readonly Color HappinessColor = Color.FromArgb(34, 197, 94);

        public EmotionalAnalysisForm()
        {
            this.WindowState = FormWindowState.Maximized;
            InitializeComponent();
            LoadAllEmotionalClips();
        }

        private void InitializeComponent()
        {
            this.Text = "Emotional Analysis Dashboard";
            this.BackColor = PrimaryDark;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.DoubleBuffered = true;

            // Top bar
            Panel topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(15, 18, 28)
            };

            Label titleLabel = new Label
            {
                Text = "EMOTIONAL ANALYSIS DASHBOARD",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(25, 15),
                AutoSize = true
            };

            Label subtitleLabel = new Label
            {
                Text = "Multi-Video Emotion Recognition Results | 5-Second Window Analysis",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(156, 163, 175),
                Location = new Point(25, 42),
                AutoSize = true
            };

            topBar.Controls.Add(titleLabel);
            topBar.Controls.Add(subtitleLabel);

            // Main split container - 25% left, 75% right
            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 2,
                BackColor = PrimaryDark,
                FixedPanel = FixedPanel.Panel1
            };
            _mainSplit.SplitterDistance = (int)(Screen.PrimaryScreen.Bounds.Width * 0.25);
            _mainSplit.SplitterMoved += MainSplit_SplitterMoved;

            // Left panel - Video browser (25%)
            Panel leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SecondaryDark,
                Padding = new Padding(12)
            };

            Label browserLabel = new Label
            {
                Text = "VIDEO BROWSER",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 35,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label browserHint = new Label
            {
                Text = "Click on any video to view analysis",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(156, 163, 175),
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _videoTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 44, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.None,
                Indent = 20,
                ItemHeight = 35
            };
            _videoTreeView.AfterSelect += VideoTreeView_AfterSelect;

            leftPanel.Controls.Add(_videoTreeView);
            leftPanel.Controls.Add(browserHint);
            leftPanel.Controls.Add(browserLabel);

            // Right panel - Chart area (75%)
            Panel rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardWhite,
                Padding = new Padding(15)
            };

            // Chart container with scroll
            _chartContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardWhite,
                AutoScroll = false
            };

            _chartPanel = new Panel
            {
                BackColor = CardWhite,
                Location = new Point(0, 0)
            };
            _chartPanel.Paint += ChartPanel_Paint;

            // Horizontal scrollbar
            _horizontalScroll = new HScrollBar
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Visible = false,
                SmallChange = 50,
                LargeChange = 200
            };
            _horizontalScroll.Scroll += HorizontalScroll_Scroll;

            _chartContainer.Controls.Add(_chartPanel);
            _chartContainer.Controls.Add(_horizontalScroll);

            // Legend panel - Bottom (increased height for better spacing)
            _legendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.FromArgb(240, 242, 245),
                Padding = new Padding(10)
            };
            _legendPanel.Paint += LegendPanel_Paint;

            // Stats panel - above legend
            _statsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 100,
                BackColor = Color.FromArgb(240, 242, 245),
                Padding = new Padding(15),
                BorderStyle = BorderStyle.FixedSingle
            };

            _statsLabel = new Label
            {
                Text = "Select a video to view analysis",
                Font = new Font("Segoe UI", 10),
                ForeColor = TextSecondary,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _statsPanel.Controls.Add(_statsLabel);

            rightPanel.Controls.Add(_chartContainer);
            rightPanel.Controls.Add(_legendPanel);
            rightPanel.Controls.Add(_statsPanel);

            _mainSplit.Panel1.Controls.Add(leftPanel);
            _mainSplit.Panel2.Controls.Add(rightPanel);

            this.Controls.Add(_mainSplit);
            this.Controls.Add(topBar);

            this.Resize += EmotionalAnalysisForm_Resize;
        }

        private void HorizontalScroll_Scroll(object sender, ScrollEventArgs e)
        {
            _scrollPosition = _horizontalScroll.Value;
            _chartPanel.Location = new Point(-_scrollPosition, 0);
        }

        private void MainSplit_SplitterMoved(object sender, SplitterEventArgs e)
        {
            int totalWidth = this.ClientSize.Width;
            if (totalWidth > 0)
            {
                int desiredWidth = (int)(totalWidth * 0.25);
                if (_mainSplit.SplitterDistance != desiredWidth)
                {
                    _mainSplit.SplitterDistance = desiredWidth;
                }
            }
            UpdateScrollBar();
            _chartPanel.Invalidate();
        }

        private void EmotionalAnalysisForm_Resize(object sender, EventArgs e)
        {
            int totalWidth = this.ClientSize.Width;
            if (totalWidth > 0)
            {
                int desiredWidth = (int)(totalWidth * 0.25);
                if (_mainSplit.SplitterDistance != desiredWidth)
                {
                    _mainSplit.SplitterDistance = desiredWidth;
                }
            }
            UpdateScrollBar();
            _chartPanel.Invalidate();
        }

        private void UpdateScrollBar()
        {
            if (_chartPanel.Width > _chartContainer.Width)
            {
                _horizontalScroll.Visible = true;
                _horizontalScroll.Maximum = _chartPanel.Width - _chartContainer.Width + _horizontalScroll.LargeChange;
                _horizontalScroll.Enabled = true;
            }
            else
            {
                _horizontalScroll.Visible = false;
                _scrollPosition = 0;
                _chartPanel.Location = new Point(0, 0);
            }
        }

        private void LegendPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // Split into two rows to prevent overlap
            var topRow = new[] { ("ANGER", AngerColor), ("FRUSTRATION", FrustrationColor), ("EXCITED", ExcitedColor) };
            var bottomRow = new[] { ("NEUTRAL", NeutralColor), ("SADNESS", SadnessColor), ("HAPPINESS", HappinessColor) };

            int startX = 30;
            int y1 = 12;
            int y2 = 38;
            int spacing = 200; // Increased spacing between items

            // Draw top row
            int currentX = startX;
            foreach (var (name, color) in topRow)
            {
                using (SolidBrush dotBrush = new SolidBrush(color))
                {
                    g.FillRectangle(dotBrush, currentX, y1, 14, 14);
                }

                using (Font legendFont = new Font("Segoe UI", 9, FontStyle.Bold))
                using (SolidBrush legendBrush = new SolidBrush(TextPrimary))
                {
                    g.DrawString(name, legendFont, legendBrush, new PointF(currentX + 20, y1 - 1));
                }
                currentX += spacing;
            }

            // Draw bottom row
            currentX = startX;
            foreach (var (name, color) in bottomRow)
            {
                using (SolidBrush dotBrush = new SolidBrush(color))
                {
                    g.FillRectangle(dotBrush, currentX, y2, 14, 14);
                }

                using (Font legendFont = new Font("Segoe UI", 9, FontStyle.Bold))
                using (SolidBrush legendBrush = new SolidBrush(TextPrimary))
                {
                    g.DrawString(name, legendFont, legendBrush, new PointF(currentX + 20, y2 - 1));
                }
                currentX += spacing;
            }
        }

        private void LoadAllEmotionalClips()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var jsonFiles = Directory.GetFiles(baseDir, "emotional_clips_*.json");

                if (jsonFiles.Length == 0)
                {
                    MessageBox.Show("No emotional clips found! Please run emotion detection first.",
                        "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _videoGroups.Clear();
                _allClips.Clear();

                foreach (string jsonFile in jsonFiles)
                {
                    string videoName = Path.GetFileNameWithoutExtension(jsonFile).Replace("emotional_clips_", "");
                    var clips = LoadClipsFromFile(jsonFile);

                    if (clips.Count > 0)
                    {
                        _videoGroups.Add(new VideoGroup
                        {
                            VideoName = videoName,
                            Clips = clips,
                            FilePath = jsonFile
                        });
                        _allClips.AddRange(clips);
                    }
                }

                PopulateVideoTree();

                if (_videoGroups.Count > 0)
                {
                    _videoTreeView.SelectedNode = _videoTreeView.Nodes[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading clips: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<EmotionalClipData> LoadClipsFromFile(string filePath)
        {
            var clips = new List<EmotionalClipData>();

            try
            {
                string json = File.ReadAllText(filePath);
                var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("emotional_clips", out var clipsArray))
                {
                    foreach (var clipElement in clipsArray.EnumerateArray())
                    {
                        var clip = new EmotionalClipData
                        {
                            ClipIndex = clipElement.GetProperty("clip_index").GetInt32(),
                            TimeInSeconds = clipElement.GetProperty("time_in_seconds").GetInt32(),
                            Emotion = clipElement.GetProperty("emotion").GetString() ?? "neutral",
                            Confidence = (float)clipElement.GetProperty("confidence").GetDouble(),
                            AudioVote = clipElement.GetProperty("audio_vote").GetString() ?? "",
                            FaceVote = clipElement.GetProperty("face_vote").GetString() ?? ""
                        };
                        clips.Add(clip);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading {filePath}: {ex.Message}");
            }

            return clips.OrderBy(c => c.TimeInSeconds).ToList();
        }

        private void PopulateVideoTree()
        {
            _videoTreeView.Nodes.Clear();

            foreach (var videoGroup in _videoGroups)
            {
                TreeNode videoNode = new TreeNode
                {
                    Tag = videoGroup,
                    ForeColor = Color.White
                };

                var emotionGroups = videoGroup.Clips.GroupBy(c => c.Emotion)
                    .Select(g => new { Emotion = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList();

                string dominantEmotion = emotionGroups.First().Emotion;
                double avgConfidence = videoGroup.Clips.Average(c => c.Confidence);

                videoNode.Text = $"{videoGroup.VideoName}  [{videoGroup.Clips.Count} clips]";
                videoNode.ToolTipText = $"Dominant: {dominantEmotion.ToUpper()}  |  Avg Confidence: {avgConfidence:P0}";

                TreeNode statsNode = new TreeNode("Emotion Distribution")
                {
                    ForeColor = Color.FromArgb(180, 190, 210)
                };

                foreach (var group in emotionGroups.Take(5))
                {
                    TreeNode emotionNode = new TreeNode($"{group.Emotion.ToUpper()}: {group.Count} clips")
                    {
                        ForeColor = GetEmotionColor(group.Emotion)
                    };
                    statsNode.Nodes.Add(emotionNode);
                }

                videoNode.Nodes.Add(statsNode);
                _videoTreeView.Nodes.Add(videoNode);
            }
        }

        private void VideoTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is VideoGroup videoGroup)
            {
                _selectedClip = null;
                _selectedVideoGroup = videoGroup;
                UpdateStatsLabel(videoGroup);

                int clipCount = videoGroup.Clips.Count;
                int neededWidth = Math.Max(_chartContainer.Width, clipCount * 63 + 200);
                _chartPanel.Width = neededWidth;
                _chartPanel.Height = 500;

                UpdateScrollBar();
                _chartPanel.Invalidate();
            }
        }

        private VideoGroup? _selectedVideoGroup;

        private void UpdateStatsLabel(VideoGroup videoGroup)
        {
            int totalClips = videoGroup.Clips.Count;
            double avgConfidence = videoGroup.Clips.Average(c => c.Confidence);
            var dominantEmotion = videoGroup.Clips.GroupBy(c => c.Emotion)
                .OrderByDescending(g => g.Count())
                .First();

            _statsLabel.Text = $"Video: {videoGroup.VideoName}  |  Total Windows: {totalClips}  |  Duration: {videoGroup.Clips.Last().TimeInSeconds}s  |  Average Confidence: {avgConfidence:P1}  |  Dominant Emotion: {dominantEmotion.Key.ToUpper()} ({dominantEmotion.Count()} clips)";
            _statsLabel.ForeColor = GetEmotionColor(dominantEmotion.Key);
        }

        private Color GetEmotionColor(string emotion)
        {
            return emotion.ToLower() switch
            {
                "anger" => AngerColor,
                "frustration" => FrustrationColor,
                "excited" => ExcitedColor,
                "neutral" => NeutralColor,
                "sadness" => SadnessColor,
                "happiness" => HappinessColor,
                _ => NeutralColor
            };
        }

        private void ChartPanel_Paint(object sender, PaintEventArgs e)
        {
            if (_isPainting) return;
            _isPainting = true;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(CardWhite);

            if (_selectedVideoGroup == null || _selectedVideoGroup.Clips.Count == 0)
            {
                DrawNoDataMessage(g);
                _isPainting = false;
                return;
            }

            DrawColumnChart(g);
            _isPainting = false;
        }

        private void DrawColumnChart(Graphics g)
        {
            var clips = _selectedVideoGroup.Clips;
            int clipCount = clips.Count;

            if (clipCount == 0) return;

            // Fixed column width for consistency
            int columnWidth = 50;
            int spacing = 8;

            int marginLeft = 100;
            int marginRight = 30;
            int marginTop = 70;
            int marginBottom = 80;

            int chartHeight = 380;

            int totalWidth = clipCount * (columnWidth + spacing);
            int startX = marginLeft;
            int startY = marginTop;

            // Draw title
            string title = $"EMOTION ANALYSIS - {_selectedVideoGroup.VideoName}";
            using (Font titleFont = new Font("Segoe UI", 14, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(TextPrimary))
            {
                SizeF titleSize = g.MeasureString(title, titleFont);
                float titleX = (_chartPanel.Width - titleSize.Width) / 2;
                g.DrawString(title, titleFont, titleBrush, titleX, 15);
            }

            // Draw Y-axis label
            using (Font axisFont = new Font("Segoe UI", 10, FontStyle.Bold))
            using (SolidBrush axisBrush = new SolidBrush(TextSecondary))
            {
                g.TranslateTransform(45, marginTop + chartHeight / 2);
                g.RotateTransform(-90);
              
                g.ResetTransform();
            }

            // Draw grid lines and Y-axis labels
            using (Pen gridPen = new Pen(Color.FromArgb(230, 235, 240), 1))
            using (Font labelFont = new Font("Segoe UI", 9))
            using (SolidBrush labelBrush = new SolidBrush(TextSecondary))
            {
                for (int i = 0; i <= 10; i++)
                {
                    int y = startY + chartHeight - (i * chartHeight / 10);
                    g.DrawLine(gridPen, startX - 5, y, startX + totalWidth, y);

                    string label = $"{i * 10}%";
                    SizeF labelSize = g.MeasureString(label, labelFont);
                    float labelX = startX - 20 - labelSize.Width;
                    float labelY = y - 8;
                    g.DrawString(label, labelFont, labelBrush, labelX, labelY);
                }
            }

            // Draw columns
            for (int i = 0; i < clipCount; i++)
            {
                var clip = clips[i];
                int x = startX + i * (columnWidth + spacing);
                int columnHeight = (int)(clip.Confidence * chartHeight);
                int y = startY + chartHeight - columnHeight;
                Color emotionColor = GetEmotionColor(clip.Emotion);

                if (columnHeight < 1) columnHeight = 1;

                // Draw column with gradient
                using (LinearGradientBrush gradientBrush = new LinearGradientBrush(
                    new Rectangle(x, y, columnWidth, columnHeight),
                    emotionColor,
                    ControlPaint.Light(emotionColor, 0.7f),
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(gradientBrush, x, y, columnWidth, columnHeight);
                }

                // Draw border
                using (Pen borderPen = new Pen(Color.White, 1))
                {
                    g.DrawRectangle(borderPen, x, y, columnWidth, columnHeight);
                }

                // Draw confidence value
                if (columnHeight > 30)
                {
                    using (Font valueFont = new Font("Segoe UI", 8, FontStyle.Bold))
                    using (SolidBrush valueBrush = new SolidBrush(Color.White))
                    {
                        string value = $"{clip.Confidence:P0}";
                        SizeF valueSize = g.MeasureString(value, valueFont);
                        if (valueSize.Width < columnWidth - 4)
                        {
                            float valueX = x + (columnWidth - valueSize.Width) / 2;
                            float valueY = y + 4;
                            g.DrawString(value, valueFont, valueBrush, valueX, valueY);
                        }
                    }
                }

                // Draw time label
                using (Font timeFont = new Font("Segoe UI", 7))
                using (SolidBrush timeBrush = new SolidBrush(TextSecondary))
                {
                    string timeLabel = $"{clip.TimeInSeconds}s";
                    SizeF timeSize = g.MeasureString(timeLabel, timeFont);
                    float timeX = x + (columnWidth - timeSize.Width) / 2;
                    float timeY = startY + chartHeight + 8;
                    g.DrawString(timeLabel, timeFont, timeBrush, timeX, timeY);
                }

                // Draw small color indicator at bottom
                using (SolidBrush barBrush = new SolidBrush(emotionColor))
                {
                    float barX = x + 2;
                    float barY = startY + chartHeight + 28;
                    float barWidth = columnWidth - 4;
                    g.FillRectangle(barBrush, barX, barY, barWidth, 3);
                }
            }
        }

        private void DrawNoDataMessage(Graphics g)
        {
            string message = "No data available. Please select a video from the left panel.";
            using (Font msgFont = new Font("Segoe UI", 12))
            using (SolidBrush msgBrush = new SolidBrush(TextSecondary))
            {
                SizeF msgSize = g.MeasureString(message, msgFont);
                g.DrawString(message, msgFont, msgBrush, new PointF((_chartPanel.Width - msgSize.Width) / 2, 200));
            }
        }

        private class VideoGroup
        {
            public string VideoName { get; set; } = "";
            public List<EmotionalClipData> Clips { get; set; } = new();
            public string FilePath { get; set; } = "";
        }

        private class EmotionalClipData
        {
            public int ClipIndex { get; set; }
            public int TimeInSeconds { get; set; }
            public string Emotion { get; set; } = "";
            public float Confidence { get; set; }
            public string AudioVote { get; set; } = "";
            public string FaceVote { get; set; } = "";
        }
    }
}