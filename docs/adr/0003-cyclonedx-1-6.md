# 0003 CycloneDX 1.6 as the common SBOM format

Status: accepted (2026-10-15)

## Context

The .NET and Angular preset builds SBOMs with several generators: cyclonedx-dotnet for the backend, cyclonedx-esbuild or cyclonedx-npm for the frontend, Syft for the container image. They are merged into one product SBOM, so they need a common version. cyclonedx-npm goes up to spec 1.6, and BSI TR-03183-2 v2.1.0 asks for CycloneDX 1.6 or later.

## Decision

Every SBOM in a pack is CycloneDX 1.6 JSON. The evaluator uses no CycloneDX library: it reads the JSON tree with a small path syntax that the TypeScript evaluator shares, so both read a document the same way.

SPDX is not supported in v0.1.

## Consequences

Profiles are plain JSON paths and work on any CycloneDX document, including SBOMs from other ecosystems. A team that only has SPDX has to convert first. Moving to CycloneDX 1.7 later means checking the generators and the profiles; the evaluator stays as it is.
