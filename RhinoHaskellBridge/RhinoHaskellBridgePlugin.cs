using Rhino;
using Rhino.Commands;
using Rhino.PlugIns;
using RhinoHaskellBridge;
using System;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace RhinoHaskellBridge
{
    // The main plugin class
    public class RhinoHaskellBridgePlugin : PlugIn
    {
        public RhinoHaskellBridgePlugin()
        {
            Instance = this;
        }

        public static RhinoHaskellBridgePlugin Instance { get; private set; }

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            RhinoApp.WriteLine("RhinoHaskellBridge loaded successfully!");

            // TODO: Register panel later
            // Rhino.UI.Panels.RegisterPanel(this, typeof(GridGeneratorPanel), 
            //     "Grid Generator", null);

            return LoadReturnCode.Success;
        }
    }

    // Command to test connection to Haskell
    [System.Runtime.InteropServices.Guid("9C2FEC48-80D4-41A9-9F2C-2D6A19B468DA")]
    public class TestHaskellConnectionCommand : Command
    {
        public override string EnglishName => "TestHaskellConnection";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                // Connect to Haskell server
                string haskellIP = "192.168.1.168";  // CHANGE THIS to your Linux machine IP!
                int port = 8080;

                RhinoApp.WriteLine($"Connecting to Haskell at {haskellIP}:{port}...");

                TcpClient client = new TcpClient(haskellIP, port);
                NetworkStream stream = client.GetStream();

                // Send message
                byte[] data = Encoding.UTF8.GetBytes("Hello from Rhino C#!");
                stream.Write(data, 0, data.Length);
                RhinoApp.WriteLine("Sent: Hello from Rhino C#!");

                // Receive response
                byte[] buffer = new byte[1024];
                int bytes = stream.Read(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytes);

                RhinoApp.WriteLine($"Haskell responded: {response}");

                stream.Close();
                client.Close();

                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error: {ex.Message}");
                return Result.Failure;
            }
        }
    }

    [System.Runtime.InteropServices.Guid("64FB9064-3044-44E6-8793-F799BFD5A989")]
    public class ShowGridPanelCommand : Command
    {
        private static GridGeneratorPanel panelInstance;

        public override string EnglishName => "ShowGridPanel";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (panelInstance == null)
            {
                panelInstance = new GridGeneratorPanel();
            }

            // Show as a floating form
            Form form = new Form
            {
                Text = "Grid Generator",
                Size = new System.Drawing.Size(320, 250),
                FormBorderStyle = FormBorderStyle.SizableToolWindow
            };

            panelInstance.Dock = DockStyle.Fill;
            form.Controls.Add(panelInstance);
            form.Show();

            return Result.Success;
        }
    }
}

