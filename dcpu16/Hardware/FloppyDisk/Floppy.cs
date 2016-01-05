using System;
using System.IO;
using dcpu16.Emulator;

namespace dcpu16.Hardware.FloppyDisk
{
    class Floppy : IHardware
    {
        private const int DefaultSize = 737280; // 1440 * 1024 bytes
        
        private ushort[] Memory;
        private string File;

        public Floppy(string file)
        {
            File = file;

            try
            {
                byte[] data = System.IO.File.ReadAllBytes(file);
                Memory = new ushort[data.Length / 2];
                for (int i = 0; i < data.Length - 1; i += 2)
                    Memory[i / 2] = (ushort)((data[i] << 8) | data[i + 1]);
            }
            catch (Exception e)
            {
                // no file or can't open
                Console.WriteLine($"Can't open {file} for floppy, defaulting to {DefaultSize} size:");
                Console.WriteLine(e.Message);
                Memory = new ushort[DefaultSize];
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
                    dcpu.X = (ushort)((Memory.Length >> 16) & 0xFFFF);
                    dcpu.Y = (ushort)(Memory.Length & 0xFFFF);
                    break;
                case 1:
                    for (int i = 0; i < dcpu.C; i++)
                        dcpu.Memory[(((dcpu.B + i) & 0xFFFF) + dcpu.MemoryAccessOffset) & dcpu.MemoryMask] = 
                            (start + i > Memory.Length) ? 
                                (ushort)0 : 
                                Memory[start + i];
                    dcpu.CycleDebt += dcpu.C;
                    break;
                case 2:
                    for (int i = 0; i < dcpu.C && i + start < Memory.Length; i++)
                        Memory[start + i] = dcpu.Memory[(((dcpu.B + i) & 0xFFFF) + dcpu.MemoryAccessOffset) & dcpu.MemoryMask];
                    dcpu.CycleDebt += dcpu.C;
                    break;
            }
        }

        public void Shutdown()
        {
            byte[] memory = new byte[Memory.Length * 2];
            for (int i = 0; i < Memory.Length; i++)
            {
                memory[2 * i] = (byte)((Memory[i] >> 8) & 0xFF);
                memory[2 * i + 1] = (byte)(Memory[i] & 0xFF);
            }

            try
            {
                System.IO.File.WriteAllBytes(File, memory);
            }
            catch (Exception)
            {
                Console.WriteLine($"Couldn't save floppy to {File}");
            }
        }

        public void UpdateInternal(Dcpu dcpu, long cyclesPassed)
        {
            
        }
    }
}
