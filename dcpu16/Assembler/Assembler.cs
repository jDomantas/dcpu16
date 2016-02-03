using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace dcpu16.Assembler
{
    class Assembler
    {
        private class Operand
        {
            public ushort Code { get; }
            public ushort NextWord { get; }
            public bool UsesNextWord { get; }

            public Operand(ushort code)
            {
                Code = code;
                NextWord = 0;
                UsesNextWord = false;
            }

            public Operand(ushort code, ushort nextWord)
            {
                Code = code;
                NextWord = nextWord;
                UsesNextWord = true;
            }
        }

        private class Expression
        {
            public int Register { get; }
            public ushort Sum { get; }
            public bool RequiresForwardLink { get; }

            public Expression(int register, ushort sum, bool link)
            {
                Register = register;
                Sum = sum;
                RequiresForwardLink = link;
            }
        }

        private Preprocessor Preprocessor;
        private Dictionary<string, InstructionDefinition> Instructions;
        private List<Error> Errors;

        private Queue<Token> Tokens;
        // tuple contains amount of tokens to the end and label address
        private Dictionary<string, Tuple<int, ushort>> LabelAddresses;
        private HashSet<string> Builtins;
        private List<ushort> MemoryDump;

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
                ["DUP"] = new InstructionDefinition(0, InstructionDefinition.Params.DuplicatedData),
            };

            Builtins = new HashSet<string>()
            {
                "A", "B", "C", "X", "Y", "Z", "I", "J",
                "R0", "R1", "R2", "R3", "R4", "R5", "R6", "R7",
                "PC", "EX", "SP",
                "PUSH", "POP", "PEEK", "PICK"
            };

            Errors = new List<Error>();
            LabelAddresses = new Dictionary<string, Tuple<int, ushort>>();
            
            Preprocessor = new Preprocessor(Instructions, Builtins);
        }

        public void AssembleFile(string path)
        {
            Preprocessor.ProcessFile(path);

            if (Preprocessor.Errors.Any())
                return;
            
            // do first pass
            Tokens = new Queue<Token>(Preprocessor.OutputTokenList);
            MemoryDump = new List<ushort>();
            AssembleCode();

            // do second pass
            Tokens = new Queue<Token>(Preprocessor.OutputTokenList);
            MemoryDump.Clear();
            AssembleCode();
            
            /*if (Errors.Count == 0)
            {
                Console.WriteLine("Memory dump: ");
                for (int i = 0; i < MemoryDump.Count; i++)
                    Console.WriteLine(MemoryDump[i].ToString("X4"));
            }*/
        }

        public IEnumerable<Error> GetErrors()
        {
            foreach (var err in Preprocessor.Errors)
                yield return err;

            foreach (var err in Errors)
                yield return err;

            if (MemoryDump.Count > 65536)
                yield return new Error($"memory image larger than 65536 words, size: {MemoryDump.Count}", null, 0);

            yield break;
        }
        
        public ushort[] GetMemoryDump()
        {
            return MemoryDump.ToArray();
        }

        private void SkipLine()
        {
            while (Tokens.Peek().Type != Token.TokenType.EndOfLine)
                Tokens.Dequeue();
            Tokens.Dequeue();
        }

        private void AssembleCode()
        {
            while (Tokens.Count > 0)
            {
                switch (Tokens.Peek().Type)
                {
                    case Token.TokenType.Label:
                        if (!LabelAddresses.ContainsKey(Tokens.Peek().TextValue))
                            LabelAddresses.Add(Tokens.Peek().TextValue, Tuple.Create(Tokens.Count, (ushort)MemoryDump.Count));
                        Tokens.Dequeue();
                        break;

                    case Token.TokenType.Name:
                        if (!Instructions.ContainsKey(Tokens.Peek().TextValue))
                        {
                            Errors.Add(new Error("unknown instruction", Tokens.Peek()));
                            SkipLine();
                            break;
                        }
                        else
                        {
                            ReadInstruction();
                            break;
                        }

                    case Token.TokenType.EndOfLine:
                        Tokens.Dequeue();
                        break;
                    case Token.TokenType.Number:
                    case Token.TokenType.PackedString:
                    case Token.TokenType.Punctuation:
                    case Token.TokenType.String:
                        break;
                }
            }
        }
        
        private void ReadInstruction()
        {
            Token name = Tokens.Dequeue();
            
            switch (Instructions[name.TextValue].Parameters)
            {
                case InstructionDefinition.Params.None:
                    if (Tokens.Peek().Type == Token.TokenType.EndOfLine)
                        Tokens.Dequeue();
                    else
                    {
                        Errors.Add(new Error("expected no parameters", Tokens.Peek()));
                        SkipLine();
                        return;
                    }
                    MemoryDump.Add(Instructions[name.TextValue].OpCode);
                    return;
                    
                case InstructionDefinition.Params.Data:
                    while (true)
                    {
                        switch (Tokens.Peek().Type)
                        {
                            case Token.TokenType.String:
                                string str = Tokens.Peek().TextValue;
                                for (int i = 0; i < str.Length; i++)
                                    MemoryDump.Add((ushort)str[i]);
                                Tokens.Dequeue();
                                break;

                            case Token.TokenType.PackedString:
                                string packstr = Tokens.Peek().TextValue;
                                for (int i = 0; i < packstr.Length; i += 2)
                                    MemoryDump.Add((ushort)((ushort)packstr[i] + (ushort)(i + 1 < packstr.Length ? (packstr[i] << 8) : 0)));
                                Tokens.Dequeue();
                                break;

                            default:
                                var value = ReadSum(false, false, false);
                                if (value == null)
                                {
                                    SkipLine();
                                    return;
                                }
                                MemoryDump.Add(value.Sum);
                                break;
                        }
                        if (Tokens.Peek().Type == Token.TokenType.EndOfLine)
                        {
                            Tokens.Dequeue();
                            return;
                        }
                        else if (Tokens.Peek().Type == Token.TokenType.Punctuation && Tokens.Peek().CharValue== ',')
                        {
                            Tokens.Dequeue();
                        }
                        else
                        {
                            Errors.Add(new Error("expected comma", Tokens.Peek()));
                            SkipLine();
                            return;
                        }
                    }
                case InstructionDefinition.Params.DuplicatedData:
                    string dupString = null, dupPstring = null;
                    ushort dupNum = 0;
                    switch (Tokens.Peek().Type)
                    {
                        case Token.TokenType.String:
                            dupString = Tokens.Peek().TextValue;
                            Tokens.Dequeue();
                            break;

                        case Token.TokenType.PackedString:
                            dupPstring = Tokens.Peek().TextValue;
                            Tokens.Dequeue();
                            break;

                        default:
                            var value = ReadSum(false, false, false);
                            if (value == null)
                            {
                                SkipLine();
                                return;
                            }
                            dupNum = value.Sum;
                            break;
                    }
                    if (Tokens.Peek().Type != Token.TokenType.Punctuation || Tokens.Peek().CharValue != ',')
                    {
                        Errors.Add(new Error("expected comma", Tokens.Peek()));
                        SkipLine();
                        return;
                    }
                    Tokens.Dequeue();
                    var dupCount = ReadSum(false, false, false);
                    if (dupCount == null)
                    {
                        SkipLine();
                        return;
                    }
                    else if (Tokens.Peek().Type != Token.TokenType.EndOfLine)
                    {
                        Errors.Add(new Error("expected end of line", Tokens.Peek()));
                        SkipLine();
                        return;
                    }
                    Tokens.Dequeue();
                    if (dupString != null)
                    {
                        for (int j = 0; j < dupCount.Sum; j++)
                            for (int i = 0; i < dupString.Length; i++)
                                MemoryDump.Add((ushort)dupString[i]);
                    }
                    else if (dupPstring != null)
                    {
                        for (int j = 0; j < dupCount.Sum; j++)
                            for (int i = 0; i < dupPstring.Length; i += 2)
                                MemoryDump.Add((ushort)((ushort)dupPstring[i] + (ushort)(i + 1 < dupPstring.Length ? (dupPstring[i] << 8) : 0)));
                    }
                    else
                    {
                        for (int j = 0; j < dupCount.Sum; j++)
                            MemoryDump.Add(dupNum);
                    }
                    break;
                case InstructionDefinition.Params.OnlyA:
                    var operand = ReadOperand(true);
                    if (Tokens.Peek().Type != Token.TokenType.EndOfLine)
                    {
                        Errors.Add(new Error("expected end of line", Tokens.Peek()));
                        SkipLine();
                        return;
                    }
                    else if (operand == null)
                    {
                        SkipLine();
                        return;
                    }
                    MemoryDump.Add((ushort)(Instructions[name.TextValue].OpCode | (operand.Code << 10)));
                    if (operand.UsesNextWord)
                        MemoryDump.Add(operand.NextWord);

                    break;
                case InstructionDefinition.Params.Two:
                    var operandB = ReadOperand(false);
                    if (operandB == null)
                    {
                        SkipLine();
                        return;
                    }
                    else if (Tokens.Peek().Type != Token.TokenType.Punctuation || Tokens.Peek().CharValue != ',')
                    {
                        Errors.Add(new Error("expected comma", Tokens.Peek()));
                        SkipLine();
                        return;
                    }
                    
                    Tokens.Dequeue();

                    var operandA = ReadOperand(true);
                    if (operandA == null)
                    {
                        SkipLine();
                        return;
                    }
                    else if (Tokens.Peek().Type != Token.TokenType.EndOfLine)
                    {
                        Errors.Add(new Error("expected end of line", Tokens.Peek()));
                        SkipLine();
                        return;
                    }
                    
                    Tokens.Dequeue();

                    MemoryDump.Add((ushort)(Instructions[name.TextValue].OpCode | (operandA.Code << 10) | (operandB.Code << 5)));
                    if (operandA.UsesNextWord) MemoryDump.Add(operandA.NextWord);
                    if (operandB.UsesNextWord) MemoryDump.Add(operandB.NextWord);

                    break;
            }
        }
        
        private Operand ReadOperand(bool isA)
        {
            Token first = Tokens.Peek();

            if (first.Type == Token.TokenType.Name)
            {
                if (first.TextValue == "PEEK")
                { Tokens.Dequeue(); return new Operand(0x19); }
                else if (first.TextValue == "PUSH")
                {
                    if (isA)
                    {
                        Errors.Add(new Error("push cannot be used as second operand", Tokens.Peek()));
                        return null;
                    }
                    else
                    { Tokens.Dequeue(); return new Operand(0x18); }
                }
                else if (first.TextValue == "POP")
                {
                    if (!isA)
                    {
                        Errors.Add(new Error("pop cannot be used as first operand", Tokens.Peek()));
                        return null;
                    }
                    else
                    { Tokens.Dequeue(); return new Operand(0x18); }
                }
                else if (first.TextValue == "PICK")
                {
                    Tokens.Dequeue();
                    var depth = ReadSum(false, false, true);
                    if (depth == null)
                        return null;
                    else
                        return new Operand(0x1A, depth.Sum);
                }
                else if (first.TextValue == "SP")
                {
                    Tokens.Dequeue();
                    return new Operand(0x1B);
                }
                else if (first.TextValue == "PC")
                {
                    Tokens.Dequeue();
                    return new Operand(0x1C);
                }
                else if (first.TextValue == "EX")
                {
                    Tokens.Dequeue();
                    return new Operand(0x1D);
                }
            }

            bool memoryReference = false;

            if (Tokens.Peek().Type == Token.TokenType.Punctuation && Tokens.Peek().CharValue == '[')
            {
                memoryReference = true;
                Tokens.Dequeue();
            }

            var expr = ReadSum(true, memoryReference, memoryReference);

            if (expr == null) return null;

            if (memoryReference)
            {
                if (Tokens.Peek().Type == Token.TokenType.Punctuation && Tokens.Peek().CharValue == ']')
                {
                    Tokens.Dequeue();
                }
                else
                {
                    Errors.Add(new Error("expected ]", Tokens.Peek()));
                    return null;
                }
            }

            if (expr.Register == -1)
            {
                if (memoryReference)
                    return new Operand(0x1E, expr.Sum);
                if (!expr.RequiresForwardLink && isA && (/*expr.Sum == 0xFFFF ||*/ expr.Sum <= 30))
                    // packed literal
                    return new Operand((ushort)((0x21 + expr.Sum) & 0xFFFF));
                else
                    return new Operand(0x1F, expr.Sum);
            }
            else if (expr.Register == 8)
            {
                // used SP register
                if (expr.Sum == 0 && !expr.RequiresForwardLink) // peek
                    return new Operand(0x19);
                else // pick
                    return new Operand(0x1A, expr.Sum);                
            }
            else
            {
                // used regular register
                if (memoryReference)
                {
                    if (expr.Sum == 0 && !expr.RequiresForwardLink)
                        return new Operand((ushort)(0x08 + expr.Register));
                    else
                        return new Operand((ushort)(0x10 + expr.Register), expr.Sum);
                }
                else
                {
                    return new Operand((ushort)expr.Register);
                }
            }
        }

        private Expression ReadSum(bool allowRegister, bool allowSP, bool allowRegisterOffset)
        {
            bool readingProduct = true;
            int register = -1;
            ushort total = 0;
            bool usedForwardLink = false;
            bool negateCurrent = false;

            if (Tokens.Peek().Type == Token.TokenType.Punctuation && Tokens.Peek().CharValue == '-')
            {
                Tokens.Dequeue();
                negateCurrent = true;
            }

            while (true)
            {
                if (!readingProduct)
                {
                    if (Tokens.Peek().Type == Token.TokenType.Punctuation &&
                        (Tokens.Peek().CharValue == '+' || Tokens.Peek().CharValue == '-'))
                    {
                        readingProduct = true;
                        negateCurrent = (Tokens.Dequeue().CharValue == '-');
                    }
                    else
                        return new Expression(register, total, usedForwardLink);
                }
                else
                {
                    if (Tokens.Peek().Type == Token.TokenType.Name)
                    {
                        int reg = -1;
                        if (Tokens.Peek().TextValue == "A") reg = 0;
                        else if (Tokens.Peek().TextValue == "B") reg = 1; 
                        else if (Tokens.Peek().TextValue == "C") reg = 2; 
                        else if (Tokens.Peek().TextValue == "X") reg = 3; 
                        else if (Tokens.Peek().TextValue == "Y") reg = 4; 
                        else if (Tokens.Peek().TextValue == "Z") reg = 5; 
                        else if (Tokens.Peek().TextValue == "I") reg = 6; 
                        else if (Tokens.Peek().TextValue == "J") reg = 7; 
                        else if (Tokens.Peek().TextValue == "SP") reg = 8;
                        else if (Builtins.Contains(Tokens.Peek().TextValue))
                        {
                            if (allowRegister) Errors.Add(new Error("expected register, label, or number", Tokens.Peek()));
                            else Errors.Add(new Error("expected label or number", Tokens.Peek()));
                            return null;
                        }

                        if (reg != -1)
                        {
                            if (register != -1)
                            {
                                Errors.Add(new Error("can't use two registers", Tokens.Peek()));
                                return null;
                            }
                            else if (!allowRegister)
                            {
                                Errors.Add(new Error("can't use register here", Tokens.Peek()));
                                return null;
                            }
                            else if (!allowSP && reg == 8)
                            {
                                Errors.Add(new Error("can't use SP here", Tokens.Peek()));
                                return null;
                            }
                            else if (negateCurrent)
                            {
                                Errors.Add(new Error("can't negate register", Tokens.Peek()));
                                return null;
                            }
                            else
                            {
                                readingProduct = false;
                                register = reg;
                                Tokens.Dequeue();
                            }
                        }
                        else if (!allowRegisterOffset && register != -1)
                        {
                            Errors.Add(new Error("can't use register + constant", Tokens.Peek()));
                            return null;
                        }
                        else
                        {
                            var product = ReadProduct();
                            if (product == null) return null;

                            usedForwardLink |= product.RequiresForwardLink;
                            if (negateCurrent) total -= product.Sum;
                            else total += product.Sum;

                            if (!allowRegisterOffset) allowRegister = allowSP = false;

                            readingProduct = false;
                        }
                    }
                    else if (!allowRegisterOffset && register != -1)
                    {
                        Errors.Add(new Error("can't use register + constant", Tokens.Peek()));
                        return null;
                    }
                    else
                    {
                        var product = ReadProduct();
                        if (product == null) return null;

                        usedForwardLink |= product.RequiresForwardLink;
                        if (negateCurrent) total -= product.Sum;
                        else total += product.Sum;

                        if (!allowRegisterOffset) allowRegister = allowSP = false;

                        readingProduct = false;
                    }
                }
            }
        }

        private Expression ReadProduct()
        {
            bool readingFactor = true;
            ushort total = 1;
            bool usedForwardLink = false;
            while (true)
            {
                if (!readingFactor)
                {
                    if (Tokens.Peek().Type == Token.TokenType.Punctuation && Tokens.Peek().CharValue == '*')
                        readingFactor = true;
                    else
                        return new Expression(-1, total, usedForwardLink);
                }
                else {
                    switch (Tokens.Peek().Type)
                    {
                        case Token.TokenType.Name:
                            if (Builtins.Contains(Tokens.Peek().TextValue))
                            {
                                Errors.Add(new Error("expected label or number", Tokens.Peek()));
                                return null;
                            }
                            else
                            {
                                if (!LabelAddresses.ContainsKey(Tokens.Peek().TextValue) ||
                                    LabelAddresses[Tokens.Peek().TextValue].Item1 < Tokens.Count)
                                    usedForwardLink = true;
                                if (LabelAddresses.ContainsKey(Tokens.Peek().TextValue))
                                    total *= LabelAddresses[Tokens.Peek().TextValue].Item2;
                                Tokens.Dequeue();
                                break;
                            }

                        case Token.TokenType.Number:
                            total *= Tokens.Peek().NumericValue;
                            Tokens.Dequeue();
                            break;

                        default:
                            Errors.Add(new Error("expected label or number", Tokens.Peek()));
                            return null;
                    }

                    readingFactor = false;
                }
            }
        }
    }
}
