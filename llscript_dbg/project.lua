ProjectName = "llscript_dbg"
project(ProjectName)

  --Settings
  kind "ConsoleApp"
  language "C"
  flags { "StaticRuntime", "FatalWarnings" }
  defines { "_CRT_SECURE_NO_WARNINGS", "LLS_DEBUG_MODE" }
  dependson { llscript_asm, llscript_host }
  
  objdir "intermediate/obj"

  files { "src/**.c", "src/**.cpp", "src/**.h", "src/**.inl", "src/**rc", "../llscript_asm/src/*.asm", "../llscript_host/src/*.c" }
  files { "project.lua" }
  
  includedirs { "src" }
  includedirs { "../llscript_host/src" }

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
  editandcontinue "Off"
	flags { "NoFramePointer", "NoBufferSecurityCheck", "NoIncrementalLink" }

filter { "configurations:Debug*" }
  defines { "_DEBUG" }
  symbols "FastLink"

filter { "configurations:Release" }
	defines { "NDEBUG" }
	optimize "Speed"
  symbols "On"
