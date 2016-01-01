using dcpu16.Assembler;
using dcpu16.Emulator;
using dcpu16.Hardware;
using dcpu16.Hardware.Clock;
using dcpu16.Hardware.ExternalDisk;
using dcpu16.Hardware.Keyboard;
using dcpu16.Hardware.Screen;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace dcpu16
{
    class Program
    {
        static ushort[] LoadBinaryFile(string path)
        {
            byte[] fileData = null;

            try
            {
                fileData = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Couldn't open {path}");
                Console.WriteLine(e.Message);
                Environment.Exit(0);
            }

            if (fileData.Length % 2 != 0)
            {
                Console.WriteLine($"Invalid binary file, length is not even: {fileData.Length}");
                Environment.Exit(0);
            }

            if (fileData.Length > 0x20000)
            {
                Console.WriteLine($"Invalid binary file, length is too big: {fileData.Length}");
                Environment.Exit(0);
            }

            ushort[] words = new ushort[fileData.Length / 2];
            for (int i = 0; i < words.Length; i++)
                words[i] = (ushort)((fileData[2 * i] << 8) + fileData[2 * i + 1]);

            return words;
        }

        static ushort[] AssembleSource(string path)
        {
            string[] lines = null;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to read file: {path}");
                Console.WriteLine(e.Message);
                Environment.Exit(0);
            }

            Assembler.Assembler asm = new Assembler.Assembler();
            asm.AssembleCode(lines);
            bool haveErrors = false;
            foreach (string error in asm.GetErrors())
            {
                Console.WriteLine($"Error: {error}");
                haveErrors = true;
            }

            if (haveErrors)
                Environment.Exit(0);

            return asm.GetMemoryDump();
        }

        static void RunEmulator(ushort[] memoryImage, List<string> hardware)
        {
            if (hardware.Count == 0)
            {
                hardware.Add("clock");
                hardware.Add("keyboard");
                hardware.Add("lem");
                hardware.Add("floppy");
            }

            List<IHardware> hardwareDevices = new List<IHardware>();
            List<KeyboardDevice> keyboards = new List<KeyboardDevice>();

            bool containsInvalid = false;

            foreach (var item in hardware)
            {
                if ("clock".StartsWith(item)) hardwareDevices.Add(new Clock());
                else if ("lem".StartsWith(item)) hardwareDevices.Add(new ScreenForm(keyboards));
                else if ("floppy".StartsWith(item)) hardwareDevices.Add(new HardDrive());
                else if ("keyboard".StartsWith(item))
                {
                    var keyboard = new KeyboardDevice();
                    hardwareDevices.Add(keyboard);
                    keyboards.Add(keyboard);
                }
                else
                {
                    Console.WriteLine($"Unknown device: {item}");
                    containsInvalid = true;
                }
            }

            if (containsInvalid)
                return;

            Dcpu emulator = new Dcpu(hardwareDevices.ToArray());
            for (int i = 0; i < memoryImage.Length; i++)
                emulator.Memory[i] = memoryImage[i];

            Console.WriteLine($"Running emulator, image size: {memoryImage.Length}");

            try
            {
                emulator.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("Crashed:");
                Console.Write(e.ToString());
            }
        }

        static void DisassembleBinary(string source, string destination)
        {
            ushort[] fileData = LoadBinaryFile(source);

            Disassembler disasm = new Disassembler();

            try
            {
                FileStream file = File.Create(destination);
                StreamWriter writer = new StreamWriter(file);

                for (int i = 0; i < fileData.Length;)
                {
                    int start = i / 2;
                    ushort word = fileData[i++];
                    string instr = disasm.DisassembleInstruction(word);
                    string memory = word.ToString("X4");
                    while (instr.Contains("next_word"))
                    {
                        if (i >= fileData.Length)
                        {
                            writer.WriteLine($"{start.ToString("X4")}:  {memory.PadRight(14)}  {instr} ; cut off");
                            break;
                        }
                        int index = instr.IndexOf("next_word");
                        ushort next = fileData[i++];
                        instr = instr.Substring(0, index) + next.ToString("X4") + instr.Substring(index + 9);
                    }
                    writer.WriteLine($"{start.ToString("X4")}    {instr}");
                }

                writer.Close();
                file.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to write to file: {destination}");
                Console.WriteLine(e.Message);
            }
        }

        static void AssembleFile(string source, string destination)
        {
            ushort[] data = AssembleSource(source);

            byte[] bytes = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                bytes[2 * i] = (byte)((data[i] >> 8) & 0xFF);
                bytes[2 * i + 1] = (byte)(data[i] & 0xFF);
            }

            try
            {
                File.WriteAllBytes(destination, bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to write to file: {destination}");
                Console.WriteLine(e.Message);
                return;
            }
        }

        static void LoadBinary(string source, List<string> hardware)
        {
            ushort[] file = LoadBinaryFile(source);

            RunEmulator(file, hardware);
        }

        static void RunProgram(string source, List<string> hardware)
        {
            ushort[] file = AssembleSource(source);

            RunEmulator(file, hardware);
        }

        static void PrintHelp()
        {
            Console.WriteLine("usage:");
            Console.WriteLine("    dcpu16");
            Console.WriteLine("        load default firmware and run");
            Console.WriteLine();
            Console.WriteLine("	   dcpu16 -run=program.dasm");
            Console.WriteLine("        assemble and run program");
            Console.WriteLine();
            Console.WriteLine("    dcpu -bin=program.bin");
            Console.WriteLine("        load and run binary file");
            Console.WriteLine();
            Console.WriteLine("    dcpu -asm=program.dasm [-o=output.dat]");
            Console.WriteLine("	       assemble program and create binary file");
            Console.WriteLine();
            Console.WriteLine("    dcpu -disasm=program.bin [-o=ouput.dasm]");
            Console.WriteLine("        dissasemble binary file");
            Console.WriteLine();
            Console.WriteLine("parameters:");
            Console.WriteLine("    -d=device");
            Console.WriteLine("        add hardware device (removes defaults), one of:");
            Console.WriteLine("        clock, keyboard, lem, floppy");
            Environment.Exit(0);
        }

        static void IncorrectUsage()
        {
            Console.WriteLine("Incorrect usage");
            PrintHelp();
        }
        
        static void Main(string[] args)
        {
            var hardware = new List<string>();
            string 
                run = null,
                assemble = null, 
                load = null, 
                disassemble = null, 
                output = null;
            bool showHelp = false;

            var options = new OptionSet() {
                { "d=|device=", "add hardware device",
                    h => hardware.Add(h) },
                { "o=|output=",
                    "set output file",
                    o => output = o },
                { "disasm=", "disassemble binary file",
                    d => disassemble = d },
                { "bin=", "load and run binary file",
                    b => load = b },
                { "asm=", "assemble program and create binary file",
                    a => assemble = a },
                { "run=", "assemble and run program",
                    r => run = r },
                { "h|?|help",  "show this message and exit",
                    v => showHelp = v != null },
            };
            
            options.Parse(args);

            if (showHelp)
                PrintHelp();

            if (disassemble != null)
            {
                if (output == null) output = disassemble + ".dasm";
                if (run != null || assemble != null || load != null || output == null)
                    IncorrectUsage();
                else
                    DisassembleBinary(disassemble, output);
            }
            else if (assemble != null)
            {
                if (output == null) output = disassemble + ".dat";
                if (run != null || disassemble != null || load != null || output == null)
                    IncorrectUsage();
                else
                    AssembleFile(assemble, output);
            }
            else if (load != null)
            {
                if (run != null || assemble != null || disassemble != null || output != null)
                    IncorrectUsage();
                else
                    LoadBinary(load, hardware);
            }
            else if (run != null)
            {
                if (load != null || assemble != null || disassemble != null || output != null)
                    IncorrectUsage();
                else
                    RunProgram(run, hardware);
            }
            else
                PrintHelp();

#if DEBUG
            Console.ReadKey();
#endif
        }
    }
}
