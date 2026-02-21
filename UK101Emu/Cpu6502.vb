Public Class Cpu6502

   ' =========================
   ' REGISTERS
   ' =========================

   Public A As Byte
   Public X As Byte
   Public Y As Byte

   Public PC As UShort
   Public SP As Byte

   ' =========================
   ' FLAGS
   ' =========================

   Public C As Boolean
   Public Z As Boolean
   Public I As Boolean
   Public D As Boolean
   Public B As Boolean
   Public V As Boolean
   Public N As Boolean

   ' =========================
   ' RESET
   ' =========================

   Public Sub Reset(mem As Memory)

      ' Stack pointer enligt 6502 reset
      SP = &HFD

      ' Nollställ register
      A = 0
      X = 0
      Y = 0

      ' Nollställ flaggor
      C = False
      Z = False
      I = False
      D = False
      B = False
      V = False
      N = False

      ' ===== Läs reset-vektor =====
      Dim lo As Byte = mem.Read(&HFFFC)
      Dim hi As Byte = mem.Read(&HFFFD)

      ' VIKTIGT: CInt innan shift
      PC = CUShort((CInt(hi) << 8) Or lo)

   End Sub

End Class
