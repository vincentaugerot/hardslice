"""Reads a HardSlice .nupkg for the release scripts, with the standard library only.

digest <nupkg>: prints the SHA-512, in base64, of the content of the package: every entry, by name and SHA-512 of its bytes,
in the order of the names, except the repository signature .signature.p7s. NuGet.org adds that signature to every package it
accepts, so the file it serves never has the bytes of the file pushed: the content digest is the same for both.

metadata <nupkg> <version>: fails unless the package ships its README and its icon at its root, its nuspec names them, and its
release notes are the changelog at the tag of the version, never at a branch that moves.
"""

import base64
import hashlib
import sys
import xml.etree.ElementTree as ElementTree
import zipfile

SIGNATURE = ".signature.p7s"
REPOSITORY = "https://github.com/vincentaugerot/hardslice"


def digest(path):
    content = hashlib.sha512()
    with zipfile.ZipFile(path) as package:
        for name in sorted(entry.filename for entry in package.infolist() if entry.filename != SIGNATURE):
            content.update(name.encode("utf-8") + b"\0" + hashlib.sha512(package.read(name)).digest())
    return base64.b64encode(content.digest()).decode("ascii")


def metadata(path, version):
    errors = []
    with zipfile.ZipFile(path) as package:
        names = set(package.namelist())
        nuspecs = [name for name in names if name.endswith(".nuspec") and "/" not in name]
        if len(nuspecs) != 1:
            return [f"{path} holds {len(nuspecs)} nuspec files at its root instead of one."]
        root = ElementTree.fromstring(package.read(nuspecs[0]))
    fields = {element.tag.rsplit("}", 1)[-1]: (element.text or "").strip() for element in root.iter()}
    for field, file in (("readme", "README.md"), ("icon", "icon.png")):
        if fields.get(field) != file:
            errors.append(f"{path}: its nuspec names {fields.get(field)!r} as {field} instead of {file}.")
        if file not in names:
            errors.append(f"{path} does not ship {file} at its root.")
    expected_notes = f"{REPOSITORY}/blob/v{version}/CHANGELOG.md"
    if fields.get("releaseNotes") != expected_notes:
        errors.append(f"{path}: its release notes are {fields.get('releaseNotes')!r} instead of {expected_notes}.")
    return errors


def main(arguments):
    if len(arguments) == 2 and arguments[0] == "digest":
        print(digest(arguments[1]))
        return 0
    if len(arguments) == 3 and arguments[0] == "metadata":
        errors = metadata(arguments[1], arguments[2])
        for error in errors:
            print(f"error: {error}", file=sys.stderr)
        return 1 if errors else 0
    print("usage: nupkg.py digest <nupkg> | metadata <nupkg> <version>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
