using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using dcpu16.Emulator;
using dcpu16.Hardware.Keyboard;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace dcpu16.Hardware.Screen
{
    partial class ScreenForm : Form, IHardware
    {
        private static ushort[] ColorPalette = new ushort[]
        {
            0x000, 0xFFF, 0x800, 0xAFE,
            0xC4C, 0x0C5, 0x00A, 0xEE7,
            0xD85, 0x640, 0xF77, 0x333,
            0x777, 0xAF6, 0x08F, 0xBBB
        };

        #region Font
        private static ushort[] PixelFont = new ushort[]
        {
            0x0000, 0x0000, // system symbol
            0x0000, 0x0000, // system symbol
            0x0000, 0x0000, // system symbol
            0x0000, 0x0000, // system symbol
            0x0000, 0x0000, // system symbol
            0x0000, 0x0000, // system symbol
            0x0010, 0x0000, // system symbol
            0x1010, 0x1010, // system symbol
            0x00ff, 0x0000, // system symbol
            0x00f0, 0x1010, // system symbol
            0x10f0, 0x0000, // system symbol
            0x101f, 0x0000, // system symbol
            0x001f, 0x1010, // system symbol
            0x10ff, 0x1010, // system symbol
            0x10f0, 0x1010, // system symbol
            0x10ff, 0x0000, // system symbol
            0x101f, 0x1010, // system symbol
            0x10ff, 0x1010, // system symbol
            0x6633, 0x99cc, // system symbol
            0x9933, 0x66cc, // system symbol
            0xfef8, 0xe080, // system symbol
            0x7f1f, 0x0701, // system symbol
            0x0107, 0x1f7f, // system symbol
            0x80e0, 0xf8fe, // system symbol
            0x5500, 0xaa00, // system symbol
            0x55aa, 0x55aa, // system symbol
            0xffaa, 0xff55, // system symbol
            0x0f0f, 0x0f0f, // system symbol
            0xf0f0, 0xf0f0, // system symbol
            0x0000, 0xffff, // system symbol
            0xffff, 0x0000, // system symbol
            0xffff, 0xffff, // system symbol
            0x0000, 0x0000, //  
            0x00be, 0x0000, // !
            0x0600, 0x0600, // "
            0x7c28, 0x7c00, // #
            0x4cd6, 0x6400, // $
            0xc238, 0x8600, // %
            0x6c52, 0xeca0, // &
            0x0006, 0x0000, // '
            0x3844, 0x8200, // (
            0x8244, 0x3800, // )
            0x2810, 0x2800, // *
            0x1038, 0x1000, // +
            0x8040, 0x0000, // ,
            0x1010, 0x1000, // -
            0x0080, 0x0000, // .
            0xc038, 0x0600, // /
            0x7c92, 0x7c00, // 0
            0x84fe, 0x8000, // 1
            0xc4b2, 0x8c00, // 2
            0x4492, 0x6c00, // 3
            0x1e10, 0xfe00, // 4
            0x4e8a, 0x7200, // 5
            0x7c92, 0x6400, // 6
            0xc232, 0x0e00, // 7
            0x6c92, 0x6c00, // 8
            0x4c92, 0x7c00, // 9
            0x0048, 0x0000, // :
            0x8058, 0x0000, // ;
            0x1028, 0x4400, // <
            0x2828, 0x2800, // =
            0x4428, 0x1000, // >
            0x04b2, 0x0c00, // ?
            0x7cb2, 0xbe00, // @
            0xfc12, 0xfc00, // A
            0xfe92, 0x6c00, // B
            0x7c82, 0x8200, // C
            0xfe82, 0x7c00, // D
            0xfe92, 0x8200, // E
            0xfe12, 0x0200, // F
            0x7c82, 0xf200, // G
            0xfe10, 0xfe00, // H
            0x82fe, 0x8200, // I
            0x4080, 0x7e00, // J
            0xfe10, 0xee00, // K
            0xfe80, 0x8000, // L
            0xfe0c, 0xfe00, // M
            0xfe02, 0xfc00, // N
            0x7c82, 0x7c00, // O
            0xfe12, 0x0c00, // P
            0x7cc2, 0xfc00, // Q
            0xfe12, 0xec00, // R
            0x4c92, 0x6400, // S
            0x02fe, 0x0200, // T
            0x7e80, 0xfe00, // U
            0x3ec0, 0x3e00, // V
            0xfe60, 0xfe00, // W
            0xee10, 0xee00, // X
            0x0ef0, 0x0e00, // Y
            0xe292, 0x8e00, // Z
            0xfe82, 0x0000, // [
            0x0638, 0xc000, // \
            0x82fe, 0x0000, // ]
            0x0402, 0x0400, // ^
            0x8080, 0x8000, // _
            0x0002, 0x0400, // `
            0x48a8, 0xf000, // a
            0xfe88, 0x7000, // b
            0x7088, 0x5000, // c
            0x7088, 0xfe00, // d
            0x70a8, 0xb000, // e
            0x10fc, 0x1200, // f
            0x90a8, 0x7800, // g
            0xfe08, 0xf000, // h
            0x08fa, 0x0000, // i
            0x4080, 0x7a00, // j
            0xfe20, 0xd800, // k
            0x02fe, 0x0000, // l
            0xf830, 0xf800, // m
            0xf808, 0xf000, // n
            0x7088, 0x7000, // o
            0xf828, 0x1000, // p
            0x1028, 0xf800, // q
            0xf808, 0x1000, // r
            0x90a8, 0x4800, // s
            0x087c, 0x8800, // t
            0x7880, 0xf800, // u
            0x38c0, 0x3800, // v
            0xf860, 0xf800, // w
            0xd820, 0xd800, // x
            0x98a0, 0x7800, // y
            0xc8a8, 0x9800, // z
            0x106c, 0x8200, // {
            0x00ee, 0x0000, // |
            0x826c, 0x1000, // }
            0x0402, 0x0402, // ~
            0x040a, 0x0400, // system symbol
        };
        #endregion

        private const int TextWidth = 32, TextHeight = 12;
        private const int ScreenWidth = TextWidth * 4, ScreenHeight = TextHeight * 8;

        private List<KeyboardDevice> Keyboards;
        private Bitmap Display;
        private byte[] DisplayData;
        private int CurrentMemoryMap;
        private int CurrentFontMap;
        private int CurrentPalleteMap;
        private int FrameCounter;
        private long CyclesToNextRefresh;

        public ScreenForm(List<KeyboardDevice> keyboards)
        {
            InitializeComponent();
            
            Keyboards = keyboards;
            Display = new Bitmap(ScreenWidth, ScreenHeight, PixelFormat.Format32bppArgb);
            
            DisplayData = new byte[4 * ScreenWidth * ScreenHeight];

            PreviewKeyDown += ScreenForm_PreviewKeyDown;
            KeyDown += ScreenForm_KeyDown;
            KeyUp += ScreenForm_KeyUp;
            KeyPress += ScreenForm_KeyPress;

            displayPanel.Paint += DisplayPanel_Paint;

            CurrentFontMap = 0;
            CurrentMemoryMap = 0;
            CurrentPalleteMap = 0;
            RefreshScreen(null);

            Show();
        }

        private void ScreenForm_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                    e.IsInputKey = true;
                    break;
            }
        }

        private void ScreenForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            for (int i = 0; i < Keyboards.Count; i++)
            {
                if (e.KeyChar == 13)
                    Keyboards[i].EnqueueKey(10);
                else
                    Keyboards[i].EnqueueKey(e.KeyChar);
            }
        }

        private void ScreenForm_KeyUp(object sender, KeyEventArgs e)
        {
            for (int i = 0; i < Keyboards.Count; i++)
            {
                if (e.KeyValue < 128)
                    Keyboards[i].SetKeyStatus((ushort)e.KeyValue, false);
            }
        }

        private void ScreenForm_KeyDown(object sender, KeyEventArgs e)
        {
            for (int i = 0; i < Keyboards.Count; i++)
            {
                switch (e.KeyCode)
                {
                    case Keys.Up: Keyboards[i].EnqueueKey(0x80); break;
                    case Keys.Right: Keyboards[i].EnqueueKey(0x81); break;
                    case Keys.Down: Keyboards[i].EnqueueKey(0x82); break;
                    case Keys.Left: Keyboards[i].EnqueueKey(0x83); break;
                }

                if (e.KeyValue < 128)
                    Keyboards[i].SetKeyStatus((ushort)e.KeyValue, true);
            }
        }

        private void DisplayPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            e.Graphics.DrawImage(Display, new Rectangle(
                (displayPanel.Width - ScreenWidth * 5) / 2, 
                (displayPanel.Height - ScreenHeight * 5) / 2, 
                ScreenWidth * 5, 
                ScreenHeight * 5));
        }
        
        public void Interrupt(Dcpu dcpu)
        {
            switch (dcpu.A)
            {
                case 0: CurrentMemoryMap = dcpu.B; break;
                case 1: CurrentFontMap = dcpu.B; break;
                case 2: CurrentPalleteMap = dcpu.B; break;
                case 3:
                    ushort color = CurrentPalleteMap == 0 ? 
                        ColorPalette[dcpu.B & 0xF] : 
                        dcpu.Memory[(CurrentPalleteMap + (dcpu.B & 0xF)) & 0xFFFF];
                    BackColor = Color.FromArgb((color & 0xF00) >> 4, color & 0xF0, (color & 0xF) << 4);
                    break;
                case 4:
                    for (int i = 0; i < 256; i++)
                        dcpu.Memory[(dcpu.B + i) & 0xFFFF] = CurrentFontMap == 0 ? 
                            PixelFont[i] : 
                            dcpu.Memory[(CurrentFontMap + i) & 0xFFFF];
                    dcpu.CycleDebt += 256;
                    break;
                case 5:
                    for (int i = 0; i < 16; i++)
                        dcpu.Memory[(dcpu.B + i) & 0xFFFF] = CurrentPalleteMap == 0 ? 
                            ColorPalette[i] : 
                            dcpu.Memory[(CurrentPalleteMap + i) & 0xFFFF];
                    dcpu.CycleDebt += 16;
                    break;
            }
        }

        public uint GetHardwareID()
        {
            return 0x7349f615;
        }

        public ushort GetHardwareVersion()
        {
            return 0x1802;
        }

        public uint GetManufacturer()
        {
            return 0x1c6c8b36; // NYA_ELEKTRISKA
        }

        private void RenderText(Dcpu dcpu)
        {
            FrameCounter++;

            for (int x = 0; x < ScreenWidth; x++)
                for (int y = 0; y < ScreenHeight; y++)
                {
                    ushort symbol = CurrentMemoryMap == 0 ? (ushort)0 : dcpu.Memory[(CurrentMemoryMap + (x >> 2) + (y >> 3) * TextWidth) & 0xFFFF];
                    int ch = symbol & 0x7F;

                    ushort foreground = CurrentPalleteMap == 0 ? 
                        ColorPalette[symbol >> 12] : 
                        dcpu.Memory[(CurrentPalleteMap + (symbol >> 12)) & 0xFFFF];

                    ushort background = CurrentPalleteMap == 0 ?
                        ColorPalette[(symbol >> 8) & 0xF] :
                        dcpu.Memory[(CurrentPalleteMap + ((symbol >> 8) & 0xF)) & 0xFFFF];

                    if ((symbol & 0x80) != 0 && (FrameCounter & 32) == 0)
                        foreground = background; // blinking characters

                    int fontData = CurrentFontMap == 0 ?
                        PixelFont[ch * 2 + ((x & 2) >> 1)] :
                        dcpu.Memory[(CurrentFontMap + ch * 2 + ((x & 2) >> 2)) & 0xFFFF];

                    if ((fontData & (1 << ((y & 0x7) + 8 * (1 - (x & 1))))) == 0) // background
                    {
                        DisplayData[(x + y * ScreenWidth) * 4 + 3] = 0xFF;
                        DisplayData[(x + y * ScreenWidth) * 4 + 2] = (byte)((background & 0xF00) >> 4);
                        DisplayData[(x + y * ScreenWidth) * 4 + 1] = (byte)((background & 0x0F0));
                        DisplayData[(x + y * ScreenWidth) * 4 + 0] = (byte)((background & 0x00F) << 4);
                    }
                    else // foreground
                    {
                        DisplayData[(x + y * ScreenWidth) * 4 + 3] = 0xFF;
                        DisplayData[(x + y * ScreenWidth) * 4 + 2] = (byte)((foreground & 0xF00) >> 4);
                        DisplayData[(x + y * ScreenWidth) * 4 + 1] = (byte)((foreground & 0x0F0));
                        DisplayData[(x + y * ScreenWidth) * 4 + 0] = (byte)((foreground & 0x00F) << 4);
                    }
                }

            var bitmapData = Display.LockBits(
                new Rectangle(0, 0, ScreenWidth, ScreenHeight), 
                ImageLockMode.ReadWrite, 
                PixelFormat.Format32bppArgb);

            var length = bitmapData.Stride * bitmapData.Height;
            
            Marshal.Copy(DisplayData, 0, bitmapData.Scan0, length);
            Display.UnlockBits(bitmapData);
        }
        
        private void RefreshScreen(Dcpu dcpu)
        {
            RenderText(dcpu);

            displayPanel.Invalidate();
        }
        
        public void UpdateInternal(Dcpu dcpu, long cyclesPassed)
        {
            CyclesToNextRefresh -= cyclesPassed;
            if (CyclesToNextRefresh <= 0)
            {
                RefreshScreen(dcpu);
                CyclesToNextRefresh = 2000 - (-CyclesToNextRefresh % 2000);
            }

            Application.DoEvents();
        }

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
        }

        public void Shutdown()
        {
            Close();
        }

        public override string ToString()
        {
            return "Screen";
        }

        private const int CP_NOCLOSE_BUTTON = 0x200;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams myCp = base.CreateParams;
                myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
                return myCp;
            }
        }
    }
}
