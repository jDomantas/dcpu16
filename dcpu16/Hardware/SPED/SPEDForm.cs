using System;
using System.Drawing;
using System.Windows.Forms;
using dcpu16.Emulator;

namespace dcpu16.Hardware.SPED
{
    partial class SPEDForm : Form, IHardware
    {
        private Bitmap Buffer;
        private Graphics BufferGraphics;
        private int CurrentRotation;
        private int TargetRotation;

        private int MemoryMapOffset;
        private int VertexCount;

        private long CyclesToRefresh;
        private int CurrentVertex;

        private double LookingYaw = Math.PI / 6;
        private double LookingPitch = Math.PI / 12;
        
        private bool LeftPressed, RightPressed, UpPressed, DownPressed;

        public SPEDForm()
        {
            InitializeComponent();

            Buffer = new Bitmap(doubleBufferedPanel1.Width - 10, doubleBufferedPanel1.Height - 10);
            BufferGraphics = Graphics.FromImage(Buffer);

            doubleBufferedPanel1.Paint += DoubleBufferedPanel1_Paint;
            
            CurrentRotation = TargetRotation = 0;

            MemoryMapOffset = 0;
            VertexCount = 0;
            CyclesToRefresh = 0;
            CurrentVertex = 0;

            RefreshDisplay(null);

            Show();
        }
        
        private void DoubleBufferedPanel1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawImage(Buffer, new Rectangle(
                (doubleBufferedPanel1.Width - Buffer.Width) / 2,
                (doubleBufferedPanel1.Height - Buffer.Height) / 2,
                Buffer.Width,
                Buffer.Height));
        }

        public uint GetHardwareID()
        {
            return 0x42babf3c;
        }

        public ushort GetHardwareVersion()
        {
            return 0x0003;
        }

        public uint GetManufacturer()
        {
            return 0x1eb37e91;
        }

        public void Interrupt(Dcpu dcpu)
        {
            switch (dcpu.A)
            {
                case 0:
                    dcpu.C = 0; // no errors
                    if (VertexCount == 0)
                        dcpu.B = 0; // no vertices
                    else if (CurrentRotation == TargetRotation)
                        dcpu.B = 1; // has vertices, but isn't spinning
                    else
                        dcpu.B = 2; // has vertices and spinning
                    break;
                case 1:
                    MemoryMapOffset = dcpu.X;
                    VertexCount = dcpu.Y;
                    if (VertexCount > 128)
                        VertexCount = 128;
                    break;
                case 2:
                    TargetRotation = dcpu.X % 360;
                    break;
            }
        }

        public void Shutdown()
        {
            Close();
        }

        private void RefreshDisplay(Dcpu dcpu)
        {
            BufferGraphics.Clear(BackColor);

            Vector base1 = new Vector(-0.8, -0.8, -1.5);
            Vector base2 = new Vector(-0.8, 0.8, -1.5);
            Vector base3 = new Vector(0.8, 0.8, -1.5);
            Vector base4 = new Vector(0.8, -0.8, -1.5);

            double camDist = 6;
            double yaw = LookingYaw;
            Vector camera = new Vector(
                camDist * Math.Cos(yaw) * Math.Cos(LookingPitch),
                camDist * Math.Sin(yaw) * Math.Cos(LookingPitch),
                camDist * Math.Sin(LookingPitch));

            ViewMatrix matrix = new ViewMatrix(camera, Vector.Zero, Vector.UnitZ);

            base1 = matrix.Transform(base1);
            base2 = matrix.Transform(base2);
            base3 = matrix.Transform(base3);
            base4 = matrix.Transform(base4);

            Point b1 = base1.ToScreenCoordinates(Buffer.Width, Buffer.Height);
            Point b2 = base2.ToScreenCoordinates(Buffer.Width, Buffer.Height);
            Point b3 = base3.ToScreenCoordinates(Buffer.Width, Buffer.Height);
            Point b4 = base4.ToScreenCoordinates(Buffer.Width, Buffer.Height);

            BufferGraphics.FillPolygon(new SolidBrush(Color.DarkGray), new Point[] { b1, b2, b3, b4});

            yaw = LookingYaw + CurrentRotation * (Math.PI / 180.0);
            camera = new Vector(
                camDist * Math.Cos(yaw) * Math.Cos(LookingPitch),
                camDist * Math.Sin(yaw) * Math.Cos(LookingPitch),
                camDist * Math.Sin(LookingPitch));

            matrix = new ViewMatrix(camera, Vector.Zero, Vector.UnitZ);

            if (VertexCount > 0)
            {
                CurrentVertex = (CurrentVertex + 3) % VertexCount;

                for (int i = 0; i < VertexCount; i++)
                {
                    ushort w1 = dcpu.Memory[(MemoryMapOffset + i * 2) & 0xFFFF];
                    ushort w2 = dcpu.Memory[(MemoryMapOffset + i * 2 + 1) & 0xFFFF];
                    ushort p1 = dcpu.Memory[(MemoryMapOffset + (i + VertexCount - 1) % VertexCount * 2) & 0xFFFF];
                    ushort p2 = dcpu.Memory[(MemoryMapOffset + (i + VertexCount - 1) % VertexCount * 2 + 1) & 0xFFFF];

                    Vector pos = new Vector(
                        (w1 >> 8) / 255.0 * 2.0 - 1,
                        (w1 & 0xFF) / 255.0 * 2.0 - 1,
                        (w2 & 0xFF) / 255.0 * 2.0 - 1);

                    Vector prev = new Vector(
                        (p1 >> 8) / 255.0 * 2.0 - 1,
                        (p1 & 0xFF) / 255.0 * 2.0 - 1,
                        (p2 & 0xFF) / 255.0 * 2.0 - 1);

                    int c = (w2 >> 8) & 3;
                    bool intensive = (w2 & 0x0400) != 0;
                    int h = intensive ? 192 : 96;

                    int behind = (CurrentVertex + VertexCount - i) % VertexCount;
                    Color col = Color.FromArgb(144 - behind, c == 1 ? h : 0, c == 2 ? h : 0, c == 3 ? h : 0);

                    Point start = matrix.Transform(prev).ToScreenCoordinates(Buffer.Width, Buffer.Height);
                    Point end = matrix.Transform(pos).ToScreenCoordinates(Buffer.Width, Buffer.Height);
                    BufferGraphics.DrawLine(new Pen(col, 2), start, end);
                }
            }

            doubleBufferedPanel1.Invalidate();
        }

        public void UpdateInternal(Dcpu dcpu, long cyclesPassed)
        {
            Application.DoEvents();

            //LookingYaw += cyclesPassed / 100000.0;

            CyclesToRefresh -= cyclesPassed;
            if (CyclesToRefresh <= 0)
            {
                if (CurrentRotation != TargetRotation)
                {
                    int inc = (TargetRotation + 360 - CurrentRotation) % 360;
                    int dec = (CurrentRotation - TargetRotation + 360) % 360;
                    if (inc < dec)
                        CurrentRotation = (CurrentRotation + 1) % 360;
                    else
                        CurrentRotation = (CurrentRotation + 359) % 360;
                }
                
                // refresh screen at 20 Hz
                CyclesToRefresh = 2000 - (-CyclesToRefresh % 2000);
                RefreshDisplay(dcpu);
            }
        }
    }
}
