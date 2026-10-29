"""Author two script-free Unity particle prefabs without starting Unity.

Uses the project's existing ParticleSystem serialization as a default-field template.
All emission, shape, color, size, material and renderer settings are authored here.
No source textures, animation events, camera scripts, colliders or audio components.
Existing output assets are only replaced with explicit --update-clash, limited to
the clash prefab and its materials. Import/playback is for manual acceptance.
"""
from pathlib import Path
import argparse
import math
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
ZERO = '{fileID: 0}'
WHITE = '{r: 1, g: 1, b: 1, a: 1}'


def bounds(text, name, indent):
    lines = text.splitlines(True)
    prefix = ' ' * indent + name + ':'
    start = next(i for i, line in enumerate(lines) if line.startswith(prefix))
    end = start + 1
    while end < len(lines):
        line = lines[end]
        depth = len(line) - len(line.lstrip(' '))
        if line.strip() and depth <= indent and not (depth == indent and line.lstrip().startswith('- ')):
            break
        end += 1
    return lines, start, end


def field(text, name, value, indent=2):
    lines, start, end = bounds(text, name, indent)
    body = str(value)
    replacement = ' ' * indent + name + ':' + (body if body.startswith('\n') else ' ' + body) + '\n'
    return ''.join(lines[:start]) + replacement + ''.join(lines[end:])


def module(text, name, changes):
    lines, start, end = bounds(text, name, 2)
    block = ''.join(lines[start:end])
    for key, value in changes.items():
        block = field(block, key, value, 4)
    return ''.join(lines[:start]) + block + ''.join(lines[end:])


def curve(keys, depth):
    p = ' ' * depth
    result = p + 'serializedVersion: 2\n' + p + 'm_Curve:\n'
    for time, value in keys:
        result += (p + '- serializedVersion: 3\n' + p + f'  time: {time}\n' + p + f'  value: {value}\n'
                   + ''.join(p + f'  {key}: {v}\n' for key, v in
                             [('inSlope', 0), ('outSlope', 0), ('tangentMode', 0), ('weightedMode', 0),
                              ('inWeight', .33333334), ('outWeight', .33333334)]))
    return result + ''.join(p + f'{key}: {v}\n' for key, v in [('m_PreInfinity', 2), ('m_PostInfinity', 2), ('m_RotationOrder', 4)])


def minmax(value, low=None, keys=None, depth=6):
    p = ' ' * depth
    mode = 1 if keys else 3 if low is not None else 0
    result = '\n' + p + f'serializedVersion: 2\n{p}minMaxState: {mode}\n{p}scalar: {value}\n{p}minScalar: {low if low is not None else value}\n'
    for name in ('maxCurve', 'minCurve'):
        result += p + name + ':\n' + curve(keys or [(0, 1), (1, 1)], depth + 2)
    return result.rstrip('\n')


def gradient(alpha, depth):
    p = ' ' * depth
    result = p + 'serializedVersion: 2\n'
    for i in range(8):
        a = alpha[i][1] if i < len(alpha) else 0
        result += p + f'key{i}: {{r: 1, g: 1, b: 1, a: {a}}}\n'
    for i in range(8):
        result += p + f'ctime{i}: {65535 if i == 1 else 0}\n'
    for i in range(8):
        result += p + f'atime{i}: {round(alpha[i][0] * 65535) if i < len(alpha) else 0}\n'
    return result + p + f'm_Mode: 0\n{p}m_NumColorKeys: 2\n{p}m_NumAlphaKeys: {len(alpha)}\n'


def color(alpha=None):
    result = '\n      serializedVersion: 2\n'
    result += f'      minMaxState: {1 if alpha else 0}\n      minColor: {WHITE}\n      maxColor: {WHITE}\n'
    for name in ('maxGradient', 'minGradient'):
        result += '      ' + name + ':\n' + gradient(alpha or [(0, 1), (1, 1)], 8)
    return result.rstrip('\n')


def guid(path):
    path = ROOT / path
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M).group(1)
    return uuid.uuid5(uuid.NAMESPACE_URL, 'zzz-pvp/parry-effects/' + path.relative_to(ROOT).as_posix()).hex


def meta(path, kind):
    target = Path(str(ROOT / path) + '.meta')
    if target.exists():
        return
    importer = {
        'prefab': 'PrefabImporter:\n  externalObjects: {}\n',
        'material': 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n',
        'shader': 'ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n',
        'folder': 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n',
    }[kind]
    target.write_text('fileFormatVersion: 2\nguid: ' + guid(path) + '\n' + importer
                      + '  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')


def write(path, content, kind, replace=False):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('w' if replace else 'x', encoding='utf-8', newline='\n') as output:
        output.write(content)
    meta(path, kind)
    print('Created', path)


def material(name, tint, intensity, shape):
    shader = guid('Assets/Art/Shader/ParryFlash.shader')
    return HEADER + f'''--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {ZERO}
  m_PrefabInstance: {ZERO}
  m_PrefabAsset: {ZERO}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}
  m_Parent: {ZERO}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Intensity: {intensity}
    - _Shape: {shape}
    m_Colors:
    - _Tint: {{r: {tint[0]}, g: {tint[1]}, b: {tint[2]}, a: {tint[3]}}}
  m_BuildTextureStacks: []
'''


def object_and_transform(go, tr, name, children=(), parent=0, components=()):
    refs = '\n'.join(f'  - component: {{fileID: {v}}}' for v in (tr, *components))
    child_refs = '\n' + '\n'.join(f'  - {{fileID: {v}}}' for v in children) if children else ' []'
    return f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {ZERO}
  m_PrefabInstance: {ZERO}
  m_PrefabAsset: {ZERO}
  serializedVersion: 6
  m_Component:
{refs}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {ZERO}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {ZERO}
  m_PrefabInstance: {ZERO}
  m_PrefabAsset: {ZERO}
  m_GameObject: {{fileID: {go}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children:{child_refs}
  m_Father: {{fileID: {parent}}}
  m_RootOrder: 0
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''


def particle(ps_template, renderer_template, index, spec, on_root=False):
    go, tr, ps_id, renderer_id = (10000 + index * 10 + n for n in range(4))
    if on_root:
        go, tr = 1000, 4000
    ps = ps_template
    # Preserve Unity's serialized default modules, but disable every optional module before authoring.
    for name in re.findall(r'^  (\w+Module):', ps, re.M):
        ps = module(ps, name, {'enabled': 1 if name == 'InitialModule' else 0})
    for name, value in {'m_GameObject': f'{{fileID: {go}}}', 'lengthInSec': .6, 'looping': 0,
                        'playOnAwake': 1, 'prewarm': 0, 'simulationSpeed': 1, 'stopAction': 0,
                        'cullingMode': 3, 'useUnscaledTime': 0, 'autoRandomSeed': 0,
                        'randomSeed': 7100 + index, 'useRigidbodyForVelocity': 0,
                        'moveWithTransform': 1, 'scalingMode': 0,
                        'startDelay': minmax(0, depth=4)}.items():
        ps = field(ps, name, value)
    spark = spec.get('speed', 0) > 0
    ps = module(ps, 'InitialModule', {
        'startLifetime': minmax(spec['life'], spec.get('life_min')),
        'startSpeed': minmax(spec.get('speed', 0), spec.get('speed_min')),
        'startSize': minmax(spec['size'], spec.get('size_min')),
        'startSizeY': minmax(1), 'startSizeZ': minmax(1), 'size3D': 0,
        'startRotation': minmax(spec.get('rotation', 0)), 'startRotationX': minmax(0),
        'startRotationY': minmax(0), 'rotation3D': 0, 'randomizeRotationDirection': 0,
        'gravityModifier': minmax(spec.get('gravity', .15 if spark else 0)), 'startColor': color(),
        'maxNumParticles': max(8, spec['count'] + 4)})
    burst = '\n    - serializedVersion: 2\n      time: 0\n      countCurve:' + minmax(spec['count'], depth=8)
    burst += '\n      cycleCount: 1\n      repeatInterval: 0.01\n      probability: 1'
    ps = module(ps, 'EmissionModule', {'enabled': 1, 'rateOverTime': minmax(0),
                  'rateOverDistance': minmax(0), 'm_BurstCount': 1, 'm_Bursts': burst})
    ps = module(ps, 'SizeModule', {'enabled': 1, 'separateAxes': 0,
                  'curve': minmax(1, keys=spec['size_curve'])})
    ps = module(ps, 'ColorModule', {'enabled': 1, 'gradient': color(spec['alpha'])})
    if spark:
        ps = module(ps, 'ShapeModule', {'enabled': 1, 'type': 0,
                    'm_Position': '{x: 0, y: 0, z: 0}', 'm_Rotation': '{x: 0, y: 0, z: 0}',
                    'm_Scale': '{x: 1, y: 0.75, z: 1}', 'radiusThickness': 1})
        lines, a, b = bounds(ps, 'ShapeModule', 2)
        shape = field(''.join(lines[a:b]), 'value', .025, 6)
        ps = ''.join(lines[:a]) + shape + ''.join(lines[b:])
    renderer = renderer_template
    mat = guid('Assets/Art/Materials/Parry/' + spec['material'] + '.mat')
    for name, value in {'m_GameObject': f'{{fileID: {go}}}',
                        'm_Materials': f'\n  - {{fileID: 2100000, guid: {mat}, type: 2}}',
                        'm_RenderMode': 1 if spark else 0, 'm_RenderAlignment': 0,
                        'm_MinParticleSize': 0, 'm_MaxParticleSize': 2,
                        'm_CameraVelocityScale': 0, 'm_VelocityScale': spec.get('stretch_speed', .035) if spark else 0,
                        'm_LengthScale': spec.get('stretch_length', 2.5) if spark else 1, 'm_SortingFudge': 0,
                        'm_Pivot': '{x: 0, y: 0, z: 0}', 'm_Flip': '{x: 0, y: 0, z: 0}',
                        'm_UseCustomVertexStreams': 0, 'm_VertexStreams': '00010304',
                        'm_EnableGPUInstancing': 0}.items():
        renderer = field(renderer, name, value)
    text = '' if on_root else object_and_transform(go, tr, spec['name'], parent=4000, components=(ps_id, renderer_id))
    return text + f'--- !u!198 &{ps_id}\n' + ps + f'--- !u!199 &{renderer_id}\n' + renderer


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--update-clash', action='store_true',
                        help='Replace only the clash prefab and its materials; keep their GUIDs.')
    update_clash = parser.parse_args().update_clash
    template = (ROOT / 'Assets/Resources/Prefabs/VFX/FX_hit_06.prefab').read_text(encoding='utf-8-sig')
    blocks = re.split(r'^--- !u!(\d+) &(\d+)\s*\n', template, flags=re.M)
    templates = {}
    for i in range(1, len(blocks), 3):
        templates.setdefault(int(blocks[i]), blocks[i + 2])
    materials = [('M_Parry_Warning_Cross', (1, .72, .035, 1), 2.1, 0),
                 ('M_Parry_Warning_Glow', (1, .48, .015, .6), 1.3, 1),
                 ('M_Parry_Clash_Core', (1, .82, 1, 1), 4.5, 0),
                 ('M_Parry_Clash_Glow', (1, .08, .48, .48), 2.2, 1),
                 ('M_Parry_Clash_Spark', (1, .48, .12, 1), 3.2, 2),
                 ('M_Parry_Clash_Flare', (1, .12, .72, 1), 4, 3),
                 ('M_Parry_Clash_Violet', (.7, .18, 1, 1), 3.8, 2),
                 ('M_Parry_Clash_Burst', (1, .32, .8, 1), 3.5, 4)]
    warning = [dict(name='Yellow_Cross', material=materials[0][0], life=.30, size=1.45, count=1,
                    size_curve=[(0, .28), (.12, 1.08), (.4, 1), (1, .78)],
                    alpha=[(0, 0), (.07, 1), (.56, .95), (1, 0)]),
               dict(name='Warm_Halo', material=materials[1][0], life=.26, size=.95, count=1,
                    size_curve=[(0, .3), (.2, 1), (1, 1.35)], alpha=[(0, 0), (.08, 1), (1, 0)])]
    # Brief white center, pink horizontal flare and two independently moving spark
    # populations. No camera effects or character overlays are part of this prefab.
    clash = [dict(name='Impact_Cross', material=materials[2][0], life=.12, size=1.6, count=1,
                  rotation=math.pi / 4, size_curve=[(0, .65), (.15, 1.15), (1, .3)],
                  alpha=[(0, 1), (.3, 1), (1, 0)]),
             dict(name='Impact_Halo', material=materials[3][0], life=.24, size=2.3, count=1,
                  size_curve=[(0, .35), (.15, .85), (1, 1.3)], alpha=[(0, .8), (.15, 1), (1, 0)]),
             dict(name='Flying_Steel_Sparks', material=materials[4][0], life=.42, life_min=.2,
                  size=.035, size_min=.012, speed=9, speed_min=3.5, count=40,
                  stretch_speed=.07, stretch_length=5, gravity=.12,
                  size_curve=[(0, .8), (.2, 1), (1, .02)], alpha=[(0, 1), (.35, 1), (1, 0)]),
             dict(name='Horizontal_Magenta_Flash', material=materials[5][0], life=.18, size=7, count=1,
                  size_curve=[(0, .65), (.12, 1), (1, .9)], alpha=[(0, 1), (.25, .9), (1, 0)]),
             dict(name='Violet_Blade_Streaks', material=materials[6][0], life=.28, life_min=.12,
                  size=.085, size_min=.035, speed=10, speed_min=4.5, count=18,
                  stretch_speed=.1, stretch_length=7, gravity=0,
                  size_curve=[(0, 1), (.3, .85), (1, .03)], alpha=[(0, 1), (.3, 1), (1, 0)]),
             dict(name='Pink_Radial_Impact', material=materials[7][0], life=.16, size=3.8, count=1,
                  rotation=.2, size_curve=[(0, .5), (.2, 1), (1, 1.2)],
                  alpha=[(0, 1), (.25, .9), (1, 0)])]
    prefabs = [('FX_Parry_Warning_Cross_Yellow', warning), ('FX_Parry_Clash_Sparks', clash)]
    if update_clash:
        materials = materials[2:]
        prefabs = prefabs[1:]
    paths = ['Assets/Art/Materials/Parry/' + name + '.mat' for name, *_ in materials]
    paths += ['Assets/Resources/Prefabs/VFX/' + name + '.prefab' for name, _ in prefabs]
    for path in paths:
        if (ROOT / path).exists() and not update_clash:
            raise SystemExit('Refusing to overwrite: ' + path)
    folder = 'Assets/Art/Materials/Parry'
    outputs = []
    for name, tint, intensity, shape in materials:
        outputs.append((folder + '/' + name + '.mat', material(name, tint, intensity, shape), 'material'))
    for name, layers in prefabs:
        # Put the main flash on the root so selecting the prefab exposes Unity's
        # particle preview controls; Play/Simulate with children includes every layer.
        text = HEADER + object_and_transform(1000, 4000, name,
            children=[10000 + i * 10 + 1 for i in range(1, len(layers))],
            components=(10002, 10003))
        for index, spec in enumerate(layers):
            text += particle(templates[198], templates[199], index, spec, on_root=index == 0)
        outputs.append(('Assets/Resources/Prefabs/VFX/' + name + '.prefab', text, 'prefab'))
    (ROOT / folder).mkdir(parents=True, exist_ok=True)
    meta(folder, 'folder')
    meta('Assets/Art/Shader/ParryFlash.shader', 'shader')
    for path, content, kind in outputs:
        write(path, content, kind, replace=update_clash)


if __name__ == '__main__':
    main()
