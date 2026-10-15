# Conformance vectors

Each folder in `cases/` is one evaluation scenario. An implementation of the cra-trail evaluator passes when, for every case, it produces the content of `expected.json` from the files listed in `pack.json` and the rules in `rules/`.

- `pack.json` lists the pack files: path inside the pack, evidence kind, and source file relative to the case folder. Build the manifest from it with `schemaVersion` 0.1, generator `cra-trail` / `conformance`, `source: null`, `tools: []`, and the `createdAt` and `signing` values given.
- `expected.json` is the evaluation result. Compare it as JSON, not as text.
- `rules/` holds rules made for these tests. They exercise the evaluator, not the Annex I mapping shipped in `rules/` at the repository root.

The .NET implementation runs these cases in `tests/CraTrail.Tests/Conformance`. The browser viewer runs the same files.
