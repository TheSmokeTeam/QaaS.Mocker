## Summary

Describe the change in one or two sentences. Focus on the behavior or maintenance outcome rather than the implementation detail alone.

## Why

Explain the problem this PR solves, the regression it prevents, or the reason the dependency/workflow change is required.

## Changes

- List the main code or configuration updates.
- Call out any dependency version changes explicitly.
- Mention any CI or workflow behavior that changed.

## Validation

- [ ] `dotnet restore QaaS.Mocker.sln --packages .\RestoredPackages.CI`
- [ ] `dotnet build QaaS.Mocker.sln --configuration Release --no-restore`
- [ ] `dotnet test QaaS.Mocker.sln --configuration Release --no-restore --maxcpucount`

## Risks

Note any rollout risk, compatibility concern, or follow-up that reviewers should watch for.
