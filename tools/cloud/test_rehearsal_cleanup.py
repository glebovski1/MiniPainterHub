"""Offline policy tests for the actual generated Azure template and action graph."""
from datetime import datetime, timedelta, timezone
import json
import unittest

from prepare_rehearsal_cleanup import APP, PLAN, SUB, ALL, template, verify_workflow_definition

NOW = datetime(2026, 11, 7, 15, tzinfo=timezone.utc)
RUN = "20261107-test01"
CLEANUP = (NOW + timedelta(minutes=60)).isoformat()
DEADLINE = (NOW + timedelta(minutes=90)).isoformat()
GROUP = SUB + "/resourceGroups/rg-mph-linux-rehearsal-" + RUN


def resources(value):
    for resource in value.get("resources", []):
        yield resource
        yield from resources(resource.get("properties", {}).get("template", {}))


def actions(value):
    for name, action in value.get("actions", {}).items():
        yield name, action
        yield from actions(action)
        yield from actions(action.get("else", {}))


class CleanupTests(unittest.TestCase):
    def setUp(self):
        self.template = template(RUN, CLEANUP, DEADLINE, [], now=NOW)
        self.resources = list(resources(self.template))
        self.workflow = next(r for r in self.resources if r["type"] == "Microsoft.Logic/workflows")
        self.definition = self.workflow["properties"]["definition"]
        self.actions = dict(actions(self.definition))

    def test_generator_creates_no_paid_dependencies_and_workflow_starts_disabled(self):
        self.assertEqual({r["type"] for r in self.resources}, {"Microsoft.Resources/resourceGroups", "Microsoft.Resources/deployments",
            "Microsoft.Logic/workflows", "Microsoft.Authorization/roleDefinitions", "Microsoft.Authorization/roleAssignments"})
        self.assertEqual(self.workflow["properties"]["state"], "Disabled")
        self.assertEqual(self.workflow["identity"], {"type": "SystemAssigned"})
        self.assertNotIn("enableWorkflow", json.dumps(self.template))

    def test_no_mutation_can_target_production_or_raise_plan_tier(self):
        allowed = {APP, APP + "/stop", APP + "/config/appsettings", APP + "/config/connectionstrings",
                   APP + "/config/web", PLAN, GROUP, self.template["outputs"]["workflowId"]["value"] + "/disable"}
        for action in self.actions.values():
            if action["type"] != "Http":
                continue
            inputs = action["inputs"]
            self.assertEqual(inputs["authentication"]["type"], "ManagedServiceIdentity")
            target = inputs["uri"].removeprefix("https://management.azure.com").split("?")[0]
            if inputs["method"] != "GET":
                self.assertIn(target, allowed)
        self.assertEqual(self.actions["Return_F1"]["inputs"]["body"]["sku"]["name"], "F1")
        self.assertNotIn("/start?", json.dumps(self.definition))

    def test_failures_in_one_branch_do_not_block_other_cost_cleanup(self):
        for branch in ("Candidate", "Plan", "Dependencies"):
            self.assertEqual(self.actions[branch]["runAfter"], {})
        for name in ("Scrub_settings", "Scrub_connections", "Remove_identity", "Disable_always_on", "Stop_again"):
            self.assertEqual(next(iter(self.actions[name]["runAfter"].values())), ALL)
        for name in ("Verify_candidate", "Verify_plan", "Verify_group_absent"):
            self.assertEqual(self.actions[name]["runAfter"], {branch: ALL for branch in ("Candidate", "Plan", "Dependencies")})

    def test_recurrence_retries_and_only_verified_cleanup_disables_it(self):
        trigger = self.definition["triggers"]["Deadline"]
        self.assertEqual(trigger["recurrence"], {"frequency": "Minute", "interval": 2, "startTime": "2026-11-07T16:00:00Z"})
        self.assertEqual(trigger["runtimeConfiguration"]["concurrency"]["runs"], 1)
        self.assertIn(CLEANUP, self.actions["Due"]["expression"])
        verify = self.actions["Cleanup_verified"]
        self.assertEqual(set(verify["actions"]), {"Disable_finished_workflow"})
        for required in ("'Stopped'", "'F1'", "404", "'Scrub_settings'", "'Remove_identity'", "'Return_F1'"):
            self.assertIn(required, verify["expression"])
        self.assertEqual(verify["else"], {"actions": {}})

    def test_deletion_requires_ownership_complete_inventory_and_allowlist(self):
        for required in (RUN, CLEANUP, "mph-linux-core-rehearsal"):
            self.assertIn(required, self.actions["Group_owned"]["expression"])
        self.assertEqual(self.actions["Inventory_matches"]["actions"]["Delete_group"]["inputs"]["method"], "DELETE")
        for required in ("nextLink", "Unexpected_resources", "200"):
            self.assertIn(required, self.actions["Inventory_matches"]["expression"])
        self.assertIn("not(contains", self.actions["Unexpected_resources"]["inputs"]["where"])
        self.assertEqual(self.definition["parameters"]["allowedResourceIds"]["defaultValue"], [])

    def test_rbac_scopes_are_exact_and_cannot_read_secrets_or_grant_roles(self):
        roles = {SUB + "/providers/Microsoft.Authorization/roleDefinitions/" + r["name"]: r
                 for r in self.resources if r["type"] == "Microsoft.Authorization/roleDefinitions"}
        grants = [r for r in self.resources if r["type"] == "Microsoft.Authorization/roleAssignments"]
        self.assertEqual(len(grants), 5)
        for grant in grants:
            role = roles[grant["properties"]["roleDefinitionId"]]
            scope = grant.get("scope", role["properties"]["assignableScopes"][0])
            permissions = role["properties"]["permissions"][0]
            self.assertEqual(permissions["dataActions"], [])
            for operation in permissions["actions"]:
                self.assertNotIn("*", operation)
                self.assertNotIn("list", operation.lower())
                self.assertNotIn("authorization/", operation.lower())
            if scope == SUB:
                self.assertEqual(permissions["actions"], ["Microsoft.Resources/subscriptions/resourceGroups/read"])
            else:
                self.assertIn(scope, (APP, PLAN, GROUP, self.template["outputs"]["workflowId"]["value"]))

    def test_expired_long_or_unbounded_windows_are_rejected(self):
        for cleanup, end in [(NOW.isoformat(), DEADLINE), (CLEANUP, (NOW + timedelta(hours=3)).isoformat()),
                             (CLEANUP, (NOW + timedelta(minutes=61)).isoformat()),
                             ("2026-11-07T16:00:00", DEADLINE)]:
            with self.assertRaises(ValueError):
                template(RUN, cleanup, end, [], now=NOW)

    def test_production_ids_traversal_and_old_group_are_rejected(self):
        for target in (APP, GROUP.replace(RUN, "20261106") + "/providers/Microsoft.Sql/servers/test",
                       GROUP + "/providers/../mphp-rg", GROUP + "/providers/X/x?test"):
            with self.assertRaises(ValueError):
                template(RUN, CLEANUP, DEADLINE, [target], now=NOW)

    def test_full_template_has_no_credentials(self):
        content = json.dumps(self.template).lower()
        for word in ("clientsecret", "sqlpassword", "bearer ", "accountkey=", "listkeys", "appsettings/list"):
            self.assertNotIn(word, content)

    def test_cross_scope_deployment_references_use_cleanup_resource_group(self):
        expected = SUB + "/resourceGroups/rg-mph-linux-cleanup-" + RUN + "/providers/Microsoft.Resources/deployments/mph-cleanup-workflow-" + RUN
        grants = [r for r in self.resources if r["type"] == "Microsoft.Authorization/roleAssignments"]
        observer = next(r for r in self.template["resources"] if r["type"] == "Microsoft.Authorization/roleAssignments")
        self.assertEqual(observer["dependsOn"], [expected])
        self.assertIn(expected, observer["properties"]["principalId"])

    def test_subscription_and_group_assignments_inherit_deployment_scope(self):
        observer = next(r for r in self.template["resources"] if r["type"] == "Microsoft.Authorization/roleAssignments")
        self.assertNotIn("scope", observer)
        module = next(r for r in self.template["resources"] if r["type"] == "Microsoft.Resources/deployments"
                      and r.get("resourceGroup") == GROUP.split("/")[-1])
        self.assertEqual(len(module["properties"]["template"]["resources"]), 1)
        self.assertNotIn("scope", module["properties"]["template"]["resources"][0])

    def test_auto_arm_is_opt_in_and_waits_for_all_permission_deployments(self):
        self.assertFalse(self.template["metadata"]["armAfterPermissions"])
        value = template(RUN, CLEANUP, DEADLINE, [], now=NOW, arm_after_permissions=True)
        workflows = [r for r in resources(value) if r["type"] == "Microsoft.Logic/workflows"]
        self.assertEqual([r["properties"]["state"] for r in workflows], ["Disabled", "Enabled"])
        original = json.loads(json.dumps(workflows[0]))
        original["properties"]["state"] = "Enabled"
        self.assertEqual(workflows[1], original)
        arm = next(r for r in value["resources"] if r["name"] == "mph-cleanup-arm-" + RUN)
        controls = SUB + "/resourceGroups/rg-mph-linux-cleanup-" + RUN
        observer = next(r for r in value["resources"] if r["type"] == "Microsoft.Authorization/roleAssignments")
        self.assertEqual(set(arm["dependsOn"]), {
            controls + "/providers/Microsoft.Resources/deployments/mph-cleanup-workflow-" + RUN,
            SUB + "/providers/Microsoft.Authorization/roleAssignments/" + observer["name"],
            SUB + "/resourceGroups/mphp-rg/providers/Microsoft.Resources/deployments/mph-cleanup-candidate-" + RUN,
            GROUP + "/providers/Microsoft.Resources/deployments/mph-cleanup-dependencies-" + RUN})

    def test_readback_allows_only_matching_azure_evaluated_schedule(self):
        actual = json.loads(json.dumps(self.definition))
        trigger = actual["triggers"]["Deadline"]
        trigger["evaluatedRecurrence"] = dict(trigger["recurrence"])
        verify_workflow_definition(actual, self.definition)
        self.assertIn("evaluatedRecurrence", trigger)
        trigger["evaluatedRecurrence"]["interval"] = 99
        with self.assertRaisesRegex(ValueError, "schedule differs"):
            verify_workflow_definition(actual, self.definition)
        trigger["evaluatedRecurrence"] = dict(trigger["recurrence"])
        actual["actions"]["Due"]["expression"] = "@true"
        with self.assertRaisesRegex(ValueError, "definition differs"):
            verify_workflow_definition(actual, self.definition)

    def test_azure_recurrence_uses_utc_z_and_rounds_fractional_deadline_up(self):
        value = template(RUN, "2026-11-07T16:00:00.311130+00:00", "2026-11-07T16:31:00Z", [], now=NOW)
        workflow = next(r for r in resources(value) if r["type"] == "Microsoft.Logic/workflows")
        definition = workflow["properties"]["definition"]
        self.assertEqual(definition["triggers"]["Deadline"]["recurrence"]["startTime"], "2026-11-07T16:00:01Z")
        self.assertIn("16:00:00.311130+00:00", definition["actions"]["Due"]["expression"])


if __name__ == "__main__":
    unittest.main()
