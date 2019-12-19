solution "llscript"
  
  editorintegration "On"
  platforms { "x64" }
  configurations { "Debug", "Release" }

  dofile "llscript_host/project.lua"
  dofile "llscript_asm/project.lua"
  dofile "llsc/project.lua"
