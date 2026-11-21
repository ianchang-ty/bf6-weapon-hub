"""
Packs raw BF6 weapon export JSON files into:
1. data/mobile_data.json & mobile-pwa/data.js (compact read-only package for mobile PWA & Android APK)
2. data/desktop_bundle.json.gz (full gunsmith + precomputed analysis bundle for Windows desktop .exe)

Usage:
  python tools/pack_data.py <path-to-raw-export-dir>
"""
import gzip, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

USED_T_KEYS = {
    'UnzoomedMinAnglesArrayIndex', 'MovingZoomedMinAnglesArrayIndex',
    'WeaponZoomTransitionIndex', 'AnimationZoomSettingsIndex',
    'WeaponZoomedMoveSpeedMultiplierIndex', 'SprintSettingsIndex', 'WeaponDeployTimeIndex',
    'CanFireWhileSprinting', 'Shot.DamageProtectionMultiplierIndex',
    'Shot.DamagePenetrationMultiplierIndex', 'Shot.InitialSpeed.z',
    'Ammo.MagazineCapacity', 'Ammo.NumberOfMagazines',
    'ReloadInfoArray[0].ReloadTimeBulletsLeft', 'ReloadInfoArray[1].ReloadTime',
    'spot.2D', 'spot.3D', 'sway',
    'DispersionBehavior.Zoomed.Stationary.IncreasePerShot',
    'DispersionBehavior.Unzoomed.Stationary.IncreasePerShot',
    'Recoil.Zoomed.RecoilAmount', 'Recoil.Zoomed.RecoilAmountMultiplier',
    'Recoil.Zoomed.RecoilAmountMultiplierExponent', 'Recoil.Zoomed.RecoilDirection',
    'Recoil.Zoomed.RecoilDirectionVariation', 'Recoil.Zoomed.RecoilDirectionVariationMultiplier',
    'Recoil.Zoomed.RecoilDirectionVariationMultiplierExponent',
    'Recoil.Zoomed.RecoilDuration', 'Recoil.Zoomed.RecoilDecreaseFactor',
}

def main():
    if len(sys.argv) < 2:
        print("Usage: python tools/pack_data.py <raw-data-directory>")
        sys.exit(1)
    src = os.path.abspath(sys.argv[1])
    full = json.load(open(os.path.join(src, 'full_data.json'), encoding='utf-8'))
    tables = json.load(open(os.path.join(src, 'tables.json'), encoding='utf-8'))['tables']

    os.makedirs(os.path.join(ROOT, 'data'), exist_ok=True)
    os.makedirs(os.path.join(ROOT, 'mobile-pwa'), exist_ok=True)

    mobile_weapons = []
    for k, w in full['weapons'].items():
        cm = w.get('cls_meta', {})
        bds = {}
        for rk, bd in w['builds'].items():
            bds[rk] = {
                'rc': bd['role_cn'],
                'c': bd['cost'],
                'st': bd['stats'],
                'it': bd['items'],
                'fk': bd.get('factory_kept', []),
                'pr': bd.get('prio', []),
                'dg': bd.get('downgrades', [])
            }
        mobile_weapons.append({
            'key': k,
            'name': w['name'],
            'en': w['en'],
            'cls': w['cls'],
            'clsId': w['clsId'],
            'labs': w['labs'],
            'fire': w['fire'],
            'burst': w.get('burst'),
            'budget': w['budget'],
            'base': w['base'],
            'bp': w['bp'],
            'bands': w['bands'],
            'sweet_spot': w.get('sweet_spot'),
            'hs_max': w.get('hs_max'),
            'cycle_s': w.get('cycle_s'),
            'ammo_cmp': w.get('ammo_cmp'),
            'notes': w.get('notes', []),
            'meta': cm,
            'builds': bds
        })

    mobile_payload = {'meta': full['meta'], 'weapons': mobile_weapons}
    mobile_json_str = json.dumps(mobile_payload, ensure_ascii=False, separators=(',', ':'))
    open(os.path.join(ROOT, 'data', 'mobile_data.json'), 'w', encoding='utf-8').write(mobile_json_str)
    open(os.path.join(ROOT, 'mobile-pwa', 'data.js'), 'w', encoding='utf-8').write(
        'window.BF6_DATA=' + mobile_json_str + ';\n'
    )

    raw_weapons = {}
    for w_entry in mobile_weapons:
        k = w_entry['key']
        rw = json.load(open(os.path.join(src, 'w', f'{k}.json'), encoding='utf-8'))
        dset = set(rw.get('default_build') or [])
        clean_slots = []
        for s in rw.get('slots', []):
            opts = []
            for o in s.get('opts', []):
                cn = (o.get('cn') or '').strip()
                if not cn:
                    continue
                if o['cost'] <= 0 and o['id'] not in dset:
                    continue
                opts.append({
                    'id': o['id'],
                    'cn': cn,
                    'type': o.get('type', ''),
                    'cost': o['cost'],
                    'd': bool(o.get('d')),
                    'desc': o.get('desc', '')
                })
            if opts:
                clean_slots.append({'id': s['id'], 'opts': opts})

        slim_t = {tk: tv for tk, tv in rw.get('t', {}).items() if tk in USED_T_KEYS}
        raw_weapons[k] = {
            'deploy': rw.get('deploy', 'dta_weapons'),
            'rof': rw.get('rof', 600),
            'cycle_ms': rw.get('cycle_ms', 100),
            'bolt': bool(rw.get('bolt')),
            'pellet_disp': rw.get('pellet_disp'),
            'pellets': rw.get('pellets', 1),
            'dmg': rw.get('dmg', []),
            'mult': rw.get('mult', {}),
            'default_build': rw.get('default_build', []),
            't': slim_t,
            'ammo': rw.get('ammo', {}),
            'mods': rw.get('mods', {}),
            'prec': (rw.get('prec') or {}).get('rows', []),
            'slots': clean_slots
        }

    desktop_payload = {
        'meta': full['meta'],
        'tables': tables,
        'weapons': mobile_weapons,
        'gunsmith': raw_weapons
    }
    desktop_bytes = json.dumps(desktop_payload, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
    gz_path = os.path.join(ROOT, 'data', 'desktop_bundle.json.gz')
    with gzip.GzipFile(gz_path, 'wb', mtime=0) as gz:
        gz.write(desktop_bytes)

    print(f"mobile_data.json: {len(mobile_json_str):,} chars")
    print(f"desktop raw JSON: {len(desktop_bytes):,} bytes -> gzipped: {os.path.getsize(gz_path):,} bytes")

if __name__ == '__main__':
    main()
