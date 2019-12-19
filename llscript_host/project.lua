ProjectName = "llscript_host"
project(ProjectName)

  --Settings
  kind "ConsoleApp"
  language "C"
  flags { "StaticRuntime", "FatalWarnings" }
  linkoptions { "/ENTRY:llshost_position_independent" }
  
  ignoredefaultlibraries { "msvcrt" }
  
  objdir "intermediate/obj"

  files { "src/**.c", "src/**.cpp", "src/**.h", "src/**.inl", "src/**rc" }
  files { "project.lua" }
  
  includedirs { "src" }

  targetname(ProjectName)
  targetdir "../builds/bin"
  debugdir "../builds/bin"
  
filter {}
configuration {}

warnings "Extra"

filter { }
  flags { "NoPCH" }
  exceptionhandling "Off"
  rtti "Off"
  floatingpoint "Fast"
	flags { "NoFramePointer", "NoBufferSecurityCheck", "NoIncrementalLink" }

filter { "configurations:Debug*" }
  defines { "_DEBUG" }
  symbols "FastLink"

filter { "configurations:Release" }
	defines { "NDEBUG" }
	optimize "Speed"
  editandcontinue "Off"
  symbols "On"
