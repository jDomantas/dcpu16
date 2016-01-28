using System.Collections.Generic;

namespace dcpu16.Assembler
{
    class Definition
    {
        public readonly List<Token> Replacement;
        public readonly Token DefiningToken;

        public Definition(Token token, List<Token> replacement)
        {
            DefiningToken = token;
            Replacement = replacement;
        }
    }
}
