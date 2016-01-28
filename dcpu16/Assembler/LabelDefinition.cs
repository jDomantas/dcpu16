namespace dcpu16.Assembler
{
    class LabelDefinition
    {
        public readonly Token DefiningToken;
        public readonly ushort Value;
        
        public LabelDefinition(Token token, ushort value)
        {
            DefiningToken = token;
            Value = value;
        }
    }
}
