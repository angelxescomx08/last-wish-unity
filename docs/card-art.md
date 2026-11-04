# Ilustraciones animadas de las cartas

> Generado por `Tools/VfxLab/card_art_status.py` a partir de `Assets/Resources/Game/Data/cards.json` y `Assets/Resources/Game/Cards/Anim/anim.json`. No editar a mano: tras exportar animaciones nuevas con `python Tools/VfxLab/export_game_card_art.py`, ejecutar `python Tools/VfxLab/card_art_status.py` (`--check` comprueba que está al día).

Cada ilustración animada mide 88×64 a 12 fps (24 fotogramas, bucle sin costura) con capa emisiva y sigue `docs/art-style.md`. Se hacen por orden: primero las cartas iniciales de cada héroe, luego las del botín. Las que no tienen animación muestran su ilustración fija (`Cards/Art`).

## Resumen

| Clase | Animadas | Iniciales animadas |
|---|---|---|
| Guerrera | 3 / 50 | 3 / 3 |
| Mago | 3 / 45 | 3 / 3 |
| Pícara | 66 / 66 | 3 / 3 |
| Neutral | 0 / 13 | — |
| Estados y fichas | 0 / 4 | — |
| **Total** | **72 / 178** | |

Ilustración fija: 178 / 178 cartas.

## Guerrera (3 / 50 animadas)

| # | Carta | Id | Tipo | Rareza | Origen | Animada |
|---|---|---|---|---|---|---|
| 1 | Bloqueo | `bloqueo_base` | Habilidad | Común | Inicial | ✅ |
| 2 | Espadazo | `espadazo_base` | Ataque | Común | Inicial | ✅ |
| 3 | Guardia Férrea | `guardia_ferrea_base` | Habilidad | Común | Inicial | ✅ |
| 4 | Corte Sangriento | `wr_corte_sangriento` | Ataque | Común | Acero | — |
| 5 | Embate Firme | `wr_embate_firme` | Ataque | Común | Acero | — |
| 6 | Escudo de Sangre | `wr_escudo_sangre` | Habilidad | Común | Escudo | — |
| 7 | Golpe Doble | `wr_golpe_doble` | Ataque | Común | Acero | — |
| 8 | Herida Abierta | `wr_herida_abierta` | Habilidad | Común | Magia | — |
| 9 | Muro de Acero | `wr_muro_acero` | Habilidad | Común | Escudo | — |
| 10 | Postura Firme | `wr_postura_firme` | Habilidad | Común | Escudo | — |
| 11 | Tajo Brutal | `wr_tajo_brutal` | Ataque | Común | Acero | — |
| 12 | Vendaje | `wr_vendaje` | Habilidad | Común | Magia | — |
| 13 | Fortaleza Interior | `wr_fortaleza_interior` | Poder | Poco común | Magia | — |
| 14 | Grito de Batalla | `wr_grito_batalla` | Habilidad | Poco común | Magia | — |
| 15 | Grito de Guerra | `m_grito_de_guerra` | Habilidad | Poco común | Magia | — |
| 16 | Hachazo Temerario | `wr_hachazo_temerario` | Ataque | Poco común | Acero | — |
| 17 | Puño de Hierro | `wr_puno_hierro` | Ataque | Poco común | Acero | — |
| 18 | Respiro | `wr_respiro` | Habilidad | Poco común | Escudo | — |
| 19 | Rugido | `wr_rugido` | Habilidad | Poco común | Magia | — |
| 20 | Sangre Hirviente | `wr_sangre_hirviente` | Poder | Poco común | Magia | — |
| 21 | Sangría | `wr_sangria` | Habilidad | Poco común | Magia | — |
| 22 | Sed de Batalla | `wr_sed_batalla` | Ataque | Poco común | Acero | — |
| 23 | Conjuro de Combate | `m_conjuro` | Ataque | Rara | Magia | — |
| 24 | Coraza Viva | `wr_coraza_viva` | Habilidad | Rara | Escudo | — |
| 25 | Golpe Pesado | `a_golpe_pesado` | Ataque | Rara | Acero | — |
| 26 | Golpe Total | `a_golpe_total` | Ataque | Rara | Acero | — |
| 27 | Golpe de Escudo | `wr_golpe_escudo` | Ataque | Rara | Acero | — |
| 28 | Martillo Sísmico | `wr_martillo_sismico` | Ataque | Rara | Acero | — |
| 29 | Pacto de Hierro | `wr_pacto_hierro` | Habilidad | Rara | Magia | — |
| 30 | Regeneración | `wr_regeneracion` | Poder | Rara | Magia | — |
| 31 | Represalia | `wr_represalia` | Ataque | Rara | Acero | — |
| 32 | Sacrificio | `wr_sacrificio` | Ataque | Rara | Acero | — |
| 33 | Segunda Piel | `wr_segunda_piel` | Poder | Rara | Escudo | — |
| 34 | Torbellino de Acero | `wr_torbellino_acero` | Ataque | Rara | Acero | — |
| 35 | Torre | `e_torre` | Habilidad | Rara | Escudo | — |
| 36 | Transfusión | `wr_transfusion` | Habilidad | Rara | Magia | — |
| 37 | Baluarte | `e_baluarte` | Habilidad | Épica | Escudo | — |
| 38 | Furia Desatada | `wr_furia_desatada` | Ataque | Épica | Épico | — |
| 39 | Gran Golpe | `a_gran_golpe` | Ataque | Épica | Acero | — |
| 40 | Gran Muralla | `e_gran_muralla` | Habilidad | Épica | Escudo | — |
| 41 | Ira Carmesí | `wr_ira_carmesi` | Ataque | Épica | Épico | — |
| 42 | Piel de Acero | `wr_piel_acero` | Poder | Épica | Épico | — |
| 43 | Bastión | `ep_bastion` | Habilidad | Legendaria | Épico | — |
| 44 | Berserker | `wr_berserker` | Poder | Legendaria | Épico | — |
| 45 | Doblar Escudo | `wr_doblar_escudo` | Habilidad | Legendaria | Épico | — |
| 46 | Ejecución | `ep_ejecucion` | Ataque | Legendaria | Épico | — |
| 47 | Escudo Impenetrable | `ep_escudo_impenet` | Habilidad | Legendaria | Épico | — |
| 48 | Golpe Vampírico | `wr_golpe_vampirico` | Ataque | Legendaria | Épico | — |
| 49 | Muralla Infranqueable | `wr_muralla_infranqueable` | Habilidad | Legendaria | Épico | — |
| 50 | Voluntad de Hierro | `wr_voluntad_hierro` | Poder | Legendaria | Épico | — |

## Mago (3 / 45 animadas)

| # | Carta | Id | Tipo | Rareza | Origen | Animada |
|---|---|---|---|---|---|---|
| 1 | Chispa Ígnea | `chispa_ignea_base` | Ataque | Común | Inicial | ✅ |
| 2 | Proyectil Arcano | `proyectil_base` | Ataque | Común | Inicial | ✅ |
| 3 | Égida | `egida_base` | Habilidad | Común | Inicial | ✅ |
| 4 | Bola de Fuego | `mg_bola_fuego` | Ataque | Común | Acero | — |
| 5 | Brisa | `mg_brisa` | Habilidad | Común | Magia | — |
| 6 | Chispa | `m_chispa` | Ataque | Común | Magia | — |
| 7 | Chispa Arcana | `mg_chispa_arcana` | Ataque | Común | Acero | — |
| 8 | Gota | `mg_gota` | Ataque | Común | Acero | — |
| 9 | Grieta | `mg_grieta` | Ataque | Común | Acero | — |
| 10 | Lengua de Fuego | `mg_lengua_fuego` | Ataque | Común | Acero | — |
| 11 | Marea | `mg_marea` | Habilidad | Común | Escudo | — |
| 12 | Muro de Roca | `mg_muro_roca` | Habilidad | Común | Escudo | — |
| 13 | Piedra | `mg_piedra` | Ataque | Común | Acero | — |
| 14 | Ráfaga | `mg_rafaga` | Ataque | Común | Acero | — |
| 15 | Visión del Caos | `m_vision_del_caos` | Habilidad | Común | Magia | — |
| 16 | Burbuja | `mg_burbuja` | Habilidad | Poco común | Escudo | — |
| 17 | Ciclón | `mg_ciclon` | Ataque | Poco común | Acero | — |
| 18 | Concentración | `m_concentracion` | Habilidad | Poco común | Magia | — |
| 19 | Hechizo Menor | `m_hechizo_menor` | Ataque | Poco común | Magia | — |
| 20 | Llamarada | `mg_llamarada` | Ataque | Poco común | Acero | — |
| 21 | Muro de Maná | `e_muro_de_mana` | Habilidad | Poco común | Escudo | — |
| 22 | Ola | `mg_ola` | Ataque | Poco común | Acero | — |
| 23 | Piel de Granito | `mg_piel_granito` | Poder | Poco común | Escudo | — |
| 24 | Vapor Hirviente | `mg_vapor_hirviente` | Ataque | Poco común | Acero | — |
| 25 | Arremetida | `a_arremetida` | Ataque | Rara | Acero | — |
| 26 | Avivar | `mg_avivar` | Habilidad | Rara | Magia | — |
| 27 | Barrera Mágica | `m_barrera_magica` | Habilidad | Rara | Magia | — |
| 28 | Capa de Hierro | `e_capa_hierro` | Habilidad | Rara | Escudo | — |
| 29 | Corriente Ascendente | `mg_corriente` | Habilidad | Rara | Magia | — |
| 30 | Diluvio | `mg_diluvio` | Ataque | Rara | Acero | — |
| 31 | Embestida | `a_embestida` | Ataque | Rara | Acero | — |
| 32 | Ojo del Huracán | `mg_ojo_huracan` | Poder | Rara | Magia | — |
| 33 | Rayo | `m_rayo` | Ataque | Rara | Magia | — |
| 34 | Remolino | `mg_remolino` | Habilidad | Rara | Magia | — |
| 35 | Terremoto | `mg_terremoto` | Ataque | Rara | Acero | — |
| 36 | Avalancha | `mg_avalancha` | Ataque | Épica | Épico | — |
| 37 | Catalizador | `mg_catalizador` | Habilidad | Épica | Magia | — |
| 38 | Combustión | `mg_combustion` | Ataque | Épica | Épico | — |
| 39 | Corazón Ígneo | `mg_corazon_igneo` | Poder | Épica | Épico | — |
| 40 | Furia | `a_furia` | Ataque | Épica | Acero | — |
| 41 | Archimago | `mg_archimago` | Poder | Legendaria | Épico | — |
| 42 | Convergencia | `mg_convergencia` | Ataque | Legendaria | Épico | — |
| 43 | Descarga | `ep_descarga` | Ataque | Legendaria | Épico | — |
| 44 | Escudo Arcano | `ep_escudo_arcano` | Habilidad | Legendaria | Épico | — |
| 45 | Tormenta | `ep_tormenta` | Ataque | Legendaria | Épico | — |

## Pícara (66 / 66 animadas)

| # | Carta | Id | Tipo | Rareza | Origen | Animada |
|---|---|---|---|---|---|---|
| 1 | Esquiva | `esquiva_base` | Habilidad | Común | Inicial | ✅ |
| 2 | Finta | `finta_base` | Ataque | Común | Inicial | ✅ |
| 3 | Puñalada | `punalada_base` | Ataque | Común | Inicial | ✅ |
| 4 | Abrir la Guardia | `r_abrir_la_guardia` | Ataque | Común | Acero | ✅ |
| 5 | Afilar | `r_afilar` | Habilidad | Común | Magia | ✅ |
| 6 | Carterista | `r_carterista` | Habilidad | Común | Magia | ✅ |
| 7 | Chatarra | `r_chatarra` | Habilidad | Común | Magia | ✅ |
| 8 | Corte y Guardia | `r_corte_y_guardia` | Ataque | Común | Acero | ✅ |
| 9 | Cuchillada Errante | `r_cuchillada_errante` | Ataque | Común | Acero | ✅ |
| 10 | Deshacerse | `r_deshacerse` | Habilidad | Común | Escudo | ✅ |
| 11 | Finta Doble | `r_finta_doble` | Habilidad | Común | Escudo | ✅ |
| 12 | Lanzar la Daga | `r_lanzar_la_daga` | Ataque | Común | Acero | ✅ |
| 13 | Lluvia de Dagas | `r_lluvia_de_dagas` | Ataque | Común | Acero | ✅ |
| 14 | Marcar Objetivo | `r_marcar_objetivo` | Habilidad | Común | Magia | ✅ |
| 15 | Paso Atrás | `r_paso_atras` | Habilidad | Común | Escudo | ✅ |
| 16 | Pinchazo | `r_pinchazo` | Ataque | Común | Acero | ✅ |
| 17 | Quiebro | `r_quiebro` | Habilidad | Común | Escudo | ✅ |
| 18 | Rodar | `r_rodar` | Habilidad | Común | Escudo | ✅ |
| 19 | Tajo Veloz | `r_tajo_veloz` | Ataque | Común | Acero | ✅ |
| 20 | Tirar y Cortar | `r_tirar_y_cortar` | Ataque | Común | Acero | ✅ |
| 21 | Agilidad | `e_agilidad` | Habilidad | Poco común | Escudo | ✅ |
| 22 | Contraataque | `r_contraataque` | Habilidad | Poco común | Escudo | ✅ |
| 23 | Corte Rápido | `a_corte_rapido` | Ataque | Poco común | Acero | ✅ |
| 24 | Escudo Reactivo | `e_escudo_reactivo` | Habilidad | Poco común | Escudo | ✅ |
| 25 | Golpe Desesperado | `r_golpe_desesperado` | Ataque | Poco común | Acero | ✅ |
| 26 | Golpe de Gracia | `r_golpe_de_gracia` | Ataque | Poco común | Acero | ✅ |
| 27 | Guardia Evasiva | `r_guardia_evasiva` | Habilidad | Poco común | Escudo | ✅ |
| 28 | Hoja Envenenada | `r_hoja_envenenada` | Habilidad | Poco común | Magia | ✅ |
| 29 | Instinto | `a_instinto` | Ataque | Poco común | Acero | ✅ |
| 30 | Juego de Manos | `r_juego_de_manos` | Habilidad | Poco común | Magia | ✅ |
| 31 | Manto Raído | `r_manto_raido` | Habilidad | Poco común | Escudo | ✅ |
| 32 | Muro de Humo | `r_muro_de_humo` | Habilidad | Poco común | Escudo | ✅ |
| 33 | Puñalada Trapera | `r_punalada_trapera` | Ataque | Poco común | Acero | ✅ |
| 34 | Reflejos | `r_reflejos` | Poder | Poco común | Magia | ✅ |
| 35 | Señuelo | `r_senuelo` | Habilidad | Poco común | Escudo | ✅ |
| 36 | Sombra Esquiva | `r_sombra_esquiva` | Habilidad | Poco común | Escudo | ✅ |
| 37 | Tajo | `a_tajo` | Ataque | Poco común | Acero | ✅ |
| 38 | Veneno | `m_veneno` | Habilidad | Poco común | Magia | ✅ |
| 39 | Capa de Sombras | `r_capa_de_sombras` | Habilidad | Rara | Escudo | ✅ |
| 40 | Corte Afortunado | `r_corte_afortunado` | Ataque | Rara | Acero | ✅ |
| 41 | Dagas Ocultas | `r_dagas_ocultas` | Habilidad | Rara | Magia | ✅ |
| 42 | Hoja Única | `r_hoja_unica` | Ataque | Rara | Acero | ✅ |
| 43 | Rebuscar | `r_rebuscar` | Habilidad | Rara | Magia | ✅ |
| 44 | Ráfaga de Cortes | `r_rafaga_de_cortes` | Ataque | Rara | Acero | ✅ |
| 45 | Tirar los Dados | `r_tirar_los_dados` | Habilidad | Rara | Magia | ✅ |
| 46 | Torbellino | `m_torbellino` | Ataque | Rara | Magia | ✅ |
| 47 | Vaciar Bolsillos | `r_vaciar_bolsillos` | Habilidad | Rara | Magia | ✅ |
| 48 | Abanico de Cuchillas | `r_abanico_de_cuchillas` | Ataque | Épica | Acero | ✅ |
| 49 | Astucia | `r_astucia` | Habilidad | Épica | Magia | ✅ |
| 50 | Danza de Sombras | `r_danza_de_sombras` | Poder | Épica | Magia | ✅ |
| 51 | Estilo Propio | `r_estilo_propio` | Habilidad | Épica | Escudo | ✅ |
| 52 | Estocada Oportuna | `r_estocada_oportuna` | Ataque | Épica | Acero | ✅ |
| 53 | Fortuna Audaz | `r_fortuna_audaz` | Poder | Épica | Épico | ✅ |
| 54 | Maestra de Dagas | `r_maestra_de_dagas` | Poder | Épica | Épico | ✅ |
| 55 | Preparación | `r_preparacion` | Habilidad | Épica | Magia | ✅ |
| 56 | Rapiña | `r_rapina` | Poder | Épica | Épico | ✅ |
| 57 | Sombra Gemela | `r_sombra_gemela` | Poder | Épica | Épico | ✅ |
| 58 | Asesina | `r_asesina` | Poder | Legendaria | Épico | ✅ |
| 59 | Cadena Perfecta | `r_cadena_perfecta` | Poder | Legendaria | Épico | ✅ |
| 60 | Lluvia de Golpes | `ep_lluvia_de_golpes` | Ataque | Legendaria | Épico | ✅ |
| 61 | Mazo Impecable | `ep_mazo_impecable` | Ataque | Legendaria | Épico | ✅ |
| 62 | Mil Cortes | `r_mil_cortes` | Ataque | Legendaria | Épico | ✅ |
| 63 | Nada que Perder | `r_nada_que_perder` | Poder | Legendaria | Épico | ✅ |
| 64 | Ritmo Letal | `r_ritmo_letal` | Poder | Legendaria | Épico | ✅ |
| 65 | Tormenta de Acero | `r_tormenta_de_acero` | Poder | Legendaria | Épico | ✅ |
| 66 | Tormenta de Veneno | `ep_tormenta_veneno` | Habilidad | Legendaria | Épico | ✅ |

## Neutral (0 / 13 animadas)

| # | Carta | Id | Tipo | Rareza | Origen | Animada |
|---|---|---|---|---|---|---|
| 1 | Parada | `e_parada` | Habilidad | Común | Escudo | — |
| 2 | Puñetazo | `a_punio` | Ataque | Común | Acero | — |
| 3 | Visión | `m_vision` | Habilidad | Común | Magia | — |
| 4 | Absorción | `m_absorcion` | Habilidad | Poco común | Magia | — |
| 5 | Escudo Sólido | `e_escudo_solido` | Habilidad | Poco común | Escudo | — |
| 6 | Golpe Férreo | `a_golpe_ferreo` | Ataque | Poco común | Acero | — |
| 7 | Guardia Sólida | `e_guardia_solida` | Habilidad | Poco común | Escudo | — |
| 8 | Impulso | `m_impulso` | Habilidad | Poco común | Magia | — |
| 9 | Patada | `a_patada` | Ataque | Poco común | Acero | — |
| 10 | Retribución | `e_retribucion` | Habilidad | Poco común | Escudo | — |
| 11 | Fortaleza | `e_fortaleza` | Habilidad | Rara | Escudo | — |
| 12 | Golpe Mortal | `ep_golpe_mortal` | Ataque | Legendaria | Épico | — |
| 13 | Poder Oculto | `ep_poder_oculto` | Habilidad | Legendaria | Épico | — |

## Estados y fichas (0 / 4 animadas)

Cartas que meten los enemigos (Espora, Moho, Herida) o que crean otras cartas (Daga Oculta).

| # | Carta | Id | Tipo | Rareza | Origen | Animada |
|---|---|---|---|---|---|---|
| 1 | Daga Oculta | `t_daga_oculta` | Ataque | Común | — | — |
| 2 | Espora | `status_espora` | Estado | Común | — | — |
| 3 | Herida | `status_herida` | Estado | Común | — | — |
| 4 | Moho | `status_moho` | Estado | Común | — | — |
