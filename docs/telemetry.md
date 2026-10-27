# Telemetría de poder

Mide el poder real de cartas, reliquias, jefes y encuentros con las partidas de quien juega,
y lo guarda en Supabase (Postgres) para consultarlo con SQL. Por ahora corre en local con Docker;
el mismo esquema sirve después para un proyecto de Supabase alojado y compartido entre jugadores.

## Cómo funciona

```text
Juego (dominio)         PowerTracker suma enteros en memoria: jugadas, maná, daño y bloqueo por carta;
                        cada combate (sala, encuentro, turnos, vida perdida, mazo y reliquias al entrar);
                        cada oferta de cartas/reliquias y qué se eligió.
Presentación            SceneManager le avisa en cada transición; CombatScene en cada carta jugada;
                        Tienda y Gachapón en cada compra o tirada. PowerJson lo convierte en un JSON.
Runtime (Unity)         SupabaseTelemetry escribe el JSON en disco en un hilo de fondo y lo sube con
                        UnityWebRequest desde una corrutina que solo existe mientras hay algo que enviar.
Supabase                public.ingest_run(payload) valida y reparte la partida en tablas (esquema telemetry);
                        las vistas del esquema analytics calculan el poder.
```

- **Rendimiento.** Durante el combate solo se suman enteros. El JSON se arma al volver al mapa
  (punto de control) y al terminar la partida: unos 10 KB de media y 36 KB con 30 combates, y
  0,1 ms de media (0,4 ms como máximo) al serializarlo en .NET 8 de escritorio. Mono en Unity será
  algo más lento, pero sigue lejos de un frame. Los archivos y la red nunca bloquean el bucle del juego.
- **Sin conexión.** Cada partida es un archivo en `telemetry/outbox` hasta que el servidor la acepta.
  Si cierras el juego o se cuelga a mitad de partida, el último punto de control se envía como
  `unfinished` en el siguiente arranque. Reenviar una partida no la duplica (`run_id` es la clave).
- **Errores.** Un fallo de la telemetría la apaga para esa partida, nunca rompe el juego. Las
  respuestas 400 del servidor (datos no válidos) se apartan a `telemetry/rejected` y no se reintentan.
- **Carpeta de datos** (Windows): `%USERPROFILE%\AppData\LocalLow\Last Wish\Último Deseo\` →
  `telemetry/` (cola), `telemetry_config.json` (configuración local) y `telemetry/player_id.txt`.

## Arrancar en local (Docker)

Requisitos: Docker Desktop abierto y Node.js (para `npx`). Desde la raíz del repositorio:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Telemetry/LocalSupabase.ps1 start
```

Arranca Supabase con `supabase/config.toml` (solo base de datos, API, Auth y Studio), aplica
`supabase/migrations` y escribe `telemetry_config.json` con la URL y la clave publicable locales.
Después juega en el editor: cada partida terminada aparece en la base al momento.

| Comando | Qué hace |
|---|---|
| `start` | Arranca los contenedores y conecta el juego |
| `connect` / `disconnect` | Solo escribe o borra `telemetry_config.json` |
| `status` | URLs y claves locales |
| `test` | Pruebas SQL (`supabase/tests`) |
| `reset` | Borra los datos locales y reaplica las migraciones |
| `stop` | Para los contenedores (los datos se conservan) |

Studio: <http://127.0.0.1:54323> → SQL Editor. Base: `postgresql://postgres:postgres@127.0.0.1:54322/postgres`.

## Consultas de poder

Las partidas modificadas en **Pruebas** (`tuned`) quedan fuera de las vistas. Cada vista trae una fila
por héroe y otra con `hero = 'all'`. Los residuos (`hp_vs_avg_*`, `death_vs_avg_*`) comparan cada
combate con la media de los combates del mismo héroe, piso y tipo de sala: **negativo = mejor que lo
normal**. Por ejemplo, `-0.05` quiere decir que el héroe pierde 5 puntos de vida máxima menos de lo habitual.

```sql
-- Cartas que más vida ahorran (mínimo 30 combates en el mazo)
select card_id, hero, combats_in_deck, hp_vs_avg_in_deck, death_vs_avg_in_deck,
       pick_rate, damage_per_mana, block_per_play, floor_delta
from analytics.card_power
where hero <> 'all' and combats_in_deck >= 30
order by hp_vs_avg_in_deck;

-- Cartas que nadie elige (con su intervalo: pick_rate_low es el límite inferior al 95 %)
select card_id, hero, offered, picked, pick_rate, pick_rate_low
from analytics.card_power
where offered >= 20
order by pick_rate;

-- Reliquias
select relic_id, hero, offered, pick_rate, combats_with, hp_vs_avg, death_vs_avg, floor_delta
from analytics.relic_power
where combats_with >= 30
order by hp_vs_avg;

-- Jefes: cuánto matan y cuánta vida quitan
select encounter, hero, fights, death_rate, death_rate_low, death_rate_high, avg_hp_lost_pct, avg_turns
from analytics.boss_power
order by hero, death_rate desc;

-- Élites y encuentros normales
select room, encounter, fights, death_rate, avg_hp_lost_pct, hp_vs_floor_avg
from analytics.encounter_power
where hero = 'all' and fights >= 20
order by room, death_rate desc;

-- Antes y después de un parche: content_hash cambia cuando cambia un número de cartas, reliquias o enemigos
select * from analytics.content_versions order by last_seen desc;
select content_hash, encounter, count(*) as fights, avg(died) as death_rate
from analytics.combat_facts
where room = 'boss'
group by content_hash, encounter
order by encounter, content_hash;
```

| Vista | Para qué |
|---|---|
| `analytics.card_power` | Tasa de elección, daño/bloqueo por jugada y por maná, vida y muertes frente a la media, piso alcanzado |
| `analytics.relic_power` | Lo mismo para reliquias (la de élite se regala, así que no cuenta como elección) |
| `analytics.encounter_power`, `analytics.boss_power` | Tasa de muerte con intervalo de Wilson, vida quitada, turnos |
| `analytics.hero_baseline` | Piso medio y mediano al morir por héroe (la base de `floor_delta`) |
| `analytics.combat_facts` | Un combate por fila con su línea base: para consultas propias |
| `analytics.content_versions` | Versiones del juego y del balance presentes en los datos |

Cómo leerlo sin engañarse:

- **Muestra.** Con pocos combates todo parece roto. Mira los intervalos (`*_low` y `*_high`) y filtra por un mínimo.
- **Selección.** Una carta que se elige en mazos que ya iban bien parece fuerte aunque no lo sea. Los
  residuos por piso y sala corrigen una parte; la otra la cubre comparar `hp_vs_avg_played` con `hp_vs_avg_in_deck`.
- **Daño por jugada.** Cuenta la vida que quitó la jugada (incluidas las reliquias que se disparan con
  ella), no el veneno ni el fuego del turno enemigo.

## Qué se envía

Un JSON por partida (esquema 1) con: id aleatorio de partida, id anónimo de instalación, versión del
juego, `content_hash`, héroe, semilla, roster, si se usó Pruebas, cómo terminó, piso, salas, oro, vida,
tiempo jugado, mazo final, reliquias (con piso y sala de origen), elecciones y combates. No incluye
nombres, cuentas, IP ni datos del equipo. Las claves exactas están en `PowerJson.Write` y las
valida `public.ingest_run` (`supabase/migrations/20261023180000_power_telemetry.sql`).

La clave publicable va dentro del juego. Es pública por diseño: con ella solo se puede llamar a
`ingest_run`, porque los esquemas `telemetry` y `analytics` no están expuestos en la API.

## Pasar a Supabase alojado (compartido)

1. Crea un proyecto en Supabase y aplica el esquema: `npx supabase link --project-ref <ref>` y
   `npx supabase db push`.
2. Pon `url` (`https://<ref>.supabase.co`) y la clave publicable en `Assets/Resources/Telemetry/telemetry.json`.
   Ese archivo va en la build; el `telemetry_config.json` local lo sigue sobrescribiendo en tu equipo.
3. Antes de publicar el juego: añade una opción en Ajustes para desactivar las estadísticas y un aviso
   de privacidad. Si hay jugadores de la UE, el RGPD puede exigir consentimiento previo. Con muchos
   jugadores, plantea limitar envíos por `player_id` y materializar las vistas.

Para apagar la telemetría en un equipo: `{"enabled": false}` en `telemetry_config.json`. Las pruebas
PlayMode la apagan solas (`SupabaseTelemetry.Suppressed`).

## Archivos

| Archivo | Papel |
|---|---|
| `Assets/Scripts/Domain/PowerRecords.cs`, `PowerTracker.cs` | Registro de la partida (C# puro) |
| `Assets/Scripts/Presentation/Telemetry/PowerJson.cs`, `PowerTracking.cs` | JSON, interfaz `ITelemetrySink` y llamadas seguras |
| `Assets/Scripts/Presentation/SceneManager.cs` (+ `CombatScene.Turns`, `ShopScene`, `GachaScene`) | Dónde se informa al tracker |
| `Assets/Scripts/Runtime/SupabaseTelemetry.cs` | Configuración, cola en disco y subida |
| `Assets/Resources/Telemetry/telemetry.json` | Configuración que va en la build |
| `supabase/config.toml`, `supabase/migrations/`, `supabase/tests/` | Supabase local, esquema y pruebas SQL |
| `Tools/Telemetry/LocalSupabase.ps1` | Arrancar, conectar, probar y parar |
| `Assets/Tests/EditMode/PowerTelemetryTests.cs`, `Assets/Tests/PlayMode/TelemetryOutboxTests.cs` | Pruebas |

`ProjectSettings` permite HTTP (*Allow downloads over HTTP*) porque Supabase local no usa TLS.
`TelemetryConfig.Endpoint` solo acepta `http://` hacia este equipo; cualquier otra dirección debe ser `https://`.
