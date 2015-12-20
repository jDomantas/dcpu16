using dcpu16.Assembler;
using dcpu16.Emulator;
using dcpu16.Hardware;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dcpu16
{
    class Program
    {
        static string InstructionToString(ushort instr)
        {
            return $"{(instr & 0x1F).ToString("X2")} {((instr >> 5) & 0x1F).ToString("X2")} {((instr >> 10) & 0x3F).ToString("X2")}";
        }

        static void Main(string[] args)
        {
            string[] source = null;

#if DEBUG
            source = File.ReadAllLines("program.dasm");
#else
            try 
            {
                if (args.Length == 0) 
                {
                    Console.WriteLine("Parameter missing");
                    return;
                }
                source = File.ReadAllLines(args[0]);
            }
            catch (Exception e) 
            {
                Console.WriteLine("Failed to read source file");
                Console.WriteLine(e.Message);
                return;
            }
#endif

            Assembler.Assembler asm = new Assembler.Assembler();
            asm.AssembleCode(source);
            bool anyErrors = false;
            foreach (var err in asm.GetErrors())
            {
                Console.WriteLine($"Error: {err}");
                anyErrors = true;
            }
            if (!anyErrors)
            {
                //Console.WriteLine("Assembled successfully");
                ushort[] memory = asm.GetMemoryDump();

#if DEBUG
                Disassembler disasm = new Disassembler();
                int skip = 0;
                var splitMatch = new string[] { "next_word" };
                for (int i = 0; i < memory.Length; i++)
                {
                    string data;

                    if (skip == 0)
                    {
                        data = $"[{i.ToString("X4")}] {InstructionToString(memory[i])}   {memory[i].ToString("X4")}\t{memory[i]}\t{(short)memory[i]}\t{disasm.DisassembleInstruction(memory[i])}";
                        var parts = data.Split(splitMatch, StringSplitOptions.None);
                        skip += parts.Length - 1;
                        if (parts.Length == 1)
                            data = parts[0];
                        else if (parts.Length == 2)
                            data = $"{parts[0]}{(i + 1 < memory.Length ? memory[i + 1] : 0).ToString("X4")}{parts[1]}";
                        else
                            data = $"{parts[0]}{(i + 1 < memory.Length ? memory[i + 1] : 0).ToString("X4")}{parts[1]}{(i + 2 < memory.Length ? memory[i + 2] : 0).ToString("X4")}{parts[2]}";
                    }
                    else
                    {
                        skip--;
                        data = $"[{i.ToString("X4")}] {InstructionToString(memory[i])}   {memory[i].ToString("X4")}\t{memory[i]}\t{(short)memory[i]}";
                    }

                    Console.WriteLine(data);
                }
#endif

                Dcpu emulator = new Dcpu(new IHardware[2] { new Hardware.Keyboard.KeyboardDevice(), new Hardware.TextTerminal.TextTeminalDevice() });
                for (int i = 0; i < memory.Length; i++)
                    emulator.Memory[i] = memory[i];
#if DEBUG
                Console.WriteLine($"Assembled program size: {memory.Length} words");
                Console.WriteLine("===== Running program =====");
#endif
                emulator.Run();
#if DEBUG
                Console.WriteLine("\n===== Emulator halted =====");
                emulator.DumpRegisters();
#endif
            }

#if DEBUG
            Console.ReadKey();
#endif
        }
    }
}
