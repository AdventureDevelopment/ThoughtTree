"""Published site as a workflow artifact: digest, fetch and check, without executing anything from it.

Run with `python3 -I` (isolated: no current directory or environment on the module path). Standard library only.

  digest <folder>
      Prints the SHA-256 digest of a published site: the SHA-256 of the sorted lines "<sha256 of file>  <path>\n".
      Independent of zip packing, so the same files give the same digest in every run.

  fetch --run-id N --workflow PATH --event EVENT [--branch BRANCH] --dest FOLDER [--expect-sha256 HEX]
      Checks with the GitHub API that run N is a successful run of workflow PATH, started by EVENT, in this
      repository, not from a fork; with --branch also that BRANCH is the default branch, no tag has its name, and
      the run's commit is in BRANCH (compared with the branch's head commit), so a run of a tag named like the
      branch is refused; finds its one artifact named site-sha256-<hex>; downloads the zip
      without handing the GitHub token to the storage host; checks the size, every entry name (no absolute path,
      no "..", no backslash, no link, no duplicate) and the unpacked total; unpacks into FOLDER (must not exist);
      and checks that the digest of the unpacked files equals the one in the artifact name (and --expect-sha256).
      Prints sha256=<hex>, commit=<sha> and run=<id> for $GITHUB_OUTPUT.

  preview-target --run-id N
      For a successful preview-build run of this repository: the one open pull request it built, if the run built
      the pull request's current head. Prints pull_request=<number> and skip=false, or skip=true when the pull
      request moved on or closed (a newer run follows). The number comes from the API, never from the artifact.

  pull-request-state N
      Prints open or closed.

Needs GITHUB_REPOSITORY, GITHUB_API_URL and GH_TOKEN (the job's own token with actions: read and
pull-requests: read).
"""

import argparse
import hashlib
import json
import os
import pathlib
import re
import stat
import sys
import urllib.error
import urllib.request
import zipfile

# The Free plan allows 250 MB per app (learn.microsoft.com/azure/static-web-apps/plans).
MAX_BYTES = 250 * 1024 * 1024
MAX_FILES = 20_000
ARTIFACT_NAME = re.compile(r"^site-sha256-([0-9a-f]{64})$")
REQUIRED_FILES = ("index.html", "staticwebapp.config.json")


def fail(message):
    print(f"::error::{message}", file=sys.stderr)
    sys.exit(1)


def digest(folder):
    root = pathlib.Path(folder)
    lines = []
    for path in sorted(root.rglob("*"), key=lambda p: p.relative_to(root).as_posix().encode("utf-8")):
        if path.is_symlink():
            fail(f"{path}: links are not allowed in a published site")
        if path.is_file():
            relative = path.relative_to(root).as_posix()
            lines.append(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {relative}\n")
    if not lines:
        fail(f"{folder}: no files")
    return hashlib.sha256("".join(lines).encode("utf-8")).hexdigest()


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def _api(path, raw=False):
    url = f"{os.environ['GITHUB_API_URL']}/repos/{os.environ['GITHUB_REPOSITORY']}{path}"
    request = urllib.request.Request(url, headers={
        "Authorization": f"Bearer {os.environ['GH_TOKEN']}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
    })
    opener = urllib.request.build_opener(_NoRedirect)
    try:
        with opener.open(request, timeout=60) as response:
            body = response.read()
    except urllib.error.HTTPError as error:
        if raw and error.code in (301, 302, 303, 307, 308):
            return error.headers["Location"]
        fail(f"GitHub API {path}: HTTP {error.code}")
    return body if raw else json.loads(body)


def _check_run(run_id, workflow, event, branch):
    repository = os.environ["GITHUB_REPOSITORY"]
    run = _api(f"/actions/runs/{run_id}")
    checks = {
        "workflow file": (run.get("path"), workflow),
        "event": (run.get("event"), event),
        "status": (run.get("status"), "completed"),
        "conclusion": (run.get("conclusion"), "success"),
        "repository": ((run.get("repository") or {}).get("full_name"), repository),
        "head repository": ((run.get("head_repository") or {}).get("full_name"), repository),
    }
    if branch is not None:
        checks["branch"] = (run.get("head_branch"), branch)
    wrong = [f"{name} is {actual!r}, expected {expected!r}" for name, (actual, expected) in checks.items() if actual != expected]
    if wrong:
        fail(f"run {run_id} cannot be deployed: " + "; ".join(wrong))
    if branch is not None:
        _check_on_branch(run_id, run, branch)
    return run


def _check_on_branch(run_id, run, branch):
    """head_branch is only a name: a run started by pushing a TAG reports the tag's name there, with event "push".
    So the run's commit must be in the branch (compared with the branch's head commit, never with the name, which
    a tag of the same name would make ambiguous), the branch must be the default branch, and no tag may carry
    the branch's name."""
    if _api("").get("default_branch") != branch:
        fail(f"run {run_id}: {branch!r} is not the repository's default branch")
    tags = [t.get("ref") for t in _api(f"/git/matching-refs/tags/{branch}") or []]
    if f"refs/tags/{branch}" in tags:
        fail(f"a tag named {branch!r} exists; it makes runs look as if they ran on the branch. Delete the tag "
             "(and restrict tag creation, infra/README.md) before promoting")
    head = ((_api(f"/branches/{branch}").get("commit") or {}).get("sha") or "")
    if not re.fullmatch(r"[0-9a-f]{40}", head) or not re.fullmatch(r"[0-9a-f]{40}", run.get("head_sha") or ""):
        fail(f"run {run_id}: commit of the run or head of {branch!r} not readable")
    status = _api(f"/compare/{run['head_sha']}...{head}").get("status")
    if status not in ("identical", "ahead"):
        fail(f"run {run_id}: its commit {run['head_sha']} is not on {branch!r} (comparison: {status!r}); "
             "it may have run from a tag or another branch")


def _run_id(text):
    if not re.fullmatch(r"[1-9][0-9]{0,19}", text or ""):
        fail(f"not a run id: {text!r}")
    return text


def _safe_entries(archive):
    names = set()
    total = 0
    infos = archive.infolist()
    if len(infos) > MAX_FILES:
        fail(f"artifact has {len(infos)} entries, more than {MAX_FILES}")
    for info in infos:
        name = info.filename
        parts = name.rstrip("/").split("/")
        mode = info.external_attr >> 16
        if (not name or name.startswith("/") or "\\" in name or ":" in name or "\0" in name
                or any(p in ("", ".", "..") for p in parts) or any(ord(c) < 32 for c in name)):
            fail(f"artifact entry with an unsafe name: {name!r}")
        if stat.S_ISLNK(mode):
            fail(f"artifact entry is a link: {name!r}")
        key = name.rstrip("/").casefold()
        if key in names:
            fail(f"artifact entry appears twice: {name!r}")
        names.add(key)
        total += info.file_size
        if total > MAX_BYTES:
            fail(f"artifact unpacks to more than {MAX_BYTES} bytes")
    return infos


def fetch(args):
    run_id = _run_id(args.run_id)
    run = _check_run(run_id, args.workflow, args.event, args.branch)
    artifacts = _api(f"/actions/runs/{run_id}/artifacts?per_page=100").get("artifacts", [])
    sites = [a for a in artifacts if ARTIFACT_NAME.match(a.get("name", "")) and not a.get("expired")]
    if len(sites) != 1:
        fail(f"run {run_id}: expected exactly one unexpired artifact named site-sha256-<hex>, found {len(sites)} "
             "(artifacts are kept for the retention period stated in infra/README.md)")
    artifact = sites[0]
    expected = ARTIFACT_NAME.match(artifact["name"]).group(1)
    if args.expect_sha256 and args.expect_sha256.lower() != expected:
        fail(f"run {run_id}: artifact digest {expected} differs from the expected {args.expect_sha256}")
    if int(artifact.get("size_in_bytes", 0)) > MAX_BYTES:
        fail(f"artifact is larger than {MAX_BYTES} bytes")

    dest = pathlib.Path(args.dest)
    if dest.exists():
        fail(f"{dest} exists already")
    zip_path = dest.with_name(dest.name + ".zip")
    location = _api(f"/actions/artifacts/{artifact['id']}/zip", raw=True)
    if not isinstance(location, str) or not location.startswith("https://"):
        fail("artifact download did not redirect to an https address")
    # The storage address is signed; the GitHub token is not sent there.
    with urllib.request.urlopen(location, timeout=300) as response, open(zip_path, "wb") as out:
        size = 0
        while chunk := response.read(1024 * 1024):
            size += len(chunk)
            if size > MAX_BYTES:
                fail(f"artifact download exceeds {MAX_BYTES} bytes")
            out.write(chunk)
    zip_digest = (artifact.get("digest") or "").removeprefix("sha256:")
    if zip_digest and hashlib.sha256(zip_path.read_bytes()).hexdigest() != zip_digest:
        fail("downloaded zip does not match the digest GitHub recorded for the artifact")

    with zipfile.ZipFile(zip_path) as archive:
        infos = _safe_entries(archive)
        dest.mkdir(parents=True)
        for info in infos:
            target = dest.joinpath(*info.filename.rstrip("/").split("/"))
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(info) as source, open(target, "xb") as out:
                written = 0
                while chunk := source.read(1024 * 1024):
                    written += len(chunk)
                    if written > info.file_size:
                        fail(f"{info.filename}: more data than its header states")
                    out.write(chunk)
            target.chmod(0o644)
    zip_path.unlink()

    for required in REQUIRED_FILES:
        if not (dest / required).is_file():
            fail(f"artifact has no {required} at its root")
    actual = digest(dest)
    if actual != expected:
        fail(f"unpacked site digest {actual} differs from the artifact's {expected}")
    print(f"sha256={actual}")
    print(f"commit={run['head_sha']}")
    print(f"run={run_id}")
    if args.branch is not None:
        print(f"on_branch={args.branch}")


def preview_target(args):
    run_id = _run_id(args.run_id)
    run = _check_run(run_id, ".github/workflows/preview-build.yml", "pull_request", None)
    numbers = [p.get("number") for p in run.get("pull_requests") or []]
    if len(numbers) != 1 or not isinstance(numbers[0], int):
        print(f"::notice::run {run_id} belongs to {len(numbers)} pull requests of this repository; no preview", file=sys.stderr)
        print("skip=true")
        return
    pull = _api(f"/pulls/{numbers[0]}")
    repository = os.environ["GITHUB_REPOSITORY"]
    if pull.get("state") != "open" or (pull.get("head") or {}).get("sha") != run["head_sha"] \
            or ((pull.get("head") or {}).get("repo") or {}).get("full_name") != repository:
        print(f"::notice::pull request {numbers[0]} is closed, from a fork, or has a newer head; no preview from run {run_id}", file=sys.stderr)
        print("skip=true")
        return
    print(f"pull_request={int(numbers[0])}")
    print("skip=false")


def pull_request_state(args):
    if not re.fullmatch(r"[1-9][0-9]{0,9}", args.number):
        fail(f"not a pull request number: {args.number!r}")
    print(_api(f"/pulls/{args.number}").get("state", "unknown"))


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("digest").add_argument("folder")
    f = commands.add_parser("fetch")
    f.add_argument("--run-id", required=True)
    f.add_argument("--workflow", required=True)
    f.add_argument("--event", required=True)
    f.add_argument("--branch")
    f.add_argument("--dest", required=True)
    f.add_argument("--expect-sha256")
    commands.add_parser("preview-target").add_argument("--run-id", required=True)
    commands.add_parser("pull-request-state").add_argument("number")
    args = parser.parse_args()
    if args.command == "digest":
        print(digest(args.folder))
    elif args.command == "fetch":
        fetch(args)
    elif args.command == "preview-target":
        preview_target(args)
    else:
        pull_request_state(args)


if __name__ == "__main__":
    main()
