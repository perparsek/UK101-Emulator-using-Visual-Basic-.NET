<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class TerminalForm
   Inherits System.Windows.Forms.Form

   'Form overrides dispose to clean up the component list.
   <System.Diagnostics.DebuggerNonUserCode()>
   Protected Overrides Sub Dispose(disposing As Boolean)
      Try
         If disposing AndAlso components IsNot Nothing Then
            components.Dispose()
         End If
      Finally
         MyBase.Dispose(disposing)
      End Try
   End Sub

   'Required by the Windows Form Designer
   Private components As System.ComponentModel.IContainer

   'NOTE: The following procedure is required by the Windows Form Designer
   'It can be modified using the Windows Form Designer.
   'Do not modify it using the code editor.
   <System.Diagnostics.DebuggerStepThrough()>
   Private Sub InitializeComponent()
      SuspendLayout()
      ' 
      ' TerminalForm
      ' 
      AutoScaleDimensions = New SizeF(13F, 32F)
      AutoScaleMode = AutoScaleMode.Font
      ClientSize = New Size(800, 450)
      Name = "TerminalForm"
      Text = "Compukit UK101"
      ResumeLayout(False)
   End Sub

End Class
