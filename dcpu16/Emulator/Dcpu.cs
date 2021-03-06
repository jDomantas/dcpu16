﻿using dcpu16.Hardware;
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
            A = 0x10000,
            B = 0x10001,
            C = 0x10002,
            X = 0x10003,
            Y = 0x10004,
            Z = 0x10005,
            I = 0x10006,
            J = 0x10007,
            PC = 0x10008,
            SP = 0x10009,
            EX = 0x1000A,
            IA = 0x1000B,
            PA = 0x1000E
        }
        
        public const int MemoryLength = 0x10000;
        public const int RegisterCount = 13;
        private const int InternalLiteral = 0x1000C;
        private const int InternalLiteral2 = 0x1000D;

        public ushort[] Memory;
        public int CycleDebt;
        private int InstructionsToSkip;
        private bool InterruptQueueingEnabled;
        private Queue<ushort> InterruptQueue;
        private bool Halted;
        private bool ProtectedMode;

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
        public ushort PA { get { return Memory[(int)Registers.PA]; } set { Memory[(int)Registers.PA] = value; } }

        private IHardware[] Devices;

        public Dcpu(IHardware[] devices)
        {
            Memory = new ushort[MemoryLength + RegisterCount + 2];
            InstructionsToSkip = 0;
            CycleDebt = 0;
            InterruptQueueingEnabled = false;
            InterruptQueue = new Queue<ushort>();
            ProtectedMode = false;

            Devices = devices;
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
            
            // attempting to execute protected memory
            if (ProtectedMode && PC < PA)
            {
                ConsumeCycle();
                ProtectedModeFault();
                return;
            }

            ushort instruction = FetchWord();

            int opCode = instruction & 0x1F;

            if (opCode == 0)
            {
                ExecuteSpecialInstruction(instruction);
                return;
            }

            int a = GetOperandA(instruction);
            int b = GetOperandB(instruction);

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
                    WriteValue(b, Memory[a]);
                    break;
                case 0x02: // ADD
                    ConsumeCycle(1);
                    newEX = (Memory[a] + Memory[b]) >> 16;
                    if (WriteValue(b, Memory[a] + Memory[b])) break;
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x03: // SUB
                    ConsumeCycle(1);
                    newEX = (Memory[a] - Memory[b]) >> 16;
                    if (WriteValue(b, Memory[b] - Memory[a])) break;
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x04: // MUL
                    ConsumeCycle(1);
                    uint product = (uint)Memory[a] * (uint)Memory[b];
                    newEX = (int)((product >> 16) & 0xFFFF);
                    if (WriteValue(b, (ushort)(product & 0xFFFF))) break;
                    EX = (ushort)newEX;
                    break;
                case 0x05: // MLI
                    ConsumeCycle(1);
                    int signedProduct = signedA * signedB;
                    newEX = ((signedProduct >> 16) & 0xFFFF);
                    if (WriteValue(b, (ushort)(signedProduct & 0xFFFF))) break;
                    EX = (ushort)newEX;
                    break;
                case 0x06: // DIV
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                    {
                        if (WriteValue(b, 0)) break;
                        EX = 0;
                    }
                    else
                    {
                        uint quotent = ((uint)Memory[b] << 16) / (uint)Memory[a];
                        newEX = (ushort)(quotent & 0xFFFF);
                        if (WriteValue(b, (ushort)((quotent >> 16) & 0xFFFF))) break;
                        EX = (ushort)newEX;
                    }
                    break;
                case 0x07: // DVI
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                    {
                        if (WriteValue(b, 0)) break;
                        EX = 0;
                    }
                    else
                    {
                        int signedQuotent = (Math.Abs(signedB) << 16) / signedA;
                        newEX = (ushort)(signedQuotent & 0xFFFF);
                        if (signedB < 0) signedQuotent *= -1;
                        if (WriteValue(b, (ushort)((signedQuotent >> 16) & 0xFFFF))) break;
                        EX = (ushort)newEX;
                    }
                    break;
                case 0x08: // MOD
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                        WriteValue(b, 0);
                    else
                        WriteValue(b, Memory[b] % Memory[a]);
                    break;
                case 0x09: // MDI
                    ConsumeCycle(2);
                    if (Memory[a] == 0)
                        WriteValue(b, 0);
                    else
                        WriteValue(b, (ushort)((Math.Abs(signedB) % signedA * (signedB < 0 ? -1 : 1)) & 0xFFFF));
                    break;
                case 0x0A: // AND
                    WriteValue(b, Memory[b] & Memory[a]);
                    break;
                case 0x0B: // OR (BOR)
                    WriteValue(b, Memory[b] | Memory[a]);
                    break;
                case 0x0C: // XOR
                    WriteValue(b, Memory[b] ^ Memory[a]);
                    break;
                case 0x0D: // SHR (logical shift)
                    newEX = (ushort)((((int)Memory[b] << 16) >> Memory[a]) & 0xFFFF);
                    if (WriteValue(b, Memory[b] >> Memory[a])) break;
                    EX = (ushort)newEX;
                    break;
                case 0x0E: // ASR (arithmetic shift)
                    newEX = (ushort)((((uint)Memory[b] << 16) >> Memory[a]) & 0xFFFF);
                    if (WriteValue(b, (ushort)(((short)Memory[b] >> Memory[a]) & 0xFFFF))) break;
                    EX = (ushort)newEX;
                    break;
                case 0x0F: // SHL
                    newEX = (ushort)((((uint)Memory[b] << Memory[a]) >> 16) & 0xFFFF);
                    if (WriteValue(b, Memory[b] << Memory[a])) break;
                    EX = (ushort)newEX;
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
                    if (WriteValue(b, Memory[b] + (ushort)((Memory[a] + EX) & 0xFFFF))) break;
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x1B: // SBX
                    ConsumeCycle(2);
                    newEX = (((Memory[b] + EX) & 0xFFFF) - Memory[a]) >> 16;
                    if (WriteValue(b, Memory[b] + (ushort)((EX - Memory[a]) & 0xFFFF))) break;
                    EX = (ushort)(newEX & 0xFFFF);
                    break;
                case 0x1E: // STI
                    ConsumeCycle(1);
                    if (WriteValue(b, Memory[a])) break;
                    Memory[(int)Registers.I]++;
                    Memory[(int)Registers.J]++;
                    break;
                case 0x1F: // STJ
                    ConsumeCycle(1);
                    if (WriteValue(b, Memory[a])) break;
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

            // executing special instruction that is not JSR or INT
            if (ProtectedMode && opCode != 1 && opCode != 8)
            {
                ProtectedModeFault();
            }
            else
            {
                switch (opCode)
                {
                    case 0x01: // JSR
                        ConsumeCycle(2);
                        if (WriteValue(--SP, PC)) break;
                        PC = Memory[operand];
                        break;
                    case 0x02: // PAG
                        Memory[operand] = PA;
                        break;
                    case 0x03: // PAS
                        PA = Memory[operand];
                        break;
                    case 0x04: // EPM
                        ProtectedMode = true;
                        PC = Memory[operand];
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
                    case 0x0D: // RPI
                        ConsumeCycle(2);
                        ProtectedMode = true;
                        InterruptQueueingEnabled = false;
                        A = Memory[SP++]; // pop A
                        PC = Memory[SP++]; // pop PC
                        break;
                    case 0x10: // HWN
                        ConsumeCycle(1);
                        Memory[operand] = (ushort)Devices.Length;
                        break;
                    case 0x11: // HWQ
                        ConsumeCycle(3);
                        if (Devices.Length <= Memory[operand])
                        {
                            A = 0;
                            B = 0;
                            C = 0;
                            X = 0;
                            Y = 0;
                        }
                        else
                        {
                            A = (ushort)(Devices[Memory[operand]].GetHardwareID() & 0xFFFF);
                            B = (ushort)((Devices[Memory[operand]].GetHardwareID() >> 16) & 0xFFFF);
                            X = (ushort)(Devices[Memory[operand]].GetManufacturer() & 0xFFFF);
                            Y = (ushort)((Devices[Memory[operand]].GetManufacturer() >> 16) & 0xFFFF);
                            C = Devices[Memory[operand]].GetHardwareVersion();
                        }
                        break;
                    case 0x12: // HWI
                        ConsumeCycle(3);
                        int hardwareNumber = Memory[operand];
                        if (hardwareNumber >= 0 && hardwareNumber < Devices.Length)
                            Devices[hardwareNumber].Interrupt(this);
                        break;
                    default: // unknown special instruction
                        Halted = true;
                        if (instruction != 0)
                            Console.WriteLine($"\nUnknown special instruction, op code: 0x{opCode.ToString("X2")}");
                        break;
                }
            }

            if (InstructionsToSkip == 0)
                PostInstruction();
        }

        private void ProtectedModeFault()
        {
            A = 0x5346;
            PC = IA;
            ProtectedMode = false;
            InterruptQueueingEnabled = true;
        }
        
        private bool WriteValue(int address, ushort value)
        {
            if (ProtectedMode && address < PA)
            {
                // invalid write
                ProtectedModeFault();
                return true;
            }

            Memory[address] = value;
            return false;
        }

        private bool WriteValue(int address, int value)
        {
            return WriteValue(address, (ushort)(value & 0xFFFF));
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

                if (WriteValue(--SP, PC) || WriteValue(--SP, PC))
                    return;
                ProtectedMode = false;
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
            return Memory[Memory[(int)Registers.PC]++];
        }

        private int GetOperandA(ushort instruction)
        {
            int code = (instruction >> 10) & 0x3F;
            if (code == 0x18)
            {
                return InstructionsToSkip > 0 ? SP : SP++;
            }
            else if (code > 0x1F)
            {
                Memory[InternalLiteral2] = (ushort)(code - 0x21);
                return InternalLiteral2;
            }
            else if (code == 0x1F)
            {
                Memory[InternalLiteral2] = FetchWord();
                return InternalLiteral2;
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
                case 0x08: return A;
                case 0x09: return B;
                case 0x0A: return C;
                case 0x0B: return X;
                case 0x0C: return Y;
                case 0x0D: return Z;
                case 0x0E: return I;
                case 0x0F: return J;
                case 0x10: return (A + FetchWord()) & 0xFFFF;
                case 0x11: return (B + FetchWord()) & 0xFFFF;
                case 0x12: return (C + FetchWord()) & 0xFFFF;
                case 0x13: return (X + FetchWord()) & 0xFFFF;
                case 0x14: return (Y + FetchWord()) & 0xFFFF;
                case 0x15: return (Z + FetchWord()) & 0xFFFF;
                case 0x16: return (I + FetchWord()) & 0xFFFF;
                case 0x17: return (J + FetchWord()) & 0xFFFF;
                case 0x18: return InstructionsToSkip > 0 ? SP - 1 : --SP;
                case 0x19: return SP;
                case 0x1A: return (SP + FetchWord()) & 0xFFFF;
                case 0x1B: return (int)Registers.SP;
                case 0x1C: return (int)Registers.PC;
                case 0x1D: return (int)Registers.EX;
                case 0x1E: return FetchWord();
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
            if (!InterruptQueueingEnabled && InterruptQueue.Count > 0)
                TriggerInterrupt(InterruptQueue.Dequeue());
        }

        public void DumpRegisters()
        {
            Console.WriteLine("===== DCPU-16 register dump =====");
            Console.WriteLine($"     Hex       Unsigned Signed");
            Console.WriteLine($"A =  {A.ToString("X4")}\t{A}\t{(short)A}");
            Console.WriteLine($"B =  {B.ToString("X4")}\t{B}\t{(short)B}");
            Console.WriteLine($"C =  {C.ToString("X4")}\t{C}\t{(short)C}");
            Console.WriteLine($"X =  {X.ToString("X4")}\t{X}\t{(short)X}");
            Console.WriteLine($"Y =  {Y.ToString("X4")}\t{Y}\t{(short)Y}");
            Console.WriteLine($"Z =  {Z.ToString("X4")}\t{Z}\t{(short)Z}");
            Console.WriteLine($"I =  {I.ToString("X4")}\t{I}\t{(short)I}");
            Console.WriteLine($"J =  {J.ToString("X4")}\t{J}\t{(short)J}");
            Console.WriteLine($"PC = {PC.ToString("X4")}\t{PC}\t{(short)PC}");
            Console.WriteLine($"SP = {SP.ToString("X4")}\t{SP}\t{(short)SP}");
            Console.WriteLine($"EX = {EX.ToString("X4")}\t{EX}\t{(short)EX}");
            Console.WriteLine($"IA = {IA.ToString("X4")}\t{IA}\t{(short)IA}");
            Console.WriteLine($"PA = {PA.ToString("X4")}\t{PA}\t{(short)PA}");
            Console.WriteLine("=================================");
        }

        public void Run(int radiation, int cyclesPerSecond)
        {
            Stopwatch clock = new Stopwatch();
            Random rnd = new Random((int)DateTime.Now.Ticks);
            clock.Start();

            CycleDebt = 0;
            Halted = false;

            long currentCycles = 0;
            long stdCyclesPerSecond = 100000;
            long stdCurrentCycles = 0;

            while (!Halted)
            {
                long ticks = clock.ElapsedTicks;
                long cycles = (ticks * cyclesPerSecond) / Stopwatch.Frequency;
                long passed = cycles - currentCycles;

                long stdCycles = (ticks * stdCyclesPerSecond) / Stopwatch.Frequency;
                long stdPassed = stdCycles - stdCurrentCycles;

                if (cycles > currentCycles)
                {
                    CycleDebt -= (int)passed;
                    currentCycles = cycles;
                }

                if (stdCycles > stdCurrentCycles)
                {
                    stdCurrentCycles = stdCycles;
                    for (int i = 0; i < stdPassed; i++)
                    {
                        // flip random bits depending on radiation setting
                        int randVal = rnd.Next(100000);
                        if (randVal < radiation)
                            Memory[rnd.Next(0x10000)] ^= (ushort)(1 << rnd.Next(16));
                    }
                }
                
                for (int i = 0; i < Devices.Length; i++)
                    Devices[i].UpdateInternal(this, stdPassed);

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
