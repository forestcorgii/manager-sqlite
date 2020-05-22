
#Region "SQLite"
Imports System.IO
Imports System.Xml.Serialization
Imports System.Data.SQLite
Imports System.Windows.Forms

<XmlRoot("manager")> Public Class manager
    <XmlIgnore> Public Connection As New SQLiteConnection
    Public Password As String
    Public DatabaseDirectory As String
    Public DatabaseName As String

    Public Const SQLiteConfigFileExtension = ".sqlite.config.xml"
    Public ReadOnly Property DatabaseFullPath As String
        Get
            Return Path.Combine(DatabaseDirectory, DatabaseName) & ".db"
        End Get
    End Property

    Sub New()

    End Sub

    Sub New(_databaseDirectory As String, _databaseName As String, Optional _password As String = "", Optional openNow As Boolean = False)
        DatabaseDirectory = _databaseDirectory
        DatabaseName = _databaseName
        Password = _password
        If openNow Then
            Open()
        End If
    End Sub

    Public Function CloneConnection() As SQLiteConnection
        Dim con As SQLiteConnection = Nothing
        con = Connection.Clone
        Return con
    End Function

    Public Function Open(Optional addOns As String = "") As Boolean
        Return Open(DatabaseFullPath, Connection, Password, addOns)
    End Function

    Public Shared Function Open(_dbpath As String, ByRef _con As SQLiteConnection, Optional _password As String = "", Optional addOns As String = "") As Boolean
        Try
            _con = New SQLiteConnection(String.Format("Data Source={0};Password={1}{2}", _dbpath, _password, addOns))
            _con.Open()
            Return True
        Catch ex As Exception
            MessageBox.Show("Can't connect right now, Please try again.", "Error: SQL Database Connection", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
        End Try
        Return False
    End Function

    Public Sub Close()
        Connection.Close()
        Connection.Dispose()
    End Sub

#Region "Other Methods"
    Public Shared Function OpenEditor(_config As manager, Optional _connectionName As String = "Local", Optional _configFilePath As String = "") As manager
        Dim filePath As String = Path.Combine(_configFilePath, _connectionName & SQLiteConfigFileExtension)
        Dim newConnection As manager
        Dim settingsEditor As New SQLite_Configuration(_config, _connectionName, filePath)
        newConnection = settingsEditor.Config
        settingsEditor.ShowDialog()
        settingsEditor.Dispose()
        Return newConnection
    End Function

    Public Shared Function StartDefaultSetup(appLocation As String, Optional appName As String = "Local")
        Dim filePath As String = Path.Combine(appLocation, appName & SQLiteConfigFileExtension)
        If File.Exists(filePath) Then
            Return confsaver_xml.XmlSerialization.ReadFromFile(filePath, New manager)
        Else
            Return OpenEditor(New manager, appName, appLocation)
        End If
    End Function
#End Region


    Public Shared Function CreateSchema(filename As String)
        Try
            SQLiteConnection.CreateFile(filename)
            Return True
        Catch : Return False
        End Try
    End Function

    Public Sub TryCreateTable(ByVal tbl As String, ByVal flds As String(), Optional overwrite As Boolean = False)
        If CheckTable(tbl) Then
            If Not overwrite Then Exit Sub
            ExecuteQuery("DROP TABLE `" & tbl & "`")
        End If

        CreateTable(tbl, flds)
    End Sub
    Public Sub CreateTable(ByVal tbl As String, ByVal flds As String())
        CreateTable(tbl, flds, Connection)
    End Sub
    Public Shared Sub CreateTable(ByVal tbl As String, ByVal flds As String(), ByVal con As SQLiteConnection)
        Dim qry As String = String.Format("CREATE TABLE `{0}`(", tbl)
        For i As Integer = 0 To flds.Length - 1
            qry &= IIf(i = 0, flds(i), "," & flds(i))
        Next
        qry &= ")"

        ExecuteQuery(qry, con)
    End Sub

    Public Shared Sub AlterTablename(tablename As String, newTablename As String, con As SQLiteConnection)
        ExecuteDataReader("Alter Table `" & tablename & "` Rename To `" & newTablename & "`", con)
    End Sub

    Public Sub AlterTablename(tablename As String, newTablename As String)
        AlterTablename(tablename, newTablename, Connection)
    End Sub

    Public Function ExecuteDataReader(query As String) As SQLiteDataReader
        Return ExecuteDataReader(query, Connection)
    End Function

    Public Shared Function ExecuteDataReader(query As String, _con As SQLiteConnection) As SQLiteDataReader
        Return New SQLiteCommand(query, _con).ExecuteReader
    End Function

    Public Function ExecuteQuery(query As String) As Boolean
        Return ExecuteQuery(query, Connection)
    End Function

    Public Shared Function ExecuteQuery(query As String, _con As SQLiteConnection) As Boolean
        Try
            Dim command As New SQLiteCommand(query, _con)
            command.ExecuteNonQuery()
            command.Dispose()
            Return True
        Catch ex As Exception
            MsgBox(ex.Message & " ExecuteQuery")
            Return False
        End Try
    End Function

    Public Sub Insert(ByVal tbl As String, ByVal fld As String(), ByVal val As Object())
        Insert(tbl, fld, val, Connection)
    End Sub
    Public Shared Sub Insert(ByVal tbl As String, ByVal fields As String(), ByVal values As Object(), con As SQLiteConnection)
        Dim qry As String = String.Format("INSERT INTO `{0}` (", tbl)
        Dim valtype As String = ""

        For i As Integer = 0 To fields.Length - 1
            Dim f As String = fields(i)
            If f = fields(0) Then
                qry &= String.Format("`{0}`", f)
            Else
                qry &= String.Format(",`{0}`", f)
            End If
        Next

        qry &= ") VALUES("

        For i As Integer = 0 To values.Length - 1
            Dim v = values(i)
            valtype = TypeName(v)
            Select Case valtype
                Case "String"
                    qry &= String.Format("'{0}',", v)
                Case "Date"
                    qry &= String.Format("'{0}',", Date.Parse(v).ToString("yyyy-MM-dd HH:mm:ss"))
                Case Else
                    qry &= String.Format("{0},", v)
            End Select
        Next
        qry &= ")"
        qry = System.Text.RegularExpressions.Regex.Replace(qry, ",\)", ")")


        ExecuteQuery(qry, con)
    End Sub
    Public Sub Update(ByVal tbl As String, ByVal fld As String(), ByVal val As Object(), ByVal condition As Object())
        Update(tbl, fld, val, condition, Connection)
    End Sub
    Public Shared Sub Update(ByVal tbl As String, ByVal fields As String(), ByVal values As Object(), ByVal condition As Object(), ByVal con As SQLiteConnection)
        Dim qry As String = String.Format("UPDATE {0} SET ", tbl)
        Dim valtype As String = ""

        If fields.Length = values.Length Then
            For f As Integer = 0 To fields.GetUpperBound(0)
                valtype = TypeName(values(f))
                If f = 0 Then
                    If valtype = "String" Then
                        qry &= String.Format("[{0}]='{1}'", fields(f), values(f))
                    Else
                        qry &= String.Format("[{0}]={1}", fields(f), values(f))
                    End If
                Else
                    If valtype = "String" Then
                        qry &= String.Format(",[{0}]='{1}'", fields(f), values(f))
                    Else
                        qry &= String.Format(",[{0}]={1}", fields(f), values(f))
                    End If
                End If
            Next
        End If

        If Not condition Is Nothing Then
            If TypeName(condition(1)) = "String" Then
                qry &= String.Format(" WHERE {0} = '{1}'", condition(0), condition(1))
            Else
                qry &= String.Format(" WHERE {0} = {1}", condition(0), condition(1))
            End If
        End If

        ExecuteQuery(qry, con)
    End Sub

    Public Function ToDT(qry As String) As DataTable
        Return ToDT(qry, Connection)
    End Function
    Public Shared Function ToDT(qry As String, _con As SQLiteConnection) As DataTable
        Try
            Dim dt As New DataTable
            Dim command As New SQLiteDataAdapter(qry, _con)
            command.Fill(dt)
            command.Dispose()
            Return dt
        Catch ex As Exception
            MsgBox(ex.Message & " ExecuteQuery")
            Return Nothing
        End Try
    End Function

    Public Function CheckTable(tbl As String) As Boolean
        Return CheckTable(tbl, Connection)
    End Function
    Public Function GetTables() As List(Of String)
        Return GetTables(Connection)
    End Function
    Public Shared Function CheckTable(tbl As String, _con As SQLiteConnection) As Boolean
        Return GetTables(_con).Contains(tbl.ToLower)
    End Function
    Public Shared Function GetTables(_con As SQLiteConnection) As List(Of String)
        Dim lst As New List(Of String)
        Using rdr As SQLiteDataReader = ExecuteDataReader("SELECT `name` FROM sqlite_master WHERE type='table';", _con)
            While rdr.Read
                lst.Add(rdr.Item(0).ToString.ToLower)
            End While
        End Using
        Return lst
    End Function
End Class
#End Region

