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
        private bool[] KeyStatus;
        private ushort InterruptMessage;
        private int MessagesPending;

        public KeyboardDevice()
        {
            KeyBuffer = new Queue<ushort>();
            KeyStatus = new bool[65536];
            InterruptMessage = 0;
            MessagesPending = 0;
        }

        public uint GetHardwareID()
        {
            return 0x30cf7406;
        }

        public ushort GetHardwareVersion()
        {
            return 0x01;
        }

        public uint GetManufacturer()
        {
            return 0;
        }

        public void Interrupt(Dcpu dcpu)
        {
            switch (dcpu.A)
            {
                case 0: KeyBuffer.Clear(); break;
                case 1: dcpu.C = KeyBuffer.Count == 0 ? (ushort)0 : KeyBuffer.Dequeue(); break;
                case 2: dcpu.C = (ushort)(KeyStatus[dcpu.B] ? 1 : 0); break;
                case 3: InterruptMessage = dcpu.B; break;
            }
        }

        public void UpdateInternal(Dcpu dcpu, long cyclesPassed)
        {
            if (InterruptMessage != 0)
            {
                while (MessagesPending > 0)
                {
                    dcpu.QueueInterrupt(InterruptMessage);
                    MessagesPending--;
                }
            }
            else
                MessagesPending = 0;
        }

        public void EnqueueKey(ushort key)
        {
            MessagesPending++;
            if (KeyBuffer.Count < 256)
                KeyBuffer.Enqueue(key);
        }

        public void SetKeyStatus(ushort key, bool down)
        {
            KeyStatus[key] = down;
            MessagesPending++;
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
