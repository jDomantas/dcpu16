using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dcpu16.Emulator;

namespace dcpu16.Hardware.TextTerminal
{
    class TextTeminalDevice : IHardware
    {
        public TextTeminalDevice()
        {

        }

        public uint GetHardwareID()
        {
            // ASCII for "TEXT"
            return 0x54455854;
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
                if ((dcpu.C >= 32 && dcpu.C <= 127) || dcpu.C == 9 || dcpu.C == 10)
                    Console.Write((char)dcpu.C);
            }
            else if (dcpu.A == 1)
            {
                if (Console.CursorLeft > 0)
                {
                    Console.CursorLeft--;
                    Console.Write(" ");
                    Console.CursorLeft--;
                }
            }
        }

        public void Update()
        {
            
        }
    }
}
