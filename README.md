# AudioRenderUnity
Provides a new renderer for Unity that translated 3D scene to audio signal.

Plugin is based on AudioRender: https://github.com/tikonen/AudioRender

Use DACDriver when translating the sound from PC to oscilloscope for more stable rendering: https://github.com/tikonen/DACDriver

Features:
 - Audio rendering (oscilloscope)
 - Emulator renderer (external included program)
 - Shader rendering (Unity)
 - Signal scaling (for oscilloscopes)
 - Signal intensity
 - Global random line offset
 - Line clipping
 - Edge angle limit
 - Backface culling
 - Occlusion culling
 - Lods
 - Static geomery
 - Skinned geometry
 - Example scenes
  - Clipping (multiple overlapping objects)
  - Cube (basic rendering)
  - Effect (explosion)
  - RenderDevice (direct drawing for custom routines)
  - Skinned (animation)
  - Triangles (lods)

Requirements:
 - Static Batching: Off
 - Dynamic Batching: Off
 - Scripting Backend: IL2CPP
 - Api Compatibility Level: .NET 4.x
 - Packages: Burst, Collections, Mathematics
 - Read/Write enabled from Unity is required for meshes
 - Set PC audio to Windows Default Audio Device (oscilloscope rendering only)
 - Set oscilloscope audio to Window Default Communications Device (oscilloscope rendering only)

Performance tips:
 - Try to keep line count as low as possible
 - Do not split model edges as this doubles the line count (use smooth faces)
 - Use high edge angle limits on wireframe objects to skip lines in geometry
 - Break static geomery into small sections for baked occlusion culling
 - Keep camera far clipping plane as low as possible
 - Use lods on distant objects
 
Command line arguments for builds:
 -audioRender (render to oscilloscope)
 -emulator (render to external emulator)
 -gfx (render oscilloscope shader in Unity)
 -scaleX [int] (scale signal for oscilloscopes)
 -scaleY [int] (scale signal for oscilloscopes)