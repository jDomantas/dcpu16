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

        private Dictionary<string, Action<string, string[]>> Instructions;

        private List<ushort> MemoryDump;

        public Assembler()
        {
            LabelValues = new Dictionary<string, int>();
            ForwardLinks = new Dictionary<string, List<int>>();
            CurrentErrors = new List<string>();
            MemoryDump = new List<ushort>();

            Instructions = new Dictionary<string, Action<string, string[]>>()
            {
                ["SET"] = (line, operands) => AssembleSimpleInstruction(line, 0x01, operands),
                ["ADD"] = (line, operands) => AssembleSimpleInstruction(line, 0x02, operands),
                ["SUB"] = (line, operands) => AssembleSimpleInstruction(line, 0x03, operands),
                ["MUL"] = (line, operands) => AssembleSimpleInstruction(line, 0x04, operands),
                ["MLI"] = (line, operands) => AssembleSimpleInstruction(line, 0x05, operands),
                ["DIV"] = (line, operands) => AssembleSimpleInstruction(line, 0x06, operands),
                ["DVI"] = (line, operands) => AssembleSimpleInstruction(line, 0x07, operands),
                ["MOD"] = (line, operands) => AssembleSimpleInstruction(line, 0x08, operands),
                ["MDI"] = (line, operands) => AssembleSimpleInstruction(line, 0x09, operands),
                ["AND"] = (line, operands) => AssembleSimpleInstruction(line, 0x0A, operands),
                ["BOR"] = (line, operands) => AssembleSimpleInstruction(line, 0x0B, operands),
                ["XOR"] = (line, operands) => AssembleSimpleInstruction(line, 0x0C, operands),
                ["SHR"] = (line, operands) => AssembleSimpleInstruction(line, 0x0D, operands),
                ["ASR"] = (line, operands) => AssembleSimpleInstruction(line, 0x0E, operands),
                ["SHL"] = (line, operands) => AssembleSimpleInstruction(line, 0x0F, operands),
                ["IFB"] = (line, operands) => AssembleSimpleInstruction(line, 0x10, operands),
                ["IFC"] = (line, operands) => AssembleSimpleInstruction(line, 0x11, operands),
                ["IFE"] = (line, operands) => AssembleSimpleInstruction(line, 0x12, operands),
                ["IFN"] = (line, operands) => AssembleSimpleInstruction(line, 0x13, operands),
                ["IFG"] = (line, operands) => AssembleSimpleInstruction(line, 0x14, operands),
                ["IFA"] = (line, operands) => AssembleSimpleInstruction(line, 0x15, operands),
                ["IFL"] = (line, operands) => AssembleSimpleInstruction(line, 0x16, operands),
                ["IFU"] = (line, operands) => AssembleSimpleInstruction(line, 0x17, operands),
                ["ADX"] = (line, operands) => AssembleSimpleInstruction(line, 0x1A, operands),
                ["SBX"] = (line, operands) => AssembleSimpleInstruction(line, 0x1B, operands),
                ["STI"] = (line, operands) => AssembleSimpleInstruction(line, 0x1E, operands),
                ["STD"] = (line, operands) => AssembleSimpleInstruction(line, 0x1F, operands),

                ["JSR"] = (line, operands) => AssembleSpecialInstruction(line, 0x01 << 5, operands),
                ["PAG"] = (line, operands) => AssembleSpecialInstruction(line, 0x02 << 5, operands),
                ["PAS"] = (line, operands) => AssembleSpecialInstruction(line, 0x03 << 5, operands),
                ["EPM"] = (line, operands) => AssembleSpecialInstruction(line, 0x04 << 5, operands),
                ["INT"] = (line, operands) => AssembleSpecialInstruction(line, 0x08 << 5, operands),
                ["IAG"] = (line, operands) => AssembleSpecialInstruction(line, 0x09 << 5, operands),
                ["IAS"] = (line, operands) => AssembleSpecialInstruction(line, 0x0A << 5, operands),
                ["RFI"] = (line, operands) => AssembleSpecialInstruction(line, 0x0B << 5, operands),
                ["IAQ"] = (line, operands) => AssembleSpecialInstruction(line, 0x0C << 5, operands),
                ["RPI"] = (line, operands) => AssembleSpecialInstruction(line, 0x0D << 5, operands),
                ["HWN"] = (line, operands) => AssembleSpecialInstruction(line, 0x10 << 5, operands),
                ["HWQ"] = (line, operands) => AssembleSpecialInstruction(line, 0x11 << 5, operands),
                ["HWI"] = (line, operands) => AssembleSpecialInstruction(line, 0x12 << 5, operands),

                ["DAT"] = (line, operands) => AssembleData(line, operands),
                
                // syntaxic sugar
                ["JMP"] = (line, operands) => AssembleSpecialInstruction(line, 0x381, operands),
                ["HLT"] = (line, operands) => AssembleNoOperandInstruction(line, 0, operands),

                // preprocessor
                [".DEFINE"] = (line, operands) => AddDefinition(line, operands),
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
            // remove comment
            line = SplitQuoted(line, ';')[0];

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
            if (instr.Length == 0) return;
            
            string name = instr.Split(' ', '\t')[0].ToUpper();

            if (!Instructions.ContainsKey(name))
            {
                CurrentErrors.Add($"Invalid instruction: {name}");
                return;
            }

            string[] operands = SplitQuoted(instr.Substring(name.Length), ',');
            for (int i = 0; i < operands.Length; i++)
            {
                operands[i] = operands[i].Trim();
                if (operands[i].Length == 0 && operands.Length > 1)
                {
                    CurrentErrors.Add($"Invalid instruction operands: {instr}");
                    return;
                }
            }
            
            var instructionHandler = Instructions[name];
            instructionHandler(instr, operands);
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
                int num = ParseNumber(data.ToUpper());
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
                return Tuple.Create(0x1A, number);
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
                if (i > 0 && (i == operand.Length || !IsValidNameCharacter(operand[i])) && IsValidNameCharacter(operand[i - 1]))
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
                    if (IsValidNameCharacter(operand[i]) && (i == 0 || !IsValidNameCharacter(operand[i - 1])))
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

            currentTotal &= 0xFFFF;

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

        private bool IsValidNameCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
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
                yield return $"Undefined label: {label}";

            if (MemoryDump.Count > 65536)
                yield return $"Memory image larger than 65536 words: {MemoryDump.Count}";

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

        private void AssembleSimpleInstruction(string line, ushort opCode, string[] operands)
        {
            if (operands.Length != 2)
            {
                CurrentErrors.Add($"Expected one operand: {line}");
                return;
            }

            var operandA = CompileOperand(operands[1].ToUpper(), 1, true);
            var operandB = CompileOperand(operands[0].ToUpper(), operandA.Item2 == -1 ? 1 : 2);
            MemoryDump.Add((ushort)(opCode | (operandB.Item1 << 5) | (operandA.Item1 << 10)));
            if (operandA.Item2 != -1) MemoryDump.Add((ushort)(operandA.Item2 & 0xFFFF));
            if (operandB.Item2 != -1) MemoryDump.Add((ushort)(operandB.Item2 & 0xFFFF));
        }

        private void AssembleSpecialInstruction(string line, ushort opCode, string[] operands)
        {
            if (operands.Length != 1)
            {
                CurrentErrors.Add($"Expected one operand: {line}");
                return;
            }

            var operand = CompileOperand(operands[0], 1, true);
            MemoryDump.Add((ushort)(opCode | (operand.Item1 << 10)));
            if (operand.Item2 != -1)
                MemoryDump.Add((ushort)(operand.Item2 & 0xFFFF));
        }

        private void AssembleData(string line, string[] operands)
        {
            for (int i = 0; i < operands.Length; i++)
                CompileData(operands[i]);
        }

        private void AssembleNoOperandInstruction(string line, ushort opCode, string[] operands)
        {
            if (operands.Length != 1 || operands[0].Length != 0)
            {
                CurrentErrors.Add($"Expected zero operands: {line}");
                return;
            }

            MemoryDump.Add(opCode);
        }

        private void AddDefinition(string line, string[] operands)
        {
            if (operands.Length != 2)
            {
                CurrentErrors.Add($"Expected one operand: {line}");
                return;
            }

            operands[0] = operands[0].ToUpper();
            operands[1] = operands[1].ToUpper();

            if (!IsValidName(operands[0]))
            {
                Console.WriteLine($"Invalid name: {operands[0]}");
                return;
            }

            int value = ParseNumber(operands[1]);
            if (value == -1)
            {
                Console.WriteLine($"Invalid integer literal: {operands[1]}");
                return;
            }

            LabelValues.Add(operands[0], value);
        }
    }
}
