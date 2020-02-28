solution "llscript"
  
  editorintegration "On"
  platforms { "x64" }
  configurations { "Debug", "Release" }

  dofile "llscript_host/project.lua"
  dofile "llscript_host_bin/project.lua"
  dofile "llscript_asm/project.lua"
  dofile "llscript_dbg/project.lua"
  dofile "llsc/project.lua"
  dofile "runsc/project.lua"
