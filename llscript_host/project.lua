ProjectName = "llscript_host"
project(ProjectName)

  --Settings
  kind "StaticLib"
  language "C"
  flags { "StaticRuntime", "FatalWarnings" }
  dependson { llscript_asm }
  
  ignoredefaultlibraries { "msvcrt" }
  
  objdir "intermediate/obj"

  files { "src/**.c", "src/**.cpp", "src/**.h", "src/**.inl", "src/**rc" }
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
  editandcontinue "Off"
	flags { "NoFramePointer", "NoBufferSecurityCheck", "NoIncrementalLink" }

filter { "configurations:Debug*" }
  defines { "_DEBUG" }
  symbols "FastLink"

filter { "configurations:Release" }
	defines { "NDEBUG" }
	optimize "Speed"
  symbols "On"
