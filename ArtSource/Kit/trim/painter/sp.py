# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance ... you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Sends a script to Painter through the envart_remote plugin and prints the reply (copied from NorseFPS).
# -----------------------------------------------------------------------------
"""Client for the envart_remote Painter plugin (Documents/Adobe/.../python/startup/envart_remote.py): python sp.py <script.py>. Copied from NorseFPS, where it was written; prints the JSON reply."""
import json
import os
import sys
import urllib.request

code = open(sys.argv[1], encoding="utf-8").read()
token = open(os.path.join(os.path.expanduser("~"), ".envart_painter_token"), encoding="utf-8").read().strip()
req = urllib.request.Request("http://[::1]:60050/exec", data=code.encode("utf-8"), headers={"X-Token": token}, method="POST")
reply = json.loads(urllib.request.urlopen(req, timeout=3600).read())
sys.stdout.reconfigure(encoding="utf-8")
if reply.get("stdout"):
    print(reply["stdout"])
if not reply.get("ok"):
    print("ERROR:\n" + reply.get("error", ""))
    sys.exit(1)
print(json.dumps(reply.get("result"), indent=1, default=str))
