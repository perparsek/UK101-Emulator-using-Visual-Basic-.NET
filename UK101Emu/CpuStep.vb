Module CpuStep

   ' ── Hjälpfunktioner ──

   Private Sub SetZN(cpu As Cpu6502, value As Byte)
      cpu.Z = (value = 0)
      cpu.N = ((value And &H80) <> 0)
   End Sub

   Private Sub Push(cpu As Cpu6502, mem As Memory, value As Byte)
      mem.Write(CUShort(&H100 Or cpu.SP), value)
      cpu.SP = CByte(cpu.SP - 1)
   End Sub

   Private Function Pull(cpu As Cpu6502, mem As Memory) As Byte
      cpu.SP = CByte(cpu.SP + 1)
      Return mem.Read(CUShort(&H100 Or cpu.SP))
   End Function

   ''' <summary>Läs signed branch-offset</summary>
   Private Function ReadBranchOffset(cpu As Cpu6502, mem As Memory) As Integer
      Dim raw As Byte = mem.Read(cpu.PC)
      cpu.PC += 1
      Return If(raw < 128, CInt(raw), CInt(raw) - 256)
   End Function

   ''' <summary>Läs 16-bit absolute adress</summary>
   Private Function ReadAbsAddr(cpu As Cpu6502, mem As Memory) As UShort
      Dim lo As Byte = mem.Read(cpu.PC) : cpu.PC += 1
      Dim hi As Byte = mem.Read(cpu.PC) : cpu.PC += 1
      Return CUShort((CUShort(hi) << 8) Or lo)
   End Function

   ''' <summary>ADC-logik (används av alla ADC-adresseringslägen)</summary>
   Private Sub DoADC(cpu As Cpu6502, value As Byte)
      Dim carryIn As Integer = If(cpu.C, 1, 0)
      Dim sum As Integer = CInt(cpu.A) + CInt(value) + carryIn
      cpu.C = (sum > 255)
      Dim result As Byte = CByte(sum And &HFF)
      cpu.V = (((cpu.A Xor result) And (value Xor result) And &H80) <> 0)
      cpu.A = result
      SetZN(cpu, cpu.A)
   End Sub

   ''' <summary>SBC-logik</summary>
   Private Sub DoSBC(cpu As Cpu6502, value As Byte)
      Dim carryIn As Integer = If(cpu.C, 0, 1)
      Dim diff As Integer = CInt(cpu.A) - CInt(value) - carryIn
      cpu.C = (diff >= 0)
      Dim result As Byte = CByte(diff And &HFF)
      cpu.V = (((cpu.A Xor result) And (cpu.A Xor value) And &H80) <> 0)
      cpu.A = result
      SetZN(cpu, cpu.A)
   End Sub

   ''' <summary>CMP-logik (jämför register med värde)</summary>
   Private Sub DoCMP(cpu As Cpu6502, reg As Byte, value As Byte)
      Dim result As Integer = CInt(reg) - CInt(value)
      cpu.C = (reg >= value)
      cpu.Z = (reg = value)
      cpu.N = ((result And &H80) <> 0)
   End Sub

   ' ════════════════════════════════════════════════════════════
   ' STEG — Exekvera en instruktion
   ' ════════════════════════════════════════════════════════════
   Public Sub StepCpu(cpu As Cpu6502, mem As Memory)

      Dim opcode As Byte = mem.Read(cpu.PC)
      cpu.PC = CUShort(cpu.PC + 1)

      Select Case opcode

         ' ══════════════════════════════════════
         ' BRK
         ' ══════════════════════════════════════
         Case &H0
            cpu.PC += 1  ' BRK skips next byte
            Push(cpu, mem, CByte((cpu.PC >> 8) And &HFF))
            Push(cpu, mem, CByte(cpu.PC And &HFF))
            Dim flags As Byte = PackFlags(cpu) Or &H10  ' B flag set
            Push(cpu, mem, flags)
            cpu.I = True
            Dim lo As Byte = mem.Read(&HFFFE)
            Dim hi As Byte = mem.Read(&HFFFF)
            cpu.PC = CUShort((CUShort(hi) << 8) Or lo)

         ' ══════════════════════════════════════
         ' ORA
         ' ══════════════════════════════════════
         Case &H9 ' ORA immediate
            cpu.A = CByte(cpu.A Or mem.Read(cpu.PC)) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &H5 ' ORA zero page
            cpu.A = CByte(cpu.A Or mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &H15 ' ORA zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            cpu.A = CByte(cpu.A Or mem.Read(zp))
            SetZN(cpu, cpu.A)
         Case &HD ' ORA absolute
            cpu.A = CByte(cpu.A Or mem.Read(ReadAbsAddr(cpu, mem)))
            SetZN(cpu, cpu.A)
         Case &H1D ' ORA absolute,X
            cpu.A = CByte(cpu.A Or mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X)))
            SetZN(cpu, cpu.A)
         Case &H19 ' ORA absolute,Y
            cpu.A = CByte(cpu.A Or mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y)))
            SetZN(cpu, cpu.A)
         Case &H1 ' ORA (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            cpu.A = CByte(cpu.A Or mem.Read(CUShort((CUShort(hi) << 8) Or lo)))
            SetZN(cpu, cpu.A)
         Case &H11 ' ORA (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            Dim addr = CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y)
            cpu.A = CByte(cpu.A Or mem.Read(addr))
            SetZN(cpu, cpu.A)

         ' ══════════════════════════════════════
         ' ASL
         ' ══════════════════════════════════════
         Case &HA ' ASL A
            cpu.C = ((cpu.A And &H80) <> 0)
            cpu.A = CByte((cpu.A << 1) And &HFF)
            SetZN(cpu, cpu.A)
         Case &H6 ' ASL zero page
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim v = mem.Read(zp)
            cpu.C = ((v And &H80) <> 0)
            v = CByte((v << 1) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &H16 ' ASL zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim v = mem.Read(zp)
            cpu.C = ((v And &H80) <> 0)
            v = CByte((v << 1) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &HE ' ASL absolute
            Dim addr = ReadAbsAddr(cpu, mem)
            Dim v = mem.Read(addr)
            cpu.C = ((v And &H80) <> 0)
            v = CByte((v << 1) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)
         Case &H1E ' ASL absolute,X
            Dim addr = CUShort(ReadAbsAddr(cpu, mem) + cpu.X)
            Dim v = mem.Read(addr)
            cpu.C = ((v And &H80) <> 0)
            v = CByte((v << 1) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)

         ' ══════════════════════════════════════
         ' BIT
         ' ══════════════════════════════════════
         Case &H24 ' BIT zero page
            Dim v = mem.Read(mem.Read(cpu.PC)) : cpu.PC += 1
            cpu.Z = ((cpu.A And v) = 0)
            cpu.N = ((v And &H80) <> 0)
            cpu.V = ((v And &H40) <> 0)
         Case &H2C ' BIT absolute
            Dim v = mem.Read(ReadAbsAddr(cpu, mem))
            cpu.Z = ((cpu.A And v) = 0)
            cpu.N = ((v And &H80) <> 0)
            cpu.V = ((v And &H40) <> 0)

         ' ══════════════════════════════════════
         ' AND
         ' ══════════════════════════════════════
         Case &H29 ' AND immediate
            cpu.A = CByte(cpu.A And mem.Read(cpu.PC)) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &H25 ' AND zero page
            cpu.A = CByte(cpu.A And mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &H35 ' AND zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            cpu.A = CByte(cpu.A And mem.Read(zp))
            SetZN(cpu, cpu.A)
         Case &H2D ' AND absolute
            cpu.A = CByte(cpu.A And mem.Read(ReadAbsAddr(cpu, mem)))
            SetZN(cpu, cpu.A)
         Case &H3D ' AND absolute,X
            cpu.A = CByte(cpu.A And mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X)))
            SetZN(cpu, cpu.A)
         Case &H39 ' AND absolute,Y
            cpu.A = CByte(cpu.A And mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y)))
            SetZN(cpu, cpu.A)
         Case &H21 ' AND (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            cpu.A = CByte(cpu.A And mem.Read(CUShort((CUShort(hi) << 8) Or lo)))
            SetZN(cpu, cpu.A)
         Case &H31 ' AND (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            cpu.A = CByte(cpu.A And mem.Read(CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y)))
            SetZN(cpu, cpu.A)

         ' ══════════════════════════════════════
         ' ROL
         ' ══════════════════════════════════════
         Case &H2A ' ROL A
            Dim oldC = If(cpu.C, 1, 0)
            cpu.C = ((cpu.A And &H80) <> 0)
            cpu.A = CByte(((cpu.A << 1) Or oldC) And &HFF)
            SetZN(cpu, cpu.A)
         Case &H26 ' ROL zero page
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim v = mem.Read(zp) : Dim oldC = If(cpu.C, 1, 0)
            cpu.C = ((v And &H80) <> 0)
            v = CByte(((v << 1) Or oldC) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &H36 ' ROL zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim v = mem.Read(zp) : Dim oldC = If(cpu.C, 1, 0)
            cpu.C = ((v And &H80) <> 0)
            v = CByte(((v << 1) Or oldC) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &H2E ' ROL absolute
            Dim addr = ReadAbsAddr(cpu, mem)
            Dim v = mem.Read(addr) : Dim oldC = If(cpu.C, 1, 0)
            cpu.C = ((v And &H80) <> 0)
            v = CByte(((v << 1) Or oldC) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)
         Case &H3E ' ROL absolute,X
            Dim addr = CUShort(ReadAbsAddr(cpu, mem) + cpu.X)
            Dim v = mem.Read(addr) : Dim oldC = If(cpu.C, 1, 0)
            cpu.C = ((v And &H80) <> 0)
            v = CByte(((v << 1) Or oldC) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)

         ' ══════════════════════════════════════
         ' EOR
         ' ══════════════════════════════════════
         Case &H49 ' EOR immediate
            cpu.A = CByte(cpu.A Xor mem.Read(cpu.PC)) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &H45 ' EOR zero page
            cpu.A = CByte(cpu.A Xor mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &H55 ' EOR zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            cpu.A = CByte(cpu.A Xor mem.Read(zp))
            SetZN(cpu, cpu.A)
         Case &H4D ' EOR absolute
            cpu.A = CByte(cpu.A Xor mem.Read(ReadAbsAddr(cpu, mem)))
            SetZN(cpu, cpu.A)
         Case &H5D ' EOR absolute,X
            cpu.A = CByte(cpu.A Xor mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X)))
            SetZN(cpu, cpu.A)
         Case &H59 ' EOR absolute,Y
            cpu.A = CByte(cpu.A Xor mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y)))
            SetZN(cpu, cpu.A)
         Case &H41 ' EOR (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            cpu.A = CByte(cpu.A Xor mem.Read(CUShort((CUShort(hi) << 8) Or lo)))
            SetZN(cpu, cpu.A)
         Case &H51 ' EOR (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            cpu.A = CByte(cpu.A Xor mem.Read(CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y)))
            SetZN(cpu, cpu.A)

         ' ══════════════════════════════════════
         ' LSR
         ' ══════════════════════════════════════
         Case &H4A ' LSR A
            cpu.C = ((cpu.A And &H1) <> 0)
            cpu.A = CByte((cpu.A >> 1) And &H7F)
            SetZN(cpu, cpu.A)
         Case &H46 ' LSR zero page
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim v = mem.Read(zp)
            cpu.C = ((v And &H1) <> 0)
            v = CByte((v >> 1) And &H7F)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &H56 ' LSR zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim v = mem.Read(zp)
            cpu.C = ((v And &H1) <> 0)
            v = CByte((v >> 1) And &H7F)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &H4E ' LSR absolute
            Dim addr = ReadAbsAddr(cpu, mem)
            Dim v = mem.Read(addr)
            cpu.C = ((v And &H1) <> 0)
            v = CByte((v >> 1) And &H7F)
            mem.Write(addr, v) : SetZN(cpu, v)
         Case &H5E ' LSR absolute,X
            Dim addr = CUShort(ReadAbsAddr(cpu, mem) + cpu.X)
            Dim v = mem.Read(addr)
            cpu.C = ((v And &H1) <> 0)
            v = CByte((v >> 1) And &H7F)
            mem.Write(addr, v) : SetZN(cpu, v)

         ' ══════════════════════════════════════
         ' ROR
         ' ══════════════════════════════════════
         Case &H6A ' ROR A
            Dim oldC = If(cpu.C, &H80, 0)
            cpu.C = ((cpu.A And &H1) <> 0)
            cpu.A = CByte(((cpu.A >> 1) Or oldC) And &HFF)
            SetZN(cpu, cpu.A)
         Case &H66 ' ROR zero page
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim v = mem.Read(zp) : Dim oldC = If(cpu.C, &H80, 0)
            cpu.C = ((v And &H1) <> 0)
            v = CByte(((v >> 1) Or oldC) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &H76 ' ROR zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim v = mem.Read(zp) : Dim oldC = If(cpu.C, &H80, 0)
            cpu.C = ((v And &H1) <> 0)
            v = CByte(((v >> 1) Or oldC) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &H6E ' ROR absolute
            Dim addr = ReadAbsAddr(cpu, mem)
            Dim v = mem.Read(addr) : Dim oldC = If(cpu.C, &H80, 0)
            cpu.C = ((v And &H1) <> 0)
            v = CByte(((v >> 1) Or oldC) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)
         Case &H7E ' ROR absolute,X
            Dim addr = CUShort(ReadAbsAddr(cpu, mem) + cpu.X)
            Dim v = mem.Read(addr) : Dim oldC = If(cpu.C, &H80, 0)
            cpu.C = ((v And &H1) <> 0)
            v = CByte(((v >> 1) Or oldC) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)

         ' ══════════════════════════════════════
         ' ADC
         ' ══════════════════════════════════════
         Case &H69 ' ADC immediate
            DoADC(cpu, mem.Read(cpu.PC)) : cpu.PC += 1
         Case &H65 ' ADC zero page
            DoADC(cpu, mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
         Case &H75 ' ADC zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            DoADC(cpu, mem.Read(zp))
         Case &H6D ' ADC absolute
            DoADC(cpu, mem.Read(ReadAbsAddr(cpu, mem)))
         Case &H7D ' ADC absolute,X
            DoADC(cpu, mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X)))
         Case &H79 ' ADC absolute,Y
            DoADC(cpu, mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y)))
         Case &H61 ' ADC (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            DoADC(cpu, mem.Read(CUShort((CUShort(hi) << 8) Or lo)))
         Case &H71 ' ADC (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            DoADC(cpu, mem.Read(CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y)))

         ' ══════════════════════════════════════
         ' SBC
         ' ══════════════════════════════════════
         Case &HE9 ' SBC immediate
            DoSBC(cpu, mem.Read(cpu.PC)) : cpu.PC += 1
         Case &HE5 ' SBC zero page
            DoSBC(cpu, mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
         Case &HF5 ' SBC zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            DoSBC(cpu, mem.Read(zp))
         Case &HED ' SBC absolute
            DoSBC(cpu, mem.Read(ReadAbsAddr(cpu, mem)))
         Case &HFD ' SBC absolute,X
            DoSBC(cpu, mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X)))
         Case &HF9 ' SBC absolute,Y
            DoSBC(cpu, mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y)))
         Case &HE1 ' SBC (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            DoSBC(cpu, mem.Read(CUShort((CUShort(hi) << 8) Or lo)))
         Case &HF1 ' SBC (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            DoSBC(cpu, mem.Read(CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y)))

         ' ══════════════════════════════════════
         ' CMP
         ' ══════════════════════════════════════
         Case &HC9 ' CMP immediate
            DoCMP(cpu, cpu.A, mem.Read(cpu.PC)) : cpu.PC += 1
         Case &HC5 ' CMP zero page
            DoCMP(cpu, cpu.A, mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
         Case &HD5 ' CMP zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            DoCMP(cpu, cpu.A, mem.Read(zp))
         Case &HCD ' CMP absolute
            DoCMP(cpu, cpu.A, mem.Read(ReadAbsAddr(cpu, mem)))
         Case &HDD ' CMP absolute,X
            DoCMP(cpu, cpu.A, mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X)))
         Case &HD9 ' CMP absolute,Y
            DoCMP(cpu, cpu.A, mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y)))
         Case &HC1 ' CMP (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            DoCMP(cpu, cpu.A, mem.Read(CUShort((CUShort(hi) << 8) Or lo)))
         Case &HD1 ' CMP (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            DoCMP(cpu, cpu.A, mem.Read(CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y)))

         ' ══════════════════════════════════════
         ' CPX
         ' ══════════════════════════════════════
         Case &HE0 ' CPX immediate
            DoCMP(cpu, cpu.X, mem.Read(cpu.PC)) : cpu.PC += 1
         Case &HE4 ' CPX zero page
            DoCMP(cpu, cpu.X, mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
         Case &HEC ' CPX absolute
            DoCMP(cpu, cpu.X, mem.Read(ReadAbsAddr(cpu, mem)))

         ' ══════════════════════════════════════
         ' CPY
         ' ══════════════════════════════════════
         Case &HC0 ' CPY immediate
            DoCMP(cpu, cpu.Y, mem.Read(cpu.PC)) : cpu.PC += 1
         Case &HC4 ' CPY zero page
            DoCMP(cpu, cpu.Y, mem.Read(mem.Read(cpu.PC))) : cpu.PC += 1
         Case &HCC ' CPY absolute
            DoCMP(cpu, cpu.Y, mem.Read(ReadAbsAddr(cpu, mem)))

         ' ══════════════════════════════════════
         ' LDA
         ' ══════════════════════════════════════
         Case &HA9 ' LDA immediate
            cpu.A = mem.Read(cpu.PC) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &HA5 ' LDA zero page
            cpu.A = mem.Read(mem.Read(cpu.PC)) : cpu.PC += 1
            SetZN(cpu, cpu.A)
         Case &HB5 ' LDA zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            cpu.A = mem.Read(zp)
            SetZN(cpu, cpu.A)
         Case &HAD ' LDA absolute
            cpu.A = mem.Read(ReadAbsAddr(cpu, mem))
            SetZN(cpu, cpu.A)
         Case &HBD ' LDA absolute,X
            cpu.A = mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X))
            SetZN(cpu, cpu.A)
         Case &HB9 ' LDA absolute,Y
            cpu.A = mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y))
            SetZN(cpu, cpu.A)
         Case &HA1 ' LDA (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            cpu.A = mem.Read(CUShort((CUShort(hi) << 8) Or lo))
            SetZN(cpu, cpu.A)
         Case &HB1 ' LDA (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            cpu.A = mem.Read(CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y))
            SetZN(cpu, cpu.A)

         ' ══════════════════════════════════════
         ' LDX
         ' ══════════════════════════════════════
         Case &HA2 ' LDX immediate
            cpu.X = mem.Read(cpu.PC) : cpu.PC += 1
            SetZN(cpu, cpu.X)
         Case &HA6 ' LDX zero page
            cpu.X = mem.Read(mem.Read(cpu.PC)) : cpu.PC += 1
            SetZN(cpu, cpu.X)
         Case &HB6 ' LDX zero page,Y
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.Y) And &HFF) : cpu.PC += 1
            cpu.X = mem.Read(zp)
            SetZN(cpu, cpu.X)
         Case &HAE ' LDX absolute
            cpu.X = mem.Read(ReadAbsAddr(cpu, mem))
            SetZN(cpu, cpu.X)
         Case &HBE ' LDX absolute,Y
            cpu.X = mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y))
            SetZN(cpu, cpu.X)

         ' ══════════════════════════════════════
         ' LDY
         ' ══════════════════════════════════════
         Case &HA0 ' LDY immediate
            cpu.Y = mem.Read(cpu.PC) : cpu.PC += 1
            SetZN(cpu, cpu.Y)
         Case &HA4 ' LDY zero page
            cpu.Y = mem.Read(mem.Read(cpu.PC)) : cpu.PC += 1
            SetZN(cpu, cpu.Y)
         Case &HB4 ' LDY zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            cpu.Y = mem.Read(zp)
            SetZN(cpu, cpu.Y)
         Case &HAC ' LDY absolute
            cpu.Y = mem.Read(ReadAbsAddr(cpu, mem))
            SetZN(cpu, cpu.Y)
         Case &HBC ' LDY absolute,X
            cpu.Y = mem.Read(CUShort(ReadAbsAddr(cpu, mem) + cpu.X))
            SetZN(cpu, cpu.Y)

         ' ══════════════════════════════════════
         ' STA
         ' ══════════════════════════════════════
         Case &H85 ' STA zero page
            mem.Write(mem.Read(cpu.PC), cpu.A) : cpu.PC += 1
         Case &H95 ' STA zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            mem.Write(zp, cpu.A)
         Case &H8D ' STA absolute
            mem.Write(ReadAbsAddr(cpu, mem), cpu.A)
         Case &H9D ' STA absolute,X
            mem.Write(CUShort(ReadAbsAddr(cpu, mem) + cpu.X), cpu.A)
         Case &H99 ' STA absolute,Y
            mem.Write(CUShort(ReadAbsAddr(cpu, mem) + cpu.Y), cpu.A)
         Case &H81 ' STA (indirect,X)
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            mem.Write(CUShort((CUShort(hi) << 8) Or lo), cpu.A)
         Case &H91 ' STA (indirect),Y
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim lo = mem.Read(zp) : Dim hi = mem.Read(CByte((zp + 1) And &HFF))
            mem.Write(CUShort(((CUShort(hi) << 8) Or lo) + cpu.Y), cpu.A)

         ' ══════════════════════════════════════
         ' STX
         ' ══════════════════════════════════════
         Case &H86 ' STX zero page
            mem.Write(mem.Read(cpu.PC), cpu.X) : cpu.PC += 1
         Case &H96 ' STX zero page,Y
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.Y) And &HFF) : cpu.PC += 1
            mem.Write(zp, cpu.X)
         Case &H8E ' STX absolute
            mem.Write(ReadAbsAddr(cpu, mem), cpu.X)

         ' ══════════════════════════════════════
         ' STY
         ' ══════════════════════════════════════
         Case &H84 ' STY zero page
            mem.Write(mem.Read(cpu.PC), cpu.Y) : cpu.PC += 1
         Case &H94 ' STY zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            mem.Write(zp, cpu.Y)
         Case &H8C ' STY absolute
            mem.Write(ReadAbsAddr(cpu, mem), cpu.Y)

         ' ══════════════════════════════════════
         ' INC / DEC memory
         ' ══════════════════════════════════════
         Case &HE6 ' INC zero page
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim v = CByte((mem.Read(zp) + 1) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &HF6 ' INC zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim v = CByte((mem.Read(zp) + 1) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &HEE ' INC absolute
            Dim addr = ReadAbsAddr(cpu, mem)
            Dim v = CByte((mem.Read(addr) + 1) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)
         Case &HFE ' INC absolute,X
            Dim addr = CUShort(ReadAbsAddr(cpu, mem) + cpu.X)
            Dim v = CByte((mem.Read(addr) + 1) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)

         Case &HC6 ' DEC zero page
            Dim zp = mem.Read(cpu.PC) : cpu.PC += 1
            Dim v = CByte((mem.Read(zp) - 1) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &HD6 ' DEC zero page,X
            Dim zp = CByte((mem.Read(cpu.PC) + cpu.X) And &HFF) : cpu.PC += 1
            Dim v = CByte((mem.Read(zp) - 1) And &HFF)
            mem.Write(zp, v) : SetZN(cpu, v)
         Case &HCE ' DEC absolute
            Dim addr = ReadAbsAddr(cpu, mem)
            Dim v = CByte((mem.Read(addr) - 1) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)
         Case &HDE ' DEC absolute,X
            Dim addr = CUShort(ReadAbsAddr(cpu, mem) + cpu.X)
            Dim v = CByte((mem.Read(addr) - 1) And &HFF)
            mem.Write(addr, v) : SetZN(cpu, v)

         ' ══════════════════════════════════════
         ' Register-instruktioner
         ' ══════════════════════════════════════
         Case &HAA ' TAX
            cpu.X = cpu.A : SetZN(cpu, cpu.X)
         Case &HA8 ' TAY
            cpu.Y = cpu.A : SetZN(cpu, cpu.Y)
         Case &H8A ' TXA
            cpu.A = cpu.X : SetZN(cpu, cpu.A)
         Case &H98 ' TYA
            cpu.A = cpu.Y : SetZN(cpu, cpu.A)
         Case &HBA ' TSX
            cpu.X = cpu.SP : SetZN(cpu, cpu.X)
         Case &H9A ' TXS
            cpu.SP = cpu.X

         Case &HE8 ' INX
            cpu.X = CByte((cpu.X + 1) And &HFF) : SetZN(cpu, cpu.X)
         Case &HC8 ' INY
            cpu.Y = CByte((cpu.Y + 1) And &HFF) : SetZN(cpu, cpu.Y)
         Case &HCA ' DEX
            cpu.X = CByte((cpu.X - 1) And &HFF) : SetZN(cpu, cpu.X)
         Case &H88 ' DEY
            cpu.Y = CByte((cpu.Y - 1) And &HFF) : SetZN(cpu, cpu.Y)

         ' ══════════════════════════════════════
         ' Stack
         ' ══════════════════════════════════════
         Case &H48 ' PHA
            Push(cpu, mem, cpu.A)
         Case &H68 ' PLA
            cpu.A = Pull(cpu, mem) : SetZN(cpu, cpu.A)
         Case &H8 ' PHP
            Push(cpu, mem, CByte(PackFlags(cpu) Or &H10))  ' B flag set
         Case &H28 ' PLP
            UnpackFlags(cpu, Pull(cpu, mem))

         ' ══════════════════════════════════════
         ' Branching
         ' ══════════════════════════════════════
         Case &H10 ' BPL
            Dim offset = ReadBranchOffset(cpu, mem)
            If Not cpu.N Then cpu.PC = CUShort(cpu.PC + offset)
         Case &H30 ' BMI
            Dim offset = ReadBranchOffset(cpu, mem)
            If cpu.N Then cpu.PC = CUShort(cpu.PC + offset)
         Case &H50 ' BVC
            Dim offset = ReadBranchOffset(cpu, mem)
            If Not cpu.V Then cpu.PC = CUShort(cpu.PC + offset)
         Case &H70 ' BVS
            Dim offset = ReadBranchOffset(cpu, mem)
            If cpu.V Then cpu.PC = CUShort(cpu.PC + offset)
         Case &H90 ' BCC
            Dim offset = ReadBranchOffset(cpu, mem)
            If Not cpu.C Then cpu.PC = CUShort(cpu.PC + offset)
         Case &HB0 ' BCS
            Dim offset = ReadBranchOffset(cpu, mem)
            If cpu.C Then cpu.PC = CUShort(cpu.PC + offset)
         Case &HD0 ' BNE
            Dim offset = ReadBranchOffset(cpu, mem)
            If Not cpu.Z Then cpu.PC = CUShort(cpu.PC + offset)
         Case &HF0 ' BEQ
            Dim offset = ReadBranchOffset(cpu, mem)
            If cpu.Z Then cpu.PC = CUShort(cpu.PC + offset)

         ' ══════════════════════════════════════
         ' Flaggor
         ' ══════════════════════════════════════
         Case &H18 ' CLC
            cpu.C = False
         Case &H38 ' SEC
            cpu.C = True
         Case &H58 ' CLI
            cpu.I = False
         Case &H78 ' SEI
            cpu.I = True
         Case &HB8 ' CLV
            cpu.V = False
         Case &HD8 ' CLD
            cpu.D = False
         Case &HF8 ' SED
            cpu.D = True

         ' ══════════════════════════════════════
         ' Jump / Call
         ' ══════════════════════════════════════
         Case &H4C ' JMP absolute
            cpu.PC = ReadAbsAddr(cpu, mem)

         Case &H6C ' JMP indirect (med 6502-bugg: page wrap)
            Dim lo As Byte = mem.Read(cpu.PC) : cpu.PC += 1
            Dim hi As Byte = mem.Read(cpu.PC) : cpu.PC += 1
            Dim ptr As UShort = CUShort((CUShort(hi) << 8) Or lo)
            Dim targetLo = mem.Read(ptr)
            ' 6502 page-boundary bug: om lo=$FF → hi läses från xx00 istället för nästa sida
            Dim hiAddr As UShort = CUShort((ptr And &HFF00) Or ((ptr + 1) And &HFF))
            Dim targetHi = mem.Read(hiAddr)
            cpu.PC = CUShort((CUShort(targetHi) << 8) Or targetLo)

         Case &H20 ' JSR absolute
            Dim target = ReadAbsAddr(cpu, mem)
            Dim returnAddr As UShort = CUShort(cpu.PC - 1)
            Push(cpu, mem, CByte((returnAddr >> 8) And &HFF))
            Push(cpu, mem, CByte(returnAddr And &HFF))
            cpu.PC = target

         Case &H60 ' RTS
            Dim lo As Byte = Pull(cpu, mem)
            Dim hi As Byte = Pull(cpu, mem)
            cpu.PC = CUShort(((CUShort(hi) << 8) Or lo) + 1)

         Case &H40 ' RTI
            UnpackFlags(cpu, Pull(cpu, mem))
            Dim lo As Byte = Pull(cpu, mem)
            Dim hi As Byte = Pull(cpu, mem)
            cpu.PC = CUShort((CUShort(hi) << 8) Or lo)

         ' ══════════════════════════════════════
         ' NOP
         ' ══════════════════════════════════════
         Case &HEA ' NOP
            ' Do nothing

         Case Else
            Throw New Exception($"Unknown opcode: {Hex(opcode)} at PC={Hex(CUShort(cpu.PC - 1))}")

      End Select

   End Sub

   ' ── Pack/Unpack status-flaggor (P-register) ──

   Private Function PackFlags(cpu As Cpu6502) As Byte
      Dim p As Byte = &H20  ' bit 5 alltid 1
      If cpu.C Then p = p Or &H1
      If cpu.Z Then p = p Or &H2
      If cpu.I Then p = p Or &H4
      If cpu.D Then p = p Or &H8
      If cpu.V Then p = p Or &H40
      If cpu.N Then p = p Or &H80
      Return p
   End Function

   Private Sub UnpackFlags(cpu As Cpu6502, p As Byte)
      cpu.C = ((p And &H1) <> 0)
      cpu.Z = ((p And &H2) <> 0)
      cpu.I = ((p And &H4) <> 0)
      cpu.D = ((p And &H8) <> 0)
      cpu.V = ((p And &H40) <> 0)
      cpu.N = ((p And &H80) <> 0)
   End Sub

End Module
