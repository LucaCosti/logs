<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form esegue l'override del metodo Dispose per pulire l'elenco dei componenti.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Richiesto da Progettazione Windows Form
    Private components As System.ComponentModel.IContainer

    'NOTA: la procedura che segue è richiesta da Progettazione Windows Form
    'Può essere modificata in Progettazione Windows Form.  
    'Non modificarla nell'editor del codice.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.components = New System.ComponentModel.Container()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Button2 = New System.Windows.Forms.Button()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.TextBox1 = New System.Windows.Forms.TextBox()
        Me.lbDebug = New System.Windows.Forms.ListBox()
        Me.OpenFileDialog1 = New System.Windows.Forms.OpenFileDialog()
        Me.ProgressBar1 = New System.Windows.Forms.ProgressBar()
        Me.Button1 = New System.Windows.Forms.Button()
        Me.Timer1 = New System.Windows.Forms.Timer(Me.components)
        Me.Button3 = New System.Windows.Forms.Button()
        Me.GroupBox1 = New System.Windows.Forms.GroupBox()
        Me.lblFirmwareVersion = New System.Windows.Forms.Label()
        Me.Label9 = New System.Windows.Forms.Label()
        Me.lblBoardStatus = New System.Windows.Forms.Label()
        Me.Label7 = New System.Windows.Forms.Label()
        Me.TabControl1 = New System.Windows.Forms.TabControl()
        Me.TabPage1 = New System.Windows.Forms.TabPage()
        Me.TabPage2 = New System.Windows.Forms.TabPage()
        Me.Label6 = New System.Windows.Forms.Label()
        Me.pbDwinProgress = New System.Windows.Forms.ProgressBar()
        Me.btnUploadDwin = New System.Windows.Forms.Button()
        Me.btnSelectZip = New System.Windows.Forms.Button()
        Me.txtDwinZipPath = New System.Windows.Forms.TextBox()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.PictureBox1 = New System.Windows.Forms.PictureBox()
        Me.GroupBox1.SuspendLayout()
        Me.TabControl1.SuspendLayout()
        Me.TabPage1.SuspendLayout()
        Me.TabPage2.SuspendLayout()
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Label4
        '
        Me.Label4.AutoSize = True
        Me.Label4.BackColor = System.Drawing.Color.Transparent
        Me.Label4.ForeColor = System.Drawing.Color.Black
        Me.Label4.Location = New System.Drawing.Point(7, 185)
        Me.Label4.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(51, 13)
        Me.Label4.TabIndex = 17
        Me.Label4.Text = "Loading: "
        '
        'Label3
        '
        Me.Label3.AutoSize = True
        Me.Label3.BackColor = System.Drawing.Color.Transparent
        Me.Label3.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label3.ForeColor = System.Drawing.Color.Black
        Me.Label3.Location = New System.Drawing.Point(4, 398)
        Me.Label3.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(46, 20)
        Me.Label3.TabIndex = 16
        Me.Label3.Text = "LOG"
        '
        'Button2
        '
        Me.Button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.Button2.Location = New System.Drawing.Point(100, 104)
        Me.Button2.Margin = New System.Windows.Forms.Padding(4)
        Me.Button2.Name = "Button2"
        Me.Button2.Size = New System.Drawing.Size(300, 75)
        Me.Button2.TabIndex = 15
        Me.Button2.Text = "FLASH FIRMWARE (Ctrl + A)"
        Me.Button2.UseMnemonic = False
        Me.Button2.UseVisualStyleBackColor = True
        '
        'Label2
        '
        Me.Label2.AutoSize = True
        Me.Label2.BackColor = System.Drawing.Color.Transparent
        Me.Label2.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label2.ForeColor = System.Drawing.Color.Black
        Me.Label2.Location = New System.Drawing.Point(3, 60)
        Me.Label2.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(284, 20)
        Me.Label2.TabIndex = 14
        Me.Label2.Text = "STEP 2 - Press button to flash FW"
        '
        'Label1
        '
        Me.Label1.AutoSize = True
        Me.Label1.BackColor = System.Drawing.Color.Transparent
        Me.Label1.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Label1.ForeColor = System.Drawing.Color.Black
        Me.Label1.Location = New System.Drawing.Point(0, 3)
        Me.Label1.Margin = New System.Windows.Forms.Padding(4, 0, 4, 0)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(247, 20)
        Me.Label1.TabIndex = 13
        Me.Label1.Text = "STEP 1 - Search .hex/.nex file"
        '
        'TextBox1
        '
        Me.TextBox1.Location = New System.Drawing.Point(43, 31)
        Me.TextBox1.Margin = New System.Windows.Forms.Padding(4)
        Me.TextBox1.Name = "TextBox1"
        Me.TextBox1.Size = New System.Drawing.Size(357, 20)
        Me.TextBox1.TabIndex = 12
        '
        'lbDebug
        '
        Me.lbDebug.BackColor = System.Drawing.Color.WhiteSmoke
        Me.lbDebug.FormattingEnabled = True
        Me.lbDebug.Location = New System.Drawing.Point(6, 379)
        Me.lbDebug.Margin = New System.Windows.Forms.Padding(4)
        Me.lbDebug.Name = "lbDebug"
        Me.lbDebug.Size = New System.Drawing.Size(526, 238)
        Me.lbDebug.TabIndex = 11
        '
        'OpenFileDialog1
        '
        Me.OpenFileDialog1.Filter = "Nex files (*.nex)|*.nex*|Hex files (*.hex*)|*.hex*"
        '
        'ProgressBar1
        '
        Me.ProgressBar1.Location = New System.Drawing.Point(43, 216)
        Me.ProgressBar1.Margin = New System.Windows.Forms.Padding(4)
        Me.ProgressBar1.Name = "ProgressBar1"
        Me.ProgressBar1.Size = New System.Drawing.Size(357, 29)
        Me.ProgressBar1.TabIndex = 10
        '
        'Button1
        '
        Me.Button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.Button1.Location = New System.Drawing.Point(417, 26)
        Me.Button1.Margin = New System.Windows.Forms.Padding(4)
        Me.Button1.Name = "Button1"
        Me.Button1.Size = New System.Drawing.Size(94, 29)
        Me.Button1.TabIndex = 9
        Me.Button1.Text = "Search.."
        Me.Button1.UseCompatibleTextRendering = True
        Me.Button1.UseVisualStyleBackColor = True
        '
        'Timer1
        '
        Me.Timer1.Interval = 1000
        '
        'Button3
        '
        Me.Button3.Enabled = False
        Me.Button3.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.Button3.Location = New System.Drawing.Point(417, 215)
        Me.Button3.Margin = New System.Windows.Forms.Padding(4)
        Me.Button3.Name = "Button3"
        Me.Button3.Size = New System.Drawing.Size(94, 29)
        Me.Button3.TabIndex = 20
        Me.Button3.Text = "Stop"
        Me.Button3.UseVisualStyleBackColor = True
        '
        'GroupBox1
        '
        Me.GroupBox1.Controls.Add(Me.lblFirmwareVersion)
        Me.GroupBox1.Controls.Add(Me.Label9)
        Me.GroupBox1.Controls.Add(Me.lblBoardStatus)
        Me.GroupBox1.Controls.Add(Me.Label7)
        Me.GroupBox1.Controls.Add(Me.TabControl1)
        Me.GroupBox1.Controls.Add(Me.Label3)
        Me.GroupBox1.Location = New System.Drawing.Point(6, 74)
        Me.GroupBox1.Margin = New System.Windows.Forms.Padding(4)
        Me.GroupBox1.Name = "GroupBox1"
        Me.GroupBox1.Padding = New System.Windows.Forms.Padding(4)
        Me.GroupBox1.Size = New System.Drawing.Size(526, 565)
        Me.GroupBox1.TabIndex = 21
        Me.GroupBox1.TabStop = False
        '
        'lblFirmwareVersion
        '
        Me.lblFirmwareVersion.AutoSize = True
        Me.lblFirmwareVersion.Location = New System.Drawing.Point(359, 0)
        Me.lblFirmwareVersion.Name = "lblFirmwareVersion"
        Me.lblFirmwareVersion.Size = New System.Drawing.Size(28, 13)
        Me.lblFirmwareVersion.TabIndex = 27
        Me.lblFirmwareVersion.Text = "N/D"
        '
        'Label9
        '
        Me.Label9.AutoSize = True
        Me.Label9.Location = New System.Drawing.Point(265, 0)
        Me.Label9.Name = "Label9"
        Me.Label9.Size = New System.Drawing.Size(89, 13)
        Me.Label9.TabIndex = 26
        Me.Label9.Text = "Firmware version:"
        '
        'lblBoardStatus
        '
        Me.lblBoardStatus.AutoSize = True
        Me.lblBoardStatus.Font = New System.Drawing.Font("Microsoft Sans Serif", 8.25!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblBoardStatus.ForeColor = System.Drawing.Color.FromArgb(CType(CType(192, Byte), Integer), CType(CType(0, Byte), Integer), CType(CType(0, Byte), Integer))
        Me.lblBoardStatus.Location = New System.Drawing.Point(79, 0)
        Me.lblBoardStatus.Name = "lblBoardStatus"
        Me.lblBoardStatus.Size = New System.Drawing.Size(113, 13)
        Me.lblBoardStatus.TabIndex = 25
        Me.lblBoardStatus.Text = "NOT CONNECTED"
        '
        'Label7
        '
        Me.Label7.AutoSize = True
        Me.Label7.Location = New System.Drawing.Point(5, 0)
        Me.Label7.Name = "Label7"
        Me.Label7.Size = New System.Drawing.Size(68, 13)
        Me.Label7.TabIndex = 24
        Me.Label7.Text = "Board Status"
        '
        'TabControl1
        '
        Me.TabControl1.Controls.Add(Me.TabPage1)
        Me.TabControl1.Controls.Add(Me.TabPage2)
        Me.TabControl1.Location = New System.Drawing.Point(0, 20)
        Me.TabControl1.Name = "TabControl1"
        Me.TabControl1.SelectedIndex = 0
        Me.TabControl1.Size = New System.Drawing.Size(526, 284)
        Me.TabControl1.TabIndex = 23
        '
        'TabPage1
        '
        Me.TabPage1.Controls.Add(Me.Label4)
        Me.TabPage1.Controls.Add(Me.Label1)
        Me.TabPage1.Controls.Add(Me.Button3)
        Me.TabPage1.Controls.Add(Me.TextBox1)
        Me.TabPage1.Controls.Add(Me.ProgressBar1)
        Me.TabPage1.Controls.Add(Me.Button1)
        Me.TabPage1.Controls.Add(Me.Button2)
        Me.TabPage1.Controls.Add(Me.Label2)
        Me.TabPage1.Location = New System.Drawing.Point(4, 22)
        Me.TabPage1.Name = "TabPage1"
        Me.TabPage1.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage1.Size = New System.Drawing.Size(518, 258)
        Me.TabPage1.TabIndex = 0
        Me.TabPage1.Text = "Firmware Uploader"
        Me.TabPage1.UseVisualStyleBackColor = True
        '
        'TabPage2
        '
        Me.TabPage2.Controls.Add(Me.Label6)
        Me.TabPage2.Controls.Add(Me.pbDwinProgress)
        Me.TabPage2.Controls.Add(Me.btnUploadDwin)
        Me.TabPage2.Controls.Add(Me.btnSelectZip)
        Me.TabPage2.Controls.Add(Me.txtDwinZipPath)
        Me.TabPage2.Controls.Add(Me.Label5)
        Me.TabPage2.Location = New System.Drawing.Point(4, 22)
        Me.TabPage2.Name = "TabPage2"
        Me.TabPage2.Padding = New System.Windows.Forms.Padding(3)
        Me.TabPage2.Size = New System.Drawing.Size(518, 258)
        Me.TabPage2.TabIndex = 1
        Me.TabPage2.Text = "DWIN Uploader"
        Me.TabPage2.UseVisualStyleBackColor = True
        '
        'Label6
        '
        Me.Label6.AutoSize = True
        Me.Label6.Location = New System.Drawing.Point(6, 172)
        Me.Label6.Name = "Label6"
        Me.Label6.Size = New System.Drawing.Size(48, 13)
        Me.Label6.TabIndex = 18
        Me.Label6.Text = "Loading:"
        '
        'pbDwinProgress
        '
        Me.pbDwinProgress.Location = New System.Drawing.Point(45, 200)
        Me.pbDwinProgress.Name = "pbDwinProgress"
        Me.pbDwinProgress.Size = New System.Drawing.Size(427, 23)
        Me.pbDwinProgress.TabIndex = 17
        '
        'btnUploadDwin
        '
        Me.btnUploadDwin.Enabled = False
        Me.btnUploadDwin.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnUploadDwin.Location = New System.Drawing.Point(110, 74)
        Me.btnUploadDwin.Margin = New System.Windows.Forms.Padding(4)
        Me.btnUploadDwin.Name = "btnUploadDwin"
        Me.btnUploadDwin.Size = New System.Drawing.Size(300, 75)
        Me.btnUploadDwin.TabIndex = 16
        Me.btnUploadDwin.Text = "UPLOAD FILES TO DISPLAY"
        Me.btnUploadDwin.UseMnemonic = False
        Me.btnUploadDwin.UseVisualStyleBackColor = True
        '
        'btnSelectZip
        '
        Me.btnSelectZip.Location = New System.Drawing.Point(440, 19)
        Me.btnSelectZip.Name = "btnSelectZip"
        Me.btnSelectZip.Size = New System.Drawing.Size(72, 20)
        Me.btnSelectZip.TabIndex = 2
        Me.btnSelectZip.Text = "Search"
        Me.btnSelectZip.UseVisualStyleBackColor = True
        '
        'txtDwinZipPath
        '
        Me.txtDwinZipPath.Location = New System.Drawing.Point(134, 19)
        Me.txtDwinZipPath.Name = "txtDwinZipPath"
        Me.txtDwinZipPath.ReadOnly = True
        Me.txtDwinZipPath.Size = New System.Drawing.Size(276, 20)
        Me.txtDwinZipPath.TabIndex = 1
        '
        'Label5
        '
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(6, 22)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(122, 13)
        Me.Label5.TabIndex = 0
        Me.Label5.Text = "Select DWIN resources:"
        '
        'PictureBox1
        '
        Me.PictureBox1.Image = Global.IDE_firmware_uploader.My.Resources.Resources.Logo_RGB_HOR
        Me.PictureBox1.Location = New System.Drawing.Point(157, 13)
        Me.PictureBox1.Margin = New System.Windows.Forms.Padding(4)
        Me.PictureBox1.Name = "PictureBox1"
        Me.PictureBox1.Size = New System.Drawing.Size(225, 42)
        Me.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage
        Me.PictureBox1.TabIndex = 22
        Me.PictureBox1.TabStop = False
        '
        'Form1
        '
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit
        Me.BackColor = System.Drawing.Color.White
        Me.ClientSize = New System.Drawing.Size(538, 646)
        Me.Controls.Add(Me.PictureBox1)
        Me.Controls.Add(Me.lbDebug)
        Me.Controls.Add(Me.GroupBox1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle
        Me.KeyPreview = True
        Me.Margin = New System.Windows.Forms.Padding(4)
        Me.Name = "Form1"
        Me.Text = "Next Solution - Firmware uploader V. 3.0.8"
        Me.GroupBox1.ResumeLayout(False)
        Me.GroupBox1.PerformLayout()
        Me.TabControl1.ResumeLayout(False)
        Me.TabPage1.ResumeLayout(False)
        Me.TabPage1.PerformLayout()
        Me.TabPage2.ResumeLayout(False)
        Me.TabPage2.PerformLayout()
        CType(Me.PictureBox1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)

    End Sub
    Friend WithEvents Label4 As System.Windows.Forms.Label
    Friend WithEvents Label3 As System.Windows.Forms.Label
    Friend WithEvents Button2 As System.Windows.Forms.Button
    Friend WithEvents Label2 As System.Windows.Forms.Label
    Friend WithEvents Label1 As System.Windows.Forms.Label
    Friend WithEvents TextBox1 As System.Windows.Forms.TextBox
    Friend WithEvents lbDebug As System.Windows.Forms.ListBox
    Friend WithEvents OpenFileDialog1 As System.Windows.Forms.OpenFileDialog
    Friend WithEvents ProgressBar1 As System.Windows.Forms.ProgressBar
    Friend WithEvents Button1 As System.Windows.Forms.Button
    Public WithEvents Timer1 As System.Windows.Forms.Timer
    Friend WithEvents Button3 As System.Windows.Forms.Button
    Friend WithEvents GroupBox1 As System.Windows.Forms.GroupBox
    Friend WithEvents PictureBox1 As System.Windows.Forms.PictureBox
    Friend WithEvents TabControl1 As TabControl
    Friend WithEvents TabPage1 As TabPage
    Friend WithEvents TabPage2 As TabPage
    Friend WithEvents Label5 As Label
    Friend WithEvents btnSelectZip As Button
    Friend WithEvents txtDwinZipPath As TextBox
    Friend WithEvents btnUploadDwin As Button
    Friend WithEvents Label6 As Label
    Friend WithEvents pbDwinProgress As ProgressBar
    Friend WithEvents lblBoardStatus As Label
    Friend WithEvents Label7 As Label
    Friend WithEvents lblFirmwareVersion As Label
    Friend WithEvents Label9 As Label
End Class
