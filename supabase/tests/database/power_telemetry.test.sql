-- supabase test db: ingestion and power views of supabase/migrations/*_power_telemetry.sql.
begin;
create extension if not exists pgtap with schema extensions;
set local search_path = public, extensions;
select plan(20);

-- Start from an empty store (the transaction is rolled back at the end, so real data is untouched).
delete from telemetry.runs;

-- A small run: two fights (a regular one won, the boss lost), one card choice, one relic choice.
create temporary table sample (doc jsonb) on commit drop;
insert into sample values ($json$
{
  "schema_version": 1,
  "run_id": "6f1d1c1e-8a2b-4c3d-9e4f-0a1b2c3d4e5f",
  "player_id": "0d6c6a44-1111-4222-8333-944455556666",
  "game_version": "0.1.0",
  "content_hash": "0123456789abcdef",
  "started_at": "2026-10-23T18:00:00Z",
  "hero": "warrior",
  "seed": 42,
  "roster": "expanded",
  "tuned": false,
  "outcome": "death",
  "floor": 1,
  "rooms": 7,
  "gold": 140,
  "hp": 0,
  "max_hp": 80,
  "played_seconds": 600.5,
  "deck": [
    {"id": "a_tajo", "upgraded": false, "chroma": "none"},
    {"id": "a_tajo", "upgraded": true, "chroma": "golden"},
    {"id": "a_golpe_ferreo", "upgraded": false, "chroma": "none"}
  ],
  "relics": [{"id": "amulet", "chroma": "none", "floor": 1, "source": "treasure"}],
  "choices": [
    {"floor": 1, "kind": "card", "source": "reward", "offered": ["a_tajo", "a_golpe_ferreo", "a_furia"], "picked": ["a_tajo"]},
    {"floor": 1, "kind": "relic", "source": "treasure", "offered": ["amulet"], "picked": ["amulet"]}
  ],
  "combats": [
    {"floor": 1, "room": "combat", "encounter": "slime", "enemies": ["slime"], "enemy_hp": 40, "outcome": "won",
     "turns": 3, "hp_start": 80, "hp_end": 72, "max_hp": 80, "hp_lost": 8, "damage": 40, "cards_played": 7, "mana": 7,
     "deck": ["a_tajo", "a_golpe_ferreo"], "relics": [],
     "cards": [{"id": "a_tajo", "plays": 4, "mana": 4, "damage": 32, "block": 0}, {"id": "a_golpe_ferreo", "plays": 3, "mana": 3, "damage": 8, "block": 15}]},
    {"floor": 1, "room": "boss", "encounter": "caballero", "enemies": ["caballero"], "enemy_hp": 200, "outcome": "lost",
     "turns": 6, "hp_start": 72, "hp_end": 0, "max_hp": 80, "hp_lost": 72, "damage": 150, "cards_played": 14, "mana": 15,
     "deck": ["a_tajo", "a_tajo", "a_golpe_ferreo"], "relics": ["amulet"],
     "cards": [{"id": "a_tajo", "plays": 9, "mana": 9, "damage": 120, "block": 0}]}
  ]
}
$json$);

select is(public.ingest_run((select doc from sample)), 'inserted', 'a new run is stored');
select is((select count(*)::integer from telemetry.runs), 1, 'one run row');
select is((select count(*)::integer from telemetry.combats), 2, 'two fights');
select is((select count(*)::integer from telemetry.combat_cards), 3, 'three card uses');
select is((select count(*)::integer from telemetry.choices), 2, 'two choices');
select is((select copies from telemetry.run_cards where card_id = 'a_tajo'), 2, 'final deck copies are counted');
select is((select golden from telemetry.run_cards where card_id = 'a_tajo'), 1, 'final deck chromas are counted');
select is(public.ingest_run((select doc from sample)), 'duplicate', 'resending the same run is a no-op');
select is((select count(*)::integer from telemetry.combats), 2, 'a duplicate adds nothing');

-- An unfinished copy (the game was closed) is replaced by the complete one.
select is(public.ingest_run((select jsonb_set(jsonb_set(doc, '{run_id}', '"11111111-2222-4333-8444-555555555555"'), '{outcome}', '"unfinished"') - 'combats' from sample)),
          'inserted', 'an unfinished run is stored');
select is(public.ingest_run((select jsonb_set(doc, '{run_id}', '"11111111-2222-4333-8444-555555555555"') from sample)),
          'replaced', 'the complete copy replaces the unfinished one');
select is((select outcome from telemetry.runs where run_id = '11111111-2222-4333-8444-555555555555'), 'death', 'the replacement keeps the final outcome');

select throws_ok($$ select public.ingest_run('{"schema_version": 1, "run_id": "not-a-uuid"}') $$, '22023', null, 'a bad run_id is rejected');
select throws_ok($$ select public.ingest_run((select jsonb_set(jsonb_set(doc, '{hero}', '"paladin"'), '{run_id}', '"22222222-2222-4333-8444-555555555555"') from sample)) $$, '22023', null, 'an unknown hero is rejected');
select throws_ok($$ select public.ingest_run((select jsonb_set(jsonb_set(doc, '{combats,0,enemies}', '["<script>"]'), '{run_id}', '"33333333-2222-4333-8444-555555555555"') from sample)) $$, '22023', null, 'identifiers are validated');

-- API keys can call the function and nothing else.
select ok(has_function_privilege('anon', 'public.ingest_run(jsonb)', 'execute'), 'anon can send runs');
select ok(not has_schema_privilege('anon', 'telemetry', 'usage'), 'anon cannot read raw telemetry');
select ok(not has_schema_privilege('anon', 'analytics', 'usage'), 'anon cannot read the analytics');

-- The power views see the run (two identical runs were stored: the sample and its replacement).
select is((select offered::integer from analytics.card_power where card_id = 'a_tajo' and hero = 'warrior'), 2, 'card offers are counted');
select is((select death_rate from analytics.encounter_power where encounter = 'caballero' and hero = 'all'), 1.0000::numeric, 'the boss killed every time');

select * from finish();
rollback;
