using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace dcpu16.Assembler
{
    class Assembler
    {
        private Preprocessor Preprocessor;
        private Dictionary<string, InstructionDefinition> Instructions;
        private List<Error> Errors;

        public Assembler()
        {
            Instructions = new Dictionary<string, InstructionDefinition>()
            {
                ["SET"] = new InstructionDefinition(0x01, InstructionDefinition.Params.Two),
                ["ADD"] = new InstructionDefinition(0x02, InstructionDefinition.Params.Two),
                ["SUB"] = new InstructionDefinition(0x03, InstructionDefinition.Params.Two),
                ["MUL"] = new InstructionDefinition(0x04, InstructionDefinition.Params.Two),
                ["MLI"] = new InstructionDefinition(0x05, InstructionDefinition.Params.Two),
                ["DIV"] = new InstructionDefinition(0x06, InstructionDefinition.Params.Two),
                ["DVI"] = new InstructionDefinition(0x07, InstructionDefinition.Params.Two),
                ["MOD"] = new InstructionDefinition(0x08, InstructionDefinition.Params.Two),
                ["MDI"] = new InstructionDefinition(0x09, InstructionDefinition.Params.Two),
                ["AND"] = new InstructionDefinition(0x0A, InstructionDefinition.Params.Two),
                ["BOR"] = new InstructionDefinition(0x0B, InstructionDefinition.Params.Two),
                ["XOR"] = new InstructionDefinition(0x0C, InstructionDefinition.Params.Two),
                ["SHR"] = new InstructionDefinition(0x0D, InstructionDefinition.Params.Two),
                ["ASR"] = new InstructionDefinition(0x0E, InstructionDefinition.Params.Two),
                ["SHL"] = new InstructionDefinition(0x0F, InstructionDefinition.Params.Two),
                ["IFB"] = new InstructionDefinition(0x10, InstructionDefinition.Params.Two),
                ["IFC"] = new InstructionDefinition(0x11, InstructionDefinition.Params.Two),
                ["IFE"] = new InstructionDefinition(0x12, InstructionDefinition.Params.Two),
                ["IFN"] = new InstructionDefinition(0x13, InstructionDefinition.Params.Two),
                ["IFG"] = new InstructionDefinition(0x14, InstructionDefinition.Params.Two),
                ["IFA"] = new InstructionDefinition(0x15, InstructionDefinition.Params.Two),
                ["IFL"] = new InstructionDefinition(0x16, InstructionDefinition.Params.Two),
                ["IFU"] = new InstructionDefinition(0x17, InstructionDefinition.Params.Two),
                ["ADX"] = new InstructionDefinition(0x1A, InstructionDefinition.Params.Two),
                ["SBX"] = new InstructionDefinition(0x1B, InstructionDefinition.Params.Two),
                ["STI"] = new InstructionDefinition(0x1E, InstructionDefinition.Params.Two),
                ["STD"] = new InstructionDefinition(0x1F, InstructionDefinition.Params.Two),

                ["JSR"] = new InstructionDefinition(0x01 << 5, InstructionDefinition.Params.OnlyA),
                ["PAG"] = new InstructionDefinition(0x02 << 5, InstructionDefinition.Params.OnlyA),
                ["PAS"] = new InstructionDefinition(0x03 << 5, InstructionDefinition.Params.OnlyA),
                ["EPM"] = new InstructionDefinition(0x04 << 5, InstructionDefinition.Params.OnlyA),
                ["INT"] = new InstructionDefinition(0x08 << 5, InstructionDefinition.Params.OnlyA),
                ["IAG"] = new InstructionDefinition(0x09 << 5, InstructionDefinition.Params.OnlyA),
                ["IAS"] = new InstructionDefinition(0x0A << 5, InstructionDefinition.Params.OnlyA),
                ["RFI"] = new InstructionDefinition(0x0B << 5, InstructionDefinition.Params.OnlyA),
                ["IAQ"] = new InstructionDefinition(0x0C << 5, InstructionDefinition.Params.OnlyA),
                ["RPI"] = new InstructionDefinition(0x0D << 5, InstructionDefinition.Params.OnlyA),
                ["HWN"] = new InstructionDefinition(0x10 << 5, InstructionDefinition.Params.OnlyA),
                ["HWQ"] = new InstructionDefinition(0x11 << 5, InstructionDefinition.Params.OnlyA),
                ["HWI"] = new InstructionDefinition(0x12 << 5, InstructionDefinition.Params.OnlyA),

                ["HLT"] = new InstructionDefinition(0, InstructionDefinition.Params.None),

                ["DAT"] = new InstructionDefinition(0, InstructionDefinition.Params.Data),
            };

            HashSet<string> builtins = new HashSet<string>()
            {
                "A", "B", "C", "X", "Y", "Z", "I", "J",
                "PC", "EX", "SP",
                "PUSH", "POP", "PEEK", "PICK"
            };

            Errors = new List<Error>();
            
            Preprocessor = new Preprocessor(Instructions, builtins);
        }

        public void AssembleFile(string path)
        {
            Preprocessor.ProcessFile(path);
            
            if (Preprocessor.Errors.Count > 0)
            {
                foreach (var err in Preprocessor.Errors)
                {
                    Console.WriteLine($"In file '{err.SourceLine.SourceFile}', line {err.SourceLine.SourceLineNumber}");
                    Console.WriteLine($"  {err.SourceLine.Value.Replace('\t', ' ')}");
                    Console.WriteLine($"{new string(' ', err.Column + 2)}^");
                    Console.WriteLine($"  {err.Message}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("===== PREPROCESSOR =====");
                foreach (var token in Preprocessor.OutputTokenList)
                    Console.Write($"{token.ToString(true)} ");
                Console.WriteLine("\n===== END ==============");
            }
        }

        public IEnumerable<Error> GetErrors()
        {
            foreach (var err in Preprocessor.Errors)
                yield return err;
            yield break;
        }
        
    }
}
