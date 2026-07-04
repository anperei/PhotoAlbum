---
name: containerization
description: Setup Dockerfiles for the project to run inside of containers for Azure Container Apps or Azure Kubernetes Service.
---

# Containerization

## Overview

This skill provides guidance to setup Dockerfiles for the project to run inside of containers for Azure Container Apps or Azure Kubernetes Service. Do not set up docker-compose for running locally as it is not required.

## Output file structure:

Create a subfolder ${taskid} under ${modernization-work-folder}. Generate files strictly in the following structure. DO NOT omit or add files or folders, and use exact file names for tracking:

```
/${modernization-work-folder}/${taskid}/
├── plan.md                 # Containerization plan with architecture, execution steps, and tracking
├── summary.md   # Summary of containerization plan for quick reference
├── containerization-scripts/         # scripts for containerization
```

**IMPORTANT - Structural Rules (DO NOT include this section in output)**
- The plan MUST strictly followed the sections listed below, in the EXACT order.
- You MUST generate the plan file strictly following the pattern first, then execute the plan. Do NOT execute any deployment scripts before the plan file is generated.

## Workflow

{Agent should fill in and polish the markdown template below to generate a containerization plan for the project. Then save it to '.azure/containerization-plan.copilotmd' file. Don't add extra validation steps! Don't change the tool name!}

# Azure Containerization Plan for TestProject Project
## **Goal**
Setup Dockerfiles for the project to run inside of containers for Azure Container Apps or Azure Kubernetes Service. Do not set up docker-compose for running locally as it is not required.

## **List of services to be containerized**
{For each service listed, provide a simple description such as language, entrypoint, dependencies, etc.}
- project1 (/path/to/fake/workspace/project1)
- project2 (/path/to/fake/workspace/project2)
- project3 (/path/to/fake/workspace/project3)
-  (/path/to/fake/workspace)
-  (/path/to/fake/workspace)

## **Execution Steps**
> **Below are the steps for Copilot to follow; ask Copilot to update or execute this plan.**
Steps to containerize the project:
1. Check containerization pre-requisites:
    1. Ensure Docker is installed and running.
2. Scan the repository using tool 'appmod-analyze-repository'.
3. Check code is ready to run in a local container:
    1. Ensure the application reads configuration from environment variables. Avoid hardcoding configuration values.
    2. Review the application's dependencies that must be hosted in the cloud, and list these for the user.
4. Create a Dockerfile for each project:
    1. Use tool 'appmod-plan-generate-dockerfile' to generate the Dockerfile.
5. Build the Docker images for each project:
    1. Build the docker images using tool 'appmod-build-docker-image' (do not call 'docker run').
    2. Scan the docker images using tool 'appmod-scan-docker-image' with scanType 'vulnerability' for vulnerabilities. Fix any critical findings.
    3. Tag the images with the proper version.
6. Summarize the successful containerization:
    1. List out the dockerfiles created for each project.
    2. Describe to the user the code edits required to successfully containerize the project.
    3. Additionally, ask if the user would like to generate Kubernetes manifests. If yes, please use tool 'appmod-generate-k8s-manifest' to generate Kubernetes manifests for the project.
