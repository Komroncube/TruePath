// SPDX-FileCopyrightText: 2024 TruePath contributors <https://github.com/ForNeVeR/TruePath>
//
// SPDX-License-Identifier: MIT

#r "nuget: Generaptor.Library, 1.2.0"

open System
open Generaptor
open Generaptor.GitHubActions
open type Generaptor.GitHubActions.Commands
open type Generaptor.Library.Actions
open type Generaptor.Library.Patterns

let mainBranch = "main"
let ubuntu = "ubuntu-22.04"
let images = [
    "macos-12"
    ubuntu
    "windows-2022"
]

let workflows = [
    let mainTriggers = [
        onPushTo mainBranch
        onPullRequestTo mainBranch
        onSchedule(day = DayOfWeek.Saturday)
        onWorkflowDispatch
    ]

    workflow "main" [
        name "Main"
        yield! mainTriggers

        job "check" [
            checkout
            yield! dotNetBuildAndTest(projectFileExtensions = [".csproj"])
        ] |> addMatrix images

        job "licenses" [
            runsOn ubuntu
            checkout

            step(name = "REUSE license check", uses = "fsfe/reuse-action@v3")
        ]

        job "encoding" [
            runsOn ubuntu
            checkout

            step(name = "Verify encoding", shell = "pwsh", run = "scripts/Test-Encoding.ps1")
        ]

        job "nowarn-empty" [
            runsOn ubuntu
            checkout

            step(name = "Verify with NoWarn as empty", run = "dotnet build /p:NoWarn='' --no-incremental")
        ]
    ]

    workflow "release" [
        name "Release"
        yield! mainTriggers
        onPushTags "v*"
        job "nuget" [
            runsOn ubuntu
            checkout
            writeContentPermissions

            let configuration = "Release"

            let versionStepId = "version"
            let versionField = "${{ steps." + versionStepId + ".outputs.version }}"
            getVersionWithScript(stepId = versionStepId, scriptPath = "scripts/Get-Version.ps1")
            dotNetPack(version = versionField)

            let releaseNotes = "./release-notes.md"
            prepareChangelog(releaseNotes)

            let projectName = "TruePath"
            let packageId = "TruePath"
            let artifacts includeSNuPkg = [
                $"./{projectName}/bin/{configuration}/{packageId}.{versionField}.nupkg"
                if includeSNuPkg then $"./{projectName}/bin/{configuration}/{packageId}.{versionField}.snupkg"
            ]
            let allArtifacts = [
                yield! artifacts true
            ]
            uploadArtifacts [
                releaseNotes
                yield! allArtifacts
            ]
            yield! ifCalledOnTagPush [
                createRelease(
                    name = $"TruePath v{versionField}",
                    releaseNotesPath = releaseNotes,
                    files = allArtifacts
                )
                yield! pushToNuGetOrg "NUGET_TOKEN" (
                    artifacts false
                )
            ]
        ]
    ]
]

EntryPoint.Process fsi.CommandLineArgs workflows
