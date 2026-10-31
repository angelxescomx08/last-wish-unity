# Eventos de Last Wish — catálogo y seguimiento

150 eventos implementados en Unity. **Arte y diseño visual: pendientes en los 150**; no se generan imágenes ni efectos. El catálogo incluye 16 historias enlazadas y 12 encuentros de varias etapas.

La fuente editable es `docs/events-catalog.json`. Ejecuta `python Tools/GenerateEvents.py` para regenerar el catálogo C# y esta lista; `--check` detecta desajustes. Python es solo una herramienta de edición: el juego ejecuta C# y no lee este JSON.

## Reglas de aparición y resolución

- Pisos 1, 2 y 3 tienen sucesos propios; también hay contenido exclusivo para 4–6, 7–9 y 10+, además de eventos generales y algunas historias que llegan hasta el piso 9.
- Cada sala selecciona una rareza entre las disponibles con pesos 50/28/14/6/2 (Común/Poco común/Raro/Épico/Legendario), y luego un evento de esa rareza. Son pesos de categoría, no porcentajes por evento. La suerte no altera la aparición de sucesos.
- Cada evento aparece una vez por partida. Si se agota el conjunto de eventos independientes del piso, se reutiliza Monedas entre las grietas para que siempre exista un encuentro.
- Las 16 historias enlazadas se desbloquean solo al aceptar la opción indicada. El próximo capítulo tiene prioridad en la siguiente sala de evento de un piso permitido; espera si aún estás demasiado arriba. Puede aparecer en el mismo piso si cumple su mínimo. Si rechazas continuar, no hay más capítulos. En los cuatro relatos con bifurcación, la elección central abre un solo final; el otro no puede aparecer.
- No hay combate obligatorio ni cambios del mapa. Los eventos de varias etapas permiten entrar, decidir y comprar dentro de la misma sala. Salir siempre está disponible; una entrada ya pagada no se devuelve.
- Los sacrificios de vida y vida máxima se validan antes de cobrar; siempre conservas al menos 1. La pérdida de vida máxima es permanente durante la partida y limita tu vida actual. El daño de un fallo tampoco mata.
- Las cartas que se mejoran son la primera sin mejorar; las que se eliminan son la primera del mazo. Las opciones lo indican. No se elimina la última carta. Las cartas recibidas respetan la clase del héroe y el sistema de recompensas existente.
- Las reliquias indicadas son las existentes, sin imágenes nuevas. No se permite comprar una que ya posees. Los precios de eventos son fijos: no usan descuentos, vales ni multiplicador de oro de Pruebas. Las ganancias sí pasan por el mecanismo normal de intereses.
- Reabrir una sala conserva su sesión, decisiones y resultado. Cada elección se aplica una vez. Las salas usan semillas independientes; abrirlas no consume la aleatoriedad de combate, tienda o mapa.
- `RunState.EventHistory` guarda por partida las decisiones, el piso, la sala y los resultados; `EventFlags` conserva las marcas de las cadenas. No añade guardado en disco (el juego actualmente no guarda partidas).

## Lista de seguimiento

| # | ID | Evento | Rareza | Pisos | Requiere | Enlaza con | Etapas | Programación | Diseño visual | Imagen |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `scattered_coins` | Monedas entre las grietas | Común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 2 | `forgotten_chest` | El cofre que respira | Común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 3 | `bottle_vendor` | El vendedor de botellas vacías | Poco común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 4 | `blood_altar` | El altar de las ocho gotas | Raro | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 5 | `whispering_well` | El pozo de los susurros | Poco común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 6 | `forgetting_pool` | La poza del olvido | Raro | 2+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 7 | `relic_peddler` | El buhonero de los bolsillos | Raro | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 8 | `masked_bazaar` | El bazar de las tres máscaras | Épico | 1+ | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 9 | `weary_guard` | La guardia que aún espera | Común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 10 | `rat_cook` | El cocinero de las ratas | Poco común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 11 | `root_spring` | El manantial de raíces | Común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 12 | `rusty_training` | El maestro de madera | Poco común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 13 | `spore_garden` | El jardín que escucha | Raro | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 14 | `collapsed_stair` | Los escalones rotos | Común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 15 | `bone_scribe` | El escriba de hueso | Común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 16 | `red_surgeon` | El cirujano carmesí | Raro | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 17 | `sunken_safe` | La caja de la cisterna | Poco común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 18 | `silent_choir` | El coro sin garganta | Raro | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 19 | `grieving_statue` | La estatua que llora sal | Poco común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 20 | `crypt_auditor` | El auditor de la cripta | Épico | 2 | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 21 | `frost_hearth` | La última brasa | Común | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 22 | `ice_sculptor` | La escultora de hielo | Raro | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 23 | `frozen_caravan` | La caravana inmóvil | Poco común | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 24 | `time_vendor` | El vendedor de minutos | Épico | 3 | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 25 | `snow_oracle` | La adivina de nieve | Raro | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 26 | `dragon_scale` | La escama bajo el lago | Legendario | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 27 | `ember_forge` | La fragua de ceniza | Poco común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 28 | `ash_pilgrims` | Los peregrinos de ceniza | Común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 29 | `lava_toll` | El peaje de la sangre negra | Raro | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 30 | `star_observatory` | El observatorio enterrado | Épico | 7+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 31 | `last_wish` | El deseo que queda | Legendario | 7+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 32 | `moon_auction` | La subasta de la luna hueca | Legendario | 1+ | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 33 | `lost_lantern` | La linterna extraviada | Poco común | 1 | — | lantern_ferryman | 1 | Implementada | Pendiente | Sin imagen |
| 34 | `lantern_ferryman` | El barquero sin ojos | Raro | 2+ | lantern_found | hidden_dock | 1 | Implementada | Pendiente | Sin imagen |
| 35 | `hidden_dock` | El muelle que no figura | Épico | 3+ | river_crossed | — | 1 | Implementada | Pendiente | Sin imagen |
| 36 | `pilgrim_seed` | La semilla del peregrino | Común | 1–2 | — | hungry_garden | 1 | Implementada | Pendiente | Sin imagen |
| 37 | `hungry_garden` | La tierra hambrienta | Poco común | 2+ | seed_carried | world_tree | 1 | Implementada | Pendiente | Sin imagen |
| 38 | `world_tree` | El árbol del último brote | Épico | 3+ | seed_planted | — | 1 | Implementada | Pendiente | Sin imagen |
| 39 | `smiling_lender` | El prestamista sonriente | Poco común | 1+ | — | debt_collector | 1 | Implementada | Pendiente | Sin imagen |
| 40 | `debt_collector` | La sombra que cobra | Raro | 2+ | debt_signed | ledger_burner | 1 | Implementada | Pendiente | Sin imagen |
| 41 | `ledger_burner` | La hoguera de los contratos | Épico | 3+ | debt_paid | — | 1 | Implementada | Pendiente | Sin imagen |
| 42 | `cracked_mask` | La máscara partida | Poco común | 1–3 | — | mirror_tailor | 1 | Implementada | Pendiente | Sin imagen |
| 43 | `mirror_tailor` | La costurera de los espejos | Raro | 2+ | mask_taken | faceless_ball | 1 | Implementada | Pendiente | Sin imagen |
| 44 | `faceless_ball` | El baile sin rostros | Épico | 3+ | mask_mended | — | 1 | Implementada | Pendiente | Sin imagen |
| 45 | `sealed_letter` | La carta sellada | Común | 1–2 | — | imprisoned_courier | 1 | Implementada | Pendiente | Sin imagen |
| 46 | `imprisoned_courier` | El mensajero encadenado | Poco común | 2+ | letter_kept | tower_reply | 1 | Implementada | Pendiente | Sin imagen |
| 47 | `tower_reply` | La respuesta de la torre | Raro | 3+ | courier_freed | — | 1 | Implementada | Pendiente | Sin imagen |
| 48 | `red_vow` | El juramento rojo | Raro | 1–3 | — | scar_broker | 1 | Implementada | Pendiente | Sin imagen |
| 49 | `scar_broker` | El tratante de cicatrices | Épico | 2+ | blood_sworn | heart_gate | 1 | Implementada | Pendiente | Sin imagen |
| 50 | `heart_gate` | La puerta del corazón | Legendario | 3+ | heart_key | — | 1 | Implementada | Pendiente | Sin imagen |
| 51 | `pocket_librarian` | La bibliotecaria de bolsillo | Poco común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 52 | `soup_of_echoes` | La sopa de los ecos | Común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 53 | `leech_cartographer` | La cartógrafa de sanguijuelas | Raro | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 54 | `coin_juggler` | El malabarista de las monedas | Poco común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 55 | `memory_laundress` | La lavandera de recuerdos | Raro | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 56 | `nail_shrine` | El santuario de los clavos | Común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 57 | `crooked_dentist` | El dentista de los colmillos | Raro | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 58 | `starling_market` | El mercado de los estorninos | Épico | 1+ | — | — | 3 | Implementada | Pendiente | Sin imagen |
| 59 | `three_cups` | Los tres vasos de barro | Poco común | 1+ | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 60 | `nameless_beggar` | La mendiga que olvidó pedir | Común | 1+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 61 | `chalk_children` | Los niños de tiza | Común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 62 | `rope_basket` | La cesta del techo | Común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 63 | `wet_boots` | Las botas junto al charco | Común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 64 | `candle_widow` | La viuda de las velas | Poco común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 65 | `wooden_toll` | El peaje de madera | Común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 66 | `snail_post` | El correo de los caracoles | Poco común | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 67 | `rust_lottery` | La tómbola oxidada | Raro | 1 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 68 | `mushroom_trial` | El juicio de los champiñones | Raro | 1 | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 69 | `bone_dominoes` | El dominó de los huesos | Común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 70 | `drowned_apothecary` | La boticaria ahogada | Poco común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 71 | `moth_archive` | El archivo de las polillas | Poco común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 72 | `ferric_confessional` | El confesionario de hierro | Raro | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 73 | `corpse_wedding` | La boda de los difuntos | Épico | 2 | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 74 | `broken_urns` | Las urnas con etiquetas | Común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 75 | `skull_weigher` | La pesadora de cráneos | Raro | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 76 | `submerged_bell` | La campana sumergida | Poco común | 2 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 77 | `avalanche_reader` | La lectora de avalanchas | Poco común | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 78 | `warm_snow` | La nieve tibia | Común | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 79 | `crystal_organ` | El órgano de cristal | Raro | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 80 | `frost_tailor` | La sastre de escarcha | Poco común | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 81 | `sleepless_sentry` | El centinela insomne | Común | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 82 | `trapped_thaw` | El deshielo cautivo | Raro | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 83 | `winter_census` | El censo del invierno | Poco común | 3 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 84 | `frozen_theatre` | El teatro congelado | Épico | 3 | — | — | 3 | Implementada | Pendiente | Sin imagen |
| 85 | `basalt_bakery` | La panadería de basalto | Común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 86 | `ash_mill` | El molino de ceniza | Poco común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 87 | `obsidian_optician` | La óptica de obsidiana | Raro | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 88 | `coal_midwife` | La partera del carbón | Raro | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 89 | `molten_fisher` | El pescador del metal líquido | Poco común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 90 | `cinder_shrine` | El santuario de las brasas frías | Poco común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 91 | `salamander_fair` | La feria de las salamandras | Épico | 4–6 | — | — | 2 | Implementada | Pendiente | Sin imagen |
| 92 | `cracked_anvil` | El yunque agrietado | Común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 93 | `sulphur_baths` | Los baños de azufre | Poco común | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 94 | `ember_tax` | El impuesto de las brasas | Raro | 4–6 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 95 | `comet_pawnshop` | La casa de empeños del cometa | Raro | 7–9 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 96 | `void_beekeeper` | La apicultora del vacío | Poco común | 7–9 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 97 | `falling_library` | La biblioteca que cae | Épico | 7–9 | — | — | 3 | Implementada | Pendiente | Sin imagen |
| 98 | `star_marrow` | El tuétano de la estrella | Épico | 7–9 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 99 | `gravity_well` | El pozo de la gravedad | Raro | 7–9 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 100 | `moon_shepherd` | El pastor de las lunas pequeñas | Poco común | 7–9 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 101 | `horizon_stitcher` | La costurera del horizonte | Épico | 7–9 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 102 | `comet_grave` | La tumba del cometa | Legendario | 7–9 | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 103 | `forgotten_sun` | El sol olvidado | Legendario | 10+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 104 | `abyssal_tailor` | El sastre del abismo | Raro | 10+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 105 | `dream_exchange` | La bolsa de los sueños | Épico | 10+ | — | — | 3 | Implementada | Pendiente | Sin imagen |
| 106 | `dead_god_table` | La mesa del dios muerto | Legendario | 10+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 107 | `upside_down_rain` | La lluvia que asciende | Común | 10+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 108 | `eternal_toll` | El peaje de lo eterno | Raro | 10+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 109 | `last_lighthouse` | El faro sin costa | Poco común | 10+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 110 | `wish_eater` | El devorador de deseos | Épico | 10+ | — | — | 1 | Implementada | Pendiente | Sin imagen |
| 111 | `glass_key` | La llave transparente | Poco común | 1–2 | — | glass_lock | 1 | Implementada | Pendiente | Sin imagen |
| 112 | `glass_lock` | La cerradura sin puerta | Raro | 2+ | glass_key_kept | glass_stair | 1 | Implementada | Pendiente | Sin imagen |
| 113 | `glass_stair` | La escalera de las grietas | Raro | 3+ | glass_lock_opened | glass_sanctum | 1 | Implementada | Pendiente | Sin imagen |
| 114 | `glass_sanctum` | El santuario que deja ver | Épico | 4+ | glass_stair_mended | — | 1 | Implementada | Pendiente | Sin imagen |
| 115 | `hollow_violin` | El violín hueco | Común | 1–3 | — | string_mender | 1 | Implementada | Pendiente | Sin imagen |
| 116 | `string_mender` | La remendadora de cuerdas | Poco común | 2+ | violin_carried | buried_concert | 1 | Implementada | Pendiente | Sin imagen |
| 117 | `buried_concert` | El concierto enterrado | Raro | 3+ | violin_repaired | final_note | 1 | Implementada | Pendiente | Sin imagen |
| 118 | `final_note` | La nota que no termina | Épico | 4+ | concert_played | — | 1 | Implementada | Pendiente | Sin imagen |
| 119 | `caged_ember` | La brasa enjaulada | Poco común | 2–4 | — | ember_nursery | 1 | Implementada | Pendiente | Sin imagen |
| 120 | `ember_nursery` | La guardería de las brasas | Raro | 4+ | ember_adopted | walking_furnace | 1 | Implementada | Pendiente | Sin imagen |
| 121 | `walking_furnace` | El horno caminante | Épico | 6+ | ember_awakened | phoenix_cradle | 1 | Implementada | Pendiente | Sin imagen |
| 122 | `phoenix_cradle` | La cuna del fénix pequeño | Legendario | 8+ | phoenix_promised | — | 1 | Implementada | Pendiente | Sin imagen |
| 123 | `salt_passport` | El pasaporte de sal | Común | 1–2 | — | customs_of_ash | 1 | Implementada | Pendiente | Sin imagen |
| 124 | `customs_of_ash` | La aduana de las cenizas | Poco común | 2+ | salt_passport_kept | sunken_embassy | 1 | Implementada | Pendiente | Sin imagen |
| 125 | `sunken_embassy` | La embajada sumergida | Raro | 3+ | salt_visa_paid | exile_return | 1 | Implementada | Pendiente | Sin imagen |
| 126 | `exile_return` | El regreso de los exiliados | Épico | 4+ | exiles_called | — | 1 | Implementada | Pendiente | Sin imagen |
| 127 | `borrowed_hour` | La hora prestada | Raro | 3–5 | — | minute_prison | 1 | Implementada | Pendiente | Sin imagen |
| 128 | `minute_prison` | La prisión de los minutos | Raro | 5+ | hour_borrowed | clock_heart | 1 | Implementada | Pendiente | Sin imagen |
| 129 | `clock_heart` | El corazón del reloj | Épico | 7+ | minutes_freed | dawn_debt | 1 | Implementada | Pendiente | Sin imagen |
| 130 | `dawn_debt` | La deuda del amanecer | Legendario | 9+ | clock_restarted | — | 1 | Implementada | Pendiente | Sin imagen |
| 131 | `stone_memory` | La piedra que recuerda | Poco común | 4–6 | — | memory_quarry | 1 | Implementada | Pendiente | Sin imagen |
| 132 | `memory_quarry` | La cantera de los recuerdos | Raro | 5+ | stone_memory_kept | sleeping_mountain | 1 | Implementada | Pendiente | Sin imagen |
| 133 | `sleeping_mountain` | La montaña dormida | Épico | 7+ | mountain_face_restored | mountain_verdict | 1 | Implementada | Pendiente | Sin imagen |
| 134 | `mountain_verdict` | El veredicto de la montaña | Legendario | 9+ | mountain_awakened | — | 1 | Implementada | Pendiente | Sin imagen |
| 135 | `copper_egg` | El huevo de cobre | Común | 1 | — | brass_incubator | 1 | Implementada | Pendiente | Sin imagen |
| 136 | `brass_incubator` | La incubadora de latón | Poco común | 2+ | copper_egg_carried | clockwork_chick, silent_bell | 1 | Implementada | Pendiente | Sin imagen |
| 137 | `clockwork_chick` | El polluelo de cuerda | Raro | 3+ | copper_chick_chosen | — | 1 | Implementada | Pendiente | Sin imagen |
| 138 | `silent_bell` | La campana sin sonido | Raro | 3+ | copper_bell_chosen | — | 1 | Implementada | Pendiente | Sin imagen |
| 139 | `stolen_name` | El nombre robado | Poco común | 1–3 | — | name_registry | 1 | Implementada | Pendiente | Sin imagen |
| 140 | `name_registry` | El registro de las identidades | Raro | 2+ | stolen_name_kept | borrowed_crown, returned_name | 1 | Implementada | Pendiente | Sin imagen |
| 141 | `returned_name` | La persona que vuelve a llamarse | Épico | 3+ | name_return_chosen | — | 1 | Implementada | Pendiente | Sin imagen |
| 142 | `borrowed_crown` | La corona de un nombre prestado | Épico | 3+ | name_crown_chosen | — | 1 | Implementada | Pendiente | Sin imagen |
| 143 | `blind_map` | El mapa de la cartógrafa ciega | Común | 1–3 | — | compass_trial | 1 | Implementada | Pendiente | Sin imagen |
| 144 | `compass_trial` | La prueba de la brújula | Raro | 2+ | blind_map_kept | treasure_lie, true_north | 1 | Implementada | Pendiente | Sin imagen |
| 145 | `true_north` | El refugio del norte verdadero | Raro | 3+ | true_north_chosen | — | 1 | Implementada | Pendiente | Sin imagen |
| 146 | `treasure_lie` | El tesoro de la mentira | Épico | 3+ | treasure_lie_chosen | — | 1 | Implementada | Pendiente | Sin imagen |
| 147 | `orphan_shadow` | La sombra huérfana | Poco común | 2–4 | — | shadow_trial | 1 | Implementada | Pendiente | Sin imagen |
| 148 | `shadow_trial` | El tribunal de las sombras | Raro | 3+ | orphan_shadow_kept | shadow_crown, shadow_reunion | 1 | Implementada | Pendiente | Sin imagen |
| 149 | `shadow_reunion` | El abrazo de dos siluetas | Épico | 4+ | shadow_reunion_chosen | — | 1 | Implementada | Pendiente | Sin imagen |
| 150 | `shadow_crown` | La reina de la noche pequeña | Legendario | 4+ | shadow_crown_chosen | — | 1 | Implementada | Pendiente | Sin imagen |

## Historias enlazadas y finales alternativos

- La linterna extraviada: `lost_lantern` → `lantern_ferryman` → `hidden_dock`.
- La semilla del peregrino: `pilgrim_seed` → `hungry_garden` → `world_tree`.
- El prestamista sonriente: `smiling_lender` → `debt_collector` → `ledger_burner` / `smiling_lender` → `debt_collector` → `ledger_burner`.
- La máscara partida: `cracked_mask` → `mirror_tailor` → `faceless_ball`.
- La carta sellada: `sealed_letter` → `imprisoned_courier` → `tower_reply`.
- El juramento rojo: `red_vow` → `scar_broker` → `heart_gate`.
- La llave transparente: `glass_key` → `glass_lock` → `glass_stair` → `glass_sanctum`.
- El violín hueco: `hollow_violin` → `string_mender` → `buried_concert` → `final_note`.
- La brasa enjaulada: `caged_ember` → `ember_nursery` → `walking_furnace` → `phoenix_cradle`.
- El pasaporte de sal: `salt_passport` → `customs_of_ash` → `sunken_embassy` → `exile_return`.
- La hora prestada: `borrowed_hour` → `minute_prison` → `clock_heart` → `dawn_debt`.
- La piedra que recuerda: `stone_memory` → `memory_quarry` → `sleeping_mountain` → `mountain_verdict`.
- El huevo de cobre: `copper_egg` → `brass_incubator` → `clockwork_chick` / `copper_egg` → `brass_incubator` → `silent_bell`.
- El nombre robado: `stolen_name` → `name_registry` → `returned_name` / `stolen_name` → `name_registry` → `borrowed_crown`.
- El mapa de la cartógrafa ciega: `blind_map` → `compass_trial` → `true_north` / `blind_map` → `compass_trial` → `treasure_lie`.
- La sombra huérfana: `orphan_shadow` → `shadow_trial` → `shadow_reunion` / `orphan_shadow` → `shadow_trial` → `shadow_crown`.

## Decisiones y consecuencias

### 01. Monedas entre las grietas (`scattered_coins`)

Etapa 1: Un hilo de monedas conduce hasta una grieta sin fondo.

- Recoger las monedas cercanas: Rescatas 22 de oro.
- Meter el brazo en la grieta [5 vida]: Extraes 42 de oro.
- Seguir el camino: Sigues tu camino.

### 02. El cofre que respira (`forgotten_chest`)

Etapa 1: El cofre se hincha y deshincha como un animal dormido.

- Abrir la tapa (65% éxito): Dentro hay 45 de oro. Fallo: Una mandíbula te causa hasta 6 de daño.
- Recoger la bolsa de al lado: Contiene 12 de oro.
- Seguir el camino: Sigues tu camino.

### 03. El vendedor de botellas vacías (`bottle_vendor`)

Etapa 1: Un anciano vende aire embotellado. Jura que recuerda cómo respirar.

- Comprar aire de primavera [30 oro]: Recuperas 20 de vida.
- Comprar aire de cumbre [45 oro]: Ganas 3 de vida máxima.
- Seguir el camino: Sigues tu camino.

### 04. El altar de las ocho gotas (`blood_altar`)

Etapa 1: Una balanza de piedra pesa sangre contra monedas.

- Entregar ocho gotas [8 vida]: Recibes 55 de oro.
- Dejar una ofrenda de oro [25 oro]: Recuperas 18 de vida.
- Seguir el camino: Sigues tu camino.

### 05. El pozo de los susurros (`whispering_well`)

Etapa 1: Una voz promete devolverte tres veces lo que arrojes.

- Lanzar una moneda de plata [20 oro] (60% éxito): El pozo devuelve 60 de oro. Fallo: El pozo guarda tu ofrenda.
- Escuchar demasiado cerca [4 vida]: Aprendes un secreto: +2 de suerte.
- Seguir el camino: Sigues tu camino.

### 06. La poza del olvido (`forgetting_pool`)

Etapa 1: En el reflejo ves la primera carta de tu mazo desvaneciéndose.

- Olvidar la primera carta del mazo [10 oro]: Una carta abandona tu mazo.
- Beber un recuerdo: Recuperas 14 de vida.
- Seguir el camino: Sigues tu camino.

### 07. El buhonero de los bolsillos (`relic_peddler`)

Etapa 1: Una mano sale de cada bolsillo de su abrigo. Solo una ofrece algo verdadero.

- Comprar la Pata de Conejo [85 oro]: La mano correcta te entrega su tesoro.
- Cambiar sangre por provisiones [7 vida]: Obtienes 35 de oro y recuperas 4 de vida.
- Seguir el camino: Sigues tu camino.

### 08. El bazar de las tres máscaras (`masked_bazaar`)

Etapa 1: La entrada exige una moneda por cada rostro que has olvidado.

- Pagar entrada y mirar los puestos [20 oro]: Cruzas la cortina. La entrada no se devuelve. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: Dentro puedes hacer una sola compra antes de que el bazar desaparezca.

- Comprar el tónico azul [30 oro]: Recuperas 30 de vida.
- Comprar una lección prohibida [40 oro]: Mejoras la primera carta sin mejorar.
- Salir sin comprar: Sigues tu camino.

### 09. La guardia que aún espera (`weary_guard`)

Etapa 1: Una guardia agotada comparte sus vendas a cambio de una conversación.

- Escuchar su historia: Te venda: recuperas 10 de vida.
- Pagarle por sus provisiones [20 oro]: Recuperas 22 de vida.
- Seguir el camino: Sigues tu camino.

### 10. El cocinero de las ratas (`rat_cook`)

Etapa 1: Una rata con gorro sirve un caldo que se mueve sin fuego.

- Comprar un plato [12 oro] (80% éxito): El caldo cura 18 de vida. Fallo: El caldo estaba vivo: hasta 4 de daño.
- Ayudar a limpiar la cocina [3 vida]: Te paga 18 de oro.
- Seguir el camino: Sigues tu camino.

### 11. El manantial de raíces (`root_spring`)

Etapa 1: Las raíces filtran agua clara entre las piedras del primer descenso.

- Beber despacio: Ganas 3 de vida máxima y recuperas 3.
- Recoger las monedas del fondo: Obtienes 18 de oro.
- Seguir el camino: Sigues tu camino.

### 12. El maestro de madera (`rusty_training`)

Etapa 1: Un muñeco de entrenamiento corrige tu postura con una voz prestada.

- Pagar una lección [25 oro]: Mejoras la primera carta sin mejorar.
- Practicar sin protección [7 vida]: Mejoras la primera carta sin mejorar.
- Seguir el camino: Sigues tu camino.

### 13. El jardín que escucha (`spore_garden`)

Etapa 1: Los hongos inclinan sus sombreros cuando oyen tu corazón.

- Darles de beber sangre [6 vida]: El jardín te bendice: +3 de suerte.
- Recoger setas seguras: Recuperas 8 de vida.
- Seguir el camino: Sigues tu camino.

### 14. Los escalones rotos (`collapsed_stair`)

Etapa 1: Bajo un tramo derrumbado reluce la bolsa de otro aventurero.

- Bajar entre las piedras (75% éxito): Rescatas 35 de oro. Fallo: La piedra cede: hasta 4 de daño.
- Buscar en el borde: Encuentras 9 de oro.
- Seguir el camino: Sigues tu camino.

### 15. El escriba de hueso (`bone_scribe`)

Etapa 1: Escribe sobre costillas y vende historias capaces de cambiar un combate.

- Comprar una historia [30 oro]: Obtienes una carta de tu clase o neutral.
- Dictarle tus heridas: Recuperas 10 de vida.
- Seguir el camino: Sigues tu camino.

### 16. El cirujano carmesí (`red_surgeon`)

Etapa 1: Sus herramientas no cortan carne: extraen futuros posibles.

- Entregar una parte de tu futuro [5 vida máxima]: Mejoras una carta y recuperas 20 de vida.
- Comprar una sutura normal [25 oro]: Recuperas 18 de vida.
- Seguir el camino: Sigues tu camino.

### 17. La caja de la cisterna (`sunken_safe`)

Etapa 1: Una caja fuerte descansa bajo un agua que no refleja tu rostro.

- Bucear hasta el fondo (55% éxito): Recuperas 70 de oro. Fallo: Algo te retiene: hasta 10 de daño.
- Pescar con una cuerda [5 oro]: Rescatas 20 de oro.
- Seguir el camino: Sigues tu camino.

### 18. El coro sin garganta (`silent_choir`)

Etapa 1: Los muertos necesitan un latido para terminar su canción.

- Prestarles tu latido [10 vida]: Ganas 5 de vida máxima.
- Ofrecer monedas para su funeral [18 oro]: Recuperas 15 de vida.
- Seguir el camino: Sigues tu camino.

### 19. La estatua que llora sal (`grieving_statue`)

Etapa 1: Su cuenco está vacío. Sus lágrimas conservan las heridas cerradas.

- Llenar el cuenco [20 oro]: Recuperas 24 de vida.
- Secar sus lágrimas: Recuperas 7 de vida.
- Seguir el camino: Sigues tu camino.

### 20. El auditor de la cripta (`crypt_auditor`)

Etapa 1: Un funcionario muerto exige revisar hasta tus pensamientos.

- Aceptar la revisión: El auditor abre su libro. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: Tu deuda está escrita en una página que aún sangra.

- Pagar y recibir el certificado [50 oro]: Ganas 4 de suerte.
- Arrancar la página (50% éxito): Recuperas una fianza de 90 de oro. Fallo: La tinta quema: hasta 12 de daño.
- Negarse y marcharse: Sigues tu camino.

### 21. La última brasa (`frost_hearth`)

Etapa 1: Una brasa minúscula mantiene caliente una sala imposible.

- Comprar leña al guardián [20 oro]: Recuperas 18 de vida.
- Calentarte unos instantes: Recuperas 8 de vida.
- Seguir el camino: Sigues tu camino.

### 22. La escultora de hielo (`ice_sculptor`)

Etapa 1: Puede tallar una técnica perfecta si le permites medir tus cicatrices.

- Dejar que talle tu recuerdo [6 vida]: Mejoras la primera carta sin mejorar.
- Comprar una miniatura [25 oro]: Ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 23. La caravana inmóvil (`frozen_caravan`)

Etapa 1: Los mercaderes siguen sonriendo bajo una capa de hielo.

- Romper el hielo con las manos [7 vida]: Rescatas 55 de oro.
- Buscar fuera de los carros: Encuentras 15 de oro.
- Seguir el camino: Sigues tu camino.

### 24. El vendedor de minutos (`time_vendor`)

Etapa 1: Vende un minuto que puedes vivir dos veces. La segunda vez siempre duele.

- Comprar un minuto [25 oro]: El reloj retrocede y se abre otro mostrador. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: Decide qué conservar del minuto repetido.

- Recordar la técnica [40 oro]: Mejoras una carta.
- Recordar el número ganador [8 vida]: Ganas 4 de suerte.
- Olvidar el minuto: Sigues tu camino.

### 25. La adivina de nieve (`snow_oracle`)

Etapa 1: Cada copo en su mano contiene una muerte que podría ser la tuya.

- Renunciar a uno de tus destinos [4 vida máxima]: Ganas 5 de suerte.
- Pedir una lectura breve [30 oro]: Ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 26. La escama bajo el lago (`dragon_scale`)

Etapa 1: Una escama enorme custodia un corazón metálico en el hielo.

- Arrancar el corazón del hielo [15 vida]: Obtienes Corazón de Hierro.
- Vender un fragmento de escama: Obtienes 85 de oro.
- Seguir el camino: Sigues tu camino.

### 27. La fragua de ceniza (`ember_forge`)

Etapa 1: El herrero solo conserva sus manos. Lo demás es humo.

- Pagar una nueva técnica [45 oro]: Mejoras una carta.
- Cambiar sangre por acero [10 vida]: Mejoras una carta.
- Seguir el camino: Sigues tu camino.

### 28. Los peregrinos de ceniza (`ash_pilgrims`)

Etapa 1: Una procesión te ofrece un lugar junto a su fuego.

- Compartir provisiones [15 oro]: Recuperas 20 de vida.
- Escuchar sus oraciones: Ganas 1 de suerte.
- Seguir el camino: Sigues tu camino.

### 29. El peaje de la sangre negra (`lava_toll`)

Etapa 1: El cobrador paga por una gota que jamás volverá a tu cuerpo.

- Vender parte de tu vitalidad [6 vida máxima]: Obtienes 130 de oro.
- Ofrecer una muestra pequeña [8 vida]: Obtienes 45 de oro.
- Seguir el camino: Sigues tu camino.

### 30. El observatorio enterrado (`star_observatory`)

Etapa 1: Aquí abajo las estrellas observan hacia arriba, buscando una salida.

- Compartir tu luz con las estrellas [20 vida]: Ganas 8 de vida máxima y 4 de suerte.
- Comprar un mapa del cielo [60 oro]: Ganas 3 de suerte.
- Seguir el camino: Sigues tu camino.

### 31. El deseo que queda (`last_wish`)

Etapa 1: Una voz ofrece cumplir algo pequeño. Lo imposible ya se lo pidió otro.

- Desear seguir viviendo: Ganas 12 de vida máxima y recuperas 40.
- Desear una fortuna: Obtienes 250 de oro.
- Seguir el camino: Sigues tu camino.

### 32. La subasta de la luna hueca (`moon_auction`)

Etapa 1: Tres sombras pujan por objetos que todavía no existen.

- Comprar un asiento [30 oro]: La subasta comienza. Solo puedes ganar un lote. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: El subastador te concede una única puja sin rivales.

- Pujar por el Corazón de Hierro [100 oro]: El lote es tuyo.
- Pujar con tu sangre por una lección [12 vida]: Mejoras una carta y ganas 3 de suerte.
- Abandonar la subasta: Sigues tu camino.

### 33. La linterna extraviada (`lost_lantern`)

Etapa 1: Una luz tiembla bajo una mochila vacía. Un nombre está grabado en el asa.

- Recoger la linterna: Llevas la luz contigo. Un barquero podría reconocerla. Marca: `lantern_found` → `lantern_ferryman`.
- Vender sus piezas: Obtienes 25 de oro.
- Seguir el camino: Sigues tu camino.

### 34. El barquero sin ojos (`lantern_ferryman`)

Etapa 1: Reconoce la luz de su hija. A cambio te ofrece un pasaje.

- Entregar la linterna y cruzar: Recuperas 12 de vida y conoces el muelle oculto. Marca: `river_crossed` → `hidden_dock`.
- Aceptar solo una recompensa: Obtienes 65 de oro y la historia termina aquí.
- Seguir el camino: Sigues tu camino.

### 35. El muelle que no figura (`hidden_dock`)

Etapa 1: Tras la travesía encuentras las mercancías de un reino desaparecido.

- Abrir el baúl del capitán: Obtienes 100 de oro y una carta.
- Descansar junto al agua: Ganas 4 de vida máxima y recuperas 24.
- Seguir el camino: Sigues tu camino.

### 36. La semilla del peregrino (`pilgrim_seed`)

Etapa 1: Un viajero moribundo pide que plantes su última semilla más abajo.

- Guardar la semilla: Prometes encontrar tierra donde nada crece. Marca: `seed_carried` → `hungry_garden`.
- Comer la semilla: Recuperas 12 de vida.
- Seguir el camino: Sigues tu camino.

### 37. La tierra hambrienta (`hungry_garden`)

Etapa 1: La semilla late cuando te acercas a un jardín de tierra gris.

- Plantarla y regarla con sangre [6 vida]: La semilla echa raíces. Algo crecerá más abajo. Marca: `seed_planted` → `world_tree`.
- Venderla al jardinero: Obtienes 50 de oro.
- Seguir el camino: Sigues tu camino.

### 38. El árbol del último brote (`world_tree`)

Etapa 1: El árbol ha crecido atravesando los pisos hasta volver a encontrarte.

- Comer su fruto: Ganas 7 de vida máxima y recuperas 25.
- Recoger las semillas de oro: Obtienes 110 de oro.
- Seguir el camino: Sigues tu camino.

### 39. El prestamista sonriente (`smiling_lender`)

Etapa 1: Te adelanta oro a cambio de que lo recuerdes cuando vuelvas a verlo.

- Firmar el préstamo: Recibes 70 de oro. Más abajo exigirá 95. Marca: `debt_signed` → `debt_collector`.
- Seguir el camino: Sigues tu camino.

### 40. La sombra que cobra (`debt_collector`)

Etapa 1: La firma sale de tu bolsillo y se convierte en una mano abierta.

- Pagar la deuda [95 oro]: Recibes un recibo para destruir el contrato. Marca: `debt_paid` → `ledger_burner`.
- Pagar con años de vida [5 vida máxima]: Recibes el mismo recibo, escrito en sangre. Marca: `debt_paid` → `ledger_burner`.
- Seguir el camino: Sigues tu camino.

### 41. La hoguera de los contratos (`ledger_burner`)

Etapa 1: El prestamista olvidó que un recibo también puede quemar su libro.

- Quemar tu recibo: Ganas 4 de vida máxima y 4 de suerte.
- Vender el recibo a su rival: Obtienes 145 de oro.
- Seguir el camino: Sigues tu camino.

### 42. La máscara partida (`cracked_mask`)

Etapa 1: La mitad de una sonrisa se aferra a una pared del corredor.

- Llevar la máscara [3 vida]: Una costurera de espejos podría arreglarla. Marca: `mask_taken` → `mirror_tailor`.
- Romperla por completo: Entre sus fragmentos hay 18 de oro.
- Seguir el camino: Sigues tu camino.

### 43. La costurera de los espejos (`mirror_tailor`)

Etapa 1: Cose el reflejo de tu máscara y te invita a un baile bajo tierra.

- Pagar la reparación [30 oro]: La máscara es una invitación al baile. Marca: `mask_mended` → `faceless_ball`.
- Venderle los fragmentos: Obtienes 45 de oro.
- Seguir el camino: Sigues tu camino.

### 44. El baile sin rostros (`faceless_ball`)

Etapa 1: Todos llevan tu rostro bajo una máscara diferente.

- Bailar con tu reflejo: Mejoras una carta y ganas 3 de suerte.
- Vaciar el guardarropa: Obtienes 105 de oro.
- Seguir el camino: Sigues tu camino.

### 45. La carta sellada (`sealed_letter`)

Etapa 1: Un sobre lleva la dirección de una prisión que aún no has visto.

- Prometer entregarla: Guardas el sobre sin abrirlo. Marca: `letter_kept` → `imprisoned_courier`.
- Abrirla y quedarte el adelanto: Contiene 20 de oro.
- Seguir el camino: Sigues tu camino.

### 46. El mensajero encadenado (`imprisoned_courier`)

Etapa 1: El destinatario sigue vivo, pero necesita que pagues al carcelero.

- Pagar su libertad y entregar la carta [25 oro]: Te confía la respuesta para la torre. Marca: `courier_freed` → `tower_reply`.
- Vender la carta al carcelero: Obtienes 55 de oro.
- Seguir el camino: Sigues tu camino.

### 47. La respuesta de la torre (`tower_reply`)

Etapa 1: Una ventana se abre al reconocer el sello del mensajero.

- Entregar la respuesta: Recibes 85 de oro y una carta.
- Pedir auxilio en lugar de oro: Recuperas 35 de vida y ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 48. El juramento rojo (`red_vow`)

Etapa 1: Una puerta viva promete reconocerte si dejas una marca en su lengua.

- Sellar el juramento con sangre [7 vida]: La puerta recuerda tu pulso. Marca: `blood_sworn` → `scar_broker`.
- Recoger las ofrendas abandonadas: Obtienes 20 de oro.
- Seguir el camino: Sigues tu camino.

### 49. El tratante de cicatrices (`scar_broker`)

Etapa 1: Reconoce la marca. Puede convertirla en una llave que seguirá latiendo.

- Convertir la cicatriz en llave [4 vida máxima]: La llave de carne apunta hacia un corazón. Marca: `heart_key` → `heart_gate`.
- Borrar la marca: Recuperas 18 de vida y termina el juramento.
- Seguir el camino: Sigues tu camino.

### 50. La puerta del corazón (`heart_gate`)

Etapa 1: La llave late al mismo ritmo que una puerta enterrada bajo otras puertas.

- Recuperar lo sacrificado: Ganas 10 de vida máxima y recuperas 30.
- Cambiar el corazón por poder: Mejoras una carta, ganas 5 de suerte y 80 de oro.
- Seguir el camino: Sigues tu camino.

### 51. La bibliotecaria de bolsillo (`pocket_librarian`)

Etapa 1: Una mujer diminuta dirige una biblioteca instalada dentro de una bota. Cobra por cada página que debes olvidar.

- Comprar un relato de combate [35 oro]: Obtienes una carta de tu clase o neutral.
- Donar la primera carta del mazo: La bibliotecaria elimina esa carta y te paga 18 de oro.
- Seguir el camino: Sigues tu camino.

### 52. La sopa de los ecos (`soup_of_echoes`)

Etapa 1: El cuenco repite las últimas palabras de quienes comieron aquí. Todas fueron «otro poco».

- Comprar una ración [18 oro]: Recuperas 16 de vida.
- Probar la cuchara abandonada: Recuperas 7 de vida.
- Seguir el camino: Sigues tu camino.

### 53. La cartógrafa de sanguijuelas (`leech_cartographer`)

Etapa 1: Sus sanguijuelas dibujan rutas sobre un pergamino húmedo. Necesita sangre fresca para interpretar la siguiente línea.

- Alimentar a sus dibujantes [8 vida]: Aprendes a reconocer los malos presagios: +3 de suerte.
- Comprar el margen del mapa [20 oro]: El margen contiene 45 de oro olvidados.
- Seguir el camino: Sigues tu camino.

### 54. El malabarista de las monedas (`coin_juggler`)

Etapa 1: Ninguna moneda toca el suelo. El artista te invita a detener una con los dedos.

- Apostar por atrapar la moneda dorada [25 oro] (55% éxito): Ganas 80 de oro. Fallo: La moneda era una ilusión; pierdes la apuesta.
- Ayudar a recoger el sombrero: Recibes una propina de 14 de oro.
- Seguir el camino: Sigues tu camino.

### 55. La lavandera de recuerdos (`memory_laundress`)

Etapa 1: Lava nombres y promesas en un río que cabe en una palangana. Reconoces tu primera carta entre la espuma.

- Lavar la primera carta del mazo [32 oro]: Eliminas esa carta y recuperas 6 de vida.
- Enjuagarte las manos [8 oro]: Recuperas 10 de vida.
- Seguir el camino: Sigues tu camino.

### 56. El santuario de los clavos (`nail_shrine`)

Etapa 1: Cada clavo sostiene una plegaria. Aún queda un hueco junto a una pequeña bolsa de limosnas.

- Clavar tu plegaria [3 vida]: Ganas 2 de vida máxima.
- Ordenar las ofrendas caídas: Encuentras 16 de oro.
- Seguir el camino: Sigues tu camino.

### 57. El dentista de los colmillos (`crooked_dentist`)

Etapa 1: El dentista te ofrece una sonrisa más afortunada. Sus instrumentos llevan grabado el precio de cada intento.

- Permitir una extracción imposible [3 vida máxima]: Ganas 4 de suerte.
- Comprar una venda limpia [17 oro]: Recuperas 14 de vida.
- Seguir el camino: Sigues tu camino.

### 58. El mercado de los estorninos (`starling_market`)

Etapa 1: Un millar de pájaros negocia desde los bolsillos de un solo abrigo. Exigen una entrada antes de abrirlo.

- Pagar y entrar en el abrigo [15 oro]: Las mangas se convierten en dos pasillos. La entrada no se devuelve. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: Los estorninos solo te permiten visitar un puesto.

- Visitar al vendedor de remedios: Una boticaria de plumas abre su maletín. Abre otra decisión en esta sala.
- Comprar una técnica al pregonero [35 oro]: Mejoras la primera carta sin mejorar.
- Seguir el camino: Sigues tu camino.

Etapa 3: La boticaria acepta monedas o una pequeña muestra de sangre.

- Comprar ungüento de plumas [26 oro]: Recuperas 28 de vida.
- Cambiar sangre por una semilla [8 vida]: Ganas 4 de vida máxima.
- Seguir el camino: Sigues tu camino.

### 59. Los tres vasos de barro (`three_cups`)

Etapa 1: Una mano invisible mueve tres vasos. Bajo uno hay oro; bajo otro, un diente; el tercero está vacío.

- Sentarse a la mesa [5 oro]: El anfitrión prepara los vasos. El asiento no se devuelve. Abre otra decisión en esta sala.
- Comprar el vaso vacío [10 oro]: Dentro queda un pequeño amuleto: +1 de suerte.
- Seguir el camino: Sigues tu camino.

Etapa 2: El anfitrión termina de mezclar los vasos. Puedes apostar o pedirle una lección.

- Apostar por el vaso del centro [15 oro] (35% éxito): Encuentras 90 de oro. Fallo: El vaso muerde tus dedos: hasta 5 de daño.
- Comprar una lección sobre sus trucos [25 oro]: Ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 60. La mendiga que olvidó pedir (`nameless_beggar`)

Etapa 1: Sostiene un cuenco al revés. Parece esperar que alguien le enseñe de nuevo a recibir.

- Dejar monedas en sus manos [22 oro]: Su agradecimiento te acompaña: +2 de suerte.
- Sentarte a su lado: El descanso recupera 6 de vida.
- Seguir el camino: Sigues tu camino.

### 61. Los niños de tiza (`chalk_children`)

Etapa 1: Unos dibujos infantiles saltan de baldosa en baldosa. Necesitan una última raya para terminar su juego.

- Completar el dibujo: Ganas 1 de suerte.
- Buscar su tesoro bajo la baldosa [3 vida]: Encuentras 24 de oro.
- Seguir el camino: Sigues tu camino.

### 62. La cesta del techo (`rope_basket`)

Etapa 1: Una cesta baja desde la oscuridad. Una nota pide comida, pero dentro solo hay una cuerda muy gastada.

- Dejar monedas para quien espera arriba [15 oro]: La cesta vuelve con vendas: recuperas 18 de vida.
- Tirar de la cuerda con fuerza (70% éxito): Cae una bolsa de 34 de oro. Fallo: La cuerda se rompe: hasta 4 de daño.
- Seguir el camino: Sigues tu camino.

### 63. Las botas junto al charco (`wet_boots`)

Etapa 1: Unas botas vacías cruzaron el charco antes que tú. Su dueño dejó los calcetines llenos de monedas.

- Vaciar los calcetines: Recoges 17 de oro.
- Seguir las huellas descalzas [3 vida]: Encuentras provisiones y recuperas 13 de vida.
- Seguir el camino: Sigues tu camino.

### 64. La viuda de las velas (`candle_widow`)

Etapa 1: Enciende una vela por cada aventurero que no regresa. Tiene una preparada con tu nombre.

- Comprar tu vela antes de que la encienda [28 oro]: Ganas 3 de vida máxima.
- Ayudar a encender las demás: Ganas 1 de suerte y recuperas 5 de vida.
- Seguir el camino: Sigues tu camino.

### 65. El peaje de madera (`wooden_toll`)

Etapa 1: Una barrera protege una pared sin puerta. El recaudador parece tan confundido como tú.

- Pagar el peaje simbólico [8 oro]: Te devuelve 24 de oro como premio por tu paciencia.
- Desmontar la barrera para él: Recuperas 9 de vida al compartir su almuerzo.
- Seguir el camino: Sigues tu camino.

### 66. El correo de los caracoles (`snail_post`)

Etapa 1: Un caracol transporta cartas urgentes desde hace doscientos años. Busca un sustituto para su descanso.

- Ordenar las cartas extraviadas [18 oro]: El correo te entrega una carta de tu clase o neutral.
- Comprar un sello antiguo [20 oro]: Ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 67. La tómbola oxidada (`rust_lottery`)

Etapa 1: El bombo solo contiene números que ya salieron. La taquillera insiste en que eso mejora tus posibilidades.

- Comprar un boleto [18 oro] (40% éxito): El premio son 75 de oro. Fallo: El número vuelve a perder.
- Cambiar sangre por un boleto premiado [7 vida]: Recibes 42 de oro.
- Seguir el camino: Sigues tu camino.

### 68. El juicio de los champiñones (`mushroom_trial`)

Etapa 1: Un círculo de hongos te acusa de pisar a su embajador. Puedes defenderte o aceptar un acuerdo.

- Declararte inocente: Los hongos te conceden una audiencia. Abre otra decisión en esta sala.
- Pagar una compensación [15 oro]: El tribunal te cura 16 de vida al despedirte.
- Seguir el camino: Sigues tu camino.

Etapa 2: El fiscal te pide una prueba de buena voluntad.

- Ofrecer sangre al micelio [6 vida]: El jurado acepta la prueba: +3 de suerte.
- Recitar tu técnica de memoria [25 oro]: El jurado la perfecciona: mejoras una carta.
- Seguir el camino: Sigues tu camino.

### 69. El dominó de los huesos (`bone_dominoes`)

Etapa 1: Dos esqueletos llevan siglos esperando una tercera persona. Las fichas recuerdan a sus antiguos dueños.

- Jugar una ronda [12 oro] (70% éxito): Ganas 38 de oro. Fallo: Pierdes la ronda y la entrada.
- Reparar la mesa coja: Recibes 15 de oro.
- Seguir el camino: Sigues tu camino.

### 70. La boticaria ahogada (`drowned_apothecary`)

Etapa 1: Vende medicinas desde el interior de una campana de buzo llena de agua negra.

- Comprar una ampolla de aire [28 oro]: Recuperas 26 de vida.
- Donar una muestra para sus estudios [8 vida]: Recibes 45 de oro.
- Seguir el camino: Sigues tu camino.

### 71. El archivo de las polillas (`moth_archive`)

Etapa 1: Las polillas comen las palabras más peligrosas. Todavía queda una técnica intacta en el borde de una página.

- Comprar la página intacta [34 oro]: Mejoras la primera carta sin mejorar.
- Dejar que devoren tu primera carta [4 vida]: Eliminas esa carta y recuperas 8 de vida.
- Seguir el camino: Sigues tu camino.

### 72. El confesionario de hierro (`ferric_confessional`)

Etapa 1: El sacerdote es una armadura vacía. Solo absolverá lo que estés dispuesto a perder para siempre.

- Confesar una parte de tu vida [4 vida máxima]: Ganas 4 de suerte.
- Dejar una limosna [20 oro]: Recuperas 20 de vida.
- Seguir el camino: Sigues tu camino.

### 73. La boda de los difuntos (`corpse_wedding`)

Etapa 1: Los invitados no tienen testigos vivos. Te ofrecen un asiento a cambio de una palabra amable.

- Aceptar la invitación: Los novios abren un lugar en su mesa. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: Antes del brindis debes decidir qué regalarles.

- Ofrecer un regalo de monedas [35 oro]: El brindis te devuelve 28 de vida y una bendición de +2 de suerte.
- Entregar la primera carta del mazo: Aceptan la carta y te entregan 55 de oro.
- Seguir el camino: Sigues tu camino.

### 74. Las urnas con etiquetas (`broken_urns`)

Etapa 1: Las etiquetas dicen «ceniza», «monedas» y «no abrir». Alguien las cambió de lugar.

- Abrir la urna que pesa más (60% éxito): Encuentras 48 de oro. Fallo: Una nube cortante causa hasta 7 de daño.
- Leer las inscripciones sin tocar nada: Ganas 1 de suerte.
- Seguir el camino: Sigues tu camino.

### 75. La pesadora de cráneos (`skull_weigher`)

Etapa 1: Una calavera de oro resulta más ligera que tus pensamientos. La tasadora ofrece un intercambio.

- Vender un pensamiento doloroso [5 vida]: Eliminas la primera carta del mazo y obtienes 25 de oro.
- Comprar su pequeño contrapeso [40 oro]: Ganas 4 de vida máxima.
- Seguir el camino: Sigues tu camino.

### 76. La campana sumergida (`submerged_bell`)

Etapa 1: Cada golpe de campana llega antes de que la cuerda se mueva. Algo quiere enseñarte ese ritmo.

- Escuchar bajo el agua [8 vida]: Mejoras la primera carta sin mejorar.
- Recoger el cepillo de limosnas: Obtienes 23 de oro.
- Seguir el camino: Sigues tu camino.

### 77. La lectora de avalanchas (`avalanche_reader`)

Etapa 1: Lee el futuro en un montón de nieve que tiembla al ritmo de tu respiración.

- Pagar por una lectura completa [38 oro]: Ganas 3 de suerte.
- Pedir una advertencia pequeña: Recuperas 9 de vida.
- Seguir el camino: Sigues tu camino.

### 78. La nieve tibia (`warm_snow`)

Etapa 1: Un pequeño claro de nieve conserva el calor de alguien que ya no está.

- Descansar en el claro: Recuperas 14 de vida.
- Excavar bajo el calor [5 vida]: Rescatas 39 de oro.
- Seguir el camino: Sigues tu camino.

### 79. El órgano de cristal (`crystal_organ`)

Etapa 1: Le falta una tecla. El cristal exige algo que palpite para volver a sonar.

- Sustituir la tecla con tu pulso [12 vida]: Ganas 6 de vida máxima.
- Comprar una partitura incompleta [35 oro]: Obtienes una carta de tu clase o neutral.
- Seguir el camino: Sigues tu camino.

### 80. La sastre de escarcha (`frost_tailor`)

Etapa 1: Cose abrigos con la escarcha que dejan tus heridas. No necesita tomar medidas.

- Dejar que cosa tus cicatrices [24 oro]: Recuperas 24 de vida.
- Venderle una cicatriz profunda [4 vida máxima]: Obtienes 85 de oro.
- Seguir el camino: Sigues tu camino.

### 81. El centinela insomne (`sleepless_sentry`)

Etapa 1: Lleva tanto tiempo despierto que confunde el silencio con un enemigo.

- Vigilar mientras duerme: Al despertar te entrega 27 de oro.
- Comprar su manual de guardia [38 oro]: Mejoras la primera carta sin mejorar.
- Seguir el camino: Sigues tu camino.

### 82. El deshielo cautivo (`trapped_thaw`)

Etapa 1: Un deshielo diminuto intenta escapar de un bloque de hielo azul.

- Liberarlo con las manos [10 vida]: Ganas 5 de vida máxima y recuperas 8.
- Alquilar un pico al guardián [25 oro]: Obtienes 65 de oro atrapados en el hielo.
- Seguir el camino: Sigues tu camino.

### 83. El censo del invierno (`winter_census`)

Etapa 1: Una escribana pregunta cuántas veces has sobrevivido al frío. En su lista ya aparece tu respuesta.

- Corregir el registro con sangre [7 vida]: Ganas 3 de suerte.
- Comprar una copia de tu expediente [20 oro]: Recuperas 12 de vida y ganas 1 de suerte.
- Seguir el camino: Sigues tu camino.

### 84. El teatro congelado (`frozen_theatre`)

Etapa 1: Los actores están presos dentro del telón de hielo. Una taquillera te ofrece participar en el último acto.

- Comprar entrada y subir al escenario [20 oro]: El telón comienza a derretirse. La entrada no se devuelve. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: El director te concede un papel antes del desenlace.

- Interpretar al héroe herido [6 vida]: El público espera tu última decisión. Abre otra decisión en esta sala.
- Interpretar al tramoyista: Recibes un salario de 70 de oro.
- Seguir el camino: Sigues tu camino.

Etapa 3: El hielo se rompe. Puedes conservar la ovación o el recuerdo del papel.

- Guardar el recuerdo: Mejoras una carta y ganas 2 de suerte.
- Aceptar las flores del público: Recuperas 30 de vida y ganas 3 de vida máxima.
- Seguir el camino: Sigues tu camino.

### 85. La panadería de basalto (`basalt_bakery`)

Etapa 1: El panadero hornea panes tan duros que los vende con instrucciones para abrirlos.

- Comprar pan recién roto [24 oro]: Recuperas 22 de vida.
- Romper un pan de ayer [7 vida]: Dentro hay 42 de oro de un cliente olvidadizo.
- Seguir el camino: Sigues tu camino.

### 86. El molino de ceniza (`ash_mill`)

Etapa 1: Las aspas giran sin viento y muelen las promesas incumplidas de los viajeros.

- Moler la primera carta del mazo [15 oro]: Eliminas esa carta y recibes 30 de oro.
- Comprar harina medicinal [16 oro]: Recuperas 19 de vida.
- Seguir el camino: Sigues tu camino.

### 87. La óptica de obsidiana (`obsidian_optician`)

Etapa 1: Los cristales muestran el error que estás a punto de cometer, pero no cómo evitarlo.

- Comprar lentes para ver el próximo error [55 oro]: Ganas 4 de suerte.
- Probar lentes demasiado afiladas [10 vida]: Ganas 3 de suerte y recuperas 6 de vida.
- Seguir el camino: Sigues tu camino.

### 88. La partera del carbón (`coal_midwife`)

Etapa 1: Acuna una brasa que acaba de nacer. Te pide calor para enseñarle a latir.

- Prestar calor de tu cuerpo [13 vida]: Ganas 6 de vida máxima.
- Comprar una piedra tibia [27 oro]: Recuperas 25 de vida.
- Seguir el camino: Sigues tu camino.

### 89. El pescador del metal líquido (`molten_fisher`)

Etapa 1: Su sedal resiste el río de metal. La caña, en cambio, está hecha de hueso fresco.

- Comprar un turno con la caña [28 oro] (55% éxito): Pescas 110 de oro. Fallo: El sedal salpica metal: hasta 10 de daño.
- Recoger la chatarra de la orilla: Obtienes 28 de oro.
- Seguir el camino: Sigues tu camino.

### 90. El santuario de las brasas frías (`cinder_shrine`)

Etapa 1: Las brasas no queman la piel: consumen la primera idea que les acercas.

- Ofrecer tu primera carta: Eliminas esa carta y recuperas 12 de vida.
- Dejar una moneda en cada brasa [40 oro]: Ganas 3 de suerte.
- Seguir el camino: Sigues tu camino.

### 91. La feria de las salamandras (`salamander_fair`)

Etapa 1: Una fila de salamandras vende permisos para entrar en un horno lleno de tenderetes.

- Comprar un permiso de visitante [18 oro]: Te permiten hacer una sola compra. El permiso no se devuelve. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: En el interior, un herrero y una criadora compiten por tu atención.

- Pedir una lección al herrero [40 oro]: Mejoras la primera carta sin mejorar.
- Comprar el alimento de las crías [60 oro]: Ganas 5 de vida máxima y recuperas 15.
- Seguir el camino: Sigues tu camino.

### 92. El yunque agrietado (`cracked_anvil`)

Etapa 1: Un golpe preciso salvaría el yunque. Un golpe torpe podría hacer saltar la bolsa que tiene debajo.

- Intentar reparar el yunque (70% éxito): El herrero mejora una carta como agradecimiento. Fallo: El acero salta: hasta 8 de daño.
- Venderle una muestra de tu sangre [6 vida]: Obtienes 38 de oro.
- Seguir el camino: Sigues tu camino.

### 93. Los baños de azufre (`sulphur_baths`)

Etapa 1: El encargado entrega toallas que no proyectan sombra. El agua promete cerrar lo que todavía duele.

- Pagar por el baño completo [35 oro]: Recuperas 32 de vida.
- Sumergir solo las manos: Recuperas 11 de vida.
- Seguir el camino: Sigues tu camino.

### 94. El impuesto de las brasas (`ember_tax`)

Etapa 1: Un cobrador asegura que todo corazón encendido debe contribuir a mantener la caldera.

- Pagar con vitalidad [5 vida máxima]: Te devuelve un fondo de 120 de oro.
- Pagar con monedas [42 oro]: Recibes una bendición de +3 de suerte.
- Seguir el camino: Sigues tu camino.

### 95. La casa de empeños del cometa (`comet_pawnshop`)

Etapa 1: El tasador compra objetos justo antes de que existan. Ya tiene preparado un recibo con tu firma.

- Empeñar una porción de tu futuro [7 vida máxima]: Obtienes 150 de oro.
- Comprar una predicción usada [60 oro]: Ganas 4 de suerte.
- Seguir el camino: Sigues tu camino.

### 96. La apicultora del vacío (`void_beekeeper`)

Etapa 1: Sus abejas recolectan el silencio entre las estrellas. La miel no refleja ninguna luz.

- Comprar un tarro de miel oscura [40 oro]: Recuperas 35 de vida.
- Ayudar a trasladar una colmena [12 vida]: Ganas 5 de vida máxima.
- Seguir el camino: Sigues tu camino.

### 97. La biblioteca que cae (`falling_library`)

Etapa 1: Los estantes caen muy despacio hacia un cielo subterráneo. La archivera vende anclas de lectura.

- Alquilar un ancla [22 oro]: Quedas sujeto a un estante. El alquiler no se devuelve. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: Un libro prohíbe ser leído; otro prohíbe ser recordado.

- Abrir el libro prohibido [8 vida]: Llegas a una página donde puedes escribir algo tuyo. Abre otra decisión en esta sala.
- Copiar el índice seguro: Obtienes una carta de tu clase o neutral y 35 de oro.
- Seguir el camino: Sigues tu camino.

Etapa 3: La página pide una sola anotación antes de cerrarse.

- Reescribir tu técnica: Mejoras una carta y ganas 3 de suerte.
- Borrar tu primera carta: Eliminas esa carta y recuperas 30 de vida.
- Seguir el camino: Sigues tu camino.

### 98. El tuétano de la estrella (`star_marrow`)

Etapa 1: Una estrella fosilizada se ha partido y todavía conserva algo de calor en su interior.

- Beber su último calor [16 vida]: Ganas 8 de vida máxima y recuperas 20.
- Vender una lasca al minero: Obtienes 95 de oro.
- Seguir el camino: Sigues tu camino.

### 99. El pozo de la gravedad (`gravity_well`)

Etapa 1: Las monedas caen hacia arriba. Una cesta espera donde debería estar el fondo.

- Saltar hacia la cesta (55% éxito): Recoges 140 de oro. Fallo: Caes en una dirección equivocada: hasta 18 de daño.
- Recoger lo que flota cerca: Obtienes 34 de oro.
- Seguir el camino: Sigues tu camino.

### 100. El pastor de las lunas pequeñas (`moon_shepherd`)

Etapa 1: Sus ovejas son lunas del tamaño de un puño. Una de ellas se ha enredado en tu sombra.

- Devolver la luna al rebaño: El pastor te concede +3 de suerte.
- Comprar una campanilla lunar [55 oro]: Recuperas 24 de vida y ganas 3 de vida máxima.
- Seguir el camino: Sigues tu camino.

### 101. La costurera del horizonte (`horizon_stitcher`)

Etapa 1: Cierra con hilo negro las grietas entre un pasillo y el siguiente. Tu reflejo se ha quedado al otro lado.

- Dejar que cosa tu reflejo al cuerpo [75 oro]: Ganas 7 de vida máxima.
- Cederle el reflejo de una herida [4 vida máxima]: Mejoras una carta y ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 102. La tumba del cometa (`comet_grave`)

Etapa 1: Bajo la lápida duerme la piedra que trajo la noche a estos pisos. Un enterrador ofrece abrirla por última vez.

- Pagar por el legado del cometa [120 oro]: Obtienes Herradura de Plata.
- Quedarte con las ofrendas del velatorio [15 vida]: Obtienes 120 de oro.
- Seguir el camino: Sigues tu camino.

### 103. El sol olvidado (`forgotten_sun`)

Etapa 1: Un sol pequeño sigue esperando que alguien recuerde su nombre. Basta con acercar las manos para oírlo.

- Recordar su nombre en voz alta: Ganas 10 de vida máxima y recuperas 35.
- Recoger sus últimas monedas de luz: Obtienes 210 de oro.
- Seguir el camino: Sigues tu camino.

### 104. El sastre del abismo (`abyssal_tailor`)

Etapa 1: Puede remendar el hueco que ha dejado el descenso en tu cuerpo, pero necesita un hilo que solo tú tienes.

- Entregar un hilo de tu vitalidad [6 vida máxima]: Mejoras una carta y recuperas 40 de vida.
- Comprar un remiendo pequeño [35 oro]: Recuperas 28 de vida.
- Seguir el camino: Sigues tu camino.

### 105. La bolsa de los sueños (`dream_exchange`)

Etapa 1: Los corredores compran pesadillas antes de que ocurran. Una campana anuncia que todavía puedes entrar.

- Comprar una licencia de intercambio [25 oro]: La licencia solo sirve para esta visita y no se devuelve. Abre otra decisión en esta sala.
- Seguir el camino: Sigues tu camino.

Etapa 2: Un corredor quiere tu sueño más antiguo; otro vende uno que aún está vacío.

- Vender tu sueño antiguo [10 vida]: Recibes 100 de oro, pero todavía debes elegir cómo gastarlos. Abre otra decisión en esta sala.
- Comprar el sueño vacío [80 oro]: Ganas 7 de vida máxima y recuperas 18.
- Seguir el camino: Sigues tu camino.

Etapa 3: El corredor exige una decisión final antes de cerrar tu cuenta.

- Comprar una técnica soñada [65 oro]: Mejoras una carta y ganas 3 de suerte.
- Comprar un despertar tranquilo [40 oro]: Recuperas 42 de vida.
- Seguir el camino: Sigues tu camino.

### 106. La mesa del dios muerto (`dead_god_table`)

Etapa 1: El plato sigue lleno aunque su dueño murió antes de que existiera la mazmorra. Queda sitio para una última persona.

- Probar el alimento imposible [24 vida]: Ganas 12 de vida máxima y 4 de suerte.
- Vender los cubiertos al sacristán: Obtienes 180 de oro.
- Seguir el camino: Sigues tu camino.

### 107. La lluvia que asciende (`upside_down_rain`)

Etapa 1: Las gotas suben desde el suelo y arrastran con ellas el cansancio de quienes pasan.

- Permanecer bajo la lluvia: Recuperas 20 de vida.
- Buscar monedas entre las gotas [8 vida]: Encuentras 55 de oro.
- Seguir el camino: Sigues tu camino.

### 108. El peaje de lo eterno (`eternal_toll`)

Etapa 1: El cobrador ya recibió monedas de todas tus posibles vidas. Solo falta la que estás viviendo ahora.

- Ceder una parte de esta vida [8 vida máxima]: Obtienes 180 de oro y +2 de suerte.
- Comprar un recibo de otra vida [65 oro]: Ganas 4 de suerte.
- Seguir el camino: Sigues tu camino.

### 109. El faro sin costa (`last_lighthouse`)

Etapa 1: Su guardián ilumina pasillos que ya no existen. Te ofrece descansar mientras busca el tuyo.

- Comprar aceite para el faro [50 oro]: Recuperas 38 de vida y ganas 2 de suerte.
- Mantener la lámpara encendida [7 vida]: Recibes 48 de oro.
- Seguir el camino: Sigues tu camino.

### 110. El devorador de deseos (`wish_eater`)

Etapa 1: Una criatura sin boca pide el deseo más pequeño que te quede. Promete no tocar los demás.

- Entregar un deseo de vivir [7 vida máxima]: Ganas 6 de suerte y mejoras una carta.
- Alimentarlo con monedas [55 oro]: Recuperas 40 de vida.
- Seguir el camino: Sigues tu camino.

### 111. La llave transparente (`glass_key`)

Etapa 1: Una llave transparente solo se ve sobre la palma de tu mano. Su diente apunta hacia abajo cuando oye una cerradura.

- Guardar la llave: La llave reconoce tu calor y espera una cerradura de cristal. Marca: `glass_key_kept` → `glass_lock`.
- Venderla al buscador de vidrio: Obtienes 28 de oro.
- Seguir el camino: Sigues tu camino.

### 112. La cerradura sin puerta (`glass_lock`)

Etapa 1: La llave vibra ante una cerradura suspendida en el aire. Al otro lado se oye una escalera que está siendo construida.

- Abrir y dejar una ofrenda [22 oro]: La cerradura se disuelve y revela el comienzo de una escalera. Marca: `glass_lock_opened` → `glass_stair`.
- Cambiar la llave por un remedio: Recuperas 24 de vida y la escalera permanece oculta.
- Seguir el camino: Sigues tu camino.

### 113. La escalera de las grietas (`glass_stair`)

Etapa 1: Los escalones transparentes se dibujan al escuchar tus pasos. La última grieta necesita algo que la mantenga unida.

- Sellar la grieta con sangre [9 vida]: Una huella roja queda suspendida. El santuario podrá reconocerte. Marca: `glass_stair_mended` → `glass_sanctum`.
- Vender los escalones sueltos: Obtienes 75 de oro y abandonas el ascenso.
- Seguir el camino: Sigues tu camino.

### 114. El santuario que deja ver (`glass_sanctum`)

Etapa 1: Desde el santuario ves todas las puertas que decidiste no abrir. Su guardián te concede un recuerdo de las que sí cruzaste.

- Conservar la claridad: Ganas 5 de suerte y mejoras una carta.
- Pedir un cuerpo menos frágil: Ganas 8 de vida máxima y recuperas 28.
- Seguir el camino: Sigues tu camino.

### 115. El violín hueco (`hollow_violin`)

Etapa 1: El estuche conserva el nombre de una intérprete enterrada muy abajo. Las cuerdas se rompen si intentas tocarlo.

- Llevar el instrumento a reparar: Guardas el violín sin tocarlo. Una remendadora podría salvar sus cuerdas. Marca: `violin_carried` → `string_mender`.
- Vender el estuche: Obtienes 20 de oro.
- Seguir el camino: Sigues tu camino.

### 116. La remendadora de cuerdas (`string_mender`)

Etapa 1: La remendadora reconoce el instrumento. La última intérprete dejó pagada la mano de obra, pero falta el material.

- Pagar por nuevas cuerdas [24 oro]: El violín vuelve a sonar y reclama un escenario bajo tierra. Marca: `violin_repaired` → `buried_concert`.
- Aceptar monedas por el instrumento: Obtienes 48 de oro.
- Seguir el camino: Sigues tu camino.

### 117. El concierto enterrado (`buried_concert`)

Etapa 1: Las tumbas forman una platea. La partitura indica que la última nota debe tocarla alguien que siga vivo.

- Tocar para los ausentes [8 vida]: El arco te abre una herida, pero una voz te enseña el final de la melodía. Marca: `concert_played` → `final_note`.
- Cobrar por devolver el violín: Obtienes 80 de oro y dejas el instrumento junto al escenario.
- Seguir el camino: Sigues tu camino.

### 118. La nota que no termina (`final_note`)

Etapa 1: La intérprete te espera junto a una única nota suspendida. Te pide decidir qué debe recordar el mundo de su canción.

- Recordar la técnica de sus manos: Mejoras una carta y obtienes otra de tu clase o neutral.
- Recordar su serenidad: Recuperas 36 de vida y ganas 4 de suerte.
- Seguir el camino: Sigues tu camino.

### 119. La brasa enjaulada (`caged_ember`)

Etapa 1: Un pájaro de carbón picotea los barrotes de una jaula apagada. Le faltan siglos para aprender a arder.

- Comprar y llevar la jaula [18 oro]: El pájaro se duerme junto a tu pulso. Necesitará un nido más caliente. Marca: `ember_adopted` → `ember_nursery`.
- Comprar solo la ceniza del fondo [10 oro]: Recuperas 13 de vida.
- Seguir el camino: Sigues tu camino.

### 120. La guardería de las brasas (`ember_nursery`)

Etapa 1: La cuidadora deposita tu pájaro entre brasas recién nacidas. Para que despierte necesita reconocer una vida prestada.

- Darle parte de tu calor [10 vida]: La brasa abre sus alas. Su primer vuelo buscará un horno que camine. Marca: `ember_awakened` → `walking_furnace`.
- Dejarlo al cuidado de la guardería: La cuidadora te paga 65 de oro y conserva al pájaro.
- Seguir el camino: Sigues tu camino.

### 121. El horno caminante (`walking_furnace`)

Etapa 1: Un horno con patas se detiene ante tu pájaro. Puede enseñarle a renacer si aceptas sostener su fuego.

- Prometer una parte de tu vitalidad [5 vida máxima]: El horno te entrega una cuna de ceniza para el próximo renacimiento. Marca: `phoenix_promised` → `phoenix_cradle`.
- Vender el pájaro al fogonero: Obtienes 125 de oro y termina su viaje contigo.
- Seguir el camino: Sigues tu camino.

### 122. La cuna del fénix pequeño (`phoenix_cradle`)

Etapa 1: La cuna se abre cuando tu pájaro se convierte en ceniza. De ella nace una llama que reconoce todo lo que entregaste.

- Dejar que la llama te restaure: Ganas 13 de vida máxima y recuperas 40.
- Guardar una pluma de su primer vuelo: Ganas 6 de suerte y 140 de oro.
- Seguir el camino: Sigues tu camino.

### 123. El pasaporte de sal (`salt_passport`)

Etapa 1: Una viajera se deshace lentamente entre sus documentos. Te pide llevar su pasaporte a una embajada que sigue abierta bajo el agua.

- Guardar el pasaporte seco: Prometes proteger el sello hasta encontrar la aduana. Marca: `salt_passport_kept` → `customs_of_ash`.
- Comprar su bolsa de provisiones [12 oro]: Recuperas 15 de vida.
- Seguir el camino: Sigues tu camino.

### 124. La aduana de las cenizas (`customs_of_ash`)

Etapa 1: El inspector reconoce el sello de sal, pero exige pagar los años de retraso de su propietaria.

- Pagar el visado atrasado [26 oro]: El inspector estampa una ruta hacia la embajada sumergida. Marca: `salt_visa_paid` → `sunken_embassy`.
- Vender el documento al inspector: Obtienes 50 de oro.
- Seguir el camino: Sigues tu camino.

### 125. La embajada sumergida (`sunken_embassy`)

Etapa 1: Las habitaciones están llenas de agua que no moja el pasaporte. La embajadora pide un último mensajero para llamar a sus exiliados.

- Prestar tu aliento al mensaje [9 vida]: Una llamada atraviesa el agua. Los exiliados sabrán dónde reunirse. Marca: `exiles_called` → `exile_return`.
- Cobrar por devolver el pasaporte: Recibes 82 de oro y abandonas la embajada.
- Seguir el camino: Sigues tu camino.

### 126. El regreso de los exiliados (`exile_return`)

Etapa 1: La viajera vuelve a tener cuerpo. A su alrededor, los exiliados preparan el primer equipaje que no tendrán que abandonar.

- Aceptar el regalo de la embajadora: Obtienes una carta de tu clase o neutral y 110 de oro.
- Pedir descansar entre los retornados: Ganas 7 de vida máxima y recuperas 30.
- Seguir el camino: Sigues tu camino.

### 127. La hora prestada (`borrowed_hour`)

Etapa 1: Un reloj sin agujas ofrece prestarte una hora que pertenece a otra persona. Su dueño quiere que se la devuelvas entera.

- Aceptar la hora prestada [20 oro]: El reloj guarda tu promesa. La hora encerrada te estará esperando más abajo. Marca: `hour_borrowed` → `minute_prison`.
- Comprar un segundo suelto [24 oro]: Ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 128. La prisión de los minutos (`minute_prison`)

Etapa 1: Los minutos caminan en círculos dentro de celdas pequeñas. Tu hora prestada está repartida entre todos ellos.

- Comprar la libertad de los minutos [38 oro]: Los minutos vuelven a juntarse y revelan el corazón de su reloj. Marca: `minutes_freed` → `clock_heart`.
- Cambiar la promesa por una indemnización: Obtienes 85 de oro y renuncias a reunir la hora.
- Seguir el camino: Sigues tu camino.

### 129. El corazón del reloj (`clock_heart`)

Etapa 1: El corazón ha olvidado cómo empezar una hora nueva. Solo recuerda cómo alargar una vieja.

- Cederle un poco de tu tiempo vital [6 vida máxima]: El corazón vuelve a latir. El primer amanecer de la mazmorra podrá encontrarte. Marca: `clock_restarted` → `dawn_debt`.
- Vender sus engranajes: Obtienes 140 de oro.
- Seguir el camino: Sigues tu camino.

### 130. La deuda del amanecer (`dawn_debt`)

Etapa 1: La hora prestada termina y, por primera vez, el corredor amanece. El reloj te devuelve el tiempo que no gastaste en huir.

- Recuperar el tiempo con vida: Ganas 14 de vida máxima y recuperas 45.
- Aprender de las horas que no viviste: Mejoras una carta, ganas 5 de suerte y 100 de oro.
- Seguir el camino: Sigues tu camino.

### 131. La piedra que recuerda (`stone_memory`)

Etapa 1: Una piedra te llama por el nombre que tuviste antes de nacer. Su cantera conserva otros recuerdos del mismo sueño.

- Llevar la piedra contigo: La piedra se calienta cuando miras hacia los pisos más profundos. Marca: `stone_memory_kept` → `memory_quarry`.
- Venderla como pisapapeles: Obtienes 40 de oro.
- Seguir el camino: Sigues tu camino.

### 132. La cantera de los recuerdos (`memory_quarry`)

Etapa 1: Los canteros extraen recuerdos de un muro que late. Tu piedra encaja en el hueco de un rostro dormido.

- Pagar para reconstruir el rostro [35 oro]: El rostro recuerda una montaña que aún no ha despertado. Marca: `mountain_face_restored` → `sleeping_mountain`.
- Cambiar la piedra por otra menos pesada: Recuperas 28 de vida y abandonas su recuerdo.
- Seguir el camino: Sigues tu camino.

### 133. La montaña dormida (`sleeping_mountain`)

Etapa 1: La montaña cabe dentro de una cámara y sueña con el cielo. Su rostro te reconoce, pero necesita un latido para despertar.

- Prestar tu pulso a la piedra [16 vida]: La cámara tiembla. La montaña te citará cuando recuerde su nombre. Marca: `mountain_awakened` → `mountain_verdict`.
- Recoger las monedas de sus sueños: Obtienes 120 de oro sin despertarla.
- Seguir el camino: Sigues tu camino.

### 134. El veredicto de la montaña (`mountain_verdict`)

Etapa 1: La montaña abre los ojos y reconoce a quien sostuvo sus recuerdos durante el descenso. No ofrece tesoros: ofrece peso para seguir existiendo.

- Aceptar la paciencia de la piedra: Ganas 12 de vida máxima y recuperas 38.
- Aceptar el filo del recuerdo: Mejoras una carta y ganas 6 de suerte.
- Seguir el camino: Sigues tu camino.

### 135. El huevo de cobre (`copper_egg`)

Etapa 1: Un huevo de cobre golpea desde dentro. Lleva grabada la dirección de una incubadora que ya no figura en ningún plano.

- Llevar el huevo a la incubadora: El huevo aprende el ritmo de tus pasos. Marca: `copper_egg_carried` → `brass_incubator`.
- Venderlo por su metal: Obtienes 24 de oro.
- Seguir el camino: Sigues tu camino.

### 136. La incubadora de latón (`brass_incubator`)

Etapa 1: La cuidadora explica que el huevo puede contener un pájaro o una campana. Lo que nazca dependerá de lo que le ofrezcas.

- Ofrecer calor para que nazca un pájaro [6 vida]: El huevo empieza a piar. El polluelo te buscará cuando termine de crecer. Marca: `copper_chick_chosen` → `clockwork_chick`.
- Ofrecer monedas para que nazca una campana [24 oro]: El huevo canta una sola nota. La campana resonará más abajo. Marca: `copper_bell_chosen` → `silent_bell`.
- Seguir el camino: Sigues tu camino.

### 137. El polluelo de cuerda (`clockwork_chick`)

Etapa 1: El polluelo te reconoce entre todos los viajeros. Lleva en el pico una pieza que podría reparar tu corazón.

- Aceptar su pequeño regalo: Ganas 6 de vida máxima y recuperas 18.
- Aprender el movimiento de sus alas: Mejoras una carta y recibes 25 de oro.
- Seguir el camino: Sigues tu camino.

### 138. La campana sin sonido (`silent_bell`)

Etapa 1: La campana no produce sonido, pero cada vibración hace caer una moneda de un bolsillo que no es el tuyo.

- Dejarla sonar una vez para ti: Obtienes 105 de oro.
- Escuchar lo que calla: Ganas 4 de suerte y recuperas 12 de vida.
- Seguir el camino: Sigues tu camino.

### 139. El nombre robado (`stolen_name`)

Etapa 1: Una placa de identificación pronuncia tu nombre antes de que puedas leerla. La persona que la perdió ya no recuerda cómo llamarse.

- Prometer devolver el nombre: La placa apunta hacia un registro donde se guardan las identidades perdidas. Marca: `stolen_name_kept` → `name_registry`.
- Vender la placa sin leer el reverso: Obtienes 30 de oro.
- Seguir el camino: Sigues tu camino.

### 140. El registro de las identidades (`name_registry`)

Etapa 1: La funcionaria puede devolver el nombre a su dueño o inscribirlo sobre una corona vacante. Solo puede usarlo una vez.

- Pagar para devolver el nombre [28 oro]: La funcionaria localiza al dueño y prepara vuestro encuentro. Marca: `name_return_chosen` → `returned_name`.
- Reclamar la corona a tu nombre [8 vida]: La corona exige reconocer tu sangre antes de elegirte. Marca: `name_crown_chosen` → `borrowed_crown`.
- Seguir el camino: Sigues tu camino.

### 141. La persona que vuelve a llamarse (`returned_name`)

Etapa 1: Al pronunciar de nuevo su nombre, la persona olvidada recuerda dónde escondió todo lo que tenía.

- Aceptar parte de sus ahorros: Obtienes 110 de oro y una carta de tu clase o neutral.
- Pedir que recuerde una bendición: Ganas 5 de vida máxima y 3 de suerte.
- Seguir el camino: Sigues tu camino.

### 142. La corona de un nombre prestado (`borrowed_crown`)

Etapa 1: La corona te llama por el nombre que elegiste tomar. A cambio de obedecerla una vez, ofrece reconocer una de tus técnicas como ley.

- Promulgar una técnica: Mejoras una carta y ganas 4 de suerte.
- Vender el derecho al trono: Obtienes 150 de oro y abandonas la corona.
- Seguir el camino: Sigues tu camino.

### 143. El mapa de la cartógrafa ciega (`blind_map`)

Etapa 1: La cartógrafa dibujó dos destinos sobre el mismo punto. Necesita que alguien consulte una brújula capaz de reconocer las mentiras.

- Llevar el mapa hasta la brújula: Guardas el mapa con sus dos destinos todavía superpuestos. Marca: `blind_map_kept` → `compass_trial`.
- Comprar solo el margen ilustrado [8 oro]: Ganas 1 de suerte.
- Seguir el camino: Sigues tu camino.

### 144. La prueba de la brújula (`compass_trial`)

Etapa 1: La aguja señala un refugio verdadero; el contrapeso señala un tesoro que solo existe mientras alguien crea en él.

- Elegir el norte verdadero [18 oro]: La brújula conserva la ruta al refugio. Marca: `true_north_chosen` → `true_north`.
- Elegir el tesoro imposible [6 vida]: El mapa exige una gota de sangre para sostener su mentira. Marca: `treasure_lie_chosen` → `treasure_lie`.
- Seguir el camino: Sigues tu camino.

### 145. El refugio del norte verdadero (`true_north`)

Etapa 1: El refugio está exactamente donde prometió la cartógrafa. La mesa lleva años esperando a alguien que creyera en una ruta honesta.

- Descansar sin prisa: Recuperas 36 de vida y ganas 5 de vida máxima.
- Estudiar el manual del guardián: Mejoras una carta y ganas 2 de suerte.
- Seguir el camino: Sigues tu camino.

### 146. El tesoro de la mentira (`treasure_lie`)

Etapa 1: El cofre existe mientras lo observas. Al apartar la mirada, las monedas empiezan a olvidar que son de oro.

- Agarrar las monedas sin parpadear (70% éxito): Rescatas 155 de oro antes de que la mentira termine. Fallo: La ilusión se rompe y te corta: hasta 12 de daño.
- Conservar una mentira pequeña: Ganas 4 de suerte.
- Seguir el camino: Sigues tu camino.

### 147. La sombra huérfana (`orphan_shadow`)

Etapa 1: Una sombra pequeña intenta seguir tus pasos sin encontrar un cuerpo al que pertenecer. Te muestra el camino hacia un tribunal oscuro.

- Acompañar a la sombra: La sombra aprende a caminar a tu lado. Marca: `orphan_shadow_kept` → `shadow_trial`.
- Comprar una vela para despedirla [16 oro]: Recuperas 15 de vida.
- Seguir el camino: Sigues tu camino.

### 148. El tribunal de las sombras (`shadow_trial`)

Etapa 1: El tribunal puede devolver la sombra a su cuerpo perdido o declararla heredera de una noche sin dueño.

- Financiar la búsqueda de su cuerpo [32 oro]: El tribunal localiza a una persona que lleva años sin proyectar sombra. Marca: `shadow_reunion_chosen` → `shadow_reunion`.
- Apadrinar su corona de oscuridad [4 vida máxima]: Tu pulso sostiene la promesa de una noche nueva. Marca: `shadow_crown_chosen` → `shadow_crown`.
- Seguir el camino: Sigues tu camino.

### 149. El abrazo de dos siluetas (`shadow_reunion`)

Etapa 1: La sombra y su cuerpo vuelven a moverse juntos. La persona recuperada conserva un regalo para quien no dejó sola a su mitad perdida.

- Aceptar un refugio junto a ambos: Ganas 7 de vida máxima y recuperas 32.
- Aceptar su pequeño legado: Obtienes 95 de oro y ganas 3 de suerte.
- Seguir el camino: Sigues tu camino.

### 150. La reina de la noche pequeña (`shadow_crown`)

Etapa 1: La sombra huérfana gobierna ahora una noche del tamaño de una habitación. Te reconoce como la primera persona que la trató como alguien.

- Pedir la memoria de su ascenso: Mejoras una carta y ganas 5 de suerte.
- Pedir que proteja tu latido: Ganas 11 de vida máxima y recuperas 25.
- Seguir el camino: Sigues tu camino.

