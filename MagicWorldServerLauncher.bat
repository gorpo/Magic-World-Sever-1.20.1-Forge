@echo off
cd /d "%~dp0"
powershell -NoProfile -Sta -ExecutionPolicy Bypass -File "%~dp0MagicWorldServerLauncher.ps1"
