ProjectName = "llscript_dbg"
project(ProjectName)

  --Settings
  kind "ConsoleApp"
  language "C"
  flags { "FatalWarnings" }
  staticruntime "On"
  defines { "_CRT_SECURE_NO_WARNINGS", "LLS_DEBUG_MODE", "LLS_LOW_PERF_MODE" }
  dependson { llscript_host, llscript_asm }
  
  objdir "intermediate/obj"

  files { "src/**.c", "src/**.cpp", "src/**.h", "src/**.inl", "src/**rc", "../llscript_host/src/*.c" }
  files { "project.lua" }
  
  includedirs { "src" }
  includedirs { "../llscript_host/src" }
  links { "../builds/lib/llscript_asm.lib" }

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
	flags { "NoBufferSecurityCheck", "NoIncrementalLink" }
  omitframepointer "On"

filter { "configurations:Debug*" }
  defines { "_DEBUG" }
  symbols "FastLink"

filter { "configurations:Release" }
	defines { "NDEBUG" }
	optimize "Speed"
  symbols "On"
