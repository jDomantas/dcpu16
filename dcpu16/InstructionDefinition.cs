namespace dcpu16
{
    class InstructionDefinition
    {
        public enum Params { Two, OnlyA, None, Data }

        public readonly Params Parameters;
        public readonly ushort OpCode;

        public InstructionDefinition(ushort opCode, Params parameters)
        {
            Parameters = parameters;
            OpCode = opCode;
        }
    }
}
