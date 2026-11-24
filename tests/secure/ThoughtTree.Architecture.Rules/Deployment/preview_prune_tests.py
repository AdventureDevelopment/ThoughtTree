"""Behaviour of `eng/deploy/preview.sh prune` (ADR-046) against a stub `az` and a stub `python3`, no network.

Owner decision 2026-11-24: previews of closed pull requests are deleted with `az staticwebapp environment delete`.
Its only tolerated failure is the known polling error (AuthorizationFailed for
'Microsoft.Web/locations/staticSitesOperationStatuses/read' at subscription scope, observed for pr13 on 2026-11-24
after Azure had accepted the DELETE); every delete is then checked on the environment list. These tests pin that:
a delete that succeeds, the known error with the environment gone, the known error with the environment still
listed, AuthorizationFailed for another action, the known action together with another one, the known action on a
scope of another shape, the known text with another error code, a non-authorisation error, open pull requests left
alone, and the wait limits.

Run by PreviewPruneScriptRules.cs (dotnet test) as
  python3 -I tests/secure/ThoughtTree.Architecture.Rules/Deployment/preview_prune_tests.py <repository root>
"""

import os
import pathlib
import subprocess
import sys
import tempfile
import unittest

ROOT = pathlib.Path(sys.argv.pop(1) if len(sys.argv) > 1 else ".").resolve()
SCRIPT = ROOT / "eng" / "deploy" / "preview.sh"
SUB = "00000000-0000-0000-0000-000000000000"
OPERATION = "11111111-2222-3333-4444-555555555555"

KNOWN = (
    "ERROR: (AuthorizationFailed) The client 'c' with object id 'o' does not have authorization to perform action "
    "'Microsoft.Web/locations/staticSitesOperationStatuses/read' over scope "
    f"'/subscriptions/{SUB}/providers/Microsoft.Web/locations/eastus2/staticSitesOperationStatuses/{OPERATION}' "
    "or the scope is invalid. If access was recently granted, please refresh your credentials.\n"
    "Code: AuthorizationFailed\n"
)
OTHER_ACTION = (
    "ERROR: (AuthorizationFailed) The client 'c' with object id 'o' does not have authorization to perform action "
    "'Microsoft.Web/staticSites/builds/delete' over scope "
    f"'/subscriptions/{SUB}/resourceGroups/rg-thoughttree-preview/providers/Microsoft.Web/staticSites/"
    "stapp-thoughttree-preview/builds/pr13' or the scope is invalid.\nCode: AuthorizationFailed\n"
)
BOTH_ACTIONS = KNOWN + OTHER_ACTION
WRONG_SCOPE = KNOWN.replace(
    f"'/subscriptions/{SUB}/providers/Microsoft.Web/locations/eastus2/staticSitesOperationStatuses/{OPERATION}'",
    f"'/subscriptions/{SUB}/resourceGroups/rg-other/providers/Microsoft.Web/staticSites/x'")
OTHER_CODE = KNOWN.replace("(AuthorizationFailed)", "(Forbidden)").replace("Code: AuthorizationFailed", "Code: Forbidden")
NOT_FOUND = "ERROR: (ResourceNotFound) The Resource 'pr13' was not found.\nCode: ResourceNotFound\n"

AZ_STUB = r"""#!/usr/bin/env bash
# Stub of az for preview_prune_tests.py: environments in $STUB/envs, delete behaviour in $STUB/mode.
echo "$*" >> "$STUB/calls"
case "$1 $2 $3" in
  "staticwebapp environment list") cat "$STUB/envs"; exit 0 ;;
  "staticwebapp environment delete")
    env=""; while [ $# -gt 0 ]; do [ "$1" = --environment-name ] && env="$2"; shift; done
    mode=$(cat "$STUB/mode")
    case "$mode" in
      ok|known) grep -vx "$env" "$STUB/envs" > "$STUB/envs.new" || true; mv "$STUB/envs.new" "$STUB/envs" ;;
    esac
    [ "$mode" = ok ] && exit 0
    cat "$STUB/error" >&2
    exit 1 ;;
esac
echo "az stub: unexpected call: $*" >&2
exit 99
"""

PYTHON_STUB = r"""#!/usr/bin/env bash
# Stub of python3 for preview_prune_tests.py: only "site_artifact.py pull-request-state N".
[ "$3" = pull-request-state ] || { echo "python3 stub: unexpected call: $*" >&2; exit 99; }
cat "$STUB/state_$4" 2>/dev/null || echo closed
"""


class Prune(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.stub = pathlib.Path(self.temp.name)
        for name, body in (("az", AZ_STUB), ("python3", PYTHON_STUB)):
            path = self.stub / name
            path.write_text(body)
            path.chmod(0o755)
        self.set_envs("pr13")
        (self.stub / "calls").write_text("")

    def tearDown(self):
        self.temp.cleanup()

    def set_envs(self, *names):
        (self.stub / "envs").write_text("".join(f"{n}\n" for n in names))

    def run_prune(self, mode, error="", checks="2", seconds="0", extra=None):
        (self.stub / "mode").write_text(mode)
        (self.stub / "error").write_text(error)
        env = {"PATH": f"{self.stub}:/usr/bin:/bin", "STUB": str(self.stub), "HOME": str(self.stub),
               "PREVIEW_PRUNE_WAIT_CHECKS": checks, "PREVIEW_PRUNE_WAIT_SECONDS": seconds}
        env.update(extra or {})
        result = subprocess.run(["bash", str(SCRIPT), "prune"], env=env, capture_output=True, text=True, timeout=60)
        return result.returncode, result.stdout + result.stderr

    def listed(self):
        return (self.stub / "envs").read_text().split()

    def test_a_successful_delete_passes_and_the_preview_is_gone(self):
        code, out = self.run_prune("ok")
        self.assertEqual(code, 0, out)
        self.assertEqual(self.listed(), [])
        self.assertIn("--environment-name pr13", (self.stub / "calls").read_text())

    def test_the_known_polling_error_passes_when_the_list_shows_the_preview_gone(self):
        code, out = self.run_prune("known", KNOWN)
        self.assertEqual(code, 0, out)
        self.assertIn("Azure accepted the delete", out)
        self.assertGreaterEqual((self.stub / "calls").read_text().count("environment list"), 2, "checked on the list")

    def test_the_known_polling_error_fails_when_the_preview_is_still_listed(self):
        code, out = self.run_prune("known_stays", KNOWN)
        self.assertEqual(code, 1, out)
        self.assertIn("Previews still listed after the wait: pr13", out)

    def test_authorization_failed_for_another_action_fails_with_its_message(self):
        code, out = self.run_prune("other", OTHER_ACTION)
        self.assertEqual(code, 1, out)
        self.assertIn("Microsoft.Web/staticSites/builds/delete", out)
        self.assertIn("Deleting preview pr13 failed", out)
        self.assertNotIn("Azure accepted the delete", out)

    def test_the_known_action_together_with_another_action_fails(self):
        code, out = self.run_prune("other", BOTH_ACTIONS)
        self.assertEqual(code, 1, out)
        self.assertNotIn("Azure accepted the delete", out)

    def test_the_known_action_on_a_scope_of_another_shape_fails(self):
        code, out = self.run_prune("other", WRONG_SCOPE)
        self.assertEqual(code, 1, out)
        self.assertNotIn("Azure accepted the delete", out)

    def test_the_known_text_with_another_error_code_fails(self):
        code, out = self.run_prune("other", OTHER_CODE)
        self.assertEqual(code, 1, out)
        self.assertNotIn("Azure accepted the delete", out)

    def test_a_non_authorisation_error_fails_with_its_message(self):
        code, out = self.run_prune("other", NOT_FOUND)
        self.assertEqual(code, 1, out)
        self.assertIn("ResourceNotFound", out)

    def test_previews_of_open_pull_requests_are_left_alone(self):
        self.set_envs("pr13", "pr14")
        (self.stub / "state_14").write_text("open\n")
        code, out = self.run_prune("ok")
        self.assertEqual(code, 0, out)
        self.assertEqual(self.listed(), ["pr14"])
        self.assertNotIn("--environment-name pr14", (self.stub / "calls").read_text())

    def test_nothing_to_remove_deletes_nothing(self):
        self.set_envs()
        code, out = self.run_prune("other", NOT_FOUND)
        self.assertEqual(code, 0, out)
        self.assertNotIn("delete", (self.stub / "calls").read_text())

    def test_wait_limits_outside_the_allowed_range_are_refused(self):
        for checks, seconds in (("0", "0"), ("19", "0"), ("2", "11"), ("x", "0")):
            with self.subTest(checks=checks, seconds=seconds):
                code, out = self.run_prune("ok", checks=checks, seconds=seconds)
                self.assertEqual(code, 2, out)
                self.assertIn("PREVIEW_PRUNE_WAIT_CHECKS", out)


if __name__ == "__main__":
    result = unittest.main(argv=[sys.argv[0], "-v"], exit=False).result
    sys.exit(0 if result.wasSuccessful() and result.testsRun > 0 else 1)
