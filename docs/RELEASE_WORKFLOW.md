# Release workflow documentation

The authoritative branching and release contract is documented in
[`ci/branching-and-releases.md`](ci/branching-and-releases.md).

The contract covers `release/X.Y` stabilization lines, dated development prereleases for testers,
tag-driven releases, immutable released versions, and milestone metadata that is not moved during
promotion.

Stabilization promotion requires no open issues labelled for any tagged version on the release
line, a staged release that is at least `PROMOTION_AGE_DAYS` days old, and no active
`promotion: freeze` issue for the version. The `force-promote` dispatch input overrides all
eligibility conditions.
