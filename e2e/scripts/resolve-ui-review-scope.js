const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const REPO_ROOT = path.resolve(__dirname, "../..");
const DEFAULT_MATRIX_PATH = path.resolve(__dirname, "../ui-review.matrix.json");

function toPosixPath(value) {
  return value.replace(/\\/g, "/").replace(/^\.\//, "");
}

function escapeRegExp(value) {
  return value.replace(/[|\\{}()[\]^$+?.]/g, "\\$&");
}

function globToRegExp(glob) {
  let pattern = "^";

  for (let index = 0; index < glob.length; index += 1) {
    const char = glob[index];
    const next = glob[index + 1];

    if (char === "*" && next === "*") {
      pattern += ".*";
      index += 1;
      continue;
    }

    if (char === "*") {
      pattern += "[^/]*";
      continue;
    }

    if (char === "?") {
      pattern += ".";
      continue;
    }

    pattern += escapeRegExp(char);
  }

  pattern += "$";
  return new RegExp(pattern, "i");
}

function matchesAny(filePath, patterns = []) {
  return patterns.some((pattern) => globToRegExp(toPosixPath(pattern)).test(filePath));
}

function loadMatrix(matrixPath = DEFAULT_MATRIX_PATH) {
  return JSON.parse(fs.readFileSync(matrixPath, "utf8"));
}

function normalizeFiles(files) {
  return Array.from(new Set(files.map((file) => toPosixPath(file.trim())).filter(Boolean))).sort();
}

function getGitDiffFiles(base, head) {
  const diffArgs = ["diff", "--name-only"];
  if (base && head) {
    diffArgs.push(base, head);
  } else {
    diffArgs.push("HEAD");
  }

  const tracked = execFileSync("git", diffArgs, {
    cwd: REPO_ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  })
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  if (base && head) {
    return tracked;
  }

  const untracked = execFileSync("git", ["ls-files", "--others", "--exclude-standard"], {
    cwd: REPO_ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"]
  })
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  return tracked.concat(untracked);
}

function resolveScope({ matrix, changedFiles }) {
  const allGroups = Object.keys(matrix.groups);
  const normalizedFiles = normalizeFiles(changedFiles);
  const uiChangedFiles = normalizedFiles.filter((file) => matchesAny(file, matrix.uiImpactPatterns));
  const fullTriggers = uiChangedFiles.filter((file) => matchesAny(file, matrix.fullSweepTriggers));

  if (fullTriggers.length > 0) {
    return {
      scope: "full",
      changedFiles: normalizedFiles,
      uiChangedFiles,
      groups: allGroups,
      matchedByGroup: Object.fromEntries(allGroups.map((group) => [group, matrix.groups[group].description])),
      reasons: fullTriggers.map((file) => `Full sweep trigger matched: ${file}`),
      reviewCommand: "npm --prefix e2e run test:ui-review:full"
    };
  }

  if (uiChangedFiles.length === 0) {
    return {
      scope: "none",
      changedFiles: normalizedFiles,
      uiChangedFiles,
      groups: [],
      matchedByGroup: {},
      reasons: ["No UI-impacting files matched the review matrix."],
      reviewCommand: null
    };
  }

  const groups = new Set(matrix.alwaysIncludeGroups || []);
  const matchedByGroup = {};
  const reasons = [];

  for (const [groupName, group] of Object.entries(matrix.groups)) {
    const matchedFile = uiChangedFiles.find((file) => matchesAny(file, group.patterns));
    if (!matchedFile) {
      continue;
    }

    groups.add(groupName);
    matchedByGroup[groupName] = matchedFile;
    reasons.push(`${groupName}: ${matchedFile}`);
  }

  if (groups.size === 0 && Array.isArray(matrix.defaultGroups)) {
    for (const group of matrix.defaultGroups) {
      groups.add(group);
    }
  }

  return {
    scope: "targeted",
    changedFiles: normalizedFiles,
    uiChangedFiles,
    groups: Array.from(groups),
    matchedByGroup,
    reasons: reasons.length > 0
      ? reasons
      : ["UI files changed, but no specific group matched. Default review groups were selected."],
    reviewCommand: "npm --prefix e2e run test:ui-review"
  };
}

function parseArgs(argv) {
  const options = {
    matrixPath: DEFAULT_MATRIX_PATH,
    files: []
  };

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    switch (arg) {
      case "--matrix":
        options.matrixPath = path.resolve(REPO_ROOT, argv[index + 1]);
        index += 1;
        break;
      case "--output":
        options.output = path.resolve(REPO_ROOT, argv[index + 1]);
        index += 1;
        break;
      case "--base":
        options.base = argv[index + 1];
        index += 1;
        break;
      case "--head":
        options.head = argv[index + 1];
        index += 1;
        break;
      case "--files":
        for (let fileIndex = index + 1; fileIndex < argv.length; fileIndex += 1) {
          if (argv[fileIndex].startsWith("--")) {
            break;
          }

          options.files.push(argv[fileIndex]);
          index = fileIndex;
        }
        break;
      default:
        break;
    }
  }

  return options;
}

function runCli() {
  const options = parseArgs(process.argv.slice(2));
  const matrix = loadMatrix(options.matrixPath);
  const changedFiles = options.files.length > 0
    ? options.files
    : getGitDiffFiles(options.base, options.head);
  const result = resolveScope({ matrix, changedFiles });
  const output = `${JSON.stringify(result, null, 2)}\n`;

  if (options.output) {
    fs.mkdirSync(path.dirname(options.output), { recursive: true });
    fs.writeFileSync(options.output, output, "utf8");
  }

  process.stdout.write(output);
}

if (require.main === module) {
  runCli();
}

module.exports = {
  DEFAULT_MATRIX_PATH,
  REPO_ROOT,
  getGitDiffFiles,
  loadMatrix,
  normalizeFiles,
  resolveScope
};
