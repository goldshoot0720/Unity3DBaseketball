"""Install a supplied rigged FBX as a game character. Standard library only, no Blender.

    python3 Tools/prepare_rigged_characters.py MiaByBy3D=~/Downloads/Miabyby-pose.fbx

For each `CharacterId=source.fbx` pair this writes
`Assets/Art/Characters/<CharacterId>/<CharacterId>.fbx` plus `<CharacterId>_BaseColor.png`,
taking the colour map from the image the exporter embedded in the FBX and then dropping that
embedded copy. Unity imports the models with materialImportMode=None, so it never reads the
embedded media; carrying it would just duplicate several megabytes per character.

The models are used as delivered otherwise - same geometry, same mixamorig skeleton, same
bind pose. Property payloads are copied as raw bytes and never re-encoded, so anything this
script does not explicitly remove survives byte for byte. `--check` proves that by rewriting
each source without edits and comparing.

New ids also have to be listed in MiaCourtAssets.CharacterIds before `Mia Court / Configure URP`.
"""
import os
import struct
import sys

HEADER = b'Kaydara FBX Binary  \x00\x1a\x00'
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TRAILER = 144  # zero word, version, 120 zero bytes and the closing magic


class Node:
    # A childless node may or may not carry a terminating null record, and which one the
    # exporter chose cannot be derived. Remember it so it can be written back.
    __slots__ = ('name', 'props', 'children', 'null_record')

    def __init__(self, name, props, children, null_record):
        self.name = name
        self.props = props
        self.children = children
        self.null_record = null_record


def _prop_len(d, p):
    """Bytes occupied by the property starting at p, type character included."""
    t = d[p:p + 1]
    if t == b'Y': return 3
    if t == b'C': return 2
    if t in b'IF': return 5
    if t in b'DL': return 9
    if t in b'fdlib': return 13 + struct.unpack_from('<I', d, p + 9)[0]
    if t in b'SR': return 5 + struct.unpack_from('<I', d, p + 1)[0]
    raise ValueError('unknown FBX property type %r at offset %d' % (t, p))


def _read_node(d, p, ver):
    wide = ver >= 7500
    if wide:
        end, nprops, plen = struct.unpack_from('<QQQ', d, p)
        nlen = d[p + 24]
        p += 25
    else:
        end, nprops, plen = struct.unpack_from('<III', d, p)
        nlen = d[p + 12]
        p += 13
    if end == 0: return None, p
    name = d[p:p + nlen]
    p += nlen
    props = []
    for _ in range(nprops):
        n = _prop_len(d, p)
        props.append(d[p:p + n])
        p += n
    children = []
    null_record = False
    while p < end:
        child, p = _read_node(d, p, ver)
        if child is None:
            null_record = True
            break
        children.append(child)
    return Node(name, props, children, null_record), end


def read(path):
    d = open(path, 'rb').read()
    if d[:23] != HEADER: raise ValueError(path + ' is not a binary FBX')
    ver = struct.unpack_from('<I', d, 23)[0]
    p = 27
    roots = []
    while True:
        node, p = _read_node(d, p, ver)
        if node is None: break
        roots.append(node)
    tail = d[p:]
    # footer id, alignment padding, fixed trailer; the padding is recomputed on write
    return ver, roots, tail[:16], tail[-TRAILER:]


def _write_node(out, node, ver):
    wide = ver >= 7500
    head = len(out)
    out += b'\0' * (25 if wide else 13)
    out += node.name
    for pr in node.props: out += pr
    plen = sum(len(pr) for pr in node.props)
    for child in node.children: _write_node(out, child, ver)
    if node.null_record: out += b'\0' * (25 if wide else 13)
    if wide: struct.pack_into('<QQQ', out, head, len(out), len(node.props), plen)
    else: struct.pack_into('<III', out, head, len(out), len(node.props), plen)
    out[head + (24 if wide else 12)] = len(node.name)


def write(path, ver, roots, footer_id, trailer):
    out = bytearray(HEADER + struct.pack('<I', ver))
    for node in roots: _write_node(out, node, ver)
    out += b'\0' * (25 if ver >= 7500 else 13)
    out += footer_id
    out += b'\0' * ((16 - ((len(out)) % 16)) % 16)  # the trailer starts 16-byte aligned
    out += trailer
    open(path, 'wb').write(bytes(out))
    return len(out)


def walk(nodes):
    for node in nodes:
        yield node
        for inner in walk(node.children): yield inner


def take_embedded_image(roots):
    """Return the one embedded image and remove it from the tree."""
    found = []
    for node in walk(roots):
        if node.name != b'Video': continue
        keep = []
        for child in node.children:
            if child.name == b'Content' and child.props:
                raw = child.props[0]
                if raw[:1] == b'R':
                    found.append(raw[5:5 + struct.unpack_from('<I', raw, 1)[0]])
                    continue
            keep.append(child)
        node.children = keep
    if len(found) != 1:
        raise ValueError('expected exactly one embedded image, found %d' % len(found))
    return found[0]


def install(character, source):
    folder = os.path.join(ROOT, 'Assets', 'Art', 'Characters', character)
    os.makedirs(folder, exist_ok=True)
    ver, roots, footer_id, trailer = read(source)
    image = take_embedded_image(roots)
    if image[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(source + ': embedded image is not a PNG')
    texture = os.path.join(folder, character + '_BaseColor.png')
    open(texture, 'wb').write(image)
    model = os.path.join(folder, character + '.fbx')
    size = write(model, ver, roots, footer_id, trailer)
    print('%-14s %s -> %.2f MB model, %.2f MB texture'
          % (character, os.path.basename(source), size / 1e6, len(image) / 1e6))


def check(source):
    """Rewrite without edits; a byte-identical result is the proof the writer is faithful."""
    ver, roots, footer_id, trailer = read(source)
    scratch = source + '.roundtrip'
    write(scratch, ver, roots, footer_id, trailer)
    same = open(source, 'rb').read() == open(scratch, 'rb').read()
    os.remove(scratch)
    print(('identical  ' if same else 'DIFFERENT  ') + os.path.basename(source))
    return same


if __name__ == '__main__':
    args = sys.argv[1:]
    if not args:
        raise SystemExit(__doc__)
    if args[0] == '--check':
        raise SystemExit(0 if all([check(os.path.expanduser(a)) for a in args[1:]]) else 1)
    for arg in args:
        character, _, source = arg.partition('=')
        if not source:
            raise SystemExit('expected CharacterId=path/to/source.fbx, got ' + arg)
        install(character, os.path.expanduser(source))
