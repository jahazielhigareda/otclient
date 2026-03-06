# C++ → C# Port Status

## 1. Overview

This repository contains two parallel implementations of the OTClient game client:

- **C++ implementation** (`src/`): The reference implementation and source of truth. It is a feature-complete, production-ready Tibia client (protocol 12.x/13.x+) built on a custom framework with OpenGL rendering, OpenAL audio, and an embedded Lua scripting engine.
- **C# port** (`csharp/`): A port-in-progress targeting .NET with Raylib-cs for rendering and MoonSharp for Lua scripting. The framework infrastructure (core, graphics context, networking primitives, UI skeleton, input, sound device, resource manager, Lua VM) is largely in place, but the actual gameplay layer—especially the Tibia binary protocol, map synchronization, entity management, item/container system, and combat—is early-stage or missing entirely.

**Overall porting status: Early-to-mid stage.** The framework scaffolding for nearly every subsystem exists in C#, but most subsystems are incomplete relative to what the C++ reference implements. The most critical gameplay gap is the network protocol: only 9 of 70+ parse handlers and 7 of 97+ send handlers have been ported, meaning that a real Tibia server connection cannot yet produce a functional in-game state.  

### High-level gap summary

| Area | Gap severity |
|------|-------------|
| Protocol (parse/send) | Critical – ~90% missing |
| Map / tile synchronization | Critical – missing |
| Entity / creature management | High – skeleton only |
| Item / container / inventory | High – missing |
| Combat & follow system | High – missing |
| Chat / channels / VIP | High – mostly missing |
| Trading / market / store | Critical – missing |
| Advanced features (forge, bestiary, wheel) | Critical – missing |
| Minimap | Medium – not started |
| Particle system | Low – basic physics ported |
| Sound | Medium – foundation ported |
| Lua bindings | Medium – framework exists, game bindings absent |

---

## 2. Subsystem alignment

| Subsystem | C++ Implementation | C# Implementation | Status | Notes |
|-----------|-------------------|-------------------|--------|-------|
| **Core / Application** | `src/framework/core/` — `Application`, `EventDispatcher`, `ModuleManager`, `Clock`, `Timer`, `Logger`, `ConfigManager`, `ResourceManager` | `csharp/src/OTClient.Core/` — `Application.cs`, `EventDispatcher.cs`, `ModuleManager.cs`, `Clock.cs`, `Timer.cs`, `Scheduler.cs`, `Logger.cs`, `ConfigManager.cs` | Partial | Core lifecycle classes exist; async dispatcher and garbage-collection helpers not ported |
| **Resource Manager / OTML** | `src/framework/core/resourcemanager.cpp`, `src/framework/otml/` — OTML parser, emitter, document | `csharp/src/OTClient.Core/Resources/` — `ResourceManager.cs`, `OTMLParser.cs`, `OTMLNode.cs`, `OtbReader.cs`, `AssetCipher.cs`, `CompressedStream.cs` | Partial | VFS (zip + folder) and OTML parsing ported; OTB reading present; binary-tree helpers and unzipper utilities not fully mirrored |
| **Networking** | `src/framework/net/` — `Connection`, `Protocol`, `InputMessage`, `OutputMessage`, `PacketRecorder`, `PacketPlayer`, `Server`; `src/client/protocolgame.h/.cpp`, `protocolgameparse.cpp`, `protocolgamesend.cpp` | `csharp/src/OTClient.Core/Net/` — `Connection.cs`, `Protocol.cs`, `ProtocolGame.cs`, `ProtocolGameParse.cs`, `ProtocolGameSend.cs`, `ProtocolHttp.cs`, `InputMessage.cs`, `OutputMessage.cs`, `RsaHelper.cs`, `XteaCipher.cs` | Partial | Connection primitives, RSA/XTEA crypto, HTTP protocol, and login sequence ported; only 9/70+ parse handlers and 7/97+ send handlers implemented |
| **Game state / Game manager** | `src/client/game.h/.cpp` — singleton with 60+ methods covering movement, items, containers, combat, channels, party, trading, quests, store, market | `csharp/src/OTClient.Core/Game/Game.cs` — `GameState` enum, `StartConnect`, `EnterGame`, `Logout`, `Update`, `SetCharacterList` | Partial | State machine and lifecycle wiring exist; all gameplay methods (movement, items, combat, channels, trading) absent |
| **Map / world** | `src/client/map.h/.cpp`, `mapio.cpp` — tile grid, creature spectators, pathfinding (`findPath`/`newFindPath`/`findPathAsync`), static/animated texts, light, minimap color | `csharp/src/OTClient.Core/Game/` — `MapTile.cs`, `MapView.cs` | Partial | Basic tile structure (`AddItem`, `AddCreature`) present; pathfinding, spectator queries, animated/static texts, and lighting integration absent |
| **Creature / entity** | `src/client/creature.h/.cpp`, `localplayer.h/.cpp`, `player.h/.cpp` — outfit, direction, walk animation, skull/shield/emblem, speed, health bars, light radius | `csharp/src/OTClient.Core/Game/Creature.cs` | Partial | Id, Name, Outfit, Direction, IsWalking, Health ported; skull/shield/emblem, speed/baseSpeed, animation phases, lighting, paperdolls absent |
| **Thing type / appearances** | `src/client/thing.h/.cpp`, `thingtype.h/.cpp`, `thingtypemanager.h/.cpp`, `spritemanager.h/.cpp`, `spriteappearances.h/.cpp` | `csharp/src/OTClient.Core/Game/ThingType.cs`, `DataManagers.cs`, `SpriteAppearances.cs` | Partial | Type definitions and data managers scaffolded; sprite sheet rendering and full appearance flag set not fully mirrored |
| **Item system** | `src/client/item.h/.cpp` — count/subtype, charges, duration, tier; `container.h/.cpp` — backpack management | `csharp/src/OTClient.Core/Game/GameObjects.cs` (items referenced); no Container class | Partial | Item object scaffolded in game objects; Container class entirely missing |
| **Map view / rendering** | `src/client/mapview.h/.cpp` — tile drawing, creature info overlay, floor visibility, shader integration, framing | `csharp/src/OTClient.Core/Game/MapView.cs` | Partial | MapView class exists and wires into Game; full tile draw pipeline, creature overlay, floor culling not implemented |
| **Graphics / draw pool** | `src/framework/graphics/` — `Painter`, `Texture`, `TextureAtlas`, `DrawPool`, `DrawPoolManager`, `FrameBuffer`, `Shader`, `FontManager`, `AnimatedTexture`, `ParticleSystem` (8 files) | `csharp/src/OTClient.Core/Graphics/` — `Painter.cs`, `Texture.cs`, `TextureAtlas.cs`, `DrawPool.cs`, `DrawPoolManager.cs`, `FrameBuffer.cs`, `Shader.cs`, `FontManager.cs`, `LightView.cs`, `ParticleSystem.cs` | Partial | All major classes present; batching in DrawPool not fully optimized; animated texture and APNG loading absent |
| **Light system** | `src/client/lightview.h/.cpp` — per-tile framebuffer, multiply composite, ambient + tile lights | `csharp/src/OTClient.Core/Graphics/LightView.cs` — `BeginCapture`, `EndCapture`, `Draw` with GLSL multiply shader | Partial | BeginCapture/EndCapture pattern ported; per-tile light source feed from map not integrated |
| **Particle system** | `src/framework/graphics/particle*.h/.cpp` — `ParticleSystem`, `ParticleEmitter`, `ParticleAffector`, `ParticleType`, `ParticleEffect` | `csharp/src/OTClient.Core/Graphics/ParticleSystem.cs` — `Particle`, `ParticleEmitter`, `ParticleEffect`, `ParticleManager` | Partial | Physics, color interpolation, and lifetime ported; `ParticleAffector` (gravity, damping, attractors) and `ParticleType` (visual appearance) classes not ported |
| **UI framework** | `src/framework/ui/` — `UIManager`, `UIWidget`, 5 layout engines, `UITextEdit`, `UITranslator`, `UIQRCode`, `UIParticles` | `csharp/src/OTClient.Core/UI/` — `UIManager.cs`, `UIWidget.cs`, `UILayouts.cs`, `UIWidgets.cs`, `UIClientWidgets.cs`, `UIWidgetFactory.cs`, `OtuiParser.cs`, `UITypes.cs` | Partial | Core widget model and AnchorLayout ported; text-edit, QR code, particle widget, HTML/CSS parser not ported |
| **Lua engine** | `src/framework/luaengine/` — `LuaInterface`, `LuaBinder`, `LuaObject`, `LuaValueCasts`; `src/client/luafunctions.cpp` — 100+ bindings for `g_game`, `g_map`, `g_things`, `g_sprites`, `g_minimap`, `g_creatures`, `g_houses` | `csharp/src/OTClient.Core/Lua/` — `LuaInterface.cs`, `LuaBinder.cs`, `LuaValueCasts.cs`, `LuaGlobals.cs`, `LuaGlobalProxies.cs`, `LuaGameProxies.cs`, `LuaBindingAttributes.cs` | Partial | MoonSharp VM integrated; event helpers (`scheduleEvent`, `cycleEvent`) present; game-object Lua bindings (`g_game` methods, `g_map`, `g_minimap`, container ops, combat) absent |
| **Sound system** | `src/framework/sound/` — OpenAL `SoundManager`, `SoundChannel`, `SoundEffect`, `SoundBuffer`, `StreamSoundSource`, `OggSoundFile`, `CombinedSoundSource`; protobuf soundbank loading (protocol 13+) | `csharp/src/OTClient.Core/Sound/` — `SoundManager.cs`, `SoundChannel.cs`, `SoundSource.cs`, `MusicSource.cs` | Partial | Raylib audio device, channels, basic play/music streaming ported; protobuf soundbank loading, OGG decoder wrapper, combined/stream sources, EAX/3D listener absent |
| **Input handling** | `src/framework/` — keyboard/mouse event system bound to Lua via `g_keyboard`/`g_mouse` | `csharp/src/OTClient.Core/Input/` — `InputManager.cs`, `InputEvents.cs`, `Key.cs` | Partial | InputManager and key mapping ported; Lua `g_keyboard`/`g_mouse` proxies registered but limited |
| **Minimap** | `src/client/minimap.h/.cpp` — `MinimapTile` (flags, color, speed), `MinimapBlock` (64×64), `draw`, `loadOtmm`/`saveOtmm`, `loadImage`/`saveImage`, tile coordinate conversions | `csharp/src/OTClient.Core/Game/` — Minimap property on `Game.cs` (type not expanded) | Missing | No minimap implementation in C# |
| **Houses** | `src/client/houses.h/.cpp` — `House`, `HouseManager`; OTBM XML I/O, tile/door management | Not implemented | Missing | Editor-only feature; no C# counterpart |
| **Animated text / Static text** | `src/client/animatedtext.h/.cpp`, `statictext.h/.cpp` — floating damage/XP text, static labels over creatures | Not implemented | Missing | Used by map rendering pipeline |
| **Missile / Effect** | `src/client/missile.h/.cpp`, `effect.h/.cpp` — projectile and tile effect animations | Not implemented | Missing | Required for combat visual feedback |
| **Paper doll / Attached effects** | `src/client/paperdoll.h/.cpp`, `attachableobject.h/.cpp`, `attachedeffect.h/.cpp`, `attachedeffectmanager.h/.cpp` | Not implemented | Missing | Required for equipment preview and visual effects on entities |
| **HTML/CSS parser** | `src/framework/html/` — HTML and CSS parser for rich-text UI | Not implemented | Missing | Used by news panels and in-game browser |
| **Platform / window** | `src/framework/platform/` — `PlatformWindow`, Win32/X11/Android/Browser windows | `csharp/src/OTClient.Core/Platform.cs` | Partial | Abstraction present; platform-specific backends rely on Raylib |
| **Proxy** | `src/framework/proxy/` — `Proxy`, `ProxyClient` for traffic routing | Not implemented | Missing | Optional networking feature |

---

## 3. Missing or incomplete features

### 3.1 Network Protocol

The Tibia protocol is by far the largest gap. `src/client/protocolgame.h` declares **97+ send methods** and **70+ parse methods**; the C# port has implemented only a small subset.

#### Missing parse handlers (`csharp/src/OTClient.Core/Net/ProtocolGameParse.cs`)

| Category | Missing handlers |
|----------|-----------------|
| Map synchronization | `parseMapDescription`, `parseFloorDescription`, `parseUpdateTile`, `parseTileAddThing`, `parseTileTransformThing`, `parseTileRemoveThing`, `parseMapMoveNorth/East/South/West`, `parseFloorChangeUp/Down` |
| Creature management | `parseCreatureMove`, `parseCreatureData`, `parseCreatureHealth`, `parseCreatureOutfit`, `parseCreatureSpeed`, `parseCreatureMana`, `parseCreatureSkull`, `parseCreatureShield`, `parseCreatureUnpass`, `parseCreatureMarks`, `parseCreatureType`, `parseCreatureTyping`, `parsePlayerHelpers` |
| Containers / inventory | `parseOpenContainer`, `parseCloseContainer`, `parseContainerAddItem`, `parseContainerUpdateItem`, `parseContainerRemoveItem`, `parseAddInventoryItem`, `parseRemoveInventoryItem` |
| NPC / trade | `parseOpenNpcTrade`, `parsePlayerGoods`, `parseCloseNpcTrade`, `parseOwnTrade`, `parseCounterTrade`, `parseCloseTrade` |
| Combat / status | `parseCancelWalk`, `parsePlayerStats`, `parsePlayerSkills`, `parsePlayerState`, `parsePlayerModes`, `parseAttachedEffect`, `parseDetachEffect`, `parseCreatureShader`, `parseMapShader` |
| Chat / channels | `parseTalk`, `parseChannelList`, `parseOpenChannel`, `parseOpenPrivateChannel`, `parseOpenOwnPrivateChannel`, `parseCloseChannel` |
| VIP | `parseVipAdd`, `parseVipState`, `parseVipLogout`, `parseBuddyGroupData` |
| Economy / market | `parseMarketEnter`, `parseMarketBrowse`, `parseMarketDetail`, `parseMarketOffers` |
| Advanced (13+) | `parseStore`, `parseStoreOffers`, `parseStoreError`, `parseStoreTransactionHistory`, `parseCompleteStorePurchase`, `parseCoinBalance`, `parseBlessings`, `parsePreyData`, `parseForgeResult`, `parseBestiaryRaces`, `parseBestiaryOverview`, `parseBestiaryMonsterData`, `parseOpenWheelWindow`, `parseImbuementDurations`, `parseRewardWall`, `parseBosstiary`, `parseCyclopediaItemDetail`, `parseHighscores`, `parseQuestLog`, `parseQuestLine`, `parseModalDialog`, `parseEditText`, `parseEditList`, `parseWorldTime`, `parseResourceBalance`, `parsePvpSituations` |

#### Missing send handlers (`csharp/src/OTClient.Core/Net/ProtocolGameSend.cs`)

Movement: `SendWalkNorthEast/SouthEast/SouthWest/NorthWest`, `SendStop`, `SendAutoWalk`, `SendGmTeleport`  
Items: `SendMove`, `SendUseItem`, `SendUseItemWith`, `SendUseOnCreature`, `SendRotateItem`, `SendOnWrapItem`, `SendLook`, `SendLookCreature`, `SendEquipItemWithTier`, `SendEquipItemWithCountOrSubType`  
Containers: `SendCloseContainer`, `SendUpContainer`, `SendRefreshContainer`, `SendSeekInContainer`, `SendBrowseField`  
Trade/NPC: `SendInspectNpcTrade`, `SendBuyItem`, `SendSellItem`, `SendCloseNpcTrade`, `SendRequestTrade`, `SendInspectTrade`, `SendAcceptTrade`, `SendRejectTrade`  
Combat: `SendAttack`, `SendFollow`, `SendCancelAttackAndFollow`, `SendChangeFightModes`  
Chat: `SendRequestChannels`, `SendJoinChannel`, `SendLeaveChannel`, `SendOpenPrivateChannel`, `SendCloseNpcChannel`, `SendTyping`  
Party: `SendInviteToParty`, `SendJoinParty`, `SendRevokeInvitation`, `SendPassLeadership`, `SendLeaveParty`, `SendShareExperience`, `SendPartyAnalyzerAction`  
VIP: `SendAddVip`, `SendRemoveVip`, `SendEditVip`, `SendEditVipGroups`  
Outfit/Mount: `SendRequestOutfit`, `SendChangeOutfit`, `SendMountStatus`  
Reports/Rules: `SendBugReport`, `SendRuleViolation`, `SendNewNewRuleViolation`  
Quests: `SendRequestQuestLog`, `SendRequestQuestLine`  
Economy: all market, store, stash, reward wall, prey, forge, imbuement, wheel, bestiary, boss, cyclopedia, highscore, quick-loot, inspection methods  

**C++ reference files:** `src/client/protocolgame.h`, `src/client/protocolgamesend.cpp`, `src/client/protocolgameparse.cpp`  
**C# files:** `csharp/src/OTClient.Core/Net/ProtocolGameSend.cs`, `csharp/src/OTClient.Core/Net/ProtocolGameParse.cs`

---

### 3.2 Game manager

**C++ reference:** `src/client/game.h/.cpp`  
**C# file:** `csharp/src/OTClient.Core/Game/Game.cs`

The C# `Game` class provides only the state-machine lifecycle. All of the following gameplay methods are absent:

- `Walk(Direction)`, `AutoWalk(positions)`, `ForceWalk`, `Turn(Direction)`, `Stop`
- `Look(Position, stackPos)`, `Move(item, destination, count)`, `Rotate(item)`, `Use(item)`, `UseWith`
- `Open(container)`, `Close(container)`, `OpenParent`, `RefreshContainer`, `SeekInContainer`
- `Attack(creature)`, `Follow(creature)`, `CancelAttack`, `CancelFollow`, `CancelAttackAndFollow`
- `Talk(message)`, `TalkChannel`, `TalkPrivate`, `SendTyping`
- `RequestChannels`, `JoinChannel`, `LeaveChannel`, `OpenPrivateChannel`, `CloseNpcChannel`
- `PartyInvite`, `PartyJoin`, `PartyLeave`, `PartyShareExperience`
- `AddVip`, `RemoveVip`, `EditVip`
- `InspectNpcTrade`, `BuyItem`, `SellItem`, `CloseNpcTrade`
- `RequestTrade`, `InspectTrade`, `AcceptTrade`, `RejectTrade`
- `RequestQuestLog`, `RequestQuestLine`
- `BuyStoreOffer`, `RequestStoreOffers`, `OpenStore`, `TransferCoins`, `OpenTransactionHistory`
- `MarketLeave`, `MarketBrowse`, `MarketCreateOffer`, `MarketAcceptOffer`
- `PreyAction`, `OpenPortableForge`, `ForgeRequest`, `ApplyImbuement`, `ClearImbuement`
- `OpenRewardWall`, `OpenWheelOfDestiny`, `ApplyWheelOfDestiny`
- `RequestBestiary`, `BuyCharmRune`, `CyclopediaRequestCharacterInfo`
- `QuickLoot`, `StashWithdraw`, `StashStow`
- All getters: `GetContainer()`, `GetVips()`, `GetChannels()`, `GetLocalPlayer()`, combat targets, etc.

---

### 3.3 Map, tile, and world

**C++ reference:** `src/client/map.h/.cpp`, `tile.h/.cpp`, `mapio.cpp`  
**C# files:** `csharp/src/OTClient.Core/Game/MapTile.cs`, `MapView.cs`

Missing from the C# map subsystem:

- **Pathfinding:** `findPath`, `newFindPath`, `findPathAsync` — no A* or waypoint system
- **Spectator queries:** `getSpectators`, `getSightSpectators`, `getSpectatorsInRange` — needed for range-based updates
- **Animated / static texts:** `addAnimatedText`, `addStaticText`, `removeStaticText` — floating damage/XP text
- **Coverage checks:** `isCovered`, `isCompletelyCovered`, `isLookPossible`, `isSightClear`
- **Aware range:** `setAwareRange`, `resetAwareRange`, `isAwareOfPosition`
- **Lighting integration:** `setLight`, `getLight` at map level
- **Tile elevation / drawElevation** — stackPos ordering and height offset for drawing
- **Minimap color** per tile (`getMinimapColor`)

---

### 3.4 Item and container system

**C++ reference:** `src/client/item.h/.cpp`, `container.h/.cpp`  
**C# files:** Item fields present in `GameObjects.cs`; no `Container` class

Missing:

- `Container` class with `getItem`, `getItems`, `getCapacity`, `getId`, `hasParent`, `isClosed`, `isUnlocked`, `hasPages`, `findItemById`
- Container event callbacks: `onOpen`, `onClose`, `onAddItem`, `onUpdateItem`, `onRemoveItem`
- Item properties: `charges`, `duration`, `tier`, `fluidType`, `actionId`, `uniqueId`
- Container open/close/navigation (up/parent) in `Game`

---

### 3.5 Creature details

**C++ reference:** `src/client/creature.h/.cpp`, `localplayer.h/.cpp`  
**C# file:** `csharp/src/OTClient.Core/Game/Creature.cs`

Missing creature attributes and behaviors:

- Skull, shield, emblem, guild emblem
- `baseSpeed`, `speed` (used for walk interval calculation)
- Animation phases (idle/walk/outfit animation)
- Light radius and color emitted by creature
- `paperdoll` integration
- `isPreWalking` / diagonal walk handling
- Attached effects and shaders
- Death animation state

---

### 3.6 Minimap

**C++ reference:** `src/client/minimap.h/.cpp`  
**C# files:** Not implemented

Entirely absent:

- `MinimapTile` (flags: seen, pathable, walkable; color; speed)
- `MinimapBlock` (64×64 tile grid, texture caching)
- `draw(position, rect, scale)` — render minimap viewport
- `updateTile(position, tile)` — sync from game tiles
- `loadOtmm`/`saveOtmm` — Tibia OTMM binary format
- `loadImage`/`saveImage` — PNG export/import
- Tile/pixel coordinate conversions

---

### 3.7 Combat visuals (Missile, Effect, AnimatedText, StaticText)

**C++ reference:** `src/client/missile.h/.cpp`, `effect.h/.cpp`, `animatedtext.h/.cpp`, `statictext.h/.cpp`  
**C# files:** Not implemented

All four classes are absent. These are required to display projectiles, tile effects, floating damage/experience text, and static labels rendered over creature heads.

---

### 3.8 Paper doll and attached effects

**C++ reference:** `src/client/paperdoll.h/.cpp`, `attachableobject.h/.cpp`, `attachedeffect.h/.cpp`, `attachedeffectmanager.h/.cpp`  
**C# files:** Not implemented

Paperdoll rendering (equipment overlay) and the attached-effect system (custom visual effects on creatures/items) are not present in the C# port.

---

### 3.9 Particle affectors and particle types

**C++ reference:** `src/framework/graphics/particleaffector.h/.cpp`, `particletype.h/.cpp`  
**C# file:** `csharp/src/OTClient.Core/Graphics/ParticleSystem.cs`

The `ParticleEmitter` class in C# supports configurable physics, but the modular `ParticleAffector` (gravity, wind, damping, color-over-time curves, attractors) and `ParticleType` (texture appearance, blending mode) classes are not ported. Existing particle behavior is therefore a subset of what the C++ system supports.

---

### 3.10 Lua game bindings

**C++ reference:** `src/client/luafunctions.cpp`  
**C# files:** `csharp/src/OTClient.Core/Lua/LuaGameProxies.cs`, `LuaGlobalProxies.cs`

The C++ `luafunctions.cpp` registers 100+ methods on the following Lua globals:

`g_game`, `g_map`, `g_things`, `g_sprites`, `g_minimap`, `g_creatures`, `g_houses`, `g_client`, plus class bindings for `Thing`, `Creature`, `Item`, `Tile`, `Container`, `Position`, `LocalPlayer`, `Outfit`.

In C# only `g_graphics`, `g_textures`, `g_fonts`, `g_drawpool`, `g_keyboard`, `g_mouse`, `g_game` (state-only), `g_map`, `g_things`, `g_sprites`, `g_creatures`, `g_client`, `g_ui` are exposed via `LuaGlobals.Register()`. The `g_game` proxy exposes only state and connection methods—none of the gameplay methods listed in §3.2. `g_minimap` and `g_houses` are absent.

---

### 3.11 Sound system

**C++ reference:** `src/framework/sound/soundmanager.h/.cpp`, `oggsoundfile.h/.cpp`, `streamsoundsource.h/.cpp`, `combinedsoundsource.h/.cpp`  
**C# files:** `csharp/src/OTClient.Core/Sound/SoundManager.cs`, `SoundChannel.cs`, `SoundSource.cs`, `MusicSource.cs`

Missing:

- Protobuf soundbank loading (`loadClientFiles`) introduced in protocol 13+
- `OggSoundFile` — manual OGG/Vorbis decoding (Raylib handles format, but soundbank references need mapping)
- `StreamSoundSource` — explicit streaming source lifecycle management
- `CombinedSoundSource` — layered audio (ambient + effect simultaneously)
- 3D positional audio: `setPosition` (listener), per-source position (Tibia uses 2D top-down)
- EAX reverb support (`isEaxEnabled`)

---

### 3.12 HTML/CSS parser

**C++ reference:** `src/framework/html/` — `HtmlParser`, `CssParser`, `HtmlManager`, `QuerySelector`  
**C# files:** Not implemented

Used for rich-text news panels and potential in-game browser content. Not started in C#.

---

## 4. Behavioral differences and risks

### 4.1 ⚠️ HIGH RISK — Protocol byte ordering and XTEA encryption

**C++ behavior:** `InputMessage` and `OutputMessage` in C++ use little-endian byte order consistently and apply XTEA decryption in 8-byte blocks over the entire payload after the 2-byte length prefix.

**C# behavior:** `XteaCipher.cs` implements XTEA decryption and the `ReadOnlySpan<uint>` indexing requires explicit int casts (`key[(int)(sum & 3)]`). The session must carefully enable XTEA only after `ParseInitGame` to match the C++ flow in `protocolgameparse.cpp`. Any ordering discrepancy will silently corrupt all subsequent packets.

**Risk:** If `ParseInitGame` does not set the cipher before the next server message is received, all subsequent packets will be undecipherable and the session will desynchronize.

---

### 4.2 ⚠️ HIGH RISK — Map state divergence

**C++ behavior:** Map tiles are updated incrementally via parse handlers (`parseMapDescription`, `parseUpdateTile`, `parseTileAddThing`, etc.). Missing any handler causes the client map to diverge permanently from server state.

**C# behavior:** No map-update parse handlers exist. The `Map` object (`MapTile` grid) is never populated from network data.

**Risk:** Any gameplay logic that reads tile or creature positions will operate on empty or stale data, leading to incorrect pathfinding, combat decisions, and UI displays.

---

### 4.3 ⚠️ HIGH RISK — Missing creature movement interpolation

**C++ behavior:** `Creature::walk()` manages walking animation via a scheduled timer, interpolates position smoothly between tiles, and updates the viewport.

**C# behavior:** `Creature.Walk()` uses a fixed 500 ms `Task.Delay` linear interpolation without integrating with the game's `Update` loop or the `EventDispatcher` timer system. Walking direction changes, diagonal movement, and pre-walking are not handled.

**Risk:** Creature positions in C# will appear jittery or incorrect at non-default server speeds.

---

### 4.4 MEDIUM RISK — Value-type vs reference-type semantics for `Position`

**C++ behavior:** `Position` is a plain struct (`uint16_t x, y; uint8_t z`) passed by value or const-ref.

**C# behavior:** `Position.cs` is a `struct` in C#. This is correct, but if any future code places `Position` in a `List<Position>` and attempts to mutate it through the list indexer (e.g., `positions[i].X = …`), it will silently mutate a copy, not the list element.

**Risk:** Low currently, but a common C# pitfall when porting C++ structs. All mutation should happen through assignment of a new struct value.

---

### 4.5 MEDIUM RISK — Event dispatcher and async concurrency model

**C++ behavior:** `EventDispatcher` and `ScheduledEvent` run on the main thread; all game state mutations are single-threaded in the render loop.

**C# behavior:** `EventDispatcher.cs` and `Scheduler.cs` use `Task`-based async. MoonSharp Lua is not thread-safe. If Lua callbacks are dispatched from background tasks, they may race with the Lua VM.

**Risk:** Lua scripts that read game state (creature positions, inventory) may observe torn state if parse handlers run on a background I/O thread without synchronizing back to the main-thread dispatcher.

---

### 4.6 MEDIUM RISK — Object lifetime and shared_ptr vs. C# GC

**C++ behavior:** `Thing`, `Creature`, `Item`, `Tile` are managed via `std::shared_ptr` chains. The map holds strong references to creatures; the protocol parser holds temporary references during parsing.

**C# behavior:** Reference types with GC. The risk is that intermediate objects (e.g., a `MapTile` returned by a parse handler) might be GC'd if the map grid does not hold a reference. No explicit weak-reference or pooling strategy is documented.

**Risk:** Medium. The GC is safe but may cause latency spikes in the draw/update loop if large tile grids or creature lists trigger generation-2 collections. No object pooling is present in the C# port.

---

### 4.7 LOW RISK — LINQ in hot paths

**C# behavior:** Several data queries (creature lists, tile item searches) will likely use LINQ when ported. LINQ allocates enumerators on each call.

**Risk:** Low at current state; becomes a concern once map drawing (called 60 times/second) and creature-info overlays are implemented. Prefer `List<T>.ForEach` or index-based loops in the hot render path.

---

### 4.8 LOW RISK — RSA key size assumption

**C++ behavior:** RSA is applied to a fixed-size 128-byte (1024-bit) block.

**C# behavior:** `RsaHelper.cs` implements the same algorithm. Ensure that the hardcoded RSA modulus matches the server's public key. Tibia's official client uses a well-known 1024-bit key; custom servers may use different keys.

---

## 5. Prioritized task list

| ID | Subsystem | Task | Impact | Priority | Notes |
|----|-----------|------|--------|----------|-------|
| T01 | Protocol | Implement `parseMapDescription` and `parseFloorDescription` to populate the `Map` tile grid from server data | High | 1 | Blocks all map rendering and gameplay |
| T02 | Protocol | Implement `parseTileAddThing`, `parseTileTransformThing`, `parseTileRemoveThing`, `parseUpdateTile` | High | 1 | Required for incremental map updates |
| T03 | Protocol | Implement `parseCreatureMove`, `parseCreatureData`, `parseCreatureHealth`, `parseCreatureOutfit`, `parseCreatureSpeed` | High | 1 | Required for seeing and interacting with other entities |
| T04 | Protocol | Implement `parseAddInventoryItem`, `parseRemoveInventoryItem`, `parseOpenContainer`, `parseCloseContainer`, `parseContainerAddItem`, `parseContainerUpdateItem`, `parseContainerRemoveItem` | High | 1 | Required for item and backpack management |
| T05 | Protocol | Implement `parsePlayerStats`, `parsePlayerSkills`, `parsePlayerState`, `parsePlayerModes` | High | 1 | Required to display player attributes and HUD |
| T06 | Game manager | Add `Walk`, `Turn`, `Stop`, `AutoWalk` to `Game.cs` + corresponding `SendWalk*`, `SendTurn*`, `SendStop`, `SendAutoWalk` in `ProtocolGameSend.cs` | High | 1 | Core movement loop |
| T07 | Map | Implement pathfinding (`FindPath` / `FindPathAsync`) in the C# `Map` class | High | 1 | Required for `AutoWalk` and NPC interaction |
| T08 | Game | Implement `Container` class mirroring `src/client/container.h` | High | 1 | Enables item management and trading |
| T09 | Protocol | Implement `parseTalk`, `parseChannelList`, `parseOpenChannel`, `parseOpenPrivateChannel`, `parseCloseChannel` | Medium | 2 | Chat system |
| T10 | Game manager | Add `Talk`, `TalkChannel`, `TalkPrivate`, `RequestChannels`, `JoinChannel`, `LeaveChannel` to `Game.cs` | Medium | 2 | Chat system completion |
| T11 | Protocol | Implement `parseVipAdd`, `parseVipState`, `parseVipLogout` | Medium | 2 | VIP/friends list |
| T12 | Game manager | Add `Attack`, `Follow`, `CancelAttack`, `CancelFollow`, `CancelAttackAndFollow` + fight mode methods | High | 2 | Combat system |
| T13 | Protocol | Implement `parseCancelWalk`, `parsePlayerModes`, `parseCreatureSkull`, `parseCreatureShield`, `parseCreatureMarks` | Medium | 2 | Combat status display |
| T14 | Creature | Add skull, shield, emblem, speed, light, animation-phase fields to `Creature.cs` | Medium | 2 | Visual completeness |
| T15 | Game manager | Implement NPC trade: `InspectNpcTrade`, `BuyItem`, `SellItem`, `CloseNpcTrade` + parse handlers | Medium | 2 | Shopping functionality |
| T16 | Game manager | Implement player-to-player trade: `RequestTrade`, `InspectTrade`, `AcceptTrade`, `RejectTrade` | Medium | 2 | Player economy |
| T17 | Graphics | Implement full `MapView` draw pipeline: tile, creature, effect, light compositing | High | 2 | Map is not rendered without this |
| T18 | Minimap | Implement `Minimap` class mirroring `src/client/minimap.h` with `MinimapTile`, `MinimapBlock`, `draw`, OTMM I/O | Medium | 2 | Navigation aid |
| T19 | Graphics | Implement `AnimatedText` and `StaticText` classes for floating damage/XP and creature labels | Medium | 2 | Visual feedback |
| T20 | Graphics | Implement `Missile` and `Effect` tile animations | Medium | 2 | Combat visual feedback |
| T21 | Lua | Expand `g_game` Lua proxy with all gameplay methods from §3.2 | High | 2 | Scripts depend on these bindings |
| T22 | Lua | Add `g_minimap` and `g_map` Lua proxies with tile/creature accessors | Medium | 2 | Lua scripts drive the UI |
| T23 | Protocol | Implement `parseQuestLog`, `parseQuestLine`, `parseModalDialog`, `parseEditText`, `parseEditList` | Medium | 2 | Standard client dialogs |
| T24 | Map | Implement `getSpectators`, `getSightSpectators`, `getSpectatorsInRange` | Medium | 2 | Range-based event delivery |
| T25 | Map | Implement coverage and sight checks: `isCovered`, `isLookPossible`, `isSightClear` | Medium | 2 | Combat line-of-sight |
| T26 | Protocol | Implement `parseMarketEnter`, `parseMarketBrowse`, `parseMarketDetail`, send methods | Medium | 3 | Market/economy |
| T27 | Protocol | Implement `parsePreyData`, `parseForgeResult`, `parseBestiaryRaces`/`Overview`/`MonsterData`, `parseOpenWheelWindow`, `parseImbuementDurations` | Low | 3 | Advanced protocol 13+ features |
| T28 | Protocol | Implement `parseStore`, `parseStoreOffers`, `parseCoinBalance`, `parseCompleteStorePurchase` | Low | 3 | In-game store |
| T29 | Sound | Implement protobuf soundbank loading (`loadClientFiles`) for protocol 13+ sound effects | Medium | 3 | Audio completeness |
| T30 | Graphics | Port `ParticleAffector` and `ParticleType` for modular particle customization | Low | 3 | Visual polish |
| T31 | UI | Port `UITextEdit` text input widget | Medium | 2 | Chat and item search input |
| T32 | UI | Port HTML/CSS parser for rich-text news panels | Low | 3 | Cosmetic |
| T33 | Graphics | Port `AnimatedTexture` and APNG loader | Low | 3 | Animated sprites |
| T34 | Game | Port paper doll and attached-effect system | Low | 3 | Equipment preview |
| T35 | Concurrency | Synchronize parse-handler dispatch back to main thread before Lua callbacks | High | 1 | Prevents Lua VM data races |

---

## 6. Recommended next steps

1. **Fix parse/send protocol parity for the core game loop (T01–T08, T35).**  
   Implement `parseMapDescription`, `parseFloorDescription`, `parseUpdateTile`, and the creature-movement parse handlers so that a real game session can populate the `Map` and `Creature` objects. At the same time, add movement send methods (`SendWalk*`, `SendTurn*`) and the corresponding `Game` methods so the player character can be moved. Ensure all parse handlers are dispatched on the main thread before touching shared state or triggering Lua events.

2. **Implement the `Container` class and inventory parse handlers (T04, T08).**  
   The container/inventory system is the backbone of item interaction. Port `src/client/container.h` to C# and wire up `parseOpenContainer`, `parseContainerAddItem`, and related handlers. Add corresponding `Game.Open()`, `Game.Close()`, `Game.UseItem()` methods.

3. **Build the `MapView` draw pipeline (T17).**  
   Without rendering, no visual feedback is possible even once the map data is correct. Port the tile draw loop from `src/client/mapview.cpp`, integrating the `DrawPool`, `LightView`, creature overlays, and effect layers. This is the highest-value graphics task.

4. **Expand Lua game bindings for all implemented gameplay methods (T21, T22).**  
   The Lua scripting layer drives the entire UI and game logic in OTClient. As soon as `Game.Walk`, `Game.Attack`, `Game.Talk`, and container methods are implemented, expose them to Lua via `LuaGameProxies.cs`. Without these bindings the Lua modules cannot function.

5. **Implement the `Minimap` class (T18).**  
   Navigation is a fundamental client feature. Port `src/client/minimap.h` with `MinimapTile`, `MinimapBlock`, `draw`, `updateTile`, and at minimum `loadOtmm`/`saveOtmm` for compatibility with Tibia map files.

6. **Add combat and channel systems (T09–T13).**  
   Implement `Game.Attack`, `Game.Follow`, `SendAttack`, `SendFollow`, `parseTalk`, `parseChannelList`, and the VIP parse handlers. These cover the most commonly used in-game interactions after movement and items.

7. **Audit and harden the concurrency model (T35).**  
   Before shipping any multiplayer session, review every parse handler to ensure that all mutations to `Map`, `Creature`, and `Game` state are marshalled to the main (render/update) thread. Introduce a thread-safe dispatch queue in `EventDispatcher` that the async network receive path can post to, matching the C++ pattern where all game-state mutations occur on the main thread.
