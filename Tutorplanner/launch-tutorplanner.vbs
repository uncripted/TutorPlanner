Option Explicit

Dim shell, fileSystem, projectRoot, appPath, databasePath
Set shell = CreateObject("WScript.Shell")
Set fileSystem = CreateObject("Scripting.FileSystemObject")

projectRoot = fileSystem.GetParentFolderName(WScript.ScriptFullName)
appPath = projectRoot & "\bin\Release\net10.0\win-x64\publish\Tutorplanner.exe"
databasePath = projectRoot & "\tutorplanner.db"

If Not fileSystem.FileExists(appPath) Then
    shell.Popup "Tutorplanner has not been published yet. Build the project first.", 8, "Tutorplanner", 48
    WScript.Quit 1
End If

shell.CurrentDirectory = fileSystem.GetParentFolderName(appPath)
shell.Environment("Process")("TUTORPLANNER_DATABASE_PATH") = databasePath
shell.Run Chr(34) & appPath & Chr(34) & " --urls http://0.0.0.0:5167", 0, False

WScript.Sleep 1500
shell.Run "http://localhost:5167", 1, False
