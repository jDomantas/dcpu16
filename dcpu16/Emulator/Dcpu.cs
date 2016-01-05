using dcpu16.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dcpu16.Emulator
{
    class Dcpu
    {
        public enum Registers
        {
            A = 0x40000,
            B = 0x40001,
            C = 0x40002,
            X = 0x40003,
            Y = 0x40004,
            Z = 0x40005,
            I = 0x40006,
            J = 0x40007,
            PC = 0x40008,
            SP = 0x40009,
            EX = 0x4000A,
            IA = 0x4000B,
            SS = 0x4000C
        }

        public bool ExtendedSpecification { get; }

        public const int MemoryLength = 0x40000;
        public const int RegisterCount = 13;
        public int MemoryMask { get; private set; }
        private const int InternalLiteral = 0x4000D;

        public ushort[] Memory;
        public int CycleDebt;
        private int InstructionsToSkip;
        private bool InterruptQueueingEnabled;
        private Queue<ushort> InterruptQueue;
        private bool Halted;
        private bool AfterSegInstruction;
        private bool ResetSegment;

        public ushort A { get { return Memory[(int)Registers.A]; } set { Memory[(int)Registers.A] = value; } }
        public ushort B { get { return Memory[(int)Registers.B]; } set { Memory[(int)Registers.B] = value; } }
        public ushort C { get { return Memory[(int)Registers.C]; } set { Memory[(int)Registers.C] = value; } }
        public ushort X { get { return Memory[(int)Registers.X]; } set { Memory[(int)Registers.X] = value; } }
        public ushort Y { get { return Memory[(int)Registers.Y]; } set { Memory[(int)Registers.Y] = value; } }
        public ushort Z { get { return Memory[(int)Registers.Z]; } set { Memory[(int)Registers.Z] = value; } }
        public ushort I { get { return Memory[(int)Registers.I]; } set { Memory[(int)Registers.I] = value; } }
        public ushort J { get { return Memory[(int)Registers.J]; } set { Memory[(int)Registers.J] = value; } }
        public ushort PC { get { return Memory[(int)Registers.PC]; } set { Memory[(int)Registers.PC] = value; } }
        public ushort SP { get { return Memory[(int)Registers.SP]; } set { Memory[(int)Registers.SP] = value; } }
        public ushort EX { get { return Memory[(int)Registers.EX]; } set { Memory[(int)Registers.EX] = value; } }
        public ushort IA { get { return Memory[(int)Registers.IA]; } set { Memory[(int)Registers.IA] = value; } }
        public ushort SS { get { return Memory[(int)Registers.SS]; } set { Memory[(int)Registers.SS] = value; } }

        public int MemoryAccessOffset { get { return SS * 4; } }
        private int FetchMemoryOffset;

        private IHardware[] Devices;

        public Dcpu(IHardware[] devices, bool useExtendedSpecification)
        {
            Memory = new ushort[MemoryLength + RegisterCount + 1];
            InstructionsToSkip = 0;
            CycleDebt = 0;
            InterruptQueueingEnabled = false;
            InterruptQueue = new Queue<ushort>();

            Devices = devices;

            ExtendedSpecification = useExtendedSpecification;

            AfterSegInstruction = true;
            ResetSegment = false;

            MemoryMask = useExtendedSpecification ? 0x3FFFF : 0xFFFF;
        }

        public void QueueInterrupt(ushort message)
        {
            if (Memory[(int)Registers.IA] == 0)
                return;

            InterruptQueue.Enqueue(message);
            if (InterruptQueue.Count > 256)
                CatchFire();
        }

        public void ExecuteInstruction()
        {
            // All cycle costs are reduced by one because it
            // takes one cycle to fetch instruction code.

            // if segment will be reset after this instruction,
            // then this is after seg instruction
            FetchMemoryOffset = ResetSegment ? 0 : MemoryAccessOffset;

            ushort instruction = FetchWord();

            int opCode = instruction & 0x1F;

            if (opCode == 0)
            {
                ExecuteSpecialInstruction(instruction);
                return;
            }

            int b = GetOperandB(instruction);
            int a = GetOperandA(instruction);

            if (InstructionsToSkip > 0)
            {
                InstructionsToSkip--;
                if (opCode >= 0x10 && opCode <= 0x17) // if instruction
                    InstructionsToSkip++;

                if (InstructionsToSkip == 0)
                    PostInstruction();

                return;
            }

            int newEX;
            int signedA = (short)Memory[a];
            int signedB = (short)Memory[b];
            if (signedA < 0)
            {
                signedA *= -1;
                signedB *= -1;
            }

            switch (opCode)
            {
                case 0x01: // SET
                    Memory[b] = Memory[a];
                    break;
                case 0x02: // ADD
                    ConsumeCycle(1);
                    newEX = (Memory[a] + Memory[b]) >> 16;
                    Memory[b] += Memory[a];
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x03: // SUB
                    ConsumeCycle(1);
                    newEX = (Memory[a] - Memory[b]) >> 16;
                    Memory[b] -= Memory[a];
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x04: // MUL
                    ConsumeCycle(1);
                    uint product = (uint)Memory[a] * (uint)Memory[b];
                    EX = (ushort)((product >> 16) & 0xFFFF);
                    Memory[b] = (ushort)(product & 0xFFFF);
                    break;
                case 0x05: // MLI
                    ConsumeCycle(1);
                    int signedProduct = signedA * signedB;
                    EX = (ushort)((signedProduct >> 16) & 0xFFFF);
                    Memory[b] = (ushort)(signedProduct & 0xFFFF);
                    break;
                case 0x06: // DIV
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                    {
                        EX = Memory[b] = 0;
                    }
                    else
                    {
                        uint quotent = ((uint)Memory[b] << 16) / (uint)Memory[a];
                        EX = (ushort)(quotent & 0xFFFF);
                        Memory[b] = (ushort)((quotent >> 16) & 0xFFFF);
                    }
                    break;
                case 0x07: // DVI
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                    {
                        EX = Memory[b] = 0;
                    }
                    else
                    {
                        int signedQuotent = (Math.Abs(signedB) << 16) / signedA;
                        EX = (ushort)(signedQuotent & 0xFFFF);
                        if (signedB < 0) signedQuotent *= -1;
                        Memory[b] = (ushort)((signedQuotent >> 16) & 0xFFFF);
                    }
                    break;
                case 0x08: // MOD
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                        Memory[b] = 0;
                    else
                        Memory[b] %= Memory[a];
                    break;
                case 0x09: // MDI
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                        Memory[b] = 0;
                    else
                        Memory[b] = (ushort)((Math.Abs(signedB) % signedA * (signedB < 0 ? -1 : 1)) & 0xFFFF);
                    break;
                case 0x0A: // AND
                    Memory[b] &= Memory[a];
                    break;
                case 0x0B: // OR (BOR)
                    Memory[b] |= Memory[a];
                    break;
                case 0x0C: // XOR
                    Memory[b] ^= Memory[a];
                    break;
                case 0x0D: // SHR (logical shift)
                    EX = (ushort)((((int)Memory[b] << 16) >> Memory[a]) & 0xFFFF);
                    Memory[b] >>= Memory[a];
                    break;
                case 0x0E: // ASR (arithmetic shift)
                    EX = (ushort)((((uint)Memory[b] << 16) >> Memory[a]) & 0xFFFF);
                    Memory[b] = (ushort)(((short)Memory[b] >> Memory[a]) & 0xFFFF);
                    break;
                case 0x0F: // SHL
                    EX = (ushort)((((uint)Memory[b] << Memory[a]) >> 16) & 0xFFFF);
                    Memory[b] <<= Memory[a];
                    break;
                case 0x10: // IFB
                    ConsumeCycle(1);
                    if ((Memory[a] & Memory[b]) == 0)
                        InstructionsToSkip = 1;
                    break;
                case 0x11: // IFC
                    ConsumeCycle(1);
                    if ((Memory[a] & Memory[b]) != 0)
                        InstructionsToSkip = 1;
                    break;
                case 0x12: // IFE
                    ConsumeCycle(1);
                    if (Memory[a] != Memory[b])
                        InstructionsToSkip = 1;
                    break;
                case 0x13: // IFN
                    ConsumeCycle(1);
                    if (Memory[a] == Memory[b])
                        InstructionsToSkip = 1;
                    break;
                case 0x14: // IFG
                    ConsumeCycle(1);
                    if (Memory[b] <= Memory[a])
                        InstructionsToSkip = 1;
                    break;
                case 0x15: // IFA
                    ConsumeCycle(1);
                    if ((short)Memory[b] <= (short)Memory[a])
                        InstructionsToSkip = 1;
                    break;
                case 0x16: // IFL
                    ConsumeCycle(1);
                    if (Memory[b] >= Memory[a])
                        InstructionsToSkip = 1;
                    break;
                case 0x17: // IFU
                    ConsumeCycle(1);
                    if ((short)Memory[b] >= (short)Memory[a])
                        InstructionsToSkip = 1;
                    break;
                case 0x1A: // ADX
                    ConsumeCycle(2);
                    newEX = (Memory[a] + Memory[b] + EX) >> 16;
                    Memory[b] += (ushort)((Memory[a] + EX) & 0xFFFF);
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x1B: // SBX
                    ConsumeCycle(2);
                    newEX = (((Memory[b] + EX) & 0xFFFF) - Memory[a]) >> 16;
                    Memory[b] += (ushort)((EX - Memory[a]) & 0xFFFF);
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x1C: // JSG
                    if (!ExtendedSpecification) goto default;
                    ConsumeCycle(1);
                    if (SS == 0)
                    {
                        SS = Memory[a];
                        PC = Memory[b];
                    }
                    break;
                case 0x1E: // STI
                    ConsumeCycle(1);
                    Memory[b] = Memory[a];
                    Memory[(int)Registers.I]++;
                    Memory[(int)Registers.J]++;
                    break;
                case 0x1F: // STJ
                    ConsumeCycle(1);
                    Memory[b] = Memory[a];
                    Memory[(int)Registers.I]--;
                    Memory[(int)Registers.J]--;
                    break;
                default: // unknown instruction
                    Halted = true;
                    Console.WriteLine($"\nUnknown instruction, op code: 0x{opCode.ToString("X2")}");
                    break;
            }

            if (InstructionsToSkip == 0)
                PostInstruction();
        }

        private void ExecuteSpecialInstruction(ushort instruction)
        {
            int opCode = (instruction >> 5) & 0x1F;

            int operand = GetOperandA(instruction);

            if (InstructionsToSkip > 0)
            {
                InstructionsToSkip--;
                
                if (InstructionsToSkip == 0)
                    PostInstruction();

                return;
            }

            switch (opCode)
            {
                case 0x01: // JSR
                    ConsumeCycle(2);
                    Memory[--SP] = PC;
                    PC = Memory[operand];
                    break;
                case 0x02: // SEG
                    if (!ExtendedSpecification) goto default;
                    if (SS == 0)
                    {
                        SS = (ushort)((Memory[operand] * 0x4000) & 0xFFFF);
                        AfterSegInstruction = true;
                        ResetSegment = true;
                    }
                    break;
                case 0x08: // INT
                    ConsumeCycle(3);
                    TriggerInterrupt(Memory[operand]);
                    break;
                case 0x09: // IAG
                    Memory[operand] = IA;
                    break;
                case 0x0A: // IAS
                    IA = Memory[operand];
                    break;
                case 0x0B: // RFI
                    ConsumeCycle(2);
                    InterruptQueueingEnabled = false;
                    A = Memory[SP++]; // pop A
                    PC = Memory[SP++]; // pop PC
                    break;
                case 0x0C: // IAQ
                    ConsumeCycle(1);
                    if (Memory[operand] != 0)
                        InterruptQueueingEnabled = true;
                    else
                        InterruptQueueingEnabled = false;
                    break;
                case 0x10: // HWN
                    ConsumeCycle(1);
                    Memory[operand] = 0;
                    break;
                case 0x11: // HWQ
                    ConsumeCycle(3);
                    A = 0;
                    B = 0;
                    C = 0;
                    X = 0;
                    Y = 0;
                    break;
                case 0x12: // HWI
                    ConsumeCycle(3);
                    if (SS == 0 || !ExtendedSpecification)
                    {
                        int hardwareNumber = Memory[operand];
                        if (hardwareNumber >= 0 && hardwareNumber < Devices.Length)
                            Devices[hardwareNumber].Interrupt(this);
                    }
                    break;
                default: // unknown special instruction
                    Halted = true;
                    if (instruction != 0)
                        Console.WriteLine($"\nUnknown special instruction, op code: 0x{opCode.ToString("X2")}");
                    break;
            }

            if (InstructionsToSkip == 0)
                PostInstruction();
        }

        private void TriggerInterrupt(ushort message)
        {
            if (InterruptQueueingEnabled)
            {
                QueueInterrupt(message);
            }
            else
            {
                if (Memory[(int)Registers.IA] == 0)
                    return;

                InterruptQueueingEnabled = true;

                Memory[--SP] = PC;
                Memory[--SP] = A;
                PC = IA;
                A = message;
            }
        }

        private void CatchFire()
        {
            Console.WriteLine("Catching fire");
            throw new Exception("DCPU-16 is on fire, baby");
        }

        private ushort FetchWord()
        {
            ConsumeCycle();
            return Memory[(PC++ + FetchMemoryOffset) & MemoryMask];
        }

        private int GetOperandA(ushort instruction)
        {
            int code = (instruction >> 10) & 0x3F;
            if (code == 0x18)
            {
                return InstructionsToSkip > 0 ? 
                    (SP + 1 + MemoryAccessOffset) & MemoryMask : 
                    (SP++ + MemoryAccessOffset) & MemoryMask;
            }
            else if (code > 0x1F)
            {
                Memory[InternalLiteral] = (ushort)(code - 0x21);
                return InternalLiteral;
            }
            else
            {
                return GetOperandB((ushort)(instruction >> 5));
            }
        }

        private int GetOperandB(ushort instruction)
        {
            int code = (instruction >> 5) & 0x1F;
            switch (code)
            {
                case 0x00: return (int)Registers.A;
                case 0x01: return (int)Registers.B;
                case 0x02: return (int)Registers.C;
                case 0x03: return (int)Registers.X;
                case 0x04: return (int)Registers.Y;
                case 0x05: return (int)Registers.Z;
                case 0x06: return (int)Registers.I;
                case 0x07: return (int)Registers.J;
                case 0x08: return (A + MemoryAccessOffset) & MemoryMask;
                case 0x09: return (B + MemoryAccessOffset) & MemoryMask;
                case 0x0A: return (C + MemoryAccessOffset) & MemoryMask;
                case 0x0B: return (X + MemoryAccessOffset) & MemoryMask;
                case 0x0C: return (Y + MemoryAccessOffset) & MemoryMask;
                case 0x0D: return (Z + MemoryAccessOffset) & MemoryMask;
                case 0x0E: return (I + MemoryAccessOffset) & MemoryMask;
                case 0x0F: return (J + MemoryAccessOffset) & MemoryMask;
                case 0x10: return (A + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x11: return (B + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x12: return (C + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x13: return (X + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x14: return (Y + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x15: return (Z + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x16: return (I + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x17: return (J + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x18: return InstructionsToSkip > 0 ? 
                        (SP - 1 + MemoryAccessOffset) & MemoryMask : 
                        (--SP + MemoryAccessOffset) & MemoryMask;
                case 0x19: return (SP + MemoryAccessOffset) & MemoryMask;
                case 0x1A: return (SP + FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x1B: return (int)Registers.SP;
                case 0x1C: return (int)Registers.PC;
                case 0x1D: return (int)Registers.EX;
                case 0x1E: return (FetchWord() + MemoryAccessOffset) & MemoryMask;
                case 0x1F: Memory[InternalLiteral] = FetchWord(); return InternalLiteral;
            }

            throw new Exception($"Invalid operand b code: 0x{code.ToString("X2")}");
        }

        private void ConsumeCycle(int amount = 1)
        {
            CycleDebt += amount;
        }

        private void PostInstruction()
        {
            // trigger queued interrupts
            if (!AfterSegInstruction)
            {
                if (!InterruptQueueingEnabled && InterruptQueue.Count > 0)
                    TriggerInterrupt(InterruptQueue.Dequeue());

                if (ResetSegment)
                    SS = 0;
            }
            else
            {
                AfterSegInstruction = false;
            }
        }

        public void DumpRegisters()
        {
            Console.WriteLine("===== DCPU-16 register dump =====");
            Console.WriteLine($"    Hex        Unsigned Signed");
            Console.WriteLine($"A = {A.ToString("X4")}\t{A}\t{(short)A}");
            Console.WriteLine($"B = {B.ToString("X4")}\t{B}\t{(short)B}");
            Console.WriteLine($"C = {C.ToString("X4")}\t{C}\t{(short)C}");
            Console.WriteLine($"X = {X.ToString("X4")}\t{X}\t{(short)X}");
            Console.WriteLine($"Y = {Y.ToString("X4")}\t{Y}\t{(short)Y}");
            Console.WriteLine($"Z = {Z.ToString("X4")}\t{Z}\t{(short)Z}");
            Console.WriteLine($"I = {I.ToString("X4")}\t{I}\t{(short)I}");
            Console.WriteLine($"J = {J.ToString("X4")}\t{J}\t{(short)J}");
            Console.WriteLine($"PC = {PC.ToString("X4")}\t{PC}\t{(short)PC}");
            Console.WriteLine($"SP = {SP.ToString("X4")}\t{SP}\t{(short)SP}");
            Console.WriteLine($"EX = {EX.ToString("X4")}\t{EX}\t{(short)EX}");
            Console.WriteLine($"IA = {IA.ToString("X4")}\t{IA}\t{(short)IA}");
            if (ExtendedSpecification)
                Console.WriteLine($"SS = {SS.ToString("X4")}\t{SS}\t{(short)SS}");
            Console.WriteLine("=================================");
        }

        public void Run(int radiation)
        {
            Stopwatch clock = new Stopwatch();
            Random rnd = new Random((int)DateTime.Now.Ticks);
            clock.Start();

            CycleDebt = 0;
            Halted = false;

            long currentCycles = 0;
            long cyclesPerSecond = 100000;
            
            while (!Halted)
            {
                long ticks = clock.ElapsedTicks;
                long cycles = (ticks * cyclesPerSecond) / Stopwatch.Frequency;
                long passed = cycles - currentCycles;
                if (cycles > currentCycles)
                {
                    CycleDebt -= (int)passed;
                    currentCycles = cycles;
                    for (int i = 0; i < passed; i++)
                    {
                        // flip random bits depending on radiation setting
                        int randVal = rnd.Next(100000);
                        if (randVal < radiation)
                            Memory[rnd.Next(0x10000)] ^= (ushort)(1 << rnd.Next(16));
                    }
                }

                for (int i = 0; i < Devices.Length; i++)
                    Devices[i].UpdateInternal(this, passed);

                while (CycleDebt <= 0 && !Halted)
                {
                    ExecuteInstruction();
                }

                Thread.Sleep(10);
            }

            for (int i = 0; i < Devices.Length; i++)
                Devices[i].Shutdown();
        }
    }
}
