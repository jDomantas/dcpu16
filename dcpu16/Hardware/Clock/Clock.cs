using dcpu16.Emulator;

namespace dcpu16.Hardware.Clock
{
    class Clock : IHardware
    {
        private ushort InterruptMessage;
        private int TickRate;
        private long CyclesPassed;

        public Clock()
        {
            InterruptMessage = 0;
            TickRate = 0;
            CyclesPassed = 0;
        }

        public uint GetHardwareID()
        {
            return 0x12d0b402;
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
                case 0: TickRate = dcpu.B; CyclesPassed = 0; break;
                case 1: dcpu.C = (ushort)(CountTicks(CyclesPassed) & 0xFFFF); break;
                case 2: InterruptMessage = dcpu.B; break;
            }
        }

        public void UpdateInternal(Dcpu dcpu, long cyclesPassed)
        {
            long prevTicks = CountTicks(CyclesPassed);
            CyclesPassed += cyclesPassed;
            long currentTicks = CountTicks(CyclesPassed);
            
            // call interrupts for passed ticks
            if (InterruptMessage != 0)
                for (long i = prevTicks; i < currentTicks; i++)
                    dcpu.QueueInterrupt(InterruptMessage);
        }
        
        public void Shutdown()
        {

        }

        private long CountTicks(long cycles)
        {
            if (TickRate == 0) return 0;

            // s = cyc / 100000
            // tps = 60 / rate
            // t = s * tps = (cyc / 100000) * (60 / rate) = cyc * 6 / (10000 * rate)
            return cycles * 6 / (TickRate * 10000);
        }

        public override string ToString()
        {
            return "Clock";
        }
    }
}
