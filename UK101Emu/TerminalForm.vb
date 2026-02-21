Imports System.IO
Imports System.Threading
Imports System.Diagnostics

Public Class TerminalForm

   Private memory As Memory
   Private cpu As Cpu6502
   Private running As Boolean = False
   Private cpuThread As Thread
   Private txtTerminal As RichTextBox

   Private Sub TerminalForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load

      Me.KeyPreview = True
      Me.DoubleBuffered = True
      Me.BackColor = Color.Black
      Me.ForeColor = Color.Lime
      Me.Font = New Font("Consolas", 14)

      ' Terminal-textbox
      txtTerminal = New RichTextBox() With {
         .Dock = DockStyle.Fill,
         .BackColor = Color.Black,
         .ForeColor = Color.Lime,
         .Font = New Font("Consolas", 14),
         .ReadOnly = True,
         .BorderStyle = BorderStyle.None,
         .WordWrap = False
      }
      Me.Controls.Add(txtTerminal)

      Debug.WriteLine("===== START =====")

      memory = New Memory()
      cpu = New Cpu6502()

      memory.Terminal = Me

      ' =========================
      ' LOAD 32K ROM @ 8000
      ' =========================
      Dim romPath As String = Path.Combine(Application.StartupPath, "all.rom")

      If Not File.Exists(romPath) Then
         MessageBox.Show("ROM saknas: " & romPath)
         Return
      End If

      Dim rom As Byte() = File.ReadAllBytes(romPath)

      Debug.WriteLine("ROM length = " & rom.Length)

      memory.LoadRom(&H8000, rom)

      Debug.WriteLine("Reset vector low = " & Hex(memory.Read(&HFFFC)))
      Debug.WriteLine("Reset vector high = " & Hex(memory.Read(&HFFFD)))

      ' =========================
      ' RESET CPU
      ' =========================
      cpu.Reset(memory)

      Debug.WriteLine("Start PC = " & Hex(cpu.PC))

      Me.Text = "PC=" & Hex(cpu.PC)

      ' =========================
      ' START CPU THREAD
      ' =========================
      running = True
      cpuThread = New Thread(AddressOf RunCpu)
      cpuThread.IsBackground = True
      cpuThread.Start()

   End Sub


   Private Sub RunCpu()

      Const TARGET_HZ As Long = 1000000        ' 1 MHz
      Const BATCH As Integer = 500              ' instruktioner per tidskontroll
      Dim TICKS_PER_SEC As Long = Stopwatch.Frequency

      Dim sw As New Stopwatch()
      sw.Start()

      Dim totalCycles As Long = 0

      While running

         ' Kör en batch instruktioner
         For i = 1 To BATCH
            CpuStep.StepCpu(cpu, memory)
            totalCycles += 4    ' snittcykler per instruktion (~4 på 6502)
         Next

         ' Beräkna hur lång tid vi borde ha tagit
         Dim expectedTicks As Long = CLng((totalCycles / CDbl(TARGET_HZ)) * TICKS_PER_SEC)
         Dim actualTicks As Long = sw.ElapsedTicks

         ' Vänta om vi kör för snabbt (spin-wait för precision)
         If actualTicks < expectedTicks Then
            Dim waitUntil = expectedTicks
            Do While sw.ElapsedTicks < waitUntil
               ' Yield till andra trådar om vi har >2ms kvar
               If (waitUntil - sw.ElapsedTicks) > (TICKS_PER_SEC \ 500) Then
                  Thread.Sleep(0)
               End If
            Loop
         End If

      End While

   End Sub


   ' =========================
   ' OUTPUT
   ' =========================
   Public Sub WriteChar(c As Char)

      If Me.InvokeRequired Then
         Me.BeginInvoke(Sub() WriteChar(c))
         Return
      End If

      txtTerminal.AppendText(c)

      ' Auto-scroll till botten
      txtTerminal.SelectionStart = txtTerminal.TextLength
      txtTerminal.ScrollToCaret()

   End Sub


   ' =========================
   ' KEY INPUT
   ' =========================
   Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)

      memory.SetKey(CByte(Asc(e.KeyChar)))

      MyBase.OnKeyPress(e)

   End Sub


   Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
      running = False
      MyBase.OnFormClosing(e)
   End Sub

End Class
