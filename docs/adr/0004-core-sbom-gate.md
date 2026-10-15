# 0004 Gate on core SBOM fields, BSI check informative

Status: accepted (2026-10-15)

## Context

BSI TR-03183-2 v2.1.0 is the most detailed SBOM guidance available, and the first design blocked releases on its required fields. Standard generators do not produce several of them: the `bsi:component:*` properties, the file name, the SHA-512 hash of the distributed file and `concluded` licences. With that gate every project would fail on the first run. The CRA asks for a machine-readable SBOM that covers at least the top-level dependencies.

## Decision

The gate uses the `sbom-core` profile: a timestamp, the name, version and purl of every component, and the dependency graph. The BSI profile is always evaluated and shown in the viewer with its missing fields, and it is not linked to an Annex I point. It blocks only when `cra.yml` sets `gate.require-bsi: true`.

## Consequences

Teams can adopt the gate with the SBOMs their tools already make, and still see how far they are from the BSI profile. Closing that gap needs an enrichment step, which is on the roadmap as `cra-trail sbom enrich`.
