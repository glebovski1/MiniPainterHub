import unittest
from datetime import datetime, timezone
from prepare_rehearsal_cleanup import APP, ALL, template
from production_candidate_cleanup import resources, with_candidate_tls_cleanup


class ProductionCandidateCleanupTests(unittest.TestCase):
    def test_both_deployments_remove_only_candidate_bindings_before_downgrade(self):
        original = template('20261108-abcdef', '2026-11-08T16:00:00Z', '2026-11-08T16:30:00Z', [],
                            now=datetime(2026,11,8,15,tzinfo=timezone.utc), arm_after_permissions=True)
        adapted = with_candidate_tls_cleanup(original)
        workflows = [r for r in resources(adapted) if r['type'] == 'Microsoft.Logic/workflows']
        self.assertEqual(len(workflows), 2)
        for workflow in workflows:
            due = workflow['properties']['definition']['actions']['Due']['actions']
            self.assertEqual(due['Plan']['runAfter'], {'Candidate': ALL})
            edits = due['Candidate']['actions']['Candidate_owned']['actions']
            for label, host in [('Apex','rollandpaint.com'), ('Www','www.rollandpaint.com')]:
                action = edits['Remove_' + label + '_if_present']['actions']['Delete_' + label + '_binding']
                self.assertEqual(action['inputs']['method'], 'DELETE')
                self.assertEqual(action['inputs']['uri'].split('?')[0], 'https://management.azure.com' + APP + '/hostNameBindings/' + host)
        roles = [r for r in resources(adapted) if r['type'] == 'Microsoft.Authorization/roleDefinitions']
        widened = [r for r in roles if 'Microsoft.Web/sites/hostNameBindings/delete' in r['properties']['permissions'][0]['actions']]
        self.assertEqual(len(widened), 1)
        grants = [r for r in resources(adapted) if r['type'] == 'Microsoft.Authorization/roleAssignments'
                  and r['properties']['roleDefinitionId'].endswith('/' + widened[0]['name'])]
        self.assertEqual(len(grants), 1)
        self.assertEqual(grants[0]['scope'], APP)
        first = next(r for r in resources(original) if r['type'] == 'Microsoft.Logic/workflows')
        self.assertEqual(first['properties']['definition']['actions']['Due']['actions']['Plan']['runAfter'], {})


if __name__ == '__main__':
    unittest.main()
