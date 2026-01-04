using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace RhinoHaskellBridge
{
    [System.Runtime.InteropServices.Guid("EA5D9814-48A6-4F09-9F99-ED33A068AA6C")]
    public class GridGeneratorPanel : UserControl
    {
        private Button selectSurfacesButton;
        private Button clearButton;
        private Button sendToHaskellButton;
        private ListBox surfaceListBox;
        private TextBox statusTextBox;
        private List<Guid> selectedSurfaceIds = new List<Guid>();

        // Gradient controls
        private TrackBar slider1, slider2, slider3, slider4, slider5;
        private Label gradientLabel;
        private Panel gradientPreview;

        public GridGeneratorPanel()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Size = new Size(320, 550);
            int yPos = 10;

            // Select Surfaces Button
            selectSurfacesButton = new Button
            {
                Text = "Add Surfaces (Right-click to finish)",
                Location = new Point(10, yPos),
                Size = new Size(300, 30)
            };
            selectSurfacesButton.Click += SelectSurfaces_Click;
            yPos += 40;

            // Surface List
            var listLabel = new Label
            {
                Text = "Selected Surfaces:",
                Location = new Point(10, yPos),
                Size = new Size(300, 20)
            };
            yPos += 25;

            surfaceListBox = new ListBox
            {
                Location = new Point(10, yPos),
                Size = new Size(300, 60)
            };
            yPos += 70;

            // Gradient Section
            gradientLabel = new Label
            {
                Text = "Z-Height Gradient (0=none, 100=full):",
                Location = new Point(10, yPos),
                Size = new Size(300, 20)
            };
            yPos += 25;

            // Gradient preview panel
            gradientPreview = new Panel
            {
                Location = new Point(10, yPos),
                Size = new Size(300, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            gradientPreview.Paint += GradientPreview_Paint;
            yPos += 40;

            // Create 5 sliders for gradient control
            int sliderSpacing = 35;
            slider1 = CreateSlider(10, yPos, "Start");
            yPos += sliderSpacing;
            slider2 = CreateSlider(10, yPos, "25%");
            yPos += sliderSpacing;
            slider3 = CreateSlider(10, yPos, "50%");
            yPos += sliderSpacing;
            slider4 = CreateSlider(10, yPos, "75%");
            yPos += sliderSpacing;
            slider5 = CreateSlider(10, yPos, "End");
            yPos += sliderSpacing + 10;

            // Clear Button
            clearButton = new Button
            {
                Text = "Clear",
                Location = new Point(10, yPos),
                Size = new Size(145, 30)
            };
            clearButton.Click += Clear_Click;

            // Send to Haskell Button
            sendToHaskellButton = new Button
            {
                Text = "Generate Grid",
                Location = new Point(165, yPos),
                Size = new Size(145, 30),
                Enabled = false
            };
            sendToHaskellButton.Click += SendToHaskell_Click;
            yPos += 40;

            // Status TextBox
            statusTextBox = new TextBox
            {
                Location = new Point(10, yPos),
                Size = new Size(300, 90),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };

            this.Controls.Add(selectSurfacesButton);
            this.Controls.Add(listLabel);
            this.Controls.Add(surfaceListBox);
            this.Controls.Add(gradientLabel);
            this.Controls.Add(gradientPreview);
            this.Controls.Add(clearButton);
            this.Controls.Add(sendToHaskellButton);
            this.Controls.Add(statusTextBox);
        }

        private TrackBar CreateSlider(int x, int y, string labelText)
        {
            var label = new Label
            {
                Text = labelText,
                Location = new Point(x, y + 5),
                Size = new Size(50, 20)
            };

            var slider = new TrackBar
            {
                Location = new Point(x + 55, y),
                Size = new Size(200, 30),
                Minimum = 0,
                Maximum = 100,
                Value = 100,
                TickFrequency = 10
            };
            slider.ValueChanged += Slider_ValueChanged;

            var valueLabel = new Label
            {
                Text = "100",
                Location = new Point(x + 260, y + 5),
                Size = new Size(40, 20)
            };
            slider.Tag = valueLabel;  // Store reference to update value

            this.Controls.Add(label);
            this.Controls.Add(slider);
            this.Controls.Add(valueLabel);

            return slider;
        }

        private void Slider_ValueChanged(object sender, EventArgs e)
        {
            var slider = sender as TrackBar;
            var label = slider.Tag as Label;
            label.Text = slider.Value.ToString();

            // Refresh gradient preview
            gradientPreview.Invalidate();
        }

        private void GradientPreview_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            int width = gradientPreview.Width;
            int height = gradientPreview.Height;

            // Draw gradient based on slider values
            var values = new[] { slider1.Value, slider2.Value, slider3.Value, slider4.Value, slider5.Value };

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)width;
                float value = InterpolateGradient(t, values);
                int gray = (int)(value * 2.55f);  // 0-100 to 0-255

                using (var pen = new Pen(Color.FromArgb(gray, gray, gray)))
                {
                    g.DrawLine(pen, x, 0, x, height);
                }
            }
        }

        private float InterpolateGradient(float t, int[] values)
        {
            // t is 0-1, interpolate between the 5 values
            float scaledT = t * (values.Length - 1);
            int index = (int)scaledT;

            if (index >= values.Length - 1)
                return values[values.Length - 1];

            float frac = scaledT - index;
            return values[index] * (1 - frac) + values[index + 1] * frac;
        }

        private void SelectSurfaces_Click(object sender, EventArgs e)
        {
            var go = new Rhino.Input.Custom.GetObject();
            go.SetCommandPrompt("Select surfaces (Right-click or Enter when done)");
            go.GeometryFilter = ObjectType.Surface;
            go.GroupSelect = false;
            go.SubObjectSelect = false;
            go.EnableClearObjectsOnEntry(false);
            go.EnableUnselectObjectsOnExit(false);
            go.DeselectAllBeforePostSelect = false;

            while (true)
            {
                var result = go.GetMultiple(1, 0);

                if (result == Rhino.Input.GetResult.Object)
                {
                    for (int i = 0; i < go.ObjectCount; i++)
                    {
                        var objRef = go.Object(i);
                        var id = objRef.ObjectId;

                        if (!selectedSurfaceIds.Contains(id))
                        {
                            selectedSurfaceIds.Add(id);
                            surfaceListBox.Items.Add($"Surface {selectedSurfaceIds.Count}");
                        }
                    }

                    statusTextBox.AppendText($"Added {go.ObjectCount} surface(s). Total: {selectedSurfaceIds.Count}\r\n");
                    sendToHaskellButton.Enabled = selectedSurfaceIds.Count > 0;
                    break;
                }
                else
                {
                    statusTextBox.AppendText($"Selection finished. Total: {selectedSurfaceIds.Count} surface(s)\r\n");
                    break;
                }
            }

            sendToHaskellButton.Enabled = selectedSurfaceIds.Count > 0;
        }

        private void Clear_Click(object sender, EventArgs e)
        {
            selectedSurfaceIds.Clear();
            surfaceListBox.Items.Clear();
            sendToHaskellButton.Enabled = false;
            statusTextBox.AppendText("Selection cleared.\r\n");
        }

        private void SendToHaskell_Click(object sender, EventArgs e)
        {
            if (selectedSurfaceIds.Count == 0)
            {
                statusTextBox.AppendText("No surfaces selected!\r\n");
                return;
            }

            try
            {
                // Get gradient values
                var gradientValues = new[]
                {
                    slider1.Value / 100.0,
                    slider2.Value / 100.0,
                    slider3.Value / 100.0,
                    slider4.Value / 100.0,
                    slider5.Value / 100.0
                };

                var surfacesJson = new System.Text.StringBuilder();
                surfacesJson.Append("{");
                surfacesJson.Append("\"gradient\":[");
                for (int i = 0; i < gradientValues.Length; i++)
                {
                    surfacesJson.Append(gradientValues[i]);
                    if (i < gradientValues.Length - 1) surfacesJson.Append(",");
                }
                surfacesJson.Append("],");

                surfacesJson.Append("\"surfaces\":[");

                for (int surfIdx = 0; surfIdx < selectedSurfaceIds.Count; surfIdx++)
                {
                    var rhinoObject = RhinoDoc.ActiveDoc.Objects.Find(selectedSurfaceIds[surfIdx]);
                    var brep = rhinoObject.Geometry as Brep;

                    if (brep == null || brep.Faces.Count == 0)
                    {
                        statusTextBox.AppendText($"Surface {surfIdx + 1} is not valid, skipping...\r\n");
                        continue;
                    }

                    var surface = brep.Faces[0];
                    var nurbsSurface = surface.ToNurbsSurface();

                    int uCount = nurbsSurface.Points.CountU;
                    int vCount = nurbsSurface.Points.CountV;
                    int uDegree = nurbsSurface.Degree(0);
                    int vDegree = nurbsSurface.Degree(1);

                    surfacesJson.Append("{");
                    surfacesJson.Append($"\"id\":\"{selectedSurfaceIds[surfIdx]}\",");
                    surfacesJson.Append($"\"uCount\":{uCount},");
                    surfacesJson.Append($"\"vCount\":{vCount},");
                    surfacesJson.Append($"\"uDegree\":{uDegree},");
                    surfacesJson.Append($"\"vDegree\":{vDegree},");

                    // Control points
                    surfacesJson.Append("\"controlPoints\":[");
                    for (int u = 0; u < uCount; u++)
                    {
                        surfacesJson.Append("[");
                        for (int v = 0; v < vCount; v++)
                        {
                            var pt = nurbsSurface.Points.GetControlPoint(u, v);
                            surfacesJson.Append($"[{pt.Location.X},{pt.Location.Y},{pt.Location.Z},{pt.Weight}]");
                            if (v < vCount - 1) surfacesJson.Append(",");
                        }
                        surfacesJson.Append("]");
                        if (u < uCount - 1) surfacesJson.Append(",");
                    }
                    surfacesJson.Append("],");

                    // U knots
                    surfacesJson.Append("\"uKnots\":[");
                    for (int i = 0; i < nurbsSurface.KnotsU.Count; i++)
                    {
                        surfacesJson.Append(nurbsSurface.KnotsU[i]);
                        if (i < nurbsSurface.KnotsU.Count - 1) surfacesJson.Append(",");
                    }
                    surfacesJson.Append("],");

                    // V knots
                    surfacesJson.Append("\"vKnots\":[");
                    for (int i = 0; i < nurbsSurface.KnotsV.Count; i++)
                    {
                        surfacesJson.Append(nurbsSurface.KnotsV[i]);
                        if (i < nurbsSurface.KnotsV.Count - 1) surfacesJson.Append(",");
                    }
                    surfacesJson.Append("]");

                    surfacesJson.Append("}");
                    if (surfIdx < selectedSurfaceIds.Count - 1) surfacesJson.Append(",");
                }

                surfacesJson.Append("]}");
                string message = surfacesJson.ToString();

                statusTextBox.AppendText($"Sending {selectedSurfaceIds.Count} surface(s) with gradient...\r\n");

                // Send to Haskell
                string haskellIP = "192.168.1.168";
                int port = 8080;

                TcpClient client = new TcpClient(haskellIP, port);
                NetworkStream stream = client.GetStream();

                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);

                statusTextBox.AppendText("Sent!\r\n");

                // Receive response
                byte[] buffer = new byte[8192];
                int bytes = stream.Read(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytes);

                statusTextBox.AppendText($"Haskell: {response}\r\n");

                stream.Close();
                client.Close();
            }
            catch (Exception ex)
            {
                statusTextBox.AppendText($"Error: {ex.Message}\r\n");
            }
        }
    }
}