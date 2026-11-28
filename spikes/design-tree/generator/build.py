"""Build a ThoughtTree event log from the repository's design documents."""
import json, re, sys, uuid, glob, os, datetime

REPO = sys.argv[1]
OUT = sys.argv[2]
GH = "https://github.com/AdventureDevelopment/ThoughtTree/blob/main/"
NS = uuid.UUID("6f1b2c9e-1d2a-4c55-9a3e-7e0c5b3f2a10")
DEVICE = str(uuid.uuid5(NS, "device:tutor"))
USER = str(uuid.uuid5(NS, "user:owner"))
START = datetime.datetime(2026, 11, 27, 21, 0, tzinfo=datetime.timezone.utc)
events = []
clock = [int(START.timestamp() * 1000)]
action = [None]
last_child = {}
ids = {}


def iid(key):
    return str(uuid.uuid5(NS, key))


def emit(etype, **data):
    clock[0] += 1
    ts = datetime.datetime.fromtimestamp(clock[0] / 1000, datetime.timezone.utc)
    ev = {"eventId": str(uuid.uuid5(NS, f"ev:{clock[0]}")), "type": etype, "schema": 1,
          "deviceId": DEVICE, "actionId": action[0], "clock": clock[0],
          "timestamp": ts.isoformat(timespec="seconds")}
    ev.update(data)
    events.append(ev)


def begin(name):
    action[0] = iid("action:" + name)


def create(key, title, note="", parent=None):
    item = iid(key)
    ids[key] = item
    title = re.sub(r"\s+", " ", title).strip()[:1000]
    data = {"itemId": item, "parentId": parent or "", "afterItemId": last_child.get(parent or "", ""),
            "title": title, "format": "markdown"}
    if note:
        data["note"] = note.strip()[:50000]
    emit("item.created", **data)
    last_child[parent or ""] = item
    return item


def tag(item, tag_key):
    emit("item.tagged", itemId=item, tagId=ids[tag_key])


rel_count = [0]


def relate(src, type_key, dst):
    rel_count[0] += 1
    emit("relation.added", relationId=iid(f"rel:{src}:{type_key}:{dst}"), fromItemId=src,
         typeId=ids[type_key], toItemId=dst)


def read(path):
    with open(os.path.join(REPO, path), encoding="utf-8") as handle:
        return handle.read()


# ---------- link rewriting (done after all ids exist) ----------
pending_notes = []


def linkify(text, base="docs/"):
    def md_link(match):
        label, target = match.group(1), match.group(2)
        adr = re.search(r"(ADR-\d{3})", target)
        if adr and target.endswith(".md") and "adr/" in target or (adr and target.startswith("ADR-")):
            key = "adr:" + adr.group(1)
            if key in ids:
                return f"[{label}](item:{ids[key]})"
        if re.match(r"^(https?:|item:|#)", target):
            return match.group(0)
        path = os.path.normpath(os.path.join(base, target.split("#")[0])).replace("\\", "/")
        return f"[{label}]({GH}{path})"

    text = re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", md_link, text)
    parts = re.split(r"(\[[^\]]*\]\([^)]*\)|`[^`]*`)", text)
    out = []
    for part in parts:
        if part.startswith("[") or part.startswith("`"):
            out.append(part)
            continue
        part = re.sub(r"(?<![\w/-])(ADR-\d{3})(?![\w-])",
                      lambda m: f"[{m.group(1)}](item:{ids['adr:' + m.group(1)]})" if 'adr:' + m.group(1) in ids else m.group(1), part)
        part = re.sub(r"(?<![\w/-])(D-\d{1,3})(?![\w-])",
                      lambda m: f"[{m.group(1)}](item:{ids['d:' + m.group(1)]})" if 'd:' + m.group(1) in ids else m.group(1), part)
        out.append(part)
    return "".join(out)


def no_headings(text):
    return re.sub(r"(?m)^#{1,6}\s+(.*)$", r"**\1**", text)


# ---------- parse ADRs ----------
adrs = {}
for path in sorted(glob.glob(os.path.join(REPO, "docs/adr/ADR-*.md"))):
    raw = open(path, encoding="utf-8").read()
    title_line = raw.splitlines()[0]
    match = re.match(r"# (ADR-\d{3}): (.*)", title_line)
    adr_id, name = match.group(1), match.group(2)
    head = re.search(r"id: .*?· status: (.*?) · kind: (\w+)", raw)
    status, kind = head.group(1).strip(), head.group(2)
    body = raw.split("\n", 1)[1]
    body = re.sub(r"(?m)^id: .*$", "", body)
    adrs[adr_id] = {"name": name, "status": status, "kind": kind, "body": body.strip(),
                    "file": os.path.relpath(path, REPO)}

ADR_TOPICS = [
    ("Events and stored data", "Event log, event shapes, ordering, storage and the formats release 1 freezes",
     [2, 4, 5, 9, 14, 16, 17, 22, 23, 29, 38, 85, 86, 87, 88]),
    ("Sync and exchange", "How devices exchange events: by file, through the server, and in which modes",
     [3, 6, 7, 21, 37, 57]),
    ("Trees, devices and sharing", "Standalone devices, several trees, pairing, sharing, keys and tree access",
     [8, 10, 11, 12, 13, 20, 28, 69]),
    ("Security and privacy", "Encryption, metadata protection and what the server and printed output may see",
     [15, 30, 43, 44, 45, 52, 73, 89]),
    ("Outline features", "Conflicts, relations, tags, undo, themes, views, focus, inbox, templates, headings",
     [18, 24, 25, 26, 58, 59, 60, 62, 65, 66, 67, 68, 72, 77, 79, 81, 82, 83, 84]),
    ("Export and import", "Readable export and import of trees and branches", [19, 78, 80]),
    ("Client and UI", "Blazor client, texts, accessibility, updates, switches, fonts and how UI work follows the UX concept",
     [1, 31, 32, 33, 36, 41, 42, 61, 63, 64]),
    ("Code and test harness", "Project boundaries, deterministic time and concurrency, the protected harness",
     [27, 34, 35, 39, 53, 54, 75, 76]),
    ("Server and operations", "Server library, infrastructure, container image, supply chain and migrations",
     [40, 46, 47, 48, 56]),
    ("Scope, licences, feedback and AI", "Release 1 scope, licences, reports and feedback, no AI on content",
     [49, 50, 51, 55, 70, 71, 74]),
]
mapped = {f"ADR-{n:03d}" for _, _, nums in ADR_TOPICS for n in nums}
missing = sorted(set(adrs) - mapped)
assert not missing, missing

# bet cards
bets_raw = read("docs/adr/bets.md")
bet_cards = {}
for chunk in re.split(r'<a id="adr-\d{3}"></a>', bets_raw)[1:]:
    match = re.search(r"## (ADR-\d{3}):", chunk)
    if match:
        card = chunk.split("\n", 2)[2] if chunk.startswith("\n\n##") else chunk
        card = re.sub(r"(?m)^## .*$", "", chunk).strip()
        bet_cards[match.group(1)] = card

# ---------- parse UX decision log ----------
ux = read("docs/ux-concept.md")
decisions = []
for line in ux.splitlines():
    match = re.match(r"^\| (D-\d+) \| ([\d-]+) \| (.*?) \| (.*) \| (.*?) \|$", line)
    if match:
        decisions.append(match.groups())


def ux_topic(text):
    first = re.split(r",| \(", text)[0].strip()
    match = re.match(r"^(\d+) (.*)$", first)
    if match:
        names = {"1": "Principles", "2": "Undo", "3": "Component library", "4": "Tokens and theming",
                 "5": "Layout", "6": "Touch interaction", "7": "Information architecture",
                 "8": "States and feedback", "9": "Onboarding", "10": "Microcopy", "11": "UX with agents"}
        return f"{int(match.group(1)):02d} {names.get(match.group(1), match.group(2))}"
    return "~" + first


def section(text, start, end=None):
    begin_index = text.index(start)
    stop = text.index(end, begin_index + len(start)) if end else len(text)
    return text[begin_index + len(start):stop].strip()


arch = read("docs/architecture.md")

# ---------- assign ids for linking before building ----------
for adr_id in adrs:
    ids["adr:" + adr_id] = iid("adr:" + adr_id)
for d in decisions:
    ids["d:" + d[0]] = iid("d:" + d[0])

# ---------- build ----------
begin("setup")
emit("device.joined", deviceId=DEVICE, deviceLabel="Tutor session", userId=USER, userName="Owner")

notes_to_link = []


def C(key, title, note="", parent=None):
    item = create(key, title, linkify(no_headings(note)) if note else "", parent)
    return item


# 1. Vision and goals
begin("vision")
root = C("vision", "🌱 ThoughtTree design",
         "Vision and goals: what ThoughtTree is for, in the owner's words and as quality goals. Source: "
         f"[ux-concept.md]({GH}docs/ux-concept.md), [architecture.md]({GH}docs/architecture.md) section 1.")
C("vision:words", "Vision in the owner's words", section(ux, "## Vision in the owner's words", "### What the owner"), root)
C("vision:wanted", "What the owner always wanted and never found", section(ux, "### What the owner always wanted and never found", "## UX principles"), root)
s1 = section(arch, "## 1. Introduction and Goals", "## 2. Constraints")
for label, nxt in [("**Key requirements**", "**Quality goals**"), ("**Quality goals**", "**Stakeholders**"), ("**Stakeholders**", None)]:
    body = section(s1, label, nxt) if nxt else section(s1, label)
    C("vision:" + label, label.strip("*"), body, root)

# 2. UX concept
begin("ux")
ux_root = C("ux", "🧭 UX concept",
            f"Principles, terms, journeys and the decision log D-1 to D-{len(decisions)}. Source: [ux-concept.md]({GH}docs/ux-concept.md). Where code and a UX decision differ, the decision wins (ADR-064).")
principles = C("ux:principles", "UX principles", "Derived from journeys A to D (D-16, D-24). They guide every later decision, including the agents'.", ux_root)
for match in re.finditer(r"(?m)^(\d+)\. \*\*(.+?)\*\* (.*)$", section(ux, "## UX principles", "## Undo (topic 2)")):
    C(f"ux:principle:{match.group(1)}", f"{match.group(1)}. {match.group(2).rstrip('.')}", match.group(3), principles)
C("ux:terms", "Terms", section(ux, "## Terms", "## Vision in the owner's words"), ux_root)
journeys = C("ux:journeys", "Journeys", "Four situations the concept was derived from.", ux_root)
jtext = section(ux, "## Journeys", "## Open questions")
for match in re.finditer(r"### (Journey [A-D]) – (.*?)\n(.*?)(?=\n### |\Z)", jtext, re.S):
    C("ux:" + match.group(1), f"{match.group(1)}: {match.group(2)}", match.group(3), journeys)
for head, nxt in [("## Undo (topic 2)", "## Component library"), ("## Component library (topic 3)", "## Design tokens"),
                  ("## Design tokens and theming (topic 4)", "## UX with agents"), ("## UX with agents (topic 11)", "## How this concept")]:
    C("ux:" + head, head.lstrip("# "), section(ux, head, nxt), ux_root)
open_ux = C("ux:open", "Open questions (UX)", "The open questions of the UX concept, in groups as written there.", ux_root)
group = None
for block in re.split(r"\n\s*\n", section(ux, "## Open questions", "## Decision log")):
    block = block.strip()
    bullet = re.match(r"^- (.+?)(?: \(([^)]*)\))?: (.*)$", block, re.S)
    header = re.match(r"^([^\n]+?)(?: \((.*)\))?:$", block)
    if bullet:
        C("ux:open:" + bullet.group(1), bullet.group(1), (f"*{bullet.group(2)}*\n\n" if bullet.group(2) else "") + bullet.group(3), open_ux)
        group = None
    elif header:
        group = C("ux:open:" + header.group(1), header.group(1), header.group(2) or "", open_ux)
    else:
        for match in re.finditer(r"(?ms)^(\d+)\. (.*?)(?=^\d+\. |\Z)", block):
            text = match.group(2).strip()
            first = re.split(r"(?<=[a-z\)])[.;:] ", text.split("\n")[0], maxsplit=1)[0]
            C(f"ux:open:q{match.group(1)}", f"{match.group(1)}. {first[:120]}{'…' if len(first) > 120 else ''}",
              text, group or open_ux)
log = C("ux:log", "Decision log", f"{len(decisions)} UX decisions, grouped by their first topic. Each title starts with its id; the date and source are in the note.", ux_root)
topic_items = {}
for d_id, date, topic, text, source in decisions:
    key = ux_topic(topic)
    if key not in topic_items:
        topic_items[key] = None
for key in sorted(topic_items, key=lambda k: (not k[0].isdigit(), k)):
    topic_items[key] = C("ux:topic:" + key, key.lstrip("~"), "", log)
for d_id, date, topic, text, source in decisions:
    first = re.split(r"(?<=[a-z\)])[.;:] ", text, maxsplit=1)[0]
    title = f"{d_id} {first[:140]}{'…' if len(first) > 140 else ''}"
    note = f"{text}\n\n- Date: {date}\n- Topic: {topic}\n- Source: {source}"
    item = C("d:" + d_id, title, note, topic_items[ux_topic(topic)])
    decisions_item = item

# 3. Architecture (arc42)
begin("arch")
arch_root = C("arch", "🏛 Architecture (arc42)", f"The twelve arc42 sections of [architecture.md]({GH}docs/architecture.md). Section 9's ADR index and open decisions are the branches \"Decisions (ADRs)\" and \"Open decisions\".", None)
heads = list(re.finditer(r"(?m)^## (\d+)\. (.*)$", arch))
for index, match in enumerate(heads):
    number, name = match.group(1), match.group(2)
    end = heads[index + 1].start() if index + 1 < len(heads) else len(arch)
    body = arch[match.end():end].strip()
    if number == "9":
        intro = "Each decision is a separate record in docs/adr. See the branches \"Decisions (ADRs)\" and \"Open decisions\"; release 1 scope is below."
        sec = C("arch:9", f"{number}. {name}", intro, arch_root)
        r1 = section(arch, "**Release 1 scope**")
        r1 = r1.split("\n## ")[0]
        C("arch:9:r1", "Release 1 scope", r1, sec)
        continue
    subs = list(re.finditer(r"(?m)^\*\*([^*\n]+)\*\*(.*)$", body))
    intro = body[:subs[0].start()].strip() if subs else body
    sec = C(f"arch:{number}", f"{number}. {name}", intro, arch_root)
    for sub_index, sub in enumerate(subs):
        sub_end = subs[sub_index + 1].start() if sub_index + 1 < len(subs) else len(body)
        content = (sub.group(2).strip() + "\n\n" + body[sub.end():sub_end].strip()).strip()
        C(f"arch:{number}:{sub.group(1)}", sub.group(1).strip(), content, sec)

# 4. Decisions (ADRs)
begin("adrs")
adr_root = C("adrs", "📜 Decisions (ADRs)", f"All {len(adrs)} architecture decision records, grouped by area. Each note holds the rules and the reason; bets carry their bet card. Format: [docs/adr/README.md]({GH}docs/adr/README.md).")
adr_items = {}
for area, description, nums in ADR_TOPICS:
    area_item = C("adr-area:" + area, area, description, adr_root)
    for n in nums:
        adr_id = f"ADR-{n:03d}"
        adr = adrs[adr_id]
        note = adr["body"]
        if adr_id in bet_cards:
            note += "\n\n**Bet card**\n\n" + bet_cards[adr_id]
        note += f"\n\nSource: [{os.path.basename(adr['file'])}]({GH}{adr['file']})"
        adr_items[adr_id] = create("adr:" + adr_id, f"{adr_id} {adr['name']}", linkify(no_headings(note), "docs/adr/"), area_item)

# 5. Open decisions
begin("open")
open_root = C("open", "❓ Open decisions", f"The table \"Open decisions\" in [architecture.md]({GH}docs/architecture.md) section 9. Tagged open or decided by its \"Settle\" column.")
open_rows = []
otext = section(arch, "**Open decisions**", "**Release 1 scope**")
for line in otext.splitlines():
    match = re.match(r"^\| (.+?) \| (.+) \| (.+?) \|$", line)
    if match and match.group(1) not in ("Topic", "---"):
        open_rows.append(match.groups())
open_items = {}
for topic, options, settle in open_rows:
    item = C("open:" + topic, topic, f"{options}\n\n**Settle:** {settle}", open_root)
    open_items[topic] = (item, settle)

# 6. Work queue
begin("plan")
plan_root = C("plan", "🗂 Next work", f"The work queue of [docs/plan/next-work.md]({GH}docs/plan/next-work.md), first item next.")
plan = read("docs/plan/next-work.md")
for match in re.finditer(r"(?ms)^(\d+)\. \*\*(.+?)\*\*(.*?)(?=^\d+\. \*\*|\Z)", plan):
    C("plan:" + match.group(1), f"{match.group(1)}. {match.group(2).rstrip('.: ')}", re.sub(r"\n\s+", " ", match.group(3)).strip(" :"), plan_root)

# 7. Tags branch
begin("tags")
tags_root = create("tags", "Tags")
emit("tree.tags-set", itemId=tags_root)
TAGS = [
    ("tag:adr", "ADR", "An architecture decision record", None),
    ("tag:constraint", "constraint", "ADR kind: obey it; only a human changes it", "tag:adr"),
    ("tag:bet", "bet", "ADR kind: firm rules, challenged only through its bet card", "tag:adr"),
    ("tag:accepted", "accepted", "ADR status Accepted", "tag:adr"),
    ("tag:superseded", "superseded", "ADR status Superseded", "tag:adr"),
    ("tag:proposed", "proposed", "ADR status Proposed: an agent's proposal awaiting the owner", "tag:adr"),
    ("tag:ux", "UX decision", "A decision D-n of the UX concept's decision log", None),
    ("tag:open", "open", "Not decided yet", None),
    ("tag:decided", "decided", "Decided by the owner", None),
    ("tag:learning", "learning session", "Came from the tutor session on event-sourcing patterns and versioning, 2026-11-27", None),
    ("tag:feature", "feature request", "Something this tree needed that the planned features cannot express", None),
]
for key, title, note, parent in TAGS:
    create(key, title, note, ids[parent] if parent else tags_root)

# 8. Relation types branch
begin("types")
types_root = create("types", "Relation types")
emit("tree.relation-types-set", itemId=types_root)
TYPES = [
    ("type:related", "related", "Two thoughts belong together", True, ""),
    ("type:amends", "amends", "This decision changes some rules of the other", False, "amended by"),
    ("type:supersedes", "supersedes", "This decision replaces the other entirely", False, "superseded by"),
    ("type:would-amend", "would amend", "A question or decision from the inbox that, once settled, changes the other", False, "would be amended by"),
]
for key, title, note, symmetric, reverse in TYPES:
    item = create(key, title, note, types_root)
    emit("relation-type.set", itemId=item, symmetric=symmetric, reverseName=reverse, replaces=[])
emit("tree.default-relation-type-set", itemId=ids["type:related"])

# tagging and ADR relations
begin("tagging")
for adr_id, adr in adrs.items():
    item = adr_items[adr_id]
    tag(item, "tag:adr")
    tag(item, "tag:" + adr["kind"])
    status = adr["status"].lower()
    tag(item, "tag:superseded" if status.startswith("superseded") else "tag:proposed" if status.startswith("proposed") else "tag:accepted")
    head = adr["body"].split("\n- **")[0]
    head = re.sub(r"\([^()]*\)", "", head)
    current = None
    seen_targets = set()
    for token in re.finditer(r"\b(Supersedes|supersedes|Amends|amends|Amended by|amended by|Superseded by|superseded by|owner decision|Owner decision)\b|ADR-\d{3}", head):
        word = token.group(1)
        if word:
            low = word.lower()
            current = "type:supersedes" if low == "supersedes" else "type:amends" if low == "amends" else None
            continue
        target = token.group(0)
        if current and target != adr_id and target in adr_items and target not in seen_targets:
            seen_targets.add(target)
            relate(item, current, adr_items[target])
for d_id, *_ in decisions:
    tag(ids["d:" + d_id], "tag:ux")
for topic, (item, settle) in open_items.items():
    tag(item, "tag:decided" if settle.lower().startswith("decided") else "tag:open")

# 9. Inbox: learning session and feature requests
begin("inbox")
inbox = create("inbox", "Inbox", "Captured, not yet sorted. Holds the questions and decisions of the learning session (tag \"learning session\") and what this tree could not express (tag \"feature request\").")
emit("tree.inbox-set", itemId=inbox)


def L(key):
    return f"item:{ids[key]}"


A = lambda n: f"[ADR-{n:03d}]({L('adr:ADR-%03d' % n)})"
ERASE = L("open:Archive, content and erasure")
learning = [
    ("learn:names", "Device labels and user names are erasable, like titles and notes",
     f"Owner decision, 2026-11-27, in the learning session. Their text moves to payloads; `deviceId` and `userId` stay in the events, because the agreed order, `seen`, the authority rules ({A(11)}) and the private-or-shared derivation use them. Extends the open decision [Archive, content and erasure]({ERASE}).",
     ["tag:decided"], ["open:Archive, content and erasure", "adr:ADR-011", "adr:ADR-015", "adr:ADR-016"]),
    ("learn:payloads", "Every text a person typed moves to a payload",
     f"Forgettable Payloads (Verraes), applied to all typed text, not only titles and notes:\n\n- `item.created`: title, note\n- `item.title-changed`, `item.note-changed`\n- `relation.note-changed`: note\n- `relation-type.set`: reverseName\n- `view.set`: name, perhaps query\n- `device.joined`: deviceLabel, userName\n- `device.label-changed`, `user.name-changed`\n\nOne payload per event, id derived from the event id. Allowed before release 1 ({A(85)}), forbidden after ({A(16)}).",
     ["tag:open"], ["open:Archive, content and erasure", "adr:ADR-016", "adr:ADR-017", "adr:ADR-015"]),
    ("learn:ids", "Are device and user ids erasable too?",
     "Ids are pseudonymous; by the lawyer's view quoted by Verraes still personal data. Keeping them preserves ordering, `seen` and authority rules; erasing them would need replacement ids.",
     ["tag:open"], ["adr:ADR-011", "adr:ADR-014"]),
    ("learn:who", "Who may erase a name?",
     f"Options: only the name's owner; any member of the tree (equal access, {A(12)}); the owner, plus anyone once the device has left.\n\n- Ben wants \"Ben Keller\" gone from \"Flat move\". Owner-only: he can; any member: Anna could too.\n- Ben deleted the app: owner-only means nobody can erase his name.\n- Misuse: someone erases everyone's names to hide who wrote what.\n\nThe {A(12)} bet assumes nobody needs revocation; the Ben scenes touch its review trigger.",
     ["tag:open"], ["adr:ADR-011", "adr:ADR-012"]),
    ("learn:trees", "Erase a name in one tree or in all trees of the device?",
     "Anna's phone was labelled \"Anna – therapy notes\" by mistake; the label reached \"Private\", \"Flat move\" and \"Book club\". Per tree: three actions. All trees: one, perhaps more than meant. A tree she has left is out of reach either way.",
     ["tag:open"], ["adr:ADR-010", "adr:ADR-011"]),
    ("learn:limits", "How the app states the limits of erasing",
     "Erasure relies on devices syncing. Ben's tablet, offline for two months, keeps the old name until it syncs; his spring Markdown export keeps it for good. Possible line: \"Erased on all devices that sync. Devices that are offline, and files exported earlier, keep it.\" New UI, sketch first.",
     ["tag:open"], ["adr:ADR-064", "adr:ADR-019"]),
    ("learn:older", "Erase older values too?",
     "\"Anna's iPhone\", then \"Anna – therapy notes\", then \"Phone\": erasing only the current value leaves the middle one in the history.",
     ["tag:open"], ["open:Archive, content and erasure"]),
    ("learn:after", "What history and the Conflict View show after erasure",
     "\"Title changed by Erased user on Erased device\". Two erased devices in one conflict both read \"Erased device\"; a neutral \"Device 1\" and \"Device 2\" keeps them apart. New UI, sketch first.",
     ["tag:open"], ["adr:ADR-018", "adr:ADR-064"]),
    ("learn:pertree", "Device label and user name per tree: A, B or C",
     "- A: one everywhere (today). Set once; every label reaches every tree.\n- B: names per tree, ids global. \"Book reader\", \"Planning iPad\", \"secret vault\"; a private label stays in its tree; erasing is per tree; still linkable by id.\n- C: names and ids per tree. Unlinkable personas; pairing's \"same person?\" step and the authority rules need a new basis.\n\nAway from A, the fields move to This tree…, a sketch first.",
     ["tag:open"], ["d:D-187", "d:D-188", "d:D-193", "adr:ADR-011", "adr:ADR-028"]),
    ("learn:conflicts", "Conflicts when a payload is missing or erased",
     f"A conflict needs differing values; equal values are no conflict ({A(18)}). With payloads, the projection reads them to compare. Open: does a missing or erased payload count as different?",
     ["tag:open"], ["adr:ADR-018"]),
    ("learn:complete", "Structure first, text later",
     f"With payloads the log alone no longer rebuilds the tree (Verraes: Completeness Guarantee). A device may hold structure without text. Spike 2, case (4), measures this; see {A(2)}.",
     ["tag:open"], ["adr:ADR-002", "plan:1"]),
    ("learn:versioning", "Next lesson: versioning and renaming item.deleted",
     f"Greg Young's \"Versioning in an Event Sourced System\": upcasting, weak schema, versioned types, double write, copy-and-replace, copy-transform. Then replacing `item.deleted` and `item.restored` by `item.lifecycle-changed` while {A(85)} still allows format changes, and stored beta data.",
     ["tag:open"], ["adr:ADR-016", "adr:ADR-085", "adr:ADR-005"]),
]
learn_root = create("learn", "Learning session 2026-11-27: erasure and payloads", f"Questions and one decision from the tutor session. Sorted out of the inbox, they would land in [Archive, content and erasure]({ERASE}) and the ADRs they would amend.", inbox)
tag(learn_root, "tag:learning")
for key, title, note, tag_keys, targets in learning:
    item = create(key, title, note, learn_root)
    tag(item, "tag:learning")
    for tag_key in tag_keys:
        tag(item, tag_key)
    for target in targets:
        relate(item, "type:would-amend" if not target.startswith("plan:") else "type:related", ids[target])

features = [
    ("feature:date", "A date of its own on a thought",
     "Every ADR, UX decision and open decision has a decision date. The only date in an event is its `Timestamp`, which says when this tree was built, not when something was decided, and it is for display only. So the dates sit in titles and notes, where they cannot be sorted or filtered."),
    ("feature:exclusive", "Tags that exclude each other",
     "An ADR is accepted, proposed or superseded, never two at once. Tags allow any combination; nothing keeps a decision from carrying both \"accepted\" and \"superseded\"."),
    ("feature:keep-in-step", "Keep a tree in step with documents it did not export",
     f"This tree is a copy of the repository's documents as of 2026-11-27. Update import ({A(78)}) re-reads only files this app exported; when an ADR changes in the repository, nothing brings the change in without building the tree again."),
    ("feature:tables", "Tables in notes",
     "Many sources are tables (event types, glossary, decision log). Notes are \"Markdown without headings\"; whether that includes tables is not stated. Some notes here keep their tables as written."),
]
for key, title, note in features:
    item = create(key, title, note, inbox)
    tag(item, "tag:feature")

with open(OUT, "w", encoding="utf-8") as handle:
    handle.write("[\n" + ",\n".join(json.dumps(ev, ensure_ascii=False) for ev in events) + "\n]\n")
counts = {}
for ev in events:
    counts[ev["type"]] = counts.get(ev["type"], 0) + 1
print(len(events), counts, os.path.getsize(OUT))
