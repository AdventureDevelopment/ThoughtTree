# ADR-048: Hardened image and supply chain

id: ADR-048 · status: Accepted

applies_to: `src/ThoughtTree.Server*/**`, `.github/**`

- **MUST** build the image with `dotnet publish` (SDK container support) on Microsoft's chiseled .NET base image, running as non-root
- **MUST** build for amd64 and arm64
- **MUST** publish to GitHub Container Registry with version tags (`1.4.2`, `1.4`)
- **MUST** scan the image for vulnerabilities in CI and rebuild when the base image gets security updates
- **MUST** sign the image and attach a software bill of materials (GitHub artifact attestations)

verify:

```bash
gh attestation verify oci://ghcr.io/adventuredevelopment/thoughttree:<version> --owner adventuredevelopment
```

why: Self-hosters run the image on their own machines; they must be able to trust where it comes from.
