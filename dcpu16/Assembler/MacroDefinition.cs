using System.Collections.Generic;

namespace dcpu16.Assembler
{
    class MacroDefinition
    {
        public readonly string[] Parameters;
        public readonly List<Token> Replacement;
        public readonly Token DefiningToken;

        public MacroDefinition(Token token, string[] parameters, List<Token> replacement)
        {
            DefiningToken = token;

            Parameters = parameters;
            Replacement = replacement;
        }
    }
}
