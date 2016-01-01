using System;
using System.IO;
using dcpu16.Emulator;

namespace dcpu16.Hardware.ExternalDisk
{
    class HardDrive : IHardware
    {
        private const int Size = 1024 * 1024;

        private ushort[] Memory;

        public HardDrive()
        {
            Memory = new ushort[Size];
            try
            {
                byte[] data = File.ReadAllBytes("harddrive.dat");
                for (int i = 0; i < Size; i++)
                    if (2 * i + 1 < data.Length)
                        Memory[i] = (ushort)((data[2 * i] << 8) | data[2 * i + 1]);
            }
            catch (Exception)
            {
                // no file or can't open
            }
        }

        public uint GetHardwareID()
        {
            return 0x4449535B;
        }

        public ushort GetHardwareVersion()
        {
            return 1;
        }

        public uint GetManufacturer()
        {
            return 0x44464C54;
        }

        public void Interrupt(Dcpu dcpu)
        {
            int start = (dcpu.X << 16) + dcpu.Y;
            switch (dcpu.A)
            {
                case 0:
                    dcpu.X = (ushort)((Size >> 16) & 0xFFFF);
                    dcpu.Y = (ushort)(Size & 0xFFFF);
                    break;
                case 1:
                    for (int i = 0; i < dcpu.C; i++)
                        dcpu.Memory[(dcpu.B + i) & 0xFFFF] = (start + i >= Size) ? (ushort)0 : Memory[start + i];
                    break;
                case 2:
                    for (int i = 0; i < dcpu.C && i + start < Size; i++)
                        Memory[start + i] = dcpu.Memory[(dcpu.B + i) & 0xFFFF];
                    break;
            }
        }

        public void Shutdown()
        {
            byte[] memory = new byte[Size * 2];
            for (int i = 0; i < Size; i++)
            {
                memory[2 * i] = (byte)((Memory[i] >> 8) & 0xFF);
                memory[2 * i + 1] = (byte)(Memory[i] & 0xFF);
            }

            try
            {
                File.WriteAllBytes("harddrive.dat", memory);
            }
            catch (Exception)
            {

            }
        }

        public void UpdateInternal(Dcpu dcpu, long cyclesPassed)
        {
            
        }
    }
}
