# Bestiario

Todos los enemigos del juego: los 19 originales del juego Python, los 17 nuevos del piso 1, los 15 nuevos del piso 2 y los 15 nuevos del piso 3.
Abreviaturas de los patrones: **A x** = ataque de x, **A x×n** = n golpes de x, **B x** = gana x de escudo.
Los números son los del piso 1. En pisos superiores, vida y valores se multiplican por `1 + 0,15 × (piso − 1)`.
Los estados no escalan.

## Contenido activo según la partida

| Partida | Contenido de enemigos |
|---|---|
| Partida del menú (`EnemyRoster.Expanded`) | Piso 1: originales y nuevos del piso 1. Piso 2: originales y nuevos del piso 2; el jefe es uno de los 3 jefes nuevos del piso 2 (ya no el Señor de la Cripta). Piso 3: originales y nuevos del piso 3, con los tríos del original; el jefe es uno de los 3 jefes nuevos del piso 3. Pisos 4+: solo los originales, igual que antes. |
| Pruebas de paridad, `new RunState(héroe, semilla)` (`EnemyRoster.Original`) | Solo los originales, tirada idéntica al motor Python. |

Con `Expanded` solo cambian las tiradas de enemigos de los pisos 1, 2 y 3. Usan el mismo número de llamadas al generador aleatorio
(también la tirada de trío del piso 3), así que mapas, recompensas, tiendas, sobres y gachapón no cambian.

## Mecánicas nuevas

| Mecánica | Regla |
|---|---|
| **Invocar** (intención «INV») | Añade esbirros al final del turno enemigo. No actúan el turno en que llegan. Nunca hay más de **3 enemigos vivos** en pantalla. Un enemigo grande (élite o jefe) ocupa lo que dos pequeños: formaciones 3 pequeños, 1 grande + 2 pequeños o 2 grandes. Si no cabe nadie, el invocador planifica su acción alternativa, y esa es la intención que ves. Cada invocador tiene un **máximo por combate** (Madre Larva 4, Rey Rata 5, Sierpe 4); después usa siempre la alternativa. |
| **Esbirro** | Enemigo invocado. **No da oro** y su muerte no alimenta Carroña. Los fragmentos del Cieno y la Plañidera resucitada sí dan oro, porque sustituyen a un enemigo del encuentro. |
| **Pinchos** | Tras cada carta tuya que le haga daño (vida o escudo), te inflige tantos puntos como acumulaciones, una vez por carta. Tu escudo lo absorbe. No es un ataque: no lo cambian señuelos ni reliquias de defensa. |
| **Desvío** y **Réplica** | Cada acumulación anula por completo un golpe. Si el golpe desviado fue durante una de tus cartas, al terminarla te devuelve su Réplica. Veneno y Fuego no se desvían. |
| **Blindaje** | Al empezar su turno conserva hasta tantas acumulaciones de escudo en lugar de perderlo. |
| **Carroña** | Cuando muere otro enemigo (que no sea un esbirro invocado) gana tantas acumulaciones de Fuerza y se cura el triple. |
| **Buche** | Las cartas que se ha tragado de tu descarte (o de tu pila de robo si el descarte está vacío). Vuelven a tu descarte cuando muere. |
| **Carga** y **Aturdido** | Prepara un golpe. Toda vida que pierda (cartas, veneno, reacciones) baja la Carga. Si llega a 0 antes de que actúe, queda Aturdido y pierde el golpe preparado. |
| **Vigilancia** | A partir de tu carta número acumulaciones + 1 de cada turno, cada carta que juegues le da 1 de Fuerza. |
| **Robar Fuerza** | Te quita Fuerza y se la queda. Si no tienes, gana una cantidad fija. |
| **Arrancar escudo** | Te quita un porcentaje de tu escudo antes de golpear y se lo queda. |
| **Muda** | Se quita Veneno, Marcado, Débil, Vulnerable, Frágil, Enredado, Congelado y los 4 elementos, y gana escudo por cada acumulación quitada. |
| **División** | Con la mitad de vida o menos planifica Dividirse. Al ejecutarlo muere y salen dos Cienos Menores con la mitad de su vida cada uno. |
| **Fases** | Cada élite nuevo y cada jefe nuevo en solitario cambia una vez al bajar a la mitad de vida. El cambio se aplaza mientras tenga Carga. |
| **Pareja de jefes** | El Sepulturero y la Plañidera reaccionan a la muerte del otro al instante (ver Los Hermanos Lápida). Hay victoria solo cuando caen los dos. |

## Piso 1: enemigos nuevos

### Normales

| Enemigo | Id | Vida | Identidad | Patrón (en bucle) | Hoja |
|---|---|---|---|---|---|
| Madre Larva | `madre_larva` | 44 | Invocadora | Puesta (invoca 1 Larva, B 5) → Escupitajo Pegajoso (A 6, Débil 1) → Puesta. Sin hueco o sin presupuesto: Nutrir Camada (aliados +2 Fuerza, cura 4 a todos). | `broodmother_sheet.png` |
| Erizo de Hueso | `erizo` | 40 | Pinchos (empieza con 2) | Rodar (A 7) → Erizarse (+2 Pinchos, B 6) → Lluvia de Púas (A 3×3) | `bonehog_sheet.png` |
| Sapo Glotón | `sapo` | 50 | Glotón | Lengüetazo (A 5, Débil 1) → Tragar (se traga 1 carta, B 6) → Panzazo (A 12) | `toad_sheet.png` |
| Cuervo Carroñero | `cuervo` | 32 | Carroña 2 | Picoteo (A 3×2) → Graznido (Vulnerable 2) → Picotazo al Ojo (A 9) | `crow_sheet.png` |
| Rata de Cripta | `rata` | 20 | Manada | Mordisco de Manada (A 2 + 1 por otra Rata viva, se recalcula tras cada carta) ×2 → Chillido (+1 Fuerza ella y sus aliados). Solo sale en grupo o invocada. | `rat_sheet.png` |
| Escarabajo Acorazado | `escarabajo` | 36 | Blindaje 12 | Enroscarse (B 8) → Cornada (A 8) → Embestida Acorazada (A 4 + la mitad de su escudo) | `beetle_sheet.png` |
| Cieno Mayor | `cieno` | 54 | División | Engullir (A 9) → Baba Pegajosa (A 5, Enredado 1) → Gelificarse (B 10). Con ≤ 50 %: Dividirse. | `ooze_sheet.png` |

### Esbirros

| Enemigo | Id | Vida | Patrón | Oro | Hoja |
|---|---|---|---|---|---|
| Larva | `larva` | 9 | Roer (A 3) ×2 → Endurecerse (+1 Fuerza, B 3) | No | `larva_sheet.png` |
| Cieno Menor | `cieno_menor` | mitad del Cieno (14 suelto) | Mordisco Viscoso (A 5) → Glóbulo (A 3, Frágil 1) | Sí | `ooze_small_sheet.png` |
| Víbora | `vibora` | 12 | Colmillo Venenoso (A 3, Veneno 2) → Acechar (+1 Fuerza, B 4) | No | `viper_sheet.png` |

### Élites

| Enemigo | Id | Vida | Identidad | Patrón | Fase (≤ 50 %, una vez) | Hoja |
|---|---|---|---|---|---|---|
| Rey Rata | `rey_rata` | 84 | Plaga | Llamar a la Plaga (invoca 2 Ratas de 16 PV, o 1 si solo cabe una; B 6) → Mordisco Real (A 10) → Festín de la Corte (cura 5 a todos, ratas +1 Fuerza) → Latigazo de Colas (A 4×3). Sin hueco: Arenga Real (corte +2 Fuerza, B 6). | Furia de la Plaga: +3 Fuerza y otra Rata | `ratking_sheet.png` |
| Coloso de la Forja | `coloso` | 96 | Golpe cargado | Pisada de Hierro (A 7, B 7) → Avivar la Caldera (Carga 16) → Martillo Pilón (A 24; si le quitas 16 de vida antes, queda Aturdido) | Sobrecalentar (+2 Fuerza, B 10); después Carga 20 y Martillo 28 | `colossus_sheet.png` |
| Duelista Espectral | `duelista` | 80 | Desvío 1, Réplica 6 | En Guardia (+1 Desvío, B 5) → Estocada (A 11) → Finta y Réplica (A 5×2, +1 Desvío) → Flecha (A 13) | Segundo Aliento: +2 Desvío y +2 Fuerza | `duelist_sheet.png` |

### Jefes

**El Gran Inquisidor** (`inquisidor`, 144 PV, `inquisitor_sheet.png`). Empieza con Vigilancia 3, así que castiga desde tu 4.ª carta de cada turno.

- Fase 1: Edicto (Débil 2, B 10) → Confiscación (te roba toda la Fuerza; si no tienes, gana 2; Frágil 2) → Veredicto (A 5×3) → Juicio (te arranca el 50 % del escudo, A 12) → Expiación (2 Heridas en tu pila de robo, B 8).
- Con ≤ 50 %, Auto de Fe: Vigilancia pasa a 2, +2 Fuerza, B 12, y reinicia el ciclo.
- Fase 2: Veredicto → Juicio (A 14) → Confiscación → Hoguera (A 18, 1 Herida en el descarte).
- Combos contra estrategias: Confiscación seguida de Veredicto suma tu Fuerza robada a los 3 golpes. Juicio castiga acumular escudo. Las Heridas atascan la mano. Vigilancia castiga vaciar la mano en un turno.

**La Sierpe Mudapiel** (`sierpe`, 132 PV, `serpent_sheet.png`).

- Fase 1: Llamar a la Nidada (invoca 1 Víbora, B 6; sin hueco Siseo: Víboras +2 Fuerza, B 6) → Colmillos (A 7, Veneno 3) → Constricción (A 10, Enredado 1) → Muda (2 de escudo por acumulación, máx. 30).
- Con ≤ 50 %, Piel Nueva: Muda sin escudo, se cura 15, +2 Fuerza, y reinicia el ciclo.
- Fase 2: Nidada Furiosa (2 Víboras) → Mordisco Letal (A 14, Veneno 4) → Muda → Coletazo (A 4×3).
- Combos contra estrategias: Muda anula el veneno y los elementos acumulados (la estrategia del Mago y la Pícara) y los convierte en escudo. Las Víboras no dan oro.

**Los Hermanos Lápida**: pareja de jefes enlazados (combate `lapida`, hojas `gravedigger_sheet.png` y `mourner_sheet.png`).

| | El Sepulturero (`sepulturero`, 84 PV) | La Plañidera (`planidera`, 64 PV) |
|---|---|---|
| Juntos (ciclo de 3, sincronizado) | Cavar Trinchera (B 8, a ella B 10) → Palazo (A 9) → Réquiem (A 4×2) | Lamento (Débil 1, Frágil 1, a él +1 Fuerza) → Alarido (A 3×2, 1 Herida) → Réquiem (A 4×2) |
| Si cae el otro | Cavar Tumba (Carga 16) → Exhumar: la desentierra con 32/64 PV, una vez por combate. Si le quitas 16 de vida mientras cava, queda Aturdido y no hay resurrección. Después, una vez, Duelo (+3 Fuerza, B 10). | Alma en Pena, al instante: gana la Fuerza de él + 3, se cura 20, B 10. |
| Solo | Palazo Brutal (A 14) → Losa (A 8, Frágil 2) | Grito de Ultratumba (A 7×2) → Maldición (Débil 2, Vulnerable 2) → Llanto Final (A 16) |

- Los dos Réquiem caen el mismo turno: un golpe doble anunciado cada 3 turnos.
- La resucitada vuelve a enlazarse con él, así que también absorbe su alma si después cae el Sepulturero.
- Matarlos el mismo turno evita todas las reacciones.

### Grupos nuevos (piso 1, vida ×0,8)

| Grupo | Clave | Miembros | Sinergia |
|---|---|---|---|
| La Plaga | `plaga` | 3 Ratas de Cripta | Cuantas más quedan vivas, más fuerte muerde cada una. |
| Festín de Carroña | `carrona` | Madre Larva + Cuervo | La Madre te entretiene con su camada; si ella cae, el Cuervo se ceba con su cuerpo. |
| Banquete del Pantano | `pantano` | Sapo + Babosa Ácida | La Babosa corroe tu escudo mientras el Sapo se traga tus cartas. |
| Bandada Hambrienta | `bandada` | Cuervo + Murciélago Vampiro | El Murciélago te desangra; si cae, el Cuervo se alimenta de él. |
| Fortín de Pinchos | `fortin` | Escarabajo + Erizo | El Escarabajo acumula escudo y el Erizo castiga cada carta que lo daña. |
| Cripta Infestada | `cripta` | Gusano de Tumba + 2 Ratas | El Gusano te envenena mientras las ratas muerden en manada. |

### Tiradas del piso 1 con `Expanded`

- **Combate:**
  - Con probabilidad 0,35 sale una pareja o grupo al azar: las 5 parejas originales del piso 1 y los 6 grupos nuevos.
  - Si no, un enemigo suelto entre los 11 normales originales y los 6 normales nuevos (todos salvo la Rata).
- **Élite:** uno de los 5 originales o de los 3 nuevos.
- **Jefe:** se elige por semilla entre Reina Micélida, La Tejedora, Caballero Hueco, El Gran Inquisidor, La Sierpe Mudapiel y Los Hermanos Lápida.

**Pruebas:**
- Combates: 1-11 normales originales, 12-17 parejas originales, 18-24 normales nuevos (incluida la Rata) y 25-30 grupos.
- Élites: 1-5 originales, 6-8 nuevos.
- Jefes: 1-3 originales, 4-6 nuevos.
- Los índices de la expansión (los de los pisos 1, 2 y 3, ver abajo) funcionan en los pisos 1, 2 y 3: crean ese contenido en el piso actual, para poder probarlo sin llegar a su piso. En los pisos 4+ se ignoran y la sala sale al azar.

## Piso 2: enemigos nuevos

Solo salen en el piso 2 (`MinFloor = MaxFloor = 2`). Las vidas de esta sección ya son las del piso 2 (base × 1,15);
los números de los patrones son los de base, como en el resto del documento (en el piso 2 se multiplican por 1,15).

### Mecánicas nuevas del piso 2

| Mecánica | Regla |
|---|---|
| **Amparo** | Cada golpe que recibe hace tantos puntos de daño menos como acumulaciones (antes del escudo). Se pierde al empezar el siguiente turno enemigo: protege durante tu turno. Castiga los golpes pequeños y muchos (dagas). |
| **Rito** | Al terminar cada una de sus acciones gana tanta Fuerza como Rito tenía al empezarla (no el turno en que lo consigue). |
| **Rencor** | Al morir te inflige tantos puntos como acumulaciones. Tu escudo lo absorbe; no es un ataque. Mátalo con escudo puesto o antes de que crezca. |
| **Cicatrización** | Al terminar su turno se cura tanto como acumulaciones, salvo si tiene Fuego o Veneno en ese momento. |
| **Cabezas** | Mordisco Múltiple golpea una vez por cabeza. Un solo golpe que le quite 20 o más de vida le corta una (mínimo 2, máximo 8). |
| **Reflejo** | Cada golpe que le quite 15 o más de vida gasta 1 acumulación y te devuelve la mitad de esa vida al terminar tu carta (tu escudo lo absorbe). Fuera de tus cartas no refleja. |
| **Ansia de Sangre** | La primera vez de cada turno que pierdes vida (por cualquier causa) gana tantas acumulaciones de Fuerza. |
| **Sangrado** (perjuicio del héroe) | Pierdes 1 de vida por cada carta que juegues, sin que el escudo lo pare. Baja 1 al final de tu turno. |
| **Necrosis** (perjuicio del héroe) | Tus curaciones no hacen nada (tampoco el exceso convertido en escudo). Baja 1 al final de tu turno. |
| **Ilusiones atadas** | Las Ilusiones se desvanecen en cuanto muere la Ilusionista que las invocó. |
| **Ofrenda Oscura** | Mata a su esbirro con más Fuerza, se queda esa Fuerza más 2 y se cura. Si matas a sus esbirros antes, la intención pasa al instante a Castigo Abisal. |
| **Espejo Maldito** | Te pasa su Veneno, Débil y Vulnerable (las reliquias que bloquean perjuicios siguen funcionando) y pierde su Marcado. |
| **Cosecha de Almas** | Su daño sube 3 por cada esqueleto suyo destruido en el combate; se recalcula tras cada carta. |
| **Robo de vida** | Se cura tanta vida como te quita con ese ataque. |

Sangrado y Necrosis son términos del glosario: se resaltan en las intenciones y explican su regla. El resto de estados nuevos
se explican en los paneles de estado del enemigo, como los del piso 1.

### Normales

| Enemigo | Id | Vida (piso 2) | Identidad | Patrón (en bucle) | Hoja |
|---|---|---|---|---|---|
| Centinela Rúnico | `centinela` | 64 | Amparo | Mazazo Rúnico (A 10) → Runa de Amparo (Amparo 4 a él y a sus aliados, B 6) → Descarga Rúnica (A 5×2) | `sentinel_sheet.png` |
| Fanático del Abismo | `fanatico` | 57 | Rito | Rito Oscuro (Rito 2, B 6; solo el primer turno) → Puñal Ritual (A 7) → Tajo de Sangre (A 3×2) → Puñal Ritual… | `zealot_sheet.png` |
| Sombra Rencorosa | `sombra` | 43 | Rencor (empieza con 6) | Zarpa Umbría (A 7, Débil 1) → Rencor Creciente (+4 Rencor, B 8) → Desgarro (A 4×2) | `shade_sheet.png` |
| Sanguijuela Gigante | `sanguijuela` | 50 | Sangrado | Ventosa (A 6, Sangrado 2) → Succión (A 7, roba vida) → Coágulo (B 10) | `leech_sheet.png` |
| Trol de Cloaca | `trol` | 64 | Cicatrización (empieza con 3) | Garrotazo (A 11) → Lamer Heridas (+1 Cicatrización, B 8) → Zarpazo Doble (A 5×2) | `troll_sheet.png` |
| Ilusionista | `ilusionista` | 46 | Espejismo | Desdoblarse (invoca 1 Ilusión, B 5) → Rayo Prismático (A 10) → Destello Cegador (A 4, Débil 1). Sin hueco o sin presupuesto (4 por combate): Brillo Hipnótico (Débil 1, Vulnerable 1, B 5). | `illusionist_sheet.png` |

### Esbirros

| Enemigo | Id | Vida (piso 2) | Patrón | Oro | Hoja |
|---|---|---|---|---|---|
| Ilusión | `ilusion` | 11 | Golpe Ilusorio (A 5) → Parpadeo (+1 Desvío, sin Réplica). Se desvanece si muere la Ilusionista. | No | `illusion_sheet.png` |
| Esqueleto Alzado | `esqueleto` | 18 | Tajo Oxidado (A 6) → Golpe de Escudo (A 3, B 5) | No | `skeleton_sheet.png` |
| Esquirla de Espejo | `esquirla` | 13 | Rencor 4. Destello Cortante (A 5) → Refractar (+1 Fuerza, B 3) | No | `shard_sheet.png` |

### Élites

| Enemigo | Id | Vida (piso 2) | Identidad | Patrón | Fase (≤ 50 %, una vez) | Hoja |
|---|---|---|---|---|---|---|
| Paladín Caído | `paladin` | 101 | Juramento | Escudo de Fe (Amparo 5, B 10) → Martillo del Juicio (A 14) → Condena (A 6×2, Vulnerable 1) | Juramento Roto: Rito 2 y +2 Fuerza; después Furia Impía (A 5×3) → Martillo del Juicio (A 16) → Grito Profano (Débil 2, B 8) | `paladin_sheet.png` |
| Condesa Carmesí | `condesa` | 96 | Vampiresa (Ansia de Sangre 1) | Beso Carmesí (A 8, roba vida) → Corte Sangrante (A 4×2, Sangrado 2) → Velo de Niebla (B 12, Débil 1) → Dentellada Carmesí (A 12) | Hambre Eterna: se cura 15, +1 Ansia de Sangre, llama a un Murciélago Vampiro (60 % de vida, no da oro); después Beso (A 9) → Corte (A 5×2) → Llamar a la Colonia (otro murciélago, B 8; sin hueco, Velo de Niebla) → Dentellada (A 14) | `countess_sheet.png` |
| Sumo Sacerdote del Abismo | `sacerdote` | 94 | Ofrenda | Llamada al Abismo (invoca un Fanático del Abismo con la mitad de vida, B 6; sin hueco Bendición Abisal: aliados +1 Rito, B 6) → Daga Ceremonial (A 10) → Ofrenda Oscura (sacrifica a su Fanático más fuerte: su Fuerza + 2, se cura 12; sin víctima Castigo Abisal, A 13) → Llamas del Abismo (A 5×2, Débil 1). Como mucho 3 fanáticos por combate. | Ascensión: Rito 2, B 10, y reinicia el ciclo | `highpriest_sheet.png` |

### Jefes (sustituyen al Señor de la Cripta en el piso 2)

**El Nigromante** (`nigromante`, 151 PV, `necromancer_sheet.png`). Invoca Esqueletos Alzados (como mucho 6 por combate).

- Fase 1: Alzar Huesos (2 Esqueletos, o 1 si solo cabe uno, B 8; sin hueco Orden de Hueso: aliados +2 Fuerza, B 8) → Toque Necrótico (A 9, Necrosis 2) → Cosecha de Almas (A 6 + 3 por esqueleto destruido) → Muro de Huesos (B 12, esqueletos B 6).
- Con ≤ 50 %, Danza Macabra: alza hasta 2 Esqueletos, +2 Fuerza, B 12, y reinicia.
- Fase 2: Cosecha de Almas (A 8 + 3 por esqueleto) → Toque Necrótico (A 11, Necrosis 3) → Alzar Huesos.
- Combos contra estrategias: Necrosis anula la curación (la Guerrera y las reliquias de curación); matar esqueletos alimenta la Cosecha, dejarlos vivos te desangra.

**La Hidra de la Cisterna** (`hidra`, 172 PV, `hydra_sheet.png`). Empieza con 3 Cabezas y Cicatrización 5.

- Ciclo: Mordisco Múltiple (A 4 por cabeza; se recalcula al instante si le cortas una) → Aliento Ácido (A 8, Frágil 2) → Sumergirse (B 14).
- A 2/3 de vida, Cabezas Nuevas: +2 Cabezas y +2 Cicatrización. A 1/3, Furia de la Hidra: +2 Cabezas, +2 Fuerza, B 12. Ninguna de las dos avanza su ciclo.
- Combos contra estrategias: la Cicatrización castiga los mazos lentos y el Fuego o el Veneno la detienen (Mago y Pícara); los golpes de 20 o más le cortan cabezas (Guerrera); el Débil reduce cada mordisco.

**La Reina de los Espejos** (`reina_espejos`, 149 PV, `mirrorqueen_sheet.png`). Empieza con Reflejo 2. Invoca Esquirlas de Espejo (como mucho 6).

- Fase 1: Fragmentar (2 Esquirlas, B 6; sin hueco Brillo Prismático: aliados +2 Fuerza, B 6) → Corte Prismático (A 6×2) → Espejo Maldito (te pasa su Veneno, Débil y Vulnerable, +1 Reflejo, B 10) → Rayo Especular (A 15).
- Con ≤ 50 %, Mil Reflejos: +2 Reflejo, +2 Fuerza y una Esquirla, y reinicia.
- Fase 2: Corte Prismático (A 7×2) → Fragmentar → Rayo Especular (A 18) → Espejo Maldito.
- Combos contra estrategias: Reflejo castiga los golpes enormes (gasta sus cargas con golpes de 15 justos antes de descargar los grandes); Espejo Maldito castiga acumular Veneno y perjuicios; las Esquirlas te cortan al romperse (Rencor), así que el daño en área sin escudo sale caro.

### Grupos nuevos (piso 2, vida ×0,8)

| Grupo | Clave | Miembros | Sinergia |
|---|---|---|---|
| Cónclave Abisal | `conclave` | Fanático + Centinela | El Centinela ampara al Fanático mientras su Rito lo hace más fuerte cada turno. |
| Festín de Sangre | `sangre` | Sanguijuela + Murciélago Vampiro | La Sanguijuela te hace sangrar con cada carta y el Murciélago se cura con tu sangre. |
| Teatro de Sombras | `teatro` | Ilusionista + Sombra | Las Ilusiones te distraen mientras la Sombra acumula Rencor. |
| Cloaca Infecta | `cloaca` | Trol + Babosa Ácida | La Babosa corroe tu escudo y el Trol cicatriza sus heridas: quémalo o envenénalo. |
| Coro de Lamentos | `coro` | 2 Sombras | Si las matas a la vez con poco escudo, su Rencor te hará pedazos. |
| Forja Rúnica | `forja` | Centinela + Cráneo Ígneo | El Centinela ampara al Cráneo Ígneo, que no para de atacar. |

### Tiradas del piso 2 con `Expanded`

- **Combate:**
  - Con probabilidad 0,5 (la del original en el piso 2) sale una pareja o grupo: las 6 parejas originales (incluida la Trampa del Tesoro) y los 6 grupos nuevos.
  - Si no, un enemigo suelto entre los 11 normales originales y los 6 normales nuevos del piso 2.
- **Élite:** uno de los 5 originales o de los 3 nuevos del piso 2.
- **Jefe:** uno de los 3 jefes nuevos del piso 2, por semilla (`PyRandom(semilla · primo ^ 0xB0552)`, independiente del jefe del piso 1).

**Pruebas:**
- Combates: 31-36 normales del piso 2 (Centinela, Fanático, Sombra, Sanguijuela, Trol, Ilusionista) y 37-42 grupos del piso 2. Las etiquetas acaban en «(piso 2)».
- Élites: 9-11 (Paladín, Condesa, Sumo Sacerdote).
- Jefes: 7-9 (El Nigromante, La Hidra de la Cisterna, La Reina de los Espejos). La fila se llama «Jefe forzado (pisos 1-3)».

### Balance

Primera pasada medida con simulaciones (`Tools/DomainSmoke` y un arnés que juega la primera carta jugable de los mazos
iniciales, 30-60 semillas × 3 héroes). Con daño extra +4 por carta, los jefes del piso 2 se ganan el 54-67 % de las veces,
frente al 41-82 % de los jefes del piso 1 llevados al piso 2. Los élites nuevos quedan en el rango de los originales; la
Condesa es la más dura para la Guerrera, porque su Ansia de Sangre se alimenta de la vida que ella misma se quita.
La Reina de los Espejos es el jefe que más castiga los mazos de golpes enormes, por diseño. Todos los números están en
`EnemyCatalog.Floor2.cs` y `EnemyPatterns.Floor2.cs`.

## Piso 3: enemigos nuevos

Una ciudadela helada: hielo, acero escarchado, magia del vacío y relojería. Solo salen en el piso 3 (`MinFloor = MaxFloor = 3`).
Las vidas de esta sección ya son las del piso 3 (base × 1,3); los números de los patrones son los de base, como en el resto
del documento (en el piso 3 se multiplican por 1,3).

### Mecánicas nuevas del piso 3

| Mecánica | Regla |
|---|---|
| **Pendón** | Mientras vive, cada golpe de sus aliados hace tantos puntos de daño más como acumulaciones. Los aliados lo muestran como **Inspiración** (suma del Pendón de los demás enemigos vivos). Se recalcula al instante: cuando cae el portador, cuando llega un aliado nuevo y antes de cada acción enemiga. Su propio Pendón no le afecta a él. |
| **Cólera** | Cada golpe que le quita vida le da tanta Fuerza como acumulaciones. Pierde esa Fuerza al terminar su siguiente acción. Los golpes que solo quitan escudo, el Veneno y el Fuego no la activan. Castiga los muchos golpes pequeños. |
| **Perforante** | Los golpes de ese ataque ignoran tu escudo (y no lo gastan). Señuelos y reliquias que cambian el daño recibido siguen funcionando. |
| **Inmortal** | El Rey Liche no baja de 1 de vida mientras viva una Filacteria. Si la Filacteria cae durante una carta, deja de ser Inmortal al terminar esa carta. |
| **Cuerda** | Campanada golpea una vez por acumulación y después la deja en 1. Cada Autómata montado por El Relojero le da 1 al terminar su acción. |
| **Rebobinar** | Recupera toda la vida que perdió desde que empezó tu turno (cartas, veneno, reacciones). Ese turno conviene defenderse en lugar de atacar. |
| **Agotamiento** (perjuicio del héroe) | Empiezas cada turno con 1 de maná menos. Baja 1 al final de tu turno. |
| **Congelación** (perjuicio del héroe) | Al terminar tu turno pierdes 1 de vida por cada carta que te quede en la mano (también Heridas y Moho), sin que el escudo lo pare. Baja 1 al final de tu turno. |
| **Tríos** | Como en el original, en el piso 3 una de cada cuatro peleas de pareja o grupo trae un tercer enemigo (vida ×0,8): uno de los 11 normales originales o de los 6 normales del piso 3 que no esté ya en el grupo. |

Agotamiento y Congelación son términos del glosario: se resaltan en las intenciones y explican su regla. El resto se
explica en los paneles de estado del enemigo.

### Normales

| Enemigo | Id | Vida (piso 3) | Identidad | Patrón (en bucle) | Hoja |
|---|---|---|---|---|---|
| Abanderado Helado | `abanderado` | 59 | Pendón 2 | Asta Helada (A 8) → Alzar el Pendón (+1 Pendón, B 8) → Carga del Pendón (A 5×2). Solo sale acompañado (grupos y tríos). | `bannerman_sheet.png` |
| Bárbaro de la Ventisca | `barbaro` | 67 | Cólera 1 | Hachazo (A 10) → Grito de Guerra (+1 Cólera, B 6) → Torbellino (A 4×3) | `barbarian_sheet.png` |
| Arcanista del Vacío | `arcanista` | 52 | Agotamiento | Drenar Maná (A 6, Agotamiento 1) → Orbe del Vacío (A 11) → Barrera Arcana (B 12) | `voidmage_sheet.png` |
| Cazador Glacial | `cazador` | 57 | Perforante | Jabalina de Hielo (A 8, Perforante) → Acechar en la Nieve (+1 Fuerza, B 6) → Ráfaga de Virotes (A 3×3) | `frosthunter_sheet.png` |
| Ánima Helada | `anima` | 46 | Congelación | Toque Gélido (A 6, Congelación 2) → Velo de Escarcha (B 10, Débil 1) → Carámbanos (A 4×2) | `frostwisp_sheet.png` |
| Ensamblador | `ensamblador` | 54 | Taller | Ensamblar (monta 1 Autómata, B 6; sin hueco o sin presupuesto, 3 por combate: Ajustar Tuercas, aliados +2 Fuerza, B 6) → Llave Inglesa (A 9) → Chispazo (A 5, Frágil 1) | `tinkerer_sheet.png` |

### Esbirros

| Enemigo | Id | Vida (piso 3) | Patrón | Oro | Hoja |
|---|---|---|---|---|---|
| Autómata de Cuerda | `automata` | 18 | Perno (A 3, Perforante) → Dar Cuerda (+1 Fuerza, B 3). Si lo montó El Relojero, le da 1 de Cuerda cada vez que actúa. | No | `automaton_sheet.png` |
| Escudero | `escudero` | 23 | Escudo en Alto (B 5, aliados B 4) → Lanzada (A 5) | No | `squire_sheet.png` |
| Filacteria | `filacteria` | 52 | Pulso Helado (B 6, aliados B 10) → Latido Vital (cura 6 a todos). Mientras viva, El Rey Liche es Inmortal. | Sí la del combate; no la que se forja en la fase | `phylactery_sheet.png` |

### Élites

| Enemigo | Id | Vida (piso 3) | Identidad | Patrón | Fase (≤ 50 %, una vez) | Hoja |
|---|---|---|---|---|---|---|
| Comandante de la Ciudadela | `comandante` | 111 | Mando (Pendón 1) | Llamar a la Guardia (1 Escudero, B 6; sin hueco Formación Cerrada: B 10, aliados B 8) → Estocada de Mando (A 12) → Toque de Corneta (+1 Pendón, B 8) → Lanza Perforante (A 10, Perforante). Como mucho 3 escuderos. | Última Carga: +2 Fuerza, +1 Pendón y otro Escudero; después Estocada A 14 y Lanza A 12 | `commander_sheet.png` |
| Wendigo | `wendigo` | 119 | Hambre (Cólera 1) | Zarpazo Doble (A 5×2) → Aullido Gélido (Congelación 2, Débil 1, B 8) → Devorar (A 11, roba vida) | Hambre Insaciable: se cura 15, +1 Cólera y +2 Fuerza; después Zarpazo A 6×2 y Devorar A 13 | `wendigo_sheet.png` |
| Cronomante | `cronomante` | 114 | Tiempo | Ralentizar (A 8, Agotamiento 1) → Paradoja (A 5×3) → Rebobinar (recupera la vida que le quites ese turno, B 8) → Golpe Anticipado (A 15) | Bucle Temporal: rebobina y +2 Fuerza; después Paradoja A 6×3 y Golpe A 17 | `chronomancer_sheet.png` |

### Jefes (sustituyen al Señor de la Cripta en el piso 3)

**El Rey Liche** (`liche`, 169 PV, `lichking_sheet.png`). El combate empieza con su Filacteria (52 PV, `phylactery_sheet.png`).

- Inmortal: mientras viva la Filacteria no baja de 1 de vida. La Filacteria le da escudo y los cura a los dos.
- Fase 1: Ventisca (A 4×3, Congelación 1) → Lanza Gélida (A 11, Perforante) → Mortaja de Hielo (B 14, Filacteria B 8) → Toque del Vacío (A 8, Agotamiento 1).
- Con ≤ 50 %: si la Filacteria ya cayó, Ritual de la Filacteria (forja otra, una vez, B 12); si no, Furia del Liche (+3 Fuerza, B 12). Reinicia.
- Fase 2: Lanza Gélida (A 13) → Ventisca (A 5×3, Congelación 2) → Toque del Vacío (A 10, Agotamiento 1).
- Combos contra estrategias: Perforante atraviesa el escudo de la Guerrera; Agotamiento te deja cartas sin jugar y Congelación te las cobra. Hay que romper la Filacteria antes de poder rematarlo.

**El Relojero** (`relojero`, 182 PV, `clockmaker_sheet.png`). Empieza con Cuerda 2. Monta Autómatas de Cuerda (como mucho 4).

- Fase 1: Montar Autómatas (2, o 1 si solo cabe uno, B 8; sin hueco Engrasar Engranajes: aliados +2 Fuerza, B 8) → Manecillas (A 6×2) → Detener el Tiempo (Agotamiento 1, B 12) → Campanada (A 4 por cada Cuerda; después le queda 1).
- Con ≤ 50 %, Hora Cero: +2 Fuerza, +2 Cuerda y hasta dos Autómatas. Reinicia.
- Fase 2: el mismo ciclo con Manecillas A 7×2 y Agotamiento 2.
- Combos contra estrategias: cada Autómata vivo le da cuerda cada turno; con dos autómatas vivos, la primera Campanada golpea 6 veces. Sus pernos son Perforantes. Romperlos pronto es la clave.

**La Dragona de Escarcha** (`dragona`, 192 PV, `frostdragon_sheet.png`). Empieza con Blindaje 12.

- Fase 1: Aliento Gélido (A 4×3, Congelación 2) → Garra Perforante (A 11, Perforante) → Plegar las Alas (B 18) → Coletazo Glaciar (A 17).
- Con ≤ 50 %, Furia Glacial: pierde el Blindaje y gana Cólera 2 y +2 Fuerza. Reinicia.
- Fase 2: Aliento Gélido (A 5×3, Congelación 2) → Garra Perforante (A 13) → Coletazo Glaciar (A 19).
- Combos contra estrategias: el Blindaje castiga los mazos lentos y Perforante el escudo; en la fase 2, la Cólera castiga los muchos golpes pequeños (Pícara).

### Grupos nuevos (piso 3, vida ×0,8)

| Grupo | Clave | Miembros | Sinergia |
|---|---|---|---|
| Guardia de la Ciudadela | `guardia` | Abanderado + Cazador | El Pendón se suma a las jabalinas del Cazador, que atraviesan tu escudo. |
| Horda de la Ventisca | `horda` | Abanderado + Bárbaro | El Abanderado inspira al Bárbaro, y cada golpe pequeño que le das al Bárbaro lo enfurece más. |
| Taller del Vacío | `taller` | Ensamblador + Gólem de Musgo | El Gólem cubre al Ensamblador mientras sus Autómatas te disparan. |
| Tormenta Blanca | `tormenta` | Ánima + Arcanista | Con menos maná te quedan más cartas en la mano, y la Congelación te las cobra. |
| Patrulla Helada | `patrulla` | Cazador + Ojo Vigilante | El Ojo te deja Vulnerable y la jabalina del Cazador atraviesa tu escudo. |
| Vanguardia Infernal | `vanguardia` | Abanderado + Diablillo | El Diablillo no para de atacar y cada golpe lleva el Pendón. |

Todos los grupos son parejas, así que la tirada de trío siempre tiene sitio para el tercero.

### Tiradas del piso 3 con `Expanded`

- **Combate:**
  - Con probabilidad 0,6 (la del original en el piso 3) sale una pareja o grupo: las 6 parejas originales y los 6 grupos nuevos.
    Después, con probabilidad 0,25 (la tirada de trío del original), se suma un tercer enemigo de los 11 normales originales y
    los 6 normales del piso 3, sin repetir un miembro.
  - Si no, un enemigo suelto entre los 11 normales originales y los 5 normales del piso 3 que salen solos (todos salvo el Abanderado).
- **Élite:** uno de los 5 originales o de los 3 nuevos del piso 3.
- **Jefe:** uno de los 3 jefes nuevos del piso 3, por semilla (`PyRandom(semilla · primo ^ 0xB0553)`, independiente de los otros pisos).

**Pruebas:**
- Combates: 43-48 normales del piso 3 (Abanderado, Bárbaro, Arcanista, Cazador, Ánima, Ensamblador) y 49-54 grupos del piso 3. Las etiquetas acaban en «(piso 3)».
- Élites: 12-14 (Comandante, Wendigo, Cronomante).
- Jefes: 10-12 (El Rey Liche, El Relojero, La Dragona de Escarcha).

### Balance

Medido con el mismo arnés que el piso 2 (primera carta jugable de los mazos iniciales, 30-40 semillas × 3 héroes).
Con daño extra +4 por carta, los jefes del piso 3 se ganan el 27-35 % de las veces; los jefes de los pisos 1 y 2 llevados al
piso 3, entre el 12 y el 100 %. Con +8 (un mazo algo mejor), 46-58 % frente a 42-100 %. Son algo más duros que la media, como
corresponde al jefe del piso. Los élites nuevos quedan en el rango de los del piso 2. Los grupos nuevos se ganan el 93 % de
las veces, frente al 95 % de las parejas originales; con trío, el 63 % frente al 58 %. Todos los números están en
`EnemyCatalog.Floor3.cs` y `EnemyPatterns.Floor3.cs`.

## Enemigos originales

| Enemigo | Id | Nivel | Vida | Identidad | Patrón | Hoja |
|---|---|---|---|---|---|---|
| Espectro | `espectro` | Normal | 42 | Lamento | Zarpazo Espectral (A 11) ×2 → Lamento (Débil 2, Frágil 1) | `wraith_sheet.png` |
| Babosa Ácida | `babosa` | Normal | 46 | Corrosión | Escupitajo Ácido (A 5, Frágil 2) → Golpe Viscoso (A 10) → Corroer (Vulnerable 2, B 5) | `slime_sheet.png` |
| Gusano de Tumba | `gusano` | Normal | 52 | Veneno | Mordida Pútrida (A 6, Veneno 3) → Enterrarse (B 12) → Bilis | `worm_sheet.png` |
| Ojo Vigilante | `ojo` | Normal | 34 | Debuffs | Mirada Fija → Rayo Ocular (A 8) → Pavor | `eye_sheet.png` |
| Cráneo Ígneo | `craneo` | Normal | 38 | Agresivo | Llamarada (A 7) ×2 → Avivar (+Fuerza) | `skull_sheet.png` |
| Murciélago Vampiro | `murcielago` | Normal | 30 | Robo de vida | Mordisco Vampírico (A 7, roba vida) → Picado (A 3×3) → Chillido | `bat_sheet.png` |
| Seta Explosiva | `seta` | Normal | 28 | Cuenta atrás (Mecha 3) | Nube de Esporas → Hincharse (B 8) → ¡Explosión! (A 24, muere) | `bomb_sheet.png` |
| Gólem de Musgo | `golem` | Normal | 72 | Muro | Muralla (B 10, escudo al aliado) → Puñetazo de Roca (A 13) → Endurecer (B 12) | `golem_sheet.png` |
| Acólito de Ceniza | `acolito` | Normal | 40 | Apoyo | Bendición de Ceniza (Fuerza y cura a aliados) → Incensario (A 6) → Penitencia → Incensario | `acolyte_sheet.png` |
| Diablillo | `diablillo` | Normal | 32 | Muy agresivo | Zarpazos (A 3×3) → Bola de Fuego (A 11, Vulnerable) → Zarpazos → Burla (+Fuerza) | `imp_sheet.png` |
| Mímico | `mimico` | Normal | 60 | Emboscada | Fingir (B 15) → Dentellada (A 18) → Lengüetazo (A 4×3) → Dentellada (A 12) | `mimic_sheet.png` |
| El Verdugo | `verdugo` | Élite | 92 | Ejecución | Sentencia (B 8, Vulnerable) → Hachazo (A 14) → Afilar (B 10) → Decapitar (A 21) | `executioner_sheet.png` |
| Bruja del Pantano | `bruja` | Élite | 80 | Maleficios | Maleficio → Rayo de Ciénaga (A 9) → Caldero Burbujeante (B 10, Moho) → Muñeco Vudú (A 4×3) | `hag_sheet.png` |
| Gárgola | `gargola` | Élite | 84 | Piel de piedra | Petrificar (B 18) → Picado (A 5×3) → Zarpazo de Piedra (A 11) | `gargoyle_sheet.png` |
| Minotauro | `minotauro` | Élite | 100 | Embestida | Escarbar (B 12) → Embestida (A 15, crece cada vez) → Pisotón (A 9) | `minotaur_sheet.png` |
| Escorpión Rey | `escorpion` | Élite | 88 | Veneno letal | Aguijonazo (A 7) → Pinzas (A 5×2) → Caparazón (B 14) → Toxina | `scorpion_sheet.png` |
| Reina Micélida | `micelida` | Jefe | 138 | Esporas y Moho | Lluvia de Esporas (A 6) → Raíces Estranguladoras (A 10) → Brote de Moho (B 12) | `mycelid_sheet.png` |
| La Tejedora | `tejedora` | Jefe | 126 | Debuffs y Banquete | Hilos Pegajosos → Colmillo (A 9) → Capullo de Seda (B 10) → Banquete (A 5×5) | `weaver_sheet.png` |
| Caballero Hueco | `caballero` | Jefe | 150 | Espadas flotantes (Espadas 2) | Danza de Espadas (A 4×2) → Llamar al Acero (B 8) → Estocada Doble (A 8×2) → … → Tajo del Verdugo (A 17) | `knight_sheet.png` |

Reglas originales:
- Los élites y jefes se enfurecen al 50 % de vida (Furia).
- Si cae un miembro de una pareja original, el otro busca Venganza.
- En los pisos 4+ (y en los pisos 2 y 3 de las partidas `Original`) el jefe es el Señor de la Cripta, que no tiene hoja propia.
- Las secuencias exactas (1368 intenciones) las comprueba `EnemyParityTests` contra el motor Python.

Parejas originales:
- Piso 1: El Culto del Fuego (Acólito + Cráneo), Muro Corrosivo (Gólem + Babosa), Vigía y Cazador (Ojo + Murciélago), Cementerio Podrido (Gusano + Seta) y Procesión de Ceniza (Espectro + Acólito).
- Pisos superiores: Trampa del Tesoro (Mímico + Diablillo).
- Desde el piso 3 hay tríos: una de cada cuatro parejas trae un tercer enemigo suelto.

## Sprites

- **Estilo:** los sprites nuevos (y los que el usuario pida rehacer) siguen `docs/art-style.md`, el estilo aprobado el 27-10-2026. Los generadores de esta sección quedan para regenerar las hojas actuales tal cual.
- Las 17 hojas nuevas del piso 1 las genera `Tools/GenerateEnemyArt.py`, las 15 del piso 2 `Tools/GenerateEnemyArtFloor2.py`
  y las 15 del piso 3 `Tools/GenerateEnemyArtFloor3.py` (Python 3.10+ y Pillow 11+, siempre con el mismo resultado). Los de
  los pisos 2 y 3 importan las primitivas, la validación y el formato del del piso 1 sin cambiarlo.
- Usan el mismo formato JSON que las 19 originales.
- El generador valida cada hoja: claves y filas, alfa del idle, destello de `hurt` 72/53/23 %, contorno oscuro, y que toda acción salvo `death` y las terminales acabe en el frame 0 del idle.

```powershell
python Tools/GenerateEnemyArt.py                        # regenera las 17 hojas
python Tools/GenerateEnemyArt.py --check                # compara byte a byte con las del repositorio
python Tools/GenerateEnemyArt.py --preview TestResults/enemy-art   # hojas de contacto ×2
python Tools/GenerateEnemyArt.py --styles               # puntos para EnemyAnimator.Styles
python Tools/GenerateEnemyArtFloor2.py                  # regenera las 15 hojas del piso 2 (mismas opciones)
python Tools/GenerateEnemyArtFloor2.py --check
python Tools/GenerateEnemyArtFloor3.py                  # regenera las 15 hojas del piso 3 (mismas opciones)
python Tools/GenerateEnemyArtFloor3.py --check
```

Nunca toca las hojas originales ni las de héroes.

### Enemigos redibujados con el método HTML (`docs/art-style.md` §3.1)

| Enemigo | Página | Hoja | Desde |
|---|---|---|---|
| Espectro | `Tools/VfxLab/enemies/wraith.html` | `wraith_sheet.png` + `wraith_halo.png`, celda 128×96 | 12-11-2026 |

Cada página es a la vez el visor (ábrela en el navegador) y la fuente de la hoja. Los brazos, mangas y dedos se escriben
fotograma a fotograma (regla del usuario del 12-11-2026). Animaciones: reposo 24, ataque 10, daño 7, habilidad 12 y
muerte 17, a 12 fps, con eventos de golpe y halo.

```powershell
python Tools/VfxLab/export_enemy_art.py                  # todas las páginas de Tools/VfxLab/enemies (necesita Node)
python Tools/VfxLab/export_enemy_art.py --enemy wraith   # una
python Tools/VfxLab/export_enemy_art.py --check          # las hojas del repositorio coinciden con las páginas
python Tools/VfxLab/export_enemy_art.py --styles         # puntos de partículas para EnemyAnimator.Styles
```
