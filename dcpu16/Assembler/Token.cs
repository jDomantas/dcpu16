using System.Collections.Generic;
using System.Text;

namespace dcpu16.Assembler
{
    class Token
    {
        private static int FileIndexCounter = 0;

        public enum TokenType
        {
            EndOfLine, EndOfFile, StartOfFile,
            Number, String, PackedString, Name,
            Punctuation, Directive, Label,
            Error
        }

        public enum AllowedExpansion { None = 0, OnlyGlobalDefinitions = 1, OnlyDefinitions = 2, All = 3 }

        public TokenType Type { get; private set; }
        public int IntValue { get; private set; }
        public string TextValue { get; private set; }
        public ushort NumericValue { get; private set; }
        public char CharValue { get; private set; }
        public AllowedExpansion Expansion { get; private set; }

        public readonly LineOfCode Origin;
        public readonly int OriginColumn;

        private Token(int fileID)
        {
            Expansion = AllowedExpansion.None;
            Type = TokenType.StartOfFile;
            IntValue = fileID;
            TextValue = null;
            NumericValue = 0;
            CharValue = '\0';
            Origin = null;
            OriginColumn = 0;
        }

        private Token(TokenType type, LineOfCode origin, int column)
        {
            Expansion = AllowedExpansion.All;
            Type = type;
            TextValue = null;
            NumericValue = 0;
            CharValue = '\0';

            Origin = origin;
            OriginColumn = column;
        }

        private Token(TokenType type, string text, LineOfCode origin, int column)
        {
            Expansion = AllowedExpansion.All;
            Type = type;
            if (type != TokenType.String && type != TokenType.PackedString && type != TokenType.Error)
                TextValue = text.ToUpper();
            else
                TextValue = text;
            NumericValue = 0;
            CharValue = '\0';

            Origin = origin;
            OriginColumn = column;
        }

        private Token(TokenType type, ushort number, LineOfCode origin, int column)
        {
            Expansion = AllowedExpansion.All;
            Type = type;
            TextValue = null;
            NumericValue = number;
            CharValue = '\0';

            Origin = origin;
            OriginColumn = column;
        }

        private Token(TokenType type, char symbol, LineOfCode origin, int column)
        {
            Expansion = AllowedExpansion.All;
            Type = type;
            TextValue = null;
            NumericValue = 0;
            CharValue = symbol;

            Origin = origin;
            OriginColumn = column;
        }

        private Token(Token from, LineOfCode newOrigin, int column)
        {
            Expansion = AllowedExpansion.All;
            Type = from.Type;
            IntValue = from.IntValue;
            TextValue = from.TextValue;
            NumericValue = from.NumericValue;
            CharValue = from.CharValue;

            Origin = newOrigin;
            OriginColumn = column;
        }

        private static bool IsNameSymbol(char symbol)
        {
            return char.IsLetterOrDigit(symbol) || symbol == '_';
        }

        public static IEnumerable<Token> Tokenize(LineOfCode line)
        {
            bool suspressDirectives = false;

            for (int i = 0; i < line.Value.Length; )
            {
                int start = i;

                if (IsNameSymbol(line.Value[i]))
                {
                    string alphanum = ReadAlphanumericToken(line.Value, ref i);
                    if (alphanum == "p" &&
                        i < line.Value.Length &&
                        (line.Value[i] == '\'' || line.Value[i] == '"'))
                        yield return ReadStringConstant(line.Value, ref i, true, line);
                    else if (i < line.Value.Length && line.Value[i] == ':')
                    {
                        yield return new Token(TokenType.Label, alphanum, line, start);
                        i++;
                    }
                    else
                        yield return ParseAlphanumeric(alphanum, line, start);
                    suspressDirectives = true;
                }
                else if (line.Value[i] == '\'' || line.Value[i] == '"')
                {
                    yield return ReadStringConstant(line.Value, ref i, false, line);
                    suspressDirectives = true;
                }
                else if (line.Value[i] == '.' && !suspressDirectives)
                {
                    i++;
                    if (i < line.Value.Length && IsNameSymbol(line.Value[i]))
                        yield return new Token(TokenType.Directive, ReadAlphanumericToken(line.Value, ref i), line, start);
                    else
                        yield return new Token(TokenType.Punctuation, '.', line, start);
                    suspressDirectives = true;
                }
                else if (line.Value[i] == ':')
                {
                    i++;
                    if (i < line.Value.Length && IsNameSymbol(line.Value[i]))
                        yield return new Token(TokenType.Label, ReadAlphanumericToken(line.Value, ref i), line, start);
                    else
                        yield return new Token(TokenType.Punctuation, ':', line, start);
                    suspressDirectives = true;
                }
                else if (!char.IsWhiteSpace(line.Value[i]))
                {
                    yield return new Token(TokenType.Punctuation, line.Value[i], line, start);
                    i++;
                    suspressDirectives = true;
                }
                else
                {
                    i++;
                }
            }

            yield return new Token(TokenType.EndOfLine, line, line.Value.Length);
            yield break;
        }
        
        private static int ParseInt(string value, int b)
        {
            if (value.Length == 0) return -1;
            
            int acc = 0;
            for (int i = 0; i < value.Length; i++)
            {
                acc *= b;
                switch (value[i])
                {
                    case '0':break;
                    case '1': acc += 1; break;
                    case '2': if (b < 3) return -1; acc += 2; break;
                    case '3': if (b < 4) return -1; acc += 3; break;
                    case '4': if (b < 5) return -1; acc += 4; break;
                    case '5': if (b < 6) return -1; acc += 5; break;
                    case '6': if (b < 7) return -1; acc += 6; break;
                    case '7': if (b < 8) return -1; acc += 7; break;
                    case '8': if (b < 9) return -1; acc += 8; break;
                    case '9': if (b < 10) return -1; acc += 9; break;
                    case 'a': case 'A': if (b < 11) return -1; acc += 10; break;
                    case 'b': case 'B': if (b < 12) return -1; acc += 11; break;
                    case 'c': case 'C': if (b < 13) return -1; acc += 12; break;
                    case 'd': case 'D': if (b < 14) return -1; acc += 13; break;
                    case 'e': case 'E': if (b < 15) return -1; acc += 14; break;
                    case 'f': case 'F': if (b < 16) return -1; acc += 15; break;
                }
            }

            return acc;
        }

        private static Token ParseAlphanumeric(string value, LineOfCode origin, int position)
        {
            if (char.IsDigit(value[0]))
            {
                int val = -1;
                if (value.StartsWith("0b"))
                    val = ParseInt(value.Substring(2), 2);
                else if (value.StartsWith("0x"))
                    val = ParseInt(value.Substring(2), 16);
                else
                    val = ParseInt(value, 10);

                if (val < 0) return new Token(TokenType.Error, $"invalid integer literal: {value}", origin, position);
                else if (val > 65535) return new Token(TokenType.Error, $"integer literal is too large: {value}", origin, position);
                else return new Token(TokenType.Number, (ushort)val, origin, position);
            }
            else
                return new Token(TokenType.Name, value, origin, position);
        }

        private static string ReadAlphanumericToken(string line, ref int position)
        {
            int start = position;
            while (position < line.Length && IsNameSymbol(line[position]))
                position++;

            return line.Substring(start, position - start);
        }

        private static Token ReadStringConstant(string line, ref int position, bool packedType, LineOfCode origin)
        {
            StringBuilder result = new StringBuilder();

            int start = position;
            bool doubleQuotes = (line[position] == '"');
            bool escaping = false;
            bool ended = false;
            position++;
            
            while (position < line.Length)
            {
                char current = line[position];

                if (escaping)
                {
                    if (current == '"' ||
                        current == '\'' ||
                        current == '\\')
                        result.Append(current);
                    else if (current == '0') result.Append('\0');
                    else if (current == 'n') result.Append((char)10);
                    else if (current == 't') result.Append((char)9);
                    else return new Token(TokenType.Error, $"invalid escape sequence: \\{current}", origin, position);

                    escaping = false;
                }
                else
                {
                    if (current == '\\') escaping = true;
                    else if (current == '"' && doubleQuotes) { ended = true; break; }
                    else if (current == '\'' && !doubleQuotes) { ended = true; break; }
                    else result.Append(current);
                }

                position++;
            }

            if (ended)
            {
                position++;
                string val = result.ToString();

                if (!packedType && val.Length == 1)
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(val);
                    return new Token(TokenType.Number, bytes[0], origin, start);
                }
                else if (packedType && (val.Length == 1 || val.Length == 2))
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(val);
                    return new Token(TokenType.Number, (ushort)(bytes[0] + (bytes.Length == 2 ? bytes[1] << 8 : 0)), origin, start);
                }
                else
                    return new Token(packedType ? TokenType.PackedString : TokenType.String, val, origin, start);
            }
            else
            {
                return new Token(TokenType.Error, "string constant not closed", origin, start);
            }
        }

        public Token ChangeOrigin(Token origin, AllowedExpansion newExpansion)
        {
            return new Token(this, origin.Origin, origin.OriginColumn) { Expansion = newExpansion };
        }

        public void ChangeNumber(int number)
        {
            TextValue = $"{TextValue}@{number}";
        }

        public override string ToString()
        {
            switch (Type)
            {
                case TokenType.Directive: return $"Token.Directive{{.{TextValue}}}";
                case TokenType.EndOfFile: return $"Token.EndOfFile";
                case TokenType.StartOfFile: return $"Token.StartOfFile";
                case TokenType.EndOfLine: return $"Token.EndOfLine";
                case TokenType.Error: return $"Token.Error{{{TextValue}}}";
                case TokenType.Label: return $"Token.Label{{{TextValue}}}";
                case TokenType.Name: return $"Token.Name{{{TextValue}}}";
                case TokenType.Number: return $"Token.Number{{{NumericValue}}}";
                case TokenType.PackedString: return $"Token.PackedString{{p\"{TextValue}\"}}";
                case TokenType.Punctuation: return $"Token.Punctuation{{{CharValue}}}";
                case TokenType.String: return $"Token.String{{\"{TextValue}\"}}";
                default: return $"Token.Unknown{{{Type}}}";
            }
        }
        
        public string ToString(bool pretty)
        {
            if (!pretty) return ToString();

            switch (Type)
            {
                case TokenType.Directive: return $".{TextValue}";
                case TokenType.EndOfFile: return $"=== END OF FILE ===\n";
                case TokenType.StartOfFile: return $"=== START OF FILE ===\n";
                case TokenType.EndOfLine: return $"\n";
                case TokenType.Error: return $"Error({TextValue})";
                case TokenType.Label: return $"{TextValue}:";
                case TokenType.Name: return TextValue;
                case TokenType.Number: return NumericValue.ToString();
                case TokenType.PackedString: return $"p\"{TextValue}\"";
                case TokenType.Punctuation: return CharValue.ToString();
                case TokenType.String: return $"\"{TextValue}\"";
                default: return $"Token.Unknown{{{Type}}}";
            }
        }

        public static Token EndOfFile()
        {
            return new Token(TokenType.EndOfFile, null, 0);
        }

        public static Token StartOfFile()
        {
            return new Token(++FileIndexCounter);
        }

        public static Token GlobalLabel(string name, Token origin)
        {
            return new Token(TokenType.Label, name, origin.Origin, origin.OriginColumn);
        }
    }
}
