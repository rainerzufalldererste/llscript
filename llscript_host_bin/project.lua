ProjectName = "llscript_host_bin"
project(ProjectName)

  --Settings
  kind "ConsoleApp"
  language "C"
  flags { "FatalWarnings" }
  staticruntime "On"
  dependson { llscript_host }
  
  ignoredefaultlibraries { "msvcrt" }
  
  objdir "intermediate/obj"

  files { "src/**.c", "src/**.cpp", "src/**.h", "src/**.inl", "src/**rc" }
  files { "project.lua" }

  links { "../builds/lib/llscript_host.lib" }
  links { "../builds/lib/llscript_asm.lib" }
  
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
