@echo off
setlocal
cd /d "%~dp0"

if exist "index.html" goto :serve
if exist "%~dp0..\Builds\WebGL\index.html" (
  cd /d "%~dp0..\Builds\WebGL"
  goto :serve
)
echo Cannot find WebGL index.html. Unzip MiaBasketball-WebGL.zip and put this script next to index.html.
exit /b 1

:serve
echo Serving %CD%
echo Open http://127.0.0.1:8080/
echo Press Ctrl+C to stop.
start "" "http://127.0.0.1:8080/"
where py >nul 2>&1 && py -m http.server 8080 && exit /b 0
where python >nul 2>&1 && python -m http.server 8080 && exit /b 0
where python3 >nul 2>&1 && python3 -m http.server 8080 && exit /b 0
echo Python not found. Install Python and retry.
exit /b 1
