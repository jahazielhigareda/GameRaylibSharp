# 🎮 Canary → C# (.NET) Port — Análisis Técnico y Roadmap Completo

> **Proyecto:** Game2dRayLib (reimplementación en C# del servidor Canary de Tibia)
> **Fecha:** 2026-02-20
> **Estado actual:** Fase 1 — Fundamentos de Networking y Movimiento

---

## 📊 PARTE 1 — Comparación Técnica: Canary (C++) vs Game2dRayLib (C#)

### 1.1 Visión General de Arquitectura

| Aspecto | Canary (C++) | Game2dRayLib (C#) | Gap |
|---|---|---|---|
| **Lenguaje** | C++20 | C# / .NET 8.0 | — |
| **Runtime** | Compilado nativo | CLR / JIT | Diferencia de rendimiento ~10–30% en hot paths |
| **Modelo de red** | ASIO (asíncrono + TCP/UDP) | LiteNetLib (UDP + reliability) | Protocolo distinto |
| **Serialización** | Protobuf + binario OTC | MessagePack | Compatible en concepto |
| **DI Container** | inject<T>() custom (Boost.DI style) | Microsoft.Extensions.DI | Equivalente funcional ✅ |
| **Scheduler/Dispatcher** | Dispatcher propio multihilo con TaskGroups | No implementado | ❌ CRÍTICO |
| **Threading** | ThreadPool manual + BS::thread_pool | System.Threading.ThreadPool | Requiere adaptación |
| **Scripting** | Lua (LuaJIT) | No implementado | ❌ Pendiente |
| **DB** | MySQL/MariaDB (raw queries) | No implementado | ❌ Pendiente |
| **Mapa/World** | OTBM custom + Zone system | Tile grid básico | ❌ Parcial |
| **Métricas** | OpenTelemetry / Prometheus | No implementado | ❌ Pendiente |
| **Logging** | spdlog (fmt) | Microsoft.Extensions.Logging | Equivalente ✅ |

---

### 1.2 Sistemas Implementados: Estado Comparativo

#### ✅ Implementados en Game2dRayLib

| Sistema | Canary equivalente | Calidad actual | Notas |
|---|---|---|---|
| Movimiento tile-based | `Map::moveCreature`, `Game::checkCreatureWalk` | ⭐⭐⭐⭐ Bueno | Interpolación visual correcta |
| Networking básico | `ServiceManager`, `Connection` | ⭐⭐⭐ Funcional | Falta protocolo completo |
| Stats (HP/MP/Level/Exp) | `Player::changeHealth`, vocations | ⭐⭐⭐⭐ Completo | Fórmulas Tibia correctas |
| Skills System | `Player::getSkillLevel`, vocMultipliers | ⭐⭐⭐⭐ Completo | 8 skills con multiplicadores |
| ECS manual | No tiene ECS — usa herencia Creature→Player | ⭐⭐⭐ Funcional | Repensar vs herencia Canary |
| Frustum Culling | `Player::canSee(pos)` | ⭐⭐⭐⭐ Bueno | — |
| Regeneración HP/MP | `Condition::executeCondition` | ⭐⭐⭐ Básico | Falta condiciones avanzadas |

#### ❌ NO Implementados (Críticos para Tibia Clone)

| Sistema | Canary equivalente | Prioridad |
|---|---|---|
| **Dispatcher/Scheduler** | `Dispatcher`, `Task`, `ScheduledTask` | 🔴 P0 |
| **Combate** | `Combat`, `CombatParams`, `ValueCallback` | 🔴 P0 |
| **Inventario** | `Container`, `Item`, `Cylinder` | 🔴 P0 |
| **Mapa OTBM** | `Map`, `MapLoader`, `Tile`, `TileState` | 🔴 P0 |
| **Criaturas/Monstruos** | `Monster`, `MonsterType`, `Spawn` | 🔴 P0 |
| **NPCs** | `Npc`, `NpcType`, diálogos | 🟠 P1 |
| **Spells/Runas** | `Spell`, `InstantSpell`, `RuneSpell` | 🟠 P1 |
| **Persistencia** | `IOLoginData`, `Database`, `DBResult` | 🟠 P1 |
| **Pathfinding A\*** | `Map::getPathTo`, `AStar` | 🟠 P1 |
| **Sistema de casas** | `House`, `HouseTile`, `HouseManager` | 🟡 P2 |
| **Market** | `IOMarket`, `MarketOffer` | 🟡 P2 |
| **Guilds** | `Guild`, `IOGuild` | 🟡 P2 |
| **Chat channels** | `Chat`, `ChatChannel` | 🟡 P2 |
| **Lua scripting** | `LuaScriptInterface`, `Scripts` | 🟡 P2 |
| **Outfits/Sprites** | `Outfit`, `SpritesLoader` | 🟡 P2 |

---

### 1.3 Análisis de Código Profundo

#### Canary: Fortalezas Arquitectónicas

**1. Dispatcher centralizado (dispatcher.cpp)**
```cpp
// Canary usa TaskGroups: Walk, Serial, GenericParallel, WalkParallel
void Dispatcher::addEvent(function<void()>&& f, string_view context, uint32_t expiresAfterMs);
void Dispatcher::scheduleEvent(uint32_t delay, function<void()>&& f, string_view context);
uint64_t Dispatcher::cycleEvent(uint32_t delay, function<void()>&& f, string_view context);
```
Cada sistema del juego (combate, decay, respawn) es un `Task` serializado o paralelo. Esto garantiza thread-safety sin locks explícitos en la mayoría del código de juego.

**2. DI con inject<T>() (container.hpp)**
```cpp
// Patrón singleton via DI en Canary
SaveManager& SaveManager::getInstance() { return inject<SaveManager>(); }
```
Todos los managers siguen este patrón. Game2dRayLib ya tiene MS.DI, que es el equivalente idiomático.

**3. Jerarquía Creature → Player / Monster / Npc**
```cpp
class Creature : enable_shared_from_this<Creature> { ... };
class Player : public Creature { ... };  // ~8000 líneas
class Monster : public Creature { ... };
class Npc : public Creature { ... };
```
Canary NO usa ECS — usa herencia profunda con composición para comportamientos complejos (wheel, combat, conditions). **El ECS de Game2dRayLib debe reconsiderarse o adaptarse a este modelo.**

**4. Cylinder (sistema de contenedores/tiles)**
```cpp
class Cylinder { virtual ReturnValue queryAdd(...) = 0; };
class Tile : public Cylinder { ... };
class Container : public Item, public Cylinder { ... };
```
Permite mover items entre tiles, containers, inventario de forma polimórfica.

#### Game2dRayLib: Fortalezas Actuales

- Proyecto estructurado correctamente en 3 capas (Client / Server / Shared)
- Uso correcto de MessagePack para serialización binaria
- Fórmulas de Tibia (XP, skills, speed) correctamente portadas en `Constants.cs`
- Inyección de dependencias ya configurada
- Interpolación visual de movimiento bien implementada
- ECS (Arch) para entidades es flexible, aunque difiere del modelo Canary

---

## ⚡ PARTE 2 — Análisis FODA

### ✅ Puntos Positivos

1. **Base sólida de networking** — LiteNetLib con UDP confiable es una buena elección para Tibia
2. **Fórmulas de juego correctas** — XP, stats, vocaciones, skills ya implementados y verificados
3. **Arquitectura cliente-servidor clara** — separación correcta desde el inicio
4. **DI container** — MS.Extensions.DI es equivalente funcional al inject<T> de Canary
5. **ECS disponible** — Arch library instalada; útil para entidades masivas (monstruos/items en mapa)
6. **Interpolación visual** — movimiento suave ya funcional en cliente
7. **Logging estructurado** — MS.Extensions.Logging compatible con cualquier sink futuro

### ❌ Puntos Negativos / Problemas Actuales

1. **Sin Dispatcher** — el corazón del servidor Canary no existe. Todo se ejecuta en el mismo hilo sin scheduling
2. **ECS vs Herencia** — Canary usa herencia profunda; el ECS actual rompe la paridad arquitectónica en comportamientos complejos (combat, conditions, skills)
3. **Sin sistema de mapas OTBM** — el mapa actual es un grid básico, Canary usa OTBM con layers, zones, house tiles
4. **Sin combate** — cero implementación de combat formulas, conditions, effects
5. **Sin inventario ni items** — el sistema Cylinder/Container de Canary es muy complejo y no tiene equivalente
6. **Sin persistencia** — no hay DB, no hay login real, sesiones en memoria
7. **Sin pathfinding** — monstruos no pueden moverse autónomamente
8. **Sin Lua** — todos los scripts de eventos, monstruos, NPCs en Canary son Lua

### 🔧 Mejoras Identificadas vs Canary

| Área | Canary problema | Solución C# |
|---|---|---|
| Memory management | `shared_ptr` overhead, GC inexistente | GC .NET maneja memoria, usar `record`/`struct` para value types |
| Thread safety | Locks manuales + dispatcher tricks | `Channel<T>`, `ImmutableCollections`, async/await idiomático |
| Config | config.lua (Lua embebido) | `appsettings.json` + `IOptions<T>` con validación fuerte |
| Scripting | LuaJIT solo | NLua o MoonSharp como alternativa C# |
| Serialización | Protobuf + binario OTC custom | MessagePack ya instalado ✅ |
| Testing | Sin tests unitarios en Canary | xUnit + NSubstitute desde día 1 |
| Métricas | OpenTelemetry opcional | OpenTelemetry .NET nativo |

---

## 🗺️ PARTE 3 — Roadmap de Transformación

### Fase 0 — Fundamentos (AHORA — 2 semanas)
> Preparar la base para todo lo que sigue. Sin esto, nada escala.

#### 0.1 Dispatcher / Task Scheduler (port de dispatcher.cpp)
**Por qué:** Es el núcleo del servidor. Sin él, combate, AI, decay, respawn son imposibles de implementar thread-safe.

```csharp
// Target API en C#
dispatcher.AddEvent(() => DoSomething(), "context");
dispatcher.ScheduleEvent(TimeSpan.FromMilliseconds(500), () => CheckCombat(), "combat");
dispatcher.CycleEvent(TimeSpan.FromSeconds(1), () => RegenerateHP(), "regen");
```

**Pasos:**
- [ ] Crear `IDispatcher` con `AddEvent`, `ScheduleEvent`, `CycleEvent`, `StopEvent`
- [ ] Implementar `Dispatcher` con `System.Threading.Channels` (productor/consumidor)
- [ ] Crear `Task` (con expiración, context, cycle flag)
- [ ] Crear `TaskGroup` enum (Serial, Walk, GenericParallel, WalkParallel)
- [ ] Test unitario: verificar orden de ejecución serial, cancelación, cycle

#### 0.2 Configuración Moderna (port de configmanager)
- [ ] Reemplazar constantes hardcodeadas por `IOptions<ServerConfig>` en `appsettings.json`
- [ ] Incluir: WorldType, ServerName, MapName, ProtectionLevel, ExperienceMultiplier, etc.
- [ ] Validación con `DataAnnotations`

#### 0.3 Pipeline de Tests
- [ ] Agregar proyecto `Server.Tests` (xUnit)
- [ ] Agregar proyecto `Shared.Tests`
- [ ] Configurar coverage con Coverlet
- [ ] CI básico (GitHub Actions: build + test)

---

### Fase 1 — Core Domain (4–6 semanas)
> Implementar la jerarquía de entidades y el sistema de items.

#### 1.1 Jerarquía Creature (port de creature.hpp/cpp)
**Por qué:** Todo en Tibia es una Creature — Players, Monsters, NPCs comparten combat, conditions, movement.

```csharp
// Jerarquía C# fiel a Canary
public abstract class Creature : IComparable<Creature>
{
    public uint Id { get; }
    public string Name { get; }
    public Position Position { get; protected set; }
    public Direction Direction { get; set; }
    public int Health { get; protected set; }
    public int MaxHealth { get; protected set; }
    // ...
    public abstract CreatureType CreatureType { get; }
    protected virtual void OnCreatureMove(Tile fromTile, Tile toTile) { }
}

public class Player : Creature { /* ~600 propiedades/métodos */ }
public class Monster : Creature { /* AI, loot, spawnInfo */ }
public class Npc : Creature { /* dialogue, shop */ }
```

**Pasos:**
- [ ] `Creature` abstract con: Id, Name, Position, Direction, Health, MaxHealth, Speed, Conditions
- [ ] `Player : Creature` con: Skills, Vocation, Inventory, Level, Exp, Mana, Soul, Stamina
- [ ] `Monster : Creature` con: MonsterType, Target, LootList, AIState
- [ ] `Npc : Creature` con: DialogueHandler, ShopItems
- [ ] Migrar ECS → herencia para Creature (mantener ECS solo para items en mapa si se desea)
- [ ] Tests: creación de cada tipo, herencia de stats, override de métodos

#### 1.2 Sistema de Items (port de item.hpp, container.hpp, cylinder.hpp)
**Por qué:** Sin items no hay inventario, loot, equipamiento ni economía.

```csharp
public interface ICylinder
{
    ReturnValue QueryAdd(int index, Item item, uint count, CylinderFlags flags, Creature? actor = null);
    ReturnValue QueryRemove(Item item, uint count, CylinderFlags flags, Creature? actor = null);
    Item? QueryDestination(ref int index, Item item, ref Item? destItem, ref CylinderFlags flags);
    void AddThing(int index, Item item);
    void RemoveThing(Item item, uint count);
}

public class Item : ICylinder { ... }
public class Container : Item { /* slots, pagination */ }
public class Tile : ICylinder { /* ground, items, creatures */ }
```

**Pasos:**
- [ ] `ItemType` con todos los atributos de items.xml de Tibia
- [ ] `Item` con ID, Count, Tier, Attributes, UniqueId, ActionId
- [ ] `Container : Item` con lista paginada y `ContainerIterator`
- [ ] Lector de `items.xml` / `items.otb` con cache
- [ ] Tests: apilar, mover entre containers, overflow, stackable

#### 1.3 Sistema de Mapa (port de map.hpp, tile.hpp, otbm_loader)
**Por qué:** Sin mapa real, el servidor no puede hospedar ningún contenido de Tibia.

```csharp
public class Map
{
    private readonly Dictionary<ulong, Tile> _tiles; // packed position key
    
    public Tile? GetTile(Position pos);
    public void SetTile(Position pos, Tile tile);
    public IEnumerable<Creature> GetSpectators(Position center, bool multiFloor, int rangeX, int rangeY);
    public bool IsSightClear(Position from, Position to, bool checkFloor);
}

public class Position
{
    public ushort X, Y;
    public byte Z; // floor 0-15
    public static ulong Pack(ushort x, ushort y, byte z) => ((ulong)x << 24) | ((ulong)y << 8) | z;
}
```

**Pasos:**
- [ ] `Position` struct con Pack/Unpack para key de diccionario
- [ ] `Tile` con Ground, TopItems, Items, Creatures, Flags (TileState)
- [ ] `Map` con GetTile/SetTile/GetSpectators/GetNeighbors
- [ ] Parser OTBM (Binary format de mapas Tibia)
- [ ] Tests: cargar mapa, walkability, spectators en rango

---

### Fase 2 — Combat System (4–6 semanas)
> El sistema más complejo. Port de combat.cpp (~3000 líneas).

#### 2.1 Condiciones (port de condition.hpp/cpp)
**Por qué:** Poison, Fire, Stun, Invisible — todo es una Condition en Canary.

```csharp
public abstract class Condition
{
    public ConditionType Type { get; }
    public ConditionId Id { get; }
    public int Ticks { get; protected set; }
    
    public abstract bool StartCondition(Creature creature);
    public abstract bool ExecuteCondition(Creature creature, int interval);
    public abstract void EndCondition(Creature creature);
    public abstract Condition Clone();
}

public class ConditionDamage : Condition { /* Poison, Fire, Energy */ }
public class ConditionSpeed : Condition { /* Haste, Paralyze */ }
public class ConditionRegeneration : Condition { /* HP/MP regen */ }
```

**Pasos:**
- [ ] `Condition` abstract + `ConditionType` enum (todos los de Canary)
- [ ] `ConditionDamage` para DoT (Poison, Fire, Energy, Earth)
- [ ] `ConditionSpeed` para Haste/Paralyze
- [ ] `ConditionRegeneration` para HP/MP (migrar regen básico existente)
- [ ] Integrar en `Creature`: `AddCondition`, `RemoveCondition`, `HasCondition`
- [ ] Tests: aplicar, ejecutar tick, expirar, stack conditions

#### 2.2 Combat Core (port de combat.cpp)
**Por qué:** PvE y PvP requieren fórmulas exactas de Tibia.

```csharp
public class Combat
{
    public CombatParams Params { get; }
    
    public static bool CanDoCombat(Creature attacker, Creature target);
    public void DoCombat(Creature caster, Creature target);
    public void DoCombatArea(Creature caster, Position pos, CombatArea area);
    
    // Fórmulas de Tibia
    public static int GetDefense(Player player);
    public static int GetAttack(Player player, Item? weapon);
    public static CombatDamage NormalizeCombatDamage(CombatDamage damage);
}
```

**Pasos:**
- [ ] `CombatParams` con: type, origin, callbacks, area
- [ ] `CombatType` enum (Physical, Fire, Energy, Earth, Ice, Holy, Death, Healing)
- [ ] Fórmulas de ataque/defensa (skill-based) de Canary
- [ ] `CombatArea` con patrones AoE (circle, cross, wave, etc.)
- [ ] `BlockType` (NoBlock, Shield, Armor)
- [ ] Eventos: `OnCreatureKilled`, `OnDamage`, `OnHeal`
- [ ] Tests: hit melee, hit magic, block con shield, kill chain

#### 2.3 Spells (port de spell.hpp, instantspell.hpp, runespell.hpp)
**Por qué:** Runas y hechizos son el core del gameplay mage/shooter.

**Pasos:**
- [ ] `Spell` abstract con: name, words, manaCost, level, vocation requirements
- [ ] `InstantSpell : Spell` con callback de ejecución
- [ ] `RuneSpell : Spell` para runas
- [ ] `SpellManager` con registry y lookup por name/id
- [ ] Tests: cast conditions, mana check, target validation

---

### Fase 3 — AI y Persistencia (4–6 semanas)

#### 3.1 Pathfinding A* (port de map.cpp getPathTo)
**Por qué:** Sin pathfinding, monstruos no pueden perseguir ni huir.

```csharp
public class AStarPathfinder
{
    public bool FindPath(
        Creature creature,
        Position startPos,
        Position targetPos,
        int maxSearchDist,
        List<Direction> outPath,
        bool allowDiagonal = true);
}
```

**Pasos:**
- [ ] A* con heurística Manhattan + Chebyshev para diagonales
- [ ] Considerar walkability de tiles (TileFlags)
- [ ] Cache de paths recientes (LRU)
- [ ] Tests: path recto, esquivar obstáculos, sin camino disponible

#### 3.2 Monster AI (port de monster.cpp)
**Por qué:** Los monstruos son el contenido principal del juego.

```csharp
public class MonsterAI
{
    private readonly Monster _monster;
    
    public void Think(int interval);        // Called by Dispatcher cycleEvent
    public void SelectTarget();             // Encuentra el mejor target
    public void DoAttack();                 // Ataca con spells/melee
    public void UpdateLookDirection();      // Gira hacia target
    private void UpdateWalkDirection();     // Mueve hacia target
}
```

**Pasos:**
- [ ] `MonsterType` con stats, loot, spells, summons de XML
- [ ] `SpawnSystem` para respawnear monstruos
- [ ] AI states: Idle, Approaching, Attacking, Fleeing
- [ ] Loot system con probabilidades
- [ ] Tests: spawn, approach target, flee at low HP, drop loot

#### 3.3 Persistencia con EF Core (port de database.cpp, IOLoginData)
**Por qué:** Jugadores necesitan persistir su progreso.

```csharp
public class GameDbContext : DbContext
{
    public DbSet<AccountEntity> Accounts { get; set; }
    public DbSet<PlayerEntity> Players { get; set; }
    public DbSet<PlayerItemEntity> PlayerItems { get; set; }
    public DbSet<GuildEntity> Guilds { get; set; }
}

public interface ILoginDataService
{
    Task<Player?> LoadPlayerByNameAsync(string name);
    Task SavePlayerAsync(Player player);
    Task<bool> AccountLoginAsync(string accountName, string password);
}
```

**Pasos:**
- [ ] Esquema DB basado en tablas Canary (accounts, players, player_items, guilds)
- [ ] `GameDbContext` con EF Core + Migrations
- [ ] `ILoginDataService` + implementación async
- [ ] `SaveManager` con guardado periódico (port de save_manager.cpp)
- [ ] Tests: CRUD de jugador, login, inventario persistido

---

### Fase 4 — Protocolo Completo (3–4 semanas)
> Hacer compatible con clientes reales de Tibia / OTC.

#### 4.1 Protocol Game (port de protocolgame.hpp)
**Por qué:** Para conectar con Tibia client real o OpenTibiaClient.

**Pasos:**
- [ ] Parser de paquetes OTC completo (RSA + XTEA)
- [ ] Login protocol (RSA handshake)
- [ ] Game protocol: move, attack, use item, chat
- [ ] Output message pool (port de outputmessage.cpp)
- [ ] XTEA encryption en C#

#### 4.2 ServiceManager (port de server.hpp)
**Pasos:**
- [ ] `ServicePort` con acceptor async por puerto
- [ ] `ServiceManager` con lifecycle start/stop
- [ ] Múltiples protocolos por puerto (login + game)

---

### Fase 5 — Sistemas Avanzados (6–8 semanas)

#### 5.1 Casa y Market
- [ ] `House` system con rent periods
- [ ] `IOMarket` con ofertas buy/sell
- [ ] `DepotChest` y `Inbox`

#### 5.2 Party y Guild
- [ ] `Party` con shared XP y loot
- [ ] `Guild` con rankings y house guilds

#### 5.3 Scripting Lua (opcional)
- [ ] Integrar MoonSharp o NLua
- [ ] `LuaScriptInterface` C# equivalente
- [ ] Port de eventos globales (GlobalEvents)

#### 5.4 Métricas y Observabilidad
- [ ] OpenTelemetry .NET
- [ ] Métricas: players online, packets/sec, tick duration
- [ ] Health checks endpoint

---

## 🏗️ PARTE 4 — Arquitectura Target

### Estructura de Proyectos Recomendada

```
Game2dRayLib/
├── src/
│   ├── Server/                          ← Servidor autoritativo
│   │   ├── Core/
│   │   │   ├── Scheduling/              ← Dispatcher, Task, TaskGroup
│   │   │   ├── Config/                  ← ConfigManager, ServerOptions
│   │   │   └── DI/                      ← ServiceCollectionExtensions
│   │   ├── Domain/
│   │   │   ├── Creatures/               ← Creature, Player, Monster, Npc
│   │   │   │   ├── Combat/              ← Combat, CombatParams, CombatArea
│   │   │   │   ├── Conditions/          ← Condition, ConditionDamage, etc.
│   │   │   │   └── Players/
│   │   │   │       ├── Skills/          ← SkillSystem
│   │   │   │       ├── Vocations/       ← VocationManager
│   │   │   │       └── Inventory/       ← Container, Equipment
│   │   │   ├── Items/                   ← Item, ItemType, Container, Cylinder
│   │   │   ├── Map/                     ← Map, Tile, Position, Zone
│   │   │   │   ├── Loaders/             ← OtbmLoader, SpawnLoader
│   │   │   │   └── Pathfinding/         ← AStarPathfinder
│   │   │   ├── Spells/                  ← Spell, InstantSpell, RuneSpell
│   │   │   └── Game/                    ← Game (central coordinator)
│   │   ├── Infrastructure/
│   │   │   ├── Database/                ← GameDbContext, Entities, Migrations
│   │   │   ├── IO/                      ← IOLoginData, IOMarket, IOGuild
│   │   │   └── Network/                 ← Protocol, Connection, ServiceManager
│   │   └── Application/
│   │       └── GameServer.cs            ← Host + startup
│   ├── Client/                          ← Cliente gráfico Raylib
│   │   ├── Rendering/                   ← SpriteRenderer, TileRenderer, UI
│   │   ├── Input/                       ← InputHandler
│   │   └── Network/                     ← ClientProtocol
│   ├── Shared/                          ← DTOs, Enums, Packets, Constants
│   └── MapEditor/                       ← Editor de mapas
├── tests/
│   ├── Server.Tests/                    ← xUnit tests del servidor
│   ├── Shared.Tests/
│   └── Integration.Tests/
└── tools/
    ├── otbm-parser/                     ← Tool para parsear mapas
    └── sprites-converter/               ← Convertir SPR/DXT a PNG
```

---

## 🧰 PARTE 5 — Stack Tecnológico Recomendado

### Dependencias Actuales (mantener)

| Package | Versión | Uso |
|---|---|---|
| LiteNetLib | 2.0.2 | Networking UDP |
| MessagePack | 3.1.4 | Serialización binaria |
| Raylib-cs | 7.0.2 | Renderizado cliente |
| MS.Extensions.DI | 10.0.3 | Inyección de dependencias |
| MS.Extensions.Logging | 10.0.3 | Logging |
| Arch | 2.1.0 | ECS (revisar si se mantiene) |

### Nuevas Dependencias Recomendadas

| Package | Uso | Justificación |
|---|---|---|
| **Microsoft.EntityFrameworkCore** | ORM DB | Async, migrations, type-safe queries |
| **xUnit** | Tests unitarios | Standard .NET |
| **NSubstitute** | Mocking | Más idiomático que Moq |
| **FluentAssertions** | Assertions expresivas | Legibilidad tests |
| **Coverlet.Collector** | Code coverage | CI pipeline |
| **MoonSharp** | Scripting Lua | Compatible LuaJIT, C# native |
| **OpenTelemetry.Sdk** | Observabilidad | Port de metrics de Canary |
| **BenchmarkDotNet** | Benchmarks | Hot paths: dispatcher, combat |
| **System.IO.Pipelines** | I/O network de alto rendimiento | Para protocol parser |

---

## 📐 PARTE 6 — Patrones de Diseño a Aplicar

### Port fiel de patrones Canary

| Patrón | Canary usa | C# equivalente |
|---|---|---|
| **Singleton via DI** | `inject<T>()` | `services.AddSingleton<T>()` |
| **Command (Tasks)** | `Task` con `func` | `Func<Task>` + contexto |
| **Observer (Events)** | Llamadas directas + Dispatcher | `IEventBus` / `MediatR` |
| **Strategy (Combat)** | `ValueCallback`, `TileCallback` | Interfaces + DI |
| **Composite (Cylinder)** | `ICylinder` → Item/Tile/Container | Interfaz C# `ICylinder` |
| **Iterator (Container)** | `ContainerIterator` | `IEnumerable<Item>` + yield |
| **Template Method (Conditions)** | `Condition::executeCondition` virtual | Abstract class + override |
| **Factory (Items)** | `Item::CreateItem(id)` | `IItemFactory` + registry |

### Nuevos patrones modernos C#

```csharp
// 1. Result<T> para errores de juego (en vez de ReturnValue enum solo)
public record Result<T>(T? Value, ReturnValue Status, string? Message = null)
{
    public bool IsSuccess => Status == ReturnValue.NoError;
}

// 2. Discriminated Unions para Events
public abstract record GameEvent;
public record CreatureMovedEvent(int EntityId, Position From, Position To) : GameEvent;
public record PlayerLevelUpEvent(int PlayerId, int NewLevel) : GameEvent;
public record CreatureDiedEvent(int EntityId, int? KillerId) : GameEvent;

// 3. Channels para Dispatcher (lock-free producer/consumer)
var channel = Channel.CreateUnbounded<ITask>(new() { SingleReader = true });

// 4. IAsyncEnumerable para streaming de spectators
public async IAsyncEnumerable<Creature> GetSpectatorsAsync(Position pos, int range);

// 5. Span<T> para parsing de paquetes (zero-copy)
public static Packet Parse(ReadOnlySpan<byte> buffer);
```

---

## 📏 PARTE 7 — Estándares de Calidad

### Coverage mínimo por fase

| Fase | Módulo | Coverage target |
|---|---|---|
| 0 | Dispatcher, Task | 90% |
| 1 | Creature, Item, Map | 80% |
| 2 | Combat, Conditions, Spells | 85% |
| 3 | AI, Pathfinding, DB | 75% |
| 4 | Protocol | 70% |

### Convenciones de código

```csharp
// ✅ Naming Canary-compatible
class Creature           // PascalCase para clases (como C++ Canary)
int GetHealth()          // PascalCase para métodos
int _health;             // _camelCase para fields privados
ICreature iCreature      // I prefix para interfaces

// ✅ Dispatcher calls siempre con context string
_dispatcher.AddEvent(() => DoAttack(), "Creature::checkCreatureAttack");

// ✅ Todos los errores de game devuelven ReturnValue
public ReturnValue AddItemToContainer(Container container, Item item, int index)

// ✅ Posiciones siempre inmutables
public readonly record struct Position(ushort X, ushort Y, byte Z);

// ✅ Tests descriptivos con Given/When/Then
[Fact]
public void Player_WhenEquipsSword_ShouldUpdateAttackSkill() { ... }
```

---

## 🚦 PARTE 8 — Estado Actual vs Objetivo Final

```
ESTADO ACTUAL (Feb 2026)
========================
[██████░░░░░░░░░░░░░░] 30% — Fundamentos

✅ Networking (LiteNetLib) ─────────── 100%
✅ Stats / Skills / Vocaciones ─────── 100%
✅ Movimiento tile-based ────────────── 95%
✅ DI Container ─────────────────────── 90%
✅ Renderizado básico ────────────────── 80%
❌ Dispatcher/Scheduler ──────────────── 0%
❌ Combat System ──────────────────────── 0%
❌ Inventory / Items ──────────────────── 0%
❌ OTBM Map ───────────────────────────── 0%
❌ Monster AI ─────────────────────────── 0%
❌ Persistencia DB ────────────────────── 0%
❌ Protocol Tibia (login/game) ─────────── 0%

OBJETIVO FASE MVP (6 meses)
============================
[████████████████░░░░] 80% — Servidor jugable

✅ Dispatcher ────────── P0 (semanas 1-2)
✅ Creature hierarchy ── P0 (semanas 3-5)
✅ Items/Inventory ───── P0 (semanas 4-6)
✅ OTBM Map ─────────── P0 (semanas 5-7)
✅ Combat básico ─────── P1 (semanas 6-10)
✅ Monster AI básico ─── P1 (semanas 8-12)
✅ Persistencia básica ── P1 (semanas 10-14)
✅ Protocol completo ──── P2 (semanas 12-16)
```

---

## 📋 Checklist de Inicio Inmediato

```
SEMANA 1 — Dispatcher
─────────────────────
[ ] Crear Server/Core/Scheduling/IDispatcher.cs
[ ] Crear Server/Core/Scheduling/Dispatcher.cs
[ ] Crear Server/Core/Scheduling/Task.cs
[ ] Crear Server/Core/Scheduling/TaskGroup.cs
[ ] Crear Server.Tests/Scheduling/DispatcherTests.cs
[ ] Integrar Dispatcher en DI (AddSingleton<IDispatcher, Dispatcher>)

SEMANA 2 — Config + Tests Infrastructure  
─────────────────────────────────────────
[ ] Crear Server.Tests.csproj (xUnit, NSubstitute, FluentAssertions)
[ ] Migrar constants hardcodeados a ServerOptions
[ ] CI GitHub Actions: build + test

SEMANA 3 — Creature Base
─────────────────────────
[ ] Refactorizar entidades ECS → Creature abstract
[ ] Player : Creature con todos los campos actuales
[ ] Migrar sistemas de stats/skills a Player
[ ] Tests: Player creation, stat calculations

SEMANA 4 — Position + Tile
───────────────────────────
[ ] Position readonly record struct (X, Y, Z)
[ ] Tile con flags, ground, items, creatures
[ ] Map básico con Dictionary<ulong, Tile>
[ ] Tests: tile access, position math
```

---

*Documento generado a partir del análisis directo del código fuente de Canary (C++) y Game2dRayLib (C#)*
*Basado en: canary/src/ completo + Game2dRaylib.sln + roadmap.md existente*