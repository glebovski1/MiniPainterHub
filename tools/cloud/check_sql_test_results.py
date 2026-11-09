"""Fail the Linux SQL gate if required relational cases did not actually pass."""
import pathlib
import sys
import xml.etree.ElementTree as ET

required = {
    'GrandfatherExistingEmailConfirmations_ConfirmsExistingUsersAndDoesNotUndoConfirmation',
    'AddHobbyProjects_UpgradesWithoutBackfill_EnforcesRelationalIndexes_AndRoundTrips',
    'LinkPostAsync_WhenRelationalRaceLoses_ReturnsWinningProjectReferenceAndReusableContext',
}
namespace = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
root = ET.parse(pathlib.Path(sys.argv[1])).getroot()
results = root.findall('.//t:UnitTestResult', namespace)
passed = set()
for result in results:
    name = result.attrib['testName'].rsplit('.', 1)[-1]
    if result.attrib.get('outcome') != 'Passed':
        raise SystemExit(f'SQL test did not pass: {name}')
    if name in required:
        output = result.findtext('t:Output/t:StdOut', default='', namespaces=namespace)
        for evidence in ['Provider=Microsoft.EntityFrameworkCore.SqlServer;',
                         'OperatingSystem=Linux;',
                         'Compatibility=170;', 'Collation=SQL_Latin1_General_CP1_CI_AS']:
            if evidence not in output:
                raise SystemExit(f'Missing SQL execution evidence for {name}: {evidence}')
        if name in passed:
            raise SystemExit(f'Duplicate SQL test result: {name}')
        passed.add(name)
if passed != required:
    raise SystemExit('Required SQL tests are missing: ' + ', '.join(sorted(required - passed)))
print(f'Verified {len(passed)} required relational tests: SQL Server, compatibility 170, production collation, no skipped cases.')
