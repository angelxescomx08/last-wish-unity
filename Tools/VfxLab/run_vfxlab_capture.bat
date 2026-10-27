@echo off
rem VFX Lab: builds the scene/prefabs and runs the capture PlayMode test in batch mode.
rem Close the Unity editor first (a project can only be open once). Results: TestResults\vfxlab\*.png,
rem TestResults\vfxlab.xml, Logs\vfxlab_build.log and Logs\vfxlab_capture.log.
setlocal
set "EDITOR=C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe"
pushd "%~dp0..\.."
set "PROJECT=%CD%"
if not exist "%EDITOR%" (echo Unity 6000.3.6f1 not found at %EDITOR% > TestResults\vfxlab_status.txt & echo done > TestResults\vfxlab_done.txt & popd & exit /b 1)
if exist TestResults\vfxlab_done.txt del TestResults\vfxlab_done.txt
echo building > TestResults\vfxlab_status.txt
start "" /wait "%EDITOR%" -batchmode -projectPath "%PROJECT%" -executeMethod LastWish.VfxLab.Editor.VfxLabBuilder.BuildBatch -quit -logFile "%PROJECT%\Logs\vfxlab_build.log"
echo build exit %ERRORLEVEL% > TestResults\vfxlab_status.txt
if "%1"=="build" goto end
start "" /wait "%EDITOR%" -batchmode -projectPath "%PROJECT%" -runTests -testPlatform PlayMode -testFilter LastWish.VfxLab.Tests -testResults "%PROJECT%\TestResults\vfxlab.xml" -logFile "%PROJECT%\Logs\vfxlab_capture.log"
echo capture exit %ERRORLEVEL% >> TestResults\vfxlab_status.txt
:end
echo done > TestResults\vfxlab_done.txt
popd
