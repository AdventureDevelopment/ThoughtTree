# ADR-049: Apache 2.0, public from the first release

id: ADR-049 · status: Accepted · kind: constraint

applies_to: `**`

- **MUST** license the whole repository under Apache 2.0 (`LICENSE` in the root, unchanged text)
- **MUST** make the repository public at the first release, after checking the git history for secrets
- **MUST NOT** commit secrets, credentials or personal data to the repository
- **MUST NOT** add code under a licence incompatible with Apache 2.0

verify:

```bash
sha256sum LICENSE   # expect cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30
```

why: Readable source makes the privacy promise checkable; others may use and offer it, but not under the name.
