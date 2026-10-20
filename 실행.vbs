Set WshShell = CreateObject("WScript.Shell")
appDir = "C:\Users\ttoxe.P00A64274-008\.gemini\antigravity\scratch\LaserWelderApp\bin\Debug\net8.0-windows"
dotnetExe = "C:\Users\ttoxe.P00A64274-008\AppData\Local\Microsoft\dotnet\dotnet.exe"
dllPath = appDir & "\LaserWelderApp.dll"

WshShell.CurrentDirectory = appDir
WshShell.Run """" & dotnetExe & """ """ & dllPath & """", 1, False
