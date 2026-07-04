---
name: execute-modernization-plan
description: Execute the modernization plan by running the tasks listed in the plan
---

# Execute modernization plan

This skill is used to execute a modernization plan to migrate the a given project to Azure

## User Input

- modernization-description: The user intent to run the modernization plan
- modernization-work-folder (Mandatory): The folder to save the modernization plan
- programming-language: Input by user or autodetect by context
- baseline-commit-sha (Mandatory): The git commit SHA captured before task execution, used for diff-based analysis

You **MUST** consider the user input before proceeding.

## Workflow

Given that modernization description, do this:
1. Read ${modernization-work-folder}/plan.md, you can have an overview with the modernization plan

2. Load all tasks from ${modernization-work-folder}/.metadata/tasks.json and execute them one by one in the order they appear in the `tasks` array in tasks.json (do not reorder tasks):
    - Refer to the json schema tasks-schema.json to update the tasks.json
    - Before starting a task, update the tasks.json status to "started"
    - After completing a task, **YOU MUST** update the tasks.json status to exactly one of: "success", "failed", or "skipped" (do NOT use "completed" or any other value) with a task summary and task successCriteriaStatus
    - Do not stop task execution until all tasks are completed or any task fails. If one task is started, wait for final result with success, skipped or failed.
    - Choose the right custom agent to execute the task based on the `type` field of the task in tasks.json, and call the custom agent with the prompt according to the task type and information in tasks.json. The custom agent will return the execution result including whether the task is successful, skipped or failed, and a summary of the execution.
        1) Custom agent usage to complete the infrastructure task:
        For tasks with `"type": "infrastructure"` in tasks.json, call custom agent `modernize-azure-platform-engineer` with prompt:

            ```md
            Generate IaC files to and provision Azure infrastructure. Provision task details:
              - TaskId: {task id from `id` field}
              - Description: {task description from `description` field}
              - IacType: {iacType}
              - Provision: {provision}
              - modernization-work-folder: {modernization-work-folder}
            ```

        2) Custom agent usage to complete the coding task:
            1) For Java upgrade tasks (tasks with `"type": "upgrade"` that target Java version, Spring Boot, Spring Framework, or Jakarta EE upgrades), you must call custom agent general-purpose with below prompt:
                ```md
                Call skill execute-java-upgrade-task to do the Java upgrade
                Upgrade this Java project using your MCP-based workflow. Run in Automatic flow mode — do NOT pause for user input at any point.

                Upgrade task details:
                  - TaskId: {task id from `id` field}
                  - Description: {task description from `description` field}
                  - Requirements: {from `requirements` field}
                  - Environment Configuration: {from `environmentConfiguration` field, may be null}
                  - Success Criteria: {from `successCriteria` field}
                  - modernization-work-folder: {modernization-work-folder}

                Working folder rules (MANDATORY):
                  - Use ${modernization-work-folder} as your working directory for all bookkeeping artifacts (plan notes, progress logs, intermediate results).
                  - Do NOT create or write into any folder outside ${modernization-work-folder} for plan/progress artifacts (e.g., do NOT use `.github/java-upgrade/...` or any other ad-hoc location). Source-code edits in the repo are of course allowed.

                Reporting (MANDATORY, before returning):
                  1. Create the subfolder ${modernization-work-folder}/${TaskId}/ if it does not exist.
                  2. Write ${modernization-work-folder}/${TaskId}/modernization-summary.md describing:
                     - finalStatus: one of "success" | "failed" | "skipped"
                     - successCriteriaStatus: { passBuild, generateNewUnitTests, passUnitTests } as booleans
                     - summary: short prose summary of what changed
                     - failureReason: short prose, only when finalStatus is "failed"
                  3. Return the same finalStatus / successCriteriaStatus / summary as your final message so the caller can update tasks.json.

                CRITICAL: This is a fully autonomous execution. Never ask the user for confirmation, never pause for review, never present options. Accept all defaults and complete the entire upgrade end-to-end.
                ```

            2) For .NET upgrade tasks (tasks with `"type": "upgrade"` that target .NET version upgrades), you must call custom agent `modernize-dotnet-upgrade-engineer` with below prompt:
                ```md
                Upgrade this .NET project using your MCP-based workflow. Run in Automatic flow mode — do NOT pause for user input at any point.

                Upgrade task details:
                  - TaskId: {task id from `id` field}
                  - Description: {task description from `description` field}
                  - Requirements: {from `requirements` field}
                  - Environment Configuration: {from `environmentConfiguration` field, may be null}
                  - Success Criteria: {from `successCriteria` field}

                CRITICAL: This is a fully autonomous execution. Never ask the user for confirmation, never pause for review, never present options. Accept all defaults and complete the entire upgrade end-to-end.
                ```

            3) You must call custom agent general-purpose for transform task with below prompt according to information from tasks.json
                ```md
                Call skill execute-modernization-task to do the code change
                Here is the transform task details:
                - TaskId (from `id` field)
                - Description (from `description` field)
                - Requirements (from `requirements` field)
                - Migration Skills (The skill list from `skills` field used for migration if available, otherwise show `hint: <description of this task>`)
                - Environment Configuration (from `environmentConfiguration` field, may be null)
                - Success Criteria (from `successCriteria` field, includes: passBuild, generateNewUnitTests, passUnitTests)
                - Exit Criteria: Ensure all code logic, configurations, support files and tests are properly migrated. Ensure both build and tests pass. Ensure the modernization is consistent (all expected goals are correctly implemented) and complete (all old technology references are fully removed or replaced).
                - modernization-work-folder: The folder to save the modernization plan from input
                ```

            4) Only use the skill execute-modernization-task in custom agent to do the code change for each task

        5. Custom agent usage to complete the security task:
            You must call custom agent general-purpose for security task with below prompt according to information from tasks.json
                ```md
                Call skill {{security-skill-for-the-task}} to do the security check and fix
                Here is the security task details:
                    - TaskId (from `id` field)
                    - Description (from `description` field)
                    - Requirements (from `requirements` field)
                    - Environment Configuration (from `environmentConfiguration` field, may be null)
                    - Success Criteria (from `successCriteria` field, includes: passBuild, generateNewUnitTests, passUnitTests)
                    - modernization-work-folder: The folder to save the cve check report and fix summary      
                ```
        {{security-skill-for-the-task}} is resolved from the `skills` array in the security task in tasks.json. Each entry in `skills` is an object with `name` and `location` fields. If the task has multiple skills, combine all skill names into a single comma-separated list (e.g., `validate-cves-and-fix, additional-security-scan`). If there is only one skill, use its `name` value directly (e.g., `validate-cves-and-fix`).

        6. Custom agent usage to complete the integration test task:
        For tasks with `"type": "integrationTest"` in tasks.json, call custom agent `general-purpose` with prompt:
                ```md
                Call skill integration-tests to generate and run integration tests for the migrated project
                Here is the integration test task details:
                    - TaskId (from `id` field)
                    - Description (from `description` field)
                    - Requirements (from `requirements` field)
                    - Test Layers (from `layers` field, e.g., [1, 2] for Layer 1 and Layer 2)
                    - modernization-work-folder: The folder to save the modernization plan from input

                The integration-tests skill should:
                - For each layer in the layers array, run the integration-tests skill with that layer parameter
                - Layer 1: Generate Local Integration Tests with TestContainers for all Azure services
                - Layer 2: Generate Smoke Tests for basic application health checks
                - Ensure all tests pass before marking the task as successful
                ```
        7. Custom agent usage to complete containerization or deploy task:
        For tasks with `"type": "deployment"` in tasks.json, call custom agent `modernize-azure-devops-engineer` with prompt:
                ```md
                Deploy the application to Azure
                Here is the deploy task details:
                - TaskId (from `id` field)
                - targetAzureService (from `targetAzureService` field)
                - deploymentTool (from `deploymentTool` field)
                - modernization-work-folder: The folder to save the modernization plan from input
                ```
            or deploy to existing azure resources with below format if the plan.md contains the section of Azure Environment with Subscription ID and Resource Group:
                ```md
                Deploy the application to existing Azure resources. 
                Here is the deploy task details:
                - TaskId (from `id` field)
                - Subscription ID: {subscriptionId}
                - Resource Group: {resourceGroup}
                - modernization-work-folder: The folder to save the modernization plan from input
                ```
        For tasks with `"type": "containerization"` in tasks.json, call custom agent `modernize-azure-devops-engineer` with prompt:
                ```md
                Containerize the application using Docker
                Here is the containerization task details:
                - TaskId (from `id` field)
                - dockerfilePath (from `dockerfilePath` field)
                - modernization-work-folder: The folder to save the modernization plan from input
                ```
    - You needn't generate any other documents except the "modernization-summary.md" for each task
    - **YOU MUST** update the tasks.json (at ${modernization-work-folder}/.metadata/tasks.json) with the final status of each task (success, failed, or skipped)
    - Make a commit when all tasks are completed with the changes made in the modernization plan.            ```

7. Final verification before completing the plan:
   After all tasks have been executed, perform an overall verification:
   - **Consistency**: All expected modernization goals across all tasks are correctly and completely implemented
   - **Completeness**: All old technology references are fully removed or replaced — no partial remnants remain in source files, configuration files, build files, or test files
   - If any gap is found, re-execute the relevant task to address it before finalizing

8. **MANDATORY: Rulebook Evidence Validation** (only when rulebook attachments are present):
   **You MUST complete this step if rulebook attachments were provided.** Do NOT skip it.
   After final verification, handle rulebook compliance evidence as a best-effort step that must not fail the overall modernization plan execution:
   - If `baseline-commit-sha` is `unknown`, do not call skill `validate-rulebook-evidence`. Instead, write `${modernization-work-folder}/rulebook-evidence.md` with a short report explaining that diff-based rulebook evidence could not be generated because the baseline commit SHA was unavailable.
   - If rulebook attachments are present and `baseline-commit-sha` is not `unknown`, call skill `validate-rulebook-evidence` to analyze code changes and produce rulebook compliance evidence:
     - baseline-commit-sha: the baseline commit SHA captured before task execution
     - rulebook-file-list: comma-separated list of rulebook file names from the attachments
     - evidence-output-path: `${modernization-work-folder}/rulebook-evidence.md`
   - If the evidence validation skill fails or cannot produce evidence, write a short failure report to `${modernization-work-folder}/rulebook-evidence.md` and continue completing the modernization plan without failing the run.