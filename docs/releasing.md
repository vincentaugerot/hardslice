# Publishing a preview on NuGet.org

The three packages, `HardSlice.Analyzers`, `HardSlice.Tool` and `HardSlice.Templates`, are published together, at the single version of `Directory.Build.props`, by the workflow [`.github/workflows/release.yml`](../.github/workflows/release.yml). A tag `v<version>` triggers it, and it refuses any tag that does not name that version.

## What the workflow does

| Job | Role |
|---|---|
| `build` | Checks that the tag names the version, then restores, builds and tests the solution from the tag, integration tests included. |
| `template` | Runs `scripts/test-template.sh` again from the tag: local packages, solutions with and without Bookings, probes of every rule. |
| `pack` | After `build` and `template`: packs the three packages, checks that there are exactly three at the right version, runs the path of a user on them (`scripts/verify-published.sh --source artifacts/packages`), then uploads them as the `packages` artifact. |
| `publish` | The only job that holds a credential and changes NuGet.org. It runs in the protected environment `nuget`, so after the approval of the maintainer, trades the OIDC token of the workflow for a NuGet.org API key valid for one hour (trusted publishing), then pushes `HardSlice.Analyzers`, `HardSlice.Tool` and last `HardSlice.Templates`, which pins the other two. |
| `verify` | A fresh runner, which has never seen the packages: waits until NuGet.org serves them (90 minutes at most), then `dotnet new install HardSlice.Templates@<version>`, generation with and without Bookings, `dotnet tool restore` and `dotnet hardslice check` in each. It checks that the template, the analyzers and the tool come from NuGet.org. Only `scripts/verify-published.sh` and `global.json` are read from the repository. |

A version pushed to NuGet.org can never be replaced, only unlisted. Everything that can fail therefore runs before `publish`.

## Prerequisites, once

1. **Public repository.** The NuGet.org pages link to the code, the rule sheets and the documentation of the repository `vincentaugerot/hardslice`.
2. **NuGet.org account** of the maintainer. The identifiers `HardSlice.Analyzers`, `HardSlice.Templates` and `HardSlice.Tool` are free; request the reservation of the prefix `HardSlice.` (without it, `dotnet new` shows `Reserved: ✘`).
3. **Trusted publishing** on NuGet.org, account menu, "Trusted Publishing": a policy for the owner `vincentaugerot`, the repository `hardslice`, the workflow file `release.yml` and the environment `nuget`.
4. **GitHub environment `nuget`** (Settings, Environments):
   - required reviewer: the maintainer, with "Prevent self-review" turned off, since the maintainer pushes the tag;
   - deployment branches and tags: tags `v*` only;
   - secret `NUGET_USER`: the name of the NuGet.org profile (not the email address), read by `NuGet/login`.
5. **Private vulnerability reporting** turned on (Settings, Security), which [SECURITY.md](../SECURITY.md) points to.

No API key is stored. Without trusted publishing, a key limited to pushing `HardSlice.*`, in a secret of the environment `nuget`, would replace the `NuGet/login` step.

## Publish

1. Check that CI is green on the commit to publish and that `Directory.Build.props` holds the intended version. The commands below read it:

   ```text
   VERSION=$(dotnet msbuild src/HardSlice.Templates/HardSlice.Templates.csproj -getProperty:Version)
   ```

2. Check the packages locally, on the path of a user:

   ```text
   for p in Analyzers Tool Templates; do dotnet pack src/HardSlice.$p/HardSlice.$p.csproj -c Release -o artifacts/packages; done
   scripts/verify-published.sh "$VERSION" "$(mktemp -d)" --source artifacts/packages
   ```

3. Create and push the tag:

   ```text
   git tag -a "v$VERSION" -m "HardSlice $VERSION"
   git push origin "v$VERSION"
   ```

4. Approve the deployment in the environment `nuget` once `build`, `template` and `pack` are green.
5. Wait for `verify`: it proves that a user installs the template from NuGet.org and passes `dotnet hardslice check`.

## When a job fails

- **Before `publish`**: nothing is published. Fix it on `main`, delete the tag (`git push origin :refs/tags/v<version>`, `git tag -d v<version>`) and create it again on the fixed commit.
- **During `publish`**: rerun the job. `--skip-duplicate` skips the versions already pushed and pushes the others.
- **`verify` waiting too long**: NuGet.org validates and indexes each package, which usually takes a few minutes and sometimes more. Rerun the job alone; the script resumes the wait.
- **`verify` failing after publication**: the packages are published and cannot be replaced. Unlist them on NuGet.org, fix the problem, and publish the next preview (`-preview.2` after `-preview.1`) with a new tag.

To check a version already published, from any machine with bash 4 or later: `scripts/verify-published.sh <version> "$(mktemp -d)"`.
