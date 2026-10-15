# 0002 Offline, deterministic CLI

Status: accepted (2026-10-15)

## Context

The evidence pack is signed in CI and may be checked months later. If the tool fetched data while building the pack, the same commit could give a different pack on a different day, and nobody could rebuild it to compare.

## Decision

The CLI makes no network calls. KEV and EPSS data come from the Grype report, which already carries them. The same inputs and the same creation time give the same zip, byte for byte: entries are written in ordinal order with a fixed date, JSON is always written the same way, and the creation time comes from `--created-at`, then `SOURCE_DATE_EPOCH`, then the clock.

## Consequences

The vulnerability data is as fresh as the Grype database used in CI; the Grype report inside the pack records when that database was built. Tests can compare packs byte for byte. Signature checks go through `gh attestation verify`, which does use the network, and `verify` says so when it cannot run it.
