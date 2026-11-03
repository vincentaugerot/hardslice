# Publishing a preview on NuGet.org

The three packages, `HardSlice.Analyzers`, `HardSlice.Tool` and `HardSlice.Templates`, are published together, at the single version of `Directory.Build.props`, by the workflow [`.github/workflows/release.yml`](../.github/workflows/release.yml). A tag `v<version>` triggers it, and it refuses any tag that does not name that version.

## What the workflow does

| Job | Role |
|---|---|
| `build` | Checks that the tag names the version, then restores, builds and tests the solution from the tag, integration tests included. |
| `template` | Runs `scripts/test-template.sh` again from the tag: local packages, solutions with and without Bookings, probes of every rule. |
| `pack` | After `build` and `template`: packs the three packages, checks that there are exactly three at the right version, runs the path of a user on them (`scripts/verify-published.sh --source artifacts/packages`), then uploads them as the `packages` artifact. |
| `publish` | The only job that holds a credential and changes NuGet.org. It runs in the protected environment `nuget`, so after the approval of the maintainer, trades the OIDC token of the workflow for a NuGet.org API key valid for one hour (trusted publishing), then runs `scripts/publish-packages.sh`. That script refuses to push anything unless the tag still points to the commit of the run, and unless every package NuGet.org already serves at that version has the content of the `packages` artifact. It then pushes the others, `HardSlice.Analyzers`, `HardSlice.Tool` and last `HardSlice.Templates`, which pins the other two. |
| `verify` | A fresh runner, which has never seen the packages: waits until NuGet.org serves them (90 minutes at most), checks that they have the content of the `packages` artifact of the run, then `dotnet new install HardSlice.Templates@<version>`, generation with and without Bookings, `dotnet tool restore` and `dotnet hardslice check` in each, then a module and a command slice added by `hardslice-module` and `hardslice-command`, completed, and `dotnet hardslice check` again. It checks that the template, the analyzers and the tool come from NuGet.org. Only the scripts it runs and `global.json` are read from the repository. |

A version pushed to NuGet.org can never be replaced, only unlisted. Everything that can fail therefore runs before `publish`. NuGet.org adds its repository signature, `.signature.p7s`, to every package it accepts: the package it serves never has the bytes of the one pushed, so the workflow compares their content, every other file of the package (`scripts/nupkg.py digest`).

## Prerequisites, once

1. **Public repository.** The NuGet.org pages link to the code, the rule sheets and the documentation of the repository `vincentaugerot/hardslice`.
2. **NuGet.org account** of the maintainer. The identifiers `HardSlice.Analyzers`, `HardSlice.Templates` and `HardSlice.Tool` are free. Request the reservation of the prefix `HardSlice.` (without it, `dotnet new` shows `Reserved: ✘`).
3. **Trusted publishing** on NuGet.org, account menu, "Trusted Publishing": a policy for the owner `vincentaugerot`, the repository `hardslice`, the workflow file `release.yml` and the environment `nuget`.
4. **GitHub environment `nuget`** (Settings, Environments):
   - required reviewer: the maintainer, with "Prevent self-review" turned off, since the maintainer pushes the tag.
   - deployment branches and tags: tags `v*` only.
   - secret `NUGET_USER`: the name of the NuGet.org profile (not the email address), read by `NuGet/login`.
5. **Private vulnerability reporting** turned on (Settings, Security), which [SECURITY.md](../SECURITY.md) points to.

No API key is stored. Without trusted publishing, a key limited to pushing `HardSlice.*`, in a secret of the environment `nuget`, would replace the `NuGet/login` step.

## Publish

1. Prepare the version in one commit on `main`, and check that CI is green on it:
   - `Directory.Build.props` holds the version to publish, a version never pushed to NuGet.org.
   - the package READMEs (`src/HardSlice.*/README.md`) link to the tag of that version, `blob/v<version>/`, except for the license, and the install line of `README.md` and of the README of `HardSlice.Templates` names it.
   - `CHANGELOG.md` has its section.
   - the rules of `src/HardSlice.Analyzers/AnalyzerReleases.Unshipped.md` move to `AnalyzerReleases.Shipped.md`, under the header of the version without its prerelease label, `## Release 0.3.0` for `0.3.0-preview.1` (the release tracking reads a `System.Version`), created at the end of the file or completed by a later preview, and `Unshipped.md` keeps its header only. Once a rule is shipped, the analyzer of the release tracking reports any later change of its category or default severity.

   `ReadmeTests` fails while the READMEs or `AnalyzerReleases.Shipped.md` do not name the version of `Directory.Build.props`. The commands below read it:

   ```text
   VERSION=$(dotnet msbuild src/HardSlice.Templates/HardSlice.Templates.csproj -getProperty:Version)
   ```

2. Check the packages locally, on the path of a user, with the full template scenario first:

   ```text
   scripts/test-template.sh
   for p in Analyzers Tool Templates; do dotnet pack src/HardSlice.$p/HardSlice.$p.csproj -c Release -o artifacts/packages; done
   scripts/verify-published.sh "$VERSION" "$(mktemp -d)" --source artifacts/packages --clean
   ```

3. Create and push the tag:

   ```text
   git tag -a "v$VERSION" -m "HardSlice $VERSION"
   git push origin "v$VERSION"
   ```

4. Approve the deployment in the environment `nuget` once `build`, `template` and `pack` are green.
5. Wait for `verify`: it proves that a user installs the template from NuGet.org and passes `dotnet hardslice check`.

## When a job fails

- **Before `publish`**: nothing is published. Fix it on `main`. If the run still waits for the approval of `publish`, reject its deployment first (Actions, the run, "Review deployments", "Reject"): an approval given later would publish the packages of the old commit, which `scripts/publish-packages.sh` refuses once the tag moves. Then delete the tag (`git push origin :refs/tags/v<version>`, `git tag -d v<version>`) and create it again on the fixed commit.
- **During `publish`, on a transient error** (network, NuGet.org unavailable, a package still being validated): rerun the job of the same run. It skips the packages NuGet.org already serves with the content of the run and pushes the others. A package pushed but not served yet is refused as a duplicate: rerun once NuGet.org serves it.
- **During `publish`, on a rejection that is not transient** (a package refused by the validation of NuGet.org) once a first package is pushed: the version is spent, since a pushed package is never replaced. Never move the tag to a fixed commit for the same version: `publish` refuses it. Fix the problem on `main`, unlist the packages already pushed, and publish the next preview (`-preview.2` after `-preview.1`) with a new tag.
- **`verify` waiting too long**: NuGet.org validates and indexes each package, which usually takes a few minutes and sometimes more. Rerun the job alone. The script resumes the wait.
- **`verify` failing after publication**: the packages are published and cannot be replaced. Unlist them on NuGet.org, fix the problem, and publish the next preview (`-preview.2` after `-preview.1`) with a new tag.

To check a version already published, from any machine with bash, curl and python3: `scripts/verify-published.sh <version> "$(mktemp -d)" --clean`. Without `--clean`, the folder, several gigabytes, stays.
