using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dcpu16.Assembler
{
    class Disassembler
    {
        public Disassembler()
        {

        }

        public string DisassembleInstruction(ushort instruction)
        {
            int opCode = instruction & 0x1F;
            string a = DisassembleOperand(instruction >> 10, true);
            string b = DisassembleOperand((instruction >> 5) & 0x1F, false);
            switch (opCode)
            {
                case 0x00: return DisassembleSpecialInstruction(instruction, a);
                case 0x01: return $"SET {b}, {a}";
                case 0x02: return $"ADD {b}, {a}";
                case 0x03: return $"SUB {b}, {a}";
                case 0x04: return $"MUL {b}, {a}";
                case 0x05: return $"MLI {b}, {a}";
                case 0x06: return $"DIV {b}, {a}";
                case 0x07: return $"DVI {b}, {a}";
                case 0x08: return $"MOD {b}, {a}";
                case 0x09: return $"MDI {b}, {a}";
                case 0x0A: return $"AND {b}, {a}";
                case 0x0B: return $"BOR {b}, {a}";
                case 0x0C: return $"XOR {b}, {a}";
                case 0x0D: return $"SHR {b}, {a}";
                case 0x0E: return $"ASR {b}, {a}";
                case 0x0F: return $"SHL {b}, {a}";
                case 0x10: return $"IFB {b}, {a}";
                case 0x11: return $"IFC {b}, {a}";
                case 0x12: return $"IFE {b}, {a}";
                case 0x13: return $"IFN {b}, {a}";
                case 0x14: return $"IFG {b}, {a}";
                case 0x15: return $"IFA {b}, {a}";
                case 0x16: return $"IFL {b}, {a}";
                case 0x17: return $"IFU {b}, {a}";
                case 0x1A: return $"ADX {b}, {a}";
                case 0x1B: return $"SBX {b}, {a}";
                case 0x1E: return $"STI {b}, {a}";
                case 0x1F: return $"STD {b}, {a}";
                default: return "Unknown instruction";
            }
        }

        public string DisassembleSpecialInstruction(ushort instruction, string operand)
        {
            int opCode = (instruction >> 5) & 0x1F;
            switch (opCode)
            {
                case 0x01: return $"JSR {operand}";
                case 0x08: return $"INT {operand}";
                case 0x09: return $"IAG {operand}";
                case 0x0A: return $"IAS {operand}";
                case 0x0B: return $"RFI {operand}";
                case 0x0C: return $"IAQ {operand}";
                case 0x10: return $"HWN {operand}";
                case 0x11: return $"HWQ {operand}";
                case 0x12: return $"HWI {operand}";
                default: return "Unknown instruction";
            }
        }

        private string DisassembleOperand(int operand, bool isA)
        {
            switch (operand)
            {
                case 0x00: return "A";
                case 0x01: return "B";
                case 0x02: return "C";
                case 0x03: return "X";
                case 0x04: return "Y";
                case 0x05: return "Z";
                case 0x06: return "I";
                case 0x07: return "J";
                case 0x08: return "[A]";
                case 0x09: return "[B]";
                case 0x0A: return "[C]";
                case 0x0B: return "[X]";
                case 0x0C: return "[Y]";
                case 0x0D: return "[Z]";
                case 0x0E: return "[I]";
                case 0x0F: return "[J]";
                case 0x10: return "[A + next_word]";
                case 0x11: return "[B + next_word]";
                case 0x12: return "[C + next_word]";
                case 0x13: return "[X + next_word]";
                case 0x14: return "[Y + next_word]";
                case 0x15: return "[Z + next_word]";
                case 0x16: return "[I + next_word]";
                case 0x17: return "[J + next_word]";
                case 0x18: return isA ? "POP" : "PUSH";
                case 0x19: return "PEEK";
                case 0x1A: return "PICK next_word";
                case 0x1B: return "SP";
                case 0x1C: return "PC";
                case 0x1D: return "EX";
                case 0x1E: return "[next_word]";
                case 0x1F: return "next_word";
                default: return (operand - 0x21).ToString("X4");
            }
        }
    }
}
