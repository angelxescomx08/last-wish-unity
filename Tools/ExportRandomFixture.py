"""Write the CPython random.Random oracle used by PyRandomTests (no game code needed)."""
import argparse, json, random, zlib
from pathlib import Path

SEEDS = [0, 1, 42, 123456789, 2**32 - 1, 2**32, 2**64 - 1, (42 * 6364136223846793005 + 1442695040888963407) & 0xFFFFFFFFFFFFFFFF,
         "shop:42:1:f1_r2_c3", "", "ñandú"]

def run(seed):
    r = random.Random(seed)
    out = {"seed": str(seed), "kind": "str" if isinstance(seed, str) else "int"}
    out["u32"] = [r.getrandbits(32) for _ in range(5)]
    out["random"] = [repr(r.random()) for _ in range(5)]
    out["randint"] = [r.randint(-3, 17) for _ in range(8)]
    out["choice"] = [r.choice([-1, 0, 1]) for _ in range(8)]
    items = list(range(30))
    r.shuffle(items); out["shuffle"] = items
    out["sample4"] = r.sample(list(range(12)), 3)
    out["sample30"] = r.sample(list(range(40)), 7)
    out["choices"] = [r.choices([1, 2, 3, 4, 5], weights=[50.0, 28.0 * 1.2, 14.0, 6.0, 2.0], k=1)[0] for _ in range(10)]
    out["bits40"] = [r.getrandbits(40) for _ in range(3)]
    out["big"] = [r.randrange(10**12) for _ in range(3)]
    return out

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="Assets/Tests/EditMode/Fixtures/py-random.json")
    args = parser.parse_args()
    data = {"cases": [run(s) for s in SEEDS], "crc": {s: zlib.crc32(s.encode()) for s in ["f1_r2_c3", "pack_acero_1", "ñ", ""]}}
    Path(args.output).write_text(json.dumps(data, indent=1), encoding="utf-8")
