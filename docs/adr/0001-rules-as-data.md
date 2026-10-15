# 0001 Rules as data

Status: accepted (2026-10-15)

## Context

The Annex I mapping will change. No harmonised standard has been cited yet, the Commission may still adopt an SBOM format, and people who adopt cra-trail will want to read and question the mapping. The viewer also has to evaluate packs in the browser, so there are two evaluators: one in C#, one in TypeScript.

## Decision

The Annex I crosswalk and the SBOM field profiles live in versioned YAML under `rules/`. The code knows five check types (`file-present`, `json-field-required`, `scan-threshold`, `vex-covers`, `attested-field`) and nothing about individual requirements. Both evaluators run the vectors in `conformance/`, and CI fails when they disagree.

The other option was to write the rules in code. That means the same logic twice, kept in step by hand.

## Consequences

A new requirement or a different threshold is a reviewed YAML change, and the rules version is written into every pack. A new kind of check is a code change in both evaluators plus new vectors. `--rules` lets a team run its own mapping without forking the tool.
