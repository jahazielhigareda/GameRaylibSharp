# 🎮 Game2dRaylib → Tibia Clone: Análisis y Roadmap Completo

---

## 📋 PARTE 1: Análisis a Profundidad del Proyecto Actual

### 1.1 Arquitectura General

El proyecto sigue una arquitectura **Cliente-Servidor autoritativo** con separación en 3 proyectos .NET 8.0:

```
Game2dRaylib/
├── Client/          → Cliente gráfico con Raylib-cs
├── Server/          → Servidor autoritativo headless
└── Shared/          → Código compartido (packets, constantes, enums)
```

**Patrón arquitectónico:** Entity Component System (ECS) manual (no usa frameworks ECS como Arch o DefaultEcs).

### 1.2 Stack Tecnológico Actual

| Componente | Tecnología | Versión |
|---|---|---|
| Runtime | .NET 8.0 | net8.0 |
| Renderizado | Raylib-cs | 7.0.2 |
| Networking | LiteNetLib | 2.0.2 |
| Serialización | MessagePack | 3.1.4 |
| DI Container | Microsoft.Extensions.DependencyInjection | 10.0.3 |
| Logging | Microsoft.Extensions.Logging.Console | 10.0.3 |

### 1.3 Sistemas Implementados (Estado Actual)

#### ✅ Completados
| Sistema | Descripción | Calidad |
|---|---|---|
| **Movimiento tile-based** | 8 direcciones, cola de 1 movimiento, interpolación visual | ⭐⭐⭐⭐ Bueno |
| **Networking básico** | Conexión, desconexión, broadcast de estado | ⭐⭐⭐ Funcional |
| **ECS manual** | Entidades con componentes, sistemas de update | ⭐⭐⭐ Funcional |
| **Frustum Culling** | Solo renderiza tiles/entidades visibles | ⭐⭐⭐⭐ Bueno |
| **Stats System** | HP, MP, Level, Exp, Cap, Soul, Stamina, Vocaciones | ⭐⭐⭐⭐ Completo |
| **Skills System** | 8 skills con niveles, tries, multiplicadores por vocación | ⭐⭐⭐⭐ Completo |
| **HUD** | Panel de stats, skills (toggle K), barras HP/MP/Exp | ⭐⭐⭐ Funcional |
| **Regeneración** | HP/MP se regeneran periódicamente | ⭐⭐⭐ Básico |
| **Interpolación** | Movimiento visual suave entre tiles | ⭐⭐⭐⭐ Bueno |
| **Cámara centrada** | Viewport sigue al jugador local | ⭐⭐⭐⭐ Bueno |

#### ❌ No Implementados (Necesarios para Tibia Clone)
- Sistema de combate (PvE y PvP)
- NPCs y diálogos
- Sistema de inventario y equipamiento
- Sistema de items/loot
- Tilemap real con editor de mapas
- Sistema de chat
- Spells y runas
- Pathfinding (A*)
- Persistencia de datos (base de datos)
- Sistema de login/cuentas
- Criaturas con IA
- Sistema de party
- Trade entre jugadores
- Casas y guild halls
- Outfits y sprites
- Sonido y música
- Sistema de luz/oscuridad
- Minimapa
- Hotkeys configurables

### 1.4 Análisis de Código por Módulo

#### 1.4.1 Shared (Código Compartido)

**Constants.cs** — Bien estructurado. Contiene:
- Fórmula de experiencia de Tibia: `50/3 * (L³ - 6L² + 17L - 12)` ✅
- Fórmula de skill tries exponencial: `base * 1.1^level * vocMultiplier` ✅
- Cálculo de step duration basado en speed ✅
- Constantes de mapa, viewport, regeneración ✅

**Vocation.cs** — Implementación correcta de las 4 vocaciones con multiplicadores diferenciados para HP/MP/Cap/Skills.

**Direction.cs** — 8 direcciones + None, con helper para offsets y detección diagonal. ✅

**PacketSerializer.cs** — Serialización simple: 1 byte de tipo + payload MessagePack. Funcional pero sin compresión ni encriptación.

**Packets/** — 7 tipos de paquetes definidos:
1. `InputPacket` (legacy, no usado)
2. `MoveRequestPacket` (dirección + tick)
3. `WorldStatePacket` (tick + lista de snapshots)
4. `JoinAcceptedPacket` (ID asignado)
5. `PlayerDisconnectedPacket` (ID)
6. `StatsUpdatePacket` (13 campos)
7. `SkillsUpdatePacket` (17 campos)

#### 1.4.2 Server

**GameLoop.cs** — Loop fijo con acumulador de tiempo. Broadcast de estado cada tick, stats cada 1 segundo. Usa `Stopwatch` + `Thread.Sleep(1)`.

**MovementSystem.cs** — Valida walkability (bordes del mapa + colisión con otros jugadores). Procesa cola de movimiento solo cuando la entidad no está en movimiento.

**StatsSystem.cs** — Solo maneja regeneración por ahora.

**NetworkManager.cs** — Acepta todas las conexiones sin autenticación. Spawn fijo en el centro del mapa. Broadcast unreliable para WorldState, reliable para stats/skills.

**PositionComponent (Server)** — Tiene interpolación visual del lado del servidor (para enviar posiciones suaves a los clientes). Incluye `StartMoveTo()` con duración calculada.

#### 1.4.3 Client

**GameLoop.cs** — Orden de ejecución: PollEvents → Input → Interpolation → BeginDrawing → Background → Render → HUD → EndDrawing.

**InputSystem.cs** — Detecta 8 direcciones con key repeat (80ms). Envía `Direction.None` al soltar teclas.

**InterpolationSystem.cs** — Lerp suave con frustum culling (snap directo para entidades lejanas).

**BackgroundSystem.cs** — Dibuja grilla de tiles con colores fijos (walkable vs wall). Calcula rango visible para otros sistemas.

**RenderSystem.cs** — Dibuja rectángulos coloreados (azul=local, rojo=remoto) con borde.

**HudSystem.cs** — Panel completo con stats, skills toggleable, barras de HP/MP/Exp centradas.

### 1.5 Problemas y Deuda Técnica Identificados

| # | Problema | Severidad | Impacto |
|---|---|---|---|
| 1 | **Sin autenticación** — Cualquiera puede conectarse | 🔴 Crítico | Seguridad |
| 2 | **Sin persistencia** — Todo se pierde al reiniciar | 🔴 Crítico | Funcionalidad |
| 3 | **Mapa hardcodeado** — Solo bordes como paredes | 🟡 Alto | Gameplay |
| 4 | **Sin sprites** — Solo rectángulos de color | 🟡 Alto | Visual |
| 5 | **WorldState envía TODOS los jugadores** — No escala | 🟡 Alto | Performance |
| 6 | **ECS manual sin queries eficientes** — O(n) por búsqueda | 🟡 Medio | Performance |
| 7 | **Sin validación anti-cheat del lado del servidor** | 🟡 Medio | Seguridad |
| 8 | **InputPacket definido pero no usado** | 🟢 Bajo | Limpieza |
| 9 | **Entity IDs estáticos (no thread-safe)** | 🟢 Bajo | Concurrencia |
| 10 | **Sin compresión de paquetes** | 🟢 Bajo | Bandwidth |

---

## 📋 PARTE 2: Roadmap Completo — Tibia Clone

### Visión General de Fases

```
Fase 0: Fundamentos y Refactoring          [2-3 semanas]
Fase 1: Mundo y Mapas                       [3-4 semanas]
Fase 2: Criaturas y Combate                 [4-6 semanas]
Fase 3: Items, Inventario y Loot            [3-4 semanas]
Fase 4: NPCs, Diálogos y Quests            [2-3 semanas]
Fase 5: Chat y Comunicación Social          [1-2 semanas]
Fase 6: Spells, Runas y Magia              [3-4 semanas]
Fase 7: Persistencia y Cuentas             [2-3 semanas]
Fase 8: UI Avanzada y Sprites              [3-4 semanas]
Fase 9: Audio, Efectos y Polish            [2-3 semanas]
Fase 10: Sistemas Avanzados                 [4-6 semanas]
Fase 11: Optimización y Deployment          [2-3 semanas]
```

**Tiempo estimado total: 6-10 meses** (desarrollador solo, dedicación parcial)

---

### 🔧 FASE 0: Fundamentos y Refactoring

**Objetivo:** Preparar la base de código para escalar. Resolver deuda técnica crítica antes de agregar features.

#### Tarea 0.1: Migrar a ECS con framework
**Objetivo:** Reemplazar el ECS manual por un framework eficiente.
**Opciones:**
- **Arch** (recomendado para C#/.NET) — Archetype-based, alto rendimiento
- **DefaultEcs** — Más maduro, buena documentación
- **Friflo.Engine.ECS** — Moderno, source-generated

**Algoritmo/Patrón:**
```
1. Definir todos los componentes como structs (value types) en vez de clases
2. Crear archetypes para: Player, Creature, NPC, Item, Projectile
3. Migrar sistemas existentes a queries del framework
4. Benchmark: medir frames con 100, 500, 1000 entidades
```

**Archivos a modificar:**
- `Server/ECS/*` y `Client/ECS/*` — Reemplazar completamente
- `Shared/` — Mover definiciones de componentes compartidos

#### Tarea 0.2: Protocolo de red robusto
**Objetivo:** Protocolo escalable con versionado, compresión y seguridad básica.

**Algoritmo:**
```
Header del paquete (nuevo formato):
[2 bytes] Packet Length
[1 byte]  Packet Type
[1 byte]  Protocol Version
[N bytes] Payload (MessagePack, opcionalmente comprimido con LZ4)

Implementar:
1. Packet pooling (reutilizar buffers)
2. Rate limiting por peer (max packets/segundo)
3. Sequence numbers para detección de duplicados
4. Delta compression para WorldState (solo enviar cambios)
```

#### Tarea 0.3: Area of Interest (AoI)
**Objetivo:** Solo enviar datos de entidades cercanas al jugador.

**Algoritmo — Grid-based Spatial Hashing:**
```
1. Dividir el mundo en celdas de NxN tiles (ej: 32x32)
2. Cada entidad se registra en su celda actual
3. Al broadcast, solo enviar entidades en celdas adyacentes al jugador
4. Cuando una entidad cambia de celda, notificar enter/leave a jugadores cercanos

Estructura de datos:
  Dictionary<(int cellX, int cellY), HashSet<Entity>> spatialGrid

Cálculo de celda:
  cellX = tileX / CELL_SIZE
  cellY = tileY / CELL_SIZE

Celdas visibles para un jugador:
  for dx in [-1, 0, 1]:
    for dy in [-1, 0, 1]:
      yield (playerCellX + dx, playerCellY + dy)
```

#### Tarea 0.4: Sistema de eventos
**Objetivo:** Desacoplar sistemas mediante eventos.

**Patrón Observer/Event Bus:**
```csharp
// Definir eventos como structs
public struct CreatureMoved { public int EntityId; public int FromX, FromY, ToX, ToY; }
public struct CreatureDied  { public int EntityId; public int KillerId; }
public struct PlayerLevelUp { public int PlayerId; public int NewLevel; }

// Event bus centralizado
public class EventBus {
    void Publish<T>(T evt);
    void Subscribe<T>(Action<T> handler);
}
```

---

### 🗺️ FASE 1: Mundo y Mapas

**Objetivo:** Reemplazar el mapa hardcodeado por un sistema de mapas real con múltiples pisos, tiles variados y un formato de archivo.

#### Tarea 1.1: Formato de mapa (OTBM-inspired)
**Objetivo:** Definir un formato binario para mapas con soporte multi-piso.

**Estructura de datos:**
```
Map:
  Header:
    width: uint16
    height: uint16
    floors: uint8 (0-15, donde 7 = nivel del suelo)
  
  Tile[width][height][floors]:
    groundItemId: uint16    (ID del tile base: grass, stone, water, etc.)
    flags: uint16           (WALKABLE, BLOCKPROJECTILE, BLOCKPATH, PZ, NOPVP, etc.)
    items: List<ItemOnTile> (items apilados en el tile)
    
  ItemOnTile:
    itemId: uint16
    count: uint8 (para stackables)
    actionId: uint16 (para scripting)
    uniqueId: uint16

Formato de archivo (.map):
  [4 bytes] Magic number "GMAP"
  [2 bytes] Version
  [Compressed data] (LZ4 o Zstd)
```

**Algoritmo de carga:**
```
1. Leer header
2. Descomprimir datos
3. Para cada tile (x, y, z):
   a. Leer ground item ID
   b. Leer flags
   c. Leer cantidad de items
   d. Para cada item: leer ID, count, actionId, uniqueId
4. Construir spatial hash para queries rápidas
5. Precalcular walkability map (bool[,,]) para pathfinding
```

#### Tarea 1.2: Tile Engine con múltiples capas
**Objetivo:** Renderizar tiles con ground, borders, items y criaturas en orden correcto.

**Algoritmo de renderizado (orden de Tibia):**
```
Para cada tile visible (de arriba-izquierda a abajo-derecha):
  1. Dibujar ground tile (capa 0)
  2. Dibujar borders/edges (capa 1) — transiciones entre terrenos
  3. Dibujar bottom items (capa 2) — items en el suelo
  4. Dibujar criaturas/jugadores (capa 3) — ordenados por Y
  5. Dibujar top items (capa 4) — items sobre criaturas (techos, árboles)
  6. Dibujar efectos (capa 5) — magia, animaciones

Painter's Algorithm simplificado:
  - Iterar tiles de norte a sur, oeste a este
  - Dentro de cada tile, respetar el orden de capas
  - Para entidades en movimiento entre tiles: usar posición visual interpolada
```

#### Tarea 1.3: Editor de mapas básico
**Objetivo:** Herramienta para crear/editar mapas.

**Funcionalidades mínimas:**
```
1. Seleccionar tile type de una paleta
2. Pintar tiles con click/drag
3. Colocar/remover items
4. Cambiar de piso (Z level)
5. Guardar/cargar en formato .map
6. Copy/paste de regiones
7. Flood fill
8. Undo/redo (Command pattern)
```

**Algoritmo Flood Fill:**
```
function FloodFill(startX, startY, targetTile, replacementTile):
  if targetTile == replacementTile: return
  queue = [(startX, startY)]
  visited = Set()
  
  while queue not empty:
    (x, y) = queue.dequeue()
    if (x, y) in visited: continue
    if map[x][y] != targetTile: continue
    
    visited.add((x, y))
    map[x][y] = replacementTile
    
    queue.enqueue((x-1, y), (x+1, y), (x, y-1), (x, y+1))
```

#### Tarea 1.4: Sistema de pisos (Z-levels)
**Objetivo:** Implementar escaleras, rampas y múltiples niveles.

**Algoritmo de cambio de piso:**
```
Cuando el jugador pisa un tile con stair/ladder:
  1. Determinar dirección (up/down) según item type
  2. Calcular posición destino en el nuevo piso:
     - Stair up: (x, y, z-1) — un piso arriba
     - Stair down: (x, y, z+1) — un piso abajo
     - Rope hole: (x, y+1, z-1) — sale un tile al sur, un piso arriba
  3. Verificar que el destino es walkable
  4. Teleportar al jugador
  5. Enviar nuevo viewport (tiles del nuevo piso)
  
Renderizado multi-piso:
  - Solo renderizar el piso actual del jugador
  - Pisos superiores: renderizar como sombra/transparencia
  - Pisos inferiores: no renderizar (ocultos)
```

---

### ⚔️ FASE 2: Criaturas y Combate

**Objetivo:** Implementar criaturas con IA, sistema de combate melee/distance/magic, y loot.

#### Tarea 2.1: Sistema de criaturas
**Objetivo:** Definir criaturas con stats, comportamiento y spawn.

**Estructura de datos:**
```csharp
public struct CreatureTemplate {
    public string Name;
    public int MaxHP, MaxMP;
    public int Experience;        // Exp que da al morir
    public int Speed;
    public int Armor, Defense;
    public int AttackMin, AttackMax;
    public int LookRange;         // Distancia a la que detecta jugadores
    public int ChaseRange;        // Distancia máxima de persecución
    public CreatureBehavior Behavior; // Melee, Ranged, Fleeing, etc.
    public List<LootEntry> LootTable;
    public List<SpellEntry> Spells;
    public Element[] Immunities;
    public Element[] Weaknesses;
}

public struct SpawnPoint {
    public int CenterX, CenterY, Floor;
    public int Radius;
    public string CreatureName;
    public int MaxCount;
    public int RespawnTime; // en segundos
}
```

#### Tarea 2.2: IA de criaturas — Máquina de estados finita (FSM)
**Objetivo:** Comportamiento realista de monstruos.

**Algoritmo — FSM con estados:**
```
Estados:
  IDLE      → Parado en spawn area, movimiento aleatorio
  ALERT     → Detectó jugador, se prepara
  CHASE     → Persigue al jugador objetivo
  ATTACK    → En rango de ataque, ejecuta ataques
  FLEE      → HP bajo, huye del jugador
  RETURN    → Perdió al objetivo, vuelve al spawn
  DEAD      → Murió, espera respawn

Transiciones:
  IDLE → ALERT:    jugador entra en lookRange
  ALERT → CHASE:   después de 0.5s delay
  CHASE → ATTACK:  jugador en rango de ataque
  ATTACK → CHASE:  jugador sale del rango de ataque
  ATTACK → FLEE:   HP < 15% (para criaturas con flag "flees")
  CHASE → RETURN:  jugador sale de chaseRange O pierde línea de visión
  RETURN → IDLE:   llegó al spawn point
  ANY → DEAD:      HP <= 0

Update por tick:
  switch (state):
    case IDLE:
      if random() < 0.02: // 2% chance por tick
        moverse a tile aleatorio adyacente dentro del spawn radius
      if distancia(target) <= lookRange:
        state = ALERT
        
    case CHASE:
      path = A_Star(myPos, targetPos)
      if path.length > 0:
        mover hacia path[0]
      if distancia(target) <= attackRange:
        state = ATTACK
      if distancia(target) > chaseRange:
        state = RETURN
        
    case ATTACK:
      if attackCooldown <= 0:
        ejecutar ataque (melee o spell aleatorio)
        attackCooldown = calculateCooldown(speed)
      if distancia(target) > attackRange:
        state = CHASE
```

#### Tarea 2.3: Pathfinding — A* optimizado
**Objetivo:** Navegación eficiente para criaturas.

**Algoritmo A*:**
```
function A_Star(start, goal, maxSteps=100):
  openSet = PriorityQueue()  // min-heap por fScore
  openSet.enqueue(start, heuristic(start, goal))
  
  cameFrom = {}
  gScore = {start: 0}
  fScore = {start: heuristic(start, goal)}
  
  steps = 0
  while openSet not empty AND steps < maxSteps:
    steps++
    current = openSet.dequeue()
    
    if current == goal:
      return reconstructPath(cameFrom, current)
    
    for neighbor in getWalkableNeighbors(current):
      // Costo: 1 para cardinal, 1.414 para diagonal
      moveCost = isDiagonal(current, neighbor) ? 1.414 : 1.0
      tentative_g = gScore[current] + moveCost
      
      if tentative_g < gScore.get(neighbor, INF):
        cameFrom[neighbor] = current
        gScore[neighbor] = tentative_g
        fScore[neighbor] = tentative_g + heuristic(neighbor, goal)
        openSet.enqueue(neighbor, fScore[neighbor])
  
  return [] // No path found

function heuristic(a, b):
  // Chebyshev distance (permite diagonales)
  dx = abs(a.x - b.x)
  dy = abs(a.y - b.y)
  return max(dx, dy) + (sqrt(2) - 1) * min(dx, dy)

Optimizaciones:
  1. Jump Point Search (JPS) para mapas con grandes áreas abiertas
  2. Hierarchical A* para mapas grandes (dividir en regiones)
  3. Path caching: reutilizar paths parciales
  4. Limitar maxSteps para evitar lag con paths imposibles
```

#### Tarea 2.4: Sistema de combate
**Objetivo:** Fórmulas de daño estilo Tibia.

**Algoritmo de daño melee:**
```
function calculateMeleeDamage(attacker, defender):
  // Fórmula de Tibia (simplificada)
  skillLevel = attacker.getWeaponSkill()
  attackValue = attacker.weapon.attack
  
  // Daño base
  maxDamage = (skillLevel * attackValue * 0.085) + (skillLevel * 0.085) + (attackValue * 0.085)
  
  // Factor aleatorio
  rawDamage = random(0, maxDamage)
  
  // Defensa
  defenseValue = defender.shield.defense + defender.shieldingSkill * 0.08
  armor = defender.getTotalArmor()
  
  // Reducción
  blocked = random(defenseValue * 0.5, defenseValue)
  absorbed = random(armor * 0.5, armor)
  
  finalDamage = max(0, rawDamage - blocked - absorbed)
  
  return finalDamage

function calculateDistanceDamage(attacker, defender, distance):
  skillLevel = attacker.getSkill(SkillType.Distance)
  attackValue = attacker.weapon.attack
  
  // Penalización por distancia
  distancePenalty = 1.0 - (distance * 0.02) // -2% por tile de distancia
  
  maxDamage = (skillLevel * attackValue * 0.09) * distancePenalty
  rawDamage = random(0, maxDamage)
  
  // Armor reduce daño
  armor = defender.getTotalArmor()
  absorbed = random(armor * 0.5, armor)
  
  return max(0, rawDamage - absorbed)
```

**Algoritmo de skill training:**
```
function onAttack(attacker, defender, weaponType):
  // Entrenar skill de arma
  attacker.skills.addTries(weaponType, 1, attacker.vocation)
  
  // Entrenar shielding del defensor (si está bloqueando)
  if defender.isBlocking:
    defender.skills.addTries(SkillType.Shielding, 1, defender.vocation)

function onMagicAttack(caster, spell):
  // Entrenar magic level
  manaCost = spell.manaCost
  caster.skills.addTries(SkillType.MagicLevel, manaCost, caster.vocation)
```

#### Tarea 2.5: Sistema de targeting y auto-attack
**Objetivo:** Click en criatura para atacar automáticamente.

**Algoritmo:**
```
Client:
  1. Click en criatura → enviar TargetPacket(creatureId)
  2. Mostrar indicador de target (cuadro rojo alrededor)
  3. Si target muere o sale de rango → limpiar target

Server:
  1. Recibir TargetPacket
  2. Validar que la criatura existe y está en rango visual
  3. Setear player.targetId = creatureId
  4. En cada tick del combat system:
     a. Si player tiene target:
        - Si en rango melee (1 tile) → ejecutar ataque melee
        - Si tiene arma de distancia y en rango → ejecutar ataque distance
        - Si fuera de rango → mover hacia el target (auto-walk)
     b. Aplicar cooldown entre ataques basado en speed
```

#### Tarea 2.6: Line of Sight (LoS)
**Objetivo:** Verificar si hay línea de visión entre dos puntos.

**Algoritmo — Bresenham's Line:**
```
function hasLineOfSight(x0, y0, x1, y1):
  dx = abs(x1 - x0)
  dy = abs(y1 - y0)
  sx = sign(x1 - x0)
  sy = sign(y1 - y0)
  err = dx - dy
  
  while (x0, y0) != (x1, y1):
    // Verificar si el tile actual bloquea proyectiles
    if map.blocksProjectile(x0, y0):
      return false
    
    e2 = 2 * err
    if e2 > -dy:
      err -= dy
      x0 += sx
    if e2 < dx:
      err += dx
      y0 += sy
  
  return true
```

---

### 🎒 FASE 3: Items, Inventario y Loot

**Objetivo:** Sistema completo de items con inventario, equipamiento, containers y loot.

#### Tarea 3.1: Definición de items
**Objetivo:** Catálogo de items con propiedades.

**Estructura de datos:**
```csharp
public struct ItemTemplate {
    public ushort Id;
    public string Name;
    public ItemType Type;        // Weapon, Armor, Shield, Helmet, Legs, Boots, Ring, Amulet, 
                                 // Rune, Potion, Food, Container, Key, Tool, Decoration
    public ushort Weight;        // En onzas (oz)
    public bool Stackable;
    public byte MaxStack;        // 100 para la mayoría
    public bool Moveable;
    public bool BlocksWalk;
    public bool BlocksProjectile;
    
    // Para armas
    public byte Attack;
    public byte Defense;
    public byte ExtraDefense;
    public WeaponType WeaponType; // Sword, Axe, Club, Distance, Wand, Rod
    public byte Range;            // Para armas de distancia
    
    // Para armaduras
    public byte Armor;
    public EquipSlot Slot;       // Head, Body, Legs, Feet, Ring, Ammo, Backpack
    
    // Para consumibles
    public ushort HealHP, HealMP;
    public ushort Duration;       // Para comida (segundos de regeneración)
    
    // Para containers
    public byte ContainerSlots;   // Capacidad (ej: backpack = 20)
    
    // Para runas
    public ushort SpellId;
    public byte Charges;
    
    // Requisitos
    public byte MinLevel;
    public Vocation RequiredVocation;
}

public enum EquipSlot : byte {
    None, Head, Necklace, Backpack, Body, RightHand, LeftHand, 
    Legs, Feet, Ring, Ammo
}
```

#### Tarea 3.2: Sistema de inventario
**Objetivo:** Inventario con slots de equipamiento y containers anidados.

**Algoritmo:**
```
Estructura del inventario del jugador:
  equipSlots[10]:  // Head, Necklace, Backpack, Body, RightHand, LeftHand, Legs, Feet, Ring, Ammo
    cada slot contiene 0 o 1 item
  
  El slot Backpack es un Container que puede contener otros items/containers

Container (árbol recursivo):
  items: Item[maxSlots]
  parent: Container?  // null si es el backpack principal
  
  function addItem(item):
    // Buscar slot vacío
    for i in 0..maxSlots:
      if items[i] == null:
        items[i] = item
        return true
    // Si no hay espacio, buscar sub-containers
    for i in 0..maxSlots:
      if items[i] is Container:
        if items[i].addItem(item):
          return true
    return false // Inventario lleno

  function removeItem(slotIndex):
    item = items[slotIndex]
    items[slotIndex] = null
    return item

Validación de equipamiento:
  function canEquip(player, item, slot):
    if item.slot != slot: return false
    if player.level < item.minLevel: return false
    if item.requiredVocation != None AND player.vocation != item.requiredVocation: return false
    if item.weight > player.freeCapacity: return false
    return true
```

#### Tarea 3.3: Sistema de loot
**Objetivo:** Drop de items al morir una criatura.

**Algoritmo:**
```csharp
public struct LootEntry {
    public ushort ItemId;
    public float Chance;      // 0.0 a 1.0 (ej: 0.05 = 5%)
    public byte MinCount;
    public byte MaxCount;
}

function generateLoot(creature):
  loot = []
  for entry in creature.lootTable:
    if random() <= entry.chance:
      count = random(entry.minCount, entry.maxCount)
      loot.add(ItemInstance(entry.itemId, count))
  
  // Crear corpse container en el tile donde murió
  corpse = createCorpse(creature)
  for item in loot:
    corpse.addItem(item)
  
  // El corpse desaparece después de N segundos
  scheduleRemoval(corpse, 120) // 2 minutos
  
  return corpse
```

#### Tarea 3.4: Drag & Drop de items
**Objetivo:** Mover items entre inventario, suelo y containers.

**Algoritmo:**
```
Tipos de movimiento:
  1. Inventario → Inventario (cambiar slot)
  2. Inventario → Suelo (drop)
  3. Suelo → Inventario (pickup)
  4. Container → Container
  5. Inventario → Container
  6. Container → Inventario
  7. Equip slot → Backpack (desequipar)
  8. Backpack → Equip slot (equipar)

Validaciones del servidor:
  - Distancia: jugador debe estar a ≤1 tile del item en el suelo
  - Peso: no exceder capacidad
  - Requisitos: nivel, vocación para equipar
  - Anti-cheat: verificar que el item existe donde el cliente dice
  - Stackable: si el destino tiene el mismo item stackable, merge
```

---

### 🗣️ FASE 4: NPCs, Diálogos y Quests

#### Tarea 4.1: Sistema de NPCs
**Objetivo:** NPCs con posición fija, diálogos y funciones (shop, quest, teleport).

**Estructura:**
```csharp
public class NpcTemplate {
    public string Name;
    public int TileX, TileY, Floor;
    public NpcType Type;          // Trader, QuestGiver, Banker, Teleporter
    public DialogTree DialogTree;
    public List<ShopItem> ShopItems; // Para traders
}

public class DialogTree {
    public DialogNode Root;
}

public class DialogNode {
    public string Text;                    // Lo que dice el NPC
    public List<string> Keywords;          // Palabras clave que activan este nodo
    public List<DialogNode> Children;      // Respuestas posibles
    public DialogAction? Action;           // Acción al llegar a este nodo
}

public enum DialogAction {
    OpenShop, GiveQuest, CompleteQuest, Teleport, Heal, DepositGold, WithdrawGold
}
```

**Algoritmo de diálogo (keyword matching):**
```
function processPlayerMessage(npc, player, message):
  message = message.toLower().trim()
  currentNode = player.currentDialogNode ?? npc.dialogTree.root
  
  // Buscar keyword match en los hijos del nodo actual
  for child in currentNode.children:
    for keyword in child.keywords:
      if message.contains(keyword):
        player.currentDialogNode = child
        
        // Ejecutar acción si existe
        if child.action:
          executeAction(child.action, npc, player)
        
        // Enviar respuesta al jugador
        sendNpcMessage(player, npc.name, child.text)
        return
  
  // No match → respuesta por defecto
  sendNpcMessage(player, npc.name, "I don't understand.")
```

#### Tarea 4.2: Sistema de quests
**Objetivo:** Misiones con objetivos, recompensas y tracking.

**Estructura:**
```csharp
public class Quest {
    public int Id;
    public string Name, Description;
    public int MinLevel;
    public List<QuestObjective> Objectives;
    public QuestReward Reward;
}

public class QuestObjective {
    public ObjectiveType Type;    // KillCreature, CollectItem, TalkToNpc, ReachLocation
    public int TargetId;          // CreatureId, ItemId, NpcId, etc.
    public int RequiredCount;
}

public class QuestReward {
    public long Experience;
    public int Gold;
    public List<(ushort itemId, byte count)> Items;
}

// Estado del jugador
public class PlayerQuestState {
    public int QuestId;
    public QuestStatus Status;    // NotStarted, InProgress, Completed
    public int[] ObjectiveProgress; // Progreso por cada objetivo
}
```

---

### 💬 FASE 5: Chat y Comunicación Social

#### Tarea 5.1: Sistema de chat
**Objetivo:** Chat con múltiples canales estilo Tibia.

**Canales:**
```
- Default (blanco): Mensaje visible para jugadores cercanos (30 tiles)
- Yell (amarillo): Mensaje visible en mayor rango (50 tiles), consume soul
- Whisper (gris): Solo visible a 1 tile de distancia
- Private Message: Mensaje directo a otro jugador
- Trade Channel: Canal global de comercio
- Help Channel: Canal de ayuda
- Guild Channel: Solo miembros del guild
- Party Channel: Solo miembros del party
```

**Algoritmo:**
```
function processChat(sender, message, channel):
  // Anti-spam: verificar cooldown
  if sender.lastMessageTime + CHAT_COOLDOWN > now():
    return "You are sending messages too fast."
  
  // Filtrar palabras prohibidas
  message = filterProfanity(message)
  
  switch channel:
    case DEFAULT:
      recipients = getPlayersInRange(sender.position, 30)
      broadcast(recipients, ChatPacket(sender.name, message, ChatColor.White))
      
    case YELL:
      if sender.stats.soul < 1: return "Not enough soul points."
      sender.stats.soul -= 1
      recipients = getPlayersInRange(sender.position, 50)
      broadcast(recipients, ChatPacket(sender.name, message.toUpper(), ChatColor.Yellow))
      
    case WHISPER:
      recipients = getPlayersInRange(sender.position, 1)
      broadcast(recipients, ChatPacket(sender.name, message, ChatColor.Gray))
      
    case PRIVATE:
      target = findPlayerByName(message.targetName)
      if target == null: return "Player is not online."
      send(target, PrivateChatPacket(sender.name, message))
      send(sender, PrivateChatPacket("To " + target.name, message))
```

#### Tarea 5.2: Sistema de Party
**Objetivo:** Grupos de jugadores con experiencia compartida.

**Algoritmo de experiencia compartida:**
```
function distributePartyExperience(party, killedCreature, killerPos):
  baseExp = killedCreature.experience
  
  // Solo miembros en rango (30 tiles del kill)
  nearbyMembers = party.members.filter(m => distance(m.pos, killerPos) <= 30)
  
  if nearbyMembers.count == 0: return
  
  // Bonus por party (Tibia formula)
  // Bonus = (memberCount - 1) * 0.2, max 1.0 (100% extra)
  partyBonus = min(1.0, (nearbyMembers.count - 1) * 0.2)
  totalExp = baseExp * (1 + partyBonus)
  
  // Verificar vocaciones únicas para bonus extra
  uniqueVocations = nearbyMembers.select(m => m.vocation).distinct().count()
  if uniqueVocations >= 4: // Las 4 vocaciones presentes
    totalExp *= 1.1 // 10% bonus extra
  
  // Distribuir equitativamente
  expPerMember = totalExp / nearbyMembers.count
  
  for member in nearbyMembers:
    // Ajustar por stamina
    staminaMultiplier = calculateStaminaMultiplier(member.stamina)
    member.addExperience(expPerMember * staminaMultiplier)
```

---

### 🔮 FASE 6: Spells, Runas y Magia

#### Tarea 6.1: Sistema de spells
**Objetivo:** Spells con diferentes tipos, áreas de efecto y cooldowns.

**Estructura:**
```csharp
public class SpellTemplate {
    public ushort Id;
    public string Name;
    public string Words;          // "exura", "exori vis", etc.
    public SpellType Type;        // Instant, Rune, Conjure
    public TargetType Target;     // Self, Single, Area, Direction
    public Element Element;       // Physical, Fire, Ice, Energy, Earth, Holy, Death
    public int ManaCost;
    public int SoulCost;
    public int Cooldown;          // En milisegundos
    public int GroupCooldown;     // Cooldown compartido con otros spells del grupo
    public int MinLevel;
    public Vocation[] Vocations;  // Vocaciones que pueden usarlo
    
    // Para área de efecto
    public AreaShape Shape;       // Circle, Cross, Beam, Wave, Ring
    public int Radius;
    
    // Efecto
    public int MinDamage, MaxDamage; // Negativo = heal
    public StatusEffect? AppliedEffect; // Poison, Fire, Paralyze, etc.
}
```

**Algoritmo de casting:**
```
function castSpell(caster, spellWords):
  spell = findSpellByWords(spellWords)
  if spell == null: return "Unknown spell."
  
  // Validaciones
  if caster.level < spell.minLevel: return "You need level {spell.minLevel}."
  if caster.vocation not in spell.vocations: return "Your vocation cannot use this spell."
  if caster.mp < spell.manaCost: return "Not enough mana."
  if caster.soul < spell.soulCost: return "Not enough soul."
  if isOnCooldown(caster, spell): return "Spell is on cooldown."
  
  // Consumir recursos
  caster.mp -= spell.manaCost
  caster.soul -= spell.soulCost
  setCooldown(caster, spell)
  
  // Entrenar magic level
  caster.skills.addTries(SkillType.MagicLevel, spell.manaCost, caster.vocation)
  
  // Calcular daño/heal
  basePower = calculateSpellPower(caster, spell)
  
  // Aplicar según tipo de target
  switch spell.target:
    case SELF:
      applyEffect(caster, caster, spell, basePower)
      
    case SINGLE:
      target = caster.target
      if target == null: return "No target."
      if distance(caster, target) > spell.radius: return "Target too far."
      if !hasLineOfSight(caster, target): return "Target not in sight."
      applyEffect(caster, target, spell, basePower)
      
    case AREA:
      targets = getEntitiesInArea(caster.position, spell.shape, spell.radius)
      for target in targets:
        applyEffect(caster, target, spell, basePower)
      
    case DIRECTION:
      targets = getEntitiesInBeam(caster.position, caster.direction, spell.radius)
      for target in targets:
        applyEffect(caster, target, spell, basePower)

function calculateSpellPower(caster, spell):
  magicLevel = caster.skills.getLevel(SkillType.MagicLevel)
  level = caster.level
  
  // Fórmula de Tibia para daño mágico
  minDmg = (level * 0.2 + magicLevel * spell.minDamage * 0.05)
  maxDmg = (level * 0.2 + magicLevel * spell.maxDamage * 0.05)
  
  return random(minDmg, maxDmg)
```

#### Tarea 6.2: Áreas de efecto
**Objetivo:** Definir formas de área para spells.

**Algoritmos de área:**
```
// Círculo (exori, utori)
function getCircleArea(center, radius):
  tiles = []
  for dx in -radius..radius:
    for dy in -radius..radius:
      if dx*dx + dy*dy <= radius*radius:
        tiles.add((center.x + dx, center.y + dy))
  return tiles

// Cruz (exori gran)
function getCrossArea(center, radius):
  tiles = [(center.x, center.y)]
  for i in 1..radius:
    tiles.add((center.x + i, center.y))
    tiles.add((center.x - i, center.y))
    tiles.add((center.x, center.y + i))
    tiles.add((center.x, center.y - i))
  return tiles

// Beam/Rayo (exevo vis lux)
function getBeamArea(origin, direction, length):
  tiles = []
  (dx, dy) = directionToOffset(direction)
  for i in 1..length:
    tiles.add((origin.x + dx*i, origin.y + dy*i))
  return tiles

// Wave (exevo flam hur)
function getWaveArea(origin, direction, length, width):
  tiles = []
  (dx, dy) = directionToOffset(direction)
  (px, py) = perpendicular(dx, dy)
  
  for i in 1..length:
    // Ancho crece con la distancia
    currentWidth = (width * i) / length
    for w in -currentWidth..currentWidth:
      x = origin.x + dx*i + px*w
      y = origin.y + dy*i + py*w
      tiles.add((x, y))
  return tiles
```

#### Tarea 6.3: Efectos de estado (DoT, Buffs, Debuffs)
**Objetivo:** Veneno, fuego, paralyze, haste, etc.

**Algoritmo:**
```csharp
public class StatusEffect {
    public StatusType Type;       // Poison, Fire, Energy, Paralyze, Haste, Invisible
    public int DamagePerTick;     // Para DoTs
    public float TickInterval;    // Segundos entre ticks
    public float Duration;        // Duración total
    public float Elapsed;         // Tiempo transcurrido
    public float SpeedModifier;   // Para Haste (+30%) o Paralyze (-50%)
}

function updateStatusEffects(entity, deltaTime):
  for effect in entity.statusEffects:
    effect.elapsed += deltaTime
    
    if effect.elapsed >= effect.duration:
      removeEffect(entity, effect)
      continue
    
    // Aplicar tick de daño
    effect.tickTimer += deltaTime
    if effect.tickTimer >= effect.tickInterval:
      effect.tickTimer -= effect.tickInterval
      
      switch effect.type:
        case POISON:
          entity.takeDamage(effect.damagePerTick, Element.Earth)
          // El daño de veneno decrece con el tiempo
          effect.damagePerTick = max(1, effect.damagePerTick - 1)
          
        case FIRE:
          entity.takeDamage(effect.damagePerTick, Element.Fire)
          
        case ENERGY:
          entity.takeDamage(effect.damagePerTick, Element.Energy)
    
    // Modificadores de velocidad
    if effect.type == HASTE:
      entity.speedModifier = 1.0 + effect.speedModifier
    elif effect.type == PARALYZE:
      entity.speedModifier = 1.0 - effect.speedModifier
```

---

### 💾 FASE 7: Persistencia y Cuentas

#### Tarea 7.1: Base de datos
**Objetivo:** Persistir cuentas, personajes, items, guilds.

**Opciones de DB:**
- **SQLite** — Para desarrollo/servidor pequeño
- **PostgreSQL** — Para producción
- **MySQL/MariaDB** — Alternativa popular

**Schema principal:**
```sql
CREATE TABLE accounts (
    id SERIAL PRIMARY KEY,
    username VARCHAR(32) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    email VARCHAR(255),
    premium_days INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW(),
    last_login TIMESTAMP
);

CREATE TABLE players (
    id SERIAL PRIMARY KEY,
    account_id INT REFERENCES accounts(id),
    name VARCHAR(32) UNIQUE NOT NULL,
    vocation SMALLINT DEFAULT 0,
    level INT DEFAULT 1,
    experience BIGINT DEFAULT 0,
    health INT DEFAULT 150,
    max_health INT DEFAULT 150,
    mana INT DEFAULT 50,
    max_mana INT DEFAULT 50,
    capacity INT DEFAULT 400,
    soul INT DEFAULT 100,
    stamina INT DEFAULT 2520,
    pos_x INT DEFAULT 100,
    pos_y INT DEFAULT 100,
    pos_z INT DEFAULT 7,
    speed FLOAT DEFAULT 150,
    -- Skills
    skill_fist INT DEFAULT 10, skill_fist_tries BIGINT DEFAULT 0,
    skill_club INT DEFAULT 10, skill_club_tries BIGINT DEFAULT 0,
    skill_sword INT DEFAULT 10, skill_sword_tries BIGINT DEFAULT 0,
    skill_axe INT DEFAULT 10, skill_axe_tries BIGINT DEFAULT 0,
    skill_distance INT DEFAULT 10, skill_distance_tries BIGINT DEFAULT 0,
    skill_shielding INT DEFAULT 10, skill_shielding_tries BIGINT DEFAULT 0,
    skill_fishing INT DEFAULT 10, skill_fishing_tries BIGINT DEFAULT 0,
    skill_magic INT DEFAULT 0, skill_magic_tries BIGINT DEFAULT 0,
    -- Timestamps
    created_at TIMESTAMP DEFAULT NOW(),
    last_login TIMESTAMP
);

CREATE TABLE player_items (
    id SERIAL PRIMARY KEY,
    player_id INT REFERENCES players(id),
    item_id INT NOT NULL,
    count INT DEFAULT 1,
    slot_type VARCHAR(20), -- 'inventory', 'equipment', 'depot'
    slot_index INT,
    container_id INT REFERENCES player_items(id), -- Para items dentro de containers
    attributes JSONB -- Propiedades especiales (charges, duration, etc.)
);

CREATE TABLE guilds (
    id SERIAL PRIMARY KEY,
    name VARCHAR(32) UNIQUE NOT NULL,
    owner_id INT REFERENCES players(id),
    motd TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE guild_members (
    guild_id INT REFERENCES guilds(id),
    player_id INT REFERENCES players(id),
    rank VARCHAR(32) DEFAULT 'Member',
    PRIMARY KEY (guild_id, player_id)
);
```

#### Tarea 7.2: Sistema de login
**Objetivo:** Autenticación segura con hashing de contraseñas.

**Algoritmo:**
```
Registro:
  1. Validar username (3-32 chars, alfanumérico)
  2. Validar password (min 8 chars, al menos 1 número)
  3. Hash password con BCrypt (cost factor 12)
  4. Insertar en DB
  5. Crear personaje inicial

Login:
  1. Recibir LoginPacket(username, password)
  2. Buscar cuenta en DB
  3. Verificar BCrypt.Verify(password, stored_hash)
  4. Si válido:
     a. Cargar datos del personaje
     b. Cargar inventario
     c. Cargar quests
     d. Spawn en última posición guardada
     e. Enviar LoginSuccessPacket con datos completos
  5. Si inválido:
     a. Enviar LoginFailedPacket("Invalid credentials")
     b. Rate limiting: máx 5 intentos por IP en 5 minutos

Auto-save periódico:
  Cada 60 segundos, guardar:
  - Posición actual
  - HP/MP actuales
  - Skills (nivel + tries)
  - Inventario
  - Estado de quests
```

---

### 🎨 FASE 8: UI Avanzada y Sprites

#### Tarea 8.1: Sistema de sprites
**Objetivo:** Reemplazar rectángulos por sprites animados.

**Formato de spritesheet:**
```
Sprite Atlas:
  - Cada sprite: 32x32 píxeles (tamaño de tile)
  - Outfits: 4 direcciones × N frames de animación
  - Criaturas: 4 direcciones × N frames
  - Items: 1 frame estático (32x32)
  - Tiles: 1 frame estático (32x32)
  - Efectos: N frames de animación

Outfit system (como Tibia):
  - Base body (por tipo de outfit)
  - Coloreable con 4 colores: head, body, legs, feet
  - Addons: 2 addons opcionales por outfit
  
Algoritmo de colorización:
  1. Sprite base tiene áreas marcadas con colores template
  2. Al renderizar, reemplazar colores template por colores del jugador
  3. Usar lookup table para mapear template → player color
```

#### Tarea 8.2: Minimapa
**Objetivo:** Mapa pequeño en esquina mostrando área explorada.

**Algoritmo:**
```
Estructura:
  minimapData[mapWidth][mapHeight]: byte (color index)
  explored[mapWidth][mapHeight]: bool

Actualización:
  Cada vez que un tile se vuelve visible para el jugador:
    explored[x][y] = true
    minimapData[x][y] = tileColorIndex(tileType)

Renderizado:
  1. Calcular viewport del minimapa (centrado en jugador)
  2. Para cada pixel del minimapa widget:
     a. Mapear pixel → tile coordinates
     b. Si explored: dibujar color del tile
     c. Si no explored: dibujar negro
  3. Dibujar punto blanco en posición del jugador
  4. Dibujar puntos verdes para party members
  5. Dibujar puntos rojos para criaturas cercanas (si tiene spell de sense)
```

#### Tarea 8.3: Ventanas arrastrables (GUI System)
**Objetivo:** Ventanas de inventario, skills, battle list, etc.

**Algoritmo de ventanas:**
```
class Window:
  x, y: int (posición)
  width, height: int
  title: string
  visible: bool
  dragging: bool
  dragOffsetX, dragOffsetY: int
  minimized: bool
  children: List<Widget>

function updateWindows(windows):
  // Procesar en orden inverso (ventana de arriba primero)
  for window in windows.reversed():
    if !window.visible: continue
    
    mousePos = getMousePosition()
    
    // Drag
    if mousePressed AND mouseInTitleBar(window, mousePos):
      window.dragging = true
      window.dragOffsetX = mousePos.x - window.x
      window.dragOffsetY = mousePos.y - window.y
      bringToFront(window)
    
    if window.dragging:
      window.x = mousePos.x - window.dragOffsetX
      window.y = mousePos.y - window.dragOffsetY
      
      if mouseReleased:
        window.dragging = false
    
    // Minimize button
    if mouseClicked AND mouseInMinimizeButton(window, mousePos):
      window.minimized = !window.minimized
    
    // Close button
    if mouseClicked AND mouseInCloseButton(window, mousePos):
      window.visible = false
    
    // Update children widgets
    if !window.minimized:
      for child in window.children:
        child.update(mousePos)
```

---

### 🔊 FASE 9: Audio, Efectos y Polish

#### Tarea 9.1: Sistema de audio
**Objetivo:** Música de fondo y efectos de sonido.

**Implementación con Raylib:**
```
Categorías de sonido:
  - Música de ambiente (por zona del mapa)
  - Efectos de combate (hit, miss, spell)
  - Efectos de UI (click, open container, equip)
  - Efectos ambientales (agua, viento, fuego)
  - Voces de criaturas

Algoritmo de audio espacial:
  function playSound3D(soundId, sourcePos, listenerPos):
    distance = distance(sourcePos, listenerPos)
    maxDistance = 15 // tiles
    
    if distance > maxDistance: return
    
    volume = 1.0 - (distance / maxDistance)
    volume = volume * volume // Cuadrático para atenuación más natural
    
    // Pan (izquierda/derecha)
    dx = sourcePos.x - listenerPos.x
    pan = clamp(dx / maxDistance, -1.0, 1.0)
    
    Raylib.SetSoundVolume(sound, volume * masterVolume)
    Raylib.SetSoundPan(sound, pan)
    Raylib.PlaySound(sound)
```

#### Tarea 9.2: Efectos visuales
**Objetivo:** Animaciones de combate, magia, texto flotante.

**Algoritmo de texto flotante (damage numbers):**
```
class FloatingText:
  text: string
  x, y: float
  velocityY: float = -60  // Sube
  lifetime: float = 1.5
  elapsed: float = 0
  color: Color
  fontSize: int

function updateFloatingTexts(texts, deltaTime):
  for text in texts:
    text.elapsed += deltaTime
    text.y += text.velocityY * deltaTime
    text.velocityY *= 0.95 // Desacelera
    
    // Fade out en el último 30%
    if text.elapsed > text.lifetime * 0.7:
      alpha = 1.0 - ((text.elapsed - text.lifetime * 0.7) / (text.lifetime * 0.3))
      text.color.a = (byte)(alpha * 255)
    
    if text.elapsed >= text.lifetime:
      texts.remove(text)

function renderFloatingTexts(texts, cameraOffset):
  for text in texts:
    screenX = text.x + cameraOffset.x
    screenY = text.y + cameraOffset.y
    Raylib.DrawText(text.text, screenX, screenY, text.fontSize, text.color)
```

---

### 🏗️ FASE 10: Sistemas Avanzados

#### Tarea 10.1: Sistema de casas
**Objetivo:** Casas comprables con puertas, camas y almacenamiento.

#### Tarea 10.2: Sistema de guilds
**Objetivo:** Guilds con ranks, guild hall, guild wars.

#### Tarea 10.3: Market/Trade system
**Objetivo:** Sistema de comercio entre jugadores y market global.

#### Tarea 10.4: Sistema de eventos
**Objetivo:** Eventos automáticos (invasiones, double exp, etc.).

#### Tarea 10.5: Anti-cheat
**Objetivo:** Validación del lado del servidor para prevenir hacks.

**Algoritmos anti-cheat:**
```
1. Speed hack detection:
   - Trackear timestamps de movimientos
   - Si un jugador se mueve más rápido que su speed permite → kick
   
2. Teleport detection:
   - Verificar que cada movimiento es a un tile adyacente
   - Si salta más de 1 tile → rechazar movimiento

3. Item duplication:
   - Todas las operaciones de items son atómicas en el servidor
   - Verificar existencia del item antes de cada operación
   - Usar transacciones de DB

4. Damage validation:
   - Recalcular daño en el servidor
   - Ignorar valores de daño enviados por el cliente

5. Rate limiting:
   - Máximo N acciones por segundo por tipo
   - Máximo N paquetes por segundo total
```

---

### 🚀 FASE 11: Optimización y Deployment

#### Tarea 11.1: Optimización de red
```
1. Delta compression: solo enviar cambios desde el último state
2. Bit packing: usar bits individuales para flags
3. Interpolación predictiva del lado del cliente
4. Packet batching: agrupar múltiples updates en un paquete
```

#### Tarea 11.2: Optimización de servidor
```
1. Object pooling para entidades y paquetes
2. Spatial hashing para queries de área
3. Async I/O para operaciones de DB
4. Thread pool para procesamiento paralelo de áreas del mapa
```

#### Tarea 11.3: Deployment
```
1. Docker container para el servidor
2. CI/CD pipeline (GitHub Actions)
3. Monitoring y logging (Prometheus + Grafana)
4. Backup automático de DB
5. Auto-scaling si se usa cloud
```

---

## 📊 Resumen de Prioridades

| Prioridad | Fase | Impacto en Gameplay |
|---|---|---|
| 🔴 P0 | Fase 0 (Fundamentos) | Base para todo lo demás |
| 🔴 P0 | Fase 1 (Mapas) | Sin mapa real no hay juego |
| 🔴 P0 | Fase 2 (Combate) | Core gameplay loop |
| 🟡 P1 | Fase 3 (Items) | Progresión del jugador |
| 🟡 P1 | Fase 7 (Persistencia) | Sin esto, no hay progreso permanente |
| 🟡 P1 | Fase 6 (Spells) | Diferenciación de vocaciones |
| 🟢 P2 | Fase 4 (NPCs) | Contenido y quests |
| 🟢 P2 | Fase 5 (Chat) | Social |
| 🟢 P2 | Fase 8 (UI/Sprites) | Polish visual |
| 🔵 P3 | Fase 9 (Audio) | Inmersión |
| 🔵 P3 | Fase 10 (Avanzados) | Endgame content |
| 🔵 P3 | Fase 11 (Deploy) | Producción |

---

## 🎯 MVP Recomendado (Mínimo Viable para "Jugar")

Para tener algo jugable lo antes posible, implementar en este orden:

1. **Mapa real** con tiles variados (Fase 1.1 + 1.2)
2. **Criaturas básicas** con IA simple (Fase 2.1 + 2.2)
3. **Combate melee** funcional (Fase 2.4)
4. **Pathfinding** para criaturas (Fase 2.3)
5. **Loot básico** (gold + items) (Fase 3.3)
6. **Inventario** con equipamiento (Fase 3.2)
7. **Persistencia** con SQLite (Fase 7.1 + 7.2)
8. **Chat básico** (Fase 5.1)
9. **3-5 spells** por vocación (Fase 6.1)
10. **Sprites** básicos (Fase 8.1)

**Tiempo estimado del MVP: 2-3 meses** de desarrollo enfocado.

---

*Documento generado el 2026-02-18 por Alex (Atoms Platform)*
*Basado en análisis del repositorio: jahazielhigareda/GameRaylibSharp*