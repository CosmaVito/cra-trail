# cra-trail

Evidence packs for the EU Cyber Resilience Act, built in CI for .NET and Angular products.

A release pipeline already produces most of what the Cyber Resilience Act (Regulation (EU) 2024/2847) asks a manufacturer to keep: SBOMs, vulnerability and secret scans, VEX statements, a disclosure policy. cra-trail collects them, evaluates them against Annex I and writes one zip, the evidence pack. A gate reads the pack and decides whether the release ships.

**Status:** early, version `0.1.0-dev`, not published on NuGet yet. The Annex I mapping in `rules/` is my reading of the regulation, not legal advice: a release that passes the gate is not compliant because of it.

## Commands

| Command | What it does |
|---|---|
| `init` | Creates `cra.yml`, a `SECURITY.md` template and the `vex` folder. |
| `pack` | Collects SBOMs, scans and VEX, evaluates Annex I and writes an evidence pack. |
| `gate` | Decides whether the release passes and prints a Markdown summary, also to `GITHUB_STEP_SUMMARY` when it is set. |
| `evaluate` | Evaluates a pack again, for example with newer rules, and writes the result as a new file. |
| `verify` | Checks the hashes of a pack and, with `--bundle`, its Sigstore signature through `gh attestation verify`. |

Exit codes: `0` passed, `1` gate blocked, `2` input error, `3` verification failed.

## Quick start

Build and install the tool from source (.NET 10 SDK):

```bash
dotnet pack src/CraTrail.Cli -c Release -o ./nupkg
dotnet tool install CraTrail --tool-path ./.tools --add-source ./nupkg --version 0.1.0-dev
```

Then, in the repository of your product:

```bash
cra-trail init
cra-trail pack \
  --product-sbom sbom.cdx.json \
  --grype grype.json \
  --sast-sarif sast.sarif \
  --secrets-sarif secrets.sarif \
  --vex vex/openvex.json \
  --cvd-policy SECURITY.md \
  --out evidence-pack.zip
cra-trail gate evidence-pack.zip
cra-trail verify evidence-pack.zip
```

`cra-trail pack --help` lists the other inputs: more SBOMs, test results, NuGet and npm audits, the tools used, the signing method.

## What the gate blocks

With the default rules and the `cra.yml` created by `init`, a release is blocked when:

- a finding listed in the CISA KEV catalogue has no VEX statement;
- a finding at or above CVSS 9.0 has no VEX statement (the threshold is set in `cra.yml`);
- the secret scanner found something;
- a component in the SBOM misses its name, version or purl, or the SBOM misses its timestamp or dependency graph;
- `cra.yml` does not set the end of the support period or the contact for vulnerability reports.

The other Annex I points are evaluated and shown in the pack. Points that no tool can check are attested by a person in `cra.yml`, or marked not applicable with a reason; changes to `cra.yml` go through pull request review like code.

## Design

- **Rules as data** ([ADR 0001](docs/adr/0001-rules-as-data.md)). The Annex I crosswalk and the SBOM profiles are versioned YAML in `rules/`; the code knows five check types and nothing about single requirements. `--rules` runs your own mapping without a fork.
- **Offline and deterministic** ([ADR 0002](docs/adr/0002-offline-deterministic-cli.md)). The CLI makes no network calls. The same inputs and creation time give the same zip, byte for byte, so a pack can be rebuilt and compared months later.
- **CycloneDX 1.6** ([ADR 0003](docs/adr/0003-cyclonedx-1-6.md)) for every SBOM in a pack. SPDX is not supported in v0.1.
- **Gate on core SBOM fields** ([ADR 0004](docs/adr/0004-core-sbom-gate.md)). The BSI TR-03183-2 profile is always evaluated and reported, and blocks only when `gate.require-bsi` is `true`.
- **Conformance vectors** in [`conformance/`](conformance/README.md): evaluation and verification cases that any implementation of the evaluator must pass.

## Example product

[`demo/cvd-inbox`](demo/cvd-inbox/README.md) is a small self-hosted inbox for vulnerability reports, in ASP.NET Core and Angular. It is the product the evidence pack is built for in the examples. It is an example, not fit for production use.

## Not in the repository yet

- A browser viewer with a TypeScript evaluator that runs the same conformance vectors.
- `cra-trail sbom enrich`, to add the BSI TR-03183-2 fields that standard generators leave out.
- The reference GitHub Actions pipeline that builds, scans, signs and gates the example product.

## License

[MIT](LICENSE).
