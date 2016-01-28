namespace dcpu16.Assembler
{
    class LineOfCode
    {
        public readonly string Value;
        public readonly string SourceFile;
        public readonly int SourceLineNumber;

        public LineOfCode(string value, string file, int lineNumber)
        {
            Value = value;
            SourceFile = file;
            SourceLineNumber = lineNumber;
        }
    }
}
