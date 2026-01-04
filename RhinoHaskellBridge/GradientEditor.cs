using Rhino.DocObjects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace RhinoHaskellBridge
{
    public class GradientEditor : UserControl
    {
        private List<GradientPoint> points = new List<GradientPoint>();
        private GradientPoint selectedPoint = null;
        private GradientPoint hoverPoint = null;
        private bool isDragging = false;

        public class GradientPoint
        {
            public float Position { get; set; }  // 0.0 to 1.0
            public float Value { get; set; }      // 0.0 to 1.0

            public GradientPoint(float pos, float val)
            {
                Position = pos;
                Value = val;
            }
        }

        public GradientEditor()
        {
            this.DoubleBuffered = true;
            this.Size = new Size(300, 150);
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.FixedSingle;

            // Initialize with default gradient (linear white to black)
            points.Add(new GradientPoint(0.0f, 1.0f));    // Start: white (full intensity)
            points.Add(new GradientPoint(1.0f, 0.0f));    // End: black (no intensity)

            this.MouseDown += GradientEditor_MouseDown;
            this.MouseMove += GradientEditor_MouseMove;
            this.MouseUp += GradientEditor_MouseUp;
        }

        public List<float> GetGradientValues(int sampleCount = 20)
        {
            var values = new List<float>();
            var sortedPoints = points.OrderBy(p => p.Position).ToList();

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)(sampleCount - 1);
                values.Add(EvaluateGradient(t, sortedPoints));
            }

            return values;
        }

        private float EvaluateGradient(float t, List<GradientPoint> sortedPoints)
        {
            if (sortedPoints.Count == 0) return 0.5f;
            if (sortedPoints.Count == 1) return sortedPoints[0].Value;

            // Find surrounding points
            GradientPoint before = sortedPoints[0];
            GradientPoint after = sortedPoints[sortedPoints.Count - 1];

            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                if (sortedPoints[i].Position <= t && sortedPoints[i + 1].Position >= t)
                {
                    before = sortedPoints[i];
                    after = sortedPoints[i + 1];
                    break;
                }
            }

            // Linear interpolation
            if (Math.Abs(after.Position - before.Position) < 0.001f)
                return before.Value;

            float localT = (t - before.Position) / (after.Position - before.Position);
            return before.Value * (1 - localT) + after.Value * localT;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int margin = 20;
            int graphWidth = this.Width - 2 * margin;
            int graphHeight = this.Height - 2 * margin;

            // Draw background grid
            using (Pen gridPen = new Pen(Color.LightGray, 1))
            {
                for (int i = 0; i <= 4; i++)
                {
                    int y = margin + (graphHeight * i / 4);
                    g.DrawLine(gridPen, margin, y, margin + graphWidth, y);

                    int x = margin + (graphWidth * i / 4);
                    g.DrawLine(gridPen, x, margin, x, margin + graphHeight);
                }
            }

            // Draw gradient preview
            DrawGradientFill(g, margin, margin, graphWidth, graphHeight);

            // Draw gradient curve
            DrawGradientCurve(g, margin, margin, graphWidth, graphHeight);

            // Draw control points
            foreach (var point in points)
            {
                DrawControlPoint(g, point, margin, graphWidth, graphHeight,
                    point == selectedPoint, point == hoverPoint);
            }

            // Draw instructions
            using (System.Drawing.Font font = new System.Drawing.Font("Arial", 8))
            using (Brush brush = new SolidBrush(Color.Black))
            {
                g.DrawString("Click to add • Drag to move • Right-click to delete",
                    font, brush, 5, this.Height - 15);
            }
        }

        private void DrawGradientFill(Graphics g, int x, int y, int width, int height)
        {
            var sortedPoints = points.OrderBy(p => p.Position).ToList();

            // Draw gradient as vertical strips
            for (int i = 0; i < width; i++)
            {
                float t = i / (float)width;
                float value = EvaluateGradient(t, sortedPoints);
                int gray = (int)(value * 255);

                using (Pen pen = new Pen(Color.FromArgb(gray, gray, gray)))
                {
                    g.DrawLine(pen, x + i, y, x + i, y + height);
                }
            }
        }

        private void DrawGradientCurve(Graphics g, int x, int y, int width, int height)
        {
            var sortedPoints = points.OrderBy(p => p.Position).ToList();

            List<PointF> curvePoints = new List<PointF>();

            for (int i = 0; i <= width; i += 2)
            {
                float t = i / (float)width;
                float value = EvaluateGradient(t, sortedPoints);

                float px = x + i;
                float py = y + height - (value * height);

                curvePoints.Add(new PointF(px, py));
            }

            if (curvePoints.Count > 1)
            {
                using (Pen curvePen = new Pen(Color.Red, 2))
                {
                    g.DrawLines(curvePen, curvePoints.ToArray());
                }
            }
        }

        private void DrawControlPoint(Graphics g, GradientPoint point, int margin,
            int width, int height, bool isSelected, bool isHover)
        {
            float px = margin + point.Position * width;
            float py = margin + height - (point.Value * height);

            int size = 8;
            Color fillColor = isSelected ? Color.Blue : (isHover ? Color.Orange : Color.White);

            using (Brush fillBrush = new SolidBrush(fillColor))
            using (Pen outlinePen = new Pen(Color.Black, 2))
            {
                g.FillEllipse(fillBrush, px - size / 2, py - size / 2, size, size);
                g.DrawEllipse(outlinePen, px - size / 2, py - size / 2, size, size);
            }
        }

        private GradientPoint GetPointAt(int mouseX, int mouseY)
        {
            int margin = 20;
            int graphWidth = this.Width - 2 * margin;
            int graphHeight = this.Height - 2 * margin;

            foreach (var point in points)
            {
                float px = margin + point.Position * graphWidth;
                float py = margin + graphHeight - (point.Value * graphHeight);

                float dist = (float)Math.Sqrt((mouseX - px) * (mouseX - px) + (mouseY - py) * (mouseY - py));

                if (dist < 10)
                    return point;
            }

            return null;
        }

        private void GradientEditor_MouseDown(object sender, MouseEventArgs e)
        {
            int margin = 20;
            int graphWidth = this.Width - 2 * margin;
            int graphHeight = this.Height - 2 * margin;

            var point = GetPointAt(e.X, e.Y);

            if (e.Button == MouseButtons.Left)
            {
                if (point != null)
                {
                    // Start dragging existing point
                    selectedPoint = point;
                    isDragging = true;
                }
                else if (e.X >= margin && e.X <= margin + graphWidth &&
                         e.Y >= margin && e.Y <= margin + graphHeight)
                {
                    // Add new point
                    float pos = (e.X - margin) / (float)graphWidth;
                    float val = 1.0f - ((e.Y - margin) / (float)graphHeight);

                    pos = Math.Max(0, Math.Min(1, pos));
                    val = Math.Max(0, Math.Min(1, val));

                    var newPoint = new GradientPoint(pos, val);
                    points.Add(newPoint);
                    selectedPoint = newPoint;
                    isDragging = true;

                    this.Invalidate();
                }
            }
            else if (e.Button == MouseButtons.Right && point != null)
            {
                // Delete point (but keep at least 2)
                if (points.Count > 2)
                {
                    points.Remove(point);
                    selectedPoint = null;
                    this.Invalidate();
                }
            }
        }

        private void GradientEditor_MouseMove(object sender, MouseEventArgs e)
        {
            int margin = 20;
            int graphWidth = this.Width - 2 * margin;
            int graphHeight = this.Height - 2 * margin;

            if (isDragging && selectedPoint != null)
            {
                float pos = (e.X - margin) / (float)graphWidth;
                float val = 1.0f - ((e.Y - margin) / (float)graphHeight);

                selectedPoint.Position = Math.Max(0, Math.Min(1, pos));
                selectedPoint.Value = Math.Max(0, Math.Min(1, val));

                this.Invalidate();
            }
            else
            {
                var oldHover = hoverPoint;
                hoverPoint = GetPointAt(e.X, e.Y);

                if (oldHover != hoverPoint)
                    this.Invalidate();
            }
        }

        private void GradientEditor_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }
    }
}