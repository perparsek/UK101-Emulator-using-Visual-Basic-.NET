Public Class Memory

   Private _memory(65535) As Byte
   Public Property Terminal As TerminalForm

   Private _keyBuffer As Byte = 0
   Private _keyReady As Boolean = False

   ' =========================
   ' READ — ACIA 6850 emulering
   '   F000 = Status register (bit 0 = RxDataReady, bit 1 = TxReady)
   '   F001 = Data register
   ' =========================
   Public Function Read(addr As UShort) As Byte

      ' F000 = Status register
      If addr = &HF000 Then
         Dim status As Byte = &H02      ' bit 1 = Tx alltid redo
         If _keyReady Then
            status = status Or &H01     ' bit 0 = Rx data finns
         End If
         Return status
      End If

      ' F001 = Data register (läs tangent)
      If addr = &HF001 Then
         Dim k = _keyBuffer
         _keyBuffer = 0
         _keyReady = False
         Return k
      End If

      Return _memory(addr)

   End Function

   ' =========================
   ' WRITE
   ' =========================
   Public Sub Write(addr As UShort, value As Byte)

      ' ---- I/O PORTS ----

      ' F000 = ACIA control register (ignorera)
      If addr = &HF000 Then
         Return
      End If

      ' F001 = Data register (skriv tecken)
      If addr = &HF001 Then
         If Terminal IsNot Nothing Then
            Terminal.WriteChar(Chr(value And &H7F))   ' 7-bit ASCII
         End If
         Return
      End If

      ' ---- ROM SKYDD ----

      ' ROM ligger 8000–FFFF
      If addr >= &H8000 Then
         Return
      End If

      _memory(addr) = value

   End Sub

   ' =========================
   ' LOAD ROM
   ' =========================
   Public Sub LoadRom(start As UShort, rom() As Byte)

      For i = 0 To rom.Length - 1
         _memory(start + i) = rom(i)
      Next

   End Sub

   ' =========================
   ' KEY INPUT
   ' =========================
   Public Sub SetKey(value As Byte)
      _keyBuffer = value
      _keyReady = True
   End Sub

End Class
