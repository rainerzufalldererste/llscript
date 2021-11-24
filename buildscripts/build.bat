git clean -ffdx
git submodule foreach --recursive "git clean -ffdx"

git merge origin/master

IF %ERRORLEVEL% GEQ 1 (
  echo "merged with master failed"
  git merge --abort
  exit 1;
)

echo "merged successfully with master"

git submodule update --init

cd mediaLib
git submodule sync --recursive
IF %ERRORLEVEL% GEQ 1 (exit 1)

git submodule update --init --recursive
IF %ERRORLEVEL% GEQ 1 (exit 1)

cd ..

"premake/premake5" vs2019
IF %ERRORLEVEL% GEQ 1 (exit 1)

"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" llscript.sln /p:Configuration=Release /v:m
IF %ERRORLEVEL% GEQ 1 (exit 1)

cd buildscripts

IF NOT EXIST ".cookie" (
  powershell -command Expand-Archive "csi.zip" "csi"
  IF %ERRORLEVEL% GEQ 1 (exit 1)
  echo . > ".cookie"
) ELSE (
  echo Skipped Unzipping CSI. Cookie found.
)

csi/csi.exe unittest.csx
IF %ERRORLEVEL% GEQ 1 (exit 1)