git clean -ffdx
git submodule foreach --recursive "git clean -ffdx"

git fetch --all
git merge origin/master

IF %ERRORLEVEL% NEQ 0 (
  echo "merged with master failed"
  git merge --abort
  exit 1;
)

echo "merged successfully with master"

git submodule update --init

cd mediaLib
git submodule sync --recursive
IF %ERRORLEVEL% NEQ 0 (exit 1)

git submodule update --init --recursive
IF %ERRORLEVEL% NEQ 0 (exit 1)

"premake/premake5.exe" vs2019
IF %ERRORLEVEL% NEQ 0 (exit 1)

"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" llscript.sln /p:Configuration=Release /v:m
IF %ERRORLEVEL% NEQ 0 (exit 1)

cd buildscripts

IF NOT EXIST ".cookie" (
  powershell -command Expand-Archive "csi.zip" "csi"
  IF %ERRORLEVEL% NEQ 0 (exit 1)
  echo . > ".cookie"
) ELSE (
  echo Skipped Unzipping CSI. Cookie found.
)

"csi\csi.exe" unittest.csx
IF %ERRORLEVEL% NEQ 0 (exit 1)