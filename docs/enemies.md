# Bestiario

Todos los enemigos del juego: los 19 originales del juego Python y los 17 nuevos del piso 1.
Abreviaturas de los patrones: **A x** = ataque de x, **A x×n** = n golpes de x, **B x** = gana x de escudo.
Los números son los del piso 1. En pisos superiores, vida y valores se multiplican por `1 + 0,15 × (piso − 1)`.
Los estados no escalan.

## Contenido activo según la partida

| Partida | Contenido de enemigos |
|---|---|
| Partida del menú (`EnemyRoster.Expanded`) | Piso 1: originales y nuevos. Pisos 2+: solo los originales, igual que antes. |
| Pruebas de paridad, `new RunState(héroe, semilla)` (`EnemyRoster.Original`) | Solo los originales, tirada idéntica al motor Python. |

Con `Expanded` solo cambian las tiradas de enemigos del piso 1. Usan el mismo número de llamadas al generador aleatorio,
así que mapas, recompensas, tiendas, sobres y gachapón no cambian.

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
- Los índices nuevos solo tienen efecto en el piso 1. En otros pisos se ignoran y la sala sale al azar.

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
- En los pisos 2+ el jefe es el Señor de la Cripta, que no tiene hoja propia.
- Las secuencias exactas (1368 intenciones) las comprueba `EnemyParityTests` contra el motor Python.

Parejas originales:
- Piso 1: El Culto del Fuego (Acólito + Cráneo), Muro Corrosivo (Gólem + Babosa), Vigía y Cazador (Ojo + Murciélago), Cementerio Podrido (Gusano + Seta) y Procesión de Ceniza (Espectro + Acólito).
- Pisos superiores: Trampa del Tesoro (Mímico + Diablillo).
- Desde el piso 3 hay tríos.

## Sprites

- Las 17 hojas nuevas las genera `Tools/GenerateEnemyArt.py` (Python 3.11 + Pillow 11), siempre con el mismo resultado.
- Usan el mismo formato JSON que las 19 originales.
- El generador valida cada hoja: claves y filas, alfa del idle, destello de `hurt` 72/53/23 %, contorno oscuro, y que toda acción salvo `death` y las terminales acabe en el frame 0 del idle.

```powershell
python Tools/GenerateEnemyArt.py                        # regenera las 17 hojas
python Tools/GenerateEnemyArt.py --check                # compara byte a byte con las del repositorio
python Tools/GenerateEnemyArt.py --preview TestResults/enemy-art   # hojas de contacto ×2
python Tools/GenerateEnemyArt.py --styles               # puntos para EnemyAnimator.Styles
```

Nunca toca las hojas originales ni las de héroes.
