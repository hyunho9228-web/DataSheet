@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\Run-DataExport.ps1" --deploy
set "exportResult=%ERRORLEVEL%"
echo.
if "%exportResult%"=="0" (echo 데이터 배포가 완료되었습니다.) else (echo 배포하지 못했습니다. 위 오류를 확인해 주세요.)
pause
exit /b %exportResult%
