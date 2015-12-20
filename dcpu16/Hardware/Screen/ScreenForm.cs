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
        private static uint[] ColorPalette = new uint[]
        {
            0x000000, 0x000080, 0x008000, 0x008080,
            0x800000, 0x800080, 0x808000, 0xC0C0C0,
            0x808080, 0x0000FF, 0x00FF00, 0x00FFFF,
            0xFF0000, 0xFF00FF, 0xFFFF00, 0xFFFFFF,
        };

        #region Font
        private static uint[] PixelFont = new uint[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //  
            0x00, 0x08, 0x08, 0x08, 0x08, 0x00, 0x08, 0x00, // !
            0x00, 0x28, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00, // "
            0x00, 0x00, 0x28, 0x7C, 0x28, 0x7C, 0x28, 0x00, // #
            0x00, 0x08, 0x1E, 0x28, 0x1C, 0x0A, 0x3C, 0x08, // $
            0x00, 0x42, 0xA4, 0x48, 0x12, 0x25, 0x42, 0x00, // %
            0x00, 0x1C, 0x20, 0x20, 0x19, 0x26, 0x19, 0x00, // &
            0x00, 0x08, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, // '
            0x00, 0x08, 0x10, 0x20, 0x20, 0x10, 0x08, 0x00, // (
            0x00, 0x10, 0x08, 0x04, 0x04, 0x08, 0x10, 0x00, // )
            0x00, 0x08, 0x2A, 0x1C, 0x1C, 0x2A, 0x08, 0x00, // *
            0x00, 0x00, 0x08, 0x08, 0x3E, 0x08, 0x08, 0x00, // +
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x10, // ,
            0x00, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x00, 0x00, // -
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, // .
            0x00, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x00, // /
            0x00, 0x3C, 0x42, 0x4E, 0x72, 0x42, 0x3C, 0x00, // 0
            0x00, 0x08, 0x18, 0x08, 0x08, 0x08, 0x1C, 0x00, // 1
            0x00, 0x3C, 0x42, 0x04, 0x18, 0x20, 0x7E, 0x00, // 2
            0x00, 0x3C, 0x42, 0x0C, 0x02, 0x42, 0x3C, 0x00, // 3
            0x00, 0x08, 0x18, 0x28, 0x48, 0x7C, 0x08, 0x00, // 4
            0x00, 0x7E, 0x40, 0x7C, 0x02, 0x42, 0x3C, 0x00, // 5
            0x00, 0x3C, 0x40, 0x7C, 0x42, 0x42, 0x3C, 0x00, // 6
            0x00, 0x7E, 0x04, 0x08, 0x10, 0x20, 0x40, 0x00, // 7
            0x00, 0x3C, 0x42, 0x3C, 0x42, 0x42, 0x3C, 0x00, // 8
            0x00, 0x3C, 0x42, 0x42, 0x3E, 0x02, 0x3C, 0x00, // 9
            0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x08, 0x00, // :
            0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x08, 0x10, // ;
            0x00, 0x00, 0x06, 0x18, 0x60, 0x18, 0x06, 0x00, // <
            0x00, 0x00, 0x00, 0x7E, 0x00, 0x7E, 0x00, 0x00, // =
            0x00, 0x00, 0x60, 0x18, 0x06, 0x18, 0x60, 0x00, // >
            0x00, 0x38, 0x44, 0x04, 0x18, 0x00, 0x10, 0x00, // ?
            0x00, 0x1E, 0x21, 0x5D, 0x55, 0x5F, 0x20, 0x1E, // @
            0x00, 0x3C, 0x42, 0x42, 0x7E, 0x42, 0x42, 0x00, // A
            0x00, 0x78, 0x44, 0x78, 0x44, 0x44, 0x78, 0x00, // B
            0x00, 0x3C, 0x42, 0x40, 0x40, 0x42, 0x3C, 0x00, // C
            0x00, 0x7C, 0x42, 0x42, 0x42, 0x42, 0x7C, 0x00, // D
            0x00, 0x7C, 0x40, 0x70, 0x40, 0x40, 0x7C, 0x00, // E
            0x00, 0x7C, 0x40, 0x70, 0x40, 0x40, 0x40, 0x00, // F
            0x00, 0x3C, 0x42, 0x40, 0x4E, 0x42, 0x3C, 0x00, // G
            0x00, 0x42, 0x42, 0x7E, 0x42, 0x42, 0x42, 0x00, // H
            0x00, 0x3E, 0x08, 0x08, 0x08, 0x08, 0x3E, 0x00, // I
            0x00, 0x7C, 0x04, 0x04, 0x04, 0x44, 0x38, 0x00, // J
            0x00, 0x44, 0x48, 0x50, 0x70, 0x48, 0x44, 0x00, // K
            0x00, 0x40, 0x40, 0x40, 0x40, 0x40, 0x7C, 0x00, // L
            0x00, 0x41, 0x63, 0x55, 0x49, 0x41, 0x41, 0x00, // M
            0x00, 0x42, 0x62, 0x52, 0x4A, 0x46, 0x42, 0x00, // N
            0x00, 0x3C, 0x42, 0x42, 0x42, 0x42, 0x3C, 0x00, // O
            0x00, 0x7C, 0x42, 0x42, 0x7C, 0x40, 0x40, 0x00, // P
            0x00, 0x3C, 0x42, 0x42, 0x42, 0x42, 0x3C, 0x02, // Q
            0x00, 0x7C, 0x42, 0x42, 0x7C, 0x44, 0x42, 0x00, // R
            0x00, 0x3C, 0x42, 0x38, 0x04, 0x42, 0x3C, 0x00, // S
            0x00, 0x7F, 0x08, 0x08, 0x08, 0x08, 0x08, 0x00, // T
            0x00, 0x42, 0x42, 0x42, 0x42, 0x42, 0x3C, 0x00, // U
            0x00, 0x42, 0x42, 0x42, 0x24, 0x24, 0x18, 0x00, // V
            0x00, 0x41, 0x41, 0x49, 0x55, 0x63, 0x41, 0x00, // W
            0x00, 0x42, 0x24, 0x18, 0x18, 0x24, 0x42, 0x00, // x
            0x00, 0x41, 0x22, 0x14, 0x08, 0x08, 0x08, 0x00, // Y
            0x00, 0x7E, 0x04, 0x08, 0x10, 0x20, 0x7E, 0x00, // Z
            0x00, 0x38, 0x20, 0x20, 0x20, 0x20, 0x38, 0x00, // [
            0x00, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x00, // \
            0x00, 0x38, 0x08, 0x08, 0x08, 0x08, 0x38, 0x00, // ]
            0x00, 0x10, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00, // ^
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7E, 0x00, // _
            0x00, 0x10, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, // `
            0x00, 0x00, 0x3C, 0x02, 0x3E, 0x42, 0x3E, 0x00, // a
            0x00, 0x40, 0x40, 0x7C, 0x42, 0x42, 0x7C, 0x00, // b
            0x00, 0x00, 0x00, 0x3C, 0x40, 0x40, 0x3C, 0x00, // c
            0x00, 0x02, 0x02, 0x3E, 0x42, 0x42, 0x3E, 0x00, // d
            0x00, 0x00, 0x3C, 0x42, 0x7E, 0x40, 0x3C, 0x00, // e
            0x00, 0x08, 0x10, 0x10, 0x38, 0x10, 0x10, 0x00, // f
            0x00, 0x00, 0x3C, 0x44, 0x44, 0x3C, 0x04, 0x38, // g
            0x00, 0x20, 0x20, 0x38, 0x24, 0x24, 0x24, 0x00, // h
            0x00, 0x08, 0x00, 0x08, 0x08, 0x08, 0x08, 0x00, // i
            0x00, 0x08, 0x00, 0x18, 0x08, 0x08, 0x08, 0x30, // j
            0x00, 0x20, 0x20, 0x24, 0x28, 0x30, 0x2C, 0x00, // k
            0x00, 0x10, 0x10, 0x10, 0x10, 0x10, 0x18, 0x00, // l
            0x00, 0x00, 0x00, 0x66, 0x5A, 0x42, 0x42, 0x00, // m
            0x00, 0x00, 0x00, 0x2E, 0x32, 0x22, 0x22, 0x00, // n
            0x00, 0x00, 0x00, 0x3C, 0x42, 0x42, 0x3C, 0x00, // o
            0x00, 0x00, 0x7C, 0x42, 0x42, 0x7C, 0x40, 0x40, // p
            0x00, 0x00, 0x3E, 0x42, 0x42, 0x3E, 0x02, 0x02, // q
            0x00, 0x00, 0x00, 0x2C, 0x32, 0x20, 0x20, 0x00, // r
            0x00, 0x00, 0x1C, 0x20, 0x18, 0x04, 0x38, 0x00, // s
            0x00, 0x10, 0x38, 0x10, 0x10, 0x10, 0x08, 0x00, // t
            0x00, 0x00, 0x00, 0x22, 0x22, 0x22, 0x1E, 0x00, // u
            0x00, 0x00, 0x00, 0x42, 0x42, 0x24, 0x18, 0x00, // v
            0x00, 0x00, 0x00, 0x81, 0x81, 0x5A, 0x66, 0x00, // w
            0x00, 0x00, 0x42, 0x24, 0x18, 0x24, 0x42, 0x00, // x
            0x00, 0x00, 0x22, 0x22, 0x14, 0x08, 0x10, 0x20, // y
            0x00, 0x00, 0x00, 0x3C, 0x08, 0x10, 0x3C, 0x00, // z
            0x00, 0x0C, 0x10, 0x10, 0x20, 0x10, 0x10, 0x0C, // {
            0x00, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, // |
            0x00, 0x30, 0x08, 0x08, 0x04, 0x08, 0x08, 0x30, // }
            0x00, 0x00, 0x00, 0x32, 0x4C, 0x00, 0x00, 0x00, // ~
            0x00, 0x7E, 0x7E, 0x7E, 0x7E, 0x7E, 0x7E, 0x00, // 

        };
        #endregion

        private enum DisplayMode { Off, Terminal, Text, Graphical }
        private const int TextWidth = 24, TextHeight = 16;
        private const int ScreenWidth = TextWidth * 8, ScreenHeight = TextHeight * 8;

        private KeyboardDevice Keyboard;
        private Bitmap Display;
        private DisplayMode CurrentDisplayMode;
        private bool TextColorEnabled;
        private int CursorX, CursorY;
        private ushort[] InternalMemory;
        private byte[] DisplayData;
        private bool NeedsRefresh;

        public ScreenForm(KeyboardDevice keyboard)
        {
            InitializeComponent();

            CurrentDisplayMode = DisplayMode.Off;
            TextColorEnabled = false;
            Keyboard = keyboard;
            Display = new Bitmap(ScreenWidth, ScreenHeight, PixelFormat.Format32bppArgb);
            
            InternalMemory = new ushort[24 * 16];

            DisplayData = new byte[4 * ScreenWidth * ScreenHeight];

            if (Keyboard != null)
            {
                KeyDown += ScreenForm_KeyDown;
                KeyUp += ScreenForm_KeyUp;
                KeyPress += ScreenForm_KeyPress;
            }

            displayPanel.Paint += DisplayPanel_Paint;

            ShowScreensaver();
            NeedsRefresh = true;

            Show();
        }

        private void ScreenForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
                Keyboard.EnqueueKey(10);
            else
                Keyboard.EnqueueKey(e.KeyChar);
        }

        private void ScreenForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyValue < 128)
                Keyboard.SetKeyStatus((ushort)e.KeyValue, false);
        }

        private void ScreenForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue < 128)
                Keyboard.SetKeyStatus((ushort)e.KeyValue, true);
        }

        private void DisplayPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            e.Graphics.DrawImage(Display, new Rectangle(
                (displayPanel.Width - ScreenWidth * 4) / 2, 
                (displayPanel.Height - ScreenHeight * 4) / 2, 
                ScreenWidth * 4, 
                ScreenHeight * 4));
        }
        
        public void Interrupt(Dcpu dcpu)
        {
            switch (dcpu.A)
            {
                case 0: SetDisplayMode(dcpu.B); break;
                case 1: TextColorEnabled = (dcpu.B != 0); NeedsRefresh = true; break;
                case 2: WriteCharacter(dcpu.C); break;
                case 3: Backspace(); break;
                case 4: InitText(dcpu); break;
                case 5: SetChar(dcpu.X, dcpu.Y, dcpu.C); break;
            }
        }

        public uint GetHardwareID()
        {
            return 0x5644454F;
        }

        public ushort GetHardwareVersion()
        {
            return 1;
        }

        public uint GetManufacturer()
        {
            return 0x44464C54;
        }

        private void RenderText()
        {
            bool colors = TextColorEnabled || CurrentDisplayMode == DisplayMode.Off;
            byte foregroundMask = (byte)(colors ? 0 : 0xFF);
            byte backgroundMask = (byte)(colors ? 0xFF : 0);

            for (int x = 0; x < ScreenWidth; x++)
                for (int y = 0; y < ScreenHeight; y++)
                {
                    ushort dat = InternalMemory[(x >> 3) + (y >> 3) * TextWidth];
                    int ch = dat & 0xFF;
                    if (ch < 32 || ch >= 128) ch = 32;
                    ch -= 32;
                    if ((PixelFont[(ch << 3) + (y & 0x7)] & (1 << (7 - (x & 0x7)))) == 0) // background
                    {
                        DisplayData[(x + y * ScreenWidth) * 4 + 3] = 0xFF;
                        DisplayData[(x + y * ScreenWidth) * 4 + 2] = (byte)((ColorPalette[(dat >> 12) & 0xF] >> 16) & backgroundMask);
                        DisplayData[(x + y * ScreenWidth) * 4 + 1] = (byte)((ColorPalette[(dat >> 12) & 0xF] >> 8) & backgroundMask);
                        DisplayData[(x + y * ScreenWidth) * 4 + 0] = (byte)((ColorPalette[(dat >> 12) & 0xF]) & backgroundMask);
                    }
                    else // foreground
                    {
                        DisplayData[(x + y * ScreenWidth) * 4 + 3] = 0xFF;
                        DisplayData[(x + y * ScreenWidth) * 4 + 2] = (byte)((ColorPalette[(dat >> 8) & 0xF] >> 16) | foregroundMask);
                        DisplayData[(x + y * ScreenWidth) * 4 + 1] = (byte)((ColorPalette[(dat >> 8) & 0xF] >> 8) | foregroundMask);
                        DisplayData[(x + y * ScreenWidth) * 4 + 0] = (byte)((ColorPalette[(dat >> 8) & 0xF]) | foregroundMask);
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

        private void RenderGraphics()
        {

        }

        private void RefreshScreen()
        {
            if (!NeedsRefresh) return;
            NeedsRefresh = false;

            if (CurrentDisplayMode == DisplayMode.Graphical)
                RenderGraphics();
            else
                RenderText();

            displayPanel.Invalidate();
        }

        private void ShowScreensaver()
        {
            for (int x = 0; x < TextWidth; x++)
                for (int y = 0; y < TextHeight; y++)
                {
                    InternalMemory[x + y * TextWidth] = 0x1F00;
                }

            InternalMemory[TextWidth / 2 - 3 + 6 * TextWidth] |= 0x44;
            InternalMemory[TextWidth / 2 - 2 + 6 * TextWidth] |= 0x43;
            InternalMemory[TextWidth / 2 - 1 + 6 * TextWidth] |= 0x50;
            InternalMemory[TextWidth / 2 - 0 + 6 * TextWidth] |= 0x55;
            InternalMemory[TextWidth / 2 + 1 + 6 * TextWidth] |= 0x2D;
            InternalMemory[TextWidth / 2 + 2 + 6 * TextWidth] |= 0x31;
            InternalMemory[TextWidth / 2 + 3 + 6 * TextWidth] |= 0x36;
        }

        private void SetDisplayMode(ushort mode)
        {
            if (mode == 0) { CurrentDisplayMode = DisplayMode.Off; ShowScreensaver(); }
            else if (mode == 1) CurrentDisplayMode = DisplayMode.Terminal;
            else if (mode == 2) CurrentDisplayMode = DisplayMode.Text;
            else if (mode == 3) CurrentDisplayMode = DisplayMode.Graphical;
            else return;

            if (CurrentDisplayMode != DisplayMode.Off)
            {
                CursorX = 0;
                CursorY = 0;
                for (int i = 0; i < InternalMemory.Length; i++)
                    InternalMemory[i] = 0;
            }

            NeedsRefresh = true;
        }

        private void NewLine()
        {
            if (CursorY < TextHeight - 1)
            {
                CursorY++;
                CursorX = 0;
                return;
            }

            for (int y = 0; y < TextHeight - 1; y++)
                for (int x = 0; x < TextWidth; x++)
                    InternalMemory[x + TextWidth * y] = InternalMemory[x + TextWidth * (y + 1)];

            for (int x = 0; x < TextWidth; x++)
                InternalMemory[x + TextWidth * (TextHeight - 1)] = 0;

            CursorX = 0;
            NeedsRefresh = true;
        }

        private void WriteCharacter(ushort c)
        {
            if (CurrentDisplayMode != DisplayMode.Terminal) return;

            if (c == 10)
            {
                NewLine();
            }
            else
            {
                InternalMemory[CursorX + CursorY * TextWidth] = c;
                CursorX++;
            }

            if (CursorX == TextWidth)
                NewLine();

            NeedsRefresh = true;
        }

        private void Backspace()
        {
            if (CurrentDisplayMode != DisplayMode.Terminal) return;

            if (CursorX > 0)
            {
                CursorX--;
                InternalMemory[CursorX + CursorY * TextWidth] = 0;
                NeedsRefresh = true;
            }
        }

        private void InitText(Dcpu dcpu)
        {
            if (CurrentDisplayMode != DisplayMode.Text) return;

            for (int i = 0; i < TextWidth * TextHeight; i++)
                InternalMemory[i] = dcpu.Memory[dcpu.B + i];

            NeedsRefresh = true;
        }

        private void SetChar(int x, int y, ushort value)
        {
            if (x >= 0 && y >= 0 && x < TextWidth && y < TextHeight)
            {
                InternalMemory[x + y * TextWidth] = value;
                NeedsRefresh = true;
            }
        }

        public void UpdateInternal()
        {
            RefreshScreen();
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
    }
}
