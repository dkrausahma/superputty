"""Build the SuperPuTTY help: docs/*.md -> SuperPutty/help/ (static HTML, works offline from file://).

    python tools/help-docs/build.py          # creates tools/help-docs/.venv on first use, then builds

The generated site is committed, so building the app and the installer needs neither Python nor internet.
Rebuild after every change in docs/ and commit docs/ and SuperPutty/help/ together.
"""
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
VENV = os.path.join(HERE, ".venv")
PYTHON = os.path.join(VENV, "Scripts", "python.exe") if os.name == "nt" else os.path.join(VENV, "bin", "python")
SITE = os.path.normpath(os.path.join(HERE, "..", "..", "SuperPutty", "help"))
SHIM_URL = "https://unpkg.com/iframe-worker/shim"


def main():
    if not os.path.exists(PYTHON):
        print("creating", VENV)
        subprocess.check_call([sys.executable, "-m", "venv", VENV])
        subprocess.check_call([PYTHON, "-m", "pip", "install", "-q", "-r", os.path.join(HERE, "requirements.txt")])
    # --clean: no leftovers of deleted pages; --strict: broken links and missing pages fail the build
    subprocess.check_call([PYTHON, "-m", "mkdocs", "build", "--clean", "--strict", "-f", os.path.join(HERE, "mkdocs.yml")])

    # the offline plugin loads its search shim (iframe-worker, MIT) from unpkg.com: use the vendored copy instead
    shim_dir = os.path.join(SITE, "assets", "javascripts")
    shutil.copy(os.path.join(HERE, "vendor", "iframe-worker-shim.js"), os.path.join(shim_dir, "iframe-worker-shim.js"))
    shutil.copy(os.path.join(HERE, "vendor", "iframe-worker-LICENSE"), os.path.join(shim_dir, "iframe-worker-LICENSE"))
    for root, _, files in os.walk(SITE):
        for name in files:
            if name.endswith(".html"):
                path = os.path.join(root, name)
                text = open(path, encoding="utf-8").read()
                if SHIM_URL in text:
                    local = os.path.relpath(os.path.join(shim_dir, "iframe-worker-shim.js"), root).replace(os.sep, "/")
                    open(path, "w", encoding="utf-8", newline="\n").write(text.replace(SHIM_URL, local))

    # the help must not load anything from the internet (offline networks, privacy)
    external = []
    for root, _, files in os.walk(SITE):
        for name in files:
            if name.endswith((".html", ".css")):
                text = open(os.path.join(root, name), encoding="utf-8").read()
                for marker in ("fonts.googleapis.com", "fonts.gstatic.com", "cdn.jsdelivr.net", "unpkg.com"):
                    if marker in text:
                        external.append(os.path.relpath(os.path.join(root, name), SITE) + ": " + marker)
    if external:
        raise SystemExit("the help loads external resources:\n  " + "\n  ".join(external))
    print("help built:", SITE)


if __name__ == "__main__":
    main()
