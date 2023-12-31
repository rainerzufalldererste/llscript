ProjectName = "runsc"
project(ProjectName)

  --Settings
  kind "ConsoleApp"
  language "C"
  flags { "FatalWarnings" }
  staticruntime "On"
  defines { "_CRT_SECURE_NO_WARNINGS" }
  
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
	flags { "NoBufferSecurityCheck", "NoIncrementalLink" }
  omitframepointer "On"
  editandcontinue "Off"

filter { "configurations:Debug*" }
  defines { "_DEBUG" }
  symbols "FastLink"

filter { "configurations:Release" }
	defines { "NDEBUG" }
	optimize "Speed"
  symbols "On"
