using dcpu16.Emulator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dcpu16.Hardware
{
    interface IHardware
    {
        void Interrupt(Dcpu dcpu);
        uint GetHardwareID();
        ushort GetHardwareVersion();
        uint GetManufacturer();
        void UpdateInternal(Dcpu dcpu, long cyclesPassed);
        void Shutdown();
    }
}
