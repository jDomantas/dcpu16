using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace dcpu16.Assembler
{
    class Preprocessor
    {
        public List<Token> OutputTokenList { get; private set; }
        public Dictionary<string, LabelDefinition> GlobalLabels { get; private set; }
        public Dictionary<int, Dictionary<string, LabelDefinition>> FileLocalLabels { get; private set; }

        private PreprocessorTokenManager Tokens;
        private Dictionary<string, Definition> Definitions;
        private Stack<Dictionary<string, Definition>> FileLocalDefinitions;
        private Dictionary<string, MacroDefinition> Macros;
        private Dictionary<string, InstructionDefinition> Instructions;
        private HashSet<string> BuiltinKeywords;
        private Stack<int> FileStack;

        private int CurrentFile { get { return FileStack.Peek(); } }
        private Dictionary<string, LabelDefinition> LocalLabels { get { return FileLocalLabels[CurrentFile]; } }
        private Dictionary<string, Definition> LocalDefinitions { get { return FileLocalDefinitions.Peek(); } }

        private HashSet<string> IncludedFiles;
        public List<Error> Errors { get; private set; }

        public Preprocessor(Dictionary<string, InstructionDefinition> instructions, HashSet<string> builtinKeywords)
        {
            Instructions = instructions;
            BuiltinKeywords = builtinKeywords;

            Tokens = new PreprocessorTokenManager();
            OutputTokenList = new List<Token>();
            IncludedFiles = new HashSet<string>();
            Errors = new List<Error>();

            Definitions = new Dictionary<string, Definition>();
            Macros = new Dictionary<string, MacroDefinition>();
            GlobalLabels = new Dictionary<string, LabelDefinition>();
            FileLocalLabels = new Dictionary<int, Dictionary<string, LabelDefinition>>();
            FileLocalDefinitions = new Stack<Dictionary<string, Definition>>();
            FileStack = new Stack<int>();
        }

        public void ProcessFile(string filename)
        {
            IncludeFile(filename, null);
            Preprocess();
            ResolveLabelScopes();

            // foreach (Token t in OutputTokenList)
            //     System.Console.Write($"{t.ToString(true)} ");
            // 
            // System.Console.ReadKey();
        }
        
        private void IncludeFile(string filename, Token includeDirective)
        {
            string globalPath = new FileInfo(filename).FullName;

            if (IncludedFiles.Contains(globalPath))
            {
                Errors.Add(new Error($"file '{filename}' included multiple times", includeDirective));
                return;
            }

            IncludedFiles.Add(globalPath);

            string[] code = File.ReadAllLines(filename);
            LineOfCode[] lines = new LineOfCode[code.Length];
            for (int i = 0; i < lines.Length; i++)
                lines[i] = new LineOfCode(code[i], filename, i + 1);

            int currentFileID = Tokens.InsertFile(lines);
            FileStack.Push(currentFileID);
        }

        private void Preprocess()
        {
            bool allowMacroExpansion = true;

            while (Tokens.Peek != null)
            {
                switch (Tokens.Peek.Type)
                {
                    case Token.TokenType.Directive:
                        HandleDirective();
                        allowMacroExpansion = true;
                        break;
                        
                    case Token.TokenType.Error:
                        SkipLine(true);
                        allowMacroExpansion = true;
                        break;

                    case Token.TokenType.Label:
                        if (!allowMacroExpansion)
                        {
                            Errors.Add(new Error("labels must be at the beginning of the line", Tokens.Peek));
                            SkipLine(false);
                        }
                        else if (!IsNameUsed(Tokens.Peek))
                        {
                            LocalLabels.Add(Tokens.Peek.TextValue, new LabelDefinition(Tokens.Peek, 0));
                            OutputTokenList.Add(Tokens.Peek);
                            Tokens.Next();
                        }
                        else
                            Tokens.Next();
                        break;

                    case Token.TokenType.Name:
                        // try to expand name as macro or as simple replacement
                        Token name = Tokens.Peek;
                        Tokens.Next();
                        if (TryExpandName(name, allowMacroExpansion))
                            OutputTokenList.Add(name);
                        // if this was macro, then we are at the beginning of line
                        allowMacroExpansion = Macros.ContainsKey(name.TextValue);
                        break;

                    case Token.TokenType.Punctuation:
                        if (Tokens.Peek.CharValue == ';')
                        {
                            allowMacroExpansion = true;
                            OutputTokenList.Add(SkipLine(true));
                            break;
                        }
                        else
                            goto case Token.TokenType.String;

                    case Token.TokenType.Number:
                    case Token.TokenType.PackedString:
                    case Token.TokenType.String:
                        // pass along to assembler
                        allowMacroExpansion = false;
                        OutputTokenList.Add(Tokens.Peek);
                        Tokens.Next();
                        break;

                    case Token.TokenType.EndOfLine:
                        // pass along to assembler
                        allowMacroExpansion = true;
                        OutputTokenList.Add(Tokens.Peek);
                        Tokens.Next();
                        break;

                    case Token.TokenType.StartOfFile:
                        OutputTokenList.Add(Tokens.Peek);
                        FileLocalDefinitions.Push(new Dictionary<string, Definition>());
                        FileStack.Push(Tokens.Peek.IntValue);
                        FileLocalLabels.Add(Tokens.Peek.IntValue, new Dictionary<string, LabelDefinition>());
                        Tokens.Next();
                        break;

                    case Token.TokenType.EndOfFile:
                        OutputTokenList.Add(Tokens.Peek);
                        FileLocalDefinitions.Pop();
                        FileStack.Pop();
                        Tokens.Next();
                        break;
                }
            }
        }

        private void ResolveLabelScopes()
        {
            FileStack.Clear();
            
            foreach (var token in OutputTokenList)
            {
                switch (token.Type)
                {
                    case Token.TokenType.StartOfFile:
                        FileStack.Push(token.IntValue);
                        break;

                    case Token.TokenType.EndOfFile:
                        FileStack.Pop();
                        break;

                    case Token.TokenType.Label:
                        // add numbers to local labels
                        if (LocalLabels.ContainsKey(token.TextValue))
                            token.ChangeNumber(CurrentFile);
                        break;
                            
                    case Token.TokenType.Name:
                        if (LocalLabels.ContainsKey(token.TextValue))
                            // referencing local label
                            token.ChangeNumber(CurrentFile);
                        else if (
                            !GlobalLabels.ContainsKey(token.TextValue) && 
                            !Instructions.ContainsKey(token.TextValue) && 
                            !BuiltinKeywords.Contains(token.TextValue))
                            // referencing not existing label
                            Errors.Add(new Error("label not found", token));
                        break;
                }
            }

            OutputTokenList.RemoveAll(t => t.Type == Token.TokenType.StartOfFile || t.Type == Token.TokenType.EndOfFile);
        }

        private Token SkipLine(bool throwErrors)
        {
            while (Tokens.Peek.Type != Token.TokenType.EndOfLine)
            {
                switch (Tokens.Peek.Type)
                {
                    case Token.TokenType.Error:
                        if (throwErrors) Errors.Add(new Error(Tokens.Peek));
                        Tokens.Next();
                        break;

                    case Token.TokenType.Punctuation:
                        if (Tokens.Peek.CharValue == ';')
                        {
                            throwErrors = false;
                            Tokens.Next();
                            break;
                        }
                        else
                            goto case Token.TokenType.Directive;

                    case Token.TokenType.Directive:
                    case Token.TokenType.Label:
                    case Token.TokenType.Name:
                    case Token.TokenType.Number:
                    case Token.TokenType.PackedString:
                    case Token.TokenType.StartOfFile:
                    case Token.TokenType.String:
                    case Token.TokenType.EndOfFile:
                        if (throwErrors) Errors.Add(new Error("expected comment or end of line", Tokens.Peek));
                        throwErrors = false;
                        Tokens.Next();
                        break;
                }
            }

            // skip end of line token
            var endOfLine = Tokens.Peek;
            Tokens.Next();
            return endOfLine;
        }

        private void HandleDirective()
        {
            string name = Tokens.Peek.TextValue;
            Tokens.Next();

            if (name == "INCLUDE")
                HandleInclude();
            else if (name == "DEFINE")
                HandleDefine();
            else if (name == "MACRO")
                HandleMacro();
            else if (name == "GLOBAL")
                HandleGlobalLabel();
            else
            {
                Errors.Add(new Error("unknown directive", Tokens.Peek));
                SkipLine(false);
            }
        }

        private void HandleInclude()
        {
            Token filename = Tokens.Peek;
            if (filename.Type != Token.TokenType.String)
            {
                Errors.Add(new Error("expected quoted file name", filename));
                SkipLine(false);
                return;
            }

            // skip filename
            Tokens.Next();

            SkipLine(true);
            IncludeFile(filename.TextValue, filename);
        }

        private void HandleDefine()
        {
            // get name of definition
            Token definition = Tokens.Peek;
            if (definition.Type != Token.TokenType.Name)
            {
                Errors.Add(new Error("invalid name", definition));
                SkipLine(false);
                return;
            }

            // if name is used then ignore
            if (IsNameUsed(definition))
            {
                SkipLine(false);
                return;
            }

            // read list of replacement tokens
            List<Token> replacement = new List<Token>();
            bool reachedEnd = false;
            while (!reachedEnd)
            {
                Token t = Tokens.Next();
                switch (t.Type)
                {
                    case Token.TokenType.Punctuation:
                        if (t.CharValue == ';')
                            goto case Token.TokenType.EndOfLine;
                        else
                            goto default;
                    case Token.TokenType.EndOfLine:
                        reachedEnd = true;
                        break;
                    default:
                        replacement.Add(t);
                        break;
                }
            }

            // check if there is recursive replacement
            if (replacement.Any(t => t.Type == Token.TokenType.Name && t.TextValue == definition.TextValue))
            {
                Errors.Add(new Error("recursive replacement detected", definition));
                SkipLine(false);
                return;
            }

            Definitions.Add(definition.TextValue, new Definition(definition, replacement));
            SkipLine(true);
        }

        private void HandleMacro()
        {
            // macro name
            Token macroName = Tokens.Peek;
            if (macroName.Type != Token.TokenType.Name)
            {
                Errors.Add(new Error("invalid macro name", macroName));
                SkipLine(false);
                return;
            }

            // if name is used then ignore
            if (IsNameUsed(macroName))
            {
                SkipLine(false);
                return;
            }

            // opening parentheses
            Token openParenth = Tokens.Next();
            if (openParenth.Type != Token.TokenType.Punctuation || openParenth.CharValue != '(')
            {
                Errors.Add(new Error("expected opening parentheses", openParenth));
                SkipLine(false);
                return;
            }

            Tokens.Next();

            List<string> parameters = new List<string>();
            if (Tokens.Peek.Type == Token.TokenType.Punctuation && Tokens.Peek.CharValue == ')')
            {
                // no parameters
            }
            else
            {
                // read parameters
                while (true)
                {
                    Token param = Tokens.Peek;
                    if (param.Type != Token.TokenType.Name)
                    {
                        Errors.Add(new Error("invalid parameter name", param));
                        SkipLine(false);
                        return;
                    }
                    
                    parameters.Add(param.TextValue);
                    Tokens.Next();

                    Token punctuation = Tokens.Peek;
                    if (punctuation.Type != Token.TokenType.Punctuation || (punctuation.CharValue != ',' && punctuation.CharValue != ')'))
                    {
                        Errors.Add(new Error("expected comma or closing parentheses", punctuation));
                        SkipLine(false);
                        return;
                    }

                    // done reading parameters
                    if (punctuation.CharValue == ')')
                        break;

                    Tokens.Next();
                }
            }

            // skip last punctiation
            Token lastPunctuation = Tokens.Peek;
            Tokens.Next();

            // should be empty line
            SkipLine(true);

            // read until ENDMACRO directive

            List<Token> replacement = new List<Token>();

            while (true)
            {
                switch (Tokens.Peek.Type)
                {
                    case Token.TokenType.Directive:
                        if (Tokens.Peek.TextValue == "ENDMACRO")
                        {
                            Macros.Add(macroName.TextValue, new MacroDefinition(macroName, parameters.ToArray(), replacement));
                            Tokens.Next();
                            SkipLine(true);
                            return;
                        }
                        else
                        {
                            Errors.Add(new Error("preprocessor directives are not supported inside macros", Tokens.Peek));
                            SkipLine(false);
                        }
                        break;

                    case Token.TokenType.EndOfFile:
                        Errors.Add(new Error("missing .ENDMACRO directive", 
                            replacement.Count == 0 ? lastPunctuation : replacement[replacement.Count - 1]));
                        return;

                    case Token.TokenType.Error:
                        Errors.Add(new Error(Tokens.Peek));
                        Tokens.Next();
                        break;

                    case Token.TokenType.Label:
                        /*if (!IsNameUsed(Tokens.Peek))
                        {
                            LocalLabels.Add(Tokens.Peek.TextValue, new LabelDefinition(Tokens.Peek, 0));
                            OutputTokenList.Add(Tokens.Peek);
                            Tokens.Next();
                        }
                        break;*/

                    case Token.TokenType.EndOfLine:
                    case Token.TokenType.Name:
                    case Token.TokenType.Number:
                    case Token.TokenType.PackedString:
                    case Token.TokenType.Punctuation:
                    case Token.TokenType.String:
                        replacement.Add(Tokens.Peek);
                        Tokens.Next();
                        break;

                    case Token.TokenType.StartOfFile:
                        Errors.Add(new Error("this should not ever happen", lastPunctuation));
                        break;
                }
            }
        }

        private void HandleGlobalLabel()
        {
            Token labelName = Tokens.Peek;
            if (labelName.Type != Token.TokenType.Name)
            {
                Errors.Add(new Error("invalid label name", labelName));
                SkipLine(false);
                return;
            }

            // skip label name
            Tokens.Next();
            
            GlobalLabels.Add(labelName.TextValue, new LabelDefinition(labelName, 0));
            OutputTokenList.Add(Token.GlobalLabel(labelName.TextValue, labelName));
            SkipLine(true);
        }

        private void ExpandMacro(string name, Token token)
        {
            List<List<Token>> parameters = new List<List<Token>>();

            // check if opening parentheses are supplied
            Token first = Tokens.Peek;
            bool needCloseParenth = false;
            if (first.Type == Token.TokenType.Punctuation && first.CharValue == '(')
            {
                Tokens.Next();
                needCloseParenth = true;
            }

            // read parameters
            bool finished = false;
            while (!finished)
            {
                switch (Tokens.Peek.Type)
                {
                    case Token.TokenType.Punctuation:
                        if (Tokens.Peek.CharValue == ',')
                        {
                            // next parameter
                            parameters.Add(new List<Token>());
                            Tokens.Next();
                        }
                        else if (Tokens.Peek.CharValue == ')')
                        {
                            // check if needed, if yes then done
                            if (needCloseParenth)
                            {
                                Tokens.Next();
                                SkipLine(true);
                                finished = true;
                            }
                            else // otherwise error
                            {
                                Errors.Add(new Error("unexpected parentheses", Tokens.Peek));
                                SkipLine(false);
                                return;
                            }
                        }
                        else if (Tokens.Peek.CharValue == ';') 
                        {
                            // same as getting end of line
                            goto case Token.TokenType.EndOfLine;
                        }
                        break;
                    case Token.TokenType.EndOfLine:
                        // if parentheses are not needed, then done
                        if (!needCloseParenth)
                        {
                            SkipLine(true);
                            finished = true;
                            break;
                        }
                        else // otherwise error
                        {
                            Errors.Add(new Error("expected closing parentheses", Tokens.Peek));
                            SkipLine(false);
                            return;
                        }
                    case Token.TokenType.Name:
                        Token nameToken = Tokens.Peek;
                        Tokens.Next();
                        if (TryExpandName(nameToken, false))
                        {
                            // no parameters yet, create first
                            if (parameters.Count == 0)
                                parameters.Add(new List<Token>());
                            // add to last parameter
                            parameters[parameters.Count - 1].Add(nameToken);
                        }
                        break;
                    default:
                        // no parameters yet, create first
                        if (parameters.Count == 0)
                            parameters.Add(new List<Token>());
                        // add to last parameter
                        parameters[parameters.Count - 1].Add(Tokens.Peek);
                        Tokens.Next();
                        break;
                }
            }

            MacroDefinition macro = Macros[name];
            if (macro.Parameters.Length != parameters.Count)
            {
                Errors.Add(new Error($"expected {macro.Parameters.Length} parameters, got {parameters.Count}", token));
                return;
            }

            // insert macro body
            Tokens.InsertMacro(macro.Replacement, token);
            // should be at the start of new (implicit) file
            System.Diagnostics.Debug.Assert(Tokens.Peek.Type == Token.TokenType.StartOfFile);

            // handle file start
            int fileID = Tokens.Peek.IntValue;
            OutputTokenList.Add(Tokens.Peek);
            FileLocalDefinitions.Push(new Dictionary<string, Definition>());
            FileStack.Push(fileID);
            FileLocalLabels.Add(fileID, new Dictionary<string, LabelDefinition>());
            Tokens.Next();

            // add local definitions for parameters
            for (int i = 0; i < macro.Parameters.Length; i++)
                LocalDefinitions.Add(macro.Parameters[i], new Definition(macro.DefiningToken, parameters[i]));
        }
        
        private bool IsNameUsed(Token token)
        {
            if (Macros.ContainsKey(token.TextValue))
                Errors.Add(new Error(string.Format("name '{0}' is already used for macro (defined at '{1}', line {2})",
                    token.TextValue,
                    Macros[token.TextValue].DefiningToken.Origin.SourceFile,
                    Macros[token.TextValue].DefiningToken.Origin.SourceLineNumber),
                    token));
            else if (Definitions.ContainsKey(token.TextValue))
                Errors.Add(new Error(string.Format("name '{0}' is already used for replacement (defined at '{1}', line {2})",
                    token.TextValue,
                    Definitions[token.TextValue].DefiningToken.Origin.SourceFile,
                    Definitions[token.TextValue].DefiningToken.Origin.SourceLineNumber),
                    token));
            else if (LocalDefinitions.ContainsKey(token.TextValue))
                Errors.Add(new Error(string.Format("name '{0}' is already used as macro parameter",
                    token.TextValue),
                    token));
            else if (GlobalLabels.ContainsKey(token.TextValue))
                Errors.Add(new Error(string.Format("name '{0}' is already used for global label (defined at '{1}', line {2})",
                    token.TextValue,
                    GlobalLabels[token.TextValue].DefiningToken.Origin.SourceFile,
                    GlobalLabels[token.TextValue].DefiningToken.Origin.SourceLineNumber),
                    token));
            else if (LocalLabels.ContainsKey(token.TextValue))
                Errors.Add(new Error(string.Format("name '{0}' is already used for local label (defined at '{1}', line {2})",
                    token.TextValue,
                    LocalLabels[token.TextValue].DefiningToken.Origin.SourceFile,
                    LocalLabels[token.TextValue].DefiningToken.Origin.SourceLineNumber),
                    token));
            else if (Instructions.ContainsKey(token.TextValue))
                Errors.Add(new Error(string.Format("name '{0}' is already used for instruction",
                    token.TextValue),
                    token));
            else if (BuiltinKeywords.Contains(token.TextValue))
                Errors.Add(new Error(string.Format("name '{0}' is already used as builtin keyword",
                    token.TextValue),
                    token));
            else
                return false;

            return true;
        }

        private bool TryExpandName(Token token, bool allowMacroExpansion)
        {
            if (Definitions.ContainsKey(token.TextValue))
            {
                if (token.Expansion < Token.AllowedExpansion.OnlyGlobalDefinitions)
                {
                    Errors.Add(new Error("cannot expand definition here", token));
                    return false;
                }
                else
                {
                    Tokens.InsertDefinitionExpansion(Definitions[token.TextValue].Replacement, token, false);
                    return false;
                }
            }
            else if (LocalDefinitions.ContainsKey(token.TextValue))
            {
                if (token.Expansion < Token.AllowedExpansion.OnlyDefinitions)
                {
                    Errors.Add(new Error("cannot expand parameter here", token));
                    return false;
                }
                else
                {
                    Tokens.InsertDefinitionExpansion(LocalDefinitions[token.TextValue].Replacement, token, false);
                    return false;
                }
            }
            else if (Macros.ContainsKey(token.TextValue))
            {
                if (!allowMacroExpansion)
                {
                    Errors.Add(new Error("macro must be used at the beginning of the line or after labels", token));
                    SkipLine(false);
                    return false;
                }
                else if (token.Expansion < Token.AllowedExpansion.All)
                {
                    Errors.Add(new Error("cannot expand macro within macro", token));
                    SkipLine(false);
                    return false;
                }
                else
                {
                    ExpandMacro(token.TextValue, token);
                    return false;
                }
            }

            return true;
        }
    }
}
