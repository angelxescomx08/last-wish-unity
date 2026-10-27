-- Power telemetry for Último Deseo.
--
-- The game sends one JSON document per run (see docs/telemetry.md). public.ingest_run validates it
-- and explodes it into the tables of the `telemetry` schema; the `analytics` schema holds the power
-- queries (cards, relics, encounters/bosses). Neither schema is exposed through the Data API: the
-- only thing an API key can do is call public.ingest_run.

create schema if not exists telemetry;
create schema if not exists analytics;
revoke all on schema telemetry from public;
revoke all on schema analytics from public;

-- ------------------------------------------------------------------ raw tables

create table telemetry.runs (
    run_id          uuid primary key,
    received_at     timestamptz not null default now(),
    schema_version  integer not null,
    player_id       uuid,
    game_version    text not null,
    content_hash    text not null,
    started_at      timestamptz,
    hero            text not null,
    seed            bigint not null,
    roster          text not null,
    tuned           boolean not null,
    outcome         text not null check (outcome in ('unfinished', 'death', 'abandon', 'quit')),
    floor_reached   integer not null check (floor_reached >= 1),
    rooms_visited   integer not null check (rooms_visited >= 0),
    gold            integer not null,
    hp              integer not null,
    max_hp          integer not null,
    played_seconds  double precision not null,
    combats_count   integer not null,
    payload         jsonb not null
);
comment on table telemetry.runs is 'One row per run sent by the game (public.ingest_run). tuned = the Pruebas screen changed the rules.';

create table telemetry.combats (
    run_id        uuid not null references telemetry.runs (run_id) on delete cascade,
    idx           integer not null,
    floor         integer not null,
    room          text not null check (room in ('combat', 'elite', 'boss')),
    encounter     text not null,
    enemies       text[] not null,
    enemy_hp      integer not null,
    outcome       text not null check (outcome in ('won', 'lost', 'abandoned')),
    turns         integer not null,
    hp_start      integer not null,
    hp_end        integer not null,
    max_hp        integer not null check (max_hp > 0),
    hp_lost       integer not null,
    damage_dealt  bigint not null,
    cards_played  integer not null,
    mana_spent    bigint not null,
    deck          text[] not null,
    relics        text[] not null,
    primary key (run_id, idx)
);
comment on table telemetry.combats is 'Every fight of a run: who it was against, how it went, and the deck/relics the hero brought.';

create table telemetry.combat_cards (
    run_id      uuid not null,
    combat_idx  integer not null,
    card_id     text not null,
    plays       integer not null,
    mana        bigint not null,
    damage      bigint not null,
    block       bigint not null,
    primary key (run_id, combat_idx, card_id),
    foreign key (run_id, combat_idx) references telemetry.combats (run_id, idx) on delete cascade
);
comment on table telemetry.combat_cards is 'Cards played in a fight: plays, mana paid, HP removed from enemies by the play and block gained.';

create table telemetry.choices (
    run_id   uuid not null references telemetry.runs (run_id) on delete cascade,
    idx      integer not null,
    floor    integer not null,
    kind     text not null check (kind in ('card', 'relic')),
    source   text not null,
    offered  text[] not null,
    picked   text[] not null,
    primary key (run_id, idx)
);
comment on table telemetry.choices is 'Every time the game offered cards or relics, and what the player took (empty = skipped).';

create table telemetry.run_cards (
    run_id    uuid not null references telemetry.runs (run_id) on delete cascade,
    card_id   text not null,
    copies    integer not null,
    upgraded  integer not null,
    golden    integer not null,
    diamond   integer not null,
    primary key (run_id, card_id)
);
comment on table telemetry.run_cards is 'The deck at the end of the run.';

create table telemetry.run_relics (
    run_id    uuid not null references telemetry.runs (run_id) on delete cascade,
    ord       integer not null,
    relic_id  text not null,
    chroma    text not null check (chroma in ('none', 'golden', 'diamond')),
    floor     integer,
    source    text not null,
    primary key (run_id, ord)
);
comment on table telemetry.run_relics is 'Relics owned at the end of the run, with the floor and room where each was obtained.';

create index combats_encounter_idx on telemetry.combats (room, encounter);
create index combats_deck_idx on telemetry.combats using gin (deck);
create index combats_relics_idx on telemetry.combats using gin (relics);
create index combat_cards_card_idx on telemetry.combat_cards (card_id);
create index run_cards_card_idx on telemetry.run_cards (card_id);
create index run_relics_relic_idx on telemetry.run_relics (relic_id);
create index runs_hero_idx on telemetry.runs (hero, outcome);

-- Defence in depth: the schema is not exposed, and the tables refuse API roles anyway.
alter table telemetry.runs enable row level security;
alter table telemetry.combats enable row level security;
alter table telemetry.combat_cards enable row level security;
alter table telemetry.choices enable row level security;
alter table telemetry.run_cards enable row level security;
alter table telemetry.run_relics enable row level security;

-- ------------------------------------------------------------------ validation helpers

-- An identifier (card, relic, enemy, encounter, hero...) or an error the API returns as 400.
create function telemetry.ident(value jsonb, what text)
returns text
language plpgsql
immutable
set search_path = ''
as $$
begin
    if value is null or jsonb_typeof(value) <> 'string' or not (value #>> '{}') ~ '^[A-Za-z0-9][A-Za-z0-9_.:+-]{0,95}$' then
        raise exception 'invalid %: %', what, coalesce(left(value::text, 100), 'null') using errcode = '22023';
    end if;
    return value #>> '{}';
end;
$$;

-- An array of identifiers with at most max_count items.
create function telemetry.idents(value jsonb, what text, max_count integer)
returns text[]
language plpgsql
immutable
set search_path = ''
as $$
declare
    result text[] := '{}';
    item jsonb;
begin
    if value is null or jsonb_typeof(value) <> 'array' then
        raise exception 'invalid %: expected an array', what using errcode = '22023';
    end if;
    if jsonb_array_length(value) > max_count then
        raise exception 'invalid %: more than % items', what, max_count using errcode = '22023';
    end if;
    for item in select x from jsonb_array_elements(value) as t(x) loop
        result := result || telemetry.ident(item, what);
    end loop;
    return result;
end;
$$;

-- A whole number inside [lo, hi].
create function telemetry.num(obj jsonb, key text, lo numeric, hi numeric)
returns numeric
language plpgsql
immutable
set search_path = ''
as $$
declare
    value jsonb := obj -> key;
    n numeric;
begin
    if value is null or jsonb_typeof(value) <> 'number' then
        raise exception 'invalid %: expected a number', key using errcode = '22023';
    end if;
    n := (value #>> '{}')::numeric;
    if n < lo or n > hi or n <> trunc(n) then
        raise exception 'invalid %: % is outside [%, %]', key, n, lo, hi using errcode = '22023';
    end if;
    return n;
end;
$$;

-- One of the allowed words.
create function telemetry.word(obj jsonb, key text, allowed text[])
returns text
language plpgsql
immutable
set search_path = ''
as $$
declare
    value text := obj ->> key;
begin
    if value is null or not (value = any (allowed)) then
        raise exception 'invalid %: %', key, coalesce(value, 'null') using errcode = '22023';
    end if;
    return value;
end;
$$;

revoke all on function telemetry.ident(jsonb, text) from public;
revoke all on function telemetry.idents(jsonb, text, integer) from public;
revoke all on function telemetry.num(jsonb, text, numeric, numeric) from public;
revoke all on function telemetry.word(jsonb, text, text[]) from public;

-- ------------------------------------------------------------------ ingestion

-- The only entry point of the game: POST /rest/v1/rpc/ingest_run {"payload": {...}}.
-- Idempotent by run_id: resending a run is a no-op ('duplicate'), and a run the game had to send
-- unfinished (the player closed the game) is replaced by a later, more complete copy ('replaced').
create function public.ingest_run(payload jsonb)
returns text
language plpgsql
security definer
set search_path = ''
as $$
declare
    max_int constant numeric := 2147483647;
    max_big constant numeric := 9223372036854775807;
    v_run_id uuid;
    v_outcome text;
    v_combats jsonb;
    v_choices jsonb;
    v_deck jsonb;
    v_relics jsonb;
    v_existing telemetry.runs%rowtype;
    v_status text := 'inserted';
    v_floor integer;
    c jsonb;
    c_idx integer;
    k jsonb;
    item jsonb;
    ord integer;
begin
    if payload is null or jsonb_typeof(payload) <> 'object' then
        raise exception 'payload must be a JSON object' using errcode = '22023';
    end if;
    if octet_length(payload::text) > 2097152 then
        raise exception 'payload larger than 2 MB' using errcode = '22023';
    end if;
    if telemetry.num(payload, 'schema_version', 1, 1000) <> 1 then
        raise exception 'unsupported schema_version %', payload ->> 'schema_version' using errcode = '22023';
    end if;
    begin
        v_run_id := (payload ->> 'run_id')::uuid;
    exception when invalid_text_representation then
        raise exception 'invalid run_id' using errcode = '22023';
    end;
    if v_run_id is null then
        raise exception 'invalid run_id' using errcode = '22023';
    end if;

    v_outcome := telemetry.word(payload, 'outcome', array['unfinished', 'death', 'abandon', 'quit']);
    v_combats := coalesce(payload -> 'combats', '[]');
    v_choices := coalesce(payload -> 'choices', '[]');
    v_deck := coalesce(payload -> 'deck', '[]');
    v_relics := coalesce(payload -> 'relics', '[]');
    if jsonb_typeof(v_combats) <> 'array' or jsonb_array_length(v_combats) > 5000 then
        raise exception 'invalid combats' using errcode = '22023';
    end if;
    if jsonb_typeof(v_choices) <> 'array' or jsonb_array_length(v_choices) > 10000 then
        raise exception 'invalid choices' using errcode = '22023';
    end if;
    if jsonb_typeof(v_deck) <> 'array' or jsonb_array_length(v_deck) > 5000 then
        raise exception 'invalid deck' using errcode = '22023';
    end if;
    if jsonb_typeof(v_relics) <> 'array' or jsonb_array_length(v_relics) > 2000 then
        raise exception 'invalid relics' using errcode = '22023';
    end if;

    -- Replace an unfinished copy with a more complete one; anything else already here wins.
    select * into v_existing from telemetry.runs r where r.run_id = v_run_id for update;
    if found then
        if v_existing.outcome = 'unfinished'
           and (v_outcome <> 'unfinished' or jsonb_array_length(v_combats) > v_existing.combats_count) then
            delete from telemetry.runs r where r.run_id = v_run_id;
            v_status := 'replaced';
        else
            return 'duplicate';
        end if;
    end if;

    insert into telemetry.runs (run_id, schema_version, player_id, game_version, content_hash, started_at, hero, seed, roster,
                                tuned, outcome, floor_reached, rooms_visited, gold, hp, max_hp, played_seconds, combats_count, payload)
    values (
        v_run_id,
        1,
        case when payload ? 'player_id' and jsonb_typeof(payload -> 'player_id') = 'string' then (payload ->> 'player_id')::uuid end,
        telemetry.ident(payload -> 'game_version', 'game_version'),
        telemetry.ident(payload -> 'content_hash', 'content_hash'),
        case when jsonb_typeof(payload -> 'started_at') = 'string' then (payload ->> 'started_at')::timestamptz end,
        telemetry.word(payload, 'hero', array['warrior', 'mage', 'rogue']),
        telemetry.num(payload, 'seed', 0, max_big)::bigint,
        telemetry.word(payload, 'roster', array['original', 'expanded']),
        coalesce((payload ->> 'tuned')::boolean, false),
        v_outcome,
        telemetry.num(payload, 'floor', 1, max_int)::integer,
        telemetry.num(payload, 'rooms', 0, max_int)::integer,
        telemetry.num(payload, 'gold', 0, max_int)::integer,
        telemetry.num(payload, 'hp', 0, max_int)::integer,
        telemetry.num(payload, 'max_hp', 1, max_int)::integer,
        greatest(0, least(1e7, coalesce((payload ->> 'played_seconds')::double precision, 0))),
        jsonb_array_length(v_combats),
        payload
    )
    on conflict (run_id) do nothing;
    if not found then
        return 'duplicate';    -- the same run arrived twice at the same moment
    end if;

    for c, c_idx in select x, (n - 1)::integer from jsonb_array_elements(v_combats) with ordinality as t(x, n) loop
        if jsonb_typeof(c) <> 'object' then
            raise exception 'invalid combat %', c_idx using errcode = '22023';
        end if;
        insert into telemetry.combats (run_id, idx, floor, room, encounter, enemies, enemy_hp, outcome, turns, hp_start, hp_end,
                                       max_hp, hp_lost, damage_dealt, cards_played, mana_spent, deck, relics)
        values (
            v_run_id, c_idx,
            telemetry.num(c, 'floor', 1, max_int)::integer,
            telemetry.word(c, 'room', array['combat', 'elite', 'boss']),
            telemetry.ident(c -> 'encounter', 'encounter'),
            telemetry.idents(c -> 'enemies', 'enemies', 50),
            telemetry.num(c, 'enemy_hp', 0, max_int)::integer,
            telemetry.word(c, 'outcome', array['won', 'lost', 'abandoned']),
            telemetry.num(c, 'turns', 0, max_int)::integer,
            telemetry.num(c, 'hp_start', 0, max_int)::integer,
            telemetry.num(c, 'hp_end', 0, max_int)::integer,
            telemetry.num(c, 'max_hp', 1, max_int)::integer,
            telemetry.num(c, 'hp_lost', 0, max_int)::integer,
            telemetry.num(c, 'damage', 0, max_big)::bigint,
            telemetry.num(c, 'cards_played', 0, max_int)::integer,
            telemetry.num(c, 'mana', 0, max_big)::bigint,
            telemetry.idents(coalesce(c -> 'deck', '[]'), 'deck', 5000),
            telemetry.idents(coalesce(c -> 'relics', '[]'), 'relics', 2000)
        );
        if jsonb_typeof(coalesce(c -> 'cards', '[]')) <> 'array' or jsonb_array_length(coalesce(c -> 'cards', '[]')) > 1000 then
            raise exception 'invalid cards of combat %', c_idx using errcode = '22023';
        end if;
        for k in select x from jsonb_array_elements(coalesce(c -> 'cards', '[]')) as t(x) loop
            insert into telemetry.combat_cards (run_id, combat_idx, card_id, plays, mana, damage, block)
            values (
                v_run_id, c_idx,
                telemetry.ident(k -> 'id', 'card id'),
                telemetry.num(k, 'plays', 0, max_int)::integer,
                telemetry.num(k, 'mana', 0, max_big)::bigint,
                telemetry.num(k, 'damage', 0, max_big)::bigint,
                telemetry.num(k, 'block', 0, max_big)::bigint
            )
            on conflict (run_id, combat_idx, card_id) do update
                set plays = telemetry.combat_cards.plays + excluded.plays,
                    mana = telemetry.combat_cards.mana + excluded.mana,
                    damage = telemetry.combat_cards.damage + excluded.damage,
                    block = telemetry.combat_cards.block + excluded.block;
        end loop;
    end loop;

    for item, ord in select x, (n - 1)::integer from jsonb_array_elements(v_choices) with ordinality as t(x, n) loop
        insert into telemetry.choices (run_id, idx, floor, kind, source, offered, picked)
        values (
            v_run_id, ord,
            telemetry.num(item, 'floor', 1, max_int)::integer,
            telemetry.word(item, 'kind', array['card', 'relic']),
            telemetry.ident(item -> 'source', 'choice source'),
            telemetry.idents(item -> 'offered', 'offered', 100),
            telemetry.idents(item -> 'picked', 'picked', 100)
        );
    end loop;

    for item in select x from jsonb_array_elements(v_deck) as t(x) loop
        insert into telemetry.run_cards (run_id, card_id, copies, upgraded, golden, diamond)
        values (
            v_run_id,
            telemetry.ident(item -> 'id', 'deck card'),
            1,
            case when (item ->> 'upgraded')::boolean then 1 else 0 end,
            case when item ->> 'chroma' = 'golden' then 1 else 0 end,
            case when item ->> 'chroma' = 'diamond' then 1 else 0 end
        )
        on conflict (run_id, card_id) do update
            set copies = telemetry.run_cards.copies + 1,
                upgraded = telemetry.run_cards.upgraded + excluded.upgraded,
                golden = telemetry.run_cards.golden + excluded.golden,
                diamond = telemetry.run_cards.diamond + excluded.diamond;
    end loop;

    for item, ord in select x, (n - 1)::integer from jsonb_array_elements(v_relics) with ordinality as t(x, n) loop
        v_floor := case when jsonb_typeof(item -> 'floor') = 'number' then telemetry.num(item, 'floor', 1, max_int)::integer end;
        insert into telemetry.run_relics (run_id, ord, relic_id, chroma, floor, source)
        values (
            v_run_id, ord,
            telemetry.ident(item -> 'id', 'relic'),
            telemetry.word(item, 'chroma', array['none', 'golden', 'diamond']),
            v_floor,
            telemetry.ident(coalesce(item -> 'source', '"unknown"'), 'relic source')
        );
    end loop;

    return v_status;
end;
$$;

comment on function public.ingest_run(jsonb) is 'Último Deseo power telemetry: stores one run (idempotent by run_id). Returns inserted, replaced or duplicate.';
revoke all on function public.ingest_run(jsonb) from public;
grant execute on function public.ingest_run(jsonb) to anon, authenticated, service_role;

-- ------------------------------------------------------------------ analytics

-- Wilson score interval of a rate (95 % by default): with few samples the interval is wide.
create function analytics.wilson_low(successes numeric, total numeric, z double precision default 1.96)
returns double precision
language sql
immutable
set search_path = ''
as $$
    select case when total is null or total <= 0 then null else
        ((successes / total) + z * z / (2 * total) - z * sqrt((successes / total) * (1 - successes / total) / total + z * z / (4 * total * total)))
        / (1 + z * z / total) end;
$$;

create function analytics.wilson_high(successes numeric, total numeric, z double precision default 1.96)
returns double precision
language sql
immutable
set search_path = ''
as $$
    select case when total is null or total <= 0 then null else
        ((successes / total) + z * z / (2 * total) + z * sqrt((successes / total) * (1 - successes / total) / total + z * z / (4 * total * total)))
        / (1 + z * z / total) end;
$$;

-- Runs that count for balance: not changed by the Pruebas screen.
create view analytics.valid_runs as
select * from telemetry.runs where not tuned;

-- Fights that finished (won or lost), with what an average fight of the same hero, floor and room
-- looks like. Residuals compare a fight with that baseline: hp_residual < 0 means the hero lost less
-- HP than usual, death_residual < 0 means it died less often than usual.
create view analytics.combat_facts as
with base as (
    select c.*, r.hero, r.player_id, r.game_version, r.content_hash, r.roster,
           c.hp_lost::double precision / c.max_hp as hp_lost_pct,
           (c.outcome = 'lost')::integer as died
    from telemetry.combats c
    join analytics.valid_runs r using (run_id)
    where c.outcome in ('won', 'lost')
)
select base.*,
       avg(hp_lost_pct) over w as expected_hp_lost_pct,
       avg(died) over w as expected_death_rate,
       count(*) over w as baseline_fights,
       hp_lost_pct - avg(hp_lost_pct) over w as hp_residual,
       died - avg(died) over w as death_residual
from base
window w as (partition by hero, floor, room);

-- Bosses, elites and regular encounters: how often they kill the hero and how much HP they take.
-- hero = 'all' aggregates every hero.
create view analytics.encounter_power as
select room,
       encounter,
       coalesce(hero, 'all') as hero,
       count(*) as fights,
       sum(died) as deaths,
       round(avg(died)::numeric, 4) as death_rate,
       round(analytics.wilson_low(sum(died), count(*))::numeric, 4) as death_rate_low,
       round(analytics.wilson_high(sum(died), count(*))::numeric, 4) as death_rate_high,
       round(avg(hp_lost_pct)::numeric, 4) as avg_hp_lost_pct,
       round(avg(hp_residual)::numeric, 4) as hp_vs_floor_avg,
       round(avg(turns)::numeric, 2) as avg_turns,
       round(avg(floor)::numeric, 2) as avg_floor,
       round(avg(damage_dealt)::numeric, 1) as avg_damage_dealt
from analytics.combat_facts
group by grouping sets ((room, encounter, hero), (room, encounter));

create view analytics.boss_power as
select * from analytics.encounter_power where room = 'boss';

-- How far each hero gets before dying (the baseline of run-level comparisons).
create view analytics.hero_baseline as
select hero,
       count(*) as runs,
       round(avg(floor_reached)::numeric, 3) as avg_floor,
       percentile_cont(0.5) within group (order by floor_reached) as median_floor,
       round(avg(rooms_visited)::numeric, 2) as avg_rooms,
       round(avg(played_seconds / 60)::numeric, 1) as avg_minutes
from analytics.valid_runs
where outcome = 'death'
group by hero;

-- Cards. Read together:
--   pick_rate                        how often players take it when it is offered
--   damage_per_mana / block_per_play what one play does
--   hp_vs_avg_in_deck / _played      HP lost (fraction of max HP) vs fights of the same hero, floor and room;
--                                    negative = the hero suffers less with it
--   death_vs_avg_in_deck             same for the chance of dying in the fight
--   floor_delta                      floor reached by runs that ended with it vs the hero average
create view analytics.card_power as
with offers as (
    select o.card_id, coalesce(r.hero, 'all') as hero,
           count(*) as offered,
           count(*) filter (where o.card_id = any (ch.picked)) as picked
    from telemetry.choices ch
    join analytics.valid_runs r using (run_id)
    cross join lateral (select distinct unnest(ch.offered) as card_id) o
    where ch.kind = 'card'
    group by grouping sets ((o.card_id, r.hero), (o.card_id))
),
plays as (
    select cc.card_id, coalesce(f.hero, 'all') as hero,
           count(*) as combats_played,
           sum(cc.plays) as plays,
           sum(cc.damage) as damage,
           sum(cc.mana) as mana,
           sum(cc.block) as block,
           avg(f.hp_residual) as hp_residual_played
    from telemetry.combat_cards cc
    join analytics.combat_facts f on f.run_id = cc.run_id and f.idx = cc.combat_idx
    group by grouping sets ((cc.card_id, f.hero), (cc.card_id))
),
in_deck as (
    select d.card_id, coalesce(f.hero, 'all') as hero,
           count(*) as combats_in_deck,
           avg(f.hp_residual) as hp_residual,
           avg(f.death_residual) as death_residual
    from analytics.combat_facts f
    cross join lateral (select distinct unnest(f.deck) as card_id) d
    group by grouping sets ((d.card_id, f.hero), (d.card_id))
),
finals as (
    select rc.card_id, coalesce(r.hero, 'all') as hero,
           count(*) as runs_with,
           avg(r.floor_reached) as avg_floor_with,
           avg(r.floor_reached - b.avg_floor) as floor_delta
    from telemetry.run_cards rc
    join analytics.valid_runs r using (run_id)
    join analytics.hero_baseline b on b.hero = r.hero
    where r.outcome = 'death'
    group by grouping sets ((rc.card_id, r.hero), (rc.card_id))
),
keys as (
    select card_id, hero from offers union select card_id, hero from plays
    union select card_id, hero from in_deck union select card_id, hero from finals
)
select k.card_id,
       k.hero,
       coalesce(o.offered, 0) as offered,
       coalesce(o.picked, 0) as picked,
       round((o.picked::numeric / nullif(o.offered, 0)), 4) as pick_rate,
       round(analytics.wilson_low(o.picked, o.offered)::numeric, 4) as pick_rate_low,
       coalesce(p.combats_played, 0) as combats_played,
       coalesce(p.plays, 0) as plays,
       round(p.damage::numeric / nullif(p.plays, 0), 2) as damage_per_play,
       round(p.damage::numeric / nullif(p.mana, 0), 2) as damage_per_mana,
       round(p.block::numeric / nullif(p.plays, 0), 2) as block_per_play,
       round(p.hp_residual_played::numeric, 4) as hp_vs_avg_played,
       coalesce(d.combats_in_deck, 0) as combats_in_deck,
       round(d.hp_residual::numeric, 4) as hp_vs_avg_in_deck,
       round(d.death_residual::numeric, 4) as death_vs_avg_in_deck,
       coalesce(fi.runs_with, 0) as runs_with,
       round(fi.avg_floor_with::numeric, 2) as avg_floor_with,
       round(fi.floor_delta::numeric, 3) as floor_delta
from keys k
left join offers o on o.card_id = k.card_id and o.hero = k.hero
left join plays p on p.card_id = k.card_id and p.hero = k.hero
left join in_deck d on d.card_id = k.card_id and d.hero = k.hero
left join finals fi on fi.card_id = k.card_id and fi.hero = k.hero;

-- Relics, with the same reading as card_power (pick_rate only counts real choices: treasure,
-- boss, shop and gachapón; relics the game simply gives, such as the elite one, are not choices).
create view analytics.relic_power as
with offers as (
    select o.relic_id, coalesce(r.hero, 'all') as hero,
           count(*) as offered,
           count(*) filter (where o.relic_id = any (ch.picked)) as picked
    from telemetry.choices ch
    join analytics.valid_runs r using (run_id)
    cross join lateral (select distinct unnest(ch.offered) as relic_id) o
    where ch.kind = 'relic'
    group by grouping sets ((o.relic_id, r.hero), (o.relic_id))
),
held as (
    select h.relic_id, coalesce(f.hero, 'all') as hero,
           count(*) as combats_with,
           avg(f.hp_residual) as hp_residual,
           avg(f.death_residual) as death_residual
    from analytics.combat_facts f
    cross join lateral (select distinct unnest(f.relics) as relic_id) h
    group by grouping sets ((h.relic_id, f.hero), (h.relic_id))
),
finals as (
    select rr.relic_id, coalesce(r.hero, 'all') as hero,
           count(distinct rr.run_id) as runs_with,
           avg(r.floor_reached) as avg_floor_with,
           avg(r.floor_reached - b.avg_floor) as floor_delta,
           avg(rr.floor) as avg_floor_obtained
    from telemetry.run_relics rr
    join analytics.valid_runs r using (run_id)
    join analytics.hero_baseline b on b.hero = r.hero
    where r.outcome = 'death'
    group by grouping sets ((rr.relic_id, r.hero), (rr.relic_id))
),
keys as (
    select relic_id, hero from offers union select relic_id, hero from held union select relic_id, hero from finals
)
select k.relic_id,
       k.hero,
       coalesce(o.offered, 0) as offered,
       coalesce(o.picked, 0) as picked,
       round((o.picked::numeric / nullif(o.offered, 0)), 4) as pick_rate,
       round(analytics.wilson_low(o.picked, o.offered)::numeric, 4) as pick_rate_low,
       coalesce(h.combats_with, 0) as combats_with,
       round(h.hp_residual::numeric, 4) as hp_vs_avg,
       round(h.death_residual::numeric, 4) as death_vs_avg,
       coalesce(fi.runs_with, 0) as runs_with,
       round(fi.avg_floor_with::numeric, 2) as avg_floor_with,
       round(fi.floor_delta::numeric, 3) as floor_delta,
       round(fi.avg_floor_obtained::numeric, 2) as avg_floor_obtained
from keys k
left join offers o on o.relic_id = k.relic_id and o.hero = k.hero
left join held h on h.relic_id = k.relic_id and h.hero = k.hero
left join finals fi on fi.relic_id = k.relic_id and fi.hero = k.hero;

-- Which builds of the game the data comes from (content_hash changes when card or relic numbers change).
create view analytics.content_versions as
select game_version, content_hash, count(*) as runs, min(received_at) as first_seen, max(received_at) as last_seen
from telemetry.runs
group by game_version, content_hash;

comment on view analytics.card_power is 'Card power: pick rate, damage/block per play, HP and deaths vs the average fight of the same hero, floor and room, floor reached.';
comment on view analytics.relic_power is 'Relic power: pick rate, HP and deaths vs the average fight of the same hero, floor and room, floor reached.';
comment on view analytics.encounter_power is 'Encounter power (regular, elite, boss): death rate with a 95 % interval, HP taken, turns.';
