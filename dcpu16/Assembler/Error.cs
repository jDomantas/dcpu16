using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dcpu16.Assembler
{
    class Error
    {
        public readonly string Message;
        public readonly LineOfCode SourceLine;
        public readonly int Column;

        public Error(string msg, LineOfCode line, int column)
        {
            Message = msg;
            SourceLine = line;
            Column = column;
        }

        public Error(string msg, Token token)
        {
            Message = msg;
            SourceLine = token.Origin;
            Column = token.OriginColumn;
        }

        public Error(Token token)
        {
            Message = token.TextValue;
            SourceLine = token.Origin;
            Column = token.OriginColumn;
        }
    }
}
