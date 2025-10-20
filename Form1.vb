Imports System.IO
Imports System.Text
Imports System.BitConverter
Imports System.Threading
Imports IDE_firmware_uploader.Peak.Can.Basic
Imports IDE_firmware_uploader.Line
Imports TPCANHandle = System.Byte
Imports System.IO.Compression


Public Class Form1
#Region "Variabili globali"
    Public Firmware(-1) As Byte

    Public Line(-1) As String
    Public ToSend(-1) As Line
    Public DimFw As Integer
    Public NPacket As Integer

    Public NByteForControl As Integer
    Public NByteForData As Integer

    'VARIABILI GLOBALI PER GESTIONE PACCHETTI INVIATI E RICEVUTI
    Public Result As Boolean
    Public NPacchetto As Integer
    Public ByteLeft As Integer
    Public strDimFw As String
    Public strNpacket As String

    'CONST
    Public Const _NPACKET = 0
    Public Const _SIZEFW_HIGH = 1
    Public Const _SIZEFW_LOW = 2
    Public Const _NPACKETSRX_HIGH = 3
    Public Const _NPACKETSRX_LOW = 4
    Public Const _CHECKSTART = 7
    Public Const _MAXATTEMPT = 10
#End Region

#Region "Variabili CAN"
    Public CAN_ID As String = "306"
    Public CAN_Connected As Boolean = False
    Public CAN_Handle As TPCANHandle
    Public CAN_HandleID As String = "51"
    Public CAN_Baudrate As TPCANBaudrate = TPCANBaudrate.PCAN_BAUD_1M
    'Public CAN_HwType As TPCANType = TPCANType.PCAN_TYPE_ISA
    'Public CAN_IOport As String = "0100"
    'Public CAN_Interrupt As String = "3"
    ' Impostazioni corrette per un adattatore PCAN-USB
    Public CAN_HwType As TPCANType = CType(&H5, TPCANType)
    Public CAN_IOport As String = "0" ' Per USB, IO Port è 0
    Public CAN_Interrupt As String = "0" ' Per USB, Interrupt è 0

    Public CAN_MsgLength As Integer = 8
    Public CAN_Timeout As Integer = 5000
    Public CAN_MsgExtended As Boolean = False
    Public CAN_TmpMsg(7) As Byte
    Public CAN_ReceiveMsg(7) As Byte  'In questa variabile ci mettiamo i byte ricevuti dal micro dal thread Thread_Receive
    Public CAN_CycleTime As Integer
    Public CAN_Attempt As Integer
    Public Const CAN_ID_DWIN_COMMAND As String = "500"   ' ID per inviare comandi F0, F1, etc.
    Public Const CAN_ID_DWIN_RESPONSE As String = "600"  ' ID per RICEVERE ACK
    Public Const CAN_ID_DWIN_HEARTBEAT As String = "604" ' ID da ascoltare per lo stato della scheda

    Public CAN_Send_Th As Thread
    Public CAN_Receive_Th As Thread
    Public isCANTalking As Boolean = False
#End Region


#Region "Helpers"
    Private Function UnzipToTempFolder(ByVal zipPath As String) As String
        Try
            ' Crea un nome di cartella unico nella directory temporanea del sistema
            Dim tempFolderPath As String = Path.Combine(Path.GetTempPath(), "DWIN_Upload_" & Guid.NewGuid().ToString())

            ' Se per qualche motivo la cartella esiste già, la eliminiamo per una estrazione pulita
            If Directory.Exists(tempFolderPath) Then
                Directory.Delete(tempFolderPath, True)
            End If

            ' Estrai il contenuto del file ZIP nella cartella temporanea
            ZipFile.ExtractToDirectory(zipPath, tempFolderPath)

            Debug($"File '{Path.GetFileName(zipPath)}' successfully unzipped to: {tempFolderPath}")
            Return tempFolderPath

        Catch ex As Exception
            Debug($"ERROR during unzip process: {ex.Message}")
            MessageBox.Show($"Error while unzipping the ZIP file: {ex.Message}", "Unzip Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return Nothing
        End Try
    End Function


#Region "Base functions"
    Public Sub Debug(ByVal str As String)
        lbDebugAdd(lbDebug, "[" & Date.Now.ToString & "] : " & str)
        lbDebugIndex(lbDebug, lbDebug.Items.Count - 1)

        'OLD
        'lbDebug.Items.Add("[" & Date.Now.ToString & "] : " & str)
        'lbDebug.SelectedIndex = lbDebug.Items.Count - 1
    End Sub

    ' Thread per l'ascolto dell'heartbeat della scheda
    Public CAN_Listen_Heartbeat_Th As Thread
    ' Variabile per memorizzare quando abbiamo ricevuto l'ultimo heartbeat
    Private lastHeartbeatTime As DateTime = DateTime.MinValue

    Public Function HexToInt(ByVal Hex As String) As Integer
        Return Convert.ToInt32(Hex, 16)
    End Function
    Public Function HexToByte(ByVal Hex As String) As Byte
        Return Convert.ToByte(Hex, 16)
    End Function

    Public Function HexToByteArray(ByVal Hex As String) As Byte()
        Return Enumerable.Range(0, Hex.Length).Where(Function(x) x Mod 2 = 0).[Select](Function(x) Convert.ToByte(Hex.Substring(x, 2), 16)).ToArray()
    End Function


    Public Function FormatHex(ByVal number As Integer, ByVal length As Integer) As String
        'Esempio
        '+-------+-------+
        '|  Hex  | B1 B2 |
        '+-------+-------+
        '| 0003  |     3 |
        '| 0033  |    33 |
        '| 0333  |  3 33 |
        '| 3333  | 33 33 |
        '+-------+-------+
        'Formattiamo sempre con 4 cifre
        Dim tmp As String = Hex(number)
        'Stringa troncata
        tmp = tmp.Substring(0, IIf(tmp.Length < length, tmp.Length, length))

        While tmp.Length <> length
            tmp = "0" & tmp
        End While
        Return tmp
    End Function


    Public Function ParseHexFileNew(ByVal Lines() As String)


        'CONDIF NEX
        Dim MODEL As String = "PIC30F5013"
        Dim DEV As String = ""
        Dim VERSION As String = ""


        'OPCODE DI APPOGGIO
        Dim OPCODE1, OPCODE2, OPCODE3, DEF_OPCODE1 As Byte()
        OPCODE1 = {&H0, &H1, &H4}
        OPCODE2 = {&H0, &H0, &H0}
        OPCODE3 = {&HFF, &HFF, &HFF}
        DEF_OPCODE1 = {&H2, &H14, &H4}


        'STRUTTURE APPOGGIO
        Dim Line(-1) As Line
        Dim ToSend(-1) As String
        Dim MemLines As List(Of MemLine) = New List(Of MemLine)
        Dim IndexPacket As Integer = 0

        'LIMITI IVT AIVT
        Dim IVT_LOW As Integer = 4 '0x0008 > 1
        Dim IVT_HIGH As Integer = 124 '0x00f8 > 1
        Dim AIVT_LOW As Integer = 132 '0x0108 > 1
        Dim AIVT_HIGH As Integer = 252 '0x001f8 > 1

        'LIMITI ZONA SCRITTURA

        Dim PADDR_MIN As Address = New Address()
        Dim PADDR_MAX As Address = New Address()
        Dim EEADDR_MIN As Address = New Address()
        Dim EEADDR_MAX As Address = New Address()

        Dim BTL_MIN As Address = New Address()
        Dim BTL_MAX As Address = New Address()

        'INIZIALIZZO I LIMITI CON QUELLI DEL PIC30F5013
        PADDR_MIN.AddrLow = 256
        PADDR_MIN.AddrHight = 0
        PADDR_MAX.AddrLow = 45054
        PADDR_MAX.AddrHight = 0

        BTL_MIN.AddrLow = 256
        BTL_MIN.AddrHight = 0
        BTL_MAX.AddrLow = 5118
        BTL_MAX.AddrHight = 0


        EEADDR_MIN.AddrLow = 64512
        EEADDR_MIN.AddrHight = 127
        EEADDR_MAX.AddrLow = 65534
        EEADDR_MAX.AddrHight = 127
        
        Dim WR_EEPROM As Boolean = False

        'METTO IL CAN ID DI DAFAULT
        CAN_ID = "306"


        'APP START CHIAMATA DAL BOOTLOADER 0x1400
        Dim APP_START As Integer = 5120

        'SIZE PAGE MEMORY
        Dim PM_ROW_SIZE As Integer = 32
        Dim PM_BYTE_SIZE As Integer = 96

        Dim currAddr As Address = New Address()
        currAddr.AddrLow = 0
        currAddr.AddrHight = 0
        currAddr.AddrLowPre = 0
        currAddr.AddrHightPre = 0

        Dim TmpRow As MemLine

        ' PARSO E ORDINO I BYTES DA INVIARE 
        For Each singleLine As String In Lines

            If singleLine.Trim.StartsWith("//") Then
                'SE E' UN COMMENTO SKKIPPO LA RIGA
                Continue For
            End If

            If Not singleLine.Trim.StartsWith(":") Then

                Dim configs As String() = singleLine.Trim.Split("=")

                If configs.Length = 2 Then
                    Try
                        Select Case configs(0).Trim().ToUpper()
                            Case "MOD"
                                MODEL = configs(1).Trim()
                                Continue For
                            Case "DEV"
                                DEV = configs(1).Trim()
                                Continue For
                            Case "VER"
                                VERSION = configs(1).Trim()
                                Continue For
                            Case "PADDR_MIN"
                                PADDR_MIN.AddrHight = Convert.ToUInt16(configs(1).Trim().Substring(0, 4), 16)
                                PADDR_MIN.AddrLow = Convert.ToUInt16(configs(1).Trim().Substring(4, 4), 16)
                                Continue For
                            Case "PADDR_MAX"
                                PADDR_MAX.AddrHight = Convert.ToUInt16(configs(1).Trim().Substring(0, 4), 16)
                                PADDR_MAX.AddrLow = Convert.ToUInt16(configs(1).Trim().Substring(4, 4), 16)
                                Continue For
                            Case "BTL_MAX"
                                BTL_MAX.AddrHight = Convert.ToUInt16(configs(1).Trim().Substring(0, 4), 16)
                                BTL_MAX.AddrLow = Convert.ToUInt16(configs(1).Trim().Substring(4, 4), 16)
                                Continue For
                            Case "BTL_MIN"
                                BTL_MIN.AddrHight = Convert.ToUInt16(configs(1).Trim().Substring(0, 4), 16)
                                BTL_MIN.AddrLow = Convert.ToUInt16(configs(1).Trim().Substring(4, 4), 16)
                                Continue For
                            Case "EEADDR_MIN"
                                EEADDR_MIN.AddrHight = Convert.ToUInt16(configs(1).Trim().Substring(0, 4), 16)
                                EEADDR_MIN.AddrLow = Convert.ToUInt16(configs(1).Trim().Substring(4, 4), 16)
                                Continue For
                            Case "EEADDR_MAX"
                                EEADDR_MAX.AddrHight = Convert.ToUInt16(configs(1).Trim().Substring(0, 4), 16)
                                EEADDR_MAX.AddrLow = Convert.ToUInt16(configs(1).Trim().Substring(4, 4), 16)
                                Continue For
                            Case "WR_EEPROM"
                                WR_EEPROM = Convert.ToBoolean(Integer.Parse(configs(1).Trim()))
                                Continue For
                            Case "CAN_ID"
                                CAN_ID = configs(1).Trim()
                                Continue For
                        End Select



                    Catch ex As Exception
                        Continue For
                    End Try

                End If
                Continue For

            End If

            TmpRow = New MemLine With {
                .Length = Convert.ToUInt16(singleLine.Substring(1, 2), 16),
                .AddrHightPre = currAddr.AddrHightPre,
                .AddrLowPre = Convert.ToUInt16(singleLine.Substring(3, 4), 16),
                .AddrLow = IIf(currAddr.AddrHightPre <> 1, CType((Convert.ToUInt16(singleLine.Substring(3, 4), 16) >> 1), UInt16), 32768 + CType((Convert.ToUInt16(singleLine.Substring(3, 4), 16) >> 1), UInt16)),
                .AddrHight = currAddr.AddrHightPre >> 1,
                .Type = Convert.ToUInt16(singleLine.Substring(7, 2), 16),
                .Data = HexToByteArray(singleLine.Substring(9, Convert.ToUInt16(singleLine.Substring(1, 2), 16) * 2))
                }



            Select Case TmpRow.Type
                Case 0

                    If TmpRow.AddrHight > 0 Then
                        Dim i As Integer = 0

                    End If

                    If ((TmpRow.Addr32 < BTL_MIN.Addr32) Or (TmpRow.Addr32 >= PADDR_MIN.Addr32 And TmpRow.Addr32 <= PADDR_MAX.Addr32) Or (WR_EEPROM.Equals(True) And TmpRow.Addr32 >= EEADDR_MIN.Addr32 And TmpRow.Addr32 <= EEADDR_MAX.Addr32)) Then



                        For i = 0 To TmpRow.Length / 4 - 1 Step 1
                            MemLines.Add(
                             New MemLine With {
                            .Length = 3,
                            .AddrLow = CType((TmpRow.AddrLow + (i * 2)), UInt16),
                            .AddrHight = TmpRow.AddrHight,
                            .Type = TmpRow.Type,
                            .Data = HexToByteArray(singleLine.Substring(9, Convert.ToUInt16(singleLine.Substring(1, 2), 16) * 2)).Skip(i * 4).Take(3).ToArray()
                        })

                            If MemLines.Last.Addr32 = 0 Then
                                Dim jj As Int32
                                jj = 0

                            End If
                        Next


                    End If
                Case 4
                    Dim Addr() As Byte = New Byte() {&H0, &H0}
                    Array.Copy(TmpRow.Data, 0, Addr, 0, 2)
                    If (BitConverter.IsLittleEndian) Then
                        Array.Reverse(Addr)
                    End If

                    currAddr.AddrHightPre = CType((BitConverter.ToUInt16(Addr, 0)), UInt16)
            End Select
        Next

        'SISTEMO SE NESSARIO LO LE ISTURZIONI TRA IVT E AIVT

        If (MemLines.FindIndex(Function(l) l.Addr32 = 128) < 0) Then
            MemLines.Add(
                             New MemLine With {
                            .Length = 3,
                            .AddrLow = 128,
                            .AddrHight = 0,
                            .Type = 0,
                            .Data = {&HFF, &HFF, &HFF}
                        })
        End If
        If (MemLines.FindIndex(Function(l) l.Addr32 = 130) < 0) Then
            MemLines.Add(
                             New MemLine With {
                            .Length = 3,
                            .AddrLow = 130,
                            .AddrHight = 0,
                            .Type = 0,
                            .Data = {&HFF, &HFF, &HFF}
                        })
        End If


        'RICONOSCO SE E' UN HEX NUOVO(0x1408) O VECCHIO(0x1400)
        If (PADDR_MIN.Addr32 = APP_START + 8) Then
            'E' NUOVO

            'AGGIUNO I COMANDI IN 0x1400 0x1402 0x1404 0x1406
            If (MemLines.FindIndex(Function(l) l.Addr32 = APP_START) < 0) Then
                MemLines.Add(
                             New MemLine With {
                            .Length = 3,
                            .AddrLow = APP_START,
                            .AddrHight = 0,
                            .Type = 0,
                            .Data = {&H1, &H0, &H0}
                        })
            End If

            If (MemLines.FindIndex(Function(l) l.Addr32 = APP_START + 2) < 0) Then
                MemLines.Add(
                             New MemLine With {
                            .Length = 3,
                            .AddrLow = APP_START + 2,
                            .AddrHight = 0,
                            .Type = 0,
                            .Data = {&H0, &H0, &H0}
                        })
            End If

            If (MemLines.FindIndex(Function(l) l.Addr32 = APP_START + 4) < 0) Then
                MemLines.Add(
                             New MemLine With {
                            .Length = 3,
                            .AddrLow = APP_START + 4,
                            .AddrHight = 0,
                            .Type = 0,
                            .Data = {&H0, &H0, &H0}
                        })
            End If

            If (MemLines.FindIndex(Function(l) l.Addr32 = APP_START + 6) < 0) Then
                MemLines.Add(
                             New MemLine With {
                            .Length = 3,
                            .AddrLow = APP_START + 6,
                            .AddrHight = 0,
                            .Type = 0,
                            .Data = {&H0, &H0, &H0}
                        })
            End If

            'METTO IL JUMP AL MAIN DELL'APPLICATIVO IN 0x1402
            Array.Copy(MemLines.FirstOrDefault(Function(l) l.Addr32 = 0).Data, 0, MemLines.FirstOrDefault(Function(l) l.Addr32 = (APP_START + 2)).Data, 0, 3)

        Else



            If MemLines.FirstOrDefault(Function(l) l.Addr32 = 0).Data.SequenceEqual(DEF_OPCODE1) Then
                'E' VECCHIO
                'NON FACCIO NIENTE
            Else
                'E' UNO DEGLI HEX STRANI (SE in 0x1402 non c'è niente o tutti FF posso scriverci senno esco )
                If MemLines.FirstOrDefault(Function(l) l.Addr32 = (APP_START + 2)).Data.SequenceEqual(OPCODE2) Or MemLines.FirstOrDefault(Function(l) l.Addr32 = (APP_START)).Data.SequenceEqual(OPCODE3) Then
                    Array.Copy(MemLines.FirstOrDefault(Function(l) l.Addr32 = 0).Data, 0, MemLines.FirstOrDefault(Function(l) l.Addr32 = (APP_START + 2)).Data, 0, 3)
                Else
                    'FIRMWARE NON CARICABILE
                    Return Nothing
                End If

            End If





        End If

        'SISTEMO LE PRIME DUE ISTRUZIONI PER RIMANERE PUNTATE AL BOOTLOADER
        Array.Copy(OPCODE1, 0, MemLines.FirstOrDefault(Function(l) l.Addr32 = 0).Data, 0, 3)
        Array.Copy(OPCODE2, 0, MemLines.FirstOrDefault(Function(l) l.Addr32 = 2).Data, 0, 3)



        'If (MemLines.FindIndex(Function(l) l.Addr32 = 0) >= 0) Then
        '    MemLines.RemoveAt(MemLines.FindIndex(Function(l) l.Addr32 = 0))
        'End If
        'If (MemLines.FindIndex(Function(l) l.Addr32 = 2) >= 0) Then
        '    MemLines.RemoveAt(MemLines.FindIndex(Function(l) l.Addr32 = 2))
        'End If
        MemLines = MemLines.OrderBy(Function(r) r.Addr32).ToList()




        'CREO LE LINE CON I PACCHETTI 
        currAddr.AddrLow = 0
        currAddr.AddrHight = 0
        For i = 0 To MemLines.Count - 1





            If (MemLines(i).AddrHight <> currAddr.AddrHight Or MemLines(i).Addr32 = 0) Then
                    currAddr.AddrHight = MemLines(i).AddrHight
                    ReDim Preserve Line(Line.Length)
                    Line(Line.Length - 1) = New Line()
                    ReDim Preserve Line(Line.Length - 1).Packet(0)
                    Line(Line.Length - 1).Packet(0) = New Msg()
                    Line(Line.Length - 1).Packet(0).Data(0) = 0
                    Line(Line.Length - 1).Packet(0).Data(1) = BitConverter.GetBytes(MemLines(i).AddrHight)(IIf(BitConverter.IsLittleEndian, 0, 1))
                    Line(Line.Length - 1).Packet(0).Data(2) = BitConverter.GetBytes(MemLines(i).AddrHight)(IIf(BitConverter.IsLittleEndian, 1, 0))
                    Line(Line.Length - 1).Packet(0).Data(3) = HexToByte("04") 'OFFSETRECORD
                    Line(Line.Length - 1).Packet(0).Data(4) = HexToByte("FF")
                    Line(Line.Length - 1).Packet(0).Data(5) = HexToByte("FF")
                    Line(Line.Length - 1).Packet(0).Data(6) = HexToByte("FF")
                    Line(Line.Length - 1).Packet(0).Data(7) = HexToByte("FF")
                End If

                'INIZIO UN PACCHETO DI TIPO DATI
                ReDim Preserve Line(Line.Length)
                Line(Line.Length - 1) = New Line()
                ReDim Preserve Line(Line.Length - 1).Packet(0)
                Line(Line.Length - 1).Packet(0) = New Msg()
                Line(Line.Length - 1).Packet(0).Data(0) = 0
                Line(Line.Length - 1).Packet(0).Data(1) = BitConverter.GetBytes(MemLines(i).AddrLow)(IIf(BitConverter.IsLittleEndian, 1, 0))
                Line(Line.Length - 1).Packet(0).Data(2) = BitConverter.GetBytes(MemLines(i).AddrLow)(IIf(BitConverter.IsLittleEndian, 0, 1))
                Line(Line.Length - 1).Packet(0).Data(3) = HexToByte("00") 'DATA
                Line(Line.Length - 1).Packet(0).Data(4) = HexToByte("FF")
                Line(Line.Length - 1).Packet(0).Data(5) = HexToByte("FF")
                Line(Line.Length - 1).Packet(0).Data(6) = HexToByte("FF")
                Line(Line.Length - 1).Packet(0).Data(7) = HexToByte("FF")

                'POPOLO UN PACCHETO CON UNA MEMORY PAGE
                Dim IndexData As Integer = 0
                Dim j As Integer = 0
                Dim k As Integer = 0
                IndexPacket = 1
                While j < PM_ROW_SIZE
                    If (IndexPacket > 14) Then
                        i += j
                        Exit While
                    End If


                    If (IndexData = 0) Then
                        ReDim Preserve Line(Line.Length - 1).Packet(IndexPacket)
                        Line(Line.Length - 1).Packet(IndexPacket) = New Msg()
                        Line(Line.Length - 1).Packet(IndexPacket).Data(IndexData) = IndexPacket
                        IndexData += 1
                    End If

                    If (IndexData > 7) Then
                        IndexData = 0
                        IndexPacket += 1
                    Else


                        If k >= 3 Then
                            k = 0
                            j += 1
                        End If
                        If (j >= PM_ROW_SIZE Or i + j > MemLines.Count - 1) Then
                            i += j - 1
                            Exit While
                        End If
                        Line(Line.Length - 1).Packet(IndexPacket).Data(IndexData) = MemLines(i + j).Data(k)
                        k += 1
                        IndexData += 1
                    End If


                    If (k > 2 And (i + j + 1 > MemLines.Count - 1 OrElse MemLines(i + j).Addr32 + 2 <> MemLines(i + j + 1).Addr32)) Then
                        i += j
                        Exit While
                    End If
                End While

                'COMPLETO IL LA LINE CON I PACCHETTI VUOTI SE SERVE
                While Line(Line.Length - 1).Packet.Length < 15
                    ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                    Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length - 1) = New Msg()
                    Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length - 1).Data(0) = Line(Line.Length - 1).Packet.Length - 1
                End While


                currAddr.AddrLow = MemLines(i - 1).AddrLow
                currAddr.AddrHight = MemLines(i - 1).AddrHight




        Next

        'CREO LA LINEA DI EOF 
        ReDim Preserve Line(Line.Length)
        Line(Line.Length - 1) = New Line()
        'Crea il primo pacchetto contenente le informazioni di controllo
        ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
        IndexPacket = Line(Line.Length - 1).Packet.Length - 1
        Line(Line.Length - 1).Packet(IndexPacket) = New Msg()
        Line(Line.Length - 1).Packet(IndexPacket).Data(0) = IndexPacket
        Line(Line.Length - 1).Packet(IndexPacket).Data(1) = HexToByte("FF")
        Line(Line.Length - 1).Packet(IndexPacket).Data(2) = HexToByte("FF")
        Line(Line.Length - 1).Packet(IndexPacket).Data(3) = HexToByte("01") 'EOF
        Line(Line.Length - 1).Packet(IndexPacket).Data(4) = HexToByte("FF")
        Line(Line.Length - 1).Packet(IndexPacket).Data(5) = HexToByte("FF")
        Line(Line.Length - 1).Packet(IndexPacket).Data(6) = HexToByte("FF")
        Line(Line.Length - 1).Packet(IndexPacket).Data(7) = HexToByte("FF")

        Return Line

    End Function


    Public Function ParseHexFile(ByVal Lines() As String)
        Dim Line(-1) As Line
        Dim ToSend(-1) As String
        Dim NByte, Count, AddressBlockOffset, AddressBlock As Integer

        Dim IndexPacket, IndexLine As Integer
        Dim LL As String 'Numero di byte
        Dim AAAA As String 'Indirizzo di partenza
        Dim AAAA_INT As Integer 'Indirizzo di partenza
        Dim TT As String 'Tipo di dato
        Dim DD As String 'Dati da inviare
        'Dim CC as String è il checksum e a noi non interessa

        Dim IVT_LOW As Integer = 4 '0x0008 > 1
        Dim IVT_HIGH As Integer = 124 '0x00f8 > 1
        Dim AIVT_LOW As Integer = 132 '0x0108 > 1
        Dim AIVT_HIGH As Integer = 252 '0x001f8 > 1
        Dim PROGRAM_LOW As Integer = 256
        Dim PROGRAM_HIGH As Integer = 45054
        Dim PM_ROW_SIZE As Integer = 96
        Dim OPCODE1, OPCODE2, OPCODE3 As String
        OPCODE1 = "000104"
        OPCODE2 = "000000"
        OPCODE3 = "FFFFFF"

        '########################################################################################
        '#
        '#                              PARSE IVT/AIVT TABLE
        '#                     (Prendo solo le line che interessano le VT)
        '#                        
        '########################################################################################
        'PREPARIAMO LE IVT/AIVT
        ':020000040000fa
        ':10|0000|00|00010400|00000000|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC     => IVT part1
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC

        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC     => IVT part2
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC

        ':10|0000|00|FFFFFF00|FFFFFF00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC     => AIVT part1
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC

        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC     => AIVT part2
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC
        ':10|0000|00|xxxxxx00|xxxxxx00|xxxxxx00|xxxxxx00|CC

        'Alla fine di questo ciclo abbiamo le IVT/AIVT da inviare al target in ToSend
        For Each singleLine As String In Lines
            Count = 0
            LL = singleLine.Substring(1, 2)
            AAAA_INT = HexToInt(singleLine.Substring(3, 4)) >> 1
            AAAA = FormatHex(HexToInt(singleLine.Substring(3, 4)) >> 1, 4)
            TT = singleLine.Substring(7, 2)
            DD = singleLine.Substring(9, 2 * HexToInt(LL))

            If (AAAA_INT >= IVT_LOW And AAAA_INT <= IVT_HIGH) Or (AAAA_INT >= AIVT_LOW And AAAA_INT <= AIVT_HIGH) Then
                If AAAA_INT = IVT_LOW Then
                    For i = 0 To OPCODE1.Length - 1 Step 2
                        ReDim Preserve ToSend(ToSend.Length)
                        ToSend(ToSend.Length - 1) = OPCODE1.Substring(i, 2)
                    Next
                    For i = 0 To OPCODE2.Length - 1 Step 2
                        ReDim Preserve ToSend(ToSend.Length)
                        ToSend(ToSend.Length - 1) = OPCODE2.Substring(i, 2)
                    Next
                ElseIf AAAA_INT = AIVT_LOW Then
                    For j = 0 To 1
                        For i = 0 To OPCODE3.Length - 1 Step 2
                            ReDim Preserve ToSend(ToSend.Length)
                            ToSend(ToSend.Length - 1) = OPCODE3.Substring(i, 2)
                        Next
                    Next
                End If
                For i = 0 To DD.Length - 1 Step 2
                    If (Count + 1) Mod 4 <> 0 Then
                        ReDim Preserve ToSend(ToSend.Length)
                        ToSend(ToSend.Length - 1) = DD.Substring(i, 2)
                    End If
                    Count += 1
                Next
            End If
        Next

        NByte = 0
        AddressBlock = -64
        IndexLine = 0
        IndexPacket = 0

        'CREO PACCHETTO DI INIZIALIZZAZIONE (OFFSET) - :020000040000fa
        ReDim Preserve Line(Line.Length)
        Line(Line.Length - 1) = New Line()
        'Crea il primo pacchetto contenente le informazioni di controllo
        ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
        IndexLine = Line.Length - 1
        IndexPacket = Line(IndexLine).Packet.Length - 1

        Line(IndexLine).Packet(IndexPacket) = New Msg()
        Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
        Line(IndexLine).Packet(IndexPacket).Data(1) = HexToByte("00")
        Line(IndexLine).Packet(IndexPacket).Data(2) = HexToByte("00")
        Line(IndexLine).Packet(IndexPacket).Data(3) = HexToByte("04") 'CAMPO CHE INDICA SE è UN DATARECORD O UN OFFSETRECORD
        Line(IndexLine).Packet(IndexPacket).Data(4) = HexToByte("FF")
        Line(IndexLine).Packet(IndexPacket).Data(5) = HexToByte("FF")
        Line(IndexLine).Packet(IndexPacket).Data(6) = HexToByte("FF")
        Line(IndexLine).Packet(IndexPacket).Data(7) = HexToByte("FF")

        For Each singleByte As String In ToSend
            If NByte Mod PM_ROW_SIZE = 0 Then
                AddressBlock += 64
                'BISOGNA CREARE QUELLO DI CONTROLLO E CREARE UNA NUOVA LINE (BLOCK)
                ReDim Preserve Line(Line.Length)
                Line(Line.Length - 1) = New Line()
                'Crea il primo pacchetto contenente le informazioni di controllo
                ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                IndexLine = Line.Length - 1
                IndexPacket = Line(IndexLine).Packet.Length - 1

                Line(IndexLine).Packet(IndexPacket) = New Msg()
                Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
                Line(IndexLine).Packet(IndexPacket).Data(1) = HexToByte(FormatHex(AddressBlock, 4).Substring(0, 2))
                Line(IndexLine).Packet(IndexPacket).Data(2) = HexToByte(FormatHex(AddressBlock, 4).Substring(2, 2))
                Line(IndexLine).Packet(IndexPacket).Data(3) = HexToByte("00")
                Line(IndexLine).Packet(IndexPacket).Data(4) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(5) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(6) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(7) = HexToByte("FF")
                NByte = 0
            End If

            If NByte Mod 7 = 0 Then
                ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                IndexLine = Line.Length - 1
                IndexPacket = Line(IndexLine).Packet.Length - 1

                Line(IndexLine).Packet(IndexPacket) = New Msg()
                Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
                Line(IndexLine).Packet(IndexPacket).Data((NByte Mod 7) + 1) = HexToByte(singleByte)
            Else
                Line(IndexLine).Packet(IndexPacket).Data((NByte Mod 7) + 1) = HexToByte(singleByte)
            End If
            NByte += 1
        Next


        '########################################################################################
        '#
        '#                              PARSE PROGRAM
        '#                        (tralasciando le IVT/AIVT)
        '#                        
        '########################################################################################

        Dim WritingBlock, RecordOffset, FirstTime As Boolean
        Dim tmp_AAAA As String = ""
        Dim NBytePacket As Integer

        NByte = 0
        NBytePacket = 0
        WritingBlock = False
        RecordOffset = False
        FirstTime = True
        ReDim Preserve ToSend(-1)

        For Each singleLine As String In Lines
            LL = singleLine.Substring(1, 2)
            AAAA_INT = HexToInt(singleLine.Substring(3, 4)) >> 1
            AAAA = FormatHex(HexToInt(singleLine.Substring(3, 4)) >> 1, 4)
            TT = singleLine.Substring(7, 2)
            DD = singleLine.Substring(9, 2 * HexToInt(LL))

            If (AAAA_INT >= PROGRAM_LOW And AAAA_INT <= PROGRAM_HIGH And TT = "00" And AddressBlockOffset = "0000") Or (TT = "00" And AddressBlockOffset <> "0000") Then
                If RecordOffset And NByte Mod PM_ROW_SIZE = 0 Then 'Se entra qui è perchè deve scrivere un recordoffset salvato in tmp_AAAA perchè stava scrivendo un blocco
                    RecordOffset = False
                    ReDim Preserve Line(Line.Length)
                    Line(Line.Length - 1) = New Line()
                    'Crea il primo pacchetto contenente le informazioni di controllo
                    ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                    IndexLine = Line.Length - 1
                    IndexPacket = Line(IndexLine).Packet.Length - 1

                    Line(IndexLine).Packet(IndexPacket) = New Msg()
                    Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
                    Line(IndexLine).Packet(IndexPacket).Data(1) = HexToByte(tmp_AAAA.Substring(0, 2))
                    Line(IndexLine).Packet(IndexPacket).Data(2) = HexToByte(tmp_AAAA.Substring(2, 2))
                    Line(IndexLine).Packet(IndexPacket).Data(3) = HexToByte("04") 'CAMPO CHE INDICA SE è UN DATARECORD O UN OFFSETRECORD
                    Line(IndexLine).Packet(IndexPacket).Data(4) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(5) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(6) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(7) = HexToByte("FF")
                    AddressBlockOffset = tmp_AAAA
                End If

                'Ora scrivo i dati programma salvati nella variabile DD
                If NByte Mod PM_ROW_SIZE = 0 Then
                    If FirstTime Then
                        AddressBlock = AAAA_INT - 64
                        FirstTime = False
                    End If

                    AddressBlock += 64
                    'BISOGNA CREARE QUELLO DI CONTROLLO E CREARE UNA NUOVA LINE (BLOCK)
                    ReDim Preserve Line(Line.Length)
                    Line(Line.Length - 1) = New Line()
                    'Crea il primo pacchetto contenente le informazioni di controllo
                    ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                    IndexLine = Line.Length - 1
                    IndexPacket = Line(IndexLine).Packet.Length - 1

                    Line(IndexLine).Packet(IndexPacket) = New Msg()
                    Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
                    Line(IndexLine).Packet(IndexPacket).Data(1) = HexToByte(FormatHex(AddressBlock, 4).Substring(0, 2))
                    Line(IndexLine).Packet(IndexPacket).Data(2) = HexToByte(FormatHex(AddressBlock, 4).Substring(2, 2))
                    Line(IndexLine).Packet(IndexPacket).Data(3) = HexToByte("00")
                    Line(IndexLine).Packet(IndexPacket).Data(4) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(5) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(6) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(7) = HexToByte("FF")
                    NByte = 0
                End If

                If NByte = 0 Or NBytePacket > 7 Then 'creo nuovo pacchetto
                    ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                    IndexLine = Line.Length - 1
                    IndexPacket = Line(IndexLine).Packet.Length - 1

                    Line(IndexLine).Packet(IndexPacket) = New Msg()
                    Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket

                    NBytePacket = 1
                End If

                For i = 0 To DD.Length - 1 Step 8
                    For j = 0 To 6 - 1 Step 2
                        Line(IndexLine).Packet(IndexPacket).Data((NBytePacket Mod 8)) = HexToByte(DD.Substring(i + j, 2))
                        NBytePacket += 1
                        NByte += 1

                        If NBytePacket > 7 Then
                            ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                            IndexLine = Line.Length - 1
                            IndexPacket = Line(IndexLine).Packet.Length - 1

                            Line(IndexLine).Packet(IndexPacket) = New Msg()
                            Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket

                            NBytePacket = 1
                        End If

                        If NByte Mod PM_ROW_SIZE = 0 And (i + j) < DD.Length - 4 - 1 Then
                            AddressBlock += 64
                            'BISOGNA CREARE QUELLO DI CONTROLLO E CREARE UNA NUOVA LINE (BLOCK)
                            ReDim Preserve Line(Line.Length)
                            Line(Line.Length - 1) = New Line()
                            'Crea il primo pacchetto contenente le informazioni di controllo
                            ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                            IndexLine = Line.Length - 1
                            IndexPacket = Line(IndexLine).Packet.Length - 1

                            Line(IndexLine).Packet(IndexPacket) = New Msg()
                            Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
                            Line(IndexLine).Packet(IndexPacket).Data(1) = HexToByte(FormatHex(AddressBlock, 4).Substring(0, 2))
                            Line(IndexLine).Packet(IndexPacket).Data(2) = HexToByte(FormatHex(AddressBlock, 4).Substring(2, 2))
                            Line(IndexLine).Packet(IndexPacket).Data(3) = HexToByte("00")
                            Line(IndexLine).Packet(IndexPacket).Data(4) = HexToByte("FF")
                            Line(IndexLine).Packet(IndexPacket).Data(5) = HexToByte("FF")
                            Line(IndexLine).Packet(IndexPacket).Data(6) = HexToByte("FF")
                            Line(IndexLine).Packet(IndexPacket).Data(7) = HexToByte("FF")
                            NByte = 0

                            ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                            IndexLine = Line.Length - 1
                            IndexPacket = Line(IndexLine).Packet.Length - 1

                            Line(IndexLine).Packet(IndexPacket) = New Msg()
                            Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket

                            NBytePacket = 1
                        End If
                    Next
                Next
            ElseIf TT = "04" Then
                If NByte Mod PM_ROW_SIZE = 0 Then
                    RecordOffset = False
                    ReDim Preserve Line(Line.Length)
                    Line(Line.Length - 1) = New Line()
                    'Crea il primo pacchetto contenente le informazioni di controllo
                    ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                    IndexLine = Line.Length - 1
                    IndexPacket = Line(IndexLine).Packet.Length - 1

                    Line(IndexLine).Packet(IndexPacket) = New Msg()
                    Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
                    Line(IndexLine).Packet(IndexPacket).Data(1) = HexToByte(AAAA.Substring(0, 2))
                    Line(IndexLine).Packet(IndexPacket).Data(2) = HexToByte(AAAA.Substring(2, 2))
                    Line(IndexLine).Packet(IndexPacket).Data(3) = HexToByte("04") 'CAMPO CHE INDICA SE è UN DATARECORD O UN OFFSETRECORD
                    Line(IndexLine).Packet(IndexPacket).Data(4) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(5) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(6) = HexToByte("FF")
                    Line(IndexLine).Packet(IndexPacket).Data(7) = HexToByte("FF")
                    AddressBlockOffset = AAAA
                Else
                    tmp_AAAA = AAAA
                    RecordOffset = True
                End If
            ElseIf DD = "" And AAAA_INT = 0 And LL = "00" And TT = "01" Then
                'End of file
                'Riempio di F l'ultimo blocco e creo il messaggio di endoffile
                For i = Line(IndexLine).Packet.Length To 14
                    ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                    IndexLine = Line.Length - 1
                    IndexPacket = Line(IndexLine).Packet.Length - 1

                    Line(IndexLine).Packet(IndexPacket) = New Msg()
                    Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket

                    NBytePacket = 1
                Next

                ReDim Preserve Line(Line.Length)
                Line(Line.Length - 1) = New Line()
                'Crea il primo pacchetto contenente le informazioni di controllo
                ReDim Preserve Line(Line.Length - 1).Packet(Line(Line.Length - 1).Packet.Length)
                IndexLine = Line.Length - 1
                IndexPacket = Line(IndexLine).Packet.Length - 1

                Line(IndexLine).Packet(IndexPacket) = New Msg()
                Line(IndexLine).Packet(IndexPacket).Data(0) = IndexPacket
                Line(IndexLine).Packet(IndexPacket).Data(1) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(2) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(3) = HexToByte("01") 'CAMPO CHE INDICA SE è UN DATARECORD O UN OFFSETRECORD
                Line(IndexLine).Packet(IndexPacket).Data(4) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(5) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(6) = HexToByte("FF")
                Line(IndexLine).Packet(IndexPacket).Data(7) = HexToByte("FF")
            End If
        Next

        Return Line
    End Function

    Public Sub Thread_Listen_For_Heartbeat()
        Dim CANMsg As TPCANMsg = Nothing
        Dim CANTimeStamp As TPCANTimestamp
        Dim stsResult As TPCANStatus

        ' Tenta di connettersi al bus CAN. Questa è l'unica connessione
        ' che verrà mantenuta attiva per tutta la durata dell'applicazione.
        Try
            CAN_connect()
            Debug($"Initial connection successful. CAN_Connected is now {CAN_Connected}.")

        Catch ex As Exception
            Debug($"




: Could not connect to the CAN adapter. Status feature will be inactive. Details: {ex.Message}")
            Return ' Termina il thread se non possiamo connetterci.
        End Try

        ' Ciclo infinito che dura quanto l'applicazione
        Do
            ' Se un altro thread sta facendo un'operazione attiva (flag isCANTalking),
            ' questo thread si mette temporaneamente in pausa per non interferire.
            If isCANTalking Then
                ' IMPORTANTE: Mentre siamo in pausa, continuiamo ad aggiornare l'ora dell'ultimo
                ' heartbeat. Questo "congela" il timer del timeout, impedendo allo stato
                ' di passare a "NOT CONNECTED" durante un'operazione di flash o upload.
                lastHeartbeatTime = DateTime.Now

                Thread.Sleep(100) ' Piccola pausa
                Continue Do       ' Salta il resto del ciclo e ricomincia
            End If

            ' Legge un messaggio dal buffer del driver CAN.
            stsResult = PCANBasic.Read(CAN_Handle, CANMsg, CANTimeStamp)

            ' Controlla se abbiamo ricevuto un messaggio e se l'ID è quello dell'heartbeat (0x604)
            If stsResult = TPCANStatus.PCAN_ERROR_OK AndAlso Hex(CANMsg.ID) = CAN_ID_DWIN_HEARTBEAT Then
                ' Abbiamo ricevuto un heartbeat. Aggiorniamo l'ora.
                lastHeartbeatTime = DateTime.Now

                ' Estrai la versione del firmware dal payload del messaggio
                Dim firmwareVersion As Byte = CANMsg.DATA(6) ' 7° byte

                ' Aggiorniamo l'interfaccia utente, ma solo se lo stato è cambiato.
                If lblBoardStatus.Text <> "CONNECTED" Then
                    lblText(lblBoardStatus, "CONNECTED")
                    lblColor(lblBoardStatus, Color.Green)
                    ' Abilita i pulsanti principali ora che sappiamo che la scheda è viva
                    btnEnabled(Button2, True)       ' Pulsante Flash Firmware
                    btnEnabled(btnUploadDwin, True) ' Pulsante Carica Risorse DWIN
                End If

                ' Aggiorniamo sempre la versione del firmware nel caso fosse cambiata
                lblText(lblFirmwareVersion, firmwareVersion.ToString())
            End If

            ' Controlla se è passato troppo tempo (2 secondi) dall'ultimo heartbeat ricevuto.
            ' Questo controllo non scatterà mai quando isCANTalking è True.
            If DateTime.Now.Subtract(lastHeartbeatTime).TotalSeconds > 2 Then
                ' Consideriamo la scheda disconnessa. Aggiorniamo l'UI solo se necessario.
                If lblBoardStatus.Text <> "NOT CONNECTED" Then
                    lblText(lblBoardStatus, "NOT CONNECTED")
                    lblColor(lblBoardStatus, Color.Red)
                    lblText(lblFirmwareVersion, "N/A") ' N/A = Not Available
                    ' Disabilita i pulsanti principali
                    ' btnEnabled(Button2, False)
                    ' btnEnabled(btnUploadDwin, False)
                End If
            End If

            ' Piccola pausa per non sovraccaricare la CPU.
            'Thread.Sleep(50)

        Loop While True
    End Sub

#End Region

#Region "DWIN"
    Private Function CreateDwinCommandSequence(ByVal folderPath As String) As List(Of TPCANMsg)
        ' Questa lista conterrà inizialmente tutti i messaggi tranne F0 e F3
        Dim commandList As New List(Of TPCANMsg)

        ' Definiamo le costanti del protocollo
        Const blockSize As Integer = 32 * 1024
        Const segmentSize As Integer = 240
        Const subsegmentSize As Integer = 7
        Const ramAddressBase As UShort = &H8000

        Try
            ' Trova tutti i file .bin e .icl da processare
            Dim filePaths = Directory.GetFiles(folderPath, "*.bin", SearchOption.AllDirectories) _
                .Concat(Directory.GetFiles(folderPath, "*.icl", SearchOption.AllDirectories)) _
                .ToList()
            Debug($"Found {filePaths.Count()} files to process in the unzipped folder.")

            ' CICLO 1: Processa ogni file trovato
            For Each filePath As String In filePaths
                Dim fileName As String = Path.GetFileNameWithoutExtension(filePath)
                Dim fileId As Integer
                ' Estrae solo le cifre dal nome del file per ottenere l'ID
                If Not Integer.TryParse(String.Concat(fileName.Where(AddressOf Char.IsDigit)), fileId) Then
                    Debug($"WARNING: Could not extract a numeric ID from file: '{fileName}'. It will be skipped.")
                    Continue For
                End If

                ' Calcola l'indirizzo flash di base usando la formula ID * 8
                Dim startBlockIndex As Integer = fileId * 8

                Dim fileData As Byte() = File.ReadAllBytes(filePath)
                Dim filePointer As Integer = 0

                ' CICLO 2: Suddivide il file in blocchi da 32KB
                While filePointer < fileData.Length
                    ' L'indirizzo flash per il comando F2 è l'indice del blocco corrente.
                    Dim currentFlashBlockAddress As UShort = CType(startBlockIndex + (filePointer / blockSize), UShort)

                    Dim block As Byte() = fileData.Skip(filePointer).Take(blockSize).ToArray()
                    Debug($"  -> Processing Block for File ID {fileId} ({block.Length} bytes) -> Flash Block Index: {currentFlashBlockAddress:X4}")

                    Dim ramAddress As UShort = ramAddressBase

                    ' CICLO 3: Suddivide il blocco in segmenti da 240 byte
                    For i As Integer = 0 To block.Length - 1 Step segmentSize
                        Dim segment As Byte() = block.Skip(i).Take(segmentSize).ToArray()
                        If segment.Length = 0 Then Continue For

                        ' A. Crea i pacchetti dati (sottosegmenti da 7 byte)
                        Dim subsegmentIndex As Byte = 0
                        For j As Integer = 0 To segment.Length - 1 Step subsegmentSize
                            Dim subsegment As Byte() = segment.Skip(j).Take(subsegmentSize).ToArray()
                            Dim msgData As New TPCANMsg With {
                            .ID = Convert.ToUInt32(CAN_ID_DWIN_COMMAND, 16), .MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD, .LEN = 8, .DATA = New Byte(7) {}
                        }
                            msgData.DATA(0) = subsegmentIndex
                            Array.Copy(subsegment, 0, msgData.DATA, 1, subsegment.Length)
                            commandList.Add(msgData)
                            subsegmentIndex += subsegmentSize
                        Next

                        ' B. Crea il comando 0xF1 (Scrittura in RAM) per questo segmento
                        Dim msgWriteRam As New TPCANMsg With {
                        .ID = Convert.ToUInt32(CAN_ID_DWIN_COMMAND, 16), .MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD, .LEN = 8, .DATA = New Byte(7) {}
                    }
                        msgWriteRam.DATA(0) = &HF1
                        msgWriteRam.DATA(1) = CType(segment.Length, Byte) ' LL (Lunghezza)
                        Dim ramAddrBytes As Byte() = BitConverter.GetBytes(ramAddress)
                        If BitConverter.IsLittleEndian Then Array.Reverse(ramAddrBytes) ' Assicura l'ordine Big-Endian
                        Array.Copy(ramAddrBytes, 0, msgWriteRam.DATA, 2, 2) ' AA AA (Indirizzo)
                        commandList.Add(msgWriteRam)
                        Debug($"    - Segment processed ({segment.Length} bytes). Generating command 0xF1 for RAM address 0x{ramAddress:X4}.")

                        ' Incrementa l'indirizzo RAM per il prossimo segmento
                        ramAddress += segmentSize ' Incrementa di 240 (0xF0)
                    Next

                    ' C. Crea il comando 0xF2 (Copia in Flash) per questo blocco
                    Dim msgWriteFlash As New TPCANMsg With {
                        .ID = Convert.ToUInt32(CAN_ID_DWIN_COMMAND, 16), .MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD, .LEN = 8, .DATA = New Byte(7) {}
                    }
                    msgWriteFlash.DATA(0) = &HF2
                    Dim flashAddrBytes As Byte() = BitConverter.GetBytes(currentFlashBlockAddress)
                    If BitConverter.IsLittleEndian Then Array.Reverse(flashAddrBytes) ' Assicura l'ordine Big-Endian
                    Array.Copy(flashAddrBytes, 0, msgWriteFlash.DATA, 1, 2) ' BB BB (Indirizzo Blocco)
                    commandList.Add(msgWriteFlash)
                    Debug($"  -> Block processed. Generating command 0xF2 for Flash Block Index 0x{currentFlashBlockAddress:X4}.")

                    ' Incrementa il puntatore per il prossimo blocco
                    filePointer += block.Length
                End While
            Next

            ' Se nessun comando è stato generato (es. file non validi o vuoti), esci.
            If commandList.Count = 0 Then
                Debug("No valid files found or files are empty. No commands were generated.")
                Return commandList
            End If

            ' D. Crea e inserisci il comando 0xF0 (INIT) all'inizio della lista
            Dim msgCount As Integer = commandList.Count
            Dim msgInit As New TPCANMsg With {
            .ID = Convert.ToUInt32(CAN_ID_DWIN_COMMAND, 16), .MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD, .LEN = 8, .DATA = New Byte(7) {}
        }
            msgInit.DATA(0) = &HF0
            Dim countBytes As Byte() = BitConverter.GetBytes(msgCount)
            If BitConverter.IsLittleEndian Then Array.Reverse(countBytes) ' Assicura l'ordine Big-Endian
            Array.Copy(countBytes, 0, msgInit.DATA, 1, 4) ' MC MC MC MC (Conteggio Messaggi)
            commandList.Insert(0, msgInit)
            Debug($"Generated 0xF0 (INIT) command for a total of {msgCount} subsequent messages.")

            ' E. Aggiungi il comando 0xF3 (RESET) alla fine della lista
            Dim msgReset As New TPCANMsg With {
            .ID = Convert.ToUInt32(CAN_ID_DWIN_COMMAND, 16), .MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD, .LEN = 8, .DATA = New Byte(7) {}
        }
            msgReset.DATA(0) = &HF3
            commandList.Add(msgReset)
            Debug("Generated 0xF3 (RESET) command as the final message.")

            Try
                ' Salva il log nella stessa cartella dell'eseguibile
                Dim logPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DWIN_Sequence_Log.txt")
                Using writer As New StreamWriter(logPath, False) ' False = sovrascrivi file esistente
                    writer.WriteLine($"Log della sequenza DWIN generato il: {DateTime.Now}")
                    writer.WriteLine($"Totale messaggi: {commandList.Count}")
                    writer.WriteLine("--- INIZIO SEQUENZA ---")

                    For i As Integer = 0 To commandList.Count - 1
                        Dim msg As TPCANMsg = commandList(i)
                        ' Formatta i dati (es: F0 00 00 12 34 00 00 00)
                        Dim dataStr As String = BitConverter.ToString(msg.DATA).Replace("-", " ")
                        writer.WriteLine($"Msg {i.ToString().PadLeft(5, "0")}: ID={msg.ID:X3}, Len={msg.LEN}, Data=[{dataStr}]")
                    Next

                    writer.WriteLine("--- FINE SEQUENZA ---")
                End Using
                Debug($"*** Sequenza DWIN salvata in: {logPath} ***")
            Catch ex As Exception
                Debug($"*** ERRORE nel salvataggio del log DWIN: {ex.Message} ***")
            End Try

            Return commandList

        Catch ex As Exception
            Debug($"CRITICAL ERROR while creating command sequence: {ex.Message}")
            Return Nothing ' Restituisce Nothing per indicare un fallimento grave
        End Try
    End Function


    '================================================================================
    ' THREAD PER L'INVIO DEI COMANDI DWIN 
    '================================================================================

    Public CAN_Send_DWIN_Th As Thread

    Public Sub Thread_Send_DWIN(ByVal commandSequenceParam As Object)
        Dim commandSequence As List(Of TPCANMsg) = CType(commandSequenceParam, List(Of TPCANMsg))

        Try
            ' ATTIVA IL FLAG: Diciamo all'heartbeat di mettersi in pausa e prendiamo il controllo dello stato.
            isCANTalking = True
            lblText(lblBoardStatus, "UPLOADING...")
            lblColor(lblBoardStatus, Color.Blue)
            Debug("DWIN upload thread started. Heartbeat listener paused.")

            ' Imposta i valori della ProgressBar.
            pbMinimum(pbDwinProgress, 0)
            pbMaximum(pbDwinProgress, commandSequence.Count - 1)
            pbValue(pbDwinProgress, 0)

            ' CICLO DI INVIO E ATTESA ACK
            For i As Integer = 0 To commandSequence.Count - 1
                Dim msgToSend As TPCANMsg = commandSequence(i)
                ' Un "comando" è un messaggio con opcode >= 0xF0 (INIT, WRITE RAM, FLASH, RESET).
                Dim isCommand As Boolean = (msgToSend.DATA(0) >= &HF0)

                ' 1. Invia il messaggio CAN.
                If Not CAN_write_DWIN(msgToSend) Then
                    Throw New Exception($"Failed to send CAN message #{i} (Command {msgToSend.DATA(0):X2}). Upload aborted.")
                End If

                ' 2. Attendi l'ACK.

                ' Debug($"Command {msgToSend.DATA(0):X2} sent. Waiting for ACK...")

                ' Imposta un timeout più lungo per il comando F2 (Flash), che è un'operazione lenta.
                Dim timeout As Integer = 5000 ' Timeout di default 5 secondi.
                    If msgToSend.DATA(0) = &HF2 Then
                        timeout = 60000 ' 60 secondi per il Flash.
                        Debug("Increased timeout to 60s for Flash operation.")
                    End If

                    Dim timeStart As Long = CLng(DateTime.UtcNow.Subtract(New DateTime(1970, 1, 1)).TotalMilliseconds)
                    Dim ackReceived As Boolean = False

                    ' Pulisci il buffer di ricezione prima di iniziare l'attesa.
                    Array.Clear(CAN_ReceiveMsg, 0, CAN_ReceiveMsg.Length)
                    CAN_ReceiveMsg(0) = 255 ' Valore di default per indicare timeout.

                    Do
                        Dim CANMsg As TPCANMsg = Nothing
                        Dim CANTimeStamp As TPCANTimestamp
                        Dim stsResult As TPCANStatus = PCANBasic.Read(CAN_Handle, CANMsg, CANTimeStamp)

                        ' Controlla se abbiamo ricevuto un messaggio e se l'ID è quello delle risposte DWIN (0x600).
                        If stsResult = TPCANStatus.PCAN_ERROR_OK AndAlso Hex(CANMsg.ID) = CAN_ID_DWIN_RESPONSE Then
                            ' Messaggio ACK ricevuto. Salviamo i dati, impostiamo il flag e usciamo dal ciclo di attesa.
                            CAN_ReceiveMsg = CANMsg.DATA
                            ackReceived = True
                            Exit Do
                        End If

                    ' Piccola pausa per non tenere la CPU al 100% durante l'attesa.
                    'Thread.Sleep(1)

                Loop While (timeout > CLng(DateTime.UtcNow.Subtract(New DateTime(1970, 1, 1)).TotalMilliseconds) - timeStart)


                    ' 3. Controlla il risultato del ciclo di attesa.
                    If Not ackReceived Then
                        Throw New Exception("Timeout waiting for ACK from the target.")
                    ElseIf Not CAN_ReceiveMsg.SequenceEqual(msgToSend.DATA) Then
                        ' L'ACK corretto deve essere una "eco" esatta del comando inviato.
                        Throw New Exception("Invalid ACK received. Expected: " & BitConverter.ToString(msgToSend.DATA) & ", Received: " & BitConverter.ToString(CAN_ReceiveMsg))
                    End If

                ' Debug("ACK received successfully.")

                ' Aggiorna la ProgressBar.
                pbValue(pbDwinProgress, i)
            Next

            Debug("DWIN UPLOAD COMPLETED SUCCESSFULLY!")
            MessageBox.Show("DWIN resources uploaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

        Catch ex As Exception
            ' Gestisce tutti gli altri errori (timeout, invio fallito, ACK errato, etc.).
            Debug($"ERROR: {ex.Message}")
            MessageBox.Show(ex.Message, "Upload Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            ' Questo blocco di pulizia finale viene eseguito SEMPRE.

            ' DISATTIVA IL FLAG: Diciamo al thread dell'heartbeat di riprendere a lavorare.
            isCANTalking = False
            Debug("DWIN upload thread finished. Heartbeat listener resumed.")

            ' Riabilita i pulsanti dell'interfaccia utente.
            btnEnabled(btnSelectZip, True)
            btnEnabled(btnUploadDwin, True)
            ' Riabilita il pulsante Flash solo se la scheda è ancora connessa.
            If lblBoardStatus.Text = "CONNECTED" Then
                btnEnabled(Button2, True)
            End If

            ' Resetta la ProgressBar.
            pbValue(pbDwinProgress, 0)
        End Try
    End Sub


    '================================================================================
    ' FUNZIONI HELPER PER LA COMUNICAZIONE CAN DWIN
    '================================================================================
    Public Function CAN_write_DWIN(ByVal msg As TPCANMsg) As Boolean
        Dim stsResult As TPCANStatus = PCANBasic.Write(CAN_Handle, msg)
        Return stsResult = TPCANStatus.PCAN_ERROR_OK
    End Function

#End Region

#Region "Delegate"
#Region "lbDebugAdd"
    Public Delegate Sub lbDebugAddDelegate(ByVal lb As ListBox, ByVal txt As String)
    Public Sub lbDebugAdd(ByVal lb As ListBox, ByVal txt As String)
        If lb.InvokeRequired Then
            lb.Invoke(New lbDebugAddDelegate(AddressOf lbDebugAdd), New Object() {lb, txt})
        Else
            lb.Items.Add(txt)
        End If
    End Sub
#End Region
#Region "lbDebugIndex"
    Public Delegate Sub lbDebugIndexDelegate(ByVal lb As ListBox, ByVal index As Integer)
    Public Sub lbDebugIndex(ByVal lb As ListBox, ByVal index As Integer)
        If lb.InvokeRequired Then
            lb.Invoke(New lbDebugIndexDelegate(AddressOf lbDebugIndex), New Object() {lb, index})
        Else
            lb.SelectedIndex = index
        End If
    End Sub
#End Region
#Region "btnEnabled"
    Public Delegate Sub btnEnabledDelegate(ByVal btn As Button, ByVal value As Boolean)
    Public Sub btnEnabled(ByVal btn As Button, ByVal value As Boolean)
        If btn.InvokeRequired Then
            Try
                btn.Invoke(New btnEnabledDelegate(AddressOf btnEnabled), New Object() {btn, value})
            Catch ex As Exception

            End Try
        Else
            btn.Enabled = value
        End If
    End Sub
#End Region
#Region "pbMaximum"
    Public Delegate Sub pbMaximumDelegate(ByVal pb As ProgressBar, ByVal value As Integer)
    Public Sub pbMaximum(ByVal pb As ProgressBar, ByVal value As Integer)
        If pb.InvokeRequired Then
            pb.Invoke(New pbMaximumDelegate(AddressOf pbMaximum), New Object() {pb, value})
        Else
            pb.Maximum = value
        End If
    End Sub
#End Region
#Region "pbMinimum"
    Public Delegate Sub pbMinimumDelegate(ByVal pb As ProgressBar, ByVal value As Integer)
    Public Sub pbMinimum(ByVal pb As ProgressBar, ByVal value As Integer)
        If pb.InvokeRequired Then
            pb.Invoke(New pbMinimumDelegate(AddressOf pbMinimum), New Object() {pb, value})
        Else
            pb.Minimum = value
        End If
    End Sub
#End Region
#Region "pbValue"
    Public Delegate Sub pbValueDelegate(ByVal pb As ProgressBar, ByVal value As Integer)
    Public Sub pbValue(ByVal pb As ProgressBar, ByVal value As Integer)
        If pb.InvokeRequired Then
            pb.Invoke(New pbValueDelegate(AddressOf pbValue), New Object() {pb, value})
        Else
            pb.Value = value
        End If
    End Sub
#End Region
#Region "Delegate per UI di Stato"
    Delegate Sub lblTextDelegate(lbl As Label, text As String)
    Public Sub lblText(lbl As Label, text As String)
        If lbl.InvokeRequired Then
            lbl.Invoke(New lblTextDelegate(AddressOf lblText), New Object() {lbl, text})
        Else
            lbl.Text = text
        End If
    End Sub

    Delegate Sub lblColorDelegate(lbl As Label, color As Color)
    Public Sub lblColor(lbl As Label, color As Color)
        If lbl.InvokeRequired Then
            lbl.Invoke(New lblColorDelegate(AddressOf lblColor), New Object() {lbl, color})
        Else
            lbl.ForeColor = color
        End If
    End Sub
#End Region
#End Region

#Region "UI elements"
    Private Sub Form1_FormClosed(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosedEventArgs) Handles Me.FormClosed


        ' Interrompi il thread di flashing del firmware, se è in esecuzione.
        If TypeOf CAN_Send_Th Is Thread AndAlso CAN_Send_Th.IsAlive Then
            CAN_Send_Th.Abort()
        End If

        ' Interrompi il vecchio thread di ricezione del firmware, se è in esecuzione.
        If TypeOf CAN_Receive_Th Is Thread AndAlso CAN_Receive_Th.IsAlive Then
            CAN_Receive_Th.Abort()
        End If

        ' Interrompi il thread di upload DWIN, se è in esecuzione.
        If TypeOf CAN_Send_DWIN_Th Is Thread AndAlso CAN_Send_DWIN_Th.IsAlive Then
            CAN_Send_DWIN_Th.Abort()
        End If

        ' Controlliamo se la connessione è stata stabilita con successo prima di tentare di chiuderla.
        If CAN_Connected Then
            Debug("Application closing. Disconnecting from CAN adapter.")
            PCANBasic.Uninitialize(CAN_Handle)
            ' Aggiorniamo la variabile di stato per coerenza.
            CAN_Connected = False
        End If
    End Sub
    Private Sub Form1_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        CheckForIllegalCrossThreadCalls = True

        ' Avviamo il thread che ascolta l'heartbeat in background
        CAN_Listen_Heartbeat_Th = New Thread(AddressOf Thread_Listen_For_Heartbeat)
        CAN_Listen_Heartbeat_Th.IsBackground = True ' Importante: il thread si chiuderà con l'app
        CAN_Listen_Heartbeat_Th.Start()

    End Sub
    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        OpenFileDialog1.Title = "Search firmware"
        OpenFileDialog1.InitialDirectory = ".\\"

        If OpenFileDialog1.ShowDialog() = DialogResult.OK Then
            TextBox1.Text = OpenFileDialog1.FileName
        End If
    End Sub
    Private Sub Button2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button2.Click
        btnEnabled(Button1, False)
        btnEnabled(Button2, False)
        btnEnabled(btnUploadDwin, False)


        CAN_Send_Th = New Thread(AddressOf Thread_Send)
        CAN_Send_Th.Start()
    End Sub
#End Region

#Region "CAN Interface"
    Public Sub CAN_connect()
        Dim stsResult As TPCANStatus

        'Seleziona il PcanHandle corretto
        CAN_Handle = Convert.ToByte(CAN_HandleID, 16)

        'Instaura una connessione
        stsResult = PCANBasic.Initialize(CAN_Handle, CAN_Baudrate, CAN_HwType, Convert.ToUInt32(CAN_IOport, 16), Convert.ToUInt16(CAN_Interrupt))

        If stsResult <> TPCANStatus.PCAN_ERROR_OK Then
            Throw New Exception("Hardware not connected")
        Else
            ' Prepares the PCAN-Basic's PCAN-Trace file
            btnEnabled(Button3, True)
            CAN_Connected = True
            ConfigureTraceFile()
        End If
    End Sub
    Public Function CAN_write(ByVal msg() As Byte)
        Dim CAN_Msg As TPCANMsg
        Dim stsResult As TPCANStatus

        CAN_Msg = New TPCANMsg()
        CAN_Msg.DATA = msg
        CAN_Msg.ID = Convert.ToUInt32(CAN_ID, 16)
        CAN_Msg.LEN = Convert.ToByte(CAN_MsgLength)
        CAN_Msg.MSGTYPE = IIf((CAN_MsgExtended), TPCANMessageType.PCAN_MESSAGE_EXTENDED, TPCANMessageType.PCAN_MESSAGE_STANDARD)

        stsResult = PCANBasic.Write(CAN_Handle, CAN_Msg)

        If stsResult <> TPCANStatus.PCAN_ERROR_OK Then
            Return False
        Else
            Return True

        End If
    End Function
    Public Sub CAN_disconnect()
        ' Releases a current connected PCAN-Basic channel
        '
        PCANBasic.Uninitialize(CAN_Handle)
        CAN_Connected = False

        Try
            If TypeOf CAN_Send_Th Is Thread Then
                If CAN_Send_Th.IsAlive Then
                    CAN_Send_Th.Abort()
                End If
            End If

            If TypeOf CAN_Receive_Th Is Thread Then
                If CAN_Receive_Th.IsAlive Then
                    CAN_Receive_Th.Abort()
                End If
            End If

        Catch ex As Exception
            Debug("Can Adapter disconnected")
            'Debug("All threads aborted")
            Exit Sub
        End Try
        Debug("Can Adapter disconnected")

    End Sub
    Public Sub Timer1_Tick(sender As System.Object, e As System.EventArgs) Handles Timer1.Tick
        Debug("Timer")
    End Sub
    Public Sub Thread_Send()
        Dim keepStarting As Boolean = True
        Try
            isCANTalking = True
            Debug("Firmware flash thread started. Heartbeat listener paused.")

            Debug($"Firmware flash starting. Current status of CAN_Connected: {CAN_Connected}.")
            If Not CAN_Connected Then
                Throw New Exception("CAN hardware is not connected or failed to initialize at startup.")
            End If
            ' La chiamata originale a CAN_connect() è stata rimossa.


            'Cosa fa:
            ' - Spacchetta in byte
            Line = File.ReadAllLines(TextBox1.Text)
            Debug("Firmware successfully loaded from file")

            'Cosa fa:
            ' - ParseHexFile
            ToSend = ParseHexFileNew(Line)
            If IsNothing(ToSend) Then
                Throw New System.Exception("Firmware not supported!")
            End If

            'Cosa fa:
            ' - Setta i parametri in base all'algoritmo
            CAN_Algoritmo1_Parameters()

            'Cosa fa:
            'Settare le variabili di base
            NPacchetto = 0
            CAN_Attempt = 0
            NPacket = 0

            'Cosa fa:
            ' - Imposto valori iniziali
            pbMinimum(ProgressBar1, 0)
            pbMaximum(ProgressBar1, ToSend.Length - 1)
            pbValue(ProgressBar1, 0)

            'Cosa fa:
            ' - Invio messaggio per iniziare la comunicazione
            ' - Mi metto in ascolto dell'ACK
            CAN_TmpMsg(_NPACKET) = HexToByte("FF")
            CAN_TmpMsg(_SIZEFW_HIGH) = HexToByte("FF")
            CAN_TmpMsg(_SIZEFW_LOW) = HexToByte("FF")
            CAN_TmpMsg(_NPACKETSRX_HIGH) = HexToByte("FF")
            CAN_TmpMsg(_NPACKETSRX_LOW) = HexToByte("FF")
            CAN_TmpMsg(_CHECKSTART) = HexToByte("FF")

            While (keepStarting)
                If CAN_write(CAN_TmpMsg) Then
                    Debug("Connecting to target device....")
                Else
                    Throw New Exception("Failed to send initial communication request.")
                End If

                CAN_Receive_Th = New Thread(AddressOf Thread_Receive)
                CAN_Receive_Th.Start()
                CAN_Receive_Th.Join()

                If CAN_ReceiveMsg(_NPACKET) = 255 Then
                    Throw New Exception("Target did not acknowledge the flash request.")
                Else
                    Debug("Target ready. Start sending packets")
                    keepStarting = False
                End If
            End While

            'Cosa fa:
            ' - Esegue algoritmo
            Result = CAN_Algoritmo1()

            'Cosa fa:
            ' - Verifica che non ci sia una bad write
            If Not Result Then
                Throw New Exception("Firmware write failed during transmission (Algoritmo1 returned False).")
            Else
                Debug("All bytes sent")
                Debug("FLASH COMPLETED SUCCESSFULLY")
                MessageBox.Show("Firmware flash completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If

        Catch ex As Exception
            If Not TypeOf ex Is Threading.ThreadAbortException Then
                Debug($"ERROR during firmware flash: {ex.Message}")
                MessageBox.Show(ex.Message, "Flash Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Finally

            isCANTalking = False
            Debug("Firmware flash thread finished. Heartbeat listener resumed.")

            ' Riabilitiamo i pulsanti di input.
            ' Il listener si occuperà di gestire lo stato di Button2.
            btnEnabled(Button1, True)
            btnEnabled(Button3, False)
            pbValue(ProgressBar1, 0)

            ' La chiamata originale a CAN_disconnect() è stata rimossa da qui.
        End Try

        'COSA DEVE FARE IN TOTALE
        'Setta variabili

        'Inizia comunicazione
        'Aspetta ACK

        'FOR
        'Invio pacchetto
        'Aspetto ACK
        'END FOR
    End Sub
    Public Sub Thread_Receive()
        'THREAD CHE GESTISCE LA RICEZIONE DEI PACCHETTI
        Dim CANMsg As TPCANMsg = Nothing
        Dim CANTimeStamp As TPCANTimestamp
        Dim stsResult As TPCANStatus
        Dim timeStart As Long = CLng(DateTime.UtcNow.Subtract(New DateTime(1970, 1, 1)).TotalMilliseconds)

        Array.Clear(CAN_ReceiveMsg, 0, CAN_ReceiveMsg.Length)
        CAN_ReceiveMsg(0) = 255

        Do
            Try
                stsResult = PCANBasic.Read(CAN_Handle, CANMsg, CANTimeStamp)
                If stsResult = TPCANStatus.PCAN_ERROR_OK And Hex(CANMsg.ID) = CAN_ID Then
                    'Abbiamo un nuovo messaggio
                    CAN_ReceiveMsg = CANMsg.DATA
                    Return
                Else

                End If
            Catch ex As Exception
                Array.Clear(CAN_ReceiveMsg, 0, CAN_ReceiveMsg.Length)
                Exit Sub
            End Try
        Loop While (250 > CLng(DateTime.UtcNow.Subtract(New DateTime(1970, 1, 1)).TotalMilliseconds) - timeStart)

        
        Debug("Timeout ACK (" & 250 / 1000 & "s) expired")

        'Faccio "partire un timer" per gestire il timeout
        'Mi metto in ascolto di un pacchetto
        'Inserisco il pacchetto ricevuto nella variabile CAN_ReceiveMsg

        'Se supero il timeout abortisco il thread
    End Sub

    'ALGORITMI
    Public Sub CAN_Algoritmo1_Parameters()
        'ALGORITMO 1 - Invia senza ACK
        NByteForControl = 1
        NByteForData = 7
        CAN_CycleTime = 10000
        'END ALGORITMO 1
    End Sub
    Public Function CAN_Algoritmo1()
        Try
            For i = 0 To ToSend.Length - 1
                For j = 0 To ToSend(i).Packet.Length - 1
                    'Svuoto array CAN_TmpMsg
                    Array.Clear(CAN_TmpMsg, 0, CAN_TmpMsg.Length)
                    NPacchetto = j

                    'Preparazione messaggio (CAN_TmpMsg)
                    CAN_TmpMsg = ToSend(i).Packet(j).Data.Clone

                    'Invia pacchetto e se non lo invia:
                    ' - Reinvio se non ho superato MAXATTEMPT
                    ' - Esco se ho superato il numero massimo di tentativi
                    If Not CAN_write(CAN_TmpMsg) Then
                        If CAN_Attempt < _MAXATTEMPT Then
                            j -= 1
                            CAN_Attempt += 1
                            Debug("Packet #" & NPacchetto & " not sent. Retry in " & CAN_CycleTime & "s. " & CAN_Attempt & "/" & _MAXATTEMPT & " attempt/s")
                            Threading.Thread.Sleep(CAN_CycleTime)
                            Continue For
                        Else
                            Debug("Max number of attempts (" & _MAXATTEMPT & ") exceeded. Transmission aborted.")
                            'Non sono riuscito a inviare il pacchetto N-esimo
                            btnEnabled(Button1, True)
                            btnEnabled(Button2, True)
                            Debug("Packet #" & NPacchetto & " not sent")
                            'CAN_disconnect()
                            Return False
                        End If
                    End If

                    CAN_Attempt = 0
                    Debug("Packet #" & NPacchetto & " sent: " & CAN_TmpMsg(0) & " " & CAN_TmpMsg(1) & " " & CAN_TmpMsg(2) & " " & CAN_TmpMsg(3) & " " & CAN_TmpMsg(4) & " " & CAN_TmpMsg(5) & " " & CAN_TmpMsg(6) & " " & CAN_TmpMsg(7) & " . Waiting for ACK")

                    'Waiting for ACK
                    CAN_Receive_Th = New Thread(AddressOf Thread_Receive)
                    CAN_Receive_Th.Start()
                    CAN_Receive_Th.Join()

                    'Cosa fa:
                    ' - Gestisco il messaggio ricevuto
                    ' - Esco se l'ACK non è corretto
                    If CAN_ReceiveMsg(_NPACKET) = 255 Then
                        'Perchè è dentro qui?
                        ' - ACK non ricevuto
                        j = -1
                        Debug("ACK not received")
                    ElseIf (NPacchetto < ToSend(i).Packet.Length - 1 And CAN_ReceiveMsg(_NPACKET) <> NPacchetto + 1) Or (NPacchetto = ToSend(i).Packet.Length - 1 And CAN_ReceiveMsg(_NPACKET) <> 0) Then
                        'Perchè è dentro qui?
                        ' - ACK non corrisponde
                        '(Es. Invia ACK3 e noi ci aspettiamo ACK5, quindi reinviamo i pacchetti dal #3)
                        j = -1

                        Debug("Target sent ACK#" & CAN_ReceiveMsg(_NPACKET) & ". Host requires " & IIf(NPacchetto = ToSend(i).Packet.Length - 1, "ACK#0", "ACK#" & NPacchetto + 1))
                    Else
                        'Aumento variabili
                        Debug("ACK#" & CAN_ReceiveMsg(_NPACKET) & " received.")
                        pbValue(ProgressBar1, i)
                    End If
                Next
                Debug("Block#" & i & " sent properly.")
            Next

            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Sub ConfigureTraceFile()
        Dim iBuffer As UInt32
        Dim stsResult As TPCANStatus

        ' Configure the maximum size of a trace file to 5 megabytes
        '
        iBuffer = 5
        stsResult = PCANBasic.SetValue(CAN_Handle, TPCANParameter.PCAN_TRACE_SIZE, iBuffer, CType(System.Runtime.InteropServices.Marshal.SizeOf(iBuffer), UInteger))
        If stsResult <> TPCANStatus.PCAN_ERROR_OK Then
            Throw New Exception("Error in method: ConfigureTraceFile()")
        End If

        ' Configure the way how trace files are created: 
        ' * Standard name is used
        ' * Existing file is ovewritten,
        ' * Only one file is created.
        ' * Recording stopts when the file size reaches 5 megabytes.
        '
        iBuffer = PCANBasic.TRACE_FILE_SINGLE Or PCANBasic.TRACE_FILE_OVERWRITE
        stsResult = PCANBasic.SetValue(CAN_Handle, TPCANParameter.PCAN_TRACE_CONFIGURE, iBuffer, CType(System.Runtime.InteropServices.Marshal.SizeOf(iBuffer), UInteger))
        If stsResult <> TPCANStatus.PCAN_ERROR_OK Then
            Throw New Exception("Error in method: ConfigureTraceFile()")
        End If
    End Sub
#End Region
#End Region


    Private Sub Button3_Click(sender As System.Object, e As System.EventArgs) Handles Button3.Click
        Debug("Stop button pressed. Aborting active operation...")
        btnEnabled(Button3, False) ' Disabilita subito se stesso

        ' Interrompi il thread di flashing del firmware, se è attivo
        If TypeOf CAN_Send_Th Is Thread AndAlso CAN_Send_Th.IsAlive Then
            CAN_Send_Th.Abort()
            ' Il blocco Finally di Thread_Send si occuperà della pulizia.
        End If

        ' Interrompi il thread di upload DWIN, se è attivo
        If TypeOf CAN_Send_DWIN_Th Is Thread AndAlso CAN_Send_DWIN_Th.IsAlive Then
            CAN_Send_DWIN_Th.Abort()
            ' Il blocco Finally di Thread_Send_DWIN si occuperà della pulizia.
        End If

    End Sub

    Private Sub Form1_KeyDown(sender As System.Object, e As System.Windows.Forms.KeyEventArgs) Handles MyBase.KeyDown
        If (e.KeyCode = Keys.A AndAlso e.Modifiers = Keys.Control) Then
            Button2.PerformClick()
        End If
    End Sub

    Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs) Handles TextBox1.TextChanged

    End Sub

    Private Sub Label5_Click(sender As Object, e As EventArgs) Handles Label5.Click

    End Sub

    Private Sub btnUploadDwin_Click(sender As Object, e As EventArgs) Handles btnUploadDwin.Click
        Dim zipFilePath As String = txtDwinZipPath.Text

        ' 1. Valida l'input dell'utente
        If String.IsNullOrWhiteSpace(zipFilePath) OrElse Not File.Exists(zipFilePath) Then
            MessageBox.Show("Please select a valid .zip file before proceeding.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Disabilita i controlli dell'interfaccia per prevenire operazioni multiple
        btnSelectZip.Enabled = False
        btnUploadDwin.Enabled = False
        ' Disabilita anche il pulsante dell'altra tab per sicurezza
        Button2.Enabled = False

        ' 2. Decomprime il file in una cartella temporanea
        Dim extractedFolderPath As String = UnzipToTempFolder(zipFilePath)

        ' 3. Controlla se il processo di decompressione è andato a buon fine
        If extractedFolderPath Is Nothing Then
            ' Se fallisce, riabilita l'interfaccia ed esci
            btnSelectZip.Enabled = True
            btnUploadDwin.Enabled = True
            If lblBoardStatus.Text = "CONNECTED" Then Button2.Enabled = True ' Riabilita il flash solo se la scheda è connessa
            Return
        End If

        ' 4. Chiama la funzione per creare la sequenza di comandi
        Debug("Starting CAN command sequence creation...")
        Dim commandSequence As List(Of TPCANMsg) = CreateDwinCommandSequence(extractedFolderPath)

        ' 5. Controlla il risultato
        If commandSequence Is Nothing Then
            ' Si è verificato un errore critico, il messaggio è già nel log
            MessageBox.Show("A critical error occurred while preparing commands. Please check the log.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            btnSelectZip.Enabled = True
            btnUploadDwin.Enabled = True
            If lblBoardStatus.Text = "CONNECTED" Then Button2.Enabled = True
            Return
        End If

        Debug($"Command sequence creation finished. {commandSequence.Count} messages are ready to be sent.")

        ' Se non sono stati generati comandi, non c'è nulla da fare e non avviamo il thread
        If commandSequence.Count = 0 Then
            MessageBox.Show("No commands were generated. Nothing to upload.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' Riabilita l'interfaccia e esci
            btnSelectZip.Enabled = True
            btnUploadDwin.Enabled = True
            If lblBoardStatus.Text = "CONNECTED" Then Button2.Enabled = True
            Return
        End If

        CAN_Send_DWIN_Th = New Thread(AddressOf Thread_Send_DWIN)
        CAN_Send_DWIN_Th.Start(commandSequence)
    End Sub

    Private Sub btnSelectZip_Click(sender As Object, e As EventArgs) Handles btnSelectZip.Click
        Using ofd As New OpenFileDialog()
            ofd.Title = "Seleziona file ZIP"
            ofd.Filter = "File ZIP (*.zip)|*.zip"
            ofd.InitialDirectory = ".\\"
            If ofd.ShowDialog() = DialogResult.OK Then
                txtDwinZipPath.Text = ofd.FileName
            End If
        End Using
    End Sub
End Class

