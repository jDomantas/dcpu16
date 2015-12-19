using dcpu16.Assembler;
using dcpu16.Emulator;
using dcpu16.Hardware;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                //Disassembler disasm = new Disassembler();
                //for (int i = 0; i < memory.Length; i++)
                //    Console.WriteLine($"[{i.ToString("X4")}] {InstructionToString(memory[i])}   {memory[i].ToString("X4")}\t{memory[i]}\t{(short)memory[i]}\t{disasm.DisassembleInstruction(memory[i])}");

                Dcpu emulator = new Dcpu(new IHardware[2] { new Hardware.Keyboard.KeyboardDevice(), new Hardware.TextTerminal.TextTeminalDevice() });
                for (int i = 0; i < memory.Length; i++)
                    emulator.Memory[i] = memory[i];

                Console.WriteLine($"Assembled program size: {memory.Length} words");
                Console.WriteLine("===== Running program =====");
                emulator.Run();
                Console.WriteLine("\n===== Emulator halted =====");
                emulator.DumpRegisters();
            }

#if DEBUG
            Console.ReadKey();
#endif
        }
    }
}
