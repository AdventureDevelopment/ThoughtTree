"""Tests of eng/deploy/site_artifact.py (ADR-046): the only check between a pull request's build artifact and the
deploy job that holds the preview identity, and between a deploy-test run and production.

No network: the GitHub API and the artifact download are replaced by recorded answers and zip fixtures built in a
temporary folder. Run by SiteArtifactScriptRules.cs (dotnet test) as
  python3 -I tests/secure/ThoughtTree.Architecture.Rules/Deployment/site_artifact_tests.py <repository root>

Mutation run, 2026-11-19 (third review N3-L2): 30 single-line mutants of site_artifact.py, each applied, tested
and reverted; 29 killed. The one survivor, "more data than its header states" accepted, is equivalent: zipfile
stops reading an entry at its declared size and checks its CRC, so the check is defence in depth. The mutant list
is in the commit body of the commit that added the DownloadLimits and Api tests.
"""

import contextlib
import email.message
import hashlib
import io
import os
import pathlib
import stat
import sys
import tempfile
import unittest
import urllib.error
import zipfile

ROOT = pathlib.Path(sys.argv.pop(1) if len(sys.argv) > 1 else ".").resolve()
sys.path.insert(0, str(ROOT / "eng" / "deploy"))
import site_artifact  # noqa: E402

REPO = "Owner/Repo"
SHA = "a" * 40


def good_run(**changes):
    run = {
        "path": ".github/workflows/deploy-test.yml",
        "event": "push",
        "status": "completed",
        "conclusion": "success",
        "head_branch": "main",
        "head_sha": SHA,
        "repository": {"full_name": REPO},
        "head_repository": {"full_name": REPO},
        "pull_requests": [],
    }
    run.update(changes)
    return run


def site_files():
    return {"index.html": b"<html></html>", "staticwebapp.config.json": b"{}", "_framework/app.wasm": b"\0asm"}


def zip_bytes(entries, link=None):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w") as archive:
        for name, data in entries.items():
            archive.writestr(name, data)
        if link:
            info = zipfile.ZipInfo(link)
            info.external_attr = (stat.S_IFLNK | 0o777) << 16
            archive.writestr(info, "/etc/passwd")
    return buffer.getvalue()


def digest_of(entries):
    lines = sorted(f"{hashlib.sha256(d).hexdigest()}  {n}\n" for n, d in entries.items() if not n.endswith("/"))
    lines.sort(key=lambda line: line.split("  ", 1)[1].rstrip("\n").encode("utf-8"))
    return hashlib.sha256("".join(lines).encode("utf-8")).hexdigest()


class FakeGitHub:
    """Recorded GitHub API answers keyed by path; unknown paths fail like an HTTP 404."""

    def __init__(self, answers, archive=b"", location="https://storage.example/artifact.zip"):
        self.answers = answers
        self.archive = archive
        self.location = location
        self.calls = []

    def api(self, path, raw=False):
        self.calls.append(path)
        if raw:
            return self.location
        for prefix, answer in self.answers.items():
            if path == prefix or path.startswith(prefix + "?"):
                return answer
        site_artifact.fail(f"GitHub API {path}: HTTP 404")

    def urlopen(self, url, timeout=None):
        assert url == self.location
        return contextlib.closing(io.BytesIO(self.archive))


class Base(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.dir = pathlib.Path(self.temp.name)
        os.environ.update(GITHUB_REPOSITORY=REPO, GITHUB_API_URL="https://api.example", GH_TOKEN="t")
        self._api, self._urlopen = site_artifact._api, site_artifact.urllib.request.urlopen
        self.stderr = io.StringIO()
        self._stderr = sys.stderr
        sys.stderr = self.stderr

    def tearDown(self):
        site_artifact._api, site_artifact.urllib.request.urlopen = self._api, self._urlopen
        sys.stderr = self._stderr
        self.temp.cleanup()

    def install(self, fake):
        site_artifact._api = fake.api
        site_artifact.urllib.request.urlopen = fake.urlopen

    def main_answers(self, run=None, compare="identical", tags=None):
        return {
            "/actions/runs/7": run or good_run(),
            "": {"default_branch": "main"},
            "/branches/main": {"name": "main", "commit": {"sha": "b" * 40}},
            f"/compare/{SHA}...{'b' * 40}": {"status": compare},
            "/git/matching-refs/tags/main": tags or [],
        }

    def fetch(self, fake, expect=None, branch="main", workflow=".github/workflows/deploy-test.yml", event="push",
              same_run=False):
        self.install(fake)
        args = type("Args", (), dict(run_id="7", workflow=workflow, event=event, branch=branch,
                                     dest=str(self.dir / "site"), expect_sha256=expect, same_run=same_run))
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            site_artifact.fetch(args)
        return dict(line.split("=", 1) for line in out.getvalue().split())

    def artifact_answers(self, entries, name=None, artifact=None, **run_changes):
        answers = self.main_answers(run=good_run(**run_changes))
        record = {"id": 1, "name": name or f"site-sha256-{digest_of(entries)}", "size_in_bytes": 100, "expired": False}
        record.update(artifact or {})
        answers["/actions/runs/7/artifacts"] = {"artifacts": [record]}
        return answers

    def assertFails(self, message, action):
        """The script stops with an error that names the reason, so a test cannot pass through another check."""
        with self.assertRaises(SystemExit):
            action()
        self.assertIn(message, self.stderr.getvalue())


class ArchiveEntries(Base):
    def rejected(self, entries, link=None):
        with zipfile.ZipFile(io.BytesIO(zip_bytes(entries, link))) as archive, self.assertRaises(SystemExit):
            site_artifact._safe_entries(archive)

    def test_a_safe_site_passes(self):
        with zipfile.ZipFile(io.BytesIO(zip_bytes(site_files()))) as archive:
            self.assertEqual(len(site_artifact._safe_entries(archive)), 3)

    def test_path_traversal_is_rejected(self):
        for name in ("../evil", "a/../../x", "a/./b", "a//b"):
            with self.subTest(name=name):
                self.rejected({"index.html": b"", name: b"x"})

    def test_absolute_drive_and_backslash_names_are_rejected(self):
        for name in ("/etc/x", "C:/x", "a\\b"):
            with self.subTest(name=name):
                self.rejected({"index.html": b"", name: b"x"})

    def test_control_characters_in_names_are_rejected(self):
        for name in ("a\nb", "a\rb", "a\x1bb"):
            with self.subTest(name=repr(name)):
                self.rejected({"index.html": b"", name: b"x"})

    def test_links_are_rejected(self):
        self.rejected({"index.html": b""}, link="evil")

    def test_names_differing_only_in_case_are_rejected(self):
        self.rejected({"index.html": b"", "INDEX.HTML": b""})

    def test_too_many_entries_are_rejected(self):
        old = site_artifact.MAX_FILES
        site_artifact.MAX_FILES = 3
        try:
            self.rejected({f"f{i}": b"" for i in range(4)})
        finally:
            site_artifact.MAX_FILES = old

    def test_too_large_a_total_is_rejected(self):
        old = site_artifact.MAX_BYTES
        site_artifact.MAX_BYTES = 10
        try:
            self.rejected({"index.html": b"x" * 11})
        finally:
            site_artifact.MAX_BYTES = old


class Digest(Base):
    def test_digest_matches_a_sha256sum_manifest(self):
        for name, data in site_files().items():
            path = self.dir / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        self.assertEqual(site_artifact.digest(self.dir), digest_of(site_files()))

    def test_a_link_in_a_published_folder_is_rejected(self):
        (self.dir / "index.html").write_bytes(b"")
        (self.dir / "link").symlink_to("/etc/passwd")
        with self.assertRaises(SystemExit):
            site_artifact.digest(self.dir)


class Fetch(Base):
    def test_a_good_artifact_is_unpacked_and_its_digest_reported(self):
        entries = site_files()
        out = self.fetch(FakeGitHub(self.artifact_answers(entries), zip_bytes(entries)))
        self.assertEqual(out["sha256"], digest_of(entries))
        self.assertEqual(out["commit"], SHA)
        self.assertEqual((self.dir / "site" / "_framework" / "app.wasm").read_bytes(), b"\0asm")

    def test_content_that_differs_from_the_name_digest_is_rejected(self):
        entries = site_files()
        answers = self.artifact_answers(entries)
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(answers, zip_bytes({**entries, "index.html": b"<script>evil</script>"})))

    def test_an_expected_digest_that_differs_is_rejected(self):
        entries = site_files()
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(self.artifact_answers(entries), zip_bytes(entries)), expect="0" * 64)

    def test_a_zip_slip_archive_writes_nothing_outside_the_folder(self):
        entries = {**site_files(), "../outside.txt": b"x"}
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(self.artifact_answers(entries), zip_bytes(entries)))
        self.assertFalse((self.dir / "outside.txt").exists())

    def test_an_archive_with_a_link_is_rejected(self):
        entries = site_files()
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(self.artifact_answers(entries), zip_bytes(entries, link="evil")))

    def test_an_archive_without_index_html_is_rejected(self):
        entries = {"staticwebapp.config.json": b"{}"}
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(self.artifact_answers(entries), zip_bytes(entries)))

    def test_a_run_with_two_site_artifacts_is_rejected(self):
        entries = site_files()
        answers = self.artifact_answers(entries)
        answers["/actions/runs/7/artifacts"]["artifacts"].append(dict(answers["/actions/runs/7/artifacts"]["artifacts"][0], id=2))
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(answers, zip_bytes(entries)))

    def test_an_existing_destination_is_not_overwritten(self):
        (self.dir / "site").mkdir()
        entries = site_files()
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(self.artifact_answers(entries), zip_bytes(entries)))

    def test_run_ids_must_be_numbers(self):
        for text in ("", "0", "12a", "1 2", "-1", "1;id"):
            with self.subTest(text=text), self.assertRaises(SystemExit):
                site_artifact._run_id(text)


class DownloadLimits(Base):
    """Third review N3-L2: checks that no earlier test exercised (mutants M11, M13 to M16 in the commit body)."""

    def test_an_expired_artifact_is_ignored_and_the_live_one_used(self):
        entries = site_files()
        answers = self.artifact_answers(entries)
        live = answers["/actions/runs/7/artifacts"]["artifacts"][0]
        answers["/actions/runs/7/artifacts"]["artifacts"].insert(0, dict(live, id=9, expired=True))
        self.assertEqual(self.fetch(FakeGitHub(answers, zip_bytes(entries)))["sha256"], digest_of(entries))

    def test_only_an_expired_artifact_is_refused(self):
        entries = site_files()
        answers = self.artifact_answers(entries, artifact={"expired": True})
        self.assertFails("exactly one unexpired artifact", lambda: self.fetch(FakeGitHub(answers, zip_bytes(entries))))

    def test_a_declared_size_over_the_limit_is_refused_before_download(self):
        entries = site_files()
        fake = FakeGitHub(self.artifact_answers(entries, artifact={"size_in_bytes": site_artifact.MAX_BYTES + 1}), zip_bytes(entries))
        self.assertFails("artifact is larger than", lambda: self.fetch(fake))
        self.assertFalse(any(c.endswith("/zip") for c in fake.calls), "no download after the size check")

    def test_a_download_longer_than_the_limit_is_cut_off(self):
        entries = site_files()
        archive = zip_bytes(entries)
        old = site_artifact.MAX_BYTES
        site_artifact.MAX_BYTES = len(archive) - 1  # the unpacked files stay below it; only the stream is too long
        try:
            fake = FakeGitHub(self.artifact_answers(entries, artifact={"size_in_bytes": 1}), archive)
            self.assertFails("artifact download exceeds", lambda: self.fetch(fake))
        finally:
            site_artifact.MAX_BYTES = old

    def test_a_download_address_without_https_is_refused(self):
        entries = site_files()
        fake = FakeGitHub(self.artifact_answers(entries), zip_bytes(entries), location="http://storage.example/artifact.zip")
        self.assertFails("did not redirect to an https address", lambda: self.fetch(fake))

    def test_a_zip_that_differs_from_github_s_recorded_digest_is_refused(self):
        entries = site_files()
        fake = FakeGitHub(self.artifact_answers(entries, artifact={"digest": "sha256:" + "0" * 64}), zip_bytes(entries))
        self.assertFails("does not match the digest GitHub recorded", lambda: self.fetch(fake))

    def test_a_zip_matching_github_s_recorded_digest_passes(self):
        entries = site_files()
        archive = zip_bytes(entries)
        recorded = "sha256:" + hashlib.sha256(archive).hexdigest()
        out = self.fetch(FakeGitHub(self.artifact_answers(entries, artifact={"digest": recorded}), archive))
        self.assertEqual(out["sha256"], digest_of(entries))


class Api(Base):
    """The real _api with a stub opener (mutants M1 to M3): errors stop the script, redirects are not followed
    (the token must not reach the storage host), and only a redirect yields a download address."""

    def setUp(self):
        super().setUp()
        self._build_opener = site_artifact.urllib.request.build_opener
        self.handlers = None
        self.requests = []

    def tearDown(self):
        site_artifact.urllib.request.build_opener = self._build_opener
        super().tearDown()

    def answer(self, code=200, body=b"{}", location=None):
        test = self

        class Opener:
            def open(self, request, timeout=None):
                test.requests.append(request)
                if code == 200:
                    return contextlib.closing(io.BytesIO(body))
                headers = email.message.Message()
                if location:
                    headers["Location"] = location
                raise urllib.error.HTTPError(request.full_url, code, "x", headers, io.BytesIO(b""))

        def build_opener(*handlers):
            test.handlers = handlers
            return Opener()

        site_artifact.urllib.request.build_opener = build_opener

    def test_an_answer_is_parsed_and_the_token_goes_to_the_api_only(self):
        self.answer(body=b'{"default_branch": "main"}')
        self.assertEqual(site_artifact._api(""), {"default_branch": "main"})
        request = self.requests[0]
        self.assertTrue(request.full_url.startswith("https://api.example/repos/Owner/Repo"))
        self.assertEqual(request.get_header("Authorization"), "Bearer t")

    def test_an_http_error_stops_the_script(self):
        for code in (401, 404, 500):
            with self.subTest(code=code):
                self.answer(code=code)
                self.assertFails(f"HTTP {code}", lambda: site_artifact._api("/actions/runs/7"))

    def test_redirects_are_not_followed(self):
        self.answer()
        site_artifact._api("")
        self.assertIn(site_artifact._NoRedirect, self.handlers)
        self.assertIsNone(site_artifact._NoRedirect().redirect_request(None, None, 302, "x", {}, "https://elsewhere"))

    def test_a_redirect_yields_the_download_address_only_for_raw_calls(self):
        self.answer(code=302, location="https://storage.example/a.zip")
        self.assertEqual(site_artifact._api("/actions/artifacts/1/zip", raw=True), "https://storage.example/a.zip")
        self.assertFails("HTTP 302", lambda: site_artifact._api("/actions/artifacts/1/zip"))

    def test_a_raw_call_that_is_not_redirected_stops_the_script(self):
        self.answer(code=404)
        self.assertFails("HTTP 404", lambda: site_artifact._api("/actions/artifacts/1/zip", raw=True))


class SameRun(Base):
    """N-L1 of the re-review: deploy-test's deploy job takes the site from its own build job, in the same run."""

    def setUp(self):
        super().setUp()
        os.environ["GITHUB_RUN_ID"] = "7"

    def test_the_running_run_s_artifact_is_fetched_with_the_build_job_s_digest(self):
        entries = site_files()
        answers = self.artifact_answers(entries, status="in_progress", conclusion=None)
        out = self.fetch(FakeGitHub(answers, zip_bytes(entries)), expect=digest_of(entries), same_run=True)
        self.assertEqual(out["sha256"], digest_of(entries))

    def test_same_run_needs_the_digest_and_the_own_run_id(self):
        entries = site_files()
        answers = self.artifact_answers(entries, status="in_progress", conclusion=None)
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(answers, zip_bytes(entries)), same_run=True)
        os.environ["GITHUB_RUN_ID"] = "8"
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(answers, zip_bytes(entries)), expect=digest_of(entries), same_run=True)

    def test_without_same_run_an_unfinished_run_is_rejected(self):
        entries = site_files()
        answers = self.artifact_answers(entries, status="in_progress", conclusion=None)
        with self.assertRaises(SystemExit):
            self.fetch(FakeGitHub(answers, zip_bytes(entries)), expect=digest_of(entries))


class RunChecks(Base):
    def check(self, run, branch="main", **answers):
        fake = FakeGitHub({**self.main_answers(run=run), **answers})
        self.install(fake)
        return site_artifact._check_run("7", ".github/workflows/deploy-test.yml", "push", branch)

    def test_a_successful_push_run_of_deploy_test_on_main_passes(self):
        self.assertEqual(self.check(good_run())["head_sha"], SHA)

    def test_wrong_workflow_event_status_or_conclusion_is_rejected(self):
        for change in ({"path": ".github/workflows/other.yml"}, {"event": "workflow_dispatch"},
                       {"status": "in_progress"}, {"conclusion": "failure"}, {"head_branch": "feature"}):
            with self.subTest(change=change), self.assertRaises(SystemExit):
                self.check(good_run(**change))

    def test_a_run_from_a_fork_or_another_repository_is_rejected(self):
        for change in ({"head_repository": {"full_name": "Attacker/Repo"}}, {"repository": {"full_name": "Other/Repo"}},
                       {"head_repository": None}):
            with self.subTest(change=change), self.assertRaises(SystemExit):
                self.check(good_run(**change))


class RunOnMain(Base):
    """N-H1 of the re-review: a run whose label is "main" is promotable only if its commit is on the branch main."""

    def check(self, run=None, compare="identical", tags=None, default="main"):
        answers = self.main_answers(run=run or good_run(), compare=compare, tags=tags)
        answers[""] = {"default_branch": default}
        self.install(FakeGitHub(answers))
        return site_artifact._check_run("7", ".github/workflows/deploy-test.yml", "push", "main")

    def test_a_commit_that_main_contains_passes(self):
        for status in ("identical", "ahead"):
            with self.subTest(status=status):
                self.assertEqual(self.check(compare=status)["head_sha"], SHA)

    def test_a_run_of_a_tag_named_main_on_a_commit_off_main_is_rejected(self):
        # A tag push reports the tag's name in head_branch and event "push"; its commit is not on main.
        for status in ("diverged", "behind"):
            with self.subTest(status=status), self.assertRaises(SystemExit):
                self.check(compare=status)

    def test_any_tag_named_like_the_branch_is_rejected(self):
        with self.assertRaises(SystemExit):
            self.check(tags=[{"ref": "refs/tags/main", "object": {"sha": SHA}}])

    def test_a_tag_that_only_starts_with_the_branch_name_does_not_block(self):
        self.assertEqual(self.check(tags=[{"ref": "refs/tags/main-1.0", "object": {"sha": SHA}}])["head_sha"], SHA)

    def test_a_branch_that_is_not_the_default_branch_is_rejected(self):
        with self.assertRaises(SystemExit):
            self.check(default="develop")

    def test_a_branch_head_or_run_commit_that_is_not_a_full_sha_is_refused(self):
        # Mutant M9: the comparison would otherwise use names such as "main", which a tag can make ambiguous.
        for head, run_sha in (("main", SHA), ("b" * 39, SHA), ("b" * 40, "main"), ("b" * 40, "A" * 40)):
            with self.subTest(head=head, run_sha=run_sha):
                answers = self.main_answers(run=good_run(head_sha=run_sha))
                answers["/branches/main"] = {"commit": {"sha": head}}
                answers[f"/compare/{run_sha}...{head}"] = {"status": "identical"}
                self.install(FakeGitHub(answers))
                self.assertFails("not readable", lambda: site_artifact._check_run("7", ".github/workflows/deploy-test.yml", "push", "main"))

    def test_the_comparison_uses_the_branch_head_commit_not_the_ambiguous_name(self):
        fake = FakeGitHub(self.main_answers())
        self.install(fake)
        site_artifact._check_run("7", ".github/workflows/deploy-test.yml", "push", "main")
        self.assertIn(f"/compare/{SHA}...{'b' * 40}", fake.calls)
        self.assertNotIn(f"/compare/{SHA}...main", fake.calls)


class PreviewTarget(Base):
    def target(self, run, pull):
        answers = {"/actions/runs/7": run, "/pulls/12": pull}
        self.install(FakeGitHub(answers))
        out = io.StringIO()
        args = type("Args", (), dict(run_id="7"))
        with contextlib.redirect_stdout(out):
            site_artifact.preview_target(args)
        return dict(line.split("=", 1) for line in out.getvalue().split())

    def preview_run(self, **changes):
        values = dict(path=".github/workflows/preview-build.yml", event="pull_request", head_branch="feature",
                      pull_requests=[{"number": 12}])
        values.update(changes)
        return good_run(**values)

    def open_pull(self, **changes):
        pull = {"state": "open", "head": {"sha": SHA, "repo": {"full_name": REPO}}}
        pull.update(changes)
        return pull

    def test_the_open_pull_request_of_the_run_is_found(self):
        self.assertEqual(self.target(self.preview_run(), self.open_pull()), {"pull_request": "12", "skip": "false"})

    def test_a_closed_moved_or_fork_pull_request_is_skipped(self):
        for pull in (self.open_pull(state="closed"), self.open_pull(head={"sha": "c" * 40, "repo": {"full_name": REPO}}),
                     self.open_pull(head={"sha": SHA, "repo": {"full_name": "Attacker/Repo"}})):
            with self.subTest(pull=pull):
                self.assertEqual(self.target(self.preview_run(), pull)["skip"], "true")

    def test_a_run_with_no_or_several_pull_requests_is_skipped(self):
        for prs in ([], [{"number": 12}, {"number": 13}], [{"number": "12"}]):
            with self.subTest(prs=prs):
                self.assertEqual(self.target(self.preview_run(pull_requests=prs), self.open_pull())["skip"], "true")

    def test_a_fork_build_run_is_rejected(self):
        with self.assertRaises(SystemExit):
            self.target(self.preview_run(head_repository={"full_name": "Attacker/Repo"}), self.open_pull())


if __name__ == "__main__":
    result = unittest.main(argv=[sys.argv[0], "-v"], exit=False).result
    sys.exit(0 if result.wasSuccessful() and result.testsRun > 0 else 1)
