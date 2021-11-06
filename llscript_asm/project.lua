ProjectName = "llscript_asm"
project(ProjectName)

  --Settings
  kind "StaticLib"
  language "C"
  flags { "FatalWarnings" }
  staticruntime "On"
  
  ignoredefaultlibraries { "msvcrt" }
  
  objdir "intermediate/obj"

  files { "src/**.c", "src/**.cpp", "src/**.h", "src/**.inl", "src/**rc", "src/*.asm" }
  files { "project.lua" }
  
  includedirs { "src" }

  targetname(ProjectName)
  targetdir "../builds/lib"
  debugdir "../builds/lib"
  
filter {}
configuration {}

warnings "Extra"

filter { }
  flags { "NoPCH" }
  exceptionhandling "Off"
  rtti "Off"
  floatingpoint "Fast"
	flags { "NoBufferSecurityCheck", "NoIncrementalLink" }
  omitframepointer "On"

filter { "configurations:Debug*" }
  defines { "_DEBUG" }
  symbols "FastLink"

filter { "configurations:Release" }
	defines { "NDEBUG" }
	optimize "Speed"
  editandcontinue "Off"
  symbols "On"
