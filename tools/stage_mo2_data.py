# Dev tool: stage a Linux-readable copy of an MO2 profile's data folder for dry-running the patcher.
# Plugins/BSAs are symlinked, SKSE INIs are copied (the patcher writes there). Edit the constants below.
# Usage: python3 -I tools/stage_mo2_data.py
import os, shutil, sys
MO2='/home/ondine/Games/Skyrim/MO2-shadow'; GAME='/home/ondine/Games/gog/Skyrim/Data'
OUT='/tmp/sslr-run/Data'
PROFILE='Simpler'
STALE=('Synthesis_Output','Created During Gameplay')  # mods whose old Experience outputs are ignored
prof=f'{MO2}/profiles/{PROFILE}'
mods=[l[1:].rstrip('\n') for l in open(f'{prof}/modlist.txt', encoding='utf-8') if l.startswith('+')]
mods.reverse()  # modlist top = highest priority; apply lowest first
shutil.rmtree(OUT, ignore_errors=True); os.makedirs(OUT)
win={}  # relpath(lower) -> (relpath, src)
def scan(root, skip_skse=False):
    for dp, dn, fn in os.walk(root):
        rel=os.path.relpath(dp, root)
        for f in fn:
            r=os.path.normpath(os.path.join(rel, f))
            lo=r.lower()
            top=lo.split(os.sep)[0]
            keep = (os.sep not in lo and lo.endswith(('.esp','.esm','.esl','.bsa'))) or \
                   lo.startswith(os.path.join('skse','plugins','skypatcher','npc')+os.sep) or \
                   lo == os.path.join('skse','plugins','experience.ini') or \
                   lo.startswith(os.path.join('skse','plugins','experience')+os.sep)
            if keep and not (skip_skse and lo.startswith('skse')): win[lo]=(r, os.path.join(dp, f))
scan(GAME)
for m in mods:
    p=f'{MO2}/mods/{m}'
    if os.path.isdir(p): scan(p, skip_skse=(m in STALE))
scan(f'{MO2}/overwrite')
n=0
for lo,(r,src) in win.items():
    if lo.startswith('skse'):
        parts=r.split(os.sep)
        canon={'skse':'SKSE','plugins':'Plugins','experience':'Experience','experience.ini':'Experience.ini','races':'Races','actors':'Actors','skypatcher':'SkyPatcher','default.ini':'Default.ini'}
        parts=[canon.get(p.lower(), p) if i<4 or p.lower()=='default.ini' else p for i,p in enumerate(parts)]
        r=os.sep.join(parts)
    dst=os.path.join(OUT, r)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if lo.startswith('skse'): shutil.copy2(src, dst)   # copies: patcher writes here
    else: os.symlink(src, dst)
    n+=1
print('staged', n, 'files')
IMPLICIT=['Skyrim.esm','Update.esm','Dawnguard.esm','HearthFires.esm','Dragonborn.esm','ccBGSSSE001-Fish.esm',
          'ccQDRSSE001-SurvivalMode.esl','ccqdrsse002-firewood.esl','ccBGSSSE037-Curios.esl','ccBGSSSE025-AdvDSGS.esm','_ResourcePack.esl']
active=[l.strip() for l in open(f'{prof}/plugins.txt', encoding='utf-8') if l.startswith('*')]
with open(os.path.join(os.path.dirname(OUT), 'plugins.txt'), 'w') as f:
    f.writelines(f'*{p}\n' for p in IMPLICIT)
    f.writelines(f'{p}\n' for p in active)
print('wrote plugins.txt with', len(IMPLICIT)+len(active), 'plugins')
