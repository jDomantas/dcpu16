using dcpu16.Assembler;
using dcpu16.Emulator;
using dcpu16.Hardware;
using dcpu16.Hardware.Clock;
using dcpu16.Hardware.ExternalDisk;
using dcpu16.Hardware.Keyboard;
using dcpu16.Hardware.Screen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace dcpu16
{
    class Program
    {
        private List<string> Devices;
        private ushort[] CurrentImage;

        private Program()
        {
            Devices = new List<string>();
            AddDefaultHardwareDevices();
            CurrentImage = null;
        }

        private void AddDefaultHardwareDevices()
        {
            Devices.Clear();

            Devices.Add("keyboard");
            Devices.Add("screen");
            Devices.Add("harddrive");
            Devices.Add("clock");
        }

        private void RunProgram(string[] args)
        {
            if (args.Length == 1)
            {
                // assemble and run program with default devices
                Assemble(new string[] { "asm", args[0] });
                RunEmulator(new string[] { "run" });
            }
            else if (args.Length == 2)
            {
                // assemble and write to file
                Assemble(new string[] { "asm", args[0], args[1] });
            }
            else
            {
                while (!ReadAndEvaluateCommand()) ;
            }
        }

        private bool ReadAndEvaluateCommand()
        {
            Console.Write("> ");
            string command = Console.ReadLine().ToLower();

            string[] parts = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                Help();
            else if (parts[0] == "help")
                Help();
            else if (parts[0] == "add")
                AddDevice(parts);
            else if (parts[0] == "remove")
                RemoveDevice(parts);
            else if (parts[0] == "list")
                ListDevices(parts);
            else if (parts[0] == "clear")
                ClearDevices(parts);
            else if (parts[0] == "run")
                RunEmulator(parts);
            else if (parts[0] == "asm")
                Assemble(parts);
            else if (parts[0] == "load")
                LoadBinary(parts);
            else if (parts[0] == "exit")
                return true;
            else
            {
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Write 'help' to see a list of commands");
            }

            return false;
        }

        private void Help()
        {
            Console.WriteLine("List of available commands:");
            Console.WriteLine("* run                   - runs currently loaded image");
            Console.WriteLine("* add [device]          - adds hardware device");
            Console.WriteLine("* remove [device]       - removes hardware device");
            Console.WriteLine("* list                  - lists hardware devices");
            Console.WriteLine("* clear                 - remove all hardware");
            Console.WriteLine("* asm [file]            - assemble and load file");
            Console.WriteLine("* asm [file] [output]   - assemble file and write to output file");
            Console.WriteLine("* load [file]           - load binary image");
        }

        private void RunEmulator(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Incorrect usage");
                Console.WriteLine("Usage: 'run'");
                return;
            }

            if (CurrentImage == null)
            {
                Console.WriteLine("Can't run, no image loaded");
                return;
            }

            IHardware[] hardware = new IHardware[Devices.Count];
            List<KeyboardDevice> keyboards = new List<KeyboardDevice>();
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i] == "screen") hardware[i] = new ScreenForm(keyboards);
                else if (Devices[i] == "keyboard") hardware[i] = new KeyboardDevice();
                else if (Devices[i] == "harddrive") hardware[i] = new HardDrive();
                else if (Devices[i] == "clock") hardware[i] = new Clock();

                if (Devices[i] == "keyboard") keyboards.Add((KeyboardDevice)hardware[i]);
            }

            Dcpu emulator = new Dcpu(hardware);
            for (int i = 0; i < CurrentImage.Length; i++)
                emulator.Memory[i] = CurrentImage[i];

            emulator.Run();
            Console.WriteLine("Program terminated");
        }

        private void AddDevice(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Incorrect usage");
                Console.WriteLine("Usage: add [device]");
                Console.WriteLine("For list of available devices, type 'list'");
                return;
            }

            if (args[1] == "screen")
                Devices.Add("screen");
            else if (args[1] == "keyboard")
                Devices.Add("keyboard");
            else if (args[1] == "harddrive")
                Devices.Add("harddrive");
            else if (args[1] == "clock")
                Devices.Add("clock");
            else
                Console.WriteLine($"Unknown device: {args[1]}");
        }

        private void RemoveDevice(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Incorrect usage");
                Console.WriteLine("Usage: remove [device]");
                Console.WriteLine("For list of added devices, type 'list'");
                return;
            }

            int index;
            if (int.TryParse(args[1], out index))
            {
                if (index < 1 || index > Devices.Count)
                    Console.WriteLine("Index out of range");
                else
                    Devices.RemoveAt(index - 1);
                return;
            }

            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i] == args[1])
                {
                    Devices.RemoveAt(i);
                    return;
                }
            }

            Console.WriteLine($"Device not found: {args[1]}");
        }

        private void ListDevices(string[] args)
        {
            for (int i = 0; i < Devices.Count; i++)
                Console.WriteLine($"{i + 1}. {Devices[i]}");

            Console.WriteLine("All available devices:");
            Console.WriteLine("* Keyboard");
            Console.WriteLine("* Screen");
            Console.WriteLine("* Harddrive");
            Console.WriteLine("* Clock");
        }

        private void ClearDevices(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Incorrect usage");
                Console.WriteLine("Usage: 'clear'");
                return;
            }

            Devices.Clear();
        }

        private void Assemble(string[] args)
        {
            string output = null;
            if (args.Length == 3)
                output = args[2];
            else if (args.Length != 2)
            {
                Console.WriteLine("Incorrect usage");
                Console.WriteLine("Usage: 'asm file' or 'asm file outputfile'");
                return;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't read file");
                Console.WriteLine(e.Message);
                return;
            }

            Assembler.Assembler asm = new Assembler.Assembler();
            asm.AssembleCode(lines);
            
            if (asm.GetErrors().Any())
            {
                Console.WriteLine("Errors:");
                foreach (var err in asm.GetErrors())
                    Console.WriteLine(err);
                return;
            }

            if (output != null)
            {
                try
                {
                    FileStream file = File.Open(output, FileMode.Create);
                    ushort[] memory = asm.GetMemoryDump();
                    for (int i = 0; i < memory.Length; i++)
                    {
                        file.WriteByte((byte)((memory[i] >> 8) & 0xFF));
                        file.WriteByte((byte)((memory[i]) & 0xFF));
                    }
                    file.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Couldn't write file");
                    Console.WriteLine(e.Message);
                    return;
                }
            }
            else
            {
                CurrentImage = asm.GetMemoryDump();
                Console.WriteLine($"Image loaded, image size: {CurrentImage.Length} words");
            }
        }
        
        private void LoadBinary(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Incorrect usage");
                Console.WriteLine("Usage: 'load file'");
                return;
            }

            CurrentImage = null;

            try
            {
                FileStream file = File.Open(args[1], FileMode.Open);
                if (file.Length % 2 != 0)
                {
                    Console.WriteLine($"File is {file.Length} bytes long");
                    Console.WriteLine("Must be a multiple of two");
                    return;
                }
                if (file.Length > 0x20000)
                {
                    Console.WriteLine($"File is {file.Length} bytes long");
                    Console.WriteLine($"Must be at most {0x20000}");
                    return;
                }

                ushort[] memory = new ushort[file.Length / 2];
                for (int i = 0; i < file.Length / 2; i++)
                {
                    int high = file.ReadByte();
                    int low = file.ReadByte();
                    memory[i] = (ushort)((high << 8) | low);
                }
                file.Close();
                CurrentImage = memory;
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't read file");
                Console.WriteLine(e.Message);
                return;
            }

            Console.WriteLine($"Image loaded, image size: {CurrentImage.Length} words");
        }

        static string InstructionToString(ushort instr)
        {
            return $"{(instr & 0x1F).ToString("X2")} {((instr >> 5) & 0x1F).ToString("X2")} {((instr >> 10) & 0x3F).ToString("X2")}";
        }
        
        static void Main(string[] args)
        {
            new Program().RunProgram(args);

#if DEBUG
            Console.ReadKey();
#endif
        }
    }
}
