ProjectName = "llscript_host"
project(ProjectName)

  --Settings
  kind "StaticLib"
  language "C"
  flags { "FatalWarnings" }
  staticruntime "On"
  dependson { llscript_asm }
  
  ignoredefaultlibraries { "msvcrt" }
  
  objdir "intermediate/obj"

  files { "src/**.c", "src/**.cpp", "src/**.h", "src/**.inl", "src/**rc" }
  files { "project.lua" }
  
  includedirs { "src" }
  linkoptions { "../builds/lib/llscript_asm.lib" }

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
	flags { "NoBufferSecurityCheck", "NoIncrementalLink" }
  omitframepointer "On"

filter { "configurations:Debug*" }
  defines { "_DEBUG" }
  symbols "FastLink"

filter { "configurations:Release" }
	defines { "NDEBUG" }
	optimize "Speed"
  symbols "On"
