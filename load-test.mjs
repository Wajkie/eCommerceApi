/**
 * Load Test — 10 stores × 100 simultaneous requests = 1000 total
 *
 * Phase 1: Onboard all 10 stores (parallel)
 * Phase 2: Fire all 1000 requests at once, grouped by store
 * Phase 3: Print summary
 *
 * Run: node load-test.mjs
 * Requires Node 18+ (native fetch)
 */

const API = "http://localhost:5041";
const STORE_COUNT = 10;
const REQUESTS_PER_STORE = 100;

// Seeded customer and product IDs from EcommerceContext seed data
const SEEDED_CUSTOMER = "11111111-1111-1111-1111-111111111111";
const PRODUCT_IDS = [1, 2, 3];

// ─── ANSI colours ────────────────────────────────────────────────────────────
const c = {
  reset: "\x1b[0m",
  bold: "\x1b[1m",
  dim: "\x1b[2m",
  green: "\x1b[32m",
  yellow: "\x1b[33m",
  red: "\x1b[31m",
  cyan: "\x1b[36m",
  magenta: "\x1b[35m",
  blue: "\x1b[34m",
};
const clr = (color, text) => `${c[color]}${text}${c.reset}`;

// ─── Helpers ─────────────────────────────────────────────────────────────────
function randomItem(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function randomUUID() {
  return crypto.randomUUID();
}

async function req(method, path, { headers = {}, body } = {}) {
  const start = performance.now();
  try {
    const res = await fetch(`${API}${path}`, {
      method,
      headers: { "Content-Type": "application/json", ...headers },
      body: body ? JSON.stringify(body) : undefined,
    });
    const ms = Math.round(performance.now() - start);
    return { ok: res.ok, status: res.status, ms, path, method };
  } catch (err) {
    const ms = Math.round(performance.now() - start);
    return { ok: false, status: 0, ms, path, method, err: err.message };
  }
}

// ─── Phase 1: Onboard stores ─────────────────────────────────────────────────
async function onboardStore(n) {
  const res = await fetch(`${API}/api/Stores/Onboard`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(`LoadTest Store ${n}`),
  });
  if (!res.ok) throw new Error(`Onboard failed: ${res.status}`);
  return res.json();
}

// ─── Phase 2: Build 100 requests per store ───────────────────────────────────
//
//  Distribution (100 total):
//   30 × GET /api/Products            — catalog browsing
//   20 × GET /api/Products/{id}       — product detail pages
//   15 × GET /api/Carts/current       — cart retrieval
//   10 × POST /api/Customers          — new customer signups
//   10 × GET /api/Orders              — admin: order list
//    8 × GET /api/Products?search=…   — search queries
//    5 × GET /api/Stores/{id}/Metrics — admin: store metrics
//    2 × GET /api/Stores              — platform-level listing
//
function buildRequests(store) {
  const { storeId, apiKey } = store;
  const storeHeader = { "X-Store-Id": storeId };
  const adminHeader = { "X-Store-Id": storeId, "X-Api-Key": apiKey };
  const searches = ["laptop", "phone", "audio", "electronics", "headphones"];

  const tasks = [];

  // 30 × catalog browse
  for (let i = 0; i < 30; i++) {
    const page = (i % 3) + 1;
    tasks.push(() => req("GET", `/api/Products?page=${page}&pageSize=10`, { headers: storeHeader }));
  }

  // 20 × product detail
  for (let i = 0; i < 20; i++) {
    const id = randomItem(PRODUCT_IDS);
    tasks.push(() => req("GET", `/api/Products/${id}`, { headers: storeHeader }));
  }

  // 15 × cart retrieval (seeded customer)
  for (let i = 0; i < 15; i++) {
    tasks.push(() =>
      req("GET", `/api/Carts/current?customerId=${SEEDED_CUSTOMER}&storeId=${storeId}`, {
        headers: storeHeader,
      })
    );
  }

  // 10 × new customer signup
  for (let i = 0; i < 10; i++) {
    tasks.push(() =>
      req("POST", "/api/Customers", {
        headers: storeHeader,
        body: { Id: randomUUID() },
      })
    );
  }

  // 10 × admin order list
  for (let i = 0; i < 10; i++) {
    tasks.push(() =>
      req("GET", `/api/Orders?page=1&pageSize=20`, { headers: adminHeader })
    );
  }

  // 8 × search queries
  for (let i = 0; i < 8; i++) {
    const term = randomItem(searches);
    tasks.push(() =>
      req("GET", `/api/Products?search=${term}`, { headers: storeHeader })
    );
  }

  // 5 × store metrics
  for (let i = 0; i < 5; i++) {
    tasks.push(() =>
      req("GET", `/api/Stores/${storeId}/Metrics`, { headers: { "X-Api-Key": apiKey } })
    );
  }

  // 2 × platform listing
  for (let i = 0; i < 2; i++) {
    tasks.push(() => req("GET", "/api/Stores"));
  }

  return tasks;
}

// ─── Reporting ────────────────────────────────────────────────────────────────
function report(results) {
  const total = results.length;
  const success = results.filter((r) => r.ok).length;
  const failed = total - success;
  const totalMs = results.reduce((s, r) => s + r.ms, 0);
  const avgMs = Math.round(totalMs / total);
  const maxMs = Math.max(...results.map((r) => r.ms));
  const minMs = Math.min(...results.map((r) => r.ms));

  // Status code buckets (exact codes)
  const statusBuckets = {};
  for (const r of results) {
    const key = r.status === 0 ? "ERR (network)" : String(r.status);
    statusBuckets[key] = (statusBuckets[key] || 0) + 1;
  }

  // Per-endpoint stats
  const endpointMap = {};
  for (const r of results) {
    const key = `${r.method} ${r.path.replace(/\?.*/, "").replace(/\/\d+$/, "/{id}").replace(/=[^&]+/g, "=…")}`;
    if (!endpointMap[key]) endpointMap[key] = { count: 0, ok: 0, totalMs: 0 };
    endpointMap[key].count++;
    if (r.ok) endpointMap[key].ok++;
    endpointMap[key].totalMs += r.ms;
  }

  const divider = "─".repeat(72);

  console.log(`\n${clr("bold", divider)}`);
  console.log(clr("bold", "  LOAD TEST RESULTS"));
  console.log(divider);

  console.log(`  Total requests : ${clr("bold", total)}`);
  console.log(`  Success        : ${clr("green", success)} (${((success / total) * 100).toFixed(1)}%)`);
  console.log(`  Failed         : ${failed > 0 ? clr("red", failed) : clr("dim", failed)}`);
  console.log(`  Avg latency    : ${clr("cyan", avgMs + "ms")}`);
  console.log(`  Min / Max      : ${clr("dim", minMs + "ms")} / ${clr("yellow", maxMs + "ms")}`);

  console.log(`\n  ${clr("bold", "Status codes")}`);
  for (const [code, count] of Object.entries(statusBuckets).sort()) {
    const color = code.startsWith("2") ? "green" : code.startsWith("4") ? "yellow" : "red";
    const pct = ((count / total) * 100).toFixed(1);
    console.log(`    ${clr(color, code.padEnd(20))} ${String(count).padStart(5)}  (${pct}%)`);
  }

  console.log(`\n  ${clr("bold", "Per-endpoint breakdown")}`);
  const sorted = Object.entries(endpointMap).sort((a, b) => b[1].count - a[1].count);
  for (const [endpoint, stats] of sorted) {
    const avg = Math.round(stats.totalMs / stats.count);
    const successRate = ((stats.ok / stats.count) * 100).toFixed(0);
    const rateColor = stats.ok === stats.count ? "green" : stats.ok > stats.count * 0.9 ? "yellow" : "red";
    console.log(
      `    ${clr("dim", stats.count.toString().padStart(4))}×  ${endpoint.padEnd(48)} avg ${String(avg).padStart(5)}ms  ${clr(rateColor, successRate + "%")}`
    );
  }

  console.log(divider + "\n");
}

// ─── Main ─────────────────────────────────────────────────────────────────────
async function main() {
  console.log(clr("bold", `\n  eCommerce API Load Test`));
  console.log(clr("dim", `  ${STORE_COUNT} stores × ${REQUESTS_PER_STORE} requests = ${STORE_COUNT * REQUESTS_PER_STORE} total\n`));

  // ── Phase 1 ──
  process.stdout.write(`  ${clr("blue", "●")} Onboarding ${STORE_COUNT} stores...`);
  const onboardStart = performance.now();
  let stores;
  try {
    stores = await Promise.all(
      Array.from({ length: STORE_COUNT }, (_, i) => onboardStore(i + 1))
    );
  } catch (err) {
    console.log(clr("red", " FAILED"));
    console.error(`\n  Error: ${err.message}`);
    console.error("  Make sure the API is running on http://localhost:5041\n");
    process.exit(1);
  }
  const onboardMs = Math.round(performance.now() - onboardStart);
  console.log(clr("green", " done") + clr("dim", ` (${onboardMs}ms)`));
  for (const s of stores) {
    console.log(clr("dim", `    ✓ ${s.storeName}  id=${s.storeId}`));
  }

  // ── Phase 2 ──
  console.log(`\n  ${clr("blue", "●")} Firing ${STORE_COUNT * REQUESTS_PER_STORE} simultaneous requests...`);
  const allTasks = stores.flatMap(buildRequests);
  const loadStart = performance.now();
  const results = await Promise.all(allTasks.map((t) => t()));
  const loadMs = Math.round(performance.now() - loadStart);
  console.log(clr("green", "  done") + clr("dim", ` (wall time: ${loadMs}ms)`));

  // ── Phase 3 ──
  report(results);
}

main();
