"""Adapt the proved rehearsal timer for a restricted production candidate.

Generation only. Remove this workflow AND its permissions before promotion.
Permanent production key storage is outside its scope. Domain removal is needed
before a candidate with TLS bindings can return to F1.
"""
import copy
from prepare_rehearsal_cleanup import APP, ALL, condition, http


def resources(value):
    for resource in value.get("resources", []):
        yield resource
        nested = resource.get("properties", {}).get("template")
        if nested:
            yield from resources(nested)


def with_candidate_tls_cleanup(prepared):
    result = copy.deepcopy(prepared)
    workflows = 0
    for resource in resources(result):
        if resource["type"] == "Microsoft.Authorization/roleDefinitions":
            actions = resource["properties"]["permissions"][0]["actions"]
            if "Microsoft.Web/sites/stop/action" in actions:
                actions.extend(["Microsoft.Web/sites/hostNameBindings/read", "Microsoft.Web/sites/hostNameBindings/delete"])
        if resource["type"] != "Microsoft.Logic/workflows":
            continue
        workflows += 1
        definition = resource["properties"]["definition"]
        due = definition["actions"]["Due"]["actions"]
        edits = due["Candidate"]["actions"]["Candidate_owned"]["actions"]
        for label, hostname in (("Apex", "rollandpaint.com"), ("Www", "www.rollandpaint.com")):
            path = APP + "/hostNameBindings/" + hostname
            read_name = "Read_" + label + "_binding"
            edits[read_name] = http("GET", path, after=("Stop_again",))
            edits["Remove_" + label + "_if_present"] = condition(
                "@equals(outputs('" + read_name + "')?['statusCode'], 200)",
                {"Delete_" + label + "_binding": http("DELETE", path)}, (read_name,))
        # Still attempt cost cleanup if candidate cleanup has a partial failure.
        # Azure recurrence retries failed downgrades; never raise the plan tier.
        due["Plan"]["runAfter"] = {"Candidate": ALL}
    if workflows not in (1, 2):
        raise ValueError("Unexpected workflow deployment structure")
    return result
