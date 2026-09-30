# Clasificación online (opcional)

El juego guarda siempre los 10 mejores tiempos de cada tramo **en el dispositivo**. Para tener una **tabla mundial**
compartida por todos los jugadores hace falta un pequeño servidor. Este repositorio trae uno listo en `server/`
(un *Worker* de Cloudflare con almacenamiento KV; el plan gratuito sobra para este juego).

## Pasos (unos 10 minutos)

1. Crea una cuenta gratuita en <https://dash.cloudflare.com> (esto lo tienes que hacer tú).
2. Instala la herramienta de Cloudflare y entra con tu cuenta:
   ```bash
   npm install -g wrangler
   wrangler login
   ```
3. Crea el almacén de tiempos y copia el `id` que te devuelve en `server/wrangler.toml` (donde pone `PON_AQUI_EL_ID_DEL_NAMESPACE`):
   ```bash
   cd server
   wrangler kv namespace create SCORES
   ```
4. Publica el servidor:
   ```bash
   wrangler deploy
   ```
   Te dará una dirección como `https://rally3d-scores.<tu-cuenta>.workers.dev`.
5. En Unity, selecciona `Assets/Resources/LeaderboardConfig.asset` y pega esa dirección en **Url**.
6. Haz un build nuevo y súbelo a itch.io.

Desde ese momento, cada tiempo se envía al servidor. La pantalla **TIEMPOS** muestra la tabla mundial (y, sin conexión,
la del dispositivo), y en los resultados aparece «PUESTO N EN LA TABLA MUNDIAL».

## Qué protege el servidor

- Solo acepta tramos del juego y nombres de hasta 16 letras, números y espacios.
- Rechaza tiempos imposibles: menos de 45 s en un tramo de unos 3 km, o más de una hora.
- No es a prueba de trampas: cualquiera con conocimientos podría enviar un tiempo falso plausible. Para un juego
  gratuito en itch.io es suficiente; si hiciera falta más, el siguiente paso sería firmar los envíos.
