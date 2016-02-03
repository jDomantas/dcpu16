using dcpu16.Assembler;
using dcpu16.Emulator;
using dcpu16.Hardware;
using dcpu16.Hardware.Clock;
using dcpu16.Hardware.FloppyDisk;
using dcpu16.Hardware.Keyboard;
using dcpu16.Hardware.Screen;
using dcpu16.Hardware.SPED;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace dcpu16
{
    class Program
    {
        static Tuple<string, string> ParseDevice(string device)
        {
            string pattern = @"
                ^
                    (
                        [^()]+
                    )
                    \(
                        (
                            [^()]+
                        )
                    \)
                $
                |
                ^
                (
                    [^()]+
                )
                $";
            var match = Regex.Match(device, pattern, RegexOptions.IgnorePatternWhitespace);
            if (match.Success)
            {
                if (match.Groups[3].Success)
                    return Tuple.Create<string, string>(device, null);
                else
                    return Tuple.Create(match.Groups[1].Value, match.Groups[2].Value);
            }

            return Tuple.Create<string, string>(null, null);
        }

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
            Assembler.Assembler asm = new Assembler.Assembler();
            try
            {
                asm.AssembleFile(path);
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"Failed to assemble file: {path}");
                Console.WriteLine(e.Message);
                Environment.Exit(0);
            }

            if (asm.GetErrors().Any())
            {
                Console.WriteLine("Errors:");
                foreach (var err in asm.GetErrors())
                {
                    Console.WriteLine($"In file '{err.SourceLine.SourceFile}', line {err.SourceLine.SourceLineNumber}");
                    Console.WriteLine($"  {err.SourceLine.Value.Replace('\t', ' ')}");
                    Console.WriteLine($"{new string(' ', err.Column + 2)}^");
                    Console.WriteLine($"  {err.Message}");
                    Console.WriteLine();
                }
                Environment.Exit(0);
            }
            else
                Console.WriteLine("Assembled successfuly");
            
            return asm.GetMemoryDump();
        }

        static void RunEmulator(ushort[] memoryImage, List<string> hardware, bool dumpregs, int radiation, int clock)
        {
            if (clock <= 0)
            {
                Console.WriteLine("Error: clock speed must be positive");
                return;
            }

            if (hardware.Count == 0)
            {
                hardware.Add("clock");
                hardware.Add("keyboard");
                hardware.Add("lem");
                hardware.Add("floppy(floppy.dat)");
            }

            List<IHardware> hardwareDevices = new List<IHardware>();
            List<KeyboardDevice> keyboards = new List<KeyboardDevice>();

            bool containsInvalid = false;

            foreach (var item in hardware)
            {
                var device = ParseDevice(item);
                if (device.Item1 == null)
                {
                    Console.WriteLine($"Unknown device: {item}");
                    containsInvalid = true;
                    continue;
                }

                if ("clock".StartsWith(device.Item1)) hardwareDevices.Add(new Clock());
                else if ("lem".StartsWith(device.Item1)) hardwareDevices.Add(new ScreenForm(keyboards));
                else if ("sped".StartsWith(device.Item1)) hardwareDevices.Add(new SPEDForm());
                else if ("floppy".StartsWith(device.Item1))
                {
                    if (device.Item2 == null)
                    {
                        Console.WriteLine("Missing file for floppy");
                        containsInvalid = true;
                    }
                    else
                        hardwareDevices.Add(new Floppy(device.Item2));
                    device = null;
                }
                else if ("keyboard".StartsWith(device.Item1))
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

                if (device != null && device.Item2 != null)
                    Console.WriteLine($"Ignored parameter for {device.Item1}: {device.Item2}");
            }

            if (containsInvalid)
                return;

            Dcpu emulator = new Dcpu(hardwareDevices.ToArray());
            for (int i = 0; i < memoryImage.Length; i++)
                emulator.Memory[i] = memoryImage[i];

            Console.WriteLine($"Running emulator, image size: {memoryImage.Length}");

            try
            {
                emulator.Run(radiation, clock);
                if (dumpregs)
                    emulator.DumpRegisters();
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
                    int start = i;
                    ushort word = fileData[i++];
                    string instr = disasm.DisassembleInstruction(word);
                    string memory = word.ToString("X4");
                    while (instr.Contains("next_word"))
                    {
                        if (i >= fileData.Length)
                        {
                            writer.WriteLine($"{start.ToString("X4")}: {memory.PadRight(14)}    {instr} ; cut off");
                            break;
                        }
                        int index = instr.LastIndexOf("next_word");
                        ushort next = fileData[i++];
                        instr = instr.Substring(0, index) + next.ToString("X4") + instr.Substring(index + 9);
                        memory = memory + " " + next.ToString("X4");
                    }
                    writer.WriteLine($"{start.ToString("X4")}: {memory.PadRight(14)}    {instr}");
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

        static void LoadBinary(string source, List<string> hardware, bool dumpregs, int radiation, int clock)
        {
            ushort[] file = LoadBinaryFile(source);

            RunEmulator(file, hardware, dumpregs, radiation, clock);
        }

        static void RunProgram(string source, List<string> hardware, bool dumpregs, int radiation, int clock)
        {
            ushort[] file = AssembleSource(source);

            RunEmulator(file, hardware, dumpregs, radiation, clock);
        }

        static void PrintHelp()
        {
            Console.WriteLine("usage:");
            Console.WriteLine("    dcpu16");
            Console.WriteLine("        load default firmware and run");
            Console.WriteLine();
            Console.WriteLine("    dcpu16 -run=program.dasm");
            Console.WriteLine("        assemble and run program");
            Console.WriteLine();
            Console.WriteLine("    dcpu -bin=program.bin");
            Console.WriteLine("        load and run binary file");
            Console.WriteLine();
            Console.WriteLine("    dcpu -asm=program.dasm [-o=output.dat]");
            Console.WriteLine("        assemble program and create binary file");
            Console.WriteLine();
            Console.WriteLine("    dcpu -disasm=program.bin [-o=ouput.dasm]");
            Console.WriteLine("        dissasemble binary file");
            Console.WriteLine();
            Console.WriteLine("parameters:");
            Console.WriteLine("    -d=device");
            Console.WriteLine("        add hardware device (removes defaults), one of:");
            Console.WriteLine("        clock, keyboard, lem, floppy");
            Console.WriteLine("        specify floppy file with -d=floppy(file.dat)");
            Console.WriteLine();
            Console.WriteLine("    -dump");
            Console.WriteLine("        dump registers after emulator halts");
            Console.WriteLine();
            Console.WriteLine("    -c=value");
            Console.WriteLine("        set clock speed (in cycles per second, default: 100000)");
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
            bool dumpRegisters = false;
            int radiation = 0;
            int clock = 100000;

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
                { "dump",  "dump registers after halted",
                    v => dumpRegisters = v != null },
                { "r=|radiation=",  "set enviroment radiation setting",
                    (int v) => radiation = v },
                { "c=|clock=",  "set clock speed",
                    (int c) => clock = c },
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
                if (output == null) output = assemble + ".dat";
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
                    LoadBinary(load, hardware, dumpRegisters, radiation, clock);
            }
            else if (run != null)
            {
                if (load != null || assemble != null || disassemble != null || output != null)
                    IncorrectUsage();
                else
                    RunProgram(run, hardware, dumpRegisters, radiation, clock);
            }
            else
                IncorrectUsage();

#if DEBUG
            Console.ReadKey();
#endif
        }
    }
}
