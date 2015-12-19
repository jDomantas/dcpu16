using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace dcpu16.Assembler
{
    class Assembler
    {
        private Dictionary<string, int> LabelValues;
        private Dictionary<string, List<int>> ForwardLinks;
        private List<string> CurrentErrors;

        private Dictionary<string, int> InstructionCodes;

        private List<ushort> MemoryDump;

        public Assembler()
        {
            LabelValues = new Dictionary<string, int>();
            ForwardLinks = new Dictionary<string, List<int>>();
            CurrentErrors = new List<string>();
            MemoryDump = new List<ushort>();

            InstructionCodes = new Dictionary<string, int>()
            {
                ["SET"] = 0x01,
                ["MOV"] = 0x01, // not specified in standard
                ["ADD"] = 0x02,
                ["SUB"] = 0x03,
                ["MUL"] = 0x04,
                ["MLI"] = 0x05,
                ["DIV"] = 0x06,
                ["DVI"] = 0x07,
                ["MOD"] = 0x08,
                ["MDI"] = 0x09,
                ["AND"] = 0x0A,
                ["BOR"] = 0x0B,
                ["XOR"] = 0x0C,
                ["SHR"] = 0x0D,
                ["ASR"] = 0x0E,
                ["SHL"] = 0x0F,
                ["IFB"] = 0x10,
                ["IFC"] = 0x11,
                ["IFE"] = 0x12,
                ["IFN"] = 0x13,
                ["IFG"] = 0x14,
                ["IFA"] = 0x15,
                ["IFL"] = 0x16,
                ["IFU"] = 0x17,
                ["ADX"] = 0x1A,
                ["SBX"] = 0x1B,
                ["STI"] = 0x1E,
                ["STD"] = 0x1F,

                ["JSR"] = 0x101,
                ["INT"] = 0x108,
                ["IAG"] = 0x109,
                ["IAS"] = 0x10A,
                ["RFI"] = 0x10B,
                ["IAQ"] = 0x10C,
                ["HWN"] = 0x110,
                ["HWQ"] = 0x111,
                ["HWI"] = 0x112,
                
                ["DAT"] = 0x201,

                // not in standart, compiles to 0x0000
                // which is invalid instruction, thus halts
                ["HLT"] = 0x301,
            };
        }

        public ushort[] GetMemoryDump()
        {
            return MemoryDump.ToArray();
        }

        public void AssembleCode(IEnumerable<string> code)
        {
            foreach (var line in code)
                AssembleLine(line);

            ProcessForwardLinks();
        }
        
        private void AssembleLine(string line)
        {
            string originalLine = line;

            string[] subStrings = SplitQuoted(line, ':');
            for (int i = 0; i < subStrings.Length - 1; i++)
                subStrings[i] = subStrings[i].Trim().ToUpper();

            // process labels
            int lastLabel = 0;
            for (int i = 0; i < subStrings.Length - 1; i++)
            {
                if (!AreQuotesMatched(subStrings[i]))
                    break;
                lastLabel = i;
                if (!IsValidName(subStrings[i]))
                    CurrentErrors.Add($"Invalid label: {subStrings[i]}");
                else if (LabelValues.ContainsKey(subStrings[i]))
                    CurrentErrors.Add($"Duplicate label definition: {subStrings[i]}");
                else
                    LabelValues.Add(subStrings[i], MemoryDump.Count);
            }

            if (subStrings.Length > 0)
                AssembleInstruction(subStrings[subStrings.Length - 1].Trim());
        }

        private void AssembleInstruction(string instr)
        {
            // remove comment
            instr = SplitQuoted(instr, ';')[0];

            if (instr.Length == 0) return;
            
            string name;
            if (instr.Length < 3)
                name = instr.ToUpper();
            else
                name = instr.Substring(0, 3).ToUpper();

            if (!InstructionCodes.ContainsKey(name))
            {
                CurrentErrors.Add($"Invalid instruction: {name}");
                return;
            }

            string[] operands = SplitQuoted(instr.Substring(3), ',');
            for (int i = 0; i < operands.Length; i++)
            {
                operands[i] = operands[i].Trim();
                if (operands[i].Length == 0 && operands.Length > 1)
                {
                    CurrentErrors.Add($"Invalid instruction operands: {instr}");
                    return;
                }
            }
            
            int instructionCode = InstructionCodes[name];

            if (instructionCode >= 0x300)
            {
                // HLT instruction, no operands
                if (operands.Length != 1 || operands[0].Length != 0)
                {
                    CurrentErrors.Add($"Expected zero operands: {instr}");
                    return;
                }

                MemoryDump.Add(0);
            }
            else if (instructionCode >= 0x200)
            {
                // DAT instruction, simply flush all operands
                for (int i = 0; i < operands.Length; i++)
                    CompileData(operands[i]);
            }
            else if (instructionCode >= 0x100)
            {
                // special instruction, one operand
                if (operands.Length != 1)
                {
                    CurrentErrors.Add($"Expected one operand: {instr}");
                    return;
                }

                var operand = CompileOperand(operands[0], 1, true);
                MemoryDump.Add((ushort)(((instructionCode & 0x1F) << 5) | (operand.Item1 << 10)));
                if (operand.Item2 != -1)
                    MemoryDump.Add((ushort)operand.Item2);
            }
            else
            {
                // simple instruction, two operands
                if (operands.Length != 2)
                {
                    CurrentErrors.Add($"Expected one operand: {instr}");
                    return;
                }

                var operandB = CompileOperand(operands[0], 1);
                var operandA = CompileOperand(operands[1], operandB.Item2 == -1 ? 1 : 2, true);
                MemoryDump.Add((ushort)(instructionCode | (operandB.Item1 << 5) | (operandA.Item1 << 10)));
                if (operandB.Item2 != -1) MemoryDump.Add((ushort)operandB.Item2);
                if (operandA.Item2 != -1) MemoryDump.Add((ushort)operandA.Item2);
            }
        }

        private void CompileData(string data)
        {
            if ((data.StartsWith("\"") && data.EndsWith("\"")) || (data.StartsWith("'") && data.EndsWith("'")))
            {
                bool doubleQuotes = data.StartsWith("\"");

                // string constant
                bool escaping = false;
                for (int i = 1; i < data.Length - 1; i++)
                {
                    if (escaping)
                    {
                        if (data[i] == '\\')
                            MemoryDump.Add('\\');
                        else if (data[i] == 'n')
                            MemoryDump.Add('\n');
                        else if (data[i] == '"')
                            MemoryDump.Add('"');
                        else if (data[i] == '\'')
                            MemoryDump.Add('\'');
                        else if (data[i] == '0')
                            MemoryDump.Add(0);
                        else
                        {
                            CurrentErrors.Add($"Invalid escape sequence: {data.Substring(i - 1, 2)}");
                            return;
                        }

                        escaping = false;
                    }
                    else
                    {
                        if ((data[i] == '"' && doubleQuotes) || (data[i] == '\'' && !doubleQuotes))
                        {
                            CurrentErrors.Add($"Invalid string constant: {data}");
                            return;
                        }
                        else if (data[i] == '\\')
                        {
                            escaping = true;
                        }
                        else
                        {
                            MemoryDump.Add(data[i]);
                        }
                    }
                }
            }
            else
            {
                // number
                int num = ParseNumber(data);
                if (num == -1)
                {
                    CurrentErrors.Add($"Invalid number: {data}");
                    return;
                }

                MemoryDump.Add((ushort)(num & 0xFFFF));
            }
        }

        private void AddForwardLink(string label, int index)
        {
            if (!ForwardLinks.ContainsKey(label))
                ForwardLinks[label] = new List<int>();

            ForwardLinks[label].Add(index);
        }

        private int ParseNumber(string str)
        {
            if (str.Length == 0)
                return -1;

            if (str.StartsWith("0X"))
            {
                int res;
                if (!int.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out res))
                    return -1;
                if (res < -0x8000 || res >= 0x10000)
                    return -1;
                return res;
            }
            else
            {
                int res;
                if (!int.TryParse(str, out res))
                    return -1;
                if (res < -0x8000 || res >= 0x10000)
                    return -1;
                return res;
            }
        }
        
        private Tuple<int, int> CompileOperand(string operand, int addressOffset, bool allowPackedLiteral = false)
        {
            string original = operand.ToUpper();
            operand = original;

            #region NamedOperands
            if (operand == "PEEK")
            {
                return Tuple.Create(0x19, -1);
            }
            else if (operand.StartsWith("PICK") && operand.Length > 5)
            {
                int number = ParseNumber(operand.Substring(5));
                if (number == -1)
                {
                    CurrentErrors.Add($"Invalid operand: {original}");
                    return Tuple.Create(0, -1);
                }
                return Tuple.Create(0x1A, -1);
            }
            else if (operand == "PUSH")
            {
                if (allowPackedLiteral)
                {
                    CurrentErrors.Add($"PUSH used as operand a: {original}");
                    return Tuple.Create(0, -1);
                }
                return Tuple.Create(0x18, -1);
            }
            else if (operand == "POP")
            {
                if (!allowPackedLiteral)
                {
                    CurrentErrors.Add($"POP used as operand b: {original}");
                    return Tuple.Create(0, -1);
                }
                return Tuple.Create(0x18, -1);
            }
            else if (operand == "SP")
            {
                return Tuple.Create(0x1b, -1);
            }
            else if (operand == "PC")
            {
                return Tuple.Create(0x1C, -1);
            }
            else if (operand == "EX")
            {
                return Tuple.Create(0x1D, -1);
            }
            #endregion

            bool brackets = operand[0] == '[';

            if (brackets)
            {
                if (operand.EndsWith("]"))
                {
                    operand = operand.Substring(1, operand.Length - 1).Trim();
                    if (operand.Length == 0)
                    {
                        CurrentErrors.Add($"Invalid operand: {original}");
                        return Tuple.Create(0, -1);
                    }
                }
                else
                {
                    CurrentErrors.Add($"Invalid operand: {original}");
                    return Tuple.Create(0, -1);
                }
            }
            
            int currentSign = 1;
            int alphanumStart = 0;
            int currentTotal = 0;
            bool usedLabel = false;
            int currentRegister = -1;

            int i = 0;
            if (operand[0] == '-')
            {
                currentSign = -1;
                i = 1;
            }
            else if (operand[0] == '+')
                i = 1;
            
            for (; i <= operand.Length; i++)
            {
                #region EndOfAlphanumericalToken
                if (i > 0 && (i == operand.Length || !char.IsLetterOrDigit(operand[i])) && char.IsLetterOrDigit(operand[i - 1]))
                {
                    // number or name ended
                    // value substring: [alphanumStart, i)
                    string sub = operand.Substring(alphanumStart, i - alphanumStart);
                    if (char.IsDigit(sub[0]))
                    {
                        // number
                        int num = ParseNumber(sub);
                        if (num == -1)
                        {
                            CurrentErrors.Add($"Invalid integer literal: {original}");
                            return Tuple.Create(0, -1);
                        }

                        currentTotal += currentSign * num;
                    }
                    else // name
                    {
                        int registerName = -1;

                        if (sub == "A") registerName = 0;
                        else if (sub == "B") registerName = 1;
                        else if (sub == "C") registerName = 2;
                        else if (sub == "X") registerName = 3;
                        else if (sub == "Y") registerName = 4;
                        else if (sub == "Z") registerName = 5;
                        else if (sub == "I") registerName = 6;
                        else if (sub == "J") registerName = 7;
                        else if (sub == "SP") registerName = 8;

                        if (registerName != -1)
                        {
                            if (currentRegister != -1 || currentSign != 1)
                            {
                                CurrentErrors.Add($"Invalid operand: {original}");
                                return Tuple.Create(0, -1);
                            }
                            else
                            {
                                currentRegister = registerName;
                            }
                        }
                        else
                        {
                            // label
                            if (LabelValues.ContainsKey(sub))
                            {
                                currentTotal += LabelValues[sub] * currentSign + (currentSign < 0 ? -1 : 0);
                            }
                            else
                            {
                                AddForwardLink(sub, (MemoryDump.Count + addressOffset) * currentSign + (currentSign < 0 ? -1 : 0));
                                usedLabel = true;
                            }
                        }
                    }

                    currentSign = 0;
                }
                #endregion

                if (i < operand.Length)
                {
                    if (char.IsLetterOrDigit(operand[i]) && (i == 0 || !char.IsLetterOrDigit(operand[i - 1])))
                        alphanumStart = i;
                    if (operand[i] == '+')
                    {
                        if (currentSign != 0)
                        {
                            CurrentErrors.Add($"Invalid operand: {original}");
                            return Tuple.Create(0, -1);
                        }

                        currentSign = 1;
                    }
                    else if (operand[i] == '-')
                    {
                        if (currentSign != 0)
                        {
                            CurrentErrors.Add($"Invalid operand: {original}");
                            return Tuple.Create(0, -1);
                        }

                        currentSign = -1;
                    }
                }
            }

            if (currentSign != 0)
            {
                CurrentErrors.Add($"Invalid operand: {original}");
                return Tuple.Create(0, -1);
            }

            if (brackets)
            {
                if (currentRegister == 8) // SP was used
                {
                    if (usedLabel || currentTotal != 0) // [SP + val] / PICK n
                        return Tuple.Create(0x1A, currentTotal);
                    else // [SP] / PEEK
                        return Tuple.Create(0x19, -1);
                }
                else if (currentRegister != -1)
                {
                    if (usedLabel || currentTotal != 0) // [reg + val]
                        return Tuple.Create(currentRegister + 0x10, currentTotal);
                    else // [reg]
                        return Tuple.Create(currentRegister + 0x08, -1);
                }
                else // [next_word]
                {
                    return Tuple.Create(0x1E, currentTotal);
                }
            }
            else
            {
                if (currentRegister == 8)
                {
                    CurrentErrors.Add($"Invalid operand: {original}");
                    return Tuple.Create(0, -1);
                }
                else if (currentRegister != -1)
                {
                    if (usedLabel || currentTotal != 0) // reg + val (invalid)
                    {
                        CurrentErrors.Add($"Invalid operand: {original}");
                        return Tuple.Create(0, -1);
                    }
                    else // reg
                        return Tuple.Create(currentRegister, -1);
                }
                else
                {
                    // expression
                    if (!allowPackedLiteral || usedLabel || currentTotal < -1 || currentTotal > 30)
                    {
                        // can't pack literal
                        return Tuple.Create(0x1F, currentTotal);
                    }
                    else
                    {
                        // packed literal
                        return Tuple.Create(0x21 + currentTotal, -1);
                    }
                }
            }
        }

        private bool IsValidName(string str)
        {
            if (str.Length == 0)
                return false;

            for (int i = 0; i < str.Length; i++)
                if (!((str[i] >= 'A' && str[i] <= 'Z') || (str[i] >= '0' && str[i] <= '9') || str[i] == '_'))
                    return false;
            if (str[0] >= '0' && str[0] <= '9')
                return false;

            return true;
        }

        private bool AreQuotesMatched(string str)
        {
            int currentQuotes = 0;
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '\\')
                    i++;
                else if(str[i] == '"')
                {
                    if (currentQuotes == 2) currentQuotes = 0;
                    else if (currentQuotes == 0) currentQuotes = 2;
                }
                else if (str[i] == '\'')
                {
                    if (currentQuotes == 1) currentQuotes = 0;
                    else if (currentQuotes == 0) currentQuotes = 1;
                }
            }

            return currentQuotes == 0;
        }

        public IEnumerable<string> GetErrors()
        {
            foreach (var error in CurrentErrors)
                yield return error;

            foreach (var label in ForwardLinks.Keys)
                yield return $"Undefined labet: {label}";

            yield break;
        }

        private void ProcessForwardLinks()
        {
            foreach (var label in ForwardLinks.Keys)
            {
                if (LabelValues.ContainsKey(label))
                {
                    foreach (var address in ForwardLinks[label])
                    {
                        int addr = (address < 0 ? -address - 1 : address);
                        if (address >= MemoryDump.Count)
                            CurrentErrors.Add("Invalid forward link encountered");
                        else
                        {
                            MemoryDump[addr] += (ushort)(LabelValues[label] & 0xFFFF);
                        }
                    }
                }
            }

            foreach (var label in LabelValues.Keys)
                if (ForwardLinks.Keys.Contains(label))
                    ForwardLinks.Remove(label);
        }

        private string[] SplitQuoted(string str, char split)
        {
            List<string> result = new List<string>();
            int currentQuotes = 0;
            int start = 0;
            for (int i = 0; i <= str.Length; i++)
            {
                if (i == str.Length || (str[i] == split && currentQuotes == 0))
                {
                    result.Add(str.Substring(start, i - start));
                    start = i + 1;
                }
                else if (str[i] == '\\' && currentQuotes != 0)
                    i++;
                else if (str[i] == '"' && (currentQuotes == 2 || currentQuotes == 0))
                    currentQuotes = 2 - currentQuotes;
                else if (str[i] == '\'' && (currentQuotes == 1 || currentQuotes == 0))
                    currentQuotes = 1 - currentQuotes;
            }

            return result.ToArray();
        }
    }
}
