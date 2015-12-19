using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dcpu16.Emulator;

namespace dcpu16.Hardware.Keyboard
{
    class KeyboardDevice : IHardware
    {
        private Queue<ushort> KeyBuffer;

        public KeyboardDevice()
        {
            KeyBuffer = new Queue<ushort>();
        }

        public uint GetHardwareID()
        {
            // ASCII for "KEYS"
            return 0x4B455953;
        }

        public ushort GetHardwareVersion()
        {
            return 0x01;
        }

        public uint GetManufacturer()
        {
            // ASCII for "DFLT"
            return 0x44464C54;
        }

        public void Interrupt(Dcpu dcpu)
        {
            if (dcpu.A == 0)
            {
                if (KeyBuffer.Count == 0)
                    dcpu.C = 0;
                else
                    dcpu.C = KeyBuffer.Dequeue();
            }
            else if (dcpu.A == 1)
            {
                if (KeyBuffer.Count == 0)
                    dcpu.C = 0;
                else
                    dcpu.C = 1;
            }
            else if (dcpu.A == 2)
            {
                KeyBuffer.Clear();
            }
        }

        public void Update()
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == 13) // change '\r' to '\n' to make more sense
                    KeyBuffer.Enqueue(10);
                else
                    KeyBuffer.Enqueue(key.KeyChar);
            }
        }
    }
}
