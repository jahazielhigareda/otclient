# OTClient → C# + Raylib Migration Roadmap

> **Target stack:** C# 13 · .NET 10 · [Raylib-cs](https://github.com/chrisdill/raylib-cs) · Lua (via NLua / MoonSharp)
> **Source:** C++17/OpenGL engine with LuaJIT scripting

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Current Architecture Analysis](#2-current-architecture-analysis)
3. [Technology Mapping](#3-technology-mapping)
4. [Phase Breakdown](#4-phase-breakdown)
   - [Phase 0 – Foundation & Scaffolding](#phase-0--foundation--scaffolding)
   - [Phase 1 – Core Engine Loop](#phase-1--core-engine-loop)
   - [Phase 2 – Rendering System](#phase-2--rendering-system)
   - [Phase 3 – Input System](#phase-3--input-system)
   - [Phase 4 – Audio System](#phase-4--audio-system)
   - [Phase 5 – Networking System](#phase-5--networking-system)
   - [Phase 6 – Lua Scripting Integration](#phase-6--lua-scripting-integration)
   - [Phase 7 – UI Framework](#phase-7--ui-framework)
   - [Phase 8 – Game Client Logic](#phase-8--game-client-logic)
   - [Phase 9 – Resource & Asset Pipeline](#phase-9--resource--asset-pipeline)
   - [Phase 10 – Platform, Tooling & Polish](#phase-10--platform-tooling--polish)
5. [Dependency Map](#5-dependency-map)
6. [Raylib Integration Plan](#6-raylib-integration-plan)
7. [Lua Interop Plan](#7-lua-interop-plan)
8. [Risk Register & Blockers](#8-risk-register--blockers)
9. [Milestones & Checkpoints](#9-milestones--checkpoints)

---

## 1. Project Overview

### Current State

OTClient is a feature-complete, open-source Tibia game client written in **C++17** with an **OpenGL** rendering backend. It ships with a full **LuaJIT** scripting engine that drives the UI, game modules, and content definitions. The codebase is split into two major layers:

| Layer | Location | Purpose |
|---|---|---|
| C++ Engine | `src/framework/` | Rendering, input, audio, networking, Lua bindings, platform |
| C++ Client | `src/client/` | Game protocol, map, creatures, items, sprites |
| Lua Modules | `modules/` | UI layout, game features, styles, hotkeys, market, etc. |
| Lua Entry | `init.lua` | Bootstrap: discovers work-dir, loads modules |

### Migration Goals

- **Replace** the entire C++17 engine and client with idiomatic **C# on .NET 10**.
- **Replace** OpenGL + GLEW + GLFW rendering with **Raylib-cs**.
- **Replace** OpenAL-Soft + libvorbis audio with **Raylib's built-in audio engine**.
- **Preserve** all Lua modules and scripts without modification where possible.
- **Preserve** the binary Tibia protocol (game data formats, `.dat`, `.spr`, `.otb`).
- Achieve feature parity with the existing C++ client across Windows, Linux, and macOS.

---

## 2. Current Architecture Analysis

### Entry Point (`src/main.cpp`)

```
main()
 ├── g_platform.init(args)          // OS/window abstraction
 ├── g_resources.init()             // PhysFS virtual filesystem
 ├── g_app.init()                   // GraphicalApplication: window + OpenGL context
 ├── g_client.init()                // Game client subsystems
 ├── g_http.init()                  // HTTP client (cpp-httplib)
 ├── g_lua.safeRunScript("init.lua")// Boot the Lua world
 └── g_app.run()                    // Main loop: poll events → update → draw
```

### Major Subsystems

| Subsystem | C++ Location | Key Classes |
|---|---|---|
| **Rendering** | `src/framework/graphics/` | `GraphicContext`, `DrawPool`, `Texture`, `Shader`, `Painter`, `FrameBuffer`, `ParticleManager` |
| **Input** | `src/framework/input/` | `EventDispatcher`, `Mouse` |
| **Audio** | `src/framework/sound/` | `SoundManager`, `SoundSource`, `SoundChannel`, `OggSoundFile` |
| **Networking** | `src/framework/net/` | `Connection`, `Protocol`, `InputMessage`, `OutputMessage`, `ProtocolHttp` |
| **UI** | `src/framework/ui/` | `UIWidget`, `UILayout` (Anchor/FlexBox/Grid/Box), `UITextEdit`, `UIMap`, `UIItem` |
| **Lua Engine** | `src/framework/luaengine/` | `LuaInterface`, `LuaBinder`, `LuaValueCasts` |
| **Filesystem** | `src/framework/core/` | `ResourceManager` (PhysFS), `FileStream`, `Logger`, `EventDispatcher` |
| **Game Logic** | `src/client/` | `Game`, `Map`, `Tile`, `Creature`, `Item`, `Outfit`, `MapView`, `LightView`, `SpriteManager`, `ThingTypeManager` |

### External Dependencies to Replace or Remove

| C++ Dependency | Role | C# Replacement |
|---|---|---|
| OpenGL / GLEW / ANGLE | Rendering | **Raylib-cs** |
| GLFW (implicit via app) | Window / context | **Raylib-cs** (built-in) |
| OpenAL-Soft | Audio engine | **Raylib-cs** `Raylib.InitAudioDevice()` |
| libvorbis / libogg | OGG audio | Raylib built-in OGG support |
| ASIO | Async TCP/TLS | `System.Net.Sockets` + `System.Net.Security` |
| cpp-httplib | HTTP client | `System.Net.Http.HttpClient` |
| OpenSSL | TLS / crypto | `System.Security.Cryptography` |
| PhysFS | Virtual filesystem | Custom `IResourceProvider` (zip + folder) |
| LuaJIT | Scripting | **NLua** or **MoonSharp** |
| FreeType | Font rendering | Raylib-cs `LoadFont` / `LoadFontEx` |
| pugixml | XML parsing | `System.Xml` / `XDocument` |
| protobuf | Protocol buffers | `Google.Protobuf` (if needed) or manual |
| nlohmann-json | JSON | `System.Text.Json` |
| zlib / liblzma | Compression | `System.IO.Compression` |
| fmt | String formatting | C# interpolated strings / `string.Format` |
| utfcpp | Unicode | Built-in .NET `string` (UTF-16) |
| discord-rpc | Discord status | Discord GameSDK C# wrapper (optional) |
| abseil | Utilities | .NET BCL equivalents |
| parallel-hashmap | Hash maps | `System.Collections.Generic.Dictionary` |
| bshoshany-thread-pool | Thread pool | `System.Threading.ThreadPool` / `Task` |
| physfs | Asset packaging | Custom ZIP loader with `System.IO.Compression` |
| inih | INI config | Custom parser or `Microsoft.Extensions.Configuration` |
| stduuid | UUID generation | `System.Guid` |

---

## 3. Technology Mapping

### Raylib API Coverage

| OTClient Subsystem | Raylib-cs API |
|---|---|
| Window creation & management | `Raylib.InitWindow`, `SetTargetFPS`, `WindowShouldClose` |
| OpenGL draw calls / shaders | `Raylib.BeginShaderMode`, `LoadShader`, `Raylib.DrawTexturePro` |
| Texture loading & atlases | `Raylib.LoadTexture`, `LoadTextureFromImage`, `GenImageColor` |
| Render-to-texture (framebuffer) | `Raylib.LoadRenderTexture`, `BeginTextureMode` |
| Font rendering (FreeType) | `Raylib.LoadFontEx` (TTF), `DrawTextEx` |
| Particle effects | Custom particle system using `Raylib.DrawCircle` / textured quads |
| 2-D sprite batching | `Raylib.BeginDrawing` → `DrawTexturePro` calls |
| Audio initialization | `Raylib.InitAudioDevice` |
| Sound loading (OGG/WAV) | `Raylib.LoadSound`, `LoadMusicStream` |
| Audio playback & channels | `Raylib.PlaySound`, `PlayMusicStream`, `SetSoundVolume` |
| Keyboard input | `Raylib.IsKeyDown`, `IsKeyPressed`, `GetCharPressed` |
| Mouse input | `Raylib.GetMousePosition`, `IsMouseButtonDown` |
| Clipboard | `Raylib.GetClipboardText`, `SetClipboardText` |

### Lua Integration

| Current (C++) | Target (C#) |
|---|---|
| LuaJIT 5.1 embedded | **NLua** (LuaJIT via P/Invoke) _or_ **MoonSharp** (pure C# Lua 5.2) |
| `g_lua.registerClass<T>()` | `lua.RegisterObject` / custom `[LuaGlobal]` attributes |
| `LuaBinder` auto-registration | Reflection-based binder in C# |
| Lua userdata → C++ objects | C# objects passed as Lua `userdata` through NLua |
| `init.lua` entry point | Unchanged — loaded by C# engine on startup |
| All `modules/` Lua scripts | Unchanged — run as-is inside new engine |

---

## 4. Phase Breakdown

---

### Phase 0 – Foundation & Scaffolding

**Objective:** Set up the .NET 10 solution structure, establish the build pipeline, and validate that Raylib-cs can open a window on all target platforms.

**Deliverables:**
- `OTClient.sln` with project references
- CI pipeline (GitHub Actions) for Windows / Linux / macOS
- Blank Raylib-cs window displaying version string
- Coding conventions document

#### Tasks

| # | Task | Notes |
|---|---|---|
| 0.1 | Create solution: `OTClient.sln` with projects `OTClient.Core`, `OTClient.Client`, `OTClient.Launcher` | Mirrors current `framework/` vs `client/` split |
| 0.2 | Add `Raylib-cs` NuGet reference to `OTClient.Core` | Verify version supports .NET 10 |
| 0.3 | Choose Lua library: evaluate **NLua** vs **MoonSharp** | See Phase 6; add chosen library now |
| 0.4 | Implement `Program.cs` entry point matching `main.cpp` boot sequence | `Init → LoadLua → Run → Shutdown` |
| 0.5 | Set up GitHub Actions CI: build + unit test on win/linux/mac runners | Mirror existing vcpkg/CMake CI |
| 0.6 | Define project-wide conventions: namespaces (`OTClient.Framework.*`), nullable reference types ON, `global using` directives | |
| 0.7 | Add `.editorconfig` and `Directory.Build.props` for uniform build settings | |
| 0.8 | Document build & run instructions in `docs/build-csharp.md` | |

**Risks:** Raylib-cs native library packaging may differ per platform (see §8).

---

### Phase 1 – Core Engine Loop

**Objective:** Implement the application lifecycle (`Application`, `EventDispatcher`, `Logger`, `Clock`, `Timer`, `Scheduler`) that all other systems will use.

**Deliverables:**
- `Application` class with `Init / Run / Deinit / Terminate` lifecycle
- `EventDispatcher` with priority queue
- `Logger` with file + console output
- `Scheduler` for deferred/repeating tasks
- Unit-tested `Clock` and `Timer`

#### Tasks

| # | Task | Notes |
|---|---|---|
| 1.1 | Port `GraphicalApplication` → `Application` class wrapping Raylib window | `Raylib.InitWindow`, `SetTargetFPS`, `WindowShouldClose` |
| 1.2 | Implement `EventDispatcher` using `ConcurrentQueue<Action>` with priority | Maps to `src/framework/core/eventdispatcher.*` |
| 1.3 | Implement `Scheduler` using `System.Threading.Timer` or `PeriodicTimer` | Maps to `src/framework/core/scheduler.*` |
| 1.4 | Implement `Logger` with log levels (FATAL/ERROR/WARN/INFO/DEBUG) | Maps to `src/framework/core/logger.*` |
| 1.5 | Implement `Clock` / `Timer` wrappers around `Stopwatch` | |
| 1.6 | Implement `ConfigManager` (INI/JSON settings) replacing `inih` | `System.Text.Json` |
| 1.7 | Implement `ModuleManager` to load/unload C# and Lua modules | Maps to `src/framework/core/modulemanager.*` |
| 1.8 | Wire up main loop: `PollEvents → Update → Render` using Raylib's frame model | |
| 1.9 | Write unit tests for `EventDispatcher`, `Scheduler`, `Timer` | |

---

### Phase 2 – Rendering System

**Objective:** Implement a complete 2-D rendering layer on top of Raylib-cs that supports sprite batching, texture atlases, custom shaders, framebuffers, fonts, and particles.

**Deliverables:**
- `GraphicsContext` wrapping Raylib window + drawing state
- `Texture` and `TextureAtlas` types
- `DrawPool` / `DrawPoolManager` for batched draws
- `Shader` and `ShaderProgram` wrappers
- `FrameBuffer` (render-to-texture)
- `FontManager` and `BitmapFont`
- `ParticleManager` and `ParticleEffect`
- `Painter` (stateless draw call API)

#### Tasks

| # | Task | Notes |
|---|---|---|
| 2.1 | Create `GraphicsContext` — init/shutdown, viewport, clear color | `Raylib.BeginDrawing` / `EndDrawing` |
| 2.2 | Implement `Texture` wrapping `Raylib.Texture2D`; support load from file, memory bytes, and generated | `Raylib.LoadTextureFromImage`, `UnloadTexture` |
| 2.3 | Implement `TextureAtlas` — pack multiple sprites into one GPU texture | Port `src/framework/graphics/textureatlas.*` logic |
| 2.4 | Implement `DrawPool` — command list for batching `DrawTexturePro` calls | |
| 2.5 | Implement `DrawPoolManager` — manages pools per render layer (ground/items/creatures/UI) | |
| 2.6 | Implement `Shader` / `ShaderProgram` wrapping `Raylib.LoadShader` / `BeginShaderMode` | Port existing GLSL shaders to Raylib-compatible GLSL |
| 2.7 | Implement `FrameBuffer` using `Raylib.LoadRenderTexture` / `BeginTextureMode` | Maps to `src/framework/graphics/framebuffer.*` |
| 2.8 | Implement `FontManager` and `BitmapFont` using `Raylib.LoadFontEx` | Maps to `src/framework/graphics/fontmanager.*` |
| 2.9 | Implement `ParticleManager`, `Particle`, `ParticleEffect` using custom update + Raylib draw | Port `src/framework/graphics/particle.*` |
| 2.10 | Implement `LightView` — dynamic light rendering pass using shader + framebuffer | Port `src/client/lightview.*` |
| 2.11 | Implement `Painter` — stateless API for rect/sprite/text draws with color/opacity | |
| 2.12 | Implement image loading from `.png`, `.bmp`, `.jpg` via `Raylib.LoadImage` | |
| 2.13 | Write rendering integration tests: draw sprite to framebuffer, read pixels, compare | |

---

### Phase 3 – Input System

**Objective:** Implement keyboard, mouse, and clipboard event handling that dispatches strongly-typed events into the `EventDispatcher`.

**Deliverables:**
- `InputManager` polling Raylib key/mouse state each frame
- `KeyEvent`, `MouseEvent`, `TextInputEvent` value types
- Widget-level focus and hit-test routing

#### Tasks

| # | Task | Notes |
|---|---|---|
| 3.1 | Implement `InputManager.Poll()` — call each frame inside main loop | |
| 3.2 | Map Raylib `KeyboardKey` enum to OTClient `Key` enum | Port `src/framework/platform/platformevent.h` key codes |
| 3.3 | Implement `KeyEvent` (key down/up/repeat) dispatching | `Raylib.IsKeyPressed`, `IsKeyDown`, `IsKeyReleased` |
| 3.4 | Implement `TextInputEvent` for text entry | `Raylib.GetCharPressed` loop |
| 3.5 | Implement `MouseEvent` (move, button, scroll) dispatching | `Raylib.GetMousePosition`, `GetMouseDelta`, `GetMouseWheelMove` |
| 3.6 | Implement clipboard integration | `Raylib.GetClipboardText`, `SetClipboardText` |
| 3.7 | Implement cursor style changes (arrow, hand, ibeam) | `Raylib.SetMouseCursor` |
| 3.8 | Add keyboard shortcut / hotkey binding layer | Maps to `modules/game_hotkeys` behavior |
| 3.9 | Write unit tests for event routing and key mapping | |

---

### Phase 4 – Audio System

**Objective:** Implement sound playback, music streaming, channel mixing, and volume control using Raylib's built-in audio engine.

**Deliverables:**
- `SoundManager` — init/shutdown, master volume
- `SoundChannel` — per-channel volume control
- `SoundSource` — one-shot sound playback
- `MusicSource` — streaming OGG music
- `SoundEffect` — positional / 2-D audio (optional)

#### Tasks

| # | Task | Notes |
|---|---|---|
| 4.1 | Initialize audio device: `Raylib.InitAudioDevice` in `Application.Init` | |
| 4.2 | Implement `SoundManager` with `Init`, `Terminate`, `SetMasterVolume` | Maps to `src/framework/sound/soundmanager.*` |
| 4.3 | Implement `SoundChannel` wrapping a named group with volume multiplier | |
| 4.4 | Implement `SoundSource` for fire-and-forget sounds using `Raylib.LoadSound` + `PlaySound` | Maps to `src/framework/sound/combinedsoundsource.*` |
| 4.5 | Implement `MusicSource` for streaming music using `Raylib.LoadMusicStream` + `UpdateMusicStream` | Maps to `src/framework/sound/streamsoundsource.*` |
| 4.6 | Support OGG Vorbis natively (Raylib handles this) | No extra library needed |
| 4.7 | Expose audio controls to Lua: `g_sounds.play(file)`, `setVolume(ch, vol)` | |
| 4.8 | Write integration tests: load OGG, play, verify no crash | |

---

### Phase 5 – Networking System

**Objective:** Implement asynchronous TCP networking and HTTP support to replace ASIO, cpp-httplib, and OpenSSL.

**Deliverables:**
- `Connection` — async TCP read/write with `TcpClient`
- `InputMessage` / `OutputMessage` — binary packet serialization
- `Protocol` — base class for game protocol handlers
- `ProtocolGame` — Tibia game protocol
- `ProtocolHttp` — HTTP client

#### Tasks

| # | Task | Notes |
|---|---|---|
| 5.1 | Implement `Connection` using `System.Net.Sockets.TcpClient` with `async`/`await` | Maps to `src/framework/net/connection.*` |
| 5.2 | Add TLS support via `System.Net.Security.SslStream` | Replaces OpenSSL |
| 5.3 | Implement `InputMessage` — binary reader with LE/BE, XOR/XTEA decryption | Port `src/framework/net/inputmessage.*` |
| 5.4 | Implement `OutputMessage` — binary writer with encryption | Port `src/framework/net/outputmessage.*` |
| 5.5 | Implement `Protocol` abstract base class with `onRecv` dispatch table | Port `src/framework/net/protocol.*` |
| 5.6 | Implement `ProtocolGame` — full Tibia 12.x protocol parse + send | Port `src/client/protocolgame.*` (~3000 lines) |
| 5.7 | Implement `ProtocolGameSend` partial class with all outgoing packet methods | Port `src/client/protocolgamesend.cpp` |
| 5.8 | Implement `ProtocolGameParse` partial class with all incoming packet parsers | Port `src/client/protocolgameparse.cpp` |
| 5.9 | Implement `ProtocolHttp` using `System.Net.Http.HttpClient` | Replaces cpp-httplib |
| 5.10 | Implement XTEA cipher in C# | Security-critical; validate against known vectors |
| 5.11 | Implement RSA key exchange in C# | `System.Security.Cryptography.RSA` |
| 5.12 | Write protocol unit tests with captured packet fixtures | |

---

### Phase 6 – Lua Scripting Integration

**Objective:** Embed a Lua runtime into the C# engine, register all engine classes and globals, and verify that the existing `init.lua` and all `modules/` scripts run without modification.

**Deliverables:**
- `LuaInterface` C# class wrapping chosen Lua library
- `LuaBinder` — reflection-based auto-registration of C# types
- All `g_*` global singletons registered in Lua
- `init.lua` executes successfully
- All existing modules load without error

#### Tasks

| # | Task | Notes |
|---|---|---|
| 6.1 | Finalize Lua library choice: **NLua** (LuaJIT via P/Invoke, best performance) _or_ **MoonSharp** (pure C#, no native deps, Lua 5.2) | Recommendation: NLua for maximal Lua compatibility with existing scripts |
| 6.2 | Implement `LuaInterface` — init, shutdown, `DoFile`, `DoString`, error handling | Maps to `src/framework/luaengine/luainterface.*` |
| 6.3 | Implement `LuaBinder` — enumerate C# classes decorated with `[LuaBinding]` and register them | Replaces C++ template-based `LuaBinder` |
| 6.4 | Implement `LuaValueCasts` — marshal C# types ↔ Lua values (int, float, string, bool, Color, Point, Rect, etc.) | Port `src/framework/luaengine/luavaluecasts.*` |
| 6.5 | Register `g_app`, `g_logger`, `g_resources`, `g_dispatcher`, `g_scheduler`, `g_modules` globals | |
| 6.6 | Register `g_graphics`, `g_textures`, `g_fonts`, `g_drawpool` globals | |
| 6.7 | Register `g_keyboard`, `g_mouse` globals | |
| 6.8 | Register `g_sounds` global | |
| 6.9 | Register `g_http` global | |
| 6.10 | Register all `g_game`, `g_map`, `g_things`, `g_sprites`, `g_creatures`, `g_client` globals | Port `src/client/luafunctions.cpp` (~1000 lines) |
| 6.11 | Register all UI widget types: `g_ui`, `UIWidget`, `UITextEdit`, `UIMap`, `UIItem`, etc. | |
| 6.12 | Implement Lua coroutine / `addEvent` / `scheduleEvent` / `cycleEvent` in C# | Core to module event system |
| 6.13 | Run `init.lua` and trace all errors; fix each missing binding | Iterative — expect many cycles |
| 6.14 | Run all 50+ Lua modules; fix binding errors for each | |
| 6.15 | Write integration tests: execute representative Lua snippets and verify return values | |

---

### Phase 7 – UI Framework

**Objective:** Implement the complete widget-based UI system (OML/OTUI parser, layout engine, widget hierarchy) rendering through Raylib.

**Deliverables:**
- `UIWidget` base class with full property set
- Layout engines: `AnchorLayout`, `FlexBoxLayout`, `GridLayout`, `BoxLayout`
- Widget types: `UIButton`, `UITextEdit`, `UICheckBox`, `UIScrollBar`, `UIScrollArea`, `UITabBar`, `UIProgressBar`, `UIWindow`, `UILabel`, `UIMap`, `UIItem`, `UICreature`, `UISprite`, `UIMinimap`
- OML/OTUI style parser
- Widget event routing (focus, click, key, mouse)

#### Tasks

| # | Task | Notes |
|---|---|---|
| 7.1 | Implement `UIWidget` — box model: position, size, margin, padding, anchor, color, opacity | Port `src/framework/ui/uiwidget.*` |
| 7.2 | Implement `UIWidget` draw pipeline: background, border, children, foreground | |
| 7.3 | Implement `AnchorLayout` — anchor-based positioning (left/right/top/bottom/center/fill) | Port `src/framework/ui/uilayout.*` |
| 7.4 | Implement `FlexBoxLayout` | |
| 7.5 | Implement `GridLayout` | |
| 7.6 | Implement `BoxLayout` (horizontal / vertical) | |
| 7.7 | Implement OTUI (style) file parser — override per-widget visual properties | Maps to `src/framework/ui/uimanager.*` |
| 7.8 | Implement OTT (template) / OTM (map) parsers as needed | |
| 7.9 | Implement widget factory: `createWidget(type, parent)` callable from Lua | |
| 7.10 | Implement `UIButton` with hover/pressed/disabled states | |
| 7.11 | Implement `UITextEdit` with cursor, selection, clipboard | |
| 7.12 | Implement `UIScrollArea` + `UIScrollBar` | |
| 7.13 | Implement `UITabBar` | |
| 7.14 | Implement `UICheckBox` and `UIRadioButton` | |
| 7.15 | Implement `UIProgressBar` and `UIProgressRect` | |
| 7.16 | Implement `UIWindow` with drag, resize, close | |
| 7.17 | Implement `UILabel` with word wrap and color markup | |
| 7.18 | Implement `UIMap` — renders the game map as a UI widget | Port `src/client/uimap.*` |
| 7.19 | Implement `UIItem` — renders a Tibia item sprite | Port `src/client/uiitem.*` |
| 7.20 | Implement `UICreature` — renders a creature outfit | Port `src/client/uicreature.*` |
| 7.21 | Implement `UIMinimap` — renders minimap texture | Port `src/client/uiminimap.*` |
| 7.22 | Implement `UISprite` — renders raw sprite ID | |
| 7.23 | Wire input events → focused widget → Lua callbacks | |
| 7.24 | Write UI widget tests: layout calculations, event propagation | |

---

### Phase 8 – Game Client Logic

**Objective:** Port all game-specific C++ logic: the game state machine, map, tiles, creatures, items, animations, and light system.

**Deliverables:**
- `Game` singleton — online state, character info, game events
- `Map` — 3-D tile grid with floor management
- `Tile` — holds creatures, items, effects, walkability
- `MapView` — camera, viewport culling, draw order
- `Creature` / `Player` / `LocalPlayer`
- `Item` / `Container`
- `ThingType` / `ThingTypeManager` — loads `.dat` / appearances
- `SpriteManager` — loads `.spr` / appearances
- `Outfit` — character visual customization
- `Effect` / `Missile` / `AnimatedText` / `StaticText`
- `Minimap`
- `LightView` — lighting overlay

#### Tasks

| # | Task | Notes |
|---|---|---|
| 8.1 | Implement `Position` — 3-D coordinate (x, y, z/floor) with direction helpers | Port `src/client/position.*` |
| 8.2 | Implement `Thing` abstract base (creatures, items, effects all inherit) | Port `src/client/thing.*` |
| 8.3 | Implement `ThingType` and `ThingTypeManager` — load appearances from `.dat` or Protobuf | Port `src/client/thingtype.*` |
| 8.4 | Implement `SpriteManager` — load `.spr` sprite sheets into `TextureAtlas` | Port `src/client/spritemanager.*` |
| 8.5 | Implement `SpriteAppearances` — load modern Tibia appearance protobuf format | Port `src/client/spriteappearances.*` |
| 8.6 | Implement `Item` and `ItemType` | Port `src/client/item.*`, `itemtype.*` |
| 8.7 | Implement `Outfit` — layers: body, head, legs, feet, addon 1, addon 2, mount | Port `src/client/outfit.*` |
| 8.8 | Implement `Creature` — walk animations, outfit rendering, health bar, name tag | Port `src/client/creature.*` |
| 8.9 | Implement `Player` and `LocalPlayer` — own character, stats, skills | Port `src/client/player.*`, `localplayer.*` |
| 8.10 | Implement `Tile` — stack of things, walkability, light emission | Port `src/client/tile.*` |
| 8.11 | Implement `Map` — floor layers `[0..15]`, creature/item tracking | Port `src/client/map.*` |
| 8.12 | Implement `MapView` — viewport, camera smooth scroll, draw-order sort | Port `src/client/mapview.*` |
| 8.13 | Implement `LightView` — additive light blend via shader + framebuffer | Port `src/client/lightview.*` |
| 8.14 | Implement `Effect` (animated spell effects on tiles) | Port `src/client/effect.*` |
| 8.15 | Implement `Missile` (projectile animated between two positions) | Port `src/client/missile.*` |
| 8.16 | Implement `AnimatedText` (damage/heal numbers floating up) | Port `src/client/animatedtext.*` |
| 8.17 | Implement `StaticText` (chat messages above creatures) | Port `src/client/statictext.*` |
| 8.18 | Implement `Minimap` — tile color lookup, map recording | Port `src/client/minimap.*` |
| 8.19 | Implement `Animator` — sprite frame sequencing | Port `src/client/animator.*` |
| 8.20 | Implement `AttachedEffect` and `AttachedEffectManager` | Port `src/client/attachedeffect.*` |
| 8.21 | Implement `Game` singleton — login, logout, online state, character list | Port `src/client/game.*` |
| 8.22 | Implement `Container` — inventory/backpack logic | Port `src/client/container.*` |
| 8.23 | Implement `Houses`, `Towns`, `Creatures` data managers | Port matching files in `src/client/` |
| 8.24 | Implement `GameConfig` — server-side configuration flags | Port `src/client/gameconfig.*` |
| 8.25 | Implement `PaperDoll` / `PaperDollManager` — equipment visual | Port `src/client/paperdoll.*` |

---

### Phase 9 – Resource & Asset Pipeline

**Objective:** Implement the virtual filesystem, asset encryption, and OTML configuration file parser.

**Deliverables:**
- `ResourceManager` — zip + folder VFS
- `FileStream` — read/write with compression
- `OTMLParser` / `OTMLDocument` / `OTMLNode` — config file format
- Asset encryption / decryption
- `.otb` (OTB item database) reader

#### Tasks

| # | Task | Notes |
|---|---|---|
| 9.1 | Implement `ResourceManager` using `System.IO.Compression.ZipArchive` for `.zip`/`.otpkg` assets | Replaces PhysFS |
| 9.2 | Implement fallback to plain filesystem for development | |
| 9.3 | Implement `FileStream` wrapper with zlib decompression via `System.IO.Compression.DeflateStream` | |
| 9.4 | Implement LZMA decompression via `System.IO.Compression` or `SharpCompress` (pure C#) | |
| 9.5 | Implement `OTMLParser` — parse `.otml`/`.otui`/`.otmod` configuration files | Port `src/framework/otml/` |
| 9.6 | Implement `OTMLDocument` and `OTMLNode` with full API | |
| 9.7 | Implement asset encryption/decryption (XOR + custom scheme) | Port `src/framework/core/resourcemanager.*` encryption |
| 9.8 | Implement `.otb` (OTB) binary item database reader | Port existing OTB reader logic |
| 9.9 | Implement module discovery: scan directories for `.otmod` files | Port `src/framework/core/modulemanager.*` |
| 9.10 | Write tests: load zip, read file, parse OTML, verify node values | |

---

### Phase 10 – Platform, Tooling & Polish

**Objective:** Final platform integration, editor tools, Discord RPC, performance profiling, and packaging.

**Deliverables:**
- Windows / Linux / macOS native builds
- Release packaging scripts
- Discord Rich Presence (optional)
- DAT/JSON dump tool
- Performance profile showing ≥60 FPS with full map visible

#### Tasks

| # | Task | Notes |
|---|---|---|
| 10.1 | Implement `Platform` class: command-line args, OS name, process restart | Port `src/framework/platform/platform.*` |
| 10.2 | Implement crash handler / exception → log file | |
| 10.3 | Implement Discord Rich Presence via C# Discord GameSDK wrapper | Optional; port `src/framework/discord/` |
| 10.4 | Implement DAT dump tool: `--dump-dat-to-json` CLI flag | Port `src/tools/datdump.*` |
| 10.5 | Implement proxy support in `Connection` | Port `src/framework/proxy/` |
| 10.6 | Profile rendering: measure frame time at 1920×1080 with full map | Target: ≤16 ms/frame |
| 10.7 | Optimize `DrawPool` batching — minimize state changes | |
| 10.8 | Implement memory pool for frequently allocated types (`Tile`, `Item`, `Creature`) | |
| 10.9 | Create publish scripts: `dotnet publish -r win-x64 -c Release --self-contained` | |
| 10.10 | Write end-to-end smoke test: connect to local OTServer, walk, interact | |
| 10.11 | Update `README.md` with new build instructions | |
| 10.12 | Audit and close all TODO/FIXME annotations | |

---

## 5. Dependency Map

```
Phase 0 (Foundation)
    └──► Phase 1 (Core Engine Loop)
             ├──► Phase 2 (Rendering)
             │        └──► Phase 7 (UI Framework)
             │                 └──► Phase 8 (Game Client Logic) ──► Phase 10
             ├──► Phase 3 (Input)
             │        └──► Phase 7
             ├──► Phase 4 (Audio)
             │        └──► Phase 8
             ├──► Phase 5 (Networking)
             │        └──► Phase 8
             ├──► Phase 6 (Lua Scripting)
             │        ├──► Phase 7 (UI bindings)
             │        └──► Phase 8 (game bindings)
             └──► Phase 9 (Resources)
                      └──► Phase 2 (texture loading)
                      └──► Phase 8 (data files)
```

### Critical Path

`Phase 0 → 1 → 6 → 7 → 8 → 10`

Lua must be functional before the UI, and the UI must work before the full game client can be validated end-to-end.

### Parallelizable Work

Once Phase 1 is done, Phases 2, 3, 4, 5, and 9 can proceed in parallel on separate branches:

| Stream A | Stream B | Stream C | Stream D |
|---|---|---|---|
| Phase 2 (Rendering) | Phase 3 (Input) | Phase 4 (Audio) | Phase 5 (Networking) |
| Phase 9 (Resources) | Phase 6 (Lua) | — | — |
| Phase 7 (UI) | — | — | — |
| Phase 8 (Game) | — | — | — |
| Phase 10 (Polish) | — | — | — |

---

## 6. Raylib Integration Plan

### Window & Lifecycle

```csharp
// Program.cs
Raylib.InitWindow(1280, 720, "OTClient");
Raylib.SetTargetFPS(60);
Raylib.InitAudioDevice();

while (!Raylib.WindowShouldClose())
{
    application.PollInput();
    application.Update(Raylib.GetFrameTime());

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    application.Render();
    Raylib.EndDrawing();
}

Raylib.CloseAudioDevice();
Raylib.CloseWindow();
```

### Rendering Architecture

```
Application.Render()
    ├── MapView.Draw()
    │       ├── DrawPool["ground"].Flush()      // ground tiles
    │       ├── DrawPool["items"].Flush()        // items on tiles
    │       ├── DrawPool["creatures"].Flush()    // creatures
    │       ├── DrawPool["effects"].Flush()      // effects/missiles
    │       └── LightView.Apply()               // light overlay shader
    └── UIManager.Draw()
            └── Root widget tree (recursive)
```

### Shader Pipeline

All existing GLSL shaders in `src/framework/graphics/shaders/` must be ported to **GLSL 330 core** (Raylib's default on desktop). Shaders are loaded at runtime:

```csharp
var lightShader = Raylib.LoadShader(null, "shaders/light.frag");
Raylib.BeginShaderMode(lightShader);
// draw light framebuffer
Raylib.EndShaderMode();
```

### Sprite Batching

The current C++ `DrawPool` is replicated in C# as a `List<DrawCommand>` sorted by texture ID to minimize state changes. Raylib's `DrawTexturePro` accepts source/dest rects and rotation, making it a direct replacement for the OpenGL quad draws.

### Font Rendering

`FontManager` uses `Raylib.LoadFontEx(path, size, codepoints, count)` for TrueType fonts and builds a `Texture2D` glyph atlas. Bitmap fonts stored in PNG sheets are loaded as textures and sampled with fixed glyph coordinates.

---

## 7. Lua Interop Plan

### Library Choice: NLua (Recommended)

**NLua** wraps the native LuaJIT or Lua 5.4 shared library via P/Invoke. It is the closest match to the existing LuaJIT integration:

- Full Lua 5.1/5.4 compatibility (existing scripts require no changes)
- Supports `userdata` (C# objects exposed to Lua)
- Supports `__index` / `__newindex` metamethods for property access
- Available on NuGet: `NLua`

**MoonSharp** is a pure-C# alternative. It has better sandboxing but only implements Lua 5.2 and has some compatibility gaps with LuaJIT-specific idioms used in OTClient modules.

### Binding Architecture

```csharp
// Attribute-driven auto-registration
[LuaBinding("g_game")]
public class Game : Singleton<Game>
{
    [LuaExport] public bool IsOnline { get; }
    [LuaExport] public void Walk(Direction dir) { ... }
}

// LuaBinder scans assemblies on startup
public class LuaBinder
{
    public void RegisterAll(Lua lua)
    {
        foreach (var type in Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<LuaBindingAttribute>() is { } attr)
                lua[attr.GlobalName] = Activator.CreateInstance(type);
        }
    }
}
```

### Type Marshalling

| C# Type | Lua Type | Notes |
|---|---|---|
| `int`, `float`, `double` | `number` | Direct |
| `string` | `string` | Direct |
| `bool` | `boolean` | Direct |
| `Color` (Raylib) | `table {r,g,b,a}` | Custom cast |
| `Point` (`Vector2`) | `table {x,y}` | Custom cast |
| `Rect` (`Rectangle`) | `table {x,y,width,height}` | Custom cast |
| `Position` | `table {x,y,z}` | Custom cast |
| C# object | `userdata` | NLua default |
| `Action` / delegate | `function` | NLua `LuaFunction` |

### Lua Entry Point

The existing `init.lua` is loaded verbatim:

```csharp
lua.DoFile(resourceManager.GetPath("init.lua"));
```

All `modules/` directories are added to Lua's `package.path` via `ResourceManager`. Each `.lua` file in the modules runs inside the same Lua state, identical to the current behavior.

### Event Bridge

Lua `addEvent`, `scheduleEvent`, and `cycleEvent` map to the C# `Scheduler`:

```csharp
// Lua: addEvent(function() ... end, 100)  --> schedules callback in 100ms
lua["addEvent"] = (LuaFunction fn, int ms) =>
    scheduler.Schedule(TimeSpan.FromMilliseconds(ms), () => fn.Call());
```

---

## 8. Risk Register & Blockers

| ID | Phase | Risk | Severity | Mitigation |
|---|---|---|---|---|
| R-01 | 0 | Raylib-cs native binaries not available for all target platforms on .NET 10 | High | Pin Raylib-cs to a tested version; build native libs from source if needed |
| R-02 | 2 | Custom GLSL shaders (light, outline, color-multiply) may not compile under Raylib's GLSL 330 profile | High | Audit all shaders before Phase 2 begins; port incrementally |
| R-03 | 2 | Sprite batching performance: C++ `DrawPool` used VAOS/VBOs directly; Raylib `DrawTexturePro` has overhead per call | Medium | Profile early; consider using Raylib's `rlgl` layer for direct GPU access if needed |
| R-04 | 5 | Tibia game protocol changes frequently; porting `protocolgame.cpp` (~5000 lines) is error-prone | High | Port with comprehensive unit tests against captured packet fixtures |
| R-05 | 5 | XTEA cipher must be bit-exact; any implementation error breaks all network communication | High | Validate against reference vectors; test with production server immediately |
| R-06 | 6 | Existing Lua modules use LuaJIT-specific extensions (bit library, FFI) that MoonSharp does not support | High | Use NLua (LuaJIT); avoid MoonSharp unless all Lua scripts are audited |
| R-07 | 6 | ~1000-line `luafunctions.cpp` registers hundreds of C++ functions; C# equivalents must match signatures exactly | High | Generate C# binding stubs from the C++ source automatically |
| R-08 | 7 | OTUI parser is a custom format; no existing C# library | Medium | Port the C++ parser directly; it is ~500 lines |
| R-09 | 8 | `.dat` and `.spr` Tibia binary formats differ across server versions (7.x → 12.x) | Medium | Preserve version-negotiation logic from `ThingTypeManager` |
| R-10 | 8 | `MapView` draw order with 16 floor layers, per-tile stacking, and light blending is complex | Medium | Port incrementally; validate visually against the C++ client screenshot-for-screenshot |
| R-11 | 10 | .NET GC pauses may cause frame drops during intensive gameplay (many allocations) | Medium | Use `ArrayPool<T>`, `Span<T>`, object pools; avoid allocations in hot path |
| R-12 | All | Binary compatibility with existing OTServer is mandatory; any break is a showstopper | Critical | Run integration tests against a real OTServer at every milestone |

---

## 9. Milestones & Checkpoints

| Milestone | Completing Phase | Testable Goal | Success Criteria |
|---|---|---|---|
| **M0 – Hello Raylib** | Phase 0 | Blank Raylib-cs window opens, shows FPS counter | Window opens at 1280×720; `git ci` passes on Windows + Linux + macOS |
| **M1 – Engine Core** | Phase 1 | `EventDispatcher`, `Scheduler`, `Logger` unit tests pass | All unit tests green; `dotnet test` exits 0 |
| **M2 – Sprite on Screen** | Phases 2, 9 | Load a Tibia `.spr` sprite sheet and render one item sprite | Correct sprite visible on screen; no rendering artifacts |
| **M3 – Input Functional** | Phase 3 | Keyboard and mouse events dispatched; clipboard works | Manual test: type in a text field, copy/paste |
| **M4 – Audio Plays** | Phase 4 | OGG ambient sound plays on loop; volume control works | Audio heard; Lua `g_sounds.play("ambient.ogg")` succeeds |
| **M5 – Network Connected** | Phase 5 | TCP connection established to OTServer; login packet sent/received | Login screen appears; character list received |
| **M6 – Lua Init** | Phase 6 | `init.lua` runs without error; all modules load | Zero Lua errors in log; `modules/` all load |
| **M7 – UI Renders** | Phase 7 | OTClient login screen renders correctly using Lua UI definitions | Visual match to C++ client login screen |
| **M8 – In-Game** | Phase 8 | Character logs in, map renders, player walks, items visible | Full walking session without crash; lighting correct |
| **M9 – Feature Parity** | Phase 8+6 | All `modules/` features work: inventory, spells, battle, market, containers | Manual QA checklist from existing module list passes |
| **M10 – Release Ready** | Phase 10 | Self-contained publish on all platforms; ≥60 FPS at 1920×1080 | Frame time ≤16 ms; no memory leaks over 30-minute session; installer works |

### Per-Milestone Checkpoint Procedure

1. **Build:** `dotnet build -c Release` exits 0 on all target RIDs.
2. **Unit tests:** `dotnet test` — all pass, no skips.
3. **Integration test:** Automated smoke test connects to local OTServer (CipSoft or compatible).
4. **Visual QA:** Screenshot comparison against reference C++ client renders.
5. **Performance:** Measure frame time with `Raylib.GetFrameTime()`, log P50/P99.
6. **Memory:** Run for 30 minutes; measure heap with `dotnet-counters`; no unbounded growth.

---

## Appendix A – Module Inventory

All 50+ Lua modules in `modules/` must be verified as working at **Milestone M9**:

| Module | Purpose |
|---|---|
| `corelib` | Foundation: math, table, string, widgets, keyboard/mouse |
| `gamelib` | Game protocol helpers, positions, creatures, items, spells |
| `modulelib` | Event control, watchlists |
| `client` | Server config, main client entry |
| `client_entergame` | Login/character selection UI |
| `client_serverlist` | Server browser |
| `client_options` | Options dialog |
| `client_styles` | Global style definitions |
| `client_terminal` | Debug terminal |
| `client_topmenu` / `client_bottommenu` | Menu bars |
| `game_interface` | Main game HUD |
| `game_inventory` | Inventory window |
| `game_containers` | Bag/backpack windows |
| `game_battle` | Battle list |
| `game_console` | Chat console |
| `game_hotkeys` | Hotkey binding UI |
| `game_spells` | Spell list |
| `game_skills` | Skills window |
| `game_healthinfo` | Health/mana bars |
| `game_cooldown` | Spell cooldown indicators |
| `game_minimap` | Minimap widget |
| `game_market` | In-game market |
| `game_imbuing` | Imbuing system |
| `game_forge` | Item forge |
| `game_cyclopedia` | Cyclopedia/bestiary |
| `game_attachedeffects` | Attached visual effects |
| `game_actionbar` | Action bar shortcuts |
| `game_analyser` | Damage analyser |
| `game_features` | Feature flag registry |
| `game_modaldialog` | Modal dialog system |
| `game_lootsplitter` | Loot distribution |
| `game_highscore` | Highscore board |
| `game_bugreport` | Bug report dialog |
| `game_joystick` | Gamepad support |
| `game_htmlsample` | HTML widget sample |

---

## Appendix B – File Count Reference

| C++ Location | Files | C# Target Namespace |
|---|---|---|
| `src/framework/graphics/` | ~40 | `OTClient.Framework.Graphics` |
| `src/framework/ui/` | ~60 | `OTClient.Framework.UI` |
| `src/framework/net/` | ~20 | `OTClient.Framework.Net` |
| `src/framework/sound/` | ~15 | `OTClient.Framework.Sound` |
| `src/framework/luaengine/` | ~10 | `OTClient.Framework.Lua` |
| `src/framework/core/` | ~25 | `OTClient.Framework.Core` |
| `src/framework/otml/` | ~8 | `OTClient.Framework.OTML` |
| `src/client/` | ~70 | `OTClient.Client` |
| **Total** | **~248** | |

---

*Generated: 2026-03-05 | Repository: jahazielhigareda/otclient*
