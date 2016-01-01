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
        private Dictionary<ushort, bool> KeyStatus;

        public KeyboardDevice()
        {
            KeyBuffer = new Queue<ushort>();
            KeyStatus = new Dictionary<ushort, bool>();
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
            else if (dcpu.A == 3)
            {
                if (KeyStatus.ContainsKey(dcpu.C) && KeyStatus[dcpu.C])
                    dcpu.A = 1;
                else
                    dcpu.A = 0;
            }
        }

        public void UpdateInternal(Dcpu dcpu, long cyclesPassed)
        {
            
        }

        public void EnqueueKey(ushort key)
        {
            if (KeyBuffer.Count < 256)
                KeyBuffer.Enqueue(key);
        }

        public void SetKeyStatus(ushort key, bool down)
        {
            if (KeyStatus.ContainsKey(key))
                KeyStatus[key] = down;
            else
                KeyStatus.Add(key, down);
        }

        public void Shutdown()
        {

        }

        public override string ToString()
        {
            return "Keyboard";
        }
    }
}
