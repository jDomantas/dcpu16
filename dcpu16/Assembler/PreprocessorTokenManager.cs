using System.Collections.Generic;

namespace dcpu16.Assembler
{
    class PreprocessorTokenManager
    {
        public Token Peek { get { return Tokens.Count == 0 ? null : Tokens.Peek().Peek(); } }
        private Stack<Queue<Token>> Tokens;

        public PreprocessorTokenManager()
        {
            Tokens = new Stack<Queue<Token>>();
        }
        
        public Token Next()
        {
            Tokens.Peek().Dequeue();
            while (Tokens.Count > 0 && Tokens.Peek().Count == 0)
                Tokens.Pop();
            return Peek;
        }
        
        public int InsertFile(IEnumerable<LineOfCode> lines)
        {
            Queue<Token> newQueue = new Queue<Token>();
            Token startToken = Token.StartOfFile();
            newQueue.Enqueue(startToken);

            foreach (var line in lines)
                foreach (var token in Token.Tokenize(line))
                    newQueue.Enqueue(token);

            newQueue.Enqueue(Token.EndOfFile());

            Tokens.Push(newQueue);

            return startToken.IntValue;
        }

        public void InsertDefinitionExpansion(List<Token> tokens, Token replaced, bool allowGlobals)
        {
            if (tokens.Count == 0) return;
            
            Queue<Token> q = new Queue<Token>(tokens.Count);
            foreach (var token in tokens)
                q.Enqueue(token.ChangeOrigin(replaced, allowGlobals ? Token.AllowedExpansion.OnlyGlobalDefinitions : Token.AllowedExpansion.None));

            Tokens.Push(q);
        }

        public int InsertMacro(List<Token> tokens, Token replaced)
        {
            Queue<Token> newQueue = new Queue<Token>(tokens.Count + 2);
            Token startToken = Token.StartOfFile();
            newQueue.Enqueue(startToken);

            foreach (var token in tokens)
                newQueue.Enqueue(token.ChangeOrigin(token, Token.AllowedExpansion.OnlyDefinitions));

            newQueue.Enqueue(Token.EndOfFile());

            Tokens.Push(newQueue);
            return startToken.IntValue;
        }
    }
}
