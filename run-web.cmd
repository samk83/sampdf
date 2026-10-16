@echo off
REM Drawing Compare web app: double-click to run. Close this window to stop.
cd /d "%~dp0"
dotnet run --project src\DrawingReview.Web -c Release
pause
