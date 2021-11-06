  ProjectName = "llsc"
project(ProjectName)

  --Settings
  kind "ConsoleApp"
  language "C#"
  flags { "FatalWarnings" }
  
  objdir "intermediate/obj"

  files { "src/**.cs" }
  files { "project.lua" }
  
  includedirs { "src" }

  targetname(ProjectName)
  targetdir "../builds/bin"
  debugdir "../builds/bin"
  
filter {}
configuration {}

warnings "Extra"
