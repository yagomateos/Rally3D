// Rally 3D online best-times table: a Cloudflare Worker with one KV namespace bound as SCORES.
//   GET  /scores?stage=<key>   -> { "entries": [ { name, time, car, date }, ... ] }  (top 10, fastest first)
//   POST /scores  { stage, name, time, car }  -> the updated top 10
// Deploy: see docs/CLASIFICACION_ONLINE.md. Free plan limits are far above what this game needs.

const TOP = 10;
const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

function json(body, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json", ...CORS } });
}

// Only the game's stage keys ("TRAMO 01_PINAR DE VALDENIEBLA"), names of letters, digits and spaces.
function cleanStage(s) { return typeof s === "string" && /^TRAMO \d\d_[A-ZÁÉÍÓÚÑÜ ]{3,40}$/.test(s) ? s : null; }
function cleanName(s) {
  const n = String(s || "").toUpperCase().replace(/[^A-Z0-9ÁÉÍÓÚÑÜ ]/g, "").trim().slice(0, 16);
  return n || "PILOTO";
}

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") return new Response(null, { headers: CORS });
    const url = new URL(request.url);
    if (url.pathname !== "/scores") return json({ error: "not found" }, 404);

    if (request.method === "GET") {
      const stage = cleanStage(url.searchParams.get("stage"));
      if (!stage) return json({ error: "bad stage" }, 400);
      return json({ entries: (await env.SCORES.get(stage, "json")) || [] });
    }

    if (request.method === "POST") {
      let body;
      try { body = await request.json(); } catch { return json({ error: "bad json" }, 400); }
      const stage = cleanStage(body.stage);
      const time = Number(body.time);
      // Plausibility: every stage is ~3 km, so under 45 s (240 km/h average) is not a real run.
      if (!stage || !isFinite(time) || time < 45 || time > 3600) return json({ error: "rejected" }, 400);
      const entry = {
        name: cleanName(body.name),
        time: Math.round(time * 1000) / 1000,
        car: String(body.car || "").slice(0, 24),
        date: new Date().toLocaleDateString("es-ES"),
      };
      const list = (await env.SCORES.get(stage, "json")) || [];
      list.push(entry);
      list.sort((a, b) => a.time - b.time);
      const top = list.slice(0, TOP);
      await env.SCORES.put(stage, JSON.stringify(top));
      return json({ entries: top });
    }
    return json({ error: "method" }, 405);
  },
};
