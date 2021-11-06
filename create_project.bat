@ECHO OFF

IF [%1]==[] GOTO MANUAL_CONFIG

IF "%1"=="1" GOTO ONE;
IF "%1"=="2" GOTO TWO;

ECHO INVALID PARAMETER (%1)

:MANUAL_CONFIG
ECHO 1. Visual Studio 2019 Solution
ECHO 2. Visual Studio 2015 Solution

CHOICE /N /C:12 /M "[1-2]:"

IF ERRORLEVEL ==2 GOTO TWO
IF ERRORLEVEL ==1 GOTO ONE
GOTO END

:TWO
 ECHO Creating VS2015 Project...
 premake\premake5.exe vs2015 %2 %3 %4 %5 %6 %7
 GOTO END

:ONE
 ECHO Creating VS2019 Project...
 premake\premake5.exe vs2019 %2 %3 %4 %5 %6 %7
 GOTO END

:END

