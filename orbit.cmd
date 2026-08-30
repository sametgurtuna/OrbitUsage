@echo off
if exist "%~dp0publish\win-x64\Orbit.exe" (
    "%~dp0publish\win-x64\Orbit.exe" %*
) else (
    dotnet run --project "%~dp0src\Orbit\Orbit.csproj" -- %*
)
