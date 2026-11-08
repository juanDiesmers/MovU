# Plan de pruebas con usuarios — Hito H5 (13 nov)

Borrador del 7 nov. Lo que está marcado **[decidir]** necesita acuerdo del equipo antes del 11 nov.

## 1. Objetivo
Comprobar con personas reales que MovU permite orientarse en la torre de Ingeniería (pisos 3–13) y medir:
- Eficiencia: tiempo, distancia, índice de desvío, SPL, cambios de piso, pisos equivocados, consultas a NPC.
- Rendimiento: FPS promedio y mínimo (RD-1: nunca bajar de 30).
- Usabilidad percibida: SUS (10 preguntas) más 3 preguntas abiertas.

## 2. Qué se prueba
- Ejecutable de Windows (`Builds/MovU_Windows.zip`, commit `f8616c2` o posterior). Misma máquina para todos [decidir cuál].
- Catálogo de 13 misiones, con guardado de progreso y menú (P = pausa).
- Condiciones de guía en el panel de sesión: libre, sin guía, directa, NavMesh.

## 3. Diseño [decidir]
Con pocos participantes (5–10) conviene **entre sujetos** con dos condiciones, por ejemplo *Directa* vs *NavMesh*, asignadas alternando (P01 directa, P02 NavMesh…). Dejar *sin guía* como línea base solo si hay ≥ 4 personas por grupo.
- Tamaño mínimo sugerido: 6 participantes (3 por condición). Ideal 10.
- Perfil: estudiantes que no hayan usado el juego; anotar si conocen el edificio real (sí/no).
- Duración por persona: ~30 min (5 consentimiento y práctica, 15 misiones, 10 SUS y preguntas).

## 4. Protocolo (evaluador: Juan Diego)
**Antes (una vez)**
1. Descomprimir el zip en cada equipo y abrir el `.exe` una vez para comprobar que arranca.
2. Borrar `movu_metrics_v2.csv` de pruebas anteriores o renombrarlo (`%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\`).
3. Tener la hoja de participantes y el enlace del SUS listos. Audífonos y ratón conectados.

**Por participante**
1. Leer el guion de bienvenida (sección 6) y pedir consentimiento verbal o firmado.
2. Panel de sesión: escribir el ID (`P01`, `P02`…), elegir la condición y pulsar Comenzar. Dejar "Continuar" desmarcado.
3. Dejar explorar el lobby 2 min (WASD, ratón, E, I, V). Sin ayudas más allá de los controles.
4. Que haga las misiones solo. El evaluador no ayuda ni da pistas; anota incidentes con la hora.
5. Si se atasca > 5 min en una misión, anotarlo y pasar a la siguiente (Enter).
6. Al terminar: SUS y 3 preguntas abiertas (sección 7). Copiar el CSV a una carpeta con el ID.

**Reglas**: no cambiar el ejecutable entre participantes; una misma máquina por condición si es posible; no corregir errores del juego durante la prueba (anotarlos).

## 5. Variables y criterios de éxito
| Variable | Fuente | Criterio sugerido [decidir] |
|---|---|---|
| Tasa de misiones completadas | CSV / hoja | ≥ 80 % |
| Tiempo por misión | `TotalTimeSec` | reportar mediana por condición |
| Índice de desvío / SPL | CSV | reportar, sin umbral |
| Pisos equivocados | `WrongFloorVisits` | reportar |
| Consultas a NPC | `NpcConsults` | reportar |
| FPS promedio / mínimo | `FPS`, `MinFPS` | min ≥ 30 (RD-1) |
| SUS | cuestionario | ≥ 68 (promedio de referencia) |

## 6. Guion de bienvenida (leer tal cual)
"Vas a probar un juego que simula la torre de Ingeniería de la Javeriana. Te daremos tareas, por ejemplo llegar a una oficina o un salón. Lo que se evalúa es el juego, no tú. Puedes parar cuando quieras. Guardamos tiempos y recorridos sin tu nombre, solo un código. No te daremos ayuda durante las tareas. ¿Aceptas participar?"

## 7. Cuestionario
- **SUS**: 10 ítems estándar, escala 1–5, puntaje 0–100 (impares: valor − 1; pares: 5 − valor; suma × 2,5). Cruzar por `ParticipantId`.
- **Preguntas abiertas**: (1) ¿Qué fue lo más confuso? (2) ¿Qué te ayudó más a orientarte? (3) ¿Qué cambiarías?
- **Datos**: carrera, semestre, ¿conoces el edificio real?, experiencia con videojuegos (nada / poca / mucha), mareo (sí/no).

## 8. Antes del 13 nov — lista de tareas
| Tarea | Quién | Fecha |
|---|---|---|
| Jugar el flujo completo una vez (teclado, ratón, audífonos, menú y guardado) | Juan Diego | 8 nov |
| Medir FPS del ejecutable en las máquinas de prueba | Juan Diego | 9 nov |
| Decidir diseño y condiciones, reclutar participantes | Equipo | 10 nov |
| Prueba piloto con 1 persona; corregir el guion | Juan Diego | 11 nov |
| Congelar el build (solo bloqueantes después) | Sergio | 11 nov |
| Imprimir consentimiento y hoja de observación; armar SUS en formulario | Juan Diego | 12 nov |

## 9. Riesgos
- El build de Linux y Mac no se ha abierto: no usarlos en la prueba.
- Si el FPS baja de 30 en algún piso, anotar en qué piso; el Occlusion Culling está pendiente.
- Si un participante cierra el juego a medias, usar "Continuar" (el progreso se guarda por ID).
- El CSV se sobrescribe por máquina: copiarlo después de cada participante.

## 10. Hoja de observación (por participante)
ID · condición · fecha · máquina · ¿conoce el edificio? · por misión: completada (s/n), pisos equivocados, ayuda a NPC, incidentes · tiempo total · SUS · notas.
